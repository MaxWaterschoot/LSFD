using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Rage;

namespace LSFD.Stations
{
    public enum StationArea
    {
        LosSantos,
        LosSantosCounty,
        BlaineCounty,
    }

    public enum StationType
    {
        Municipal,
        Airport,
        Military,
    }

    public static class StationAreaExtensions
    {
        public static string ToDisplayName(this StationArea area)
        {
            switch (area)
            {
                case StationArea.LosSantos: return "Los Santos";
                case StationArea.LosSantosCounty: return "Los Santos County";
                case StationArea.BlaineCounty: return "Blaine County";
                default: return area.ToString();
            }
        }
    }

    [XmlRoot("FireStations")]
    public class FireStationList
    {
        [XmlElement("Station")]
        public List<FireStationData> Stations { get; set; } = new List<FireStationData>();
    }

    public class FireStationData
    {
        [XmlAttribute] public string Name { get; set; }
        [XmlAttribute] public string Address { get; set; }
        [XmlAttribute] public StationArea Area { get; set; }
        [XmlAttribute] public StationType Type { get; set; }
        [XmlAttribute] public bool Enabled { get; set; } = true;

        public XmlPosition Entrance { get; set; }

        /// <summary>Where apparatus spawn. When left out, the nearest street to the entrance is used.</summary>
        public XmlPosition VehicleSpawn { get; set; }

        [XmlIgnore]
        public Vector3 Position => Entrance.ToVector3();

        public FireStationData()
        {
        }

        public FireStationData(string name, string address, StationArea area, StationType type, Vector3 entrance)
        {
            Name = name;
            Address = address;
            Area = area;
            Type = type;
            Entrance = new XmlPosition(entrance, 0f);
        }
    }

    public class XmlPosition
    {
        [XmlAttribute] public float X { get; set; }
        [XmlAttribute] public float Y { get; set; }
        [XmlAttribute] public float Z { get; set; }
        [XmlAttribute] public float Heading { get; set; }

        public XmlPosition()
        {
        }

        public XmlPosition(Vector3 position, float heading)
        {
            X = (float)Math.Round(position.X, 2);
            Y = (float)Math.Round(position.Y, 2);
            Z = (float)Math.Round(position.Z, 2);
            Heading = (float)Math.Round(heading, 1);
        }

        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }
    }
}
