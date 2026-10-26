using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LSFD.API;
using LSFD.Core;
using Rage;

namespace LSFD.Utils
{
    /// <summary>
    /// Writes Plugins/LSFD/BugReport.txt for testers to attach to a bug report: versions, the installed mods, the LSFD
    /// settings, what LSFD was doing and the end of RagePluginHook.log. Only file names are listed, nothing personal.
    /// </summary>
    internal static class BugReport
    {
        private const string LogFile = "RagePluginHook.log";
        private const int LogLines = 400;

        public static string FileName => Path.Combine(ModFolder.Root, "BugReport.txt");

        public static string Write()
        {
            var report = new StringBuilder();
            Section(report, "Versions");
            report.AppendLine($"LSFD {typeof(BugReport).Assembly.GetName().Version} (API {Functions.ApiVersion})");
            report.AppendLine($"RAGE Plugin Hook {typeof(Game).Assembly.GetName().Version}");
            report.AppendLine($"GTA V {Game.ProductVersion} (build {Game.BuildNumber})");
            report.AppendLine($"Windows {Environment.OSVersion.Version}, .NET {Environment.Version}");
            report.AppendLine($"Language {L.Language}, resolution {Game.Resolution.Width}x{Game.Resolution.Height}");
            report.AppendLine($"Written {DateTime.Now:yyyy-MM-dd HH:mm}");

            Section(report, "State");
            report.AppendLine($"On duty: {Lsfd.Duty.IsOnDuty}");
            report.AppendLine($"Current call: {Lsfd.Callouts.Current?.FriendlyName ?? "none"}");
            report.AppendLine($"Apparatus: {Lsfd.Apparatus.All.Count}, crew: {Lsfd.Crew.Members.Count}, patients: {Lsfd.Patients.All.Count}");
            report.AppendLine($"Calls: {string.Join(", ", Functions.GetRegisteredCallouts())}");

            Section(report, "Mods");
            Files(report, "GTA V folder (.asi, .dll)", ".", "*.asi", "*.dll");
            Files(report, "RAGE Plugin Hook plugins", "Plugins", "*.dll");
            Files(report, "LSFD callout packs", ModFolder.Callouts, "*.dll", "*.xml");
            Files(report, "LSFD languages", ModFolder.Languages, "*.xml");
            report.AppendLine($"mods folder: {(Directory.Exists("mods") ? "yes" : "no")}, LSPDFR: {(Directory.Exists(Path.Combine("Plugins", "LSPDFR")) ? "yes" : "no")}");

            Section(report, "LSFD.ini");
            report.AppendLine(ReadShared(ModFolder.SettingsFile) ?? "(not found)");

            Section(report, $"Last {LogLines} lines of {LogFile}");
            string log = ReadShared(LogFile);
            report.AppendLine(log == null ? "(not found)" : string.Join(Environment.NewLine, log.Split('\n').Reverse().Take(LogLines).Reverse()));

            File.WriteAllText(FileName, report.ToString());
            return FileName;
        }

        private static void Section(StringBuilder report, string title)
        {
            report.AppendLine();
            report.AppendLine($"== {title} ==");
        }

        private static void Files(StringBuilder report, string title, string folder, params string[] patterns)
        {
            IEnumerable<string> names = Directory.Exists(folder)
                ? patterns.SelectMany(p => Directory.GetFiles(folder, p)).Select(Path.GetFileName).OrderBy(n => n)
                : Enumerable.Empty<string>();
            report.AppendLine($"{title}: {string.Join(", ", names.DefaultIfEmpty("none"))}");
        }

        // RAGE Plugin Hook keeps its log open, so it has to be shared.
        private static string ReadShared(string path)
        {
            if (!File.Exists(path))
                return null;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }
}
