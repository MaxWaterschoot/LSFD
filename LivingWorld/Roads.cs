using Rage;
using Rage.Native;

namespace LSFD.LivingWorld
{
    /// <summary>Road and visibility helpers for placing things in the world.</summary>
    internal static class Roads
    {
        /// <summary>The nearest road node and the direction the road runs there.</summary>
        public static bool TryGetRoad(Vector3 position, out Vector3 node, out Vector3 direction)
        {
            Vector3 nodePosition;
            float heading;
            bool found = NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(
                position.X, position.Y, position.Z, out nodePosition, out heading, 1, 3f, 0);

            node = nodePosition;
            direction = MathHelper.ConvertHeadingToDirection(heading);
            return found;
        }

        /// <summary>Whether any part of the sphere is in front of the gameplay camera.</summary>
        public static bool IsOnScreen(Vector3 position, float radius)
        {
            return NativeFunction.Natives.IS_SPHERE_VISIBLE<bool>(position.X, position.Y, position.Z, radius);
        }

        /// <summary>Blaine County and the other areas outside the city, where the sheriff patrols.</summary>
        public static bool IsCounty(Vector3 position)
        {
            return position.Y > 1200f || position.X < -2200f || position.X > 2000f;
        }
    }

    /// <summary>
    /// An area where AI traffic slows down or stops, as used by traffic control plugins. Speed 0 stops traffic completely.
    /// </summary>
    internal static class SpeedZone
    {
        // ADD_ROAD_NODE_SPEED_ZONE and REMOVE_ROAD_NODE_SPEED_ZONE; by hash, older RPH builds know them by other names.
        private const ulong AddHash = 0x2CE544C68FB812A0;
        private const ulong RemoveHash = 0x1033371FC8E842A7;

        public static int Add(Vector3 position, float radius, float speed)
        {
            return NativeFunction.CallByHash<int>(AddHash, position.X, position.Y, position.Z, radius, speed, false);
        }

        public static void Remove(int zone)
        {
            NativeFunction.CallByHash<bool>(RemoveHash, zone);
        }
    }
}
