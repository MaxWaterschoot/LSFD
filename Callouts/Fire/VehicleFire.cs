using LSFD.API;
using LSFD.Core;
using Rage;

namespace LSFD.Callouts
{
    [CalloutInfo("Vehicle Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority2, AllowAmbient = true)]
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

            CalloutMessage = L.T("Vehicle Fire");
            CalloutAdvisory = driverBurned
                ? L.T("Vehicle fully involved, the driver got out with ~r~burn injuries~s~.")
                : L.T("A vehicle is on fire on the roadway, the driver reportedly got out.");
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
                driver = scene.Track(Patients.Spawn(vehicle.GetOffsetPositionRight(5f), PatientCondition.Injured, 0f, InjuryType.Burns));
            else
                driver = scene.SpawnCaller(vehicle.GetOffsetPositionRight(6f), "Driver", new[]
                {
                    L.T("I smelled something burning, so I pulled over. Then the smoke came out from under the hood."),
                    CallerLines.Pick(L.T("Everything I own is in that car."), L.T("Is it going to explode? There's a full tank in there."), L.T("I only just bought it!")),
                });

            Game.DisplayHelp(L.T("Extinguish the burning ~r~vehicle~s~.") + (driverBurned ? L.T(" Treat the ~r~driver~s~.") : ""));
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
