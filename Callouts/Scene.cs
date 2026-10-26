using System.Collections.Generic;
using Rage;

namespace LSFD.Callouts
{
    /// <summary>Spawning helpers shared by the built-in callouts; tracks what it spawned so a callout can clean up in one call.</summary>
    internal class Scene
    {
        private static readonly string[] CarModels =
        {
            "asea", "premier", "emperor", "fugitive", "tailgater", "washington", "stanier", "primo", "ingot", "intruder", "minivan",
        };

        private static readonly string[] BikeModels = { "bati", "akuma", "pcj", "sanchez", "faggio", "hexer" };

        private readonly List<Entity> entities = new List<Entity>();

        public Vehicle SpawnCar(Vector3 position, float heading) => SpawnVehicle(CarModels[MathHelper.GetRandomInteger(CarModels.Length)], position, heading);

        public Vehicle SpawnBike(Vector3 position, float heading) => SpawnVehicle(BikeModels[MathHelper.GetRandomInteger(BikeModels.Length)], position, heading);

        public Vehicle SpawnVehicle(Model model, Vector3 position, float heading)
        {
            model.LoadAndWait();
            var vehicle = new Vehicle(model, position, heading) { IsPersistent = true };
            model.Dismiss();
            return Track(vehicle);
        }

        /// <summary>A shaken-up witness standing around, on the phone.</summary>
        public Ped SpawnBystander(Vector3 position)
        {
            var ped = new Ped(position) { IsPersistent = true, BlockPermanentEvents = true };
            ped.Tasks.PlayAnimation("amb@world_human_stand_mobile@male@text@base", "base", 1f, AnimationFlags.Loop);
            return Track(ped);
        }

        public T Track<T>(T entity) where T : Entity
        {
            entities.Add(entity);
            return entity;
        }

        public void Dismiss()
        {
            foreach (Entity entity in entities)
            {
                if (!entity.Exists())
                    continue;

                if (entity is Ped ped)
                    ped.BlockPermanentEvents = false;
                entity.Dismiss();
            }
            entities.Clear();
        }
    }
}
