using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Stations;
using LSFD.Utils;
using Rage;

namespace LSFD.Core
{
    /// <summary>
    /// Dispatch on the radio, put together from the voice fragments in Plugins/LSFD/Audio/Dispatch/: e.g. tones, "Engine
    /// One, respond. Residential Structure Fire. Second alarm. Priority one, code three. Caller reports occupants
    /// trapped inside." Fragments are found by name: "Head-on Collision" is Callouts/HEAD_ON_COLLISION.wav.
    /// </summary>
    internal class DispatchRadio
    {
        private const string Root = "Dispatch/";
        private const uint DuplicateTime = 4000;

        // What the advisory text says → the fragment after "Caller reports". The first match wins.
        private static readonly (string[] words, string file)[] Advisories =
        {
            (new[] { "not breathing", "no pulse" }, "NOT_BREATHING"),
            (new[] { "multiple patients", "mass casualty" }, "MULTIPLE_PATIENTS"),
            (new[] { "trapped inside" }, "OCCUPANTS_TRAPPED"),
            (new[] { "trapped" }, "PATIENT_TRAPPED"),
            (new[] { "reported out", "are outside", "occupants are outside" }, "OCCUPANTS_OUT"),
            (new[] { "gas" }, "ODOR_OF_GAS"),
            (new[] { "smoke showing", "smoke and flames", "fully involved" }, "SMOKE_SHOWING"),
        };

        private readonly DutyManager duty;
        private readonly FireStationManager stations;
        private readonly ApparatusManager apparatus;
        private string lastPhrase;
        private uint lastPhraseAt;

        public DispatchRadio(DutyManager duty, FireStationManager stations, ApparatusManager apparatus)
        {
            this.duty = duty;
            this.stations = stations;
            this.apparatus = apparatus;
        }

        /// <summary>A call is dispatched to the player.</summary>
        public void OnCalloutOffered(Callout callout)
        {
            // Nobody dispatches a call the player found themselves.
            if (callout.IsAmbient)
                return;

            Ped player = Game.LocalPlayer.Character;
            if (Settings.PagerAlert && player.Exists() && !player.IsInAnyVehicle(false))
                Sound.Play(Settings.AlarmVolume, "Alarms/PAGER_ALERT.wav");

            if (Settings.DispatchRadio)
                Sound.Radio(Settings.RadioVolume, Describe(callout), Root + "Tones/" + ToneFor(callout.Priority));
        }

        public void OnCalloutAccepted(Callout callout) => Say("Phrases/COPY");

        public void OnCalloutEnded(Callout callout)
        {
            if (callout.IsCompleted)
                Say("Phrases/CALL_COMPLETED");
            else if (duty.IsOnDuty && !Game.LocalPlayer.IsDead)
                Say("Phrases/CALL_CANCELLED");
        }

        public void OnCalloutDeclined(Callout callout) => Say("Phrases/CALL_REASSIGNED");

        public void OnDutyStateChanged(bool onDuty) => Say(onDuty ? "Phrases/ON_DUTY" : "Phrases/OFF_DUTY");

        public void OnBackupSent(ApparatusType type) =>
            Say(type == ApparatusType.Ambulance ? "Phrases/AMBULANCE_EN_ROUTE" : "Phrases/ADDITIONAL_UNITS_EN_ROUTE");

        /// <summary>"Be advised, second alarm. Additional units are en route."</summary>
        public void OnFireEscalated(FireIncident incident)
        {
            if (!Settings.DispatchRadio)
                return;

            var files = new List<string> { Root + "Phrases/BE_ADVISED.wav" };
            if (incident.AlarmLevel >= 1 && incident.AlarmLevel <= 3)
                files.Add(Root + $"Alarms/ALARM_{incident.AlarmLevel}.wav");
            files.Add(Root + "Phrases/ADDITIONAL_UNITS_EN_ROUTE.wav");
            Remember("Phrases/ADDITIONAL_UNITS_EN_ROUTE");
            Sound.Radio(Settings.RadioVolume, files, Root + "Tones/ATTENTION_TONE.wav");
        }

        private IEnumerable<string> Describe(Callout callout)
        {
            var files = new List<string>();

            ApparatusType unit = apparatus.Current?.Definition.Type ?? ApparatusType.Engine;
            files.Add(Root + $"Units/{unit.ToString().ToUpperInvariant()}.wav");
            int station = duty.HomeStation != null ? stations.Stations.ToList().IndexOf(duty.HomeStation) + 1 : 0;
            if (station >= 1 && station <= 10)
                files.Add(Root + $"Numbers/{station:00}.wav");
            files.Add(Root + "Phrases/RESPOND.wav");

            string name = FileName(L.English(callout.CalloutMessage ?? callout.FriendlyName));
            files.Add(Sound.Exists(Root + $"Callouts/{name}.wav")
                ? Root + $"Callouts/{name}.wav"
                : Root + $"Categories/{callout.Category.ToString().ToUpperInvariant()}.wav");

            if (callout.AlarmLevel >= 1 && callout.AlarmLevel <= 3 && callout.Category == CalloutCategory.Fire)
                files.Add(Root + $"Alarms/ALARM_{callout.AlarmLevel}.wav");

            files.Add(Root + $"Response/{callout.Priority.ToString().ToUpperInvariant()}.wav");
            files.Add(Root + (callout.Priority == CalloutPriority.Priority3 ? "Response/CODE_2.wav" : "Response/CODE_3.wav"));

            string advisory = AdvisoryFor(L.English(callout.CalloutAdvisory));
            if (advisory != null)
            {
                files.Add(Root + "Phrases/CALLER_REPORTS.wav");
                files.Add(Root + $"Advisories/{advisory}.wav");
            }
            return files;
        }

        private void Say(string phrase)
        {
            if (!Settings.DispatchRadio)
                return;
            // Two systems reporting the same thing (e.g. backup on an escalation) is said once.
            if (phrase == lastPhrase && Game.GameTime - lastPhraseAt < DuplicateTime)
                return;

            Remember(phrase);
            Sound.Radio(Settings.RadioVolume, new[] { Root + phrase + ".wav" });
        }

        private void Remember(string phrase)
        {
            lastPhrase = phrase;
            lastPhraseAt = Game.GameTime;
        }

        private static string ToneFor(CalloutPriority priority)
        {
            switch (priority)
            {
                case CalloutPriority.Priority1: return "ALERT_TONE_P1.wav";
                case CalloutPriority.Priority2: return "ALERT_TONE_P2.wav";
                default: return "ALERT_TONE_P3.wav";
            }
        }

        private static string AdvisoryFor(string advisory)
        {
            if (string.IsNullOrEmpty(advisory))
                return null;

            string text = Regex.Replace(advisory, "~.~", "").ToLowerInvariant();
            return Advisories.FirstOrDefault(a => a.words.Any(text.Contains)).file;
        }

        /// <summary>"Head-on Collision" → "HEAD_ON_COLLISION".</summary>
        public static string FileName(string text) => Regex.Replace(text ?? "", "[^A-Za-z0-9]+", "_").Trim('_').ToUpperInvariant();
    }
}
