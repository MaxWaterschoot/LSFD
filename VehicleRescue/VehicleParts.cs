using Rage;

namespace LSFD.VehicleRescue
{
    /// <summary>Door and window bones of a vehicle and their indices.</summary>
    internal static class VehicleParts
    {
        public static readonly string[] DoorBones = { "door_dside_f", "door_pside_f", "door_dside_r", "door_pside_r" };
        public static readonly string[] WindowBones = { "window_lf", "window_rf", "window_lr", "window_rr" };

        public const string WindscreenBone = "windscreen";
        public const int WindscreenIndex = 6;

        /// <summary>The door next to a seat (-1 = driver, 0 = front passenger, 1/2 = rear).</summary>
        public static int DoorForSeat(int seat) => seat + 1;

        /// <summary>Index and position of the closest door/window bone within <paramref name="maxDistance"/>, or -1.</summary>
        public static int FindClosest(Vehicle vehicle, string[] bones, Vector3 position, float maxDistance, out Vector3 bonePosition)
        {
            int closest = -1;
            bonePosition = Vector3.Zero;
            float closestDistance = maxDistance;

            for (int i = 0; i < bones.Length; i++)
            {
                if (!vehicle.HasBone(bones[i]))
                    continue;

                Vector3 bone = vehicle.GetBonePosition(bones[i]);
                float distance = bone.DistanceTo(position);
                if (distance < closestDistance)
                {
                    closest = i;
                    closestDistance = distance;
                    bonePosition = bone;
                }
            }

            return closest;
        }
    }
}
