using System;
using System.Linq;
using LSFD.Core;
using LSFD.UI;
using Rage;

namespace LSFD.API
{
    /// <summary>Entry point for other plugins and callout packs to interact with LSFD.</summary>
    public static class Functions
    {
        public delegate void DutyStateChangedEventHandler(bool onDuty);

        /// <summary>Raised when the player goes on or off duty.</summary>
        public static event DutyStateChangedEventHandler OnDutyStateChanged;

        /// <summary>Registers a callout type. Only needed for callouts outside of Plugins/LSFD/, those are registered automatically.</summary>
        public static bool RegisterCallout(Type calloutType) => Lsfd.Registry.Register(calloutType);

        public static string[] GetRegisteredCallouts() => Lsfd.Registry.Names.ToArray();

        public static bool IsPlayerOnDuty() => Lsfd.Duty != null && Lsfd.Duty.IsOnDuty;

        public static bool IsCalloutRunning() => Lsfd.Callouts?.Current != null;

        /// <summary>The callout the player is currently assigned to, or null.</summary>
        public static Callout GetCurrentCallout() => Lsfd.Callouts?.Current;

        /// <summary>Offers a specific callout to the player right away. Returns false when the player is busy or the callout cancelled itself.</summary>
        public static bool StartCallout(string name) => Lsfd.Callouts != null && Lsfd.Callouts.Dispatch(name);

        public static void StopCurrentCallout() => Lsfd.Callouts?.EndCurrent(null);

        /// <summary>Shows a notification in the LSFD dispatch style.</summary>
        public static void DisplayNotification(string subtitle, string text) => Notify.Dispatch(subtitle, text);

        /// <summary>A random position on a street around the player, handy for <see cref="Callout.CalloutPosition"/>.</summary>
        public static Vector3 GetRandomCalloutLocation(float minDistance = 300f, float maxDistance = 900f)
        {
            Vector3 around = Game.LocalPlayer.Character.Position.Around2D(minDistance, maxDistance);
            return World.GetNextPositionOnStreet(around);
        }

        internal static void RaiseDutyStateChanged(bool onDuty)
        {
            if (OnDutyStateChanged == null)
                return;

            // One faulty subscriber shouldn't break the others or LSFD itself.
            foreach (DutyStateChangedEventHandler handler in OnDutyStateChanged.GetInvocationList())
            {
                try
                {
                    handler(onDuty);
                }
                catch (Exception e)
                {
                    Game.LogTrivial($"LSFD: OnDutyStateChanged handler {handler.Method.DeclaringType} threw: {e}");
                }
            }
        }
    }
}
