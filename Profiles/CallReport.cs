using System;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using Rage;

namespace LSFD.Profiles
{
    /// <summary>
    /// Follows the accepted call from acceptance to the end and turns it into a report for the profile: response time
    /// against a target, water used, people rescued and lost, and a score out of 100.
    /// </summary>
    internal class CallTracker
    {
        // The target is the time to get going plus the drive at a typical emergency speed through traffic.
        private const float TurnoutSeconds = 60f;
        private const float EmergencySpeed = 17f;
        private const float NonEmergencySpeed = 12f;
        private const int MinTargetSeconds = 120;

        private readonly uint acceptedAt = Game.GameTime;
        private readonly float waterAtStart = FireApparatus.TotalWaterUsed;
        private readonly int targetSeconds;
        private uint? arrivedAt;

        public Callout Callout { get; }
        public int Rescued { get; set; }
        public int Treated { get; set; }
        public int Resuscitated { get; set; }
        public int Transported { get; set; }
        public int Deceased { get; set; }
        public int Escalations { get; set; }

        public CallTracker(Callout callout)
        {
            Callout = callout;
            float distance = Game.LocalPlayer.Character.DistanceTo(callout.CalloutPosition);
            float speed = callout.Priority == CalloutPriority.Priority3 ? NonEmergencySpeed : EmergencySpeed;
            targetSeconds = Math.Max(MinTargetSeconds, (int)(TurnoutSeconds + distance / speed));
        }

        public void Process()
        {
            if (!arrivedAt.HasValue && Callout.HasArrived)
                arrivedAt = Game.GameTime;
        }

        public CallRecord Finish()
        {
            var record = new CallRecord
            {
                Date = DateTime.Now,
                Name = Callout.CalloutMessage ?? Callout.FriendlyName,
                Category = Callout.Category.ToString(),
                Location = World.GetStreetName(Callout.CalloutPosition),
                AlarmLevel = Callout.AlarmLevel,
                Completed = Callout.IsCompleted,
                Minutes = (int)((Game.GameTime - acceptedAt) / 60000),
                OnView = Callout.IsAmbient,
                ResponseSeconds = arrivedAt.HasValue && !Callout.IsAmbient ? (int)((arrivedAt.Value - acceptedAt) / 1000) : -1,
                TargetSeconds = targetSeconds,
                WaterUsed = (int)(FireApparatus.TotalWaterUsed - waterAtStart),
                Rescued = Rescued,
                Treated = Treated,
                Resuscitated = Resuscitated,
                Transported = Transported,
                Deceased = Deceased,
                Escalations = Escalations,
            };
            record.Score = Score(record, arrivedAt.HasValue);
            return record;
        }

        private static int Score(CallRecord record, bool arrived)
        {
            int score = 100;

            // Up to 30 for a slow response, the full 30 for never getting there.
            if (!arrived)
                score -= 30;
            else if (record.ResponseSeconds > record.TargetSeconds)
                score -= Math.Min(30, (int)((record.ResponseSeconds / (float)record.TargetSeconds - 1f) * 40f));

            score -= 25 * record.Deceased;
            score -= 10 * record.Escalations;
            if (!record.Completed)
                score -= 40;

            score += 5 * (record.Rescued + record.Resuscitated);
            return Math.Max(0, Math.Min(100, score));
        }
    }

    internal static class CallScore
    {
        public static string Grade(int score) =>
            score >= 90 ? "A" : score >= 75 ? "B" : score >= 60 ? "C" : score >= 40 ? "D" : "F";

        /// <summary>The score with a color: green for good, red for bad.</summary>
        public static string Display(int score) =>
            $"{(score >= 75 ? "~g~" : score >= 50 ? "~y~" : "~r~")}{score}~s~ ({Grade(score)})";

        /// <summary>Career points for a completed call: up to 10 for a perfect one.</summary>
        public static int Points(CallRecord record) => record.Completed && record.HasReport ? record.Score / 10 : 0;

        /// <summary>"4:12 (target 3:30)", "On view" or "-".</summary>
        public static string Response(CallRecord record)
        {
            if (record.OnView)
                return L.T("On view");
            if (record.ResponseSeconds < 0)
                return "-";
            string color = record.ResponseSeconds <= record.TargetSeconds ? "~g~" : "~o~";
            return L.F($"{color}{Time(record.ResponseSeconds)}~s~ (target {Time(record.TargetSeconds)})");
        }

        public static string Time(int seconds) => $"{seconds / 60}:{seconds % 60:00}";
    }
}
