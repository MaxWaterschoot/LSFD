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

        // Drop a STATION_ALARM.wav in Plugins/LSFD/Audio/Alarms/ for a recording of real station tones.
        private const string RecordedAlarm = "Alarms/STATION_ALARM.wav";
        private static readonly string[] DefaultAlarm = { "Alarms/STATION_PA_CHIME.wav", "Dispatch/Tones/FIRE_TONES.wav" };

        private readonly ApparatusManager apparatus;
        private readonly CrewManager crew;
        private readonly DutyManager duty;

        private FireApparatus firstDue;
        private Vector3 bay;
        private uint alarmAt;
        private int tonesPlayed;
        private bool turningOut;
        private bool alarmPlaying;

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
            alarmPlaying = PlayAlarm();
            turningOut = true;
            crew.MountUp(firstDue);
            Notify.Dispatch(L.T("~r~Station alarm"), L.F($"{callout.CalloutMessage}: turn out! Get to the ~y~{firstDue.Definition.Name}~s~."));
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

            if (!alarmPlaying && tonesPlayed < AlarmTones && Game.GameTime - alarmAt >= tonesPlayed * ToneInterval)
            {
                PlayTone();
                tonesPlayed++;
            }

            if (firstDue.Vehicle.DistanceTo(bay) > TurnedOutDistance)
            {
                turningOut = false;
                float seconds = (Game.GameTime - alarmAt) / 1000f;
                string rating = seconds <= 60f ? "~g~" : seconds <= 90f ? "~y~" : "~o~";
                Game.DisplaySubtitle(L.F($"Turnout time: {rating}{(int)seconds / 60}:{(int)seconds % 60:00}"), 5000);
            }
        }

        // The recording when there is one, otherwise the PA chime and the fire tones; game beeps when neither is there.
        private static bool PlayAlarm()
        {
            if (Sound.Exists(RecordedAlarm))
                return Sound.Play(Settings.AlarmVolume, RecordedAlarm) != null;
            return Sound.Play(Settings.AlarmVolume, DefaultAlarm) != null;
        }

        private static void PlayTone()
        {
            NativeFunction.CallByName<uint>("PLAY_SOUND_FRONTEND", -1, "TIMER_STOP", "HUD_MINI_GAME_SOUNDSET", true);
        }
    }
}
