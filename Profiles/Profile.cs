using System;
using System.Collections.Generic;
using System.Xml.Serialization;

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

        /// <summary>Name of the outfit from Outfits.xml worn on duty; empty for the default firefighter.</summary>
        public string Outfit { get; set; } = "";

        [XmlArray("History"), XmlArrayItem("Call")]
        public List<CallRecord> History { get; set; } = new List<CallRecord>();

        /// <summary>Career points, which decide the rank.</summary>
        [XmlIgnore]
        public int Points => CallsCompleted * 10 + FiresExtinguished * 10 + PeopleRescued * 15
            + PatientsResuscitated * 20 + PatientsTreated * 5 + PatientsTransported * 5;
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
    }

    internal static class Ranks
    {
        private static readonly (int points, string name)[] All =
        {
            (0, "Probationary Firefighter"),
            (150, "Firefighter"),
            (500, "Engineer"),
            (1200, "Lieutenant"),
            (2500, "Captain"),
            (5000, "Battalion Chief"),
            (10000, "Fire Chief"),
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
