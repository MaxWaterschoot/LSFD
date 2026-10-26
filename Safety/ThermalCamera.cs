using System.Drawing;
using System.Linq;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Firefighting;
using LSFD.Medical;
using LSFD.UI;
using Rage;
using Rage.Native;

namespace LSFD.Safety
{
    /// <summary>
    /// The thermal imaging camera (TIC): taken from an apparatus, it shows heat through the smoke and marks victims,
    /// flames and the hotspots that still have to be overhauled.
    /// </summary>
    internal class ThermalCamera
    {
        private const float MarkerRange = 35f;
        private const int VictimMarkerType = 0;
        private const int SpotMarkerType = 28;

        private static readonly Color VictimColor = Color.FromArgb(220, 255, 255, 255);
        private static readonly Color FlameColor = Color.FromArgb(200, 255, 245, 220);
        private static readonly Color HotspotColor = Color.FromArgb(200, 255, 140, 0);

        private readonly FireSystem fire;
        private readonly PatientManager patients;

        public ThermalCamera(FireSystem fire, PatientManager patients)
        {
            this.fire = fire;
            this.patients = patients;
        }

        /// <summary>The apparatus the camera was taken from, or null when the player doesn't carry one.</summary>
        public FireApparatus Source { get; private set; }

        public bool IsCarried => Source != null;
        public bool IsActive { get; private set; }

        /// <summary>Victims and hotspots seen by the camera this frame.</summary>
        public int VisibleVictims { get; private set; }
        public int VisibleHotspots { get; private set; }

        public void Take(FireApparatus source)
        {
            Stow();
            Source = source;
            source.SetTaken(EquipmentType.ThermalCamera, true);
            Notify.Info($"Thermal imaging camera taken. Press ~b~{Settings.ThermalCameraKey}~s~ to turn it on.");
        }

        public void Stow()
        {
            SetActive(false);
            Source?.SetTaken(EquipmentType.ThermalCamera, false);
            Source = null;
        }

        public void Process(bool onDuty)
        {
            if (!IsCarried)
                return;

            if (!onDuty)
            {
                Stow();
                return;
            }

            Ped player = Game.LocalPlayer.Character;
            bool usable = player.Exists() && !player.IsDead && !player.IsInAnyVehicle(false);

            if (usable && Game.IsKeyDown(Settings.ThermalCameraKey))
                SetActive(!IsActive);
            else if (!usable)
                SetActive(false);

            if (IsActive)
                DrawMarkers(player.Position);
        }

        private void SetActive(bool active)
        {
            if (active == IsActive)
                return;

            IsActive = active;
            NativeFunction.CallByName<uint>("SET_SEETHROUGH", active);
            VisibleVictims = 0;
            VisibleHotspots = 0;
        }

        private void DrawMarkers(Vector3 position)
        {
            var victims = patients.All
                .Where(p => p.Ped.Exists() && (p.NeedsRescue || p.NeedsCare) && p.Ped.DistanceTo(position) <= MarkerRange)
                .ToList();
            foreach (Patient victim in victims)
                DrawMarker(VictimMarkerType, victim.Ped.Position + new Vector3(0f, 0f, 1.2f), 0.5f, VictimColor, true);

            int hotspots = 0;
            foreach (var spot in fire.GetHotSpots(position, MarkerRange))
            {
                if (spot.burning)
                {
                    DrawMarker(SpotMarkerType, spot.position + new Vector3(0f, 0f, 0.3f), 0.3f + 0.4f * spot.heat, FlameColor, false);
                }
                else
                {
                    DrawMarker(SpotMarkerType, spot.position + new Vector3(0f, 0f, 0.2f), 0.35f, HotspotColor, false);
                    hotspots++;
                }
            }

            VisibleVictims = victims.Count;
            VisibleHotspots = hotspots;
        }

        private static void DrawMarker(int type, Vector3 position, float scale, Color color, bool bob)
        {
            NativeFunction.CallByName<uint>("DRAW_MARKER", type,
                position.X, position.Y, position.Z,
                0f, 0f, 0f,
                0f, 0f, 0f,
                scale, scale, scale,
                (int)color.R, (int)color.G, (int)color.B, (int)color.A,
                bob, true, 2, false, 0, 0, false);
        }
    }
}
