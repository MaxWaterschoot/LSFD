using System;
using LSFD.Core;
using LSFD.UI;
using LSFD.Utils;
using Rage;
using Rage.Native;

namespace LSFD.Safety
{
    /// <summary>
    /// The player's SCBA: a cylinder of air that lasts <see cref="Settings.AirSupplySeconds"/>, a face piece to put on
    /// before going into smoke, the low-air alarm and the PASS device. Cylinders are swapped at an apparatus that carries them.
    /// </summary>
    internal class BreathingApparatus
    {
        private const float LowAirFraction = 0.25f;
        private const float CriticalAirFraction = 0.1f;
        private const uint LowAirAlarmInterval = 2000;
        private const uint CriticalAirAlarmInterval = 700;
        private const float SprintAirUse = 1.8f;
        private const float RunAirUse = 1.3f;

        private const string MaskAnimationDictionary = "mp_masks@standard_car@ds@";
        private const string MaskAnimation = "put_on_mask";

        // Drop a LOW_AIR.wav in Plugins/LSFD/Audio/Alarms/ to replace the default beep with a real low-air bell.
        private const string LowAirSound = "Alarms/LOW_AIR.wav";
        private const string PassSound = "Alarms/PASS_ALARM.wav";

        // The PASS device sounds when a firefighter hasn't moved for this long.
        private const uint PassAlarmTime = 30000;
        private const float StillSpeed = 0.3f;

        private bool lowAirWarned;
        private uint stillSince;
        private SoundHandle passAlarm;
        private bool emptyWarned;
        private uint nextAlarmAt;

        public BreathingApparatus()
        {
            Air = Capacity;
        }

        /// <summary>Seconds of air in a full cylinder.</summary>
        public float Capacity => Settings.AirSupplySeconds;

        /// <summary>Seconds of air left.</summary>
        public float Air { get; private set; }

        public float AirFraction => Air / Capacity;
        public bool IsMaskOn { get; private set; }

        /// <summary>Whether the player is breathing clean air from the cylinder.</summary>
        public bool IsSupplying => IsMaskOn && Air > 0f;

        public bool IsLowOnAir => AirFraction <= LowAirFraction;

        /// <summary>Puts a full cylinder on the player's back.</summary>
        public void Refill()
        {
            Air = Capacity;
            lowAirWarned = false;
            emptyWarned = false;
        }

        public void SetMask(bool on)
        {
            if (on == IsMaskOn)
                return;

            IsMaskOn = on;
            Ped player = Game.LocalPlayer.Character;
            if (player.Exists() && !player.IsInAnyVehicle(false))
                player.Tasks.PlayAnimation(MaskAnimationDictionary, MaskAnimation, 4f, AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);

            Game.DisplaySubtitle(on
                ? L.F($"~b~Face piece on.~s~ Breathing from your cylinder ({AirFraction:P0}).")
                : L.T("Face piece off."), 2500);
        }

        public void Process(bool onDuty)
        {
            Ped player = Game.LocalPlayer.Character;
            if (!onDuty || !player.Exists() || player.IsDead)
            {
                IsMaskOn = false;
                StopPassAlarm();
                return;
            }

            if (Game.IsKeyDown(Settings.MaskKey))
                SetMask(!IsMaskOn);

            if (!IsMaskOn)
            {
                StopPassAlarm();
                return;
            }

            float use = player.IsSprinting ? SprintAirUse : player.IsRunning ? RunAirUse : 1f;
            Air = Math.Max(0f, Air - Game.FrameTime * use);
            ProcessAlarms();
            ProcessPassDevice(player);
        }

        // Standing on the nozzle or working a tool counts as moving; lying still in the smoke doesn't.
        private void ProcessPassDevice(Ped player)
        {
            bool still = player.Speed < StillSpeed && !player.IsInAnyVehicle(false) && !Lsfd.Hose.IsHolding && !Lsfd.Interactions.Progress.HasValue;
            if (!still || !Settings.PassAlarm)
            {
                stillSince = Game.GameTime;
                StopPassAlarm();
                return;
            }

            if (passAlarm == null && Game.GameTime - stillSince >= PassAlarmTime)
            {
                passAlarm = Sound.PlayLooped(PassSound, Settings.AlarmVolume);
                Game.DisplaySubtitle(L.T("~r~PASS alarm~s~: you haven't moved for 30 seconds. Move to reset it."), 4000);
            }
        }

        private void StopPassAlarm()
        {
            passAlarm?.Stop();
            passAlarm = null;
        }

        private void ProcessAlarms()
        {
            if (Air <= 0f)
            {
                if (!emptyWarned)
                {
                    emptyWarned = true;
                    Notify.Warning(L.T("~r~Your SCBA cylinder is empty!~s~ Get out of the smoke and swap it at an engine."));
                }
                return;
            }

            if (!IsLowOnAir)
                return;

            if (!lowAirWarned)
            {
                lowAirWarned = true;
                Notify.Warning(L.F($"~o~Low air~s~: {AirFraction:P0} left. Head out and swap your cylinder at an engine."));
            }

            if (Game.GameTime < nextAlarmAt)
                return;

            nextAlarmAt = Game.GameTime + (AirFraction <= CriticalAirFraction ? CriticalAirAlarmInterval : LowAirAlarmInterval);
            PlayLowAirAlarm();
        }

        private void PlayLowAirAlarm()
        {
            if (!Sound.Exists(LowAirSound) || Sound.Play(Settings.AlarmVolume, LowAirSound) == null)
                NativeFunction.CallByName<uint>("PLAY_SOUND_FRONTEND", -1, "5_SEC_WARNING", "HUD_MINI_GAME_SOUNDSET", true);
        }
    }
}
