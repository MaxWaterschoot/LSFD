using System;
using System.IO;
using System.Linq;
using LSFD.API;
using LSFD.Core;
using LSFD.UI;
using LSFD.Utils;
using Rage;

namespace LSFD.Profiles
{
    /// <summary>Loads and saves the player's profile and the outfits, and keeps the statistics up to date.</summary>
    internal class ProfileManager
    {
        private const string OutfitsFile = "Outfits.xml";
        private const int HistorySize = 50;
        private const uint AutoSaveInterval = 120000;

        private readonly string fileName;
        private uint lastSave = Game.GameTime;
        private uint dutyClockAt;
        private Callout trackedCall;
        private uint trackedCallStartedAt;

        public Profile Profile { get; }
        public OutfitList Outfits { get; }

        public string Rank => Ranks.For(Profile.Points);

        public ProfileManager()
        {
            string name = Sanitize(Game.LocalPlayer.Name);
            fileName = name + ".xml";
            Profile = XmlStore.Load(fileName, () => new Profile { Name = name }, ModFolder.Profiles);
            Outfits = XmlStore.Load(OutfitsFile, () => new OutfitList(), ModFolder.Profiles);
        }

        /// <summary>The outfit the player chose, or null for the default firefighter.</summary>
        public Outfit SelectedOutfit => Outfits.Outfits.FirstOrDefault(o => o.Name == Profile.Outfit);

        public void SelectOutfit(Outfit outfit)
        {
            Profile.Outfit = outfit?.Name ?? "";
            Save();
        }

        /// <summary>Saves what the player wears now (e.g. an EUP outfit) as a new outfit and selects it.</summary>
        public Outfit SaveCurrentOutfit()
        {
            int number = Outfits.Outfits.Count + 1;
            while (Outfits.Outfits.Any(o => o.Name == $"Outfit {number}"))
                number++;

            Outfit outfit = Outfit.Capture(Game.LocalPlayer.Character, $"Outfit {number}");
            Outfits.Outfits.Add(outfit);
            XmlStore.Save(OutfitsFile, Outfits, ModFolder.Profiles);
            SelectOutfit(outfit);
            return outfit;
        }

        public void Process(bool onDuty)
        {
            if (onDuty)
            {
                if (dutyClockAt == 0)
                    dutyClockAt = Game.GameTime;
                Profile.HoursOnDuty += (Game.GameTime - dutyClockAt) / 3600000.0;
                dutyClockAt = Game.GameTime;
            }
            else
            {
                dutyClockAt = 0;
            }

            if (Game.GameTime - lastSave > AutoSaveInterval)
                Save();
        }

        public void OnCalloutAccepted(Callout callout)
        {
            trackedCall = callout;
            trackedCallStartedAt = Game.GameTime;
            Profile.CallsAttended++;
        }

        public void OnCalloutEnded(Callout callout)
        {
            if (callout.IsCompleted)
                Award(() => Profile.CallsCompleted++);

            int minutes = trackedCall == callout ? (int)((Game.GameTime - trackedCallStartedAt) / 60000) : 0;
            Profile.History.Insert(0, new CallRecord
            {
                Date = DateTime.Now,
                Name = callout.CalloutMessage ?? callout.FriendlyName,
                Category = callout.Category.ToString(),
                Location = World.GetStreetName(callout.CalloutPosition),
                AlarmLevel = callout.AlarmLevel,
                Completed = callout.IsCompleted,
                Minutes = minutes,
            });
            if (Profile.History.Count > HistorySize)
                Profile.History.RemoveRange(HistorySize, Profile.History.Count - HistorySize);

            trackedCall = null;
            Save();
        }

        public void OnFireOut() => Award(() => Profile.FiresExtinguished++);
        public void OnPersonRescued() => Award(() => Profile.PeopleRescued++);
        public void OnPatientTreated() => Award(() => Profile.PatientsTreated++);
        public void OnPatientResuscitated() => Award(() => Profile.PatientsResuscitated++);
        public void OnPatientTransported() => Award(() => Profile.PatientsTransported++);

        public void Save()
        {
            lastSave = Game.GameTime;
            try
            {
                XmlStore.Save(fileName, Profile, ModFolder.Profiles);
            }
            catch (Exception e)
            {
                Game.LogTrivial($"LSFD: could not save the profile: {e.Message}");
            }
        }

        // Tells the player when the change took them to the next rank.
        private void Award(Action change)
        {
            string before = Rank;
            change();
            string after = Rank;
            if (after != before)
            {
                Notify.Info($"~g~Promoted~s~ to ~y~{after}~s~!");
                Lsfd.Hud.ShowPromotion(after);
            }
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Default";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
