using System;
using LSFD.Apparatus;
using Rage;

namespace LSFD.API
{
    public delegate void CalloutEventHandler(Callout callout);

    public delegate void FireIncidentEventHandler(FireIncident incident);

    public delegate void PatientEventHandler(Ped patient);

    /// <param name="vehicle">The apparatus that was spawned.</param>
    /// <param name="type">What kind of unit it is.</param>
    /// <param name="isAiUnit">True for AI units responding on their own (backup, alarms, ambulances), false for the player's.</param>
    public delegate void ApparatusSpawnedEventHandler(Vehicle vehicle, ApparatusType type, bool isAiUnit);

    /// <summary>
    /// Things that happen in LSFD that other plugins can react to. Subscribe in <see cref="CalloutPack.Initialize"/> and
    /// unsubscribe in <see cref="CalloutPack.Finally"/>. Handlers run on the LSFD game fiber; an exception in one is logged
    /// and does not affect LSFD or the other handlers.
    /// </summary>
    /// <remarks>Added in API version 2.</remarks>
    public static class Events
    {
        /// <summary>The player accepted a call; <see cref="Callout.OnCalloutAccepted"/> has already run.</summary>
        public static event CalloutEventHandler OnCalloutAccepted;

        /// <summary>An accepted call ended. <see cref="Callout.IsCompleted"/> tells whether it was handled or cancelled.</summary>
        public static event CalloutEventHandler OnCalloutEnded;

        /// <summary>A <see cref="FireIncident"/> is completely out, hotspots included.</summary>
        public static event FireIncidentEventHandler OnFireExtinguished;

        /// <summary>A victim was carried out of a building to safety.</summary>
        public static event PatientEventHandler OnPatientRescued;

        /// <summary>A patient became stable, by treatment or by successful CPR.</summary>
        public static event PatientEventHandler OnPatientStabilized;

        /// <summary>An ambulance handed a patient over at a hospital. The ped is deleted right after this event.</summary>
        public static event PatientEventHandler OnPatientTransported;

        /// <summary>An apparatus was spawned at a station, for the player or as an AI unit.</summary>
        public static event ApparatusSpawnedEventHandler OnApparatusSpawned;

        internal static void RaiseCalloutAccepted(Callout callout) => Raise(OnCalloutAccepted, nameof(OnCalloutAccepted), callout);

        internal static void RaiseCalloutEnded(Callout callout) => Raise(OnCalloutEnded, nameof(OnCalloutEnded), callout);

        internal static void RaiseFireExtinguished(FireIncident incident) => Raise(OnFireExtinguished, nameof(OnFireExtinguished), incident);

        internal static void RaisePatientRescued(Ped patient) => Raise(OnPatientRescued, nameof(OnPatientRescued), patient);

        internal static void RaisePatientStabilized(Ped patient) => Raise(OnPatientStabilized, nameof(OnPatientStabilized), patient);

        internal static void RaisePatientTransported(Ped patient) => Raise(OnPatientTransported, nameof(OnPatientTransported), patient);

        internal static void RaiseApparatusSpawned(Vehicle vehicle, ApparatusType type, bool isAiUnit) =>
            Raise(OnApparatusSpawned, nameof(OnApparatusSpawned), vehicle, type, isAiUnit);

        // One faulty subscriber shouldn't break the others or LSFD itself; a handler from a pack built against an
        // incompatible LSFD fails here too (MissingMethodException etc.).
        internal static void Raise(Delegate handlers, string eventName, params object[] args)
        {
            if (handlers == null)
                return;

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke(args);
                }
                catch (Exception e)
                {
                    Exception cause = e.InnerException ?? e;
                    Game.LogTrivial($"LSFD: {eventName} handler {handler.Method.DeclaringType}.{handler.Method.Name} threw: {cause}");
                }
            }
        }
    }
}
