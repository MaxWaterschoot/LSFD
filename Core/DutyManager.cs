using System;
using LSFD.API;
using LSFD.Profiles;
using LSFD.Safety;
using LSFD.Stations;
using LSFD.UI;
using Rage;

namespace LSFD.Core
{
    internal class DutyManager
    {
        private const string FirefighterModel = "s_m_y_fireman_01";
        private const int FadeMilliseconds = 600;

        private readonly BreathingApparatus scba;
        private readonly ProfileManager profiles;
        private Model? originalModel;

        // The player's own clothes, put back on when going off duty in the same model.
        private Outfit ownClothes;

        public DutyManager(BreathingApparatus scba, ProfileManager profiles)
        {
            this.scba = scba;
            this.profiles = profiles;
        }

        /// <summary>Changes into an outfit right away, e.g. from the locker room; null is the default firefighter.</summary>
        public void ChangeOutfit(Outfit outfit)
        {
            profiles.SelectOutfit(outfit);
            if (IsOnDuty)
                ChangeClothes(SuitUp);
        }

        private void SuitUp()
        {
            Outfit outfit = profiles.SelectedOutfit;
            if (outfit != null)
                outfit.ApplyToPlayer();
            else if (Settings.ChangeModelOnDuty)
                Game.LocalPlayer.Model = FirefighterModel;
        }

        public bool IsOnDuty { get; private set; }
        public FireStation HomeStation { get; private set; }

        public event Action<bool> DutyStateChanged;

        public void GoOnDuty(FireStation station)
        {
            if (IsOnDuty)
                return;

            // Like walking into the locker room: fade out, suit up, fade back in.
            ChangeClothes(() =>
            {
                originalModel = Game.LocalPlayer.Model;
                ownClothes = Outfit.Capture(Game.LocalPlayer.Character, "Own clothes");
                SuitUp();
                RestockPersonalGear();
            });

            IsOnDuty = true;
            HomeStation = station;
            Notify.Info($"~g~On duty~s~ at ~y~{station.Name}~s~. Open the tablet with ~b~{Settings.TabletKey}~s~.");

            DutyStateChanged?.Invoke(true);
            Functions.RaiseDutyStateChanged(true);
        }

        public void GoOffDuty()
        {
            if (!IsOnDuty)
                return;

            ChangeClothes(() =>
            {
                if (ownClothes != null)
                    ownClothes.ApplyToPlayer();
                else if (originalModel.HasValue)
                    Game.LocalPlayer.Model = originalModel.Value;
                originalModel = null;
                ownClothes = null;
            });

            IsOnDuty = false;
            HomeStation = null;
            Notify.Info("You are now ~b~off duty~s~.");

            DutyStateChanged?.Invoke(false);
            Functions.RaiseDutyStateChanged(false);
        }

        /// <summary>
        /// Personal protective gear every firefighter carries; the rest comes from the apparatus. Turnout gear is no
        /// body armor: it keeps the flames off (see HazardManager), the smoke and heat are still dangerous.
        /// </summary>
        public void RestockPersonalGear()
        {
            Ped player = Game.LocalPlayer.Character;
            player.Inventory.GiveFlashlight();
            player.Health = player.MaxHealth;
            scba.Refill();
        }

        private static void ChangeClothes(Action change)
        {
            Game.FadeScreenOut(FadeMilliseconds, true);
            change();
            GameFiber.Sleep(400);
            Game.FadeScreenIn(FadeMilliseconds, false);
        }
    }
}
