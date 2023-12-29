using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rage;

namespace LSFD.Data
{
    internal class Data
    {
        private List<FireStationData> fireStations;

        public Data()
        {
            fireStations = new List<FireStationData>();
        }

        public void AddFireStation(FireStationData station)
        {
            fireStations.Add(station);
        }

        public List<FireStationData> GetAllFireStations()
        {
            return fireStations;
        }
    }
    internal class FireStationData
    {
        public string Name { get; set; }
        public Vector3 BlipPosition { get; set; }
        public Vector3 MarkerPosition { get; set; }
        public Vector3 TruckSpawnPoint { get; set; }

        public FireStationData(string name, Vector3 blipPos, Vector3 markerPos, Vector3 spawnPoint)
        {
            Name = name;
            BlipPosition = blipPos;
            MarkerPosition = markerPos;
            TruckSpawnPoint = spawnPoint;
        }
    }
}
