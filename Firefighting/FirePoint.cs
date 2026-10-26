using LSFD.Utils;
using Rage;

namespace LSFD.Firefighting
{
    internal enum FirePointState
    {
        Burning,
        /// <summary>Flames knocked down, but still hot enough to rekindle.</summary>
        Smoldering,
        Out,
    }

    /// <summary>One spot of an incident that burns. The GTA fire on it is only the visual; this holds the real state.</summary>
    internal class FirePoint
    {
        public FirePoint(Vector3 position, Vector3 spreadFrom, float intensity, bool isPlaced)
        {
            Position = position;
            SpreadFrom = spreadFrom;
            Intensity = intensity;
            IsPlaced = isPlaced;
        }

        public Vector3 Position { get; set; }

        /// <summary>The spot the fire spread here from; a point inside a building must be in sight of it.</summary>
        public Vector3 SpreadFrom { get; }

        /// <summary>Whether <see cref="Position"/> has been checked against the world (floor, walls). Only possible once streamed in.</summary>
        public bool IsPlaced { get; set; }

        public FirePointState State { get; set; } = FirePointState.Burning;

        /// <summary>Size of the flames, 0 to 1.</summary>
        public float Intensity { get; set; }

        /// <summary>Heat left after knockdown; overhaul (more water) brings it to 0.</summary>
        public float Heat { get; set; }

        /// <summary>Heat a spot keeps after its flames are knocked down.</summary>
        public float HeatAfterKnockdown { get; set; } = 1f;

        public uint LastWaterAt { get; set; }

        /// <summary>When a smoldering spot gets its next chance to rekindle.</summary>
        public uint RekindleCheckAt { get; set; }

        /// <summary>Handle of the GTA script fire showing the flames, 0 when there is none.</summary>
        public int ScriptFire { get; set; }

        public uint NextVisualCheck { get; set; }

        /// <summary>Light smoke rising from a hotspot.</summary>
        public LoopedParticle HotspotSmoke { get; } = new LoopedParticle(FireEffects.HotspotSmokeAsset);

        public bool IsBurning => State == FirePointState.Burning;
        public bool IsHot => State != FirePointState.Out;
    }
}
