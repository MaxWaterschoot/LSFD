using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Utils;
using Rage;

namespace LSFD.Apparatus
{
    /// <summary>Loads Plugins/LSFD/Apparatus.xml. Add-on vehicles can be added there.</summary>
    internal class ApparatusRepository
    {
        private const string FileName = "Apparatus.xml";

        public List<ApparatusDefinition> Definitions { get; private set; }

        public IEnumerable<ApparatusDefinition> Spawnable => Definitions.Where(d => d.Spawnable);

        public ApparatusRepository()
        {
            Load();
        }

        public void Load()
        {
            Definitions = XmlStore.Load(FileName, () => new ApparatusList { Vehicles = CreateDefaults() }).Vehicles;
            Game.LogTrivial($"Loaded {Definitions.Count} apparatus definitions.");
        }

        public ApparatusDefinition FindByModel(Model model)
        {
            return Definitions.FirstOrDefault(d => new Model(d.Model).Hash == model.Hash);
        }

        private static List<ApparatusDefinition> CreateDefaults()
        {
            return new List<ApparatusDefinition>
            {
                new ApparatusDefinition
                {
                    Name = "Engine",
                    Model = "firetruk",
                    Type = ApparatusType.Engine,
                    WaterCapacity = 3000f,
                    PumpRate = 30f,
                    HoseLength = 30f,
                    SupplyLineLength = 35f,
                    MinCrew = 2,
                    MaxCrew = 4,
                    Equipment =
                    {
                        EquipmentType.AttackHose, EquipmentType.SupplyLine, EquipmentType.FireExtinguisher,
                        EquipmentType.Axe, EquipmentType.Crowbar, EquipmentType.Flashlight,
                        EquipmentType.WindowPunch, EquipmentType.Cribbing, EquipmentType.HydraulicSpreader, EquipmentType.HydraulicCutter,
                        EquipmentType.AED,
                    },
                },
                new ApparatusDefinition
                {
                    Name = "Ambulance",
                    Model = "ambulance",
                    Type = ApparatusType.Ambulance,
                    MinCrew = 1,
                    MaxCrew = 1,
                    Equipment = { EquipmentType.FireExtinguisher, EquipmentType.Flashlight, EquipmentType.AED },
                },
                new ApparatusDefinition
                {
                    Name = "Lifeguard Unit",
                    Model = "lguard",
                    Type = ApparatusType.Lifeguard,
                    Equipment = { EquipmentType.FireExtinguisher, EquipmentType.Flashlight, EquipmentType.Flare },
                },
            };
        }
    }
}
