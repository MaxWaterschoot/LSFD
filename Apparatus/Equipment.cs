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
                case EquipmentType.AttackHose: return "Attack hose";
                case EquipmentType.SupplyLine: return "Supply line";
                case EquipmentType.FireExtinguisher: return "Fire extinguisher";
                case EquipmentType.Axe: return "Axe";
                case EquipmentType.Crowbar: return "Halligan bar";
                case EquipmentType.Flashlight: return "Flashlight";
                case EquipmentType.Flare: return "Road flares";
                case EquipmentType.WindowPunch: return "Window punch";
                case EquipmentType.Cribbing: return "Cribbing";
                case EquipmentType.HydraulicSpreader: return "Hydraulic spreader";
                case EquipmentType.HydraulicCutter: return "Hydraulic cutter";
                case EquipmentType.AED: return "AED";
                case EquipmentType.SCBA: return "SCBA cylinders";
                case EquipmentType.ThermalCamera: return "Thermal imaging camera";
                case EquipmentType.PPVFan: return "PPV fan";
                case EquipmentType.GasDetector: return "Gas detector";
                case EquipmentType.Absorbent: return "Absorbent";
                case EquipmentType.TrafficCones: return "Traffic cones";
                default: return type.ToString();
            }
        }
    }
}
