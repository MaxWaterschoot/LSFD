using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LSFD.API;
using Rage;

namespace LSFD.Core
{
    internal enum InteriorModMode
    {
        /// <summary>Detect the Enable All Interiors mod from its files.</summary>
        Auto,
        Enabled,
        Disabled,
    }

    internal static class Settings
    {
        private const string Path = "Plugins/LSFD.ini";

        public static Keys InteractKey { get; private set; } = Keys.E;
        public static Keys AcceptCalloutKey { get; private set; } = Keys.Y;
        public static Keys NozzlePatternKey { get; private set; } = Keys.X;
        public static Keys PressureUpKey { get; private set; } = Keys.PageUp;
        public static Keys PressureDownKey { get; private set; } = Keys.PageDown;

        public static bool ChangeModelOnDuty { get; private set; } = true;

        public static float HydrantFlowRate { get; private set; } = 80f;
        public static int HoseRopeType { get; private set; } = 4;
        public static bool HideNozzleWeapon { get; private set; } = true;
        public static Keys TabletKey { get; private set; } = Keys.F7;

        public static InteriorModMode EnableAllInteriors { get; private set; } = InteriorModMode.Auto;

        public static bool CalloutsEnabled { get; private set; } = true;
        private static readonly Dictionary<CalloutCategory, bool> EnabledCategories = new Dictionary<CalloutCategory, bool>();
        public static int MinSecondsBetweenCallouts { get; private set; } = 90;
        public static int MaxSecondsBetweenCallouts { get; private set; } = 240;
        public static int AcceptTimeoutSeconds { get; private set; } = 20;

        public static bool IsCategoryEnabled(CalloutCategory category)
        {
            return !EnabledCategories.TryGetValue(category, out bool enabled) || enabled;
        }

        public static void Load()
        {
            var ini = new InitializationFile(Path);
            ini.Create();

            TabletKey = ini.ReadEnum("Keys", "TabletKey", TabletKey);
            InteractKey = ini.ReadEnum("Keys", "InteractKey", InteractKey);
            AcceptCalloutKey = ini.ReadEnum("Keys", "AcceptCalloutKey", AcceptCalloutKey);
            NozzlePatternKey = ini.ReadEnum("Keys", "NozzlePatternKey", NozzlePatternKey);
            PressureUpKey = ini.ReadEnum("Keys", "PressureUpKey", PressureUpKey);
            PressureDownKey = ini.ReadEnum("Keys", "PressureDownKey", PressureDownKey);

            ChangeModelOnDuty = ini.ReadBoolean("Duty", "ChangeModelOnDuty", ChangeModelOnDuty);

            HydrantFlowRate = ini.ReadSingle("Water", "HydrantFlowRate", HydrantFlowRate);
            HoseRopeType = ini.ReadInt32("Water", "HoseRopeType", HoseRopeType);
            HideNozzleWeapon = ini.ReadBoolean("Water", "HideNozzleWeapon", HideNozzleWeapon);

            EnableAllInteriors = ini.ReadEnum("Interiors", "EnableAllInteriors", EnableAllInteriors);

            CalloutsEnabled = ini.ReadBoolean("Callouts", "Enabled", CalloutsEnabled);
            foreach (CalloutCategory category in Enum.GetValues(typeof(CalloutCategory)))
                EnabledCategories[category] = ini.ReadBoolean("Callouts", $"{category}Calls", true);
            MinSecondsBetweenCallouts = ini.ReadInt32("Callouts", "MinSecondsBetweenCallouts", MinSecondsBetweenCallouts);
            MaxSecondsBetweenCallouts = ini.ReadInt32("Callouts", "MaxSecondsBetweenCallouts", MaxSecondsBetweenCallouts);
            AcceptTimeoutSeconds = ini.ReadInt32("Callouts", "AcceptTimeoutSeconds", AcceptTimeoutSeconds);

            if (MaxSecondsBetweenCallouts < MinSecondsBetweenCallouts)
                MaxSecondsBetweenCallouts = MinSecondsBetweenCallouts;

            Game.LogTrivial($"LSFD settings loaded (tablet: {TabletKey}, interact: {InteractKey}, callouts: {CalloutsEnabled}).");
        }
    }
}
