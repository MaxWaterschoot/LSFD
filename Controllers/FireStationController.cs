using LSFD.Data;
using LSFD.Stations;
using Rage;

namespace LSFD.Controllers
{
    internal class FireStationController
    {
        private Data.Data data;

        public FireStationController()
        {
            data = new Data.Data();
        }

        public void LoadFireStations()
        {

            var fireStations = data.GetAllFireStations();

            foreach (var stationData in fireStations)
            {
                var fireStation = new FireStation(stationData.BlipPosition, stationData.MarkerPosition, stationData.TruckSpawnPoint);

           

                if (fireStation != null)
                {
                    fireStation.OnDuty();
                }
            }
        }

        public void CleanupFireStations()
        {

            var fireStations = data.GetAllFireStations();

            foreach (var stationData in fireStations)
            {
                var fireStation = new FireStation(stationData.BlipPosition, stationData.MarkerPosition, stationData.TruckSpawnPoint);

                if (fireStation != null)
                {
                    fireStation.Cleanup();
                }
            }
        }
    }
}
