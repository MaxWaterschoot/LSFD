using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Crew;
using LSFD.Medical;
using LSFD.Stations;
using RAGENativeUI;
using RAGENativeUI.Elements;
using RAGENativeUI.PauseMenu;
using Rage;
using Rage.Native;

namespace LSFD.UI
{
    /// <summary>The in-game fire department tablet (MDT): dispatch, vehicle control, scene management and stations.</summary>
    internal class Tablet
    {
        private const float SceneRadius = 100f;

        private readonly DutyManager duty;
        private readonly CalloutManager callouts;
        private readonly ApparatusManager apparatus;
        private readonly CrewManager crew;
        private readonly FireStationManager stations;
        private readonly PatientManager patients;
        private readonly Action<FireApparatus> returnApparatus;
        private readonly Func<ApparatusType, Vector3, bool> requestBackup;

        private readonly TabView view = new TabView("LSFD Mobile Data Terminal") { Name = "LOS SANTOS FIRE DEPARTMENT", PauseGame = false };
        private readonly TabInteractiveListItem dispatchTab = new TabInteractiveListItem("Active Dispatch", new List<UIMenuItem>());
        private readonly TabInteractiveListItem vehicleTab = new TabInteractiveListItem("Vehicle Control", new List<UIMenuItem>());
        private readonly TabInteractiveListItem sceneTab = new TabInteractiveListItem("Scene Management", new List<UIMenuItem>());
        private readonly TabInteractiveListItem stationsTab = new TabInteractiveListItem("Fire Stations", new List<UIMenuItem>());

        // Items are rebuilt on every refresh; these map them back to what they do.
        private readonly Dictionary<UIMenuItem, Action> actions = new Dictionary<UIMenuItem, Action>();

        public bool IsOpen => view.Visible;

        public Tablet(DutyManager duty, CalloutManager callouts, ApparatusManager apparatus, CrewManager crew,
            FireStationManager stations, PatientManager patients, Action<FireApparatus> returnApparatus,
            Func<ApparatusType, Vector3, bool> requestBackup)
        {
            this.patients = patients;
            this.requestBackup = requestBackup;
            this.duty = duty;
            this.callouts = callouts;
            this.apparatus = apparatus;
            this.crew = crew;
            this.stations = stations;
            this.returnApparatus = returnApparatus;

            foreach (TabInteractiveListItem tab in new[] { dispatchTab, vehicleTab, sceneTab, stationsTab })
            {
                view.AddTab(tab);
                tab.BackingMenu.OnItemSelect += (sender, item, index) =>
                {
                    if (!(item is UIMenuCheckboxItem))
                        Run(item);
                };
                tab.BackingMenu.OnCheckboxChange += (sender, item, isChecked) => Run(item);
                tab.BackingMenu.OnScrollerChange += (sender, item, oldIndex, newIndex) => Run(item);
            }

            view.RefreshIndex();
            Game.RawFrameRender += (sender, e) =>
            {
                if (view.Visible)
                    view.DrawTextures(e.Graphics);
            };
        }

        public void Process()
        {
            if (Game.IsKeyDown(Settings.TabletKey))
            {
                if (view.Visible)
                    view.Visible = false;
                else
                    Open();
            }

            if (!view.Visible)
                return;

            view.Money = World.TimeOfDay.ToString(@"hh\:mm");
            view.MoneySubtitle = duty.IsOnDuty ? $"ON DUTY · {duty.HomeStation.Name.ToUpperInvariant()}" : "OFF DUTY";
            view.Update();
        }

        public void Close()
        {
            view.Visible = false;
        }

        private void Open()
        {
            Refresh();
            view.Visible = true;
        }

        private void Refresh()
        {
            actions.Clear();
            SetItems(dispatchTab, BuildDispatchTab());
            SetItems(vehicleTab, BuildVehicleTab());
            SetItems(sceneTab, BuildSceneTab());
            SetItems(stationsTab, BuildStationsTab());
        }

        private List<UIMenuItem> BuildDispatchTab()
        {
            var items = new List<UIMenuItem>();

            if (!duty.IsOnDuty)
            {
                items.Add(Info("Off duty", "Go on duty at a fire station to receive calls."));
                return items;
            }

            Callout offered = callouts.Offered;
            Callout current = callouts.Current;

            if (offered != null)
            {
                items.Add(Theme.CreateHeader("Incoming call"));
                items.Add(Action(new UIMenuItem($"Respond: {offered.CalloutMessage}", Describe(offered)) { RightLabel = offered.Priority.ToCode() },
                    callouts.AcceptOffered));
                items.Add(Info("Details", CalloutManager.Describe(offered), Distance(offered.CalloutPosition)));
                items.Add(Action(new UIMenuItem("Decline", "Let dispatch assign another unit."), callouts.DeclineOffered));
            }
            else if (current != null)
            {
                items.Add(Theme.CreateHeader("Assigned call"));
                items.Add(Info($"{current.Priority.ToCode()} {current.CalloutMessage}", Describe(current),
                    current.HasArrived ? "~g~On scene" : Distance(current.CalloutPosition)));
                items.Add(Info("Details", CalloutManager.Describe(current)));
                items.Add(Action(new UIMenuItem("Set GPS to the scene"), () =>
                {
                    NativeFunction.CallByName<uint>("SET_NEW_WAYPOINT", current.CalloutPosition.X, current.CalloutPosition.Y);
                    Notify.Info("GPS set to the scene.");
                }));
                items.Add(Action(new UIMenuItem("Clear call", "Report the call as handled or cancelled."), () => callouts.EndCurrent("Call cleared.")));

                items.Add(Theme.CreateHeader("Request additional units"));
                items.Add(Action(new UIMenuItem("Engine company", "An AI engine with crew responds and works the scene."),
                    () => requestBackup(ApparatusType.Engine, current.StagingPosition)));
                items.Add(Action(new UIMenuItem("Ambulance", "An AI ambulance crew responds and takes care of patients."),
                    () => requestBackup(ApparatusType.Ambulance, current.StagingPosition)));
            }
            else
            {
                items.Add(Info("No active calls", "You are available. Dispatch will send you the next call."));
                items.Add(Action(new UIMenuItem("Request a call", "Ask dispatch for a call right away."), () =>
                {
                    if (!callouts.DispatchRandom())
                        Notify.Dispatch("~o~No calls", "There are no calls waiting right now.");
                }));
            }

            return items;
        }

        private List<UIMenuItem> BuildVehicleTab()
        {
            var items = new List<UIMenuItem>();

            if (apparatus.All.Count == 0)
            {
                items.Add(Info("No apparatus", "Request an apparatus at a fire station, or walk up to a fire truck."));
                return items;
            }

            foreach (FireApparatus unit in apparatus.All.Where(a => a.IsValid))
            {
                Vehicle vehicle = unit.Vehicle;
                string role = unit.IsAiUnit ? (unit.ResponseDestination.HasValue ? " · AI, en route" : " · AI") : "";
                items.Add(Theme.CreateHeader($"{unit.Definition.Name}{role} · {Distance(vehicle.Position)}"));

                if (unit.Definition.HasPump)
                {
                    items.Add(Info("Water tank", "Connect a supply line to a hydrant to refill.",
                        unit.IsSupplied ? "~b~On hydrant" : $"{unit.Water:N0} / {unit.Definition.WaterCapacity:N0} L"));

                    var pump = new UIMenuCheckboxItem("Pump engaged", unit.PumpEngaged, "Feeds water to the attack hose.");
                    items.Add(Action(pump, () => unit.PumpEngaged = pump.Checked));

                    var pressure = new UIMenuNumericScrollerItem<int>("Pump pressure", "Discharge pressure to the attack hose.", 20, 100, 10)
                    {
                        Value = (int)Math.Round(unit.PumpPressure * 100f),
                        Formatter = value => $"{value}%",
                    };
                    items.Add(Action(pressure, () => unit.PumpPressure = pressure.Value / 100f));
                }

                var lights = new UIMenuCheckboxItem("Emergency lights", vehicle.IsSirenOn);
                items.Add(Action(lights, () => vehicle.IsSirenOn = lights.Checked));

                var silent = new UIMenuCheckboxItem("Silent mode (lights only)", vehicle.IsSirenSilent);
                items.Add(Action(silent, () => vehicle.IsSirenSilent = silent.Checked));

                items.Add(Action(new UIMenuItem("Return to station", "Sends this apparatus and its crew back to quarters."), () =>
                {
                    returnApparatus(unit);
                    Notify.Info($"~y~{unit.Definition.Name}~s~ is returning to quarters.");
                }));
            }

            return items;
        }

        private List<UIMenuItem> BuildSceneTab()
        {
            var items = new List<UIMenuItem>();
            Vector3 player = Game.LocalPlayer.Character.Position;

            int fires = Fires.CountFiresInRange(player, SceneRadius);
            items.Add(Theme.CreateHeader("Scene"));
            items.Add(Info("Fires nearby", $"Fires within {SceneRadius:0} m of you.", fires == 0 ? "~g~None" : $"~r~{fires}"));

            var onScene = patients.All.Where(p => p.Ped.Exists() && p.Ped.DistanceTo(player) <= SceneRadius).ToList();
            if (onScene.Count > 0)
            {
                int trapped = onScene.Count(p => p.NeedsRescue);
                int critical = onScene.Count(p => p.Condition == PatientCondition.CardiacArrest);
                int waiting = onScene.Count(p => p.NeedsCare) - critical;
                int stable = onScene.Count(p => p.Condition == PatientCondition.Stable);
                int deceased = onScene.Count(p => p.Condition == PatientCondition.Deceased);

                items.Add(Info("Patients", "Patients within the scene area.", $"{onScene.Count}"));
                if (trapped > 0)
                    items.Add(Info("  Trapped in building", "Carry them out to the staging area.", $"~r~{trapped}"));
                if (critical > 0)
                    items.Add(Info("  Cardiac arrest", "Needs CPR now.", $"~r~{critical}"));
                if (waiting > 0)
                    items.Add(Info("  Awaiting treatment", "Injured or unconscious.", $"~o~{waiting}"));
                if (stable > 0)
                    items.Add(Info("  Stable", "Ready for transport.", $"~g~{stable}"));
                if (deceased > 0)
                    items.Add(Info("  Deceased", "", $"{deceased}"));
            }

            items.Add(Theme.CreateHeader($"Crew ({crew.Members.Count})"));
            if (crew.Members.Count == 0)
            {
                items.Add(Info("No crew", "Apparatus come with AI firefighters when requested at a station."));
                return items;
            }

            foreach (CrewMember member in crew.Members)
            {
                string status = CrewManager.JobToDisplayName(member.Job) ?? CrewManager.OrderToDisplayName(member.Order);
                string unit = member.Apparatus.Definition.Name + (member.Apparatus.IsAiUnit ? " (AI unit)" : "");
                items.Add(Info(member.Name, $"Riding on the {unit}.", status));
            }

            items.Add(Theme.CreateHeader("Orders to your crew"));
            items.Add(CrewOrderItem("Work the scene", "Do what is most urgent: CPR, rescues, extrication, treatment, then the fire.", CrewOrder.WorkScene));
            items.Add(CrewOrderItem("Search and rescue", "Carry victims out of buildings and extricate trapped occupants.", CrewOrder.RescueVictims));
            items.Add(CrewOrderItem("Patient care", "CPR and first aid for every patient on scene.", CrewOrder.TreatPatients));
            items.Add(CrewOrderItem("Extinguish nearby fires", "Grab extinguishers and attack the closest fires.", CrewOrder.Extinguish));
            items.Add(CrewOrderItem("Follow me", "Walk with you.", CrewOrder.FollowPlayer));
            items.Add(CrewOrderItem("Hold position", "Stay where they are.", CrewOrder.HoldPosition));
            items.Add(CrewOrderItem("Return to apparatus", "Get back in the vehicles.", CrewOrder.ReturnToApparatus));

            return items;
        }

        private List<UIMenuItem> BuildStationsTab()
        {
            var items = new List<UIMenuItem>();

            foreach (var area in stations.StationsByArea.GroupBy(s => s.Data.Area))
            {
                items.Add(Theme.CreateHeader(area.Key.ToDisplayName()));
                foreach (FireStation station in area)
                {
                    var item = new UIMenuItem(station.Name, $"{station.Data.Address} ({station.Data.Type}). Select to set a GPS waypoint.")
                    {
                        RightLabel = Distance(station.Position),
                    };
                    items.Add(Action(item, () =>
                    {
                        NativeFunction.CallByName<uint>("SET_NEW_WAYPOINT", station.Position.X, station.Position.Y);
                        Notify.Info($"GPS set to ~y~{station.Name}~s~.");
                    }));
                }
            }

            return items;
        }

        private UIMenuItem CrewOrderItem(string text, string description, CrewOrder order)
        {
            return Action(new UIMenuItem(text, description), () => crew.Command(order));
        }

        private UIMenuItem Action(UIMenuItem item, Action action)
        {
            actions[item] = action;
            return item;
        }

        private static UIMenuItem Info(string text, string description, string rightLabel = null)
        {
            return new UIMenuItem(text, description) { Skipped = true, RightLabel = rightLabel ?? "" };
        }

        private void Run(UIMenuItem item)
        {
            if (!actions.TryGetValue(item, out Action action))
                return;

            action();
            // Show the new state right away (e.g. after accepting a call), but keep scrollers where they are.
            if (!(item is UIMenuScrollerItem))
                Refresh();
        }

        private static void SetItems(TabInteractiveListItem tab, List<UIMenuItem> items)
        {
            int index = tab.Index;
            tab.Items = items;
            tab.RefreshIndex();
            tab.Index = Math.Max(0, Math.Min(index, items.Count - 1));
        }

        private static string Describe(Callout callout)
        {
            return $"{World.GetStreetName(callout.CalloutPosition)}. {callout.CalloutAdvisory}";
        }

        private static string Distance(Vector3 position)
        {
            float meters = Game.LocalPlayer.Character.DistanceTo(position);
            return meters < 1000f ? $"{meters:0} m" : $"{meters / 1000f:0.0} km";
        }
    }
}
