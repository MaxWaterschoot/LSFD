using Rage;
using Rage.Native;
using System;
using static RAGENativeUI.Elements.UIMenuSliderPanel;
using System.Drawing;

namespace LSFD.Stations
{
    internal class FireStation
    {
        private Blip blip;
        private Marker checkpointMarker;
        private Vector3 truckSpawnPoint;

        public FireStation(Vector3 blipPosition, Vector3 markerPosition, Vector3 spawnPoint)
        {
            blip = new Blip(blipPosition);
            blip.Sprite = BlipSprite.FireTruck; // Set the blip sprite to a fire station icon

            
            // checkpointMarker = new Marker(markerPosition, Color.Green, MarkerType.VerticalCylinder); // You can customize the marker appearance

            truckSpawnPoint = spawnPoint;
        }

        public void OnDuty()
        {
            Game.LogTrivialDebug("Fire station has been loaded in.");
        }

        public void SpawnFireTruck()
        {
            Vehicle fireTruck = new Vehicle("firetruk", truckSpawnPoint);
            fireTruck.IsPersistent = true;

        }

        public void Cleanup()
        {
            blip.Delete();
           // checkpointMarker.Delete();
        }
    }
}
