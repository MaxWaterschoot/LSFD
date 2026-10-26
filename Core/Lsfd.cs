using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Buildings;
using LSFD.Crew;
using LSFD.Firefighting;
using LSFD.LivingWorld;
using LSFD.Medical;
using LSFD.Profiles;
using LSFD.Safety;
using LSFD.Stations;
using LSFD.UI;
using LSFD.Utils;
using LSFD.VehicleRescue;
using LSFD.Water;
using Rage;

namespace LSFD.Core
{
    /// <summary>Creates all systems, wires them together and runs them every tick.</summary>
    internal static class Lsfd
    {
        public static ProfileManager Profiles { get; private set; }
        public static FireSystem Fire { get; private set; }
        public static ForcibleEntry Entry { get; private set; }
        public static Ventilation Ventilation { get; private set; }
        public static DeckGun DeckGun { get; private set; }
        public static BreathingApparatus Scba { get; private set; }
        public static ThermalCamera ThermalCamera { get; private set; }
        public static HazardManager Hazards { get; private set; }
        public static FireStationManager Stations { get; private set; }
        public static DutyManager Duty { get; private set; }
        public static ApparatusManager Apparatus { get; private set; }
        public static CrewManager Crew { get; private set; }
        public static HoseManager Hose { get; private set; }
        public static HydrantManager Hydrants { get; private set; }
        public static ToolManager Tools { get; private set; }
        public static ExtricationManager Extrication { get; private set; }
        public static PatientManager Patients { get; private set; }
        public static AmbulanceTransport Transport { get; private set; }
        public static FireLocationRepository FireLocations { get; private set; }
        public static CalloutRegistry Registry { get; private set; }
        public static CalloutManager Callouts { get; private set; }
        public static InteractionRunner Interactions { get; private set; }
        public static SceneActionManager SceneActions { get; private set; }
        public static MenuManager Menus { get; private set; }
        public static Tablet Tablet { get; private set; }
        public static Hud Hud { get; private set; }
        public static StationLife StationLife { get; private set; }
        public static GameDispatch GameDispatch { get; private set; }
        public static TrafficControl Traffic { get; private set; }
        public static PoliceSupport Police { get; private set; }
        public static AmbientIncidents Ambient { get; private set; }
        public static OtherUnits OtherUnits { get; private set; }
        public static DispatchRadio Radio { get; private set; }
        public static WitnessManager Witnesses { get; private set; }

        // AI units sent to the current call; they go back to quarters when it ends.
        private static readonly List<FireApparatus> sceneUnits = new List<FireApparatus>();

        public static void Initialize()
        {
            Settings.Load();
            L.Load(Settings.Language);
            Profiles = new ProfileManager();

            Stations = new FireStationManager();
            Stations.LoadFireStations();
            Fire = new FireSystem();
            Entry = new ForcibleEntry();
            Ventilation = new Ventilation(Fire);
            Scba = new BreathingApparatus();
            Duty = new DutyManager(Scba, Profiles);
            Apparatus = new ApparatusManager();
            Hose = new HoseManager(Fire);
            DeckGun = new DeckGun(Fire, Apparatus);
            Hydrants = new HydrantManager(Apparatus);
            Tools = new ToolManager();
            Extrication = new ExtricationManager(Tools, Apparatus);
            Patients = new PatientManager(Tools, Fire);
            Crew = new CrewManager(Patients, Extrication, Fire, Entry, Apparatus);
            Transport = new AmbulanceTransport(Crew, Apparatus, Patients, ReturnApparatus, RequestBackup);
            Patients.TransportRequired = Settings.PatientTransport && Apparatus.FindDefinition(ApparatusType.Ambulance) != null;
            ThermalCamera = new ThermalCamera(Fire, Patients);
            Hazards = new HazardManager(Fire, Scba, ThermalCamera, Hose);
            FireLocations = new FireLocationRepository();

            Registry = new CalloutRegistry();
            Registry.RegisterFromAssembly(typeof(Lsfd).Assembly);
            Registry.LoadCalloutAssemblies();
            Registry.LoadXmlCallouts();
            Callouts = new CalloutManager(Registry, Duty);

            GameDispatch = new GameDispatch();
            Traffic = new TrafficControl(Callouts, Apparatus, Tools);
            Police = new PoliceSupport();
            Ambient = new AmbientIncidents(Registry, Callouts, Duty);
            OtherUnits = new OtherUnits(Apparatus, Stations, Callouts, ReturnApparatus);
            Radio = new DispatchRadio(Duty, Stations, Apparatus);
            Witnesses = new WitnessManager();

            // In order of priority: the first one with something to offer gets the interact key.
            SceneActions = new SceneActionManager(Tools);
            Interactions = new InteractionRunner(Patients, SceneActions, Witnesses, Fire, Entry, Ventilation, Hydrants, Extrication, Apparatus, Stations, Traffic);

            Menus = new MenuManager(Duty, Apparatus, Hose, Hydrants, Tools, Scba, ThermalCamera, Ventilation, Profiles);
            Tablet = new Tablet(Duty, Callouts, Apparatus, Crew, Stations, Patients, Fire, Profiles, OtherUnits, ReturnApparatus, RequestBackup);
            Hud = new Hud();

            Stations.StationMenuRequested += Menus.OpenStationMenu;
            Apparatus.CompartmentMenuRequested += Menus.OpenCompartmentMenu;
            Apparatus.Spawned += Crew.SpawnCrew;
            // The radio first, so "Copy" comes before "Additional units are en route".
            Callouts.CalloutOffered += Radio.OnCalloutOffered;
            Callouts.CalloutAccepted += Radio.OnCalloutAccepted;
            Callouts.CalloutEnded += Radio.OnCalloutEnded;
            Callouts.CalloutDeclined += Radio.OnCalloutDeclined;
            Duty.DutyStateChanged += Radio.OnDutyStateChanged;
            Callouts.CalloutAccepted += OnCalloutAccepted;
            Callouts.CalloutEnded += OnCalloutEnded;
            Duty.DutyStateChanged += OnDutyStateChanged;
            Extrication.PatientExtricated += ped => Patients.Register(ped, PatientCondition.Injured);
            StationLife = new StationLife(Apparatus, Crew, Duty);
            Duty.DutyStateChanged += StationLife.OnDutyStateChanged;
            Callouts.CalloutAccepted += StationLife.OnCalloutAccepted;
            Callouts.CalloutAccepted += Profiles.OnCalloutAccepted;
            Callouts.CalloutEnded += Profiles.OnCalloutEnded;
            Profiles.ReportReady += Hud.ShowCallReport;
            Patients.ConditionChanged += (ped, condition) =>
            {
                if (condition == PatientCondition.Deceased)
                    Profiles.OnPatientDied();
            };
            Patients.Milestone += OnPatientMilestone;
            Fire.Escalated += OnFireEscalated;
            Fire.StatusChanged += OnFireStatusChanged;
            Fire.Rekindled += OnFireRekindled;
            Callouts.CalloutAccepted += Police.Respond;
            Callouts.CalloutEnded += callout => Police.ReleaseAll();
            Callouts.CalloutDeclined += OtherUnits.TakeOver;

            // The public events for add-ons.
            Callouts.CalloutAccepted += Events.RaiseCalloutAccepted;
            Callouts.CalloutEnded += Events.RaiseCalloutEnded;
            Apparatus.Spawned += unit => Events.RaiseApparatusSpawned(unit.Vehicle, unit.Definition.Type, unit.IsAiUnit);
            Fire.StatusChanged += (incident, status) =>
            {
                if (status == FireStatus.Out)
                    Events.RaiseFireExtinguished(incident);
            };
        }

        public static void Tick()
        {
            bool uiOpen = Menus.IsAnyMenuOpen || Tablet.IsOpen;

            Menus.Process();
            if (!Menus.IsAnyMenuOpen)
                Tablet.Process();

            Stations.Process(canOpenMenu: !uiOpen);
            Apparatus.Process();
            Crew.Process();
            Transport.Process(Duty.IsOnDuty);
            Hose.Process();
            DeckGun.Process();
            Fire.Process(ExtinguisherUsers());
            Ventilation.Process();
            Hydrants.Process();
            Tools.Process();
            Extrication.Process();
            Patients.Process();
            Callouts.Process();
            SceneActions.Process();
            Scba.Process(Duty.IsOnDuty);
            ThermalCamera.Process(Duty.IsOnDuty);
            Hazards.Process(Duty.IsOnDuty, uiOpen);
            Profiles.Process(Duty.IsOnDuty);
            StationLife.Process();
            GameDispatch.Process(Duty.IsOnDuty);
            Traffic.Process(Duty.IsOnDuty);
            Police.Process();
            Ambient.Process();
            OtherUnits.Process(Duty.IsOnDuty);
            Witnesses.Process();
            Sound.Process();

            if (!uiOpen)
                Interactions.Process();

            Hud.Process(Hose, Hydrants, Apparatus, Tools, Interactions, Fire, Scba, Hazards, ThermalCamera, Duty.IsOnDuty);
        }

        public static void Shutdown()
        {
            Menus?.CloseAll();
            Tablet?.Close();
            Callouts?.EndCurrent(null);
            Police?.RemoveAll();
            Traffic?.Clear();
            OtherUnits?.Clear();
            GameDispatch?.Restore();
            Transport?.Shutdown();
            Duty?.GoOffDuty();
            ReturnAllApparatus();
            Fire?.RemoveAll();
            Entry?.ReleaseAll();
            Ventilation?.Stow();
            ThermalCamera?.Stow();
            Hazards?.Reset();
            Stations?.CleanupFireStations();
            Profiles?.Save();
            Registry?.Shutdown();
            Witnesses?.Clear();
            Sound.Shutdown();
        }

        /// <summary>Sends an apparatus back to quarters, stowing everything that is connected to it.</summary>
        public static void ReturnApparatus(FireApparatus apparatus)
        {
            if (Hose.Source == apparatus)
                Hose.StowHose();
            if (Hydrants.CarryingFrom == apparatus)
                Hydrants.ReturnSupplyLine();
            Hydrants.Disconnect(apparatus, false);
            if (Tools.HeldFrom == apparatus)
                Tools.StowTool();
            if (ThermalCamera.Source == apparatus)
                ThermalCamera.Stow();
            if (Ventilation.Source == apparatus)
                Ventilation.Stow();

            Crew.Dismiss(apparatus);
            Apparatus.Dismiss(apparatus);
        }

        /// <summary>Sends an AI unit of the given type from the nearest station to <paramref name="destination"/>.</summary>
        public static bool RequestBackup(ApparatusType type, Vector3 destination)
        {
            ApparatusDefinition definition = Apparatus.FindDefinition(type);
            // Units out on other calls can't come; when every station is busy, the nearest one sends what it has.
            FireStation station = OtherUnits.FindAvailableStation(type, destination) ?? Stations.GetNearestStation(destination);
            if (definition == null || station == null)
            {
                Notify.Dispatch(L.T("~o~No units"), L.F($"There is no {type} available in Apparatus.xml."));
                return false;
            }

            FireApparatus unit = Apparatus.SpawnResponding(definition, station, destination);
            if (unit == null)
                return false;

            sceneUnits.Add(unit);
            Radio.OnBackupSent(type);
            return true;
        }

        // Like real dispatch: every alarm above the first brings another engine company, and calls with
        // patients get an ambulance.
        private static void OnCalloutAccepted(Callout callout)
        {
            Vector3 scene = callout.StagingPosition;

            for (int i = 1; i < callout.AlarmLevel; i++)
                RequestBackup(ApparatusType.Engine, scene);

            if (callout.Category == CalloutCategory.Medical || callout.Category == CalloutCategory.Traffic)
                RequestBackup(ApparatusType.Ambulance, scene);
        }

        // The player's own extinguisher; the hose nozzle is an extinguisher as well but cools through its water stream.
        // AI crews cool the spot they work on themselves.
        private static IEnumerable<Ped> ExtinguisherUsers()
        {
            Ped player = Game.LocalPlayer.Character;
            if (!Hose.IsHolding && player.Exists())
                yield return player;
        }

        private static void OnFireEscalated(FireIncident incident)
        {
            string alarm = CalloutPriorityExtensions.AlarmToDisplay(incident.AlarmLevel);
            Notify.Dispatch(L.F($"~r~Upgraded to {alarm}"), L.F($"The fire at ~y~{World.GetStreetName(incident.Origin)}~s~ is spreading. Another engine company is responding."));

            if (incident.Owner != null && incident.Owner == Callouts.Current)
            {
                Profiles.OnFireEscalated(incident.Owner);
                Radio.OnFireEscalated(incident);
                RequestBackup(ApparatusType.Engine, incident.Owner.StagingPosition);
            }
        }

        private static void OnFireStatusChanged(FireIncident incident, FireStatus status)
        {
            if (incident.Owner == null || incident.Owner != Callouts.Current)
                return;

            switch (status)
            {
                case FireStatus.UnderControl:
                    Notify.Dispatch(L.T("Fire under control"), L.T("Command reports the fire is ~y~under control~s~. Keep working it."));
                    break;
                case FireStatus.Knockdown:
                    Notify.Dispatch(L.T("Knockdown"), L.T("No open flames left. ~o~Overhaul~s~: soak every hotspot, the thermal camera shows them."));
                    break;
                case FireStatus.Out:
                    Notify.Dispatch(L.T("~g~Fire out"), L.T("All hotspots are overhauled, the fire is out."));
                    Profiles.OnFireOut();
                    break;
            }
        }

        private static void OnPatientMilestone(PatientMilestone milestone, Ped patient)
        {
            switch (milestone)
            {
                case PatientMilestone.Rescued:
                    Profiles.OnPersonRescued();
                    Events.RaisePatientRescued(patient);
                    break;
                case PatientMilestone.Treated:
                    Profiles.OnPatientTreated();
                    Events.RaisePatientStabilized(patient);
                    break;
                case PatientMilestone.Resuscitated:
                    Profiles.OnPatientResuscitated();
                    Events.RaisePatientStabilized(patient);
                    break;
                case PatientMilestone.Transported:
                    Profiles.OnPatientTransported();
                    Events.RaisePatientTransported(patient);
                    break;
            }
        }

        private static void OnFireRekindled(FireIncident incident)
        {
            bool wasBeaten = incident.Status == FireStatus.UnderControl || incident.Status == FireStatus.Knockdown;
            if (wasBeaten && incident.Owner != null && incident.Owner == Callouts.Current)
                Notify.Warning(L.T("~r~Rekindle!~s~ A hotspot burst into flames again."));
        }

        private static void OnCalloutEnded(Callout callout)
        {
            Entry.ReleaseAll();

            foreach (FireApparatus unit in sceneUnits.Where(u => Apparatus.All.Contains(u)).ToList())
            {
                // An ambulance on its way to hospital finishes the transport first.
                if (Transport.IsBusy(unit))
                    Transport.ReturnWhenDone(unit);
                else
                    ReturnApparatus(unit);
            }
            sceneUnits.Clear();
        }

        private static void ReturnAllApparatus()
        {
            if (Apparatus == null)
                return;

            foreach (FireApparatus apparatus in Apparatus.All.ToList())
                ReturnApparatus(apparatus);
        }

        private static void OnDutyStateChanged(bool onDuty)
        {
            Hud.ShowDutyChanged(onDuty);

            if (onDuty)
            {
                Scba.Refill();
            }
            else
            {
                Interactions.Cancel();
                Police.RemoveAll();
                Transport.Shutdown();
                ReturnAllApparatus();
                ThermalCamera.Stow();
                Scba.SetMask(false);
            }
        }
    }
}
