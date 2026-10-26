using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Buildings;
using LSFD.Crew;
using LSFD.Firefighting;
using LSFD.Medical;
using LSFD.Profiles;
using LSFD.Safety;
using LSFD.Stations;
using LSFD.UI;
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

        // AI units sent to the current call; they go back to quarters when it ends.
        private static readonly List<FireApparatus> sceneUnits = new List<FireApparatus>();

        public static void Initialize()
        {
            Settings.Load();
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

            // In order of priority: the first one with something to offer gets the interact key.
            SceneActions = new SceneActionManager(Tools);
            Interactions = new InteractionRunner(Patients, SceneActions, Fire, Entry, Ventilation, Hydrants, Extrication, Apparatus, Stations);

            Menus = new MenuManager(Duty, Apparatus, Hose, Hydrants, Tools, Scba, ThermalCamera, Ventilation, Profiles);
            Tablet = new Tablet(Duty, Callouts, Apparatus, Crew, Stations, Patients, Fire, Profiles, ReturnApparatus, RequestBackup);
            Hud = new Hud();

            Stations.StationMenuRequested += Menus.OpenStationMenu;
            Apparatus.CompartmentMenuRequested += Menus.OpenCompartmentMenu;
            Apparatus.Spawned += Crew.SpawnCrew;
            Callouts.CalloutCompleted += Hud.ShowCalloutCompleted;
            Callouts.CalloutAccepted += OnCalloutAccepted;
            Callouts.CalloutEnded += OnCalloutEnded;
            Duty.DutyStateChanged += OnDutyStateChanged;
            Extrication.PatientExtricated += ped => Patients.Register(ped, PatientCondition.Injured);
            StationLife = new StationLife(Apparatus, Crew, Duty);
            Duty.DutyStateChanged += StationLife.OnDutyStateChanged;
            Callouts.CalloutAccepted += StationLife.OnCalloutAccepted;
            Callouts.CalloutAccepted += Profiles.OnCalloutAccepted;
            Callouts.CalloutEnded += Profiles.OnCalloutEnded;
            Patients.Milestone += OnPatientMilestone;
            Fire.Escalated += OnFireEscalated;
            Fire.StatusChanged += OnFireStatusChanged;
            Fire.Rekindled += OnFireRekindled;

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

            if (!uiOpen)
                Interactions.Process();

            Hud.Process(Hose, Hydrants, Apparatus, Tools, Interactions, Fire, Scba, Hazards, ThermalCamera, Duty.IsOnDuty);
        }

        public static void Shutdown()
        {
            Menus?.CloseAll();
            Tablet?.Close();
            Callouts?.EndCurrent(null);
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
            FireStation station = Stations.GetNearestStation(destination);
            if (definition == null || station == null)
            {
                Notify.Dispatch("~o~No units", $"There is no {type} available in Apparatus.xml.");
                return false;
            }

            FireApparatus unit = Apparatus.SpawnResponding(definition, station, destination);
            if (unit != null)
                sceneUnits.Add(unit);
            return unit != null;
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
            Notify.Dispatch($"~r~Upgraded to {alarm}", $"The fire at ~y~{World.GetStreetName(incident.Origin)}~s~ is spreading. Another engine company is responding.");

            if (incident.Owner != null && incident.Owner == Callouts.Current)
                RequestBackup(ApparatusType.Engine, incident.Owner.StagingPosition);
        }

        private static void OnFireStatusChanged(FireIncident incident, FireStatus status)
        {
            if (incident.Owner == null || incident.Owner != Callouts.Current)
                return;

            switch (status)
            {
                case FireStatus.UnderControl:
                    Notify.Dispatch("Fire under control", "Command reports the fire is ~y~under control~s~. Keep working it.");
                    break;
                case FireStatus.Knockdown:
                    Notify.Dispatch("Knockdown", "No open flames left. ~o~Overhaul~s~: soak every hotspot, the thermal camera shows them.");
                    break;
                case FireStatus.Out:
                    Notify.Dispatch("~g~Fire out", "All hotspots are overhauled, the fire is out.");
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
                Notify.Warning("~r~Rekindle!~s~ A hotspot burst into flames again.");
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
                Transport.Shutdown();
                ReturnAllApparatus();
                ThermalCamera.Stow();
                Scba.SetMask(false);
            }
        }
    }
}
