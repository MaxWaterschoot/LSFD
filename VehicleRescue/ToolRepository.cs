using System.Collections.Generic;
using System.Linq;
using LSFD.Apparatus;
using LSFD.Utils;

namespace LSFD.VehicleRescue
{
    /// <summary>Loads Plugins/LSFD/Data/Tools.xml.</summary>
    internal class ToolRepository
    {
        private const string FileName = "Tools.xml";

        private readonly List<ToolDefinition> tools;

        public ToolRepository()
        {
            tools = XmlStore.Load(FileName, () => new ToolList { Tools = CreateDefaults() }).Tools;
        }

        public ToolDefinition Get(EquipmentType type)
        {
            return tools.FirstOrDefault(t => t.Type == type) ?? new ToolDefinition { Type = type };
        }

        private static List<ToolDefinition> CreateDefaults()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition { Type = EquipmentType.WindowPunch, Prop = "prop_tool_hammer", OffsetX = 0.1f, Pitch = -90f, UseSeconds = 1.5f },
                new ToolDefinition { Type = EquipmentType.Cribbing, UseSeconds = 5f, AnimName = "fixing_a_ped" },
                new ToolDefinition { Type = EquipmentType.HydraulicSpreader, UseSeconds = 6f },
                new ToolDefinition { Type = EquipmentType.HydraulicCutter, Prop = "prop_tool_consaw", OffsetX = 0.1f, OffsetY = 0.05f, UseSeconds = 8f },
                new ToolDefinition
                {
                    Type = EquipmentType.AED, UseSeconds = 5f,
                    AnimDictionary = "amb@medic@standing@tendtodead@idle_a", AnimName = "idle_a",
                },
            };
        }
    }
}
