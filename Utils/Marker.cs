using System.Drawing;
using Rage;
using Rage.Native;

namespace LSFD.Utils
{
    /// <summary>The classic GTA mission marker: a glowing cylinder on the ground with a bobbing arrow above it.</summary>
    internal class Marker
    {
        private const int CylinderType = 1;
        private const int ArrowType = 0;
        private const float DrawDistance = 60f;

        private readonly float radius;
        private readonly Color color;

        // The ground can only be found once the area is streamed in, so it is looked up when needed and kept once found.
        private Vector3? ground;

        public Vector3 Position { get; }

        public Marker(Vector3 position, float radius, Color color)
        {
            Position = position;
            this.radius = radius;
            this.color = color;
        }

        private Vector3 GroundPosition
        {
            get
            {
                if (ground.HasValue)
                    return ground.Value;

                float? z = World.GetGroundZ(Position + new Vector3(0f, 0f, 1f), false, false);
                Vector3 position = new Vector3(Position.X, Position.Y, (z ?? Position.Z - 1f) - 0.05f);
                if (z.HasValue)
                    ground = position;
                return position;
            }
        }

        public bool Contains(Vector3 position)
        {
            if (position.DistanceTo2D(Position) > radius)
                return false;

            return System.Math.Abs(position.Z - GroundPosition.Z) < 2.5f;
        }

        /// <summary>Draws the marker; call every frame.</summary>
        public void Draw(Vector3 cameraPosition)
        {
            if (cameraPosition.DistanceTo(Position) > DrawDistance)
                return;

            Vector3 groundPosition = GroundPosition;
            DrawMarker(CylinderType, groundPosition, new Vector3(radius * 2f, radius * 2f, 0.8f), Color.FromArgb(110, color), false);
            DrawMarker(ArrowType, groundPosition + new Vector3(0f, 0f, 1.6f), new Vector3(0.45f, 0.45f, 0.45f), Color.FromArgb(200, color), true);
        }

        private static void DrawMarker(int type, Vector3 position, Vector3 scale, Color color, bool bob)
        {
            NativeFunction.CallByName<uint>("DRAW_MARKER", type,
                position.X, position.Y, position.Z,
                0f, 0f, 0f,
                bob ? 180f : 0f, 0f, 0f,
                scale.X, scale.Y, scale.Z,
                (int)color.R, (int)color.G, (int)color.B, (int)color.A,
                bob, false, 2, bob, 0, 0, false);
        }
    }
}
