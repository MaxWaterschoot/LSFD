using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Firefighting;
using LSFD.Profiles;
using LSFD.Safety;
using LSFD.Stations;
using LSFD.VehicleRescue;
using LSFD.Water;
using RAGENativeUI;
using RAGENativeUI.Elements;

namespace LSFD.UI
{
    /// <summary>The menus at fire stations and at the compartments of an apparatus. Everything else lives on the <see cref="Tablet"/>.</summary>
    internal class MenuManager
    {
        private readonly DutyManager duty;
        private readonly ApparatusManager apparatus;
        private readonly HoseManager hose;
        private readonly HydrantManager hydrants;
        private readonly ToolManager tools;
        private readonly BreathingApparatus scba;
        private readonly ThermalCamera thermalCamera;
        private readonly Ventilation ventilation;

        private readonly MenuPool pool = new MenuPool();

        private readonly UIMenu stationMenu = Theme.CreateMenu(L.T("Fire station"));
        private readonly UIMenu requestApparatusMenu = Theme.CreateMenu(L.T("Request apparatus"));
        private readonly UIMenu lockerMenu = Theme.CreateMenu(L.T("Locker room"));
        private readonly ProfileManager profiles;
        private readonly UIMenu compartmentMenu = Theme.CreateMenu("Compartments");

        // Menu items are rebuilt every time a menu opens; these map them back to what they do.
        private readonly Dictionary<UIMenuItem, Action> actions = new Dictionary<UIMenuItem, Action>();

        private FireStation currentStation;

        public MenuManager(DutyManager duty, ApparatusManager apparatus, HoseManager hose, HydrantManager hydrants, ToolManager tools,
            BreathingApparatus scba, ThermalCamera thermalCamera, Ventilation ventilation, ProfileManager profiles)
        {
            this.profiles = profiles;
            this.ventilation = ventilation;
            this.scba = scba;
            this.thermalCamera = thermalCamera;
            this.duty = duty;
            this.apparatus = apparatus;
            this.hose = hose;
            this.hydrants = hydrants;
            this.tools = tools;

            foreach (UIMenu menu in new[] { stationMenu, requestApparatusMenu, compartmentMenu, lockerMenu })
            {
                // Checkboxes are handled by OnCheckboxChange only, so their action doesn't run twice.
                menu.OnItemSelect += (sender, item, index) =>
                {
                    if (!(item is UIMenuCheckboxItem))
                        Run(item);
                };
                menu.OnCheckboxChange += (sender, item, isChecked) => Run(item);
                pool.Add(menu);
            }
        }

        public bool IsAnyMenuOpen => pool.IsAnyMenuOpen();

        public void Process()
        {
            pool.ProcessMenus();
        }

        public void CloseAll()
        {
            pool.CloseAllMenus();
        }

        public void OpenStationMenu(FireStation station)
        {
            currentStation = station;
            Rebuild(stationMenu, station.Name);

            stationMenu.AddItem(new UIMenuItem(station.Name, station.Data.Address)
            {
                Skipped = true,
                RightLabel = station.Data.Area.ToDisplayName(),
            });

            if (duty.IsOnDuty)
            {
                Add(stationMenu, new UIMenuItem(L.T("Go off duty"), L.T("Clock out and change back into your own clothes.")), () =>
                {
                    pool.CloseAllMenus();
                    duty.GoOffDuty();
                });
            }
            else
            {
                Add(stationMenu, new UIMenuItem(L.T("Go on duty"), L.T("Suit up in the locker room and clock in at this station.")), () =>
                {
                    pool.CloseAllMenus();
                    duty.GoOnDuty(currentStation);
                });
            }

            UIMenuItem requestItem = Add(stationMenu, new UIMenuItem(L.T("Request apparatus"), L.T("Have a vehicle and its crew brought out in front of the station.")), null);
            requestItem.Enabled = duty.IsOnDuty;
            stationMenu.BindMenuToItem(requestApparatusMenu, requestItem);
            BuildRequestApparatusMenu();

            UIMenuItem lockerItem = Add(stationMenu, new UIMenuItem(L.T("Locker room"), L.T("Choose the outfit you wear on duty (EUP outfits too).")), null);
            stationMenu.BindMenuToItem(lockerMenu, lockerItem);
            BuildLockerMenu();

            Add(stationMenu, new UIMenuItem(L.T("Restock personal gear"), L.T("A full SCBA cylinder, your health and a flashlight.")), () =>
            {
                duty.RestockPersonalGear();
                Notify.Info(L.T("Personal gear restocked."));
            }).Enabled = duty.IsOnDuty;

            stationMenu.RefreshIndex();
            stationMenu.Visible = true;
        }

        public void OpenCompartmentMenu(FireApparatus source)
        {
            Rebuild(compartmentMenu, source.Definition.Name);
            ApparatusDefinition definition = source.Definition;

            if (definition.HasPump)
            {
                compartmentMenu.AddItem(new UIMenuItem(L.T("Water tank"), L.T("Connect a supply line to a hydrant to refill the tank."))
                {
                    Skipped = true,
                    RightLabel = source.IsSupplied ? L.T("~b~On hydrant") : $"{source.Water:N0} / {definition.WaterCapacity:N0} L",
                });
            }

            if (definition.FoamCapacity > 0f)
            {
                compartmentMenu.AddItem(new UIMenuItem(L.T("Foam tank"), L.F($"Switch the nozzle to foam with {Settings.FoamKey} for fuel and vehicle fires."))
                {
                    Skipped = true,
                    RightLabel = $"{source.Foam:N0} / {definition.FoamCapacity:N0} L",
                });
            }

            AddHoseItems(source);
            AddSafetyItems(source);
            AddHandToolItems(source);
            AddWeaponToolItems(source);

            compartmentMenu.RefreshIndex();
            compartmentMenu.Visible = true;
        }

        private void AddHoseItems(FireApparatus source)
        {
            ApparatusDefinition definition = source.Definition;

            if (definition.Carries(EquipmentType.AttackHose))
            {
                bool holdingThis = hose.Source == source;
                Add(compartmentMenu, new UIMenuItem(holdingThis ? L.T("Stow attack hose") : L.T("Pull attack hose"),
                    L.F($"{definition.HoseLength:0} m line with an adjustable straight/fog nozzle.")), () =>
                {
                    if (holdingThis)
                    {
                        hose.StowHose();
                    }
                    else
                    {
                        tools.StowTool();
                        hose.TakeHose(source);
                    }
                    pool.CloseAllMenus();
                }).Enabled = definition.HasPump;
            }

            if (definition.Carries(EquipmentType.SupplyLine))
            {
                string text = source.IsSupplied ? L.T("Disconnect supply line") : hydrants.CarryingFrom == source ? L.T("Return supply line") : L.T("Take supply line");
                Add(compartmentMenu, new UIMenuItem(text, L.F($"{definition.SupplyLineLength:0} m line to connect this apparatus to a fire hydrant.")), () =>
                {
                    if (source.IsSupplied)
                        hydrants.Disconnect(source);
                    else if (hydrants.CarryingFrom == source)
                        hydrants.ReturnSupplyLine();
                    else
                        hydrants.TakeSupplyLine(source);
                    pool.CloseAllMenus();
                });
            }
        }

        private void AddSafetyItems(FireApparatus source)
        {
            ApparatusDefinition definition = source.Definition;

            if (definition.Carries(EquipmentType.SCBA))
            {
                var item = new UIMenuItem(L.T("Swap SCBA cylinder"), L.T("Replace the cylinder on your back with a full one."))
                {
                    RightLabel = $"{(scba.IsLowOnAir ? "~r~" : "")}{scba.AirFraction:P0}",
                };
                Add(compartmentMenu, item, () =>
                {
                    scba.Refill();
                    Notify.Info(L.T("Full ~b~SCBA cylinder~s~ on your back."));
                    pool.CloseAllMenus();
                });
            }

            if (definition.Carries(EquipmentType.PPVFan))
            {
                bool taken = ventilation.Source == source;
                Add(compartmentMenu, new UIMenuItem(taken ? L.T("Put PPV fan back") : L.T("Take PPV fan"),
                    L.T("Blows the smoke out of a building: set it up in the doorway, facing in. The fresh air also feeds the fire.")), () =>
                {
                    if (taken)
                        ventilation.Stow();
                    else
                        ventilation.Take(source);
                    pool.CloseAllMenus();
                });
            }

            if (definition.Carries(EquipmentType.ThermalCamera))
            {
                bool carryingThis = thermalCamera.Source == source;
                var item = new UIMenuCheckboxItem(EquipmentType.ThermalCamera.ToDisplayName(), carryingThis,
                    L.F($"Shows victims, flames and hotspots through the smoke. Turn it on with {Settings.ThermalCameraKey}."));
                Add(compartmentMenu, item, () =>
                {
                    if (item.Checked)
                        thermalCamera.Take(source);
                    else
                        thermalCamera.Stow();
                });
            }
        }

        private void AddHandToolItems(FireApparatus source)
        {
            var handTools = source.Definition.Equipment.Where(e => e.IsHandTool()).ToList();
            if (handTools.Count == 0)
                return;

            compartmentMenu.AddItem(Theme.CreateHeader(L.T("Rescue tools")));
            foreach (EquipmentType tool in handTools)
            {
                bool holding = tools.IsHolding(tool) && tools.HeldFrom == source;
                var item = new UIMenuItem(tool.ToDisplayName(), holding ? L.T("Put it back in the compartment.") : L.T("Carry it in your hands."))
                {
                    RightLabel = holding ? L.T("~g~In hand") : "",
                };
                Add(compartmentMenu, item, () =>
                {
                    if (holding)
                    {
                        tools.StowTool();
                    }
                    else
                    {
                        hose.StowHose();
                        tools.TakeTool(source, tool);
                    }
                    pool.CloseAllMenus();
                });
            }
        }

        private void AddWeaponToolItems(FireApparatus source)
        {
            var weaponTools = source.Definition.Equipment.Where(e => e.GetWeaponName() != null).ToList();
            if (weaponTools.Count == 0)
                return;

            compartmentMenu.AddItem(Theme.CreateHeader(L.T("Hand tools")));
            foreach (EquipmentType tool in weaponTools)
            {
                var item = new UIMenuCheckboxItem(tool.ToDisplayName(), source.IsTaken(tool), L.T("Take it from or put it back in the compartment."));
                Add(compartmentMenu, item, () =>
                {
                    if (item.Checked)
                        apparatus.TakeEquipment(source, tool);
                    else
                        apparatus.StowEquipment(source, tool);
                });
            }
        }

        private void BuildLockerMenu()
        {
            Rebuild(lockerMenu, L.T("Locker room"));
            Outfit selected = profiles.SelectedOutfit;

            Add(lockerMenu, new UIMenuItem(L.T("Standard turnout gear"), L.T("The default LSFD firefighter."))
            {
                RightLabel = selected == null ? L.T("~g~Wearing") : "",
            }, () => ChangeOutfit(null));

            if (profiles.Outfits.Outfits.Count > 0)
                lockerMenu.AddItem(Theme.CreateHeader("Outfits.xml"));
            foreach (Outfit outfit in profiles.Outfits.Outfits)
            {
                Outfit choice = outfit;
                Add(lockerMenu, new UIMenuItem(outfit.Name, L.F($"Model {outfit.Model}. Rename or edit it in Plugins/LSFD/Profiles/Outfits.xml."))
                {
                    RightLabel = selected == outfit ? L.T("~g~Wearing") : "",
                }, () => ChangeOutfit(choice));
            }

            lockerMenu.AddItem(Theme.CreateHeader("EUP"));
            Add(lockerMenu, new UIMenuItem(L.T("Save what I'm wearing"), L.T("Dress up with the EUP menu (or any outfit tool) first, then save it here as your duty outfit.")), () =>
            {
                Outfit saved = profiles.SaveCurrentOutfit();
                Notify.Info(L.F($"Saved as ~y~{saved.Name}~s~ and selected. Rename it in Profiles/Outfits.xml."));
                pool.CloseAllMenus();
            });

            lockerMenu.RefreshIndex();
        }

        private void ChangeOutfit(Outfit outfit)
        {
            pool.CloseAllMenus();
            duty.ChangeOutfit(outfit);
            Notify.Info(outfit == null ? L.T("Wearing the ~y~standard turnout gear~s~.") : L.F($"Wearing ~y~{outfit.Name}~s~."));
        }

        private void BuildRequestApparatusMenu()
        {
            Rebuild(requestApparatusMenu, L.T("Request apparatus"));

            foreach (var group in apparatus.Repository.Spawnable.GroupBy(d => d.HasPump))
            {
                requestApparatusMenu.AddItem(Theme.CreateHeader(group.Key ? L.T("Fire apparatus") : L.T("Support units")));
                foreach (ApparatusDefinition definition in group)
                {
                    string tank = definition.HasPump ? L.F($"{definition.WaterCapacity:N0} L tank, ") : "";
                    string crew = definition.MaxCrew > 0 ? L.F($"crew of {definition.MinCrew}-{definition.MaxCrew}, ") : "";
                    string equipment = string.Join(", ", definition.Equipment.Select(e => e.ToDisplayName()));
                    var item = new UIMenuItem(definition.Name, L.F($"{tank}{crew}carries: {equipment}."))
                    {
                        RightLabel = definition.Type.ToString(),
                        LeftBadge = UIMenuItem.BadgeStyle.Car,
                    };
                    Add(requestApparatusMenu, item, () =>
                    {
                        pool.CloseAllMenus();
                        apparatus.Spawn(definition, currentStation);
                    });
                }
            }

            requestApparatusMenu.RefreshIndex();
        }

        private void Rebuild(UIMenu menu, string subtitle)
        {
            foreach (UIMenuItem item in menu.MenuItems)
                actions.Remove(item);
            menu.Clear();
            menu.SubtitleText = subtitle.ToUpperInvariant();
        }

        private UIMenuItem Add(UIMenu menu, UIMenuItem item, Action action)
        {
            menu.AddItem(item);
            if (action != null)
                actions[item] = action;
            return item;
        }

        private void Run(UIMenuItem item)
        {
            if (actions.TryGetValue(item, out Action action))
                action();
        }
    }
}
