using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Apparatus;
using LSFD.Core;
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

        private readonly MenuPool pool = new MenuPool();

        private readonly UIMenu stationMenu = Theme.CreateMenu("Fire station");
        private readonly UIMenu requestApparatusMenu = Theme.CreateMenu("Request apparatus");
        private readonly UIMenu compartmentMenu = Theme.CreateMenu("Compartments");

        // Menu items are rebuilt every time a menu opens; these map them back to what they do.
        private readonly Dictionary<UIMenuItem, Action> actions = new Dictionary<UIMenuItem, Action>();

        private FireStation currentStation;

        public MenuManager(DutyManager duty, ApparatusManager apparatus, HoseManager hose, HydrantManager hydrants, ToolManager tools)
        {
            this.duty = duty;
            this.apparatus = apparatus;
            this.hose = hose;
            this.hydrants = hydrants;
            this.tools = tools;

            foreach (UIMenu menu in new[] { stationMenu, requestApparatusMenu, compartmentMenu })
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
                Add(stationMenu, new UIMenuItem("Go off duty", "Clock out and change back into your own clothes."), () =>
                {
                    pool.CloseAllMenus();
                    duty.GoOffDuty();
                });
            }
            else
            {
                Add(stationMenu, new UIMenuItem("Go on duty", "Suit up in the locker room and clock in at this station."), () =>
                {
                    pool.CloseAllMenus();
                    duty.GoOnDuty(currentStation);
                });
            }

            UIMenuItem requestItem = Add(stationMenu, new UIMenuItem("Request apparatus", "Have a vehicle and its crew brought out in front of the station."), null);
            requestItem.Enabled = duty.IsOnDuty;
            stationMenu.BindMenuToItem(requestApparatusMenu, requestItem);
            BuildRequestApparatusMenu();

            Add(stationMenu, new UIMenuItem("Restock personal gear", "Refills your armor, health and flashlight."), () =>
            {
                duty.RestockPersonalGear();
                Notify.Info("Personal gear restocked.");
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
                compartmentMenu.AddItem(new UIMenuItem("Water tank", "Connect a supply line to a hydrant to refill the tank.")
                {
                    Skipped = true,
                    RightLabel = source.IsSupplied ? "~b~On hydrant" : $"{source.Water:N0} / {definition.WaterCapacity:N0} L",
                });
            }

            AddHoseItems(source);
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
                Add(compartmentMenu, new UIMenuItem(holdingThis ? "Stow attack hose" : "Pull attack hose",
                    $"{definition.HoseLength:0} m line with an adjustable straight/fog nozzle."), () =>
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
                string text = source.IsSupplied ? "Disconnect supply line" : hydrants.CarryingFrom == source ? "Return supply line" : "Take supply line";
                Add(compartmentMenu, new UIMenuItem(text, $"{definition.SupplyLineLength:0} m line to connect this apparatus to a fire hydrant."), () =>
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

        private void AddHandToolItems(FireApparatus source)
        {
            var handTools = source.Definition.Equipment.Where(e => e.IsHandTool()).ToList();
            if (handTools.Count == 0)
                return;

            compartmentMenu.AddItem(Theme.CreateHeader("Rescue tools"));
            foreach (EquipmentType tool in handTools)
            {
                bool holding = tools.IsHolding(tool) && tools.HeldFrom == source;
                var item = new UIMenuItem(tool.ToDisplayName(), holding ? "Put it back in the compartment." : "Carry it in your hands.")
                {
                    RightLabel = holding ? "~g~In hand" : "",
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

            compartmentMenu.AddItem(Theme.CreateHeader("Hand tools"));
            foreach (EquipmentType tool in weaponTools)
            {
                var item = new UIMenuCheckboxItem(tool.ToDisplayName(), source.IsTaken(tool), "Take it from or put it back in the compartment.");
                Add(compartmentMenu, item, () =>
                {
                    if (item.Checked)
                        apparatus.TakeEquipment(source, tool);
                    else
                        apparatus.StowEquipment(source, tool);
                });
            }
        }

        private void BuildRequestApparatusMenu()
        {
            Rebuild(requestApparatusMenu, "Request apparatus");

            foreach (var group in apparatus.Repository.Spawnable.GroupBy(d => d.HasPump))
            {
                requestApparatusMenu.AddItem(Theme.CreateHeader(group.Key ? "Fire apparatus" : "Support units"));
                foreach (ApparatusDefinition definition in group)
                {
                    string tank = definition.HasPump ? $"{definition.WaterCapacity:N0} L tank, " : "";
                    string crew = definition.MaxCrew > 0 ? $"crew of {definition.MinCrew}-{definition.MaxCrew}, " : "";
                    string equipment = string.Join(", ", definition.Equipment.Select(e => e.ToDisplayName()));
                    var item = new UIMenuItem(definition.Name, $"{tank}{crew}carries: {equipment}.")
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
