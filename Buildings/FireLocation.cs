using System.Collections.Generic;
using System.Xml.Serialization;
using Rage;

namespace LSFD.Buildings
{
    public enum FireLocationKind
    {
        Store,
        Commercial,
        Office,
        Apartment,
        House,
        Industrial,
        Public,
    }

    public class XmlVector
    {
        [XmlAttribute] public float X { get; set; }
        [XmlAttribute] public float Y { get; set; }
        [XmlAttribute] public float Z { get; set; }

        public Vector3 ToVector3() => new Vector3(X, Y, Z);
    }

    [XmlRoot("FireLocations")]
    public class FireLocationList
    {
        [XmlElement("Location")]
        public List<FireLocation> Locations { get; set; } = new List<FireLocation>();
    }

    /// <summary>A place inside a building where structure fires can break out.</summary>
    public class FireLocation
    {
        [XmlAttribute] public string Name { get; set; }
        [XmlAttribute] public FireLocationKind Kind { get; set; }
        [XmlAttribute] public float X { get; set; }
        [XmlAttribute] public float Y { get; set; }
        [XmlAttribute] public float Z { get; set; }

        /// <summary>True for interiors that are locked in the vanilla game and need the Enable All Interiors mod.</summary>
        [XmlAttribute] public bool RequiresOpenInteriors { get; set; }

        /// <summary>Optional street position where units stage and the GPS leads to; the nearest street is used when left out.</summary>
        [XmlElement] public XmlVector Staging { get; set; }

        [XmlIgnore]
        public Vector3 Position => new Vector3(X, Y, Z);

        public Vector3 GetStagingPosition()
        {
            return Staging?.ToVector3() ?? World.GetNextPositionOnStreet(Position);
        }

        public FireLocation()
        {
        }

        public FireLocation(string name, FireLocationKind kind, float x, float y, float z, bool requiresOpenInteriors = false)
        {
            Name = name;
            Kind = kind;
            X = x;
            Y = y;
            Z = z;
            RequiresOpenInteriors = requiresOpenInteriors;
        }
    }
}
