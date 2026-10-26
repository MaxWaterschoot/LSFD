using System;
using LSFD.Core;
using Rage;
using Rage.Native;

namespace LSFD.API
{
    /// <summary>Vehicle rescue helpers for callouts: crashing vehicles and trapping patients that need to be extricated.</summary>
    public static class Rescue
    {
        /// <summary>Raised when the player extricates a trapped patient.</summary>
        public static event Action<Ped> OnPatientExtricated
        {
            add => Lsfd.Extrication.PatientExtricated += value;
            remove => Lsfd.Extrication.PatientExtricated -= value;
        }

        /// <summary>
        /// Puts <paramref name="ped"/> in <paramref name="vehicle"/> and traps them: the doors are jammed until the player
        /// forces or cuts the door next to their seat, and then extricates them.
        /// </summary>
        /// <param name="ped">The patient.</param>
        /// <param name="vehicle">The crashed vehicle.</param>
        /// <param name="seat">-1 for the driver, 0 for the front passenger, 1 and 2 for the rear seats.</param>
        public static void TrapInVehicle(Ped ped, Vehicle vehicle, int seat = -1) => Lsfd.Extrication.Trap(ped, vehicle, seat);

        public static bool IsTrapped(Ped ped) => Lsfd.Extrication.IsTrapped(ped);

        public static bool IsExtricated(Ped ped) => Lsfd.Extrication.IsExtricated(ped);

        /// <summary>Whether the player stabilized the vehicle with cribbing.</summary>
        public static bool IsStabilized(Vehicle vehicle) => Lsfd.Extrication.IsStabilized(vehicle);

        /// <summary>Stops tracking a patient and unfreezes their vehicle; call this when your callout ends.</summary>
        public static void Release(Ped ped, Vehicle vehicle)
        {
            Lsfd.Extrication.Release(ped);
            Lsfd.Extrication.ReleaseVehicle(vehicle);
        }

        /// <summary>Makes a vehicle look like it was in a crash: crumpled where it was hit, broken glass and a damaged engine.</summary>
        /// <param name="vehicle">The vehicle to damage.</param>
        /// <param name="impactDirection">Where the impact was, relative to the vehicle (e.g. 0,1,0 for a frontal crash).</param>
        /// <param name="severity">0 to 1.</param>
        public static void CrashVehicle(Vehicle vehicle, Vector3 impactDirection, float severity = 0.8f)
        {
            Vector3 dimensions = vehicle.Model.Dimensions;
            Vector3 impact = new Vector3(impactDirection.X * dimensions.X / 2f, impactDirection.Y * dimensions.Y / 2f, 0.2f);

            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = impact + new Vector3(MathHelper.GetRandomSingle(-0.4f, 0.4f), MathHelper.GetRandomSingle(-0.3f, 0.3f), 0f);
                NativeFunction.CallByName<uint>("SET_VEHICLE_DAMAGE", vehicle, offset.X, offset.Y, offset.Z, 400f * severity, 150f, true);
            }

            vehicle.EngineHealth = 1000f - 800f * severity;
            if (severity > 0.5f)
                NativeFunction.CallByName<uint>("SMASH_VEHICLE_WINDOW", vehicle, 6);
        }
    }
}
