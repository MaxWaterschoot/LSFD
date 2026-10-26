using System;

namespace LSFD.API
{
    /// <summary>How often a callout is picked compared to the other registered callouts.</summary>
    public enum CalloutProbability
    {
        Never = 0,
        VeryLow = 1,
        Low = 2,
        Medium = 4,
        High = 8,
        VeryHigh = 16,
    }

    /// <summary>The kind of incident; players can turn whole categories off in LSFD.ini.</summary>
    public enum CalloutCategory
    {
        Fire,
        Traffic,
        Medical,
        Rescue,
        /// <summary>Gas leaks, fuel spills and other hazardous materials.</summary>
        Hazmat,
    }

    /// <summary>Response priority, as dispatch centers use it.</summary>
    public enum CalloutPriority
    {
        /// <summary>Life or property in immediate danger: respond Code 3 (lights and sirens).</summary>
        Priority1 = 1,
        /// <summary>Urgent but stable: respond Code 3, drive with care.</summary>
        Priority2 = 2,
        /// <summary>Non-emergency: respond Code 2, no lights and sirens.</summary>
        Priority3 = 3,
    }

    /// <summary>Marks a <see cref="Callout"/> so LSFD can register it.</summary>
    /// <example>
    /// <code>[CalloutInfo("Dumpster Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority3)]</code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CalloutInfoAttribute : Attribute
    {
        /// <summary>Unique name of the callout, also used by <see cref="Functions.StartCallout"/>.</summary>
        public string Name { get; }

        public CalloutProbability Probability { get; }

        public CalloutCategory Category { get; set; } = CalloutCategory.Fire;

        /// <summary>Default priority; a callout can change <see cref="Callout.Priority"/> before it is displayed.</summary>
        public CalloutPriority Priority { get; set; } = CalloutPriority.Priority2;

        /// <summary>
        /// Set to true when the callout takes place in interiors that are locked in the vanilla game.
        /// It is then only dispatched when <see cref="Interiors.AreInteriorsUnlocked"/> is true.
        /// </summary>
        public bool RequiresOpenInteriors { get; set; }

        /// <summary>
        /// Set to true when the incident can also be one the player comes across while driving around. LSFD then picks a
        /// spot on the road ahead of the player, so the callout must take its location from
        /// <see cref="Functions.GetRandomCalloutLocation"/>. Added in API version 2.
        /// </summary>
        public bool AllowAmbient { get; set; }

        public CalloutInfoAttribute(string name, CalloutProbability probability)
        {
            Name = name;
            Probability = probability;
        }
    }

    public static class CalloutPriorityExtensions
    {
        /// <summary>Short code shown in notifications and the tablet, e.g. "~r~P1".</summary>
        public static string ToCode(this CalloutPriority priority)
        {
            switch (priority)
            {
                case CalloutPriority.Priority1: return "~r~P1~s~";
                case CalloutPriority.Priority2: return "~o~P2~s~";
                default: return "~y~P3~s~";
            }
        }

        public static string ToResponse(this CalloutPriority priority)
        {
            return priority == CalloutPriority.Priority3 ? "Code 2" : "Code 3";
        }

        /// <summary>"1st Alarm", "2nd Alarm", ...; empty for 0.</summary>
        public static string AlarmToDisplay(int alarmLevel)
        {
            switch (alarmLevel)
            {
                case 0: return "";
                case 1: return "1st Alarm";
                case 2: return "2nd Alarm";
                case 3: return "3rd Alarm";
                default: return $"{alarmLevel}th Alarm";
            }
        }
    }
}
