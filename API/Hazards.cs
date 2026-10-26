using LSFD.Core;
using Rage;

namespace LSFD.API
{
    /// <summary>An area with an invisible toxic atmosphere (e.g. carbon monoxide) that hurts without an SCBA.</summary>
    public sealed class ToxicAtmosphere
    {
        internal ToxicAtmosphere(Vector3 position, float radius, float strength)
        {
            Position = position;
            Radius = radius;
            Strength = strength;
        }

        public Vector3 Position { get; set; }
        public float Radius { get; set; }

        /// <summary>0 to 1; lower it as the area is ventilated.</summary>
        public float Strength { get; set; }

        public bool IsRemoved { get; private set; }

        public void Remove()
        {
            IsRemoved = true;
        }
    }

    public static class Hazards
    {
        /// <summary>Adds a toxic atmosphere; breathing in it without the SCBA face piece costs health.</summary>
        public static ToxicAtmosphere AddToxicAtmosphere(Vector3 position, float radius, float strength = 1f)
        {
            var zone = new ToxicAtmosphere(position, radius, strength);
            Lsfd.Hazards.AddToxicAtmosphere(zone);
            return zone;
        }
    }
}
