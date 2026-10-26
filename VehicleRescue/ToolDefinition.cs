using System.Collections.Generic;
using System.Xml.Serialization;
using LSFD.Apparatus;

namespace LSFD.VehicleRescue
{
    [XmlRoot("Tools")]
    public class ToolList
    {
        [XmlElement("Tool")]
        public List<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();
    }

    /// <summary>How a hand tool looks and is used. Props and animations can point to add-on content.</summary>
    public class ToolDefinition
    {
        [XmlAttribute] public EquipmentType Type { get; set; }

        /// <summary>Model of the prop held in the hand; leave empty for no prop.</summary>
        [XmlAttribute] public string Prop { get; set; } = "";

        /// <summary>Ped bone the prop is attached to (57005 = right hand, 18905 = left hand).</summary>
        [XmlAttribute] public int Bone { get; set; } = 57005;

        [XmlAttribute] public float OffsetX { get; set; }
        [XmlAttribute] public float OffsetY { get; set; }
        [XmlAttribute] public float OffsetZ { get; set; }
        [XmlAttribute] public float Pitch { get; set; }
        [XmlAttribute] public float Roll { get; set; }
        [XmlAttribute] public float Yaw { get; set; }

        /// <summary>Animation played while using the tool.</summary>
        [XmlAttribute] public string AnimDictionary { get; set; } = "mini@repair";
        [XmlAttribute] public string AnimName { get; set; } = "fixing_a_player";

        /// <summary>Seconds the interact key has to be held to finish the job.</summary>
        [XmlAttribute] public float UseSeconds { get; set; } = 4f;
    }
}
