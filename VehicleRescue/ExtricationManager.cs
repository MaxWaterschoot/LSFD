using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.UI;
using Rage;
using Rage.Native;

namespace LSFD.VehicleRescue
{
    /// <summary>
    /// Vehicle rescue: breaking windows, stabilizing vehicles, forcing and cutting doors and extricating trapped patients.
    /// Works on any vehicle; patients trapped through <see cref="API.Rescue"/> can only be reached once a door is open.
    /// </summary>
    internal class ExtricationManager : IInteractionProvider
    {
        private const float VehicleSearchRadius = 6f;
        private const float DoorReach = 1.6f;
        private const float WindowReach = 1.4f;
        private const float CribbingReach = 3f;
        private const float ExtricateSeconds = 4f;
        private const float DoorOpenRatio = 0.5f;

        private class TrappedPatient
        {
            public Ped Ped;
            public Vehicle Vehicle;
            public int Door;
            public bool Extricated;
            public Ped Rescuer;
        }

        private readonly ToolManager tools;
        private readonly ApparatusManager apparatus;
        private readonly List<TrappedPatient> patients = new List<TrappedPatient>();
        private readonly HashSet<Vehicle> stabilized = new HashSet<Vehicle>();

        public event Action<Ped> PatientExtricated;

        public ExtricationManager(ToolManager tools, ApparatusManager apparatus)
        {
            this.tools = tools;
            this.apparatus = apparatus;
        }

        public void Trap(Ped ped, Vehicle vehicle, int seat)
        {
            ped.WarpIntoVehicle(vehicle, seat);
            ped.IsPersistent = true;
            ped.BlockPermanentEvents = true;
            ped.Health = Math.Min(ped.Health, 140);
            NativeFunction.CallByName<uint>("SET_PED_CAN_BE_DRAGGED_OUT", ped, false);
            NativeFunction.CallByName<uint>("SET_VEHICLE_DOORS_LOCKED", vehicle, 4);

            patients.Add(new TrappedPatient { Ped = ped, Vehicle = vehicle, Door = VehicleParts.DoorForSeat(seat) });
        }

        public bool IsTrapped(Ped ped) => patients.Any(p => p.Ped == ped && !p.Extricated);
        public bool IsExtricated(Ped ped) => patients.Any(p => p.Ped == ped && p.Extricated);
        public bool IsStabilized(Vehicle vehicle) => stabilized.Contains(vehicle);

        /// <summary>A trapped patient near <paramref name="position"/> that no crew member is working on yet, or null.</summary>
        public Ped FindUnclaimedTrapped(Vector3 position, float radius)
        {
            return patients
                .Where(p => !p.Extricated && p.Rescuer == null && p.Ped.Exists() && p.Vehicle.Exists() && p.Vehicle.DistanceTo(position) <= radius)
                .OrderBy(p => p.Vehicle.DistanceTo(position))
                .Select(p => p.Ped)
                .FirstOrDefault();
        }

        /// <summary>Where a rescuer stands to work on the door of a trapped patient.</summary>
        public Vector3 GetWorkPosition(Ped ped)
        {
            TrappedPatient patient = patients.First(p => p.Ped == ped);
            Vector3 door = patient.Vehicle.GetBonePosition(VehicleParts.DoorBones[patient.Door]);
            return door + (door - patient.Vehicle.Position).ToNormalized() * 0.8f;
        }

        public void Claim(Ped ped, Ped rescuer)
        {
            TrappedPatient patient = patients.FirstOrDefault(p => p.Ped == ped);
            if (patient != null)
                patient.Rescuer = rescuer;
        }

        /// <summary>A crew member finished working on the vehicle: stabilize it, take the door off and get the patient out.</summary>
        public void ExtricateByCrew(Ped ped)
        {
            TrappedPatient patient = patients.FirstOrDefault(p => p.Ped == ped && !p.Extricated);
            if (patient == null || !patient.Vehicle.Exists())
                return;

            if (!IsStabilized(patient.Vehicle))
            {
                NativeFunction.CallByName<uint>("FREEZE_ENTITY_POSITION", patient.Vehicle, true);
                stabilized.Add(patient.Vehicle);
            }
            if (!IsDoorAccessible(patient.Vehicle, patient.Door))
                CutDoor(patient.Vehicle, patient.Door);

            Extricate(patient);
        }

        public void Release(Ped ped)
        {
            patients.RemoveAll(p => p.Ped == ped);
        }

        public void ReleaseVehicle(Vehicle vehicle)
        {
            if (vehicle.Exists())
                NativeFunction.CallByName<uint>("FREEZE_ENTITY_POSITION", vehicle, false);
            stabilized.Remove(vehicle);
        }

        public void Process()
        {
            patients.RemoveAll(p => !p.Ped.Exists());
            stabilized.RemoveWhere(v => !v.Exists());
        }

        public Interaction GetInteraction()
        {
            Ped player = Game.LocalPlayer.Character;
            if (player.IsInAnyVehicle(false))
                return null;

            Vehicle vehicle = FindWorkVehicle(player.Position);
            if (vehicle == null)
                return null;

            return GetPatientInteraction(vehicle, player.Position) ?? GetToolInteraction(vehicle, player.Position);
        }

        private Interaction GetPatientInteraction(Vehicle vehicle, Vector3 playerPosition)
        {
            foreach (TrappedPatient patient in patients.Where(p => p.Vehicle == vehicle && !p.Extricated))
            {
                Vector3 doorPosition = vehicle.GetBonePosition(VehicleParts.DoorBones[patient.Door]);
                if (doorPosition.DistanceTo(playerPosition) > DoorReach)
                    continue;

                if (IsDoorAccessible(vehicle, patient.Door))
                    return new Interaction("extricate the ~r~patient", () => Extricate(patient), ExtricateSeconds, "EXTRICATING")
                    {
                        AnimationDictionary = "mini@repair",
                        AnimationName = "fixing_a_ped",
                    };

                if (tools.Held == null)
                    return Interaction.Hint("The door is ~r~jammed~s~. Get the ~y~hydraulic tools~s~ from an apparatus.");
            }
            return null;
        }

        private Interaction GetToolInteraction(Vehicle vehicle, Vector3 playerPosition)
        {
            ToolDefinition tool = tools.Held;
            if (tool == null)
                return null;

            bool hasPatients = patients.Any(p => p.Vehicle == vehicle && !p.Extricated);
            bool needsCribbing = hasPatients && !IsStabilized(vehicle);

            switch (tool.Type)
            {
                case EquipmentType.WindowPunch:
                    return GetWindowInteraction(vehicle, playerPosition, tool);

                case EquipmentType.Cribbing:
                    if (IsStabilized(vehicle) || vehicle.DistanceTo(playerPosition) > CribbingReach)
                        return null;
                    return WithAnimation(new Interaction("stabilize the vehicle with ~y~cribbing", () => Stabilize(vehicle), tool.UseSeconds, "STABILIZING"), tool);

                case EquipmentType.HydraulicSpreader:
                case EquipmentType.HydraulicCutter:
                    int door = VehicleParts.FindClosest(vehicle, VehicleParts.DoorBones, playerPosition, DoorReach, out _);
                    if (door < 0 || !vehicle.Doors[door].IsValid() || vehicle.Doors[door].IsDamaged)
                        return null;
                    if (needsCribbing)
                        return Interaction.Hint("~o~Stabilize the vehicle with cribbing~s~ before using hydraulic tools.");

                    return tool.Type == EquipmentType.HydraulicSpreader
                        ? IsDoorAccessible(vehicle, door) ? null : WithAnimation(new Interaction("force the door open", () => ForceDoor(vehicle, door), tool.UseSeconds, "SPREADING"), tool)
                        : WithAnimation(new Interaction("cut the door off", () => CutDoor(vehicle, door), tool.UseSeconds, "CUTTING"), tool);

                default:
                    return null;
            }
        }

        private static Interaction GetWindowInteraction(Vehicle vehicle, Vector3 playerPosition, ToolDefinition tool)
        {
            int window = VehicleParts.FindClosest(vehicle, VehicleParts.WindowBones, playerPosition, WindowReach, out _);
            if (window >= 0 && IsWindowIntact(vehicle, window))
                return WithAnimation(new Interaction("break the window", () => SmashWindow(vehicle, window), tool.UseSeconds, "BREAKING"), tool);

            bool nearWindscreen = vehicle.HasBone(VehicleParts.WindscreenBone)
                && vehicle.GetBonePosition(VehicleParts.WindscreenBone).DistanceTo(playerPosition) <= WindowReach + 0.6f;
            if (nearWindscreen && IsWindowIntact(vehicle, VehicleParts.WindscreenIndex))
                return WithAnimation(new Interaction("break the windscreen", () => SmashWindow(vehicle, VehicleParts.WindscreenIndex), tool.UseSeconds * 2f, "BREAKING"), tool);

            return null;
        }

        private Vehicle FindWorkVehicle(Vector3 position)
        {
            return World.GetEntities(position, VehicleSearchRadius, GetEntitiesFlags.ConsiderAllVehicles)
                .OfType<Vehicle>()
                .Where(v => v.Exists() && apparatus.All.All(a => a.Vehicle != v))
                .OrderBy(v => v.DistanceTo(position))
                .FirstOrDefault();
        }

        private void Stabilize(Vehicle vehicle)
        {
            NativeFunction.CallByName<uint>("FREEZE_ENTITY_POSITION", vehicle, true);
            stabilized.Add(vehicle);
            Notify.Info("Vehicle ~g~stabilized~s~. It is safe to use the hydraulic tools.");
        }

        private static void ForceDoor(Vehicle vehicle, int door)
        {
            vehicle.Doors[door].Open(false);
        }

        private static void CutDoor(Vehicle vehicle, int door)
        {
            vehicle.Doors[door].BreakOff();
        }

        private static void SmashWindow(Vehicle vehicle, int window)
        {
            NativeFunction.CallByName<uint>("SMASH_VEHICLE_WINDOW", vehicle, window);
        }

        private static bool IsWindowIntact(Vehicle vehicle, int window)
        {
            return NativeFunction.CallByName<bool>("IS_VEHICLE_WINDOW_INTACT", vehicle, window);
        }

        private static bool IsDoorAccessible(Vehicle vehicle, int door)
        {
            VehicleDoor vehicleDoor = vehicle.Doors[door];
            return !vehicleDoor.IsValid() || vehicleDoor.IsDamaged || vehicleDoor.AngleRatio >= DoorOpenRatio;
        }

        private void Extricate(TrappedPatient patient)
        {
            Ped ped = patient.Ped;
            Vector3 outside = patient.Vehicle.GetBonePosition(VehicleParts.DoorBones[patient.Door]);
            Vector3 away = (outside - patient.Vehicle.Position).ToNormalized() * 1.5f;

            ped.Tasks.ClearImmediately();
            ped.Position = outside + away;
            ped.Tasks.PlayAnimation("combat@damage@writhe", "writhe_loop", 1f, AnimationFlags.Loop);

            patient.Extricated = true;
            Notify.Info("Patient ~g~extricated~s~. Treat their injuries.");
            PatientExtricated?.Invoke(ped);
        }

        private static Interaction WithAnimation(Interaction interaction, ToolDefinition tool)
        {
            interaction.AnimationDictionary = tool.AnimDictionary;
            interaction.AnimationName = tool.AnimName;
            return interaction;
        }
    }
}
