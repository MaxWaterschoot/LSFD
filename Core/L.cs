using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using LSFD.Utils;
using Rage;

namespace LSFD.Core
{
    /// <summary>A language file in Plugins/LSFD/Languages/, e.g. nl.xml.</summary>
    [XmlRoot("Language")]
    public class LanguageFile
    {
        /// <summary>The name of the language in that language, e.g. "Nederlands".</summary>
        [XmlAttribute] public string Name { get; set; }

        /// <summary>Formats numbers and times, e.g. "nl-NL" writes 1.240 L.</summary>
        [XmlAttribute] public string Culture { get; set; }

        [XmlAttribute] public string Translators { get; set; }

        [XmlElement("String")] public List<LanguageString> Strings { get; set; } = new List<LanguageString>();
    }

    public class LanguageString
    {
        /// <summary>The English text, exactly as LSFD shows it; {0}, {1} are filled in by LSFD.</summary>
        [XmlAttribute("en")] public string English { get; set; }

        /// <summary>What the {0}, {1} placeholders are, for translators.</summary>
        [XmlAttribute("note")] public string Note { get; set; }

        /// <summary>The translation; empty means the English text is shown.</summary>
        [XmlText] public string Text { get; set; }
    }

    /// <summary>
    /// Translations. Every text the player sees goes through <see cref="T"/> (fixed text) or <see cref="F"/> (text with
    /// values in it). The English text is the key: a language file maps it to the translation, and anything not
    /// translated (yet) is shown in English. A word that needs different translations gets a context after a bar,
    /// e.g. "her|possessive"; in English the context isn't shown.
    /// </summary>
    internal static class L
    {
        public const string DefaultLanguage = "en";
        private const int MaxReverseEntries = 2000;

        private static Dictionary<string, string> strings = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> toEnglish = new Dictionary<string, string>();
        private static readonly HashSet<string> missing = new HashSet<string>();

        public static string Language { get; private set; } = DefaultLanguage;
        public static string LanguageName { get; private set; } = "English";
        public static CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

        public static string Folder => ModFolder.Languages;

        /// <summary>
        /// Loads Plugins/LSFD/Languages/<paramref name="language"/>.xml, plus files for callout packs such as
        /// nl.MyPack.xml. English when it is "en" or the file is missing.
        /// </summary>
        public static void Load(string language)
        {
            strings = new Dictionary<string, string>();
            missing.Clear();
            toEnglish.Clear();
            Language = DefaultLanguage;
            LanguageName = "English";
            Culture = CultureInfo.InvariantCulture;

            if (string.IsNullOrWhiteSpace(language) || language.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                return;

            string path = Path.Combine(Folder, language + ".xml");
            if (!File.Exists(path))
            {
                Game.LogTrivial($"LSFD: language file {path} not found, using English.");
                return;
            }

            LanguageFile file = Read(path);
            if (file == null)
                return;

            Add(file);
            foreach (string pack in Directory.GetFiles(Folder, language + ".*.xml"))
                Add(Read(pack));

            Language = language;
            LanguageName = string.IsNullOrWhiteSpace(file.Name) ? language : file.Name;
            Culture = TryCulture(file.Culture);
            Game.LogTrivial($"LSFD: language {LanguageName} loaded, {strings.Count} translations.");
        }

        private static LanguageFile Read(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                    return (LanguageFile)new XmlSerializer(typeof(LanguageFile)).Deserialize(stream);
            }
            catch (Exception e)
            {
                Game.LogTrivial($"LSFD: could not read {path}: {e.Message} {e.InnerException?.Message}");
                return null;
            }
        }

        private static void Add(LanguageFile file)
        {
            if (file == null)
                return;
            foreach (LanguageString entry in file.Strings.Where(s => !string.IsNullOrEmpty(s.English) && !string.IsNullOrWhiteSpace(s.Text)))
                strings[entry.English] = entry.Text;
        }

        /// <summary>The translation of a fixed text.</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english))
                return english;

            int context = english.IndexOf('|');
            string shown = context < 0 ? english : english.Substring(0, context);
            if (Language == DefaultLanguage)
                return shown;

            if (strings.TryGetValue(english, out string translated))
            {
                Remember(translated, shown);
                return translated;
            }

            missing.Add(english);
            return shown;
        }

        /// <summary>The translation of a text with values in it: <c>L.F($"{count} patients")</c> looks up "{0} patients".</summary>
        public static string F(FormattableString text)
        {
            object[] arguments = text.GetArguments();
            string english = string.Format(CultureInfo.InvariantCulture, text.Format, arguments);
            if (Language == DefaultLanguage)
                return english;

            string format = T(text.Format);
            try
            {
                string translated = string.Format(Culture, format, arguments);
                Remember(translated, english);
                return translated;
            }
            catch (FormatException)
            {
                Game.LogTrivial($"LSFD: the {LanguageName} translation of \"{text.Format}\" has wrong placeholders.");
                return english;
            }
        }

        /// <summary>
        /// The English text a translated text came from, e.g. to pick the right radio fragment. A text put together
        /// from several translations gets each of them back in English.
        /// </summary>
        public static string English(string text)
        {
            if (string.IsNullOrEmpty(text) || Language == DefaultLanguage)
                return text;
            if (toEnglish.TryGetValue(text, out string english))
                return english;

            foreach (var pair in toEnglish.Where(p => p.Key.Length > 3).OrderByDescending(p => p.Key.Length))
            {
                if (text.Contains(pair.Key))
                    text = text.Replace(pair.Key, pair.Value);
            }
            return text;
        }

        /// <summary>
        /// Writes Languages/missing_[language].xml with every text that was shown in English this session because the
        /// language file has no translation for it. Returns the path, or null when nothing is missing.
        /// </summary>
        public static string ExportMissing()
        {
            if (Language == DefaultLanguage || missing.Count == 0)
                return null;

            var file = new LanguageFile
            {
                Name = LanguageName,
                Culture = Culture.Name,
                Strings = missing.OrderBy(s => s).Select(s => new LanguageString { English = s, Text = "" }).ToList(),
            };
            string fileName = $"missing_{Language}.xml";
            XmlStore.Save(fileName, file, Folder);
            return Path.Combine(Folder, fileName);
        }

        public static IEnumerable<string> AvailableLanguages()
        {
            if (!Directory.Exists(Folder))
                return new[] { DefaultLanguage };

            return new[] { DefaultLanguage }.Concat(Directory.GetFiles(Folder, "*.xml")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !name.StartsWith("missing_", StringComparison.OrdinalIgnoreCase) && !name.Contains(".") && name != "template"))
                .Distinct();
        }

        private static void Remember(string translated, string english)
        {
            if (translated == english)
                return;
            if (toEnglish.Count > MaxReverseEntries)
                toEnglish.Clear();
            toEnglish[translated] = english;
        }

        private static CultureInfo TryCulture(string name)
        {
            try
            {
                return string.IsNullOrWhiteSpace(name) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
