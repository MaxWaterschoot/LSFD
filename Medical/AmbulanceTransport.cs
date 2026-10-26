using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Crew;
using LSFD.UI;
using Rage;
using Rage.Native;

namespace LSFD.Medical
{
    /// <summary>
    /// AI ambulances taking stable patients to hospital. The two paramedics get the stretcher out of the back, wheel it
    /// to the patient, load them up, push them into the ambulance, get in and drive to the nearest hospital, where the
    /// patient is handed over. LSFD calls an ambulance on its own when a patient is waiting and none is on the way.
    /// </summary>
    internal class AmbulanceTransport
    {
        private const uint ThinkInterval = 250;
        private const uint ScanInterval = 2000;
        private const uint RequestCooldown = 20000;
        private const float PatientSearchRadius = 100f;
        private const float AmbulanceCoverRadius = 150f;
        private const int MaxAmbulancesAtOnce = 3;

        private const float WalkSpeed = 1f;
        private const float StretcherReach = 1.4f;
        private const uint UnloadMilliseconds = 2500;
        private const uint LoadMilliseconds = 5000;
        private const uint BoardTimeout = 15000;

        // The ambulance is handed over once it reaches the hospital, or once it is out of sight for a while.
        private const float HandoverDistance = 30f;
        private const float OutOfSightDistance = 350f;
        private const uint MinDriveTime = 45000;

        private const int RearLeftDoor = 2;
        private const int RearRightDoor = 3;
        private const int AttendantSeat = 1;
        private const int PatientSeat = 2;

        private const string PushDictionary = "anim@heists@box_carry@";
        private const string PushAnimation = "idle";
        private const string KneelDictionary = "amb@medic@standing@tendtodead@idle_a";
        private const string KneelAnimation = "idle_a";
        private const string LieDictionary = "anim@gangops@morgue@table@";
        private const string LieAnimation = "body_search";

        private readonly CrewManager crew;
        private readonly ApparatusManager apparatus;
        private readonly PatientManager patients;
        private readonly Action<FireApparatus> returnApparatus;
        private readonly Func<ApparatusType, Vector3, bool> requestBackup;
        private readonly List<Operation> operations = new List<Operation>();
        private readonly HashSet<FireApparatus> returnWhenDone = new HashSet<FireApparatus>();

        private uint nextThink;
        private uint nextScan;
        private uint lastRequest;

        public AmbulanceTransport(CrewManager crew, ApparatusManager apparatus, PatientManager patients,
            Action<FireApparatus> returnApparatus, Func<ApparatusType, Vector3, bool> requestBackup)
        {
            this.crew = crew;
            this.apparatus = apparatus;
            this.patients = patients;
            this.returnApparatus = returnApparatus;
            this.requestBackup = requestBackup;
        }

        /// <summary>Whether the ambulance is loading or driving a patient, so it must not be sent home yet.</summary>
        public bool IsBusy(FireApparatus unit) => operations.Any(o => o.Unit == unit);

        /// <summary>The call is over: send the ambulance home once it has finished its transport.</summary>
        public void ReturnWhenDone(FireApparatus unit)
        {
            returnWhenDone.Add(unit);
        }

        public void Process(bool onDuty)
        {
            if (Game.GameTime < nextThink)
                return;
            nextThink = Game.GameTime + ThinkInterval;

            foreach (Operation operation in operations.ToList())
            {
                if (!Advance(operation))
                    Finish(operation);
            }

            if (!onDuty || !patients.TransportRequired || Game.GameTime < nextScan)
                return;
            nextScan = Game.GameTime + ScanInterval;

            AssignAmbulances();
            RequestAmbulances();
        }

        public void Shutdown()
        {
            foreach (Operation operation in operations.ToList())
                Abort(operation);
        }

        // Ambulances on scene with both paramedics free pick up the nearest waiting patient.
        private void AssignAmbulances()
        {
            foreach (FireApparatus unit in OnSceneAmbulances().Where(u => !IsBusy(u)))
            {
                var medics = crew.Members.Where(m => m.Apparatus == unit && m.IsValid).ToList();
                CrewMember driver = medics.FirstOrDefault(m => m.IsDriver);
                CrewMember attendant = medics.FirstOrDefault(m => !m.IsDriver);
                if (driver == null || attendant == null || driver.Job != CrewJob.None || attendant.Job != CrewJob.None)
                    continue;

                Patient patient = patients.All
                    .Where(p => p.Ped.Exists() && p.AwaitsTransport && p.Ped.DistanceTo(unit.Vehicle.Position) <= PatientSearchRadius)
                    .OrderBy(p => p.TriagePriority)
                    .ThenBy(p => p.Ped.DistanceTo(unit.Vehicle.Position))
                    .FirstOrDefault();
                if (patient == null)
                    continue;

                Start(unit, attendant, driver, patient);
            }
        }

        // Every waiting patient needs an ambulance; call more when there aren't enough on scene or on the way.
        private void RequestAmbulances()
        {
            if (Game.GameTime - lastRequest < RequestCooldown)
                return;

            Vector3 player = Game.LocalPlayer.Character.Position;
            var waiting = patients.All.Where(p => p.Ped.Exists() && p.AwaitsTransport && p.Ped.DistanceTo(player) <= AmbulanceCoverRadius).ToList();
            if (waiting.Count == 0)
                return;

            var ambulances = apparatus.All.Where(a => a.IsValid && a.IsAiUnit && !a.IsCommitted && a.Definition.Type == ApparatusType.Ambulance).ToList();
            int available = ambulances.Count(a => !IsBusy(a) && Covers(a, waiting[0].Ped.Position));
            if (available >= waiting.Count || ambulances.Count >= MaxAmbulancesAtOnce)
                return;

            Vector3 street = World.GetNextPositionOnStreet(waiting[0].Ped.Position);
            if (requestBackup(ApparatusType.Ambulance, street))
            {
                lastRequest = Game.GameTime;
                Notify.Dispatch(L.T("~b~Ambulance requested"), L.T("A patient is ready for transport, an ambulance is on its way."));
            }
        }

        private static bool Covers(FireApparatus ambulance, Vector3 position)
        {
            Vector3 heading = ambulance.ResponseDestination ?? ambulance.Vehicle.Position;
            return heading.DistanceTo(position) <= AmbulanceCoverRadius;
        }

        private IEnumerable<FireApparatus> OnSceneAmbulances()
        {
            return apparatus.All.Where(a => a.IsValid && a.IsAiUnit && !a.IsCommitted && a.Definition.Type == ApparatusType.Ambulance && !a.ResponseDestination.HasValue);
        }

        private void Start(FireApparatus unit, CrewMember attendant, CrewMember driver, Patient patient)
        {
            patient.Transport = TransportState.BeingLoaded;
            patient.Caregiver = attendant.Ped;
            attendant.StartJob(CrewJob.Transport);
            driver.StartJob(CrewJob.Transport);

            operations.Add(new Operation(unit, attendant, driver, patient));
            Game.DisplaySubtitle(L.F($"~b~{attendant.Name}:~s~ Getting the stretcher."), 3000);
        }

        /// <summary>Moves the transport along; returns false when it is over (handed over or aborted).</summary>
        private bool Advance(Operation o)
        {
            if (o.Stage != Stage.Driving && !IsIntact(o))
            {
                Abort(o);
                return false;
            }

            Vehicle ambulance = o.Unit.Vehicle;
            switch (o.Stage)
            {
                case Stage.ToRear:
                    bool attendantThere = GetOutAndMove(o.Attendant, o.Unit.RearPosition, 0.8f);
                    bool driverThere = GetOutAndMove(o.Driver, o.Unit.RearPosition + ambulance.RightVector * 1.2f, 1f);
                    if (attendantThere && (driverThere || o.TimeInStage > BoardTimeout))
                    {
                        OpenRearDoors(ambulance);
                        o.Attendant.Ped.Heading = ambulance.Heading + 180f;
                        o.Attendant.Ped.Tasks.PlayAnimation("mini@repair", "fixing_a_ped", 4f, AnimationFlags.Loop);
                        o.NextStage(Stage.Unloading);
                    }
                    break;

                case Stage.Unloading:
                    if (o.TimeInStage < UnloadMilliseconds)
                        break;
                    if (!SpawnStretcher(o))
                        return AbortWithMessage(o, L.F($"Stretcher model ~y~{Settings.StretcherModel}~s~ doesn't exist, check LSFD.ini."));
                    AttachStretcher(o);
                    o.NextStage(Stage.ToPatient);
                    break;

                case Stage.ToPatient:
                    Vector3 patientPosition = o.Patient.Ped.Position;
                    CrewManager.MoveTo(o.Driver, patientPosition + o.Patient.Ped.RightVector * 1.2f, 0.8f, WalkSpeed);
                    if (CrewManager.MoveTo(o.Attendant, patientPosition, o.StretcherLength / 2f + StretcherReach, WalkSpeed))
                    {
                        PutStretcherDownNextTo(o);
                        Kneel(o.Attendant, patientPosition);
                        Kneel(o.Driver, patientPosition);
                        o.NextStage(Stage.Loading);
                    }
                    break;

                case Stage.Loading:
                    if (o.TimeInStage < LoadMilliseconds)
                        break;
                    PutPatientOnStretcher(o);
                    o.Driver.Ped.Tasks.Clear();
                    AttachStretcher(o);
                    Game.DisplaySubtitle(L.F($"~b~{o.Attendant.Name}:~s~ Patient is on the stretcher, moving to the ambulance."), 3000);
                    o.NextStage(Stage.ToAmbulance);
                    break;

                case Stage.ToAmbulance:
                    CrewManager.MoveTo(o.Driver, o.Unit.RearPosition + ambulance.RightVector * 1.2f, 1f, WalkSpeed);
                    if (CrewManager.MoveTo(o.Attendant, o.Unit.RearPosition - ambulance.ForwardVector * (o.StretcherLength / 2f), 0.8f, WalkSpeed))
                    {
                        LoadIntoAmbulance(o);
                        o.NextStage(Stage.Boarding);
                    }
                    break;

                case Stage.Boarding:
                    bool seated = o.Attendant.Ped.CurrentVehicle == ambulance && o.Driver.Ped.CurrentVehicle == ambulance;
                    if (!seated && o.TimeInStage < BoardTimeout)
                        break;
                    if (!seated)
                    {
                        o.Attendant.Ped.WarpIntoVehicle(ambulance, AttendantSeat);
                        o.Driver.Ped.WarpIntoVehicle(ambulance, -1);
                    }
                    Depart(o);
                    o.NextStage(Stage.Driving);
                    break;

                case Stage.Driving:
                    if (!ambulance.Exists() || !o.Driver.IsValid)
                        return false;
                    bool atHospital = ambulance.DistanceTo(o.Hospital.position) <= HandoverDistance;
                    bool outOfSight = o.TimeInStage > MinDriveTime && ambulance.DistanceTo(Game.LocalPlayer.Character) > OutOfSightDistance;
                    if (atHospital || outOfSight)
                    {
                        HandOver(o);
                        return false;
                    }
                    break;
            }

            return true;
        }

        private bool IsIntact(Operation o)
        {
            return o.Unit.IsValid && o.Attendant.IsValid && o.Driver.IsValid
                && o.Attendant.Job == CrewJob.Transport && o.Driver.Job == CrewJob.Transport
                && o.Patient.Ped.Exists() && patients.Find(o.Patient.Ped) == o.Patient;
        }

        private static bool GetOutAndMove(CrewMember member, Vector3 target, float distance)
        {
            Ped ped = member.Ped;
            if (ped.IsInAnyVehicle(false))
            {
                if (!ped.IsInAnyVehicle(true) || Game.GameTime - member.LeaveVehicleIssuedAt > 4000)
                {
                    ped.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
                    member.LeaveVehicleIssuedAt = Game.GameTime;
                }
                return false;
            }
            return CrewManager.MoveTo(member, target, distance, WalkSpeed);
        }

        private static void OpenRearDoors(Vehicle ambulance)
        {
            NativeFunction.CallByName<uint>("SET_VEHICLE_DOOR_OPEN", ambulance, RearLeftDoor, false, false);
            NativeFunction.CallByName<uint>("SET_VEHICLE_DOOR_OPEN", ambulance, RearRightDoor, false, false);
        }

        private static bool SpawnStretcher(Operation o)
        {
            Model model = Settings.StretcherModel;
            if (!model.IsValid)
                return false;

            model.LoadAndWait();
            o.Stretcher = new Rage.Object(model, o.Unit.RearPosition) { IsPersistent = true };
            model.Dismiss();

            // The long side of the stretcher points the way it is pushed.
            Vector3 size = model.Dimensions;
            o.StretcherLength = Math.Abs(Settings.StretcherHeading % 180f) < 45f ? size.Y : size.X;
            o.StretcherHeight = size.Z;
            NativeFunction.CallByName<uint>("SET_ENTITY_COLLISION", o.Stretcher, false, false);
            return true;
        }

        /// <summary>The attendant pushes the stretcher in front of them; the patient, if on it, comes along.</summary>
        private static void AttachStretcher(Operation o)
        {
            float forward = o.StretcherLength / 2f + 0.35f;
            o.Stretcher.AttachTo(o.Attendant.Ped, 0, new Vector3(0f, forward, -1f), new Rotator(0f, 0f, Settings.StretcherHeading));
            o.Attendant.Ped.Tasks.PlayAnimation(PushDictionary, PushAnimation, 4f,
                AnimationFlags.Loop | AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);
        }

        private static void PutStretcherDownNextTo(Operation o)
        {
            o.Stretcher.Detach();
            Ped patient = o.Patient.Ped;
            Vector3 position = patient.Position + patient.RightVector * 1f;
            float? ground = World.GetGroundZ(position + new Vector3(0f, 0f, 1f), false, false);
            o.Stretcher.Position = new Vector3(position.X, position.Y, ground ?? patient.Position.Z - 1f);
            o.Stretcher.Heading = o.Attendant.Ped.Heading + Settings.StretcherHeading;
        }

        private static void Kneel(CrewMember member, Vector3 patientPosition)
        {
            Ped ped = member.Ped;
            ped.Heading = MathHelper.ConvertDirectionToHeading(patientPosition - ped.Position);
            ped.Tasks.PlayAnimation(KneelDictionary, KneelAnimation, 4f, AnimationFlags.Loop);
        }

        private static void PutPatientOnStretcher(Operation o)
        {
            Ped patient = o.Patient.Ped;
            patient.Tasks.ClearImmediately();
            patient.AttachTo(o.Stretcher, 0, new Vector3(0f, 0f, o.StretcherHeight + Settings.PatientOnStretcherHeight), new Rotator(0f, 0f, 0f));
            patient.Tasks.PlayAnimation(LieDictionary, LieAnimation, 8f, AnimationFlags.Loop);
        }

        private void LoadIntoAmbulance(Operation o)
        {
            Vehicle ambulance = o.Unit.Vehicle;
            Ped patient = o.Patient.Ped;

            DeleteStretcher(o);
            patient.Detach();
            patient.Tasks.ClearImmediately();
            patient.WarpIntoVehicle(ambulance, PatientSeat);
            o.Patient.Transport = TransportState.Loaded;
            o.Patient.Caregiver = null;

            o.Attendant.Ped.Tasks.Clear();
            o.Attendant.Ped.Tasks.EnterVehicle(ambulance, AttendantSeat);
            o.Driver.Ped.Tasks.EnterVehicle(ambulance, -1);
        }

        private void Depart(Operation o)
        {
            Vehicle ambulance = o.Unit.Vehicle;
            NativeFunction.CallByName<uint>("SET_VEHICLE_DOORS_SHUT", ambulance, false);

            o.Hospital = Hospitals.GetNearest(ambulance.Position);
            // Resuscitated patients go in with lights and siren.
            bool urgent = o.Patient.WasResuscitated;
            ambulance.IsSirenOn = urgent;
            ambulance.IsSirenSilent = false;
            o.Driver.Ped.Tasks.DriveToPosition(ambulance, o.Hospital.position, urgent ? 25f : 15f,
                urgent ? VehicleDrivingFlags.Emergency : VehicleDrivingFlags.Normal, HandoverDistance - 5f);

            Notify.Dispatch(L.T("~b~Patient transport"), L.F($"~y~{o.Unit.Definition.Name}~s~ is transporting a patient to {o.Hospital.name}") +
                (urgent ? L.T(" (~r~Code 3~s~).") : "."));
        }

        private void HandOver(Operation o)
        {
            Ped patient = o.Patient.Ped;
            patients.Release(patient);
            // Before deleting, so add-ons still get a valid ped.
            patients.ReportTransported(patient);
            if (patient.Exists())
                patient.Delete();

            Notify.Dispatch(L.T("~g~Patient handed over"), L.F($"The patient was handed over at {o.Hospital.name}."));
            returnWhenDone.Add(o.Unit);
        }

        private bool AbortWithMessage(Operation o, string message)
        {
            Notify.Warning(message);
            Abort(o);
            return false;
        }

        /// <summary>Something got in the way (e.g. the crew got another order): put the patient back down.</summary>
        private void Abort(Operation o)
        {
            DeleteStretcher(o);

            Ped patient = o.Patient.Ped;
            if (patient.Exists() && o.Patient.Transport == TransportState.BeingLoaded)
            {
                patient.Detach();
                o.Patient.Transport = TransportState.None;
                if (o.Patient.Caregiver == o.Attendant.Ped)
                    o.Patient.Caregiver = null;
                patients.ApplyPose(o.Patient);
            }

            foreach (CrewMember member in new[] { o.Attendant, o.Driver }.Where(m => m.Job == CrewJob.Transport))
                crew.Command(member, CrewOrder.TreatPatients);

            operations.Remove(o);
            Finish(o);
        }

        private void Finish(Operation o)
        {
            DeleteStretcher(o);
            operations.Remove(o);

            if (returnWhenDone.Remove(o.Unit))
                returnApparatus(o.Unit);
        }

        private static void DeleteStretcher(Operation o)
        {
            if (o.Stretcher.Exists())
                o.Stretcher.Delete();
            o.Stretcher = null;
        }

        private enum Stage
        {
            ToRear,
            Unloading,
            ToPatient,
            Loading,
            ToAmbulance,
            Boarding,
            Driving,
        }

        private sealed class Operation
        {
            public Operation(FireApparatus unit, CrewMember attendant, CrewMember driver, Patient patient)
            {
                Unit = unit;
                Attendant = attendant;
                Driver = driver;
                Patient = patient;
                StageStartedAt = Game.GameTime;
            }

            public FireApparatus Unit { get; }

            /// <summary>Pushes the stretcher and rides in the back with the patient.</summary>
            public CrewMember Attendant { get; }

            public CrewMember Driver { get; }
            public Patient Patient { get; }

            public Stage Stage { get; private set; } = Stage.ToRear;
            public uint StageStartedAt { get; private set; }
            public uint TimeInStage => Game.GameTime - StageStartedAt;

            public Rage.Object Stretcher { get; set; }
            public float StretcherLength { get; set; } = 2f;
            public float StretcherHeight { get; set; } = 0.8f;
            public (string name, Vector3 position) Hospital { get; set; }

            public void NextStage(Stage stage)
            {
                Stage = stage;
                StageStartedAt = Game.GameTime;
                Attendant.NextPhase();
                Driver.NextPhase();
            }
        }
    }
}
