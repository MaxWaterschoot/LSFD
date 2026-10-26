using LSFD.Buildings;
using LSFD.Core;
using Rage;

namespace LSFD.API
{
    /// <summary>A building from FireLocations.xml where a fire can break out.</summary>
    public sealed class BuildingInfo
    {
        public string Name { get; }
        public FireLocationKind Kind { get; }

        /// <summary>Where the fire is, inside the building.</summary>
        public Vector3 Position { get; }

        /// <summary>Street position in front of the building, for the GPS route and staging.</summary>
        public Vector3 Staging { get; }

        internal BuildingInfo(FireLocation location)
        {
            Name = location.Name;
            Kind = location.Kind;
            Position = location.Position;
            Staging = location.GetStagingPosition();
        }
    }

    /// <summary>Interior helpers for callouts, including support for the Enable All Interiors mod.</summary>
    public static class Interiors
    {
        /// <summary>
        /// Whether the Enable All Interiors mod is installed (or forced on in LSFD.ini), so normally locked buildings can be entered.
        /// Callouts with <see cref="CalloutInfoAttribute.RequiresOpenInteriors"/> are only dispatched when this is true.
        /// </summary>
        public static bool AreInteriorsUnlocked => InteriorSupport.AreInteriorsUnlocked;

        /// <summary>The interior ID at a position, or 0 when it is outside.</summary>
        public static int GetInteriorAt(Vector3 position) => InteriorSupport.GetInteriorAt(position);

        /// <summary>
        /// Loads the interior at <paramref name="position"/> and waits (up to 5 s) until it is ready, so fires and peds
        /// can be spawned inside. Call it from <c>OnArrival</c>. Returns false if the interior did not load.
        /// </summary>
        public static bool PrepareInterior(Vector3 position) => InteriorSupport.Prepare(position);

        /// <summary>
        /// A random building from FireLocations.xml between <paramref name="minDistance"/> and <paramref name="maxDistance"/>
        /// from the player, of one of the given kinds (any kind when none are given). Locked buildings are only returned
        /// when interiors are unlocked. Returns null when nothing fits.
        /// </summary>
        public static BuildingInfo GetRandomBuilding(float minDistance, float maxDistance, params FireLocationKind[] kinds)
        {
            FireLocation location = Lsfd.FireLocations.GetRandom(Game.LocalPlayer.Character.Position, minDistance, maxDistance, kinds);
            return location == null ? null : new BuildingInfo(location);
        }
    }
}
