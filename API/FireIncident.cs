using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Firefighting;
using Rage;

namespace LSFD.API
{
    /// <summary>How far a fire incident is along, as reported over the radio.</summary>
    public enum FireStatus
    {
        /// <summary>Spreading: more fire than a moment ago.</summary>
        Growing,
        /// <summary>Burning, but not getting bigger.</summary>
        Burning,
        /// <summary>Well under half of its peak and shrinking: crews are winning.</summary>
        UnderControl,
        /// <summary>No open flames left, but hotspots can still rekindle until they are overhauled.</summary>
        Knockdown,
        /// <summary>Completely out, hotspots included.</summary>
        Out,
    }

    /// <summary>Settings for a fire started with <see cref="Callout.StartFireIncident"/>.</summary>
    public class FireIncidentOptions
    {
        /// <summary>Fire points burning when the incident starts.</summary>
        public int InitialPoints { get; set; } = 2;

        /// <summary>Intensity (0-1) of the initial fire points.</summary>
        public float InitialIntensity { get; set; } = 0.5f;

        /// <summary>How far from the origin the fire can spread, in meters.</summary>
        public float SpreadRadius { get; set; } = 8f;

        /// <summary>Seconds between spreads to a new spot, before <c>GrowthMultiplier</c> in LSFD.ini.</summary>
        public float SpreadSeconds { get; set; } = 50f;

        /// <summary>
        /// Size of each alarm in fire points. The incident goes to the next alarm when it burns as big as
        /// three quarters of this per alarm, and it can never be bigger than one alarm above its current one.
        /// </summary>
        public int PointsPerAlarm { get; set; } = 6;

        /// <summary>The incident never escalates beyond this alarm.</summary>
        public int MaxAlarmLevel { get; set; } = 3;

        /// <summary>
        /// A fire inside a building: the fire stays on the floor, can't spread through walls and the
        /// interior fills with smoke.
        /// </summary>
        public bool IsIndoor { get; set; }

        /// <summary>Gas fires burn hotter and spread faster.</summary>
        public bool IsGasFire { get; set; }

        /// <summary>Meters above the origin where the smoke column rises from, e.g. the roof of the building.</summary>
        public float SmokeColumnHeight { get; set; } = 1f;
    }

    /// <summary>
    /// A fire that LSFD simulates on top of the GTA fires: it grows over time, spreads, escalates to a higher alarm
    /// when nobody fights it and can rekindle from hotspots that weren't overhauled. Start one with
    /// <see cref="Callout.StartFireIncident"/>.
    /// </summary>
    public sealed class FireIncident
    {
        internal readonly List<FirePoint> Points = new List<FirePoint>();

        internal FireIncident(Vector3 origin, FireIncidentOptions options, int alarmLevel, Callout owner)
        {
            Origin = origin;
            Options = options ?? new FireIncidentOptions();
            AlarmLevel = Math.Max(1, alarmLevel);
            Owner = owner;
            StartedAt = Game.GameTime;
        }

        public Vector3 Origin { get; }
        public FireIncidentOptions Options { get; }

        /// <summary>The callout the incident belongs to, or null.</summary>
        public Callout Owner { get; }

        public FireStatus Status { get; internal set; } = FireStatus.Growing;

        /// <summary>1 for a 1st alarm etc. Goes up on its own when the fire grows too big.</summary>
        public int AlarmLevel { get; internal set; }

        /// <summary>Total intensity of all open flames; one fully burning spot counts as 1.</summary>
        public float Size { get; internal set; }

        /// <summary>The biggest <see cref="Size"/> so far.</summary>
        public float PeakSize { get; internal set; }

        public int BurningPoints => Points.Count(p => p.State == FirePointState.Burning);

        /// <summary>Spots without flames that can still rekindle.</summary>
        public int Hotspots => Points.Count(p => p.State == FirePointState.Smoldering);

        public bool IsOut => Status == FireStatus.Out;

        /// <summary>Game time (ms) the incident started.</summary>
        public uint StartedAt { get; }

        internal bool IsRemoved { get; private set; }

        /// <summary>Raised when <see cref="Status"/> changes.</summary>
        public event Action<FireIncident, FireStatus> StatusChanged;

        /// <summary>Puts the fire out instantly and removes everything it created.</summary>
        public void Remove()
        {
            IsRemoved = true;
        }

        internal void RaiseStatusChanged()
        {
            StatusChanged?.Invoke(this, Status);
        }
    }

    public static class FireStatusExtensions
    {
        public static string ToDisplayName(this FireStatus status)
        {
            switch (status)
            {
                case FireStatus.Growing: return "~r~Growing";
                case FireStatus.Burning: return "~o~Burning";
                case FireStatus.UnderControl: return "~y~Under control";
                case FireStatus.Knockdown: return "~b~Knockdown";
                case FireStatus.Out: return "~g~Fire out";
                default: return status.ToString();
            }
        }
    }
}
