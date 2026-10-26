using System;
using System.Linq;
using LSFD.API;
using LSFD.Buildings;
using LSFD.Core;
using LSFD.Stations;
using LSFD.UI;
using LSFD.Utils;
using Rage;
using Rage.Attributes;

[assembly: Plugin("LSFD", Description = "Los Santos Fire Department: go on duty, respond to fires and save lives.", Author = "DeEchteBelg", PrefersSingleInstance = true, ExitPoint = "LSFD.EntryPoint.OnUnload")]

namespace LSFD
{
    public static class EntryPoint
    {
        public static void Main()
        {
            Game.LogTrivial($"LSFD {typeof(EntryPoint).Assembly.GetName().Version} is loading.");

            var missing = ModFolder.FindMissingFolders();
            if (missing.Count > 0)
            {
                Game.LogTrivial($"LSFD is not installed correctly, missing: {string.Join(", ", missing)}. " +
                    "Copy the Plugins folder from the download into your GTA V folder.");
                Notify.Warning("Not loaded: the ~r~Plugins/LSFD~s~ folder is missing or incomplete. Reinstall LSFD from the download.");
                return;
            }

            Lsfd.Initialize();
            Game.AddConsoleCommands();

            Notify.Info($"Loaded. Visit a ~r~fire station~s~ to go on duty. Tablet: ~b~{Settings.TabletKey}");
            Game.LogTrivial("LSFD has been loaded.");

            while (true)
            {
                GameFiber.Yield();

                try
                {
                    Lsfd.Tick();
                }
                catch (Exception e)
                {
                    Game.LogTrivial("LSFD error: " + e);
                    Lsfd.Callouts.EndCurrent(null);
                }
            }
        }

        public static void OnUnload(bool isTerminating)
        {
            Game.LogTrivial("LSFD is unloading.");

            // When the game is shutting down the world is gone, so there is nothing to clean up.
            if (!isTerminating)
                Lsfd.Shutdown();
        }

        [ConsoleCommand("Prints the player's position and heading.")]
        private static void LSFD_PrintPosition()
        {
            Ped player = Game.LocalPlayer.Character;
            Vector3 p = player.Position;
            Game.LogTrivial($"new Vector3({p.X:0.0}f, {p.Y:0.0}f, {p.Z:0.0}f), heading {player.Heading:0.0}");
        }

        [ConsoleCommand("Offers a random call, or the call with the given name (you must be on duty).")]
        private static void LSFD_Dispatch(string name = "")
        {
            if (!Functions.IsPlayerOnDuty())
            {
                Game.LogTrivial("Go on duty first.");
                return;
            }

            bool dispatched = string.IsNullOrEmpty(name) ? Lsfd.Callouts.DispatchRandom() : Functions.StartCallout(name);
            if (!dispatched)
                Game.LogTrivial($"Could not dispatch a call. Registered calls: {string.Join(", ", Functions.GetRegisteredCallouts())}");
        }

        [ConsoleCommand("Puts an incident on the road ahead of you, as if you came across it (you must be on duty and free).")]
        private static void LSFD_AmbientIncident()
        {
            if (!Functions.IsPlayerOnDuty() || !Lsfd.Ambient.TryStart())
                Game.LogTrivial("No incident: go on duty, finish your call first, and make sure there is a road ahead with calls marked AllowAmbient/Ambient.");
        }

        [ConsoleCommand("Sends other AI units to a random call elsewhere in the city (you must be on duty).")]
        private static void LSFD_OtherUnitsIncident()
        {
            if (!Functions.IsPlayerOnDuty() || !Lsfd.OtherUnits.StartRandom())
                Game.LogTrivial("No incident: go on duty first, or no station had a free unit.");
        }

        [ConsoleCommand("Reloads the XML calls in Plugins/LSFD/Callouts/ after you edited them.")]
        private static void LSFD_ReloadXmlCallouts()
        {
            int count = Lsfd.Registry.ReloadXmlCallouts();
            Game.LogTrivial($"Loaded {count} XML call(s). See above for any that were skipped.");
        }

        [ConsoleCommand("Lists all registered calls.")]
        private static void LSFD_ListCallouts()
        {
            Game.LogTrivial("Registered calls: " + string.Join(", ", Functions.GetRegisteredCallouts()));
        }

        [ConsoleCommand("Lists all fire stations, sorted per area.")]
        private static void LSFD_ListStations()
        {
            foreach (var area in Lsfd.Stations.AllStationData.GroupBy(s => s.Area))
            {
                Game.LogTrivial($"--- {area.Key.ToDisplayName()} ---");
                foreach (FireStationData s in area)
                    Game.LogTrivial($"{s.Name} [{s.Type}]{(s.Enabled ? "" : " (disabled)")} - {s.Address} @ {s.Position}");
            }
        }

        [ConsoleCommand("Moves the entrance marker of the nearest fire station to where you are standing.")]
        private static void LSFD_SetStationEntrance()
        {
            Ped player = Game.LocalPlayer.Character;
            FireStationData station = Lsfd.Stations.FindNearestStationData(player.Position);
            Lsfd.Stations.SetEntrance(station, player.Position, player.Heading);
            Game.LogTrivial($"Entrance of {station.Name} set to {player.Position}, saved to Stations.xml.");
        }

        [ConsoleCommand("Sets where apparatus spawn for the nearest fire station, using your current vehicle (or your position).")]
        private static void LSFD_SetStationVehicleSpawn()
        {
            Ped player = Game.LocalPlayer.Character;
            Entity reference = player.IsInAnyVehicle(false) ? (Entity)player.CurrentVehicle : player;
            FireStationData station = Lsfd.Stations.FindNearestStationData(reference.Position);
            Lsfd.Stations.SetVehicleSpawn(station, reference.Position, reference.Heading);
            Game.LogTrivial($"Vehicle spawn of {station.Name} set to {reference.Position}, saved to Stations.xml.");
        }

        [ConsoleCommand("Adds your position as a building fire location to FireLocations.xml (stand inside the building). " +
            "Kind: Store, Commercial, Office, Apartment, House, Industrial or Public.")]
        private static void LSFD_AddFireLocation(string name, string kind = "House")
        {
            if (!Enum.TryParse(kind, true, out FireLocationKind parsedKind))
            {
                Game.LogTrivial($"Unknown kind '{kind}'. Use one of: {string.Join(", ", Enum.GetNames(typeof(FireLocationKind)))}.");
                return;
            }

            var location = Lsfd.FireLocations.Add(name, parsedKind, Game.LocalPlayer.Character.Position);
            Game.LogTrivial($"Added fire location '{location.Name}'{(location.RequiresOpenInteriors ? " (needs Enable All Interiors)" : "")}.");
        }

        [ConsoleCommand("Starts a growing fire 10 m in front of you to test the fire system, SCBA and thermal camera. Indoor fills the interior with smoke.")]
        private static void LSFD_StartFire(bool indoor = false, int alarmLevel = 1)
        {
            Ped player = Game.LocalPlayer.Character;
            Vector3 position = indoor ? player.Position : player.GetOffsetPositionFront(10f);
            Lsfd.Fire.Start(position, new FireIncidentOptions { IsIndoor = indoor, InitialPoints = 1 + alarmLevel }, alarmLevel, null);
            Game.LogTrivial($"Started a {(indoor ? "indoor" : "outdoor")} fire at {position}. Remove all with LSFD_RemoveFires.");
        }

        [ConsoleCommand("Removes every fire started by LSFD.")]
        private static void LSFD_RemoveFires()
        {
            Lsfd.Fire.RemoveAll();
        }

        [ConsoleCommand("Reloads Plugins/LSFD/Data/Stations.xml.")]
        private static void LSFD_ReloadStations()
        {
            Lsfd.Stations.ReloadFireStations();
        }
    }
}
