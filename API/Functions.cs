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
        /// <summary>
        /// The LSFD API version this DLL was compiled against. It is a constant, so it is baked into your DLL when you
        /// build: return it from <see cref="CalloutPack.ApiVersion"/> and LSFD knows which API your pack expects.
        /// Use <see cref="InstalledApiVersion"/> for the version of the LSFD that is actually running.
        /// </summary>
        /// <remarks>
        /// 1: the first public API. 2: <see cref="Events"/>, <see cref="CalloutPack.ApiVersion"/> and XML callouts.
        /// A new version only adds to the API; LSFD keeps loading packs built for any version from
        /// <see cref="OldestSupportedApiVersion"/> up.
        /// </remarks>
        public const int ApiVersion = 2;

        /// <summary>Packs built for an older API than this are not loaded.</summary>
        public const int OldestSupportedApiVersion = 1;

        /// <summary>The API version of the running LSFD (unlike <see cref="ApiVersion"/>, not baked into your DLL).</summary>
        public static int InstalledApiVersion => ApiVersion;

        public delegate void DutyStateChangedEventHandler(bool onDuty);

        /// <summary>Raised when the player goes on or off duty.</summary>
        public static event DutyStateChangedEventHandler OnDutyStateChanged;

        /// <summary>Registers a callout type. Only needed for callouts outside of Plugins/LSFD/Callouts/, those are registered automatically.</summary>
        public static bool RegisterCallout(Type calloutType) => Lsfd.Registry.Register(calloutType);

        public static string[] GetRegisteredCallouts() => Lsfd.Registry.Names.ToArray();

        /// <summary>Whether the player carries this equipment right now (hand tool, weapon or thermal camera).</summary>
        public static bool IsPlayerCarrying(LSFD.Apparatus.EquipmentType type) => Lsfd.SceneActions.IsCarrying(type);

        /// <summary>Sends an AI unit (e.g. an extra ambulance) from the nearest station to <paramref name="destination"/>.</summary>
        public static bool RequestBackup(LSFD.Apparatus.ApparatusType type, Vector3 destination) => Lsfd.RequestBackup(type, destination);

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
            // An incident the player comes across: the spot ahead of the player was picked already.
            if (AmbientLocation.HasValue)
                return AmbientLocation.Value;

            Vector3 around = Game.LocalPlayer.Character.Position.Around2D(minDistance, maxDistance);
            return World.GetNextPositionOnStreet(around);
        }

        /// <summary>Set while an ambient incident is being set up; see <see cref="CalloutInfoAttribute.AllowAmbient"/>.</summary>
        internal static Vector3? AmbientLocation { get; set; }

        internal static void RaiseDutyStateChanged(bool onDuty) => Events.Raise(OnDutyStateChanged, nameof(OnDutyStateChanged), onDuty);
    }
}
