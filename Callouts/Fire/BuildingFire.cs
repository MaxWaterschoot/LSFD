using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Buildings;
using Rage;

namespace LSFD.Callouts
{
    /// <summary>
    /// A fire inside a building from FireLocations.xml. The alarm level decides how big the fire is and how many
    /// occupants are trapped; they have to be carried out and treated before the call is over.
    /// </summary>
    internal abstract class BuildingFire : Callout
    {
        private readonly List<int> fires = new List<int>();
        private readonly List<Ped> victims = new List<Ped>();
        private readonly Scene scene = new Scene();

        private BuildingInfo building;
        private int trappedCount;
        private uint arrivedAt;

        protected abstract FireLocationKind[] Kinds { get; }
        protected abstract string Title { get; }

        /// <summary>Most occupants that can be trapped at a 1st alarm; more at higher alarms.</summary>
        protected virtual int BaseOccupants => 1;

        protected virtual bool IsGasFire => false;

        private float SceneRadius => 8f + 4f * AlarmLevel;

        public override bool OnBeforeCalloutDisplayed()
        {
            building = Interiors.GetRandomBuilding(300f, 3000f, Kinds);
            if (building == null)
                return false;

            AlarmLevel = RollAlarmLevel();
            trappedCount = MathHelper.GetRandomInteger(0, BaseOccupants + AlarmLevel);

            CalloutPosition = building.Staging;
            CalloutMessage = Title;
            CalloutAreaRadius = 30f + 10f * AlarmLevel;
            CalloutAdvisory = $"~y~{building.Name}~s~. " + (trappedCount > 0
                ? $"Caller reports ~r~{trappedCount} occupant{(trappedCount == 1 ? "" : "s")} trapped~s~ inside."
                : "Occupants are reported out of the building.");
            return true;
        }

        protected override void OnArrival()
        {
            arrivedAt = Game.GameTime;
            Interiors.PrepareInterior(building.Position);

            int fireCount = 3 + 3 * AlarmLevel;
            for (int i = 0; i < fireCount; i++)
                fires.Add(Fires.StartFire(building.Position.Around2D(0.5f, 2f + 2f * AlarmLevel), 15 + 5 * AlarmLevel, IsGasFire));

            for (int i = 0; i < trappedCount; i++)
            {
                var condition = MathHelper.GetRandomInteger(4) == 0 ? PatientCondition.Injured : PatientCondition.Unconscious;
                victims.Add(Patients.SpawnTrappedInBuilding(building.Position.Around2D(1f, 4f), building.Staging, condition));
            }

            int bystanders = MathHelper.GetRandomInteger(1, 4);
            for (int i = 0; i < bystanders; i++)
                scene.SpawnBystander(building.Staging.Around2D(3f, 8f));

            Game.DisplayHelp(trappedCount > 0
                ? "Search the building and ~r~carry the victims out~s~, then extinguish the ~r~fire~s~. Your crew can help from the tablet."
                : "Stretch a ~b~hose line~s~ into the building and extinguish the ~r~fire~s~.");
        }

        public override void Process()
        {
            if (!HasArrived || Game.GameTime - arrivedAt < 5000)
                return;

            if (Fires.CountFiresInRange(building.Position, SceneRadius) == 0 && Patients.AreAllHandled(victims.ToArray()))
                Complete();
        }

        protected override void OnEnd()
        {
            foreach (int fire in fires)
                Fires.RemoveFire(fire);

            foreach (Ped victim in victims.Where(v => v.Exists()))
            {
                Patients.Release(victim);
                victim.Dismiss();
            }
            scene.Dismiss();
        }
    }

    [CalloutInfo("Commercial Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1)]
    internal class CommercialFire : BuildingFire
    {
        protected override FireLocationKind[] Kinds => new[] { FireLocationKind.Store, FireLocationKind.Commercial, FireLocationKind.Office, FireLocationKind.Public };
        protected override string Title => "Commercial Structure Fire";
    }

    [CalloutInfo("Apartment Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1, RequiresOpenInteriors = true)]
    internal class ApartmentFire : BuildingFire
    {
        protected override FireLocationKind[] Kinds => new[] { FireLocationKind.Apartment };
        protected override string Title => "Apartment Fire";
        protected override int BaseOccupants => 2;
    }

    [CalloutInfo("House Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1, RequiresOpenInteriors = true)]
    internal class HouseFire : BuildingFire
    {
        protected override FireLocationKind[] Kinds => new[] { FireLocationKind.House };
        protected override string Title => "Residential Structure Fire";
        protected override int BaseOccupants => 2;
    }

    [CalloutInfo("Industrial Building Fire", CalloutProbability.Low, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1, RequiresOpenInteriors = true)]
    internal class IndustrialBuildingFire : BuildingFire
    {
        protected override FireLocationKind[] Kinds => new[] { FireLocationKind.Industrial };
        protected override string Title => "Industrial Building Fire";
        protected override bool IsGasFire => true;
    }
}
