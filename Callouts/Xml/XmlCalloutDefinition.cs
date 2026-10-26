using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using LSFD.API;
using LSFD.Apparatus;
using Rage;

namespace LSFD.Callouts.Xml
{
    /// <summary>
    /// A file with data-driven callouts in Plugins/LSFD/Callouts/, for people who don't want to program.
    /// See docs/XML-CALLOUTS.md for every element and attribute.
    /// </summary>
    [XmlRoot("Callouts")]
    public class XmlCalloutFile
    {
        [XmlElement("Callout")]
        public List<XmlCalloutDefinition> Callouts { get; set; } = new List<XmlCalloutDefinition>();

        internal static XmlCalloutFile Load(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                    return (XmlCalloutFile)new XmlSerializer(typeof(XmlCalloutFile)).Deserialize(stream);
            }
            catch (Exception e)
            {
                // XmlSerializer puts the line and column in the outer message and the cause in the inner one.
                Game.LogTrivial($"LSFD: could not read {path}: {e.Message} {e.InnerException?.Message}");
                return null;
            }
        }
    }

    public class XmlCalloutDefinition
    {
        /// <summary>Unique name, also used with the LSFD_StartCallout console command.</summary>
        [XmlAttribute] public string Name { get; set; }
        [XmlAttribute] public CalloutProbability Probability { get; set; } = CalloutProbability.Medium;
        [XmlAttribute] public CalloutCategory Category { get; set; } = CalloutCategory.Fire;
        [XmlAttribute] public CalloutPriority Priority { get; set; } = CalloutPriority.Priority2;

        /// <summary>Title of the dispatch notification; the name when left out.</summary>
        public string Message { get; set; }
        public string Advisory { get; set; }

        /// <summary>Help text shown on arrival.</summary>
        public string ArrivalHelp { get; set; }

        /// <summary>Notification when everything is done; a standard one when left out.</summary>
        public string CompletedMessage { get; set; }

        public XmlLocation Location { get; set; } = new XmlLocation();
        public XmlAlarm Alarm { get; set; }
        public XmlFire Fire { get; set; }

        [XmlElement("Patient")] public List<XmlPatient> Patients { get; set; } = new List<XmlPatient>();
        [XmlElement("Vehicle")] public List<XmlVehicle> Vehicles { get; set; } = new List<XmlVehicle>();
        [XmlElement("Bystander")] public List<XmlBystander> Bystanders { get; set; } = new List<XmlBystander>();
        [XmlElement("ToxicAtmosphere")] public List<XmlToxicAtmosphere> ToxicAtmospheres { get; set; } = new List<XmlToxicAtmosphere>();
        [XmlElement("Task")] public List<XmlTask> Tasks { get; set; } = new List<XmlTask>();
        [XmlElement("Backup")] public List<XmlBackup> Backup { get; set; } = new List<XmlBackup>();

        /// <summary>Why the definition can't be used, or null.</summary>
        internal string Validate()
        {
            bool hasWork = Fire != null || Patients.Any(p => p.Count > 0) || Tasks.Count > 0
                || Vehicles.Any(v => v.OnFire || v.TrapDriver);
            if (!hasWork)
                return "there is nothing to do: add a Fire, Patient, Task or a Vehicle with OnFire or TrapDriver";

            foreach (XmlVehicle vehicle in Vehicles.Where(v => !v.IsRandomModel))
            {
                if (!new Model(vehicle.Model).IsValid)
                    return $"vehicle model '{vehicle.Model}' does not exist";
            }

            XmlTask badTask = Tasks.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Equipment) && t.RequiredEquipment == null);
            if (badTask != null)
                return $"task equipment '{badTask.Equipment}' does not exist";

            if (Location.Positions.Count == 0 && Location.MinDistance > Location.MaxDistance)
                return "Location MinDistance is bigger than MaxDistance";
            return null;
        }
    }

    /// <summary>Fixed positions (one is picked at random), or a random street around the player.</summary>
    public class XmlLocation
    {
        [XmlAttribute] public float MinDistance { get; set; } = 300f;
        [XmlAttribute] public float MaxDistance { get; set; } = 900f;
        [XmlAttribute] public float AreaRadius { get; set; } = 30f;

        [XmlElement("Position")] public List<XmlPoint> Positions { get; set; } = new List<XmlPoint>();
    }

    public class XmlPoint
    {
        [XmlAttribute] public float X { get; set; }
        [XmlAttribute] public float Y { get; set; }
        [XmlAttribute] public float Z { get; set; }
        [XmlAttribute] public float Heading { get; set; }

        internal Vector3 Position => new Vector3(X, Y, Z);
    }

    /// <summary>A fixed alarm level, or a random one when one of the chances is set.</summary>
    public class XmlAlarm
    {
        [XmlAttribute] public int Level { get; set; } = 1;
        [XmlAttribute] public int SecondAlarmChance { get; set; }
        [XmlAttribute] public int ThirdAlarmChance { get; set; }
    }

    /// <summary>A growing <see cref="FireIncident"/> at the location, started when the call is accepted.</summary>
    public class XmlFire
    {
        [XmlAttribute] public int Points { get; set; } = 2;
        [XmlAttribute] public float Intensity { get; set; } = 0.5f;
        [XmlAttribute] public float SpreadRadius { get; set; } = 10f;
        [XmlAttribute] public float SpreadSeconds { get; set; } = 50f;
        [XmlAttribute] public int PointsPerAlarm { get; set; } = 6;
        [XmlAttribute] public int MaxAlarm { get; set; } = 3;
        [XmlAttribute] public FireClass Class { get; set; } = FireClass.Ordinary;
        [XmlAttribute] public bool Gas { get; set; }

        /// <summary>Meters from the location to the electrical panel, for electrical fires; -1 for none.</summary>
        [XmlAttribute] public float PowerPanelDistance { get; set; } = -1f;
    }

    public class XmlPatient
    {
        [XmlAttribute] public PatientCondition Condition { get; set; } = PatientCondition.Injured;
        [XmlAttribute] public InjuryType Injury { get; set; } = InjuryType.Trauma;
        [XmlAttribute] public int Count { get; set; } = 1;

        /// <summary>Walking wounded: minor injuries, green at triage, doesn't get worse.</summary>
        [XmlAttribute] public bool Minor { get; set; }

        /// <summary>How far from the location they are, at most, in meters.</summary>
        [XmlAttribute] public float Distance { get; set; } = 6f;
    }

    public class XmlVehicle
    {
        /// <summary>A model name, or "random" for a random car.</summary>
        [XmlAttribute] public string Model { get; set; } = "random";

        /// <summary>0 for intact, up to 1 for a heavy crash.</summary>
        [XmlAttribute] public float Crash { get; set; }
        [XmlAttribute] public bool OnFire { get; set; }

        /// <summary>Traps a driver that has to be extricated (and then treated).</summary>
        [XmlAttribute] public bool TrapDriver { get; set; }

        /// <summary>How far from the location it is, at most, in meters (snapped to the road).</summary>
        [XmlAttribute] public float Distance { get; set; }

        internal bool IsRandomModel => string.IsNullOrWhiteSpace(Model) || Model.Equals("random", StringComparison.OrdinalIgnoreCase);
    }

    public class XmlBystander
    {
        [XmlAttribute] public int Count { get; set; } = 1;
        [XmlAttribute] public float Distance { get; set; } = 12f;
    }

    /// <summary>An invisible toxic atmosphere (like CO) at the location; hurts without the SCBA face piece.</summary>
    public class XmlToxicAtmosphere
    {
        [XmlAttribute] public float Radius { get; set; } = 12f;
        [XmlAttribute] public float Strength { get; set; } = 0.8f;
    }

    /// <summary>A <see cref="SceneAction"/>: something the player does by holding the interact key at a spot.</summary>
    public class XmlTask
    {
        /// <summary>Finishes "Hold E to ...", e.g. "close the ~y~gas valve".</summary>
        [XmlAttribute] public string Text { get; set; } = "check the ~y~scene";
        [XmlAttribute] public float Seconds { get; set; } = 4f;

        /// <summary>Equipment the player must carry, e.g. GasDetector; empty for none.</summary>
        [XmlAttribute] public string Equipment { get; set; }

        [XmlAttribute] public float OffsetX { get; set; }
        [XmlAttribute] public float OffsetY { get; set; }
        [XmlAttribute] public float OffsetZ { get; set; }

        /// <summary>Shown when the task is done.</summary>
        [XmlAttribute] public string DoneMessage { get; set; }

        /// <summary>Removes the toxic atmospheres once done, e.g. for "ventilate the house".</summary>
        [XmlAttribute] public bool ClearsToxicAtmosphere { get; set; }

        internal EquipmentType? RequiredEquipment =>
            Enum.TryParse(Equipment, true, out EquipmentType type) ? type : (EquipmentType?)null;
    }

    /// <summary>Extra AI units sent when the call is accepted (on top of the ones LSFD sends itself).</summary>
    public class XmlBackup
    {
        [XmlAttribute] public ApparatusType Type { get; set; } = ApparatusType.Engine;
        [XmlAttribute] public int Count { get; set; } = 1;
    }
}
