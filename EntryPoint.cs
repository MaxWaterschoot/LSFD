using LSFD.MainClasses;
using Newtonsoft.Json;
using Rage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using static LSFD.MainClasses.Checkpoint;

[assembly: Rage.Attributes.Plugin("LSFD", Description = "This is a LSFD plugin. It's also my first plugin.", Author = "DeEchteBelg")]


namespace LSFD
{

    
    public class EntryPoint
    {
        public static Player LocalPlayer = null;
        public static List<Station> stations; 
        static void Main() 
        {
            Game.LogTrivialDebug("LSFD is loading");

            LocalPlayer = Rage.Game.LocalPlayer;
            GenerateCheckpoints();
        }
        private static void OnUnload(bool isTerminating)
        {
            Game.LogTrivialDebug("LSFD is unloading");
        }

        static void GenerateCheckpoints() {

            string json = File.ReadAllText("StationsData.json");

            stations = JsonConvert.DeserializeObject<List<Station>>(json);

            foreach (var station in stations)
            {
                new Checkpoint(station.Position, Color.DarkRed, 5, 2);
                Console.WriteLine("StationName: {0}, Type: {1}, Position: {2}", station.StationName, station.Type, station.Position);
            }
        }
    }
}
