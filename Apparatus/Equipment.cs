using LSFD.Core;
namespace LSFD.Apparatus
{
    /// <summary>Equipment an apparatus can carry. Which items a vehicle has is set per apparatus in Apparatus.xml.</summary>
    public enum EquipmentType
    {
        /// <summary>Attack line with an adjustable nozzle, fed by the apparatus' water tank.</summary>
        AttackHose,
        /// <summary>Supply line to connect the apparatus to a fire hydrant and refill the tank.</summary>
        SupplyLine,
        FireExtinguisher,
        Axe,
        Crowbar,
        Flashlight,
        Flare,
        /// <summary>Spring-loaded glass breaker for side windows.</summary>
        WindowPunch,
        /// <summary>Wooden blocks to stabilize a crashed vehicle before cutting.</summary>
        Cribbing,
        /// <summary>Hydraulic spreader ("jaws of life") to force jammed doors open.</summary>
        HydraulicSpreader,
        /// <summary>Hydraulic cutter / rescue saw to cut doors off.</summary>
        HydraulicCutter,
        /// <summary>Automated external defibrillator; greatly improves the odds of CPR.</summary>
        AED,
        /// <summary>Spare SCBA cylinders to swap an empty one for a full one.</summary>
        SCBA,
        /// <summary>Thermal imaging camera to find victims and hotspots in the smoke.</summary>
        ThermalCamera,
        /// <summary>Positive pressure ventilation fan to blow the smoke out of a building.</summary>
        PPVFan,
        /// <summary>Multi-gas detector for CO alarms and gas leaks.</summary>
        GasDetector,
        /// <summary>Absorbent granules for fuel and oil spills.</summary>
        Absorbent,
        /// <summary>Traffic cones to cordon off a scene.</summary>
        TrafficCones,
    }

    public static class EquipmentTypeExtensions
    {
        /// <summary>The weapon given to the player for this equipment, or null for equipment that isn't a weapon.</summary>
        public static string GetWeaponName(this EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.FireExtinguisher: return "WEAPON_FIREEXTINGUISHER";
                case EquipmentType.Axe: return "WEAPON_HATCHET";
                case EquipmentType.Crowbar: return "WEAPON_CROWBAR";
                case EquipmentType.Flashlight: return "WEAPON_FLASHLIGHT";
                case EquipmentType.Flare: return "WEAPON_FLARE";
                default: return null;
            }
        }

        /// <summary>Tools the player carries in their hands instead of as a weapon. Defined in Tools.xml.</summary>
        public static bool IsHandTool(this EquipmentType type)
        {
            return type == EquipmentType.WindowPunch || type == EquipmentType.Cribbing
                || type == EquipmentType.HydraulicSpreader || type == EquipmentType.HydraulicCutter
                || type == EquipmentType.AED || type == EquipmentType.GasDetector || type == EquipmentType.Absorbent
                || type == EquipmentType.TrafficCones;
        }

        public static string ToDisplayName(this EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.AttackHose: return L.T("Attack hose");
                case EquipmentType.SupplyLine: return L.T("Supply line");
                case EquipmentType.FireExtinguisher: return L.T("Fire extinguisher");
                case EquipmentType.Axe: return L.T("Axe");
                case EquipmentType.Crowbar: return L.T("Halligan bar");
                case EquipmentType.Flashlight: return L.T("Flashlight");
                case EquipmentType.Flare: return L.T("Road flares");
                case EquipmentType.WindowPunch: return L.T("Window punch");
                case EquipmentType.Cribbing: return L.T("Cribbing");
                case EquipmentType.HydraulicSpreader: return L.T("Hydraulic spreader");
                case EquipmentType.HydraulicCutter: return L.T("Hydraulic cutter");
                case EquipmentType.AED: return L.T("AED");
                case EquipmentType.SCBA: return L.T("SCBA cylinders");
                case EquipmentType.ThermalCamera: return L.T("Thermal imaging camera");
                case EquipmentType.PPVFan: return L.T("PPV fan");
                case EquipmentType.GasDetector: return L.T("Gas detector");
                case EquipmentType.Absorbent: return L.T("Absorbent");
                case EquipmentType.TrafficCones: return L.T("Traffic cones");
                default: return type.ToString();
            }
        }
    }
}
