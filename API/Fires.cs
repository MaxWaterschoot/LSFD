using System.Linq;
using Rage;
using Rage.Native;

namespace LSFD.API
{
    /// <summary>Helpers for starting and checking fires in callouts.</summary>
    public static class Fires
    {
        /// <summary>Starts a fire on the ground at <paramref name="position"/> and returns its script handle.</summary>
        /// <param name="position">Where to start the fire; it is placed on the ground below this point.</param>
        /// <param name="maxChildren">How far the fire may spread (0-25).</param>
        /// <param name="isGasFire">Gas fires burn hotter and spread faster.</param>
        public static int StartFire(Vector3 position, int maxChildren = 25, bool isGasFire = false)
        {
            float z = World.GetGroundZ(position, false, false) ?? position.Z;
            return NativeFunction.CallByName<int>("START_SCRIPT_FIRE", position.X, position.Y, z, maxChildren, isGasFire);
        }

        /// <summary>Removes a fire started with <see cref="StartFire"/>.</summary>
        public static void RemoveFire(int handle)
        {
            NativeFunction.CallByName<uint>("REMOVE_SCRIPT_FIRE", handle);
        }

        /// <summary>Sets an entity (vehicle, ped or object) on fire.</summary>
        public static void SetOnFire(Entity entity)
        {
            NativeFunction.CallByName<int>("START_ENTITY_FIRE", entity);
        }

        /// <summary>Puts out every fire within <paramref name="radius"/>.</summary>
        public static void ExtinguishInRange(Vector3 position, float radius)
        {
            NativeFunction.CallByName<uint>("STOP_FIRE_IN_RANGE", position.X, position.Y, position.Z, radius);
        }

        /// <summary>Number of fires burning within <paramref name="radius"/>, handy to check whether a scene is under control.</summary>
        public static int CountFiresInRange(Vector3 position, float radius)
        {
            return World.GetAllFires().Count(f => f.IsValid() && f.Position.DistanceTo(position) <= radius);
        }
    }
}
