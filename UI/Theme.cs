using System;
using System.Drawing;
using System.Windows.Forms;
using RAGENativeUI;
using RAGENativeUI.Elements;

namespace LSFD.UI
{
    /// <summary>The LSFD look: colors and menu styling shared by all UI.</summary>
    internal static class Theme
    {
        public static readonly Color Banner = Color.FromArgb(255, 150, 18, 18);
        public static readonly Color Highlight = Color.FromArgb(255, 220, 60, 40);
        public static readonly Color Water = Color.FromArgb(255, 70, 150, 230);
        public static readonly Color WaterLow = Color.FromArgb(255, 230, 70, 50);
        public static readonly Color BarBackground = Color.FromArgb(255, 30, 30, 30);

        public static UIMenu CreateMenu(string subtitle)
        {
            var menu = new UIMenu("LSFD", subtitle.ToUpperInvariant());
            menu.SetBannerType(Banner);
            menu.MouseControlsEnabled = false;
            menu.AllowCameraMovement = true;
            return menu;
        }

        /// <summary>A non-selectable header row to group menu items.</summary>
        public static UIMenuItem CreateHeader(string text)
        {
            return new UIMenuItem($"~h~{text}") { Skipped = true };
        }

        /// <summary>A button for a configurable key, falling back to a text label when RAGENativeUI has no icon for it.</summary>
        public static InstructionalButtonId ButtonFor(Keys key)
        {
            return Enum.TryParse(key.ToString(), out InstructionalKey instructionalKey)
                ? new InstructionalButtonId(instructionalKey)
                : new InstructionalButtonId(key.ToString());
        }
    }
}
