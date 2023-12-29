using LSFD.MainClasses;
using Rage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSFD.Stations
{

    internal class FireStation
    {
        private Blip blip { set; get; }
        private Checkpoint checkpoint { set; get; }
        private Vector3 TruckSpawn { set; get; }

        public FireStation() { 
            blip = new Blip();

        }
    }
   
}
