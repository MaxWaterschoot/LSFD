using System;

namespace LSFD.Water
{
    public enum NozzlePattern
    {
        /// <summary>Long reach and high impact, for knocking down a fire from a distance.</summary>
        Straight,
        /// <summary>Short, wide cone that cools a large area and protects the firefighter.</summary>
        Fog,
    }

    /// <summary>The adjustable settings of the attack hose nozzle.</summary>
    internal class Nozzle
    {
        private const float MinPressure = 0.2f;
        private const float MaxPressure = 1f;
        private const float PressureStep = 0.1f;

        public NozzlePattern Pattern { get; private set; } = NozzlePattern.Straight;

        /// <summary>Pressure from 0.2 to 1.</summary>
        public float Pressure { get; private set; } = 0.7f;

        /// <summary>Pressure the pump of the apparatus delivers (0.2 to 1), set every frame by the hose.</summary>
        public float PumpPressure { get; set; } = 1f;

        /// <summary>What actually comes out: the nozzle setting limited by the pump.</summary>
        public float EffectivePressure => Pressure * PumpPressure;

        public bool IsStraight => Pattern == NozzlePattern.Straight;

        /// <summary>Speed the water leaves the nozzle with, in m/s.</summary>
        public float LaunchSpeed => IsStraight ? 8f + 20f * EffectivePressure : 4f + 6f * EffectivePressure;

        /// <summary>Half the opening angle of the spray cone, in degrees.</summary>
        public float ConeAngle => IsStraight ? 4f : 35f;

        public float ImpactRadius => IsStraight ? 1.5f : 3.5f;

        /// <summary>How fast the stream cools a fire; a fire goes out at 1.</summary>
        public float CoolingPerSecond => IsStraight ? 0.6f + 1.4f * EffectivePressure : 1.2f;

        /// <summary>Share of the pump rate the nozzle flows at full pressure.</summary>
        public float FlowFactor => IsStraight ? 1f : 0.7f;

        public string EffectName => IsStraight ? "water_cannon_jet" : "water_cannon_spray";
        public float EffectScale => IsStraight ? 0.4f + 0.4f * EffectivePressure : 0.8f + 0.6f * EffectivePressure;

        public void TogglePattern()
        {
            Pattern = IsStraight ? NozzlePattern.Fog : NozzlePattern.Straight;
        }

        public void ChangePressure(int steps)
        {
            float pressure = (float)Math.Round(Pressure + steps * PressureStep, 2);
            Pressure = Math.Max(MinPressure, Math.Min(MaxPressure, pressure));
        }
    }
}
