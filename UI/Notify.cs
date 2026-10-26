using LSFD.Core;
using Rage;

namespace LSFD.UI
{
    /// <summary>Notifications in one consistent LSFD style.</summary>
    internal static class Notify
    {
        private const string Icon = "CHAR_CALL911";

        public static void Info(string text)
        {
            Game.DisplayNotification(Icon, Icon, "LSFD", L.T("~r~Fire Department"), text);
        }

        public static void Dispatch(string subtitle, string text)
        {
            Game.DisplayNotification(Icon, Icon, L.T("LSFD Dispatch"), subtitle, text);
        }

        public static void Warning(string text)
        {
            Game.DisplayNotification(Icon, Icon, "LSFD", L.T("~o~Attention"), text);
        }
    }
}
