using LSFD.Core;
using Rage;

namespace LSFD.Callouts
{
    /// <summary>Helpers to write what callers and witnesses say about the people involved.</summary>
    internal static class CallerLines
    {
        // Height of a storey, to tell which floor a victim is on.
        private const float FloorHeight = 3.2f;

        public static string Pick(params string[] options) => options[MathHelper.GetRandomInteger(options.Length)];

        /// <summary>"son", "wife", ... for how the caller knows the victim.</summary>
        public static string Relation(Ped ped) => IsMale(ped)
            ? Pick(L.T("son"), L.T("husband"), L.T("brother"), L.T("dad"), L.T("roommate|male"), L.T("neighbor|male"))
            : Pick(L.T("daughter"), L.T("wife"), L.T("sister"), L.T("mom"), L.T("roommate|female"), L.T("neighbor|female"));

        // "her" is two words in many languages, so the possessive one has a context of its own.
        public static string He(Ped ped) => IsMale(ped) ? L.T("he") : L.T("she");
        public static string Him(Ped ped) => IsMale(ped) ? L.T("him") : L.T("her");
        public static string His(Ped ped) => IsMale(ped) ? L.T("his") : L.T("her|possessive");

        /// <summary>"He", "She".</summary>
        public static string Capital(string word) => char.ToUpperInvariant(word[0]) + word.Substring(1);

        /// <summary>Where in the building <paramref name="victim"/> is, as a caller standing at <paramref name="outside"/> would say it.</summary>
        public static string WhereInBuilding(Vector3 victim, Vector3 buildingCenter, Vector3 outside)
        {
            int floor = 1 + System.Math.Max(0, (int)System.Math.Round((victim.Z - outside.Z) / FloorHeight));
            string level = floor == 1 ? L.T("on the ground floor") : L.F($"on the {Ordinal(floor)} floor");
            bool back = victim.DistanceTo2D(outside) > buildingCenter.DistanceTo2D(outside);
            return $"{level}, {(back ? L.T("in the back") : L.T("near the front"))}";
        }

        public static string Ordinal(int number)
        {
            switch (number)
            {
                case 1: return L.T("first");
                case 2: return L.T("second");
                case 3: return L.T("third");
                case 4: return L.T("fourth");
                case 5: return L.T("fifth");
                default: return L.F($"{number}th");
            }
        }

        private static bool IsMale(Ped ped) => !ped.Exists() || ped.IsMale;
    }
}
