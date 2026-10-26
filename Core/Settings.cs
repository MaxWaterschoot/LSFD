using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LSFD.API;
using LSFD.Utils;
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
        public static Keys InteractKey { get; private set; } = Keys.E;
        public static Keys AcceptCalloutKey { get; private set; } = Keys.Y;
        public static Keys NozzlePatternKey { get; private set; } = Keys.X;
        public static Keys PressureUpKey { get; private set; } = Keys.PageUp;
        public static Keys PressureDownKey { get; private set; } = Keys.PageDown;
        public static Keys MaskKey { get; private set; } = Keys.K;
        public static Keys FoamKey { get; private set; } = Keys.H;
        public static Keys ThermalCameraKey { get; private set; } = Keys.T;

        public static bool ChangeModelOnDuty { get; private set; } = true;

        public static float HydrantFlowRate { get; private set; } = 80f;
        public static int HoseRopeType { get; private set; } = 4;
        public static bool HideNozzleWeapon { get; private set; } = true;
        public static Keys TabletKey { get; private set; } = Keys.F7;

        public static float FireGrowthMultiplier { get; private set; } = 1f;
        public static bool FireEscalation { get; private set; } = true;
        public static float RekindleChance { get; private set; } = 0.5f;
        public static bool SmokeColumns { get; private set; } = true;

        public static int AirSupplySeconds { get; private set; } = 600;
        public static bool TurnoutGearFireProof { get; private set; } = true;
        public static bool SmokeAndHeatDamage { get; private set; } = true;
        public static bool SmokeScreenEffect { get; private set; } = true;

        public static string FanModel { get; private set; } = "prop_ld_fan_01";
        public static float FanHeading { get; private set; }
        public static bool PatientTransport { get; private set; } = true;
        public static bool StationApparatus { get; private set; } = true;
        public static bool StationAlarm { get; private set; } = true;
        public static string StretcherModel { get; private set; } = "v_med_emptybed";
        public static float StretcherHeading { get; private set; }
        public static float PatientOnStretcherHeight { get; private set; }

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
            var keys = new InitializationFile(ModFolder.KeysFile);
            keys.Create();

            TabletKey = keys.ReadEnum("Keys", "TabletKey", TabletKey);
            InteractKey = keys.ReadEnum("Keys", "InteractKey", InteractKey);
            AcceptCalloutKey = keys.ReadEnum("Keys", "AcceptCalloutKey", AcceptCalloutKey);
            NozzlePatternKey = keys.ReadEnum("Keys", "NozzlePatternKey", NozzlePatternKey);
            PressureUpKey = keys.ReadEnum("Keys", "PressureUpKey", PressureUpKey);
            PressureDownKey = keys.ReadEnum("Keys", "PressureDownKey", PressureDownKey);
            MaskKey = keys.ReadEnum("Keys", "MaskKey", MaskKey);
            FoamKey = keys.ReadEnum("Keys", "FoamKey", FoamKey);
            ThermalCameraKey = keys.ReadEnum("Keys", "ThermalCameraKey", ThermalCameraKey);

            var ini = new InitializationFile(ModFolder.SettingsFile);
            ini.Create();

            ChangeModelOnDuty = ini.ReadBoolean("Duty", "ChangeModelOnDuty", ChangeModelOnDuty);

            HydrantFlowRate = ini.ReadSingle("Water", "HydrantFlowRate", HydrantFlowRate);
            HoseRopeType = ini.ReadInt32("Water", "HoseRopeType", HoseRopeType);
            HideNozzleWeapon = ini.ReadBoolean("Water", "HideNozzleWeapon", HideNozzleWeapon);

            FireGrowthMultiplier = Math.Max(0.1f, ini.ReadSingle("Fire", "GrowthMultiplier", FireGrowthMultiplier));
            FireEscalation = ini.ReadBoolean("Fire", "AlarmEscalation", FireEscalation);
            RekindleChance = Math.Max(0f, Math.Min(1f, ini.ReadSingle("Fire", "RekindleChance", RekindleChance)));
            SmokeColumns = ini.ReadBoolean("Fire", "SmokeColumns", SmokeColumns);

            AirSupplySeconds = Math.Max(30, ini.ReadInt32("Safety", "AirSupplySeconds", AirSupplySeconds));
            TurnoutGearFireProof = ini.ReadBoolean("Safety", "TurnoutGearFireProof", TurnoutGearFireProof);
            SmokeAndHeatDamage = ini.ReadBoolean("Safety", "SmokeAndHeatDamage", SmokeAndHeatDamage);
            SmokeScreenEffect = ini.ReadBoolean("Safety", "SmokeScreenEffect", SmokeScreenEffect);

            FanModel = ini.ReadString("Ventilation", "FanModel", FanModel);
            FanHeading = ini.ReadSingle("Ventilation", "FanHeading", FanHeading);
            StationApparatus = ini.ReadBoolean("Station", "FirstDueEngine", StationApparatus);
            StationAlarm = ini.ReadBoolean("Station", "StationAlarm", StationAlarm);
            PatientTransport = ini.ReadBoolean("Medical", "PatientTransport", PatientTransport);
            StretcherModel = ini.ReadString("Medical", "StretcherModel", StretcherModel);
            StretcherHeading = ini.ReadSingle("Medical", "StretcherHeading", StretcherHeading);
            PatientOnStretcherHeight = ini.ReadSingle("Medical", "PatientOnStretcherHeight", PatientOnStretcherHeight);

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
