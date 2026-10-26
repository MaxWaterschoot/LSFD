using System.Collections.Generic;
using System.Xml.Serialization;

namespace LSFD.Apparatus
{
    public enum ApparatusType
    {
        Engine,
        Ladder,
        Rescue,
        Brush,
        AirportCrash,
        Ambulance,
        Lifeguard,
        Command,
    }

    [XmlRoot("Apparatus")]
    public class ApparatusList
    {
        [XmlElement("Vehicle")]
        public List<ApparatusDefinition> Vehicles { get; set; } = new List<ApparatusDefinition>();
    }

    /// <summary>A kind of fire department vehicle and what it carries.</summary>
    public class ApparatusDefinition
    {
        [XmlAttribute] public string Name { get; set; }
        [XmlAttribute] public string Model { get; set; }
        [XmlAttribute] public ApparatusType Type { get; set; }

        /// <summary>Water tank size in liters, 0 for vehicles without a pump.</summary>
        [XmlAttribute] public float WaterCapacity { get; set; }

        /// <summary>Liters per second the pump delivers to the attack hose at full pressure.</summary>
        [XmlAttribute] public float PumpRate { get; set; } = 25f;

        [XmlAttribute] public float HoseLength { get; set; } = 30f;

        /// <summary>Attack lines that can be stretched at the same time (the player's and the crew's).</summary>
        [XmlAttribute] public int AttackLines { get; set; } = 2;

        /// <summary>Foam concentrate in liters, 0 for none. Switch the nozzle to foam for fuel and vehicle fires.</summary>
        [XmlAttribute] public float FoamCapacity { get; set; }

        /// <summary>Whether the vehicle has a roof-mounted water cannon (deck gun) that cools LSFD fires.</summary>
        [XmlAttribute] public bool HasDeckGun { get; set; }
        [XmlAttribute] public float SupplyLineLength { get; set; } = 30f;

        /// <summary>AI firefighters riding along; a random number between these is spawned with the vehicle.</summary>
        [XmlAttribute] public int MinCrew { get; set; }
        [XmlAttribute] public int MaxCrew { get; set; }

        /// <summary>Ped model of the crew, e.g. paramedics on an ambulance. Empty means firefighters.</summary>
        [XmlAttribute] public string CrewModel { get; set; }

        /// <summary>Livery index to apply, -1 keeps the default.</summary>
        [XmlAttribute] public int Livery { get; set; } = -1;

        /// <summary>Whether the vehicle can be requested from the station menu.</summary>
        [XmlAttribute] public bool Spawnable { get; set; } = true;

        [XmlArray("Equipment"), XmlArrayItem("Item")]
        public List<EquipmentType> Equipment { get; set; } = new List<EquipmentType>();

        [XmlIgnore]
        public bool HasPump => WaterCapacity > 0f;

        public bool Carries(EquipmentType type)
        {
            return Equipment.Contains(type);
        }
    }
}
