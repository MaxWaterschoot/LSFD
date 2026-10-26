using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Crew;
using LSFD.Firefighting;
using LSFD.LivingWorld;
using LSFD.Medical;
using LSFD.Profiles;
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
        private readonly FireSystem fire;
        private readonly Action<FireApparatus> returnApparatus;
        private readonly Func<ApparatusType, Vector3, bool> requestBackup;

        private readonly TabView view = new TabView(L.T("LSFD Mobile Data Terminal")) { Name = L.T("LOS SANTOS FIRE DEPARTMENT"), PauseGame = false };
        private readonly TabInteractiveListItem dispatchTab = new TabInteractiveListItem(L.T("Active Dispatch"), new List<UIMenuItem>());
        private readonly TabInteractiveListItem vehicleTab = new TabInteractiveListItem(L.T("Vehicle Control"), new List<UIMenuItem>());
        private readonly TabInteractiveListItem sceneTab = new TabInteractiveListItem(L.T("Scene Management"), new List<UIMenuItem>());
        private readonly TabInteractiveListItem stationsTab = new TabInteractiveListItem(L.T("Fire Stations"), new List<UIMenuItem>());
        private readonly TabInteractiveListItem profileTab = new TabInteractiveListItem("Profile", new List<UIMenuItem>());
        private readonly ProfileManager profiles;
        private readonly OtherUnits otherUnits;

        // Items are rebuilt on every refresh; these map them back to what they do.
        private readonly Dictionary<UIMenuItem, Action> actions = new Dictionary<UIMenuItem, Action>();

        public bool IsOpen => view.Visible;

        public Tablet(DutyManager duty, CalloutManager callouts, ApparatusManager apparatus, CrewManager crew,
            FireStationManager stations, PatientManager patients, FireSystem fire, ProfileManager profiles, OtherUnits otherUnits,
            Action<FireApparatus> returnApparatus, Func<ApparatusType, Vector3, bool> requestBackup)
        {
            this.otherUnits = otherUnits;
            this.profiles = profiles;
            this.fire = fire;
            this.patients = patients;
            this.requestBackup = requestBackup;
            this.duty = duty;
            this.callouts = callouts;
            this.apparatus = apparatus;
            this.crew = crew;
            this.stations = stations;
            this.returnApparatus = returnApparatus;

            foreach (TabInteractiveListItem tab in new[] { dispatchTab, vehicleTab, sceneTab, stationsTab, profileTab })
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
            view.MoneySubtitle = duty.IsOnDuty ? L.F($"ON DUTY · {duty.HomeStation.Name.ToUpperInvariant()}") : L.T("OFF DUTY");
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
            SetItems(profileTab, BuildProfileTab());
        }

        private List<UIMenuItem> BuildDispatchTab()
        {
            var items = new List<UIMenuItem>();

            if (!duty.IsOnDuty)
            {
                items.Add(Info(L.T("Off duty"), L.T("Go on duty at a fire station to receive calls.")));
                return items;
            }

            Callout offered = callouts.Offered;
            Callout current = callouts.Current;

            if (offered != null)
            {
                items.Add(Theme.CreateHeader(L.T("Incoming call")));
                items.Add(Action(new UIMenuItem(L.F($"Respond: {offered.CalloutMessage}"), Describe(offered)) { RightLabel = offered.Priority.ToCode() },
                    callouts.AcceptOffered));
                items.Add(Info(L.T("Details"), CalloutManager.Describe(offered), Distance(offered.CalloutPosition)));
                items.Add(Action(new UIMenuItem(L.T("Decline"), L.T("Let dispatch assign another unit.")), callouts.DeclineOffered));
            }
            else if (current != null)
            {
                items.Add(Theme.CreateHeader(L.T("Assigned call")));
                items.Add(Info($"{current.Priority.ToCode()} {current.CalloutMessage}", Describe(current),
                    current.HasArrived ? L.T("~g~On scene") : Distance(current.CalloutPosition)));
                items.Add(Info(L.T("Details"), CalloutManager.Describe(current)));
                items.Add(Action(new UIMenuItem(L.T("Set GPS to the scene")), () =>
                {
                    NativeFunction.CallByName<uint>("SET_NEW_WAYPOINT", current.CalloutPosition.X, current.CalloutPosition.Y);
                    Notify.Info(L.T("GPS set to the scene."));
                }));
                items.Add(Action(new UIMenuItem(L.T("Clear call"), L.T("Report the call as handled or cancelled.")), () => callouts.EndCurrent(L.T("Call cleared."))));

                items.Add(Theme.CreateHeader(L.T("Request additional units")));
                items.Add(Action(new UIMenuItem(L.T("Engine company"), L.T("An AI engine with crew responds and works the scene.")),
                    () => requestBackup(ApparatusType.Engine, current.StagingPosition)));
                items.Add(Action(new UIMenuItem(L.T("Ambulance"), L.T("An AI ambulance crew responds and takes care of patients.")),
                    () => requestBackup(ApparatusType.Ambulance, current.StagingPosition)));
            }
            else
            {
                items.Add(Info(L.T("No active calls"), L.T("You are available. Dispatch will send you the next call.")));
                items.Add(Action(new UIMenuItem(L.T("Request a call"), L.T("Ask dispatch for a call right away.")), () =>
                {
                    if (!callouts.DispatchRandom())
                        Notify.Dispatch(L.T("~o~No calls"), L.T("There are no calls waiting right now."));
                }));

                if (profiles.LastReport != null)
                    items.AddRange(BuildReport(profiles.LastReport));
            }

            if (otherUnits.Incidents.Count > 0)
            {
                items.Add(Theme.CreateHeader(L.T("Other units")));
                foreach (OtherIncident incident in otherUnits.Incidents)
                {
                    string units = string.Join(", ", incident.Units.Select(u => $"{u.Name} ({(u.OnScene ? L.T("on scene") : L.T("en route"))})"));
                    items.Add(Info(incident.Name, $"{incident.Street}~n~{units}", Distance(incident.Position)));
                }
            }

            return items;
        }

        private static IEnumerable<UIMenuItem> BuildReport(CallRecord report)
        {
            yield return Theme.CreateHeader(L.F($"Last call report: {report.Name}"));
            yield return Info(L.T("Outcome"), L.F($"{report.Location} · {report.Minutes} min on the call."), report.Completed ? L.T("~g~Completed") : L.T("~o~Not completed"));
            yield return Info(L.T("Response time"), L.T("From accepting the call to arriving on scene, against a target for the distance."), CallScore.Response(report));
            yield return Info(L.T("Water used"), L.T("Pumped by every apparatus on the call, from the tank or a hydrant."), $"{report.WaterUsed:N0} L");
            yield return Info(L.T("People rescued"), L.T("Carried out of buildings."), $"{report.Rescued}");
            yield return Info(L.T("Patients treated"), report.Resuscitated > 0 ? L.F($"{report.Resuscitated} resuscitated.") : "", $"{report.Treated + report.Resuscitated}");
            if (report.Deceased > 0)
                yield return Info(L.T("Patients lost"), "", $"~r~{report.Deceased}");
            if (report.Escalations > 0)
                yield return Info(L.T("Alarms struck during the call"), L.T("The fire grew to a bigger alarm."), $"~o~{report.Escalations}");
            yield return Info(L.T("Score"), L.T("Response time, lives lost, fire escalation and whether the call was completed; rescues earn extra. Completed calls add a tenth of the score to your career points."), CallScore.Display(report.Score));
        }

        private List<UIMenuItem> BuildVehicleTab()
        {
            var items = new List<UIMenuItem>();

            if (apparatus.All.Count == 0)
            {
                items.Add(Info(L.T("No apparatus"), L.T("Request an apparatus at a fire station, or walk up to a fire truck.")));
                return items;
            }

            foreach (FireApparatus unit in apparatus.All.Where(a => a.IsValid))
            {
                Vehicle vehicle = unit.Vehicle;
                string role = unit.IsCommitted ? L.T(" · AI, other call") : unit.IsAiUnit ? (unit.ResponseDestination.HasValue ? L.T(" · AI, en route") : L.T(" · AI")) : "";
                items.Add(Theme.CreateHeader($"{unit.Definition.Name}{role} · {Distance(vehicle.Position)}"));

                if (unit.Definition.HasPump)
                {
                    items.Add(Info(L.T("Water tank"), L.T("Connect a supply line to a hydrant to refill."),
                        unit.IsSupplied ? L.T("~b~On hydrant") : $"{unit.Water:N0} / {unit.Definition.WaterCapacity:N0} L"));

                    var pump = new UIMenuCheckboxItem(L.T("Pump engaged"), unit.PumpEngaged, L.T("Feeds water to the attack hose."));
                    items.Add(Action(pump, () => unit.PumpEngaged = pump.Checked));

                    var pressure = new UIMenuNumericScrollerItem<int>(L.T("Pump pressure"), L.T("Discharge pressure to the attack hose."), 20, 100, 10)
                    {
                        Value = (int)Math.Round(unit.PumpPressure * 100f),
                        Formatter = value => $"{value}%",
                    };
                    items.Add(Action(pressure, () => unit.PumpPressure = pressure.Value / 100f));
                }

                var lights = new UIMenuCheckboxItem(L.T("Emergency lights"), vehicle.IsSirenOn);
                items.Add(Action(lights, () => vehicle.IsSirenOn = lights.Checked));

                var silent = new UIMenuCheckboxItem(L.T("Silent mode (lights only)"), vehicle.IsSirenSilent);
                items.Add(Action(silent, () => vehicle.IsSirenSilent = silent.Checked));

                items.Add(Action(new UIMenuItem(L.T("Return to station"), L.T("Sends this apparatus and its crew back to quarters.")), () =>
                {
                    returnApparatus(unit);
                    Notify.Info(L.F($"~y~{unit.Definition.Name}~s~ is returning to quarters."));
                }));
            }

            return items;
        }

        private List<UIMenuItem> BuildSceneTab()
        {
            var items = new List<UIMenuItem>();
            Vector3 player = Game.LocalPlayer.Character.Position;

            items.Add(Theme.CreateHeader(L.T("Scene")));
            FireIncident incident = fire.FindNearest(player, SceneRadius);
            if (incident != null)
            {
                int minutes = (int)((Game.GameTime - incident.StartedAt) / 60000);
                items.Add(Info(L.T("Fire"), L.T("Growing → burning → under control → knockdown → out."), incident.Status.ToDisplayName()));
                items.Add(Info(L.T("  Alarm"), L.T("Goes up when the fire grows too big; every alarm brings another engine."),
                    CalloutPriorityExtensions.AlarmToDisplay(incident.AlarmLevel)));
                items.Add(Info(L.T("  Burning spots"), L.T("Spots with open flames."), incident.BurningPoints == 0 ? "~g~0" : $"~r~{incident.BurningPoints}"));
                items.Add(Info(L.T("  Hotspots"), L.T("Knocked down, but they rekindle unless you overhaul them with water. The thermal camera shows them."),
                    incident.Hotspots == 0 ? "~g~0" : $"~o~{incident.Hotspots}"));
                items.Add(Info(L.T("  Time since dispatch"), "", $"{minutes} min"));
                if (incident.Options.Class != FireClass.Ordinary)
                    items.Add(Info(L.T("  Fire class"), L.T("Flammable: use foam. Electrical: isolate the power first. Battery: lots of water, keeps rekindling."),
                        incident.Options.Class.ToString()));
                if (incident.Options.Class == FireClass.Electrical)
                    items.Add(Info(L.T("  Power"), L.T("Isolate it at the panel (yellow arrow) before using water."), incident.IsPowerIsolated ? L.T("~g~Isolated") : L.T("~r~LIVE")));
                if (incident.Options.IsIndoor)
                    items.Add(Info(L.T("  Ventilation"), L.T("A PPV fan in the doorway pushes the smoke out, but feeds the fire."), incident.IsVentilated ? L.T("~b~PPV running") : L.T("None")));
            }
            else
            {
                int fires = Fires.CountFiresInRange(player, SceneRadius);
                items.Add(Info(L.T("Fires nearby"), L.F($"Fires within {SceneRadius:0} m of you."), fires == 0 ? L.T("~g~None") : $"~r~{fires}"));
            }

            var onScene = patients.All.Where(p => p.Ped.Exists() && p.Ped.DistanceTo(player) <= SceneRadius).ToList();
            if (onScene.Count > 0)
            {
                int trapped = onScene.Count(p => p.NeedsRescue);
                int critical = onScene.Count(p => p.Condition == PatientCondition.CardiacArrest);
                int waiting = onScene.Count(p => p.NeedsCare) - critical;
                int stable = onScene.Count(p => p.Condition == PatientCondition.Stable && p.Transport == TransportState.None);
                int transported = onScene.Count(p => p.Transport != TransportState.None);
                int deceased = onScene.Count(p => p.Condition == PatientCondition.Deceased);

                items.Add(Info(L.T("Patients"), L.T("Patients within the scene area."), $"{onScene.Count}"));
                if (trapped > 0)
                    items.Add(Info(L.T("  Trapped in building"), L.T("Carry them out to the staging area."), $"~r~{trapped}"));
                if (critical > 0)
                    items.Add(Info(L.T("  Cardiac arrest"), L.T("Needs CPR now."), $"~r~{critical}"));
                if (waiting > 0)
                    items.Add(Info(L.T("  Awaiting treatment"), L.T("Injured or unconscious."), $"~o~{waiting}"));
                if (stable > 0)
                    items.Add(Info(L.T("  Stable"), L.T("Waiting for an ambulance to take them to hospital."), $"~g~{stable}"));
                if (transported > 0)
                    items.Add(Info(L.T("  In transport"), L.T("Being loaded or in the ambulance."), $"~b~{transported}"));
                if (deceased > 0)
                    items.Add(Info("  Deceased", "", $"{deceased}"));

                if (patients.IsMassCasualty(player))
                {
                    int untagged = onScene.Count(p => p.NeedsTriage);
                    items.Add(Theme.CreateHeader(L.T("Mass casualty incident: triage")));
                    items.Add(Info(L.T("  Red (immediate)"), "", $"~r~{onScene.Count(p => p.Triage == TriageTag.Red)}"));
                    items.Add(Info(L.T("  Yellow (delayed)"), "", $"~y~{onScene.Count(p => p.Triage == TriageTag.Yellow)}"));
                    items.Add(Info(L.T("  Green (minor)"), "", $"~g~{onScene.Count(p => p.Triage == TriageTag.Green)}"));
                    items.Add(Info(L.T("  Black (expectant)"), "", $"{onScene.Count(p => p.Triage == TriageTag.Black)}"));
                    if (untagged > 0)
                        items.Add(Info(L.T("  Not triaged yet"), L.T("Hold E at a patient, or order the crew to patient care."), $"~o~{untagged}"));
                }
            }

            items.Add(Theme.CreateHeader(L.F($"Crew ({crew.Members.Count})")));
            if (crew.Members.Count == 0)
            {
                items.Add(Info(L.T("No crew"), L.T("Apparatus come with AI firefighters when requested at a station.")));
                return items;
            }

            foreach (CrewMember member in crew.Members)
            {
                string status = CrewManager.JobToDisplayName(member.Job) ?? CrewManager.OrderToDisplayName(member.Order);
                string unit = member.Apparatus.Definition.Name + (member.Apparatus.IsAiUnit ? L.T(" (AI unit)") : "");
                items.Add(Info(member.Name, L.F($"Riding on the {unit}."), status));
            }

            items.Add(Theme.CreateHeader(L.T("Orders to your crew")));
            items.Add(CrewOrderItem(L.T("Work the scene"), L.T("Do what is most urgent: CPR, rescues, extrication, treatment, then the fire."), CrewOrder.WorkScene));
            items.Add(CrewOrderItem(L.T("Search and rescue"), L.T("Carry victims out of buildings and extricate trapped occupants."), CrewOrder.RescueVictims));
            items.Add(CrewOrderItem(L.T("Patient care"), L.T("CPR and first aid for every patient on scene."), CrewOrder.TreatPatients));
            items.Add(CrewOrderItem(L.T("Attack the fire"), L.T("Stretch hose lines from the engines (extinguishers when none are left) and attack the closest fires."), CrewOrder.Extinguish));
            items.Add(Action(new UIMenuItem(L.T("Operate the deck gun"), L.T("One firefighter per engine works the roof-mounted water cannon.")), () => crew.CommandDeckGun()));
            items.Add(CrewOrderItem(L.T("Follow me"), L.T("Walk with you."), CrewOrder.FollowPlayer));
            items.Add(CrewOrderItem(L.T("Hold position"), L.T("Stay where they are."), CrewOrder.HoldPosition));
            items.Add(CrewOrderItem(L.T("Return to apparatus"), L.T("Get back in the vehicles."), CrewOrder.ReturnToApparatus));

            return items;
        }

        private List<UIMenuItem> BuildProfileTab()
        {
            Profile profile = profiles.Profile;
            int points = profile.Points;
            int? next = Ranks.NextAt(points);

            var items = new List<UIMenuItem>
            {
                Theme.CreateHeader(profile.Name),
                Info(L.T("Rank"), next.HasValue ? L.F($"{next.Value - points} points to the next rank.") : L.T("The top of the department."), $"~y~{profiles.Rank}"),
                Info(L.T("Points"), L.T("Calls, fires, rescues and patients all count."), $"{points}"),
                Info(L.T("Hours on duty"), "", $"{profile.HoursOnDuty:0.0}"),
                Info(L.T("Average score"), L.T("Of your last 10 call reports."), profiles.AverageScore(10) is int average ? CallScore.Display(average) : "-"),
                Theme.CreateHeader(L.T("Statistics")),
                Info(L.T("Calls completed"), "", $"{profile.CallsCompleted} / {profile.CallsAttended}"),
                Info(L.T("Fires put out"), "", $"{profile.FiresExtinguished}"),
                Info(L.T("People rescued"), L.T("Carried out of burning buildings."), $"{profile.PeopleRescued}"),
                Info(L.T("Patients treated"), "", $"{profile.PatientsTreated}"),
                Info(L.T("Patients resuscitated"), "", $"{profile.PatientsResuscitated}"),
                Info(L.T("Patients transported"), "", $"{profile.PatientsTransported}"),
                Theme.CreateHeader(L.T("Recent calls")),
            };

            if (profile.History.Count == 0)
                items.Add(Info(L.T("No calls yet"), L.T("Accepted calls are logged here.")));

            foreach (CallRecord call in profile.History.Take(15))
            {
                string alarm = CalloutPriorityExtensions.AlarmToDisplay(call.AlarmLevel);
                string details = L.F($"{CalloutCategoryExtensions.ToDisplayName(call.Category)}{(alarm != "" ? " · " + alarm : "")} · {call.Location} · {call.Minutes} min");
                if (call.HasReport)
                    details += L.F($"~n~Response {CallScore.Response(call)} · Water {call.WaterUsed:N0} L · Rescued {call.Rescued}") +
                        (call.Deceased > 0 ? L.F($" · Lost {call.Deceased}") : "");
                string outcome = call.Completed ? L.T("~g~Completed") : L.T("~o~Cancelled");
                items.Add(Info($"{call.Date:dd/MM HH:mm}  {call.Name}", details, call.HasReport ? $"{outcome}~s~ · {call.Score}" : outcome));
            }

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
                    var item = new UIMenuItem(station.Name, L.F($"{station.Data.Address} ({station.Data.Type.ToDisplayName()}). Select to set a GPS waypoint."))
                    {
                        RightLabel = Distance(station.Position),
                    };
                    items.Add(Action(item, () =>
                    {
                        NativeFunction.CallByName<uint>("SET_NEW_WAYPOINT", station.Position.X, station.Position.Y);
                        Notify.Info(L.F($"GPS set to ~y~{station.Name}~s~."));
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
