using System.Drawing;
using Rage;
using Rage.Native;

namespace LSFD.Utils
{
    internal static class Draw
    {
        private static readonly Color HoseColor = Color.FromArgb(255, 200, 40, 30);

        /// <summary>Draws a hose that runs from <paramref name="from"/> down to the ground, along it, and back up to <paramref name="to"/>.</summary>
        public static void Hose(Vector3 from, Vector3 to)
        {
            Vector3 fromGround = OnGround(from);
            Vector3 toGround = OnGround(to);

            Line(from, fromGround, HoseColor);
            Line(fromGround, toGround, HoseColor);
            Line(toGround, to, HoseColor);
        }

        public static void Line(Vector3 a, Vector3 b, Color color)
        {
            NativeFunction.CallByName<uint>("DRAW_LINE", a.X, a.Y, a.Z, b.X, b.Y, b.Z, (int)color.R, (int)color.G, (int)color.B, (int)color.A);
        }

        private static Vector3 OnGround(Vector3 position)
        {
            float? z = World.GetGroundZ(position, false, false);
            return new Vector3(position.X, position.Y, (z ?? position.Z - 1f) + 0.05f);
        }
    }
}
