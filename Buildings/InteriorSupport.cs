using System.IO;
using System.Linq;
using LSFD.Core;
using Rage;
using Rage.Native;

namespace LSFD.Buildings
{
    /// <summary>
    /// Detects the Enable All Interiors mod (by HKH191, a ScriptHookVDotNet script) and loads interiors so fires and
    /// peds can be placed inside.
    /// </summary>
    internal static class InteriorSupport
    {
        private const int InteriorLoadTimeout = 5000;

        // SHVDN scripts live in "scripts"; the rest covers ASI and RPH mods.
        private static readonly string[] SearchFolders = { "scripts", ".", "Plugins" };

        // File names of mods that unlock interiors. Open All Interiors is discontinued but still recognized.
        private static readonly string[] InteriorModNames = { "enableallinteriors", "openallinteriors" };

        private static bool? interiorsUnlocked;

        public static bool AreInteriorsUnlocked
        {
            get
            {
                if (!interiorsUnlocked.HasValue)
                    interiorsUnlocked = Detect();
                return interiorsUnlocked.Value;
            }
        }

        /// <summary>The interior at a position, or 0 when it is outside.</summary>
        public static int GetInteriorAt(Vector3 position)
        {
            return NativeFunction.CallByName<int>("GET_INTERIOR_AT_COORDS", position.X, position.Y, position.Z);
        }

        /// <summary>Loads the interior at <paramref name="position"/> (if any) and waits until it is ready. Must be called from a game fiber.</summary>
        public static bool Prepare(Vector3 position)
        {
            int interior = GetInteriorAt(position);
            if (interior == 0)
                return true;

            NativeFunction.CallByName<uint>("PIN_INTERIOR_IN_MEMORY", interior);
            GameFiber.SleepUntil(() => NativeFunction.CallByName<bool>("IS_INTERIOR_READY", interior), InteriorLoadTimeout);
            return NativeFunction.CallByName<bool>("IS_INTERIOR_READY", interior);
        }

        private static bool Detect()
        {
            if (Settings.EnableAllInteriors != InteriorModMode.Auto)
                return Settings.EnableAllInteriors == InteriorModMode.Enabled;

            string found = SearchFolders
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.GetFiles(folder))
                .FirstOrDefault(IsInteriorModFile);

            Game.LogTrivial(found != null
                ? $"LSFD: interior mod detected ({found}), interior callouts are enabled."
                : "LSFD: Enable All Interiors not found, callouts that need locked interiors are disabled. " +
                  "Set [Interiors] EnableAllInteriors=Enabled in LSFD.ini to override.");
            return found != null;
        }

        private static bool IsInteriorModFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".dll" && extension != ".asi")
                return false;

            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
            return InteriorModNames.Any(name.Contains);
        }
    }
}
