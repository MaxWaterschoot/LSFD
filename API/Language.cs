using System;
using LSFD.Core;

namespace LSFD.API
{
    /// <summary>
    /// Translations for callout packs: wrap the texts you show in <see cref="T"/> or <see cref="F"/> and they can be
    /// translated in a language file, just like LSFD's own. Put your translations in Plugins/LSFD/Languages/, e.g.
    /// nl.MyPack.xml next to nl.xml; texts without a translation are shown in English.
    /// </summary>
    public static class Language
    {
        /// <summary>The language the player chose, e.g. "en" or "nl".</summary>
        public static string Current => L.Language;

        /// <summary>The translation of a fixed text: <c>Language.T("Extinguish the fire.")</c>.</summary>
        public static string T(string english) => L.T(english);

        /// <summary>
        /// The translation of a text with values: <c>Language.F($"{count} patients")</c>. The language file has the text
        /// with {0}, {1} for the values: "{0} patients".
        /// </summary>
        public static string F(FormattableString text) => L.F(text);
    }
}
