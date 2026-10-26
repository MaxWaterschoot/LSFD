namespace LSFD.Firefighting
{
    /// <summary>
    /// Particle effects used for smoke. All are from the base game; if one doesn't show up in game,
    /// change the name here (see the ptfx lists on the GTA modding wikis).
    /// </summary>
    internal static class FireEffects
    {
        /// <summary>The large black column from the Agency heist, visible from far away.</summary>
        public const string SmokeColumnAsset = "scr_agencyheistb";
        public const string SmokeColumnEffect = "scr_env_agency3b_smoke";

        public const string HotspotSmokeAsset = "core";
        public const string HotspotSmokeEffect = "ent_amb_smoke_foundry";
        public const float HotspotSmokeScale = 0.15f;

        /// <summary>Distance (m) the smoke column stays visible from.</summary>
        public const float SmokeColumnDrawDistance = 3000f;
    }
}
