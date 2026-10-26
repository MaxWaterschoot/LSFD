using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Utils;
using Rage;

namespace LSFD.Buildings
{
    /// <summary>Loads Plugins/LSFD/Data/FireLocations.xml; add your own with the LSFD_AddFireLocation console command.</summary>
    internal class FireLocationRepository
    {
        private const string FileName = "FireLocations.xml";

        private readonly List<FireLocation> locations;

        public FireLocationRepository()
        {
            locations = XmlStore.Load(FileName, () => new FireLocationList { Locations = CreateDefaults() }).Locations;
        }

        /// <summary>Locations that can be used right now, i.e. not needing Enable All Interiors when it isn't installed.</summary>
        public IEnumerable<FireLocation> Available =>
            locations.Where(l => !l.RequiresOpenInteriors || InteriorSupport.AreInteriorsUnlocked);

        public FireLocation GetRandom(Vector3 around, float minDistance, float maxDistance, params FireLocationKind[] kinds)
        {
            var candidates = Available
                .Where(l => kinds.Length == 0 || kinds.Contains(l.Kind))
                .Where(l => l.Position.DistanceTo2D(around) >= minDistance && l.Position.DistanceTo2D(around) <= maxDistance)
                .ToList();
            return candidates.Count == 0 ? null : candidates[MathHelper.GetRandomInteger(candidates.Count)];
        }

        public FireLocation Add(string name, FireLocationKind kind, Vector3 position)
        {
            // Interiors that the vanilla game keeps locked only work with Enable All Interiors; assume that for anything indoors.
            bool indoors = InteriorSupport.GetInteriorAt(position) != 0;
            var location = new FireLocation(name, kind,
                (float)Math.Round(position.X, 2), (float)Math.Round(position.Y, 2), (float)Math.Round(position.Z, 2), indoors);

            locations.Add(location);
            XmlStore.Save(FileName, new FireLocationList { Locations = locations });
            return location;
        }

        private static List<FireLocation> CreateDefaults()
        {
            var defaults = new List<FireLocation>
            {
                // Stores that are open in the vanilla game, so these work without Enable All Interiors.
                new FireLocation("24/7 Innocence Boulevard", FireLocationKind.Store, 25.7f, -1347.3f, 29.5f),
                new FireLocation("24/7 Clinton Avenue", FireLocationKind.Store, 373.9f, 325.9f, 103.6f),
                new FireLocation("24/7 Palomino Freeway", FireLocationKind.Store, 2557.5f, 382.3f, 108.6f),
                new FireLocation("24/7 Inseno Road", FireLocationKind.Store, -3038.9f, 586.0f, 7.9f),
                new FireLocation("24/7 Barbareno Road", FireLocationKind.Store, -3241.9f, 1001.5f, 12.8f),
                new FireLocation("24/7 Route 68", FireLocationKind.Store, 547.4f, 2671.7f, 42.2f),
                new FireLocation("24/7 Sandy Shores", FireLocationKind.Store, 1961.5f, 3740.7f, 32.3f),
                new FireLocation("24/7 Senora Freeway", FireLocationKind.Store, 2678.9f, 3280.7f, 55.2f),
                new FireLocation("24/7 Paleto Bay", FireLocationKind.Store, 1729.2f, 6414.1f, 35.0f),
                new FireLocation("LTD Grove Street", FireLocationKind.Store, -48.5f, -1757.5f, 29.4f),
                new FireLocation("LTD Mirror Park", FireLocationKind.Store, 1163.4f, -323.8f, 69.2f),
                new FireLocation("LTD Little Seoul", FireLocationKind.Store, -707.5f, -914.3f, 19.2f),
                new FireLocation("LTD Banham Canyon", FireLocationKind.Store, -1820.5f, 792.5f, 138.1f),
                new FireLocation("LTD Grapeseed", FireLocationKind.Store, 1698.4f, 4924.4f, 42.1f),
                new FireLocation("Rob's Liquor El Rancho", FireLocationKind.Store, 1135.8f, -982.3f, 46.4f),
                new FireLocation("Rob's Liquor Vespucci", FireLocationKind.Store, -1222.9f, -907.0f, 12.3f),
                new FireLocation("Rob's Liquor Morningwood", FireLocationKind.Store, -1487.6f, -379.1f, 40.2f),
                new FireLocation("Rob's Liquor Great Ocean", FireLocationKind.Store, -2968.2f, 390.9f, 15.0f),
                new FireLocation("Scoops Liquor Route 68", FireLocationKind.Store, 1166.0f, 2708.9f, 38.2f),
                new FireLocation("Ace Liquor Sandy Shores", FireLocationKind.Store, 1392.6f, 3604.7f, 35.0f),
            };

            // Interiors from the RAGE MP wiki (wiki.rage.mp/wiki/Interiors_and_Locations) that sit at their real place
            // in the city. They are locked in single player, so they need Enable All Interiors.
            defaults.AddRange(new[]
            {
                // High-rise apartments
                Locked("4 Integrity Way, Apt 28", FireLocationKind.Apartment, -18.08f, -583.67f, 79.47f),
                Locked("4 Integrity Way, Apt 30", FireLocationKind.Apartment, -35.31f, -580.42f, 88.71f),
                Locked("Del Perro Heights, Apt 4", FireLocationKind.Apartment, -1468.14f, -541.82f, 73.44f),
                Locked("Del Perro Heights, Apt 7", FireLocationKind.Apartment, -1477.14f, -538.75f, 55.53f),
                Locked("Richards Majestic, Apt 2", FireLocationKind.Apartment, -915.81f, -379.43f, 113.67f),
                Locked("Tinsel Towers, Apt 42", FireLocationKind.Apartment, -614.86f, 40.68f, 97.60f),
                Locked("Eclipse Towers, Apt 3", FireLocationKind.Apartment, -773.41f, 341.77f, 211.40f),

                // Houses
                Locked("3655 Wild Oats Drive", FireLocationKind.House, -169.29f, 486.49f, 137.44f),
                Locked("2044 North Conker Avenue", FireLocationKind.House, 340.94f, 437.18f, 149.39f),
                Locked("2045 North Conker Avenue", FireLocationKind.House, 373.02f, 416.11f, 145.70f),
                Locked("2862 Hillcrest Avenue", FireLocationKind.House, -676.13f, 588.61f, 145.17f),
                Locked("2868 Hillcrest Avenue", FireLocationKind.House, -763.11f, 615.91f, 144.14f),
                Locked("2874 Hillcrest Avenue", FireLocationKind.House, -857.80f, 682.56f, 152.65f),
                Locked("2677 Whispymound Drive", FireLocationKind.House, 120.50f, 549.95f, 184.10f),
                Locked("2133 Mad Wayne Thunder Drive", FireLocationKind.House, -1288.00f, 440.75f, 97.69f),
                Locked("Lester's House, Murrieta Heights", FireLocationKind.House, 1273.90f, -1719.31f, 54.77f),
                Locked("Trailer, Sandy Shores", FireLocationKind.House, 1975.55f, 3820.54f, 33.45f),
                Locked("O'Neil Ranch, Grapeseed", FireLocationKind.House, 2469.03f, 4955.28f, 45.12f),

                // Offices
                Locked("Arcadius Business Center", FireLocationKind.Office, -141.29f, -620.96f, 168.82f),
                Locked("Maze Bank Tower", FireLocationKind.Office, -75.50f, -827.19f, 243.39f),
                Locked("Lombank West", FireLocationKind.Office, -1579.70f, -565.04f, 108.52f),
                Locked("Maze Bank West", FireLocationKind.Office, -1392.63f, -480.49f, 72.04f),
                Locked("Lifeinvader Office", FireLocationKind.Office, -1044.19f, -236.95f, 37.96f),
                Locked("Solomon Richards' Office", FireLocationKind.Office, -1005.84f, -478.92f, 50.03f),
                Locked("Psychiatrist's Office", FireLocationKind.Office, -1908.02f, -573.42f, 19.10f),

                // Shops, businesses and public buildings
                Locked("Vangelico Jewelry Store", FireLocationKind.Store, -630.07f, -236.33f, 38.06f),
                Locked("Max Renda Shop", FireLocationKind.Store, -585.82f, -282.72f, 35.45f),
                Locked("Lost MC Clubhouse", FireLocationKind.Commercial, 982.01f, -100.87f, 74.85f),
                Locked("Union Depository", FireLocationKind.Public, 2.70f, -667.02f, 16.13f),
                Locked("Tivoli Cinema, Morningwood", FireLocationKind.Public, -1427.30f, -245.10f, 16.80f),
                Locked("Morgue, Rancho", FireLocationKind.Public, 275.45f, -1361.11f, 24.54f),
                Locked("FIB Building Lobby", FireLocationKind.Public, 110.40f, -744.20f, 45.75f),

                // Industrial
                Locked("Omega's Garage, Harmony", FireLocationKind.Industrial, 2331.34f, 2574.07f, 46.68f),
            });

            return defaults;
        }

        private static FireLocation Locked(string name, FireLocationKind kind, float x, float y, float z)
        {
            return new FireLocation(name, kind, x, y, z, requiresOpenInteriors: true);
        }
    }
}
