using System.Linq;
using LSFD.API;
using LSFD.Buildings;
using Rage;

namespace LSFD.Callouts
{
    [CalloutInfo("Electrical Fire", CalloutProbability.Low, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1)]
    internal class ElectricalFire : Callout
    {
        private BuildingInfo building;
        private FireIncident fire;

        public override bool OnBeforeCalloutDisplayed()
        {
            building = Interiors.GetRandomBuilding(300f, 2500f, FireLocationKind.Commercial, FireLocationKind.Office, FireLocationKind.Industrial, FireLocationKind.Store);
            if (building == null)
                return false;

            AlarmLevel = RollAlarmLevel(15, 0);
            CalloutPosition = building.Staging;
            CalloutMessage = "Electrical Fire";
            CalloutAdvisory = $"~y~{building.Name}~s~. Fire in the electrical room, ~o~power still on~s~.";
            return true;
        }

        public override bool OnCalloutAccepted()
        {
            fire = StartFireIncident(building.Position, new FireIncidentOptions
            {
                IsIndoor = true,
                Class = FireClass.Electrical,
                PowerPanel = building.Position.Around2D(2f, 3f),
                InitialPoints = AlarmLevel,
                SpreadRadius = 6f,
                SmokeColumnHeight = 8f,
            });
            return true;
        }

        protected override void OnArrival()
        {
            Interiors.PrepareInterior(building.Position);
            Interiors.LockDoors(building.Position, 15f, 0.5f);
            Game.DisplayHelp("~o~Live electrics~s~: water will shock you. ~y~Isolate the power~s~ at the panel first, or use an extinguisher.");
        }

        public override void Process()
        {
            if (HasArrived && fire.IsOut)
                Complete();
        }
    }

    [CalloutInfo("Electric Vehicle Fire", CalloutProbability.Low, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority2)]
    internal class ElectricVehicleFire : Callout
    {
        private static readonly string[] ElectricModels = { "surge", "voltic", "dilettante", "cyclone", "neon", "raiden", "imorgon", "khamelion" };

        private readonly Scene scene = new Scene();
        private FireIncident fire;
        private Vehicle vehicle;

        public override bool OnBeforeCalloutDisplayed()
        {
            CalloutPosition = Functions.GetRandomCalloutLocation();
            CalloutMessage = "Electric Vehicle Fire";
            CalloutAdvisory = "An electric car is on fire, the battery is popping. ~o~Expect a long cooling job~s~.";
            return true;
        }

        public override bool OnCalloutAccepted()
        {
            fire = StartFireIncident(CalloutPosition, new FireIncidentOptions
            {
                Class = FireClass.Battery,
                InitialPoints = 2,
                InitialIntensity = 0.8f,
                SpreadRadius = 3f,
                PointsPerAlarm = 3,
                MaxAlarmLevel = 2,
            });
            return true;
        }

        protected override void OnArrival()
        {
            string[] available = ElectricModels.Where(m => new Model(m).IsValid).ToArray();
            vehicle = scene.SpawnVehicle(available.Length > 0 ? available[MathHelper.GetRandomInteger(available.Length)] : "asea",
                CalloutPosition, MathHelper.GetRandomSingle(0f, 360f));
            vehicle.EngineHealth = 0f;
            Fires.SetOnFire(vehicle);
            scene.SpawnBystander(vehicle.GetOffsetPositionRight(7f));

            Game.DisplayHelp("~o~Lithium battery fire~s~: foam doesn't help, it takes a lot of water and it keeps rekindling. " +
                "Keep cooling until the thermal camera shows no hotspots.");
        }

        public override void Process()
        {
            if (!HasArrived || !fire.IsOut)
                return;

            Fires.ExtinguishInRange(CalloutPosition, 8f);
            Complete();
        }

        protected override void OnEnd()
        {
            scene.Dismiss();
        }
    }
}
