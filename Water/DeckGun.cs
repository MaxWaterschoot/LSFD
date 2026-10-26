using LSFD.API;
using LSFD.Apparatus;
using LSFD.Firefighting;
using LSFD.Utils;
using Rage;

namespace LSFD.Water
{
    /// <summary>
    /// The roof-mounted water cannon of the player's apparatus. GTA's cannon only puts out GTA fires, so this cools the
    /// LSFD fire points along where it is aimed (it follows the camera, like the cannon itself).
    /// </summary>
    internal class DeckGun
    {
        private const float Range = 60f;
        private const float StreamRadius = 2f;
        private const float ImpactRadius = 3.5f;
        private const float CoolingPerSecond = 1.6f;

        private readonly FireSystem fire;
        private readonly ApparatusManager apparatus;

        public DeckGun(FireSystem fire, ApparatusManager apparatus)
        {
            this.fire = fire;
            this.apparatus = apparatus;
        }

        public void Process()
        {
            FireApparatus current = apparatus.Current;
            if (current == null || !current.Definition.HasDeckGun || (current.Water <= 0f && !current.IsSupplied))
                return;
            if (!Game.IsControlPressed(0, GameControl.VehicleAttack) && !Game.IsControlPressed(0, GameControl.VehicleAttack2))
                return;

            Vehicle vehicle = current.Vehicle;
            Vector3 origin = vehicle.Position + new Vector3(0f, 0f, vehicle.Model.Dimensions.Z / 2f + 0.5f);
            Vector3 end = origin + GameplayCamera.Direction * Range;
            HitResult hit = World.TraceLine(origin, end, TraceFlags.IntersectWorld | TraceFlags.IntersectObjects, vehicle);
            Vector3 impact = hit.Hit ? hit.HitPosition : end;

            fire.ApplyWater(position => position.DistanceTo(impact) <= ImpactRadius || DistanceToSegment(position, origin, impact) <= StreamRadius,
                CoolingPerSecond * Game.FrameTime);
        }

        internal static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSquared = ab.LengthSquared();
            if (lengthSquared < 0.0001f)
                return point.DistanceTo(a);

            float t = System.Math.Max(0f, System.Math.Min(1f, Vector3.Dot(point - a, ab) / lengthSquared));
            return point.DistanceTo(a + ab * t);
        }
    }
}
