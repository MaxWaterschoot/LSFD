using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using LSFD.Core;

namespace LSFD.Profiles
{
    /// <summary>A player's career: statistics, call history and the outfit they wear on duty. Saved in Plugins/LSFD/Profiles/.</summary>
    [XmlRoot("Profile")]
    public class Profile
    {
        [XmlAttribute] public string Name { get; set; }

        public int CallsAttended { get; set; }
        public int CallsCompleted { get; set; }
        public int FiresExtinguished { get; set; }
        public int PeopleRescued { get; set; }
        public int PatientsTreated { get; set; }
        public int PatientsResuscitated { get; set; }
        public int PatientsTransported { get; set; }
        public double HoursOnDuty { get; set; }

        /// <summary>Points earned with the score of call reports.</summary>
        public int ReportPoints { get; set; }

        /// <summary>Name of the outfit from Outfits.xml worn on duty; empty for the default firefighter.</summary>
        public string Outfit { get; set; } = "";

        [XmlArray("History"), XmlArrayItem("Call")]
        public List<CallRecord> History { get; set; } = new List<CallRecord>();

        /// <summary>Career points, which decide the rank.</summary>
        [XmlIgnore]
        public int Points => CallsCompleted * 10 + FiresExtinguished * 10 + PeopleRescued * 15
            + PatientsResuscitated * 20 + PatientsTreated * 5 + PatientsTransported * 5 + ReportPoints;
    }

    public class CallRecord
    {
        [XmlAttribute] public DateTime Date { get; set; }
        [XmlAttribute] public string Name { get; set; }
        [XmlAttribute] public string Category { get; set; }
        [XmlAttribute] public string Location { get; set; }
        [XmlAttribute] public int AlarmLevel { get; set; }
        [XmlAttribute] public bool Completed { get; set; }
        [XmlAttribute] public int Minutes { get; set; }

        // The call report; calls from before reports existed have no score.
        [XmlAttribute] public bool OnView { get; set; }
        /// <summary>Seconds from accepting to arriving; -1 when on view or never on scene.</summary>
        [XmlAttribute] public int ResponseSeconds { get; set; } = -1;
        [XmlAttribute] public int TargetSeconds { get; set; }
        /// <summary>Liters of water pumped by every apparatus during the call.</summary>
        [XmlAttribute] public int WaterUsed { get; set; }
        [XmlAttribute] public int Rescued { get; set; }
        [XmlAttribute] public int Treated { get; set; }
        [XmlAttribute] public int Resuscitated { get; set; }
        [XmlAttribute] public int Transported { get; set; }
        [XmlAttribute] public int Deceased { get; set; }
        /// <summary>How often the fire went up an alarm during the call.</summary>
        [XmlAttribute] public int Escalations { get; set; }
        /// <summary>0 to 100; -1 for calls without a report.</summary>
        [XmlAttribute] public int Score { get; set; } = -1;

        [XmlIgnore] public bool HasReport => Score >= 0;
    }

    internal static class Ranks
    {
        private static readonly (int points, string name)[] All =
        {
            (0, L.T("Probationary Firefighter")),
            (150, L.T("Firefighter")),
            (500, L.T("Engineer")),
            (1200, L.T("Lieutenant")),
            (2500, L.T("Captain")),
            (5000, L.T("Battalion Chief")),
            (10000, L.T("Fire Chief")),
        };

        public static string For(int points)
        {
            string rank = All[0].name;
            foreach (var (needed, name) in All)
            {
                if (points >= needed)
                    rank = name;
            }
            return rank;
        }

        /// <summary>Points needed for the next rank, or null at the top.</summary>
        public static int? NextAt(int points)
        {
            foreach (var (needed, _) in All)
            {
                if (needed > points)
                    return needed;
            }
            return null;
        }
    }
}
