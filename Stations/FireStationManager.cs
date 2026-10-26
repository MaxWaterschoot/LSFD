using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Core;
using LSFD.Utils;
using Rage;

namespace LSFD.Stations
{
    internal class FireStationManager : IInteractionProvider
    {
        private readonly StationRepository repository = new StationRepository();
        private readonly List<FireStation> stations = new List<FireStation>();
        private FireStation stationPlayerIsIn;

        /// <summary>Raised when the player interacts with a station marker.</summary>
        public event Action<FireStation> StationMenuRequested;

        public IReadOnlyList<FireStation> Stations => stations;

        /// <summary>The loaded stations sorted per area, then by name.</summary>
        public IEnumerable<FireStation> StationsByArea => stations.OrderBy(s => s.Data.Area).ThenBy(s => s.Name);

        public IEnumerable<FireStationData> AllStationData => repository.GetFireStationsByArea();

        public void LoadFireStations()
        {
            foreach (var stationData in repository.GetAllFireStations().Where(s => s.Enabled))
                stations.Add(new FireStation(stationData));

            Game.LogTrivial($"Loaded {stations.Count} fire stations.");
        }

        public void ReloadFireStations()
        {
            CleanupFireStations();
            repository.Load();
            LoadFireStations();
        }

        public Interaction GetInteraction()
        {
            FireStation station = stationPlayerIsIn;
            return station == null
                ? null
                : new Interaction($"open the ~r~{station.Name}~s~ menu", () => StationMenuRequested?.Invoke(station));
        }

        /// <summary>Draws the markers and opens the station menu when the player steps into one, like any GTA mission marker.</summary>
        public void Process(bool canOpenMenu)
        {
            Vector3 camera = GameplayCamera.Position;
            foreach (FireStation station in stations)
                station.DrawMarker(camera);

            FireStation inside = stations.FirstOrDefault(s => s.IsPlayerInside());
            if (inside != null && inside != stationPlayerIsIn && canOpenMenu)
                StationMenuRequested?.Invoke(inside);
            stationPlayerIsIn = inside;
        }

        public FireStation GetNearestStation(Vector3 position)
        {
            return stations.OrderBy(s => s.Position.DistanceTo(position)).FirstOrDefault();
        }

        public FireStationData FindNearestStationData(Vector3 position)
        {
            return repository.GetAllFireStations().OrderBy(s => s.Position.DistanceTo(position)).FirstOrDefault();
        }

        public void SetEntrance(FireStationData station, Vector3 position, float heading)
        {
            station.Entrance = new XmlPosition(position, heading);
            SaveAndReload();
        }

        public void SetVehicleSpawn(FireStationData station, Vector3 position, float heading)
        {
            station.VehicleSpawn = new XmlPosition(position, heading);
            SaveAndReload();
        }

        private void SaveAndReload()
        {
            repository.Save();
            CleanupFireStations();
            LoadFireStations();
        }

        public void CleanupFireStations()
        {
            foreach (var station in stations)
                station.Cleanup();
            stations.Clear();
        }
    }
}
