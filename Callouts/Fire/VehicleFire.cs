using LSFD.API;
using Rage;

namespace LSFD.Callouts
{
    [CalloutInfo("Vehicle Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority2)]
    internal class VehicleFire : Callout
    {
        private const float SceneRadius = 15f;

        private static readonly string[] VehicleModels = { "asea", "premier", "emperor", "speedo", "mule", "minivan", "pounder" };

        private readonly Scene scene = new Scene();
        private Vehicle vehicle;
        private Ped driver;
        private bool driverBurned;
        private uint arrivedAt;

        public override bool OnBeforeCalloutDisplayed()
        {
            CalloutPosition = Functions.GetRandomCalloutLocation();
            AlarmLevel = 1;
            driverBurned = MathHelper.GetRandomInteger(3) == 0;

            CalloutMessage = "Vehicle Fire";
            CalloutAdvisory = driverBurned
                ? "Vehicle fully involved, the driver got out with ~r~burn injuries~s~."
                : "A vehicle is on fire on the roadway, the driver reportedly got out.";
            return true;
        }

        protected override void OnArrival()
        {
            arrivedAt = Game.GameTime;

            vehicle = scene.SpawnVehicle(VehicleModels[MathHelper.GetRandomInteger(VehicleModels.Length)], CalloutPosition, MathHelper.GetRandomSingle(0f, 360f));
            vehicle.EngineHealth = 50f;
            Fires.SetOnFire(vehicle);
            Fires.StartFire(vehicle.GetOffsetPositionFront(2.5f), 10);

            if (driverBurned)
                driver = scene.Track(Patients.Spawn(vehicle.GetOffsetPositionRight(5f), PatientCondition.Injured));
            else
                driver = scene.SpawnBystander(vehicle.GetOffsetPositionRight(6f));

            Game.DisplayHelp("Extinguish the burning ~r~vehicle~s~." + (driverBurned ? " Treat the ~r~driver~s~." : ""));
        }

        public override void Process()
        {
            if (!HasArrived || Game.GameTime - arrivedAt < 5000)
                return;

            bool vehicleBurning = vehicle.Exists() && vehicle.IsOnFire;
            if (!vehicleBurning && Fires.CountFiresInRange(CalloutPosition, SceneRadius) == 0 && Patients.AreAllHandled(driver))
                Complete();
        }

        protected override void OnEnd()
        {
            Fires.ExtinguishInRange(CalloutPosition, SceneRadius);
            Patients.Release(driver);
            scene.Dismiss();
        }
    }
}
