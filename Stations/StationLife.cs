using System.IO;
using System.Media;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Crew;
using LSFD.UI;
using LSFD.Utils;
using Rage;
using Rage.Native;

namespace LSFD.Stations
{
    /// <summary>
    /// Life at the station: when you go on duty the first-due engine stands in the bay with its crew around, and when a
    /// call comes in the station alarm sounds, the crew runs to the engine and your turnout time is measured.
    /// </summary>
    internal class StationLife
    {
        private const float AtStationDistance = 100f;
        private const float ParkedDistance = 15f;
        private const float TurnedOutDistance = 25f;
        private const int AlarmTones = 4;
        private const uint ToneInterval = 1200;

        // Drop a StationAlarm.wav in Plugins/LSFD/Audio/Alarms/ for real station tones.
        private static readonly string AlarmSoundFile = Path.Combine(ModFolder.AlarmAudio, "StationAlarm.wav");

        private readonly ApparatusManager apparatus;
        private readonly CrewManager crew;
        private readonly DutyManager duty;

        private FireApparatus firstDue;
        private Vector3 bay;
        private uint alarmAt;
        private int tonesPlayed;
        private bool turningOut;
        private SoundPlayer alarmSound;
        private bool alarmSoundLoaded;

        public StationLife(ApparatusManager apparatus, CrewManager crew, DutyManager duty)
        {
            this.apparatus = apparatus;
            this.crew = crew;
            this.duty = duty;
        }

        public void OnDutyStateChanged(bool onDuty)
        {
            firstDue = null;
            turningOut = false;
            if (!onDuty || !Settings.StationApparatus || duty.HomeStation == null)
                return;

            ApparatusDefinition engine = apparatus.FindDefinition(ApparatusType.Engine);
            if (engine == null)
                return;

            FireStation station = duty.HomeStation;
            firstDue = apparatus.Spawn(engine, station);
            if (firstDue == null)
                return;

            station.GetVehicleSpawn(out bay, out _);
            crew.StandBy(firstDue, station.Position);
        }

        /// <summary>A call was accepted: sound the station alarm if the player and the engine are still in quarters.</summary>
        public void OnCalloutAccepted(Callout callout)
        {
            if (!Settings.StationAlarm || firstDue == null || !firstDue.IsValid || duty.HomeStation == null)
                return;
            if (firstDue.Vehicle.DistanceTo(bay) > ParkedDistance || Game.LocalPlayer.Character.DistanceTo(duty.HomeStation.Position) > AtStationDistance)
                return;

            alarmAt = Game.GameTime;
            tonesPlayed = 0;
            turningOut = true;
            crew.MountUp(firstDue);
            Notify.Dispatch("~r~Station alarm", $"{callout.CalloutMessage}: turn out! Get to the ~y~{firstDue.Definition.Name}~s~.");
        }

        public void Process()
        {
            if (!turningOut)
                return;

            if (firstDue == null || !firstDue.IsValid)
            {
                turningOut = false;
                return;
            }

            if (tonesPlayed < AlarmTones && Game.GameTime - alarmAt >= tonesPlayed * ToneInterval)
            {
                PlayTone();
                tonesPlayed++;
            }

            if (firstDue.Vehicle.DistanceTo(bay) > TurnedOutDistance)
            {
                turningOut = false;
                float seconds = (Game.GameTime - alarmAt) / 1000f;
                string rating = seconds <= 60f ? "~g~" : seconds <= 90f ? "~y~" : "~o~";
                Game.DisplaySubtitle($"Turnout time: {rating}{(int)seconds / 60}:{(int)seconds % 60:00}", 5000);
            }
        }

        private void PlayTone()
        {
            if (!alarmSoundLoaded)
            {
                alarmSoundLoaded = true;
                if (File.Exists(AlarmSoundFile))
                {
                    try
                    {
                        alarmSound = new SoundPlayer(AlarmSoundFile);
                        alarmSound.Load();
                    }
                    catch (System.Exception e)
                    {
                        Game.LogTrivial($"LSFD: could not load {AlarmSoundFile}: {e.Message}");
                        alarmSound = null;
                    }
                }
            }

            if (alarmSound != null)
            {
                // A recording plays once, it doesn't need the separate tones.
                if (tonesPlayed == 0)
                    alarmSound.Play();
            }
            else
            {
                NativeFunction.CallByName<uint>("PLAY_SOUND_FRONTEND", -1, "TIMER_STOP", "HUD_MINI_GAME_SOUNDSET", true);
            }
        }
    }
}
