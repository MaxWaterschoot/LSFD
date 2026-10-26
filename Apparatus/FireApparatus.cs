using System;
using System.Collections.Generic;
using LSFD.Core;
using LSFD.UI;
using Rage;

namespace LSFD.Apparatus
{
    /// <summary>A fire department vehicle in the world, with its own water tank and equipment.</summary>
    internal class FireApparatus
    {
        private readonly HashSet<EquipmentType> takenEquipment = new HashSet<EquipmentType>();

        public Vehicle Vehicle { get; }
        public ApparatusDefinition Definition { get; }

        /// <summary>Liters of water in the tank.</summary>
        public float Water { get; private set; }

        /// <summary>Driven by AI and responding on its own (mutual aid / backup), instead of being the player's vehicle.</summary>
        public bool IsAiUnit { get; set; }

        /// <summary>Out on a call of another station (see LivingWorld.OtherUnits); it doesn't help at the player's scene.</summary>
        public bool IsCommitted { get; set; }

        /// <summary>Where an AI unit is responding to, or null once it has arrived.</summary>
        public Vector3? ResponseDestination { get; set; }

        /// <summary>Liters of foam concentrate left.</summary>
        public float Foam { get; private set; }

        /// <summary>Attack lines the AI crew has stretched from this apparatus.</summary>
        public int CrewLinesOut { get; set; }

        /// <summary>Lines still in the hose bed, counting the player's hose.</summary>
        public int FreeAttackLines => Definition.AttackLines - CrewLinesOut - (IsTaken(EquipmentType.AttackHose) ? 1 : 0);

        /// <summary>Whether the pump feeds the attack hose; set from the tablet.</summary>
        public bool PumpEngaged { get; set; } = true;

        /// <summary>Pump discharge pressure from 0.2 to 1; limits what the nozzle can deliver.</summary>
        public float PumpPressure { get; set; } = 1f;

        /// <summary>Position of the hydrant this apparatus is connected to, if any.</summary>
        public Vector3? ConnectedHydrant { get; private set; }

        public bool IsValid => Vehicle.Exists() && !Vehicle.IsDead;
        public bool IsSupplied => ConnectedHydrant.HasValue;
        public float WaterFraction => Definition.HasPump ? Water / Definition.WaterCapacity : 0f;

        /// <summary>The pump panel and compartments at the back of the vehicle.</summary>
        public Vector3 RearPosition => Vehicle.GetOffsetPositionFront(-Vehicle.Model.Dimensions.Y / 2f - 0.6f);

        public FireApparatus(Vehicle vehicle, ApparatusDefinition definition)
        {
            Vehicle = vehicle;
            Definition = definition;
            Water = definition.WaterCapacity;
            Foam = definition.FoamCapacity;
        }

        /// <summary>Takes up to <paramref name="liters"/> of foam and returns how much was delivered.</summary>
        public float DrawFoam(float liters)
        {
            float delivered = Math.Min(liters, Foam);
            Foam -= delivered;
            return delivered;
        }

        public void RefillFoam()
        {
            Foam = Definition.FoamCapacity;
        }

        public bool IsTaken(EquipmentType type) => takenEquipment.Contains(type);

        public void SetTaken(EquipmentType type, bool taken)
        {
            if (taken)
                takenEquipment.Add(type);
            else
                takenEquipment.Remove(type);
        }

        /// <summary>Takes up to <paramref name="liters"/> from the tank and returns how much was delivered. Unlimited while on a hydrant.</summary>
        public float DrawWater(float liters)
        {
            if (IsSupplied)
                return liters;

            float delivered = Math.Min(liters, Water);
            Water -= delivered;
            return delivered;
        }

        public void ConnectHydrant(Vector3 hydrant)
        {
            ConnectedHydrant = hydrant;
        }

        public void DisconnectHydrant()
        {
            ConnectedHydrant = null;
        }

        public void Update(float deltaSeconds)
        {
            if (!IsSupplied)
                return;

            Water = Math.Min(Definition.WaterCapacity, Water + Settings.HydrantFlowRate * deltaSeconds);

            if (Vehicle.DistanceTo(ConnectedHydrant.Value) > Definition.SupplyLineLength)
            {
                DisconnectHydrant();
                Notify.Warning($"The supply line of your ~y~{Definition.Name}~s~ snapped off the hydrant.");
            }
        }
    }
}
