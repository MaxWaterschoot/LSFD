using Rage;
using Rage.Native;

namespace LSFD.Utils
{
    internal static class GameplayCamera
    {
        public static Vector3 Position => NativeFunction.CallByName<Vector3>("GET_GAMEPLAY_CAM_COORD");

        public static Rotator Rotation
        {
            get
            {
                Vector3 rotation = NativeFunction.CallByName<Vector3>("GET_GAMEPLAY_CAM_ROT", 2);
                return new Rotator(rotation.X, rotation.Y, rotation.Z);
            }
        }

        public static Vector3 Direction => Rotation.ToVector();
    }
}
