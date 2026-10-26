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

        /// <summary>The file name in Plugins/LSFD/Languages/ without .xml; "en" is English.</summary>
        public static string Language { get; private set; } = "en";

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

        public static bool DisableGameFireDispatch { get; private set; } = true;
        public static bool TrafficControl { get; private set; } = true;
        public static bool PoliceAtScene { get; private set; } = true;
        public static string PoliceVehicle { get; private set; } = "police";
        public static string PoliceOfficerModel { get; private set; } = "s_m_y_cop_01";
        public static string SheriffVehicle { get; private set; } = "sheriff";
        public static string SheriffOfficerModel { get; private set; } = "s_m_y_sheriff_01";
        public static bool AmbientIncidents { get; private set; } = true;
        public static int MinSecondsBetweenAmbientIncidents { get; private set; } = 300;
        public static int MaxSecondsBetweenAmbientIncidents { get; private set; } = 900;
        public static bool OtherUnitsIncidents { get; private set; } = true;
        public static int MinSecondsBetweenOtherIncidents { get; private set; } = 120;
        public static int MaxSecondsBetweenOtherIncidents { get; private set; } = 360;
        public static bool OtherUnitsNotifications { get; private set; } = true;

        public static bool DispatchRadio { get; private set; } = true;
        public static float RadioVolume { get; private set; } = 0.8f;
        public static float AlarmVolume { get; private set; } = 0.7f;
        public static bool PagerAlert { get; private set; } = true;
        public static bool SceneAlarms { get; private set; } = true;
        public static bool PassAlarm { get; private set; } = true;
        public static bool CprMetronome { get; private set; } = true;

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

            Language = ini.ReadString("General", "Language", Language).Trim();
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

            DisableGameFireDispatch = ini.ReadBoolean("World", "DisableGameFireDispatch", DisableGameFireDispatch);
            TrafficControl = ini.ReadBoolean("World", "TrafficControl", TrafficControl);
            PoliceAtScene = ini.ReadBoolean("World", "PoliceAtScene", PoliceAtScene);
            PoliceVehicle = ini.ReadString("World", "PoliceVehicle", PoliceVehicle);
            PoliceOfficerModel = ini.ReadString("World", "PoliceOfficerModel", PoliceOfficerModel);
            SheriffVehicle = ini.ReadString("World", "SheriffVehicle", SheriffVehicle);
            SheriffOfficerModel = ini.ReadString("World", "SheriffOfficerModel", SheriffOfficerModel);
            AmbientIncidents = ini.ReadBoolean("World", "AmbientIncidents", AmbientIncidents);
            MinSecondsBetweenAmbientIncidents = Math.Max(30, ini.ReadInt32("World", "MinSecondsBetweenAmbientIncidents", MinSecondsBetweenAmbientIncidents));
            MaxSecondsBetweenAmbientIncidents = Math.Max(MinSecondsBetweenAmbientIncidents,
                ini.ReadInt32("World", "MaxSecondsBetweenAmbientIncidents", MaxSecondsBetweenAmbientIncidents));
            OtherUnitsIncidents = ini.ReadBoolean("World", "OtherUnitsIncidents", OtherUnitsIncidents);
            MinSecondsBetweenOtherIncidents = Math.Max(30, ini.ReadInt32("World", "MinSecondsBetweenOtherIncidents", MinSecondsBetweenOtherIncidents));
            MaxSecondsBetweenOtherIncidents = Math.Max(MinSecondsBetweenOtherIncidents,
                ini.ReadInt32("World", "MaxSecondsBetweenOtherIncidents", MaxSecondsBetweenOtherIncidents));
            OtherUnitsNotifications = ini.ReadBoolean("World", "OtherUnitsNotifications", OtherUnitsNotifications);

            DispatchRadio = ini.ReadBoolean("Audio", "DispatchRadio", DispatchRadio);
            RadioVolume = ReadVolume(ini, "RadioVolume", RadioVolume);
            AlarmVolume = ReadVolume(ini, "AlarmVolume", AlarmVolume);
            PagerAlert = ini.ReadBoolean("Audio", "PagerAlert", PagerAlert);
            SceneAlarms = ini.ReadBoolean("Audio", "SceneAlarms", SceneAlarms);
            PassAlarm = ini.ReadBoolean("Audio", "PassAlarm", PassAlarm);
            CprMetronome = ini.ReadBoolean("Audio", "CprMetronome", CprMetronome);

            Game.LogTrivial($"LSFD settings loaded (tablet: {TabletKey}, interact: {InteractKey}, callouts: {CalloutsEnabled}).");
        }

        // 0 to 100 in the ini.
        private static float ReadVolume(InitializationFile ini, string key, float fallback)
        {
            return Math.Max(0, Math.Min(100, ini.ReadInt32("Audio", key, (int)Math.Round(fallback * 100f)))) / 100f;
        }
    }
}
