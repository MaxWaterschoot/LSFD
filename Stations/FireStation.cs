using System.Drawing;
using LSFD.Utils;
using Rage;
using Rage.Native;

namespace LSFD.Stations
{
    internal class FireStation
    {
        private const float MarkerRadius = 1.2f;
        private static readonly Color MarkerColor = Color.FromArgb(230, 60, 40);

        private readonly Blip blip;
        private readonly Marker marker;

        public FireStationData Data { get; }
        public string Name => Data.Name;
        public Vector3 Position => Data.Position;

        public FireStation(FireStationData data)
        {
            Data = data;

            blip = new Blip(Position);
            blip.Sprite = BlipSprite.FireTruck;
            blip.Color = Color.Red;
            blip.Name = Name;
            NativeFunction.CallByName<uint>("SET_BLIP_AS_SHORT_RANGE", (uint)blip.Handle, true);

            marker = new Marker(Position, MarkerRadius, MarkerColor);
        }

        public bool IsPlayerInside()
        {
            Ped player = Game.LocalPlayer.Character;
            return player.Exists() && !player.IsInAnyVehicle(false) && marker.Contains(player.Position);
        }

        public void DrawMarker(Vector3 cameraPosition)
        {
            marker.Draw(cameraPosition);
        }

        /// <summary>Where apparatus for this station spawn, from Stations.xml or else the nearest street.</summary>
        public void GetVehicleSpawn(out Vector3 position, out float heading)
        {
            if (Data.VehicleSpawn != null)
            {
                position = Data.VehicleSpawn.ToVector3();
                heading = Data.VehicleSpawn.Heading;
            }
            else
            {
                position = World.GetNextPositionOnStreet(Position);
                heading = 0f;
            }
        }

        public void Cleanup()
        {
            if (blip.Exists())
                blip.Delete();
        }
    }
}
