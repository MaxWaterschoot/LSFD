using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LSFD.Utils
{
    /// <summary>
    /// The Plugins/LSFD/ folder that ships with the download. LSFD only starts when it is installed:
    /// <code>
    /// Plugins/LSFD/
    ///   Audio/       dispatch and radio audio, sirens, alarms
    ///   Data/        Stations.xml, Apparatus.xml, Tools.xml, FireLocations.xml
    ///   Callouts/    callout DLLs and XML callouts
    ///   Profiles/    player profiles
    ///   LSFD.ini
    ///   Keys.ini
    /// </code>
    /// </summary>
    internal static class ModFolder
    {
        public const string Root = "Plugins/LSFD";

        public static readonly string Audio = Path.Combine(Root, "Audio");
        public static readonly string DispatchAudio = Path.Combine(Audio, "Dispatch");
        public static readonly string RadioAudio = Path.Combine(Audio, "Radio");
        public static readonly string SirenAudio = Path.Combine(Audio, "Sirens");
        public static readonly string AlarmAudio = Path.Combine(Audio, "Alarms");
        public static readonly string Data = Path.Combine(Root, "Data");
        public static readonly string Callouts = Path.Combine(Root, "Callouts");
        public static readonly string Profiles = Path.Combine(Root, "Profiles");

        public static readonly string SettingsFile = Path.Combine(Root, "LSFD.ini");
        public static readonly string KeysFile = Path.Combine(Root, "Keys.ini");

        private static readonly string[] RequiredFolders = { Root, Audio, Data, Callouts, Profiles };

        /// <summary>The required folders that are not there, relative to the GTA V folder.</summary>
        public static List<string> FindMissingFolders()
        {
            return RequiredFolders.Where(folder => !Directory.Exists(folder)).ToList();
        }
    }
}
