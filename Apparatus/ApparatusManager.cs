using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Core;
using LSFD.Stations;
using LSFD.UI;
using Rage;
using Rage.Native;

namespace LSFD.Apparatus
{
    /// <summary>Keeps track of the fire department vehicles the player uses and their tanks and equipment.</summary>
    internal class ApparatusManager : IInteractionProvider
    {
        private const float CompartmentRadius = 2.2f;
        private const float WaterCannonFlowRate = 45f;

        private readonly List<FireApparatus> apparatus = new List<FireApparatus>();

        private uint lastAmbientScan;

        public ApparatusRepository Repository { get; } = new ApparatusRepository();

        public IReadOnlyList<FireApparatus> All => apparatus;

        /// <summary>Raised when the player opens the compartments at the back of an apparatus.</summary>
        public event Action<FireApparatus> CompartmentMenuRequested;

        /// <summary>Raised when an apparatus is requested at a station.</summary>
        public event Action<FireApparatus> Spawned;

        /// <summary>The apparatus the player is currently driving or riding in, if any.</summary>
        public FireApparatus Current
        {
            get
            {
                Ped player = Game.LocalPlayer.Character;
                return player.IsInAnyVehicle(false) ? GetOrRegister(player.CurrentVehicle) : null;
            }
        }

        public FireApparatus Spawn(ApparatusDefinition definition, FireStation station)
        {
            Model model = definition.Model;
            if (!model.IsValid)
            {
                Notify.Warning(L.F($"Vehicle model ~y~{definition.Model}~s~ does not exist, check Apparatus.xml."));
                return null;
            }

            model.LoadAndWait();
            station.GetVehicleSpawn(out Vector3 position, out float heading);

            var vehicle = new Vehicle(model, position, heading) { IsPersistent = true };
            if (definition.Livery >= 0)
                NativeFunction.CallByName<uint>("SET_VEHICLE_LIVERY", vehicle, definition.Livery);
            model.Dismiss();

            var spawned = new FireApparatus(vehicle, definition);
            apparatus.Add(spawned);
            Spawned?.Invoke(spawned);

            Notify.Info(L.F($"Your ~y~{definition.Name}~s~ is ready in front of the station."));
            return spawned;
        }

        /// <summary>
        /// Sends an AI-driven unit with its crew from <paramref name="station"/> to <paramref name="destination"/>;
        /// <paramref name="announce"/> tells the player it is on its way to their scene.
        /// </summary>
        public FireApparatus SpawnResponding(ApparatusDefinition definition, FireStation station, Vector3 destination, bool announce = true)
        {
            Model model = definition.Model;
            if (!model.IsValid)
                return null;

            model.LoadAndWait();
            station.GetVehicleSpawn(out Vector3 position, out float heading);
            var vehicle = new Vehicle(model, position, heading) { IsPersistent = true, IsSirenOn = true };
            if (definition.Livery >= 0)
                NativeFunction.CallByName<uint>("SET_VEHICLE_LIVERY", vehicle, definition.Livery);
            model.Dismiss();

            var unit = new FireApparatus(vehicle, definition) { IsAiUnit = true, ResponseDestination = destination };
            apparatus.Add(unit);
            Spawned?.Invoke(unit);

            if (announce)
                Notify.Dispatch(L.T("~b~Unit responding"), L.F($"~y~{definition.Name}~s~ from {station.Name} is en route to the scene."));
            return unit;
        }

        public ApparatusDefinition FindDefinition(ApparatusType type)
        {
            return Repository.Spawnable.FirstOrDefault(d => d.Type == type);
        }

        /// <summary>Finds the apparatus for a vehicle, registering it when it is a known fire department model (e.g. an ambient fire truck).</summary>
        public FireApparatus GetOrRegister(Vehicle vehicle)
        {
            if (!vehicle.Exists())
                return null;

            FireApparatus existing = apparatus.FirstOrDefault(a => a.Vehicle == vehicle);
            if (existing != null)
                return existing;

            ApparatusDefinition definition = Repository.FindByModel(vehicle.Model);
            if (definition == null)
                return null;

            var registered = new FireApparatus(vehicle, definition);
            apparatus.Add(registered);
            return registered;
        }

        public FireApparatus GetNearest(Vector3 position, float maxDistance)
        {
            return apparatus
                .Where(a => a.IsValid && a.Vehicle.DistanceTo(position) <= maxDistance)
                .OrderBy(a => a.Vehicle.DistanceTo(position))
                .FirstOrDefault();
        }

        public Interaction GetInteraction()
        {
            Ped player = Game.LocalPlayer.Character;
            if (player.IsInAnyVehicle(false))
                return null;

            // Also pick up ambient fire trucks the player walks up to.
            if (Game.GameTime - lastAmbientScan > 1000)
            {
                lastAmbientScan = Game.GameTime;
                foreach (Vehicle vehicle in World.GetEntities(player.Position, 12f, GetEntitiesFlags.ConsiderAllVehicles).OfType<Vehicle>())
                    GetOrRegister(vehicle);
            }

            FireApparatus nearby = apparatus.FirstOrDefault(a => a.IsValid && a.RearPosition.DistanceTo(player.Position) <= CompartmentRadius);
            return nearby == null
                ? null
                : new Interaction(L.F($"open the ~y~{nearby.Definition.Name}~s~ compartments"), () => CompartmentMenuRequested?.Invoke(nearby));
        }

        public void TakeEquipment(FireApparatus source, EquipmentType type)
        {
            string weapon = type.GetWeaponName();
            if (weapon != null)
                Game.LocalPlayer.Character.Inventory.GiveNewWeapon(weapon, 1000, true);
            source.SetTaken(type, true);
        }

        public void StowEquipment(FireApparatus source, EquipmentType type)
        {
            string weapon = type.GetWeaponName();
            if (weapon != null)
                NativeFunction.CallByName<uint>("REMOVE_WEAPON_FROM_PED", Game.LocalPlayer.Character, Game.GetHashKey(weapon));
            source.SetTaken(type, false);
        }

        public void Process()
        {
            apparatus.RemoveAll(a => !a.Vehicle.Exists());

            foreach (var a in apparatus)
                a.Update(Game.FrameTime);

            ProcessWaterCannon();
        }

        // The vanilla fire truck has a roof-mounted water cannon, let it use the tank as well.
        private void ProcessWaterCannon()
        {
            FireApparatus current = Current;
            if (current == null || !current.Definition.HasPump)
                return;

            if (current.Water <= 0f && !current.IsSupplied)
            {
                Game.DisableControlAction(0, GameControl.VehicleAttack, true);
                Game.DisableControlAction(0, GameControl.VehicleAttack2, true);
                return;
            }

            if (Game.IsControlPressed(0, GameControl.VehicleAttack) || Game.IsControlPressed(0, GameControl.VehicleAttack2))
                current.DrawWater(WaterCannonFlowRate * Game.FrameTime);
        }

        public void Dismiss(FireApparatus target)
        {
            target.DisconnectHydrant();
            if (target.Vehicle.Exists())
                target.Vehicle.Dismiss();
            apparatus.Remove(target);
        }
    }
}
