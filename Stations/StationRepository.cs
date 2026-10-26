using System.Collections.Generic;
using System.Linq;
using LSFD.Utils;
using Rage;

namespace LSFD.Stations
{
    /// <summary>Loads and saves Plugins/LSFD/Data/Stations.xml.</summary>
    internal class StationRepository
    {
        private const string FileName = "Stations.xml";

        private List<FireStationData> fireStations;

        public StationRepository()
        {
            Load();
        }

        public List<FireStationData> GetAllFireStations()
        {
            return fireStations;
        }

        public IEnumerable<FireStationData> GetFireStationsByArea()
        {
            return fireStations.OrderBy(s => s.Area).ThenBy(s => s.Name);
        }

        public void Load()
        {
            fireStations = XmlStore.Load(FileName, () => new FireStationList { Stations = CreateDefaultStations() }).Stations;
        }

        public void Save()
        {
            XmlStore.Save(FileName, new FireStationList { Stations = fireStations });
        }

        // The seven fire stations of vanilla GTA V. Positions are approximate, use the
        // LSFD_SetStationEntrance / LSFD_SetStationVehicleSpawn console commands to correct them in-game.
        private static List<FireStationData> CreateDefaultStations()
        {
            return new List<FireStationData>
            {
                new FireStationData("Davis Fire Station", "Macdonald Street, Davis", StationArea.LosSantos, StationType.Municipal, new Vector3(199.8f, -1643.4f, 29.8f)),
                new FireStationData("El Burro Heights Fire Station", "LSFD Station 7, Capital Boulevard", StationArea.LosSantos, StationType.Municipal, new Vector3(1193.4f, -1473.7f, 34.9f)),
                new FireStationData("Rockford Hills Fire Station", "South Boulevard Del Perro, Rockford Hills", StationArea.LosSantos, StationType.Municipal, new Vector3(-635.1f, -121.9f, 39.0f)),
                new FireStationData("LSIA Fire Station", "Los Santos International Airport", StationArea.LosSantos, StationType.Airport, new Vector3(-1068.0f, -2379.0f, 14.0f)),
                new FireStationData("Sandy Shores Fire Station", "Alhambra Drive, Sandy Shores", StationArea.BlaineCounty, StationType.Municipal, new Vector3(1691.5f, 3585.8f, 35.6f)),
                new FireStationData("Paleto Bay Fire Station", "Blaine County Fire Station 1, Paleto Boulevard", StationArea.BlaineCounty, StationType.Municipal, new Vector3(-379.5f, 6118.3f, 31.8f)),
                // Inside the military base, the army attacks anyone without a hangar there.
                new FireStationData("Fort Zancudo Fire Station", "Fort Zancudo", StationArea.BlaineCounty, StationType.Military, new Vector3(-2096.0f, 2830.0f, 32.8f)) { Enabled = false },
            };
        }
    }
}
