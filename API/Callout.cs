using System.Collections.Generic;
using System.Drawing;
using LSFD.Core;
using Rage;

namespace LSFD.API
{
    public enum CalloutState
    {
        /// <summary>Offered to the player, waiting to be accepted.</summary>
        Displayed,
        Accepted,
        Ended,
    }

    /// <summary>
    /// Base class for every LSFD callout. Inherit from it, add a <see cref="CalloutInfoAttribute"/>
    /// and put the compiled DLL in GTA V/Plugins/LSFD/Callouts/ to have LSFD load it.
    /// </summary>
    /// <remarks>
    /// The life cycle is: <see cref="OnBeforeCalloutDisplayed"/> → (player accepts) <see cref="OnCalloutAccepted"/>
    /// → <see cref="OnArrival"/> once the player is close → <see cref="Process"/> every tick → <see cref="End"/>.
    /// Call <see cref="Complete"/> (or <see cref="End"/>) yourself once the scene is dealt with.
    /// </remarks>
    public abstract class Callout
    {
        private static readonly Color AreaColor = Color.FromArgb(150, 230, 70, 30);

        private readonly List<FireIncident> fireIncidents = new List<FireIncident>();
        private readonly List<SceneAction> sceneActions = new List<SceneAction>();
        private Blip areaBlip;
        private Blip routeBlip;

        /// <summary>The name from the <see cref="CalloutInfoAttribute"/>.</summary>
        public string FriendlyName { get; internal set; }

        public CalloutCategory Category { get; internal set; }

        /// <summary>Response priority; starts at the attribute's value and can be changed in <see cref="OnBeforeCalloutDisplayed"/>.</summary>
        public CalloutPriority Priority { get; protected set; }

        /// <summary>
        /// Size of the incident for fires: 1 for a 1st alarm, 2 for a 2nd alarm etc. 0 when it doesn't apply.
        /// LSFD sends one extra AI engine company for every alarm above the first.
        /// </summary>
        public int AlarmLevel { get; protected set; }

        /// <summary>Where AI units drive to and stage. Defaults to <see cref="CalloutPosition"/>.</summary>
        public Vector3 StagingPosition
        {
            get => stagingPosition ?? CalloutPosition;
            protected set => stagingPosition = value;
        }

        private Vector3? stagingPosition;

        /// <summary>Where the incident is. Must be set in <see cref="OnBeforeCalloutDisplayed"/>.</summary>
        public Vector3 CalloutPosition { get; protected set; }

        /// <summary>Title shown in the dispatch notification, e.g. "Vehicle Fire".</summary>
        public string CalloutMessage { get; protected set; }

        /// <summary>Extra details shown in the dispatch notification.</summary>
        public string CalloutAdvisory { get; protected set; }

        /// <summary>Radius of the area blip around <see cref="CalloutPosition"/>.</summary>
        public float CalloutAreaRadius { get; protected set; } = 40f;

        /// <summary>How close the player has to get before <see cref="OnArrival"/> is called.</summary>
        public float ArrivalDistance { get; protected set; } = 120f;

        public CalloutState State { get; private set; } = CalloutState.Displayed;
        public bool HasArrived { get; private set; }

        /// <summary>Whether the callout ended through <see cref="Complete"/>.</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>Set up the callout (position, message). Return false to cancel it, e.g. when no good location was found.</summary>
        public virtual bool OnBeforeCalloutDisplayed() => true;

        /// <summary>The player accepted the call. Return false to abort.</summary>
        public virtual bool OnCalloutAccepted() => true;

        /// <summary>The player declined or ignored the call.</summary>
        public virtual void OnCalloutNotAccepted()
        {
        }

        /// <summary>Called once when the player is within <see cref="ArrivalDistance"/>. Spawn the scene here, so it is streamed in.</summary>
        protected virtual void OnArrival()
        {
        }

        /// <summary>Called every tick while the callout is running.</summary>
        public virtual void Process()
        {
        }

        /// <summary>Clean up everything the callout created. Called once when the callout ends.</summary>
        protected virtual void OnEnd()
        {
        }

        /// <summary>Ends the callout as successfully handled.</summary>
        public void Complete()
        {
            IsCompleted = true;
            End();
        }

        /// <summary>Ends the callout and cleans it up.</summary>
        public void End()
        {
            if (State == CalloutState.Ended)
                return;

            State = CalloutState.Ended;
            DeleteBlips();
            try
            {
                OnEnd();
            }
            finally
            {
                RemoveFireIncidents();
            }
        }

        /// <summary>
        /// Starts a fire that grows, spreads and escalates on its own (see <see cref="FireIncident"/>). Start it in
        /// <see cref="OnCalloutAccepted"/> so it keeps growing while the player responds: the later they are, the bigger
        /// the fire. The incident uses and raises this callout's <see cref="AlarmLevel"/>, and is removed when the callout ends.
        /// </summary>
        protected FireIncident StartFireIncident(Vector3 origin, FireIncidentOptions options = null)
        {
            FireIncident incident = Lsfd.Fire.Start(origin, options, AlarmLevel, this);
            fireIncidents.Add(incident);
            return incident;
        }

        /// <summary>Adds a <see cref="SceneAction"/> that is removed when the callout ends.</summary>
        protected SceneAction AddSceneAction(SceneAction action)
        {
            sceneActions.Add(action);
            return SceneActions.Add(action);
        }

        internal void RaiseAlarmLevel(int level)
        {
            if (level > AlarmLevel)
                AlarmLevel = level;
        }

        internal void Initialize(string friendlyName, CalloutCategory category, CalloutPriority priority)
        {
            FriendlyName = friendlyName;
            Category = category;
            Priority = priority;
        }

        /// <summary>Picks an alarm level: mostly 1st alarms, sometimes bigger.</summary>
        protected static int RollAlarmLevel(int secondAlarmChance = 25, int thirdAlarmChance = 8)
        {
            int roll = MathHelper.GetRandomInteger(100);
            return roll < thirdAlarmChance ? 3 : roll < thirdAlarmChance + secondAlarmChance ? 2 : 1;
        }

        internal void Display()
        {
            areaBlip = new Blip(CalloutPosition, CalloutAreaRadius) { Color = AreaColor, Name = CalloutMessage };
        }

        internal bool Accept()
        {
            State = CalloutState.Accepted;

            routeBlip = new Blip(CalloutPosition) { Color = Color.OrangeRed, Name = CalloutMessage };
            routeBlip.EnableRoute(Color.OrangeRed);

            return OnCalloutAccepted();
        }

        internal void Decline()
        {
            State = CalloutState.Ended;
            DeleteBlips();
            try
            {
                OnCalloutNotAccepted();
            }
            finally
            {
                RemoveFireIncidents();
            }
        }

        internal void Tick()
        {
            if (!HasArrived && Game.LocalPlayer.Character.DistanceTo(CalloutPosition) <= ArrivalDistance)
            {
                HasArrived = true;
                if (routeBlip.Exists())
                    routeBlip.Delete();
                OnArrival();
            }

            if (State == CalloutState.Accepted)
                Process();
        }

        private void RemoveFireIncidents()
        {
            foreach (FireIncident incident in fireIncidents)
                incident.Remove();
            fireIncidents.Clear();
            foreach (SceneAction action in sceneActions)
                action.Remove();
            sceneActions.Clear();
        }

        private void DeleteBlips()
        {
            if (areaBlip.Exists())
                areaBlip.Delete();
            if (routeBlip.Exists())
                routeBlip.Delete();
        }
    }
}
