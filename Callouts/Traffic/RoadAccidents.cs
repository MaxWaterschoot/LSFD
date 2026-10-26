using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Core;
using Rage;

namespace LSFD.Callouts
{
    /// <summary>
    /// Base for road accidents: spawns the wrecks and patients, and completes once every patient is extricated
    /// (if trapped), treated and every fire is out.
    /// </summary>
    internal abstract class RoadAccident : Callout
    {
        protected readonly Scene Scene = new Scene();
        protected readonly List<Ped> PatientsOnScene = new List<Ped>();
        private readonly List<(Ped ped, Vehicle vehicle)> trapped = new List<(Ped, Vehicle)>();
        private uint arrivedAt;

        protected abstract string Title { get; }
        protected abstract string Advisory { get; }
        protected abstract void BuildScene(float heading);

        public override bool OnBeforeCalloutDisplayed()
        {
            CalloutPosition = Functions.GetRandomCalloutLocation();
            CalloutMessage = Title;
            CalloutAdvisory = Advisory;
            return true;
        }

        protected override void OnArrival()
        {
            arrivedAt = Game.GameTime;
            BuildScene(MathHelper.GetRandomSingle(0f, 360f));
        }

        public override void Process()
        {
            if (!HasArrived || Game.GameTime - arrivedAt < 3000)
                return;

            bool everyoneOut = trapped.All(t => Rescue.IsExtricated(t.ped));
            if (everyoneOut && Patients.AreAllHandled(PatientsOnScene.ToArray()) && Fires.CountFiresInRange(CalloutPosition, 25f) == 0)
                Complete();
        }

        protected override void OnEnd()
        {
            foreach (var (ped, vehicle) in trapped)
                Rescue.Release(ped, vehicle);
            foreach (Ped ped in PatientsOnScene)
                Patients.Release(ped);
            Scene.Dismiss();
        }

        protected Vehicle Wreck(Vector3 position, float heading, Vector3 impact, float minSeverity, float maxSeverity)
        {
            Vehicle vehicle = Scene.SpawnCar(position, heading);
            Rescue.CrashVehicle(vehicle, impact, MathHelper.GetRandomSingle(minSeverity, maxSeverity));
            return vehicle;
        }

        protected Ped Trap(Vehicle vehicle, int seat = -1)
        {
            var ped = Scene.Track(new Ped(vehicle.Position + new Vector3(0f, 0f, 2f)));
            Rescue.TrapInVehicle(ped, vehicle, seat);
            trapped.Add((ped, vehicle));
            // Once extricated, the patient still needs to be treated.
            PatientsOnScene.Add(ped);
            return ped;
        }

        /// <summary>Someone who saw it happen and has something to tell the player.</summary>
        protected Ped Witness(Vector3 position, params string[] statement) => Scene.SpawnCaller(position, "Witness", statement);

        /// <summary>Someone who got out by themselves with minor injuries (green).</summary>
        protected Ped WalkingWounded(Vector3 position)
        {
            Ped ped = Scene.Track(Patients.SpawnWalkingWounded(position));
            PatientsOnScene.Add(ped);
            return ped;
        }

        protected Ped Patient(Vector3 position, PatientCondition condition)
        {
            Ped ped = Scene.Track(Patients.Spawn(position, condition));
            PatientsOnScene.Add(ped);
            return ped;
        }

        /// <summary>A random condition for someone who was hit hard.</summary>
        protected static PatientCondition SeriousCondition()
        {
            int roll = MathHelper.GetRandomInteger(100);
            return roll < 15 ? PatientCondition.CardiacArrest : roll < 45 ? PatientCondition.Unconscious : PatientCondition.Injured;
        }
    }

    [CalloutInfo("Vehicle Collision", CalloutProbability.High, Category = CalloutCategory.Traffic, Priority = CalloutPriority.Priority1, AllowAmbient = true)]
    internal class VehicleCollision : RoadAccident
    {
        protected override string Title => L.T("Head-on Collision");
        protected override string Advisory => L.T("Two vehicles, one occupant ~r~trapped~s~. Extrication required.");

        protected override void BuildScene(float heading)
        {
            Vehicle first = Wreck(CalloutPosition, heading, new Vector3(0f, 1f, 0f), 0.6f, 1f);
            Vector3 second = first.GetOffsetPositionFront(5.5f) + first.RightVector * MathHelper.GetRandomSingle(-0.8f, 0.8f);
            Vehicle other = Wreck(second, heading + 180f + MathHelper.GetRandomSingle(-25f, 25f), new Vector3(0f, 1f, 0f), 0.5f, 0.9f);

            Ped driver = Trap(first);
            Patient(other.GetOffsetPositionRight(3f), PatientCondition.Injured);
            Witness(other.GetOffsetPositionRight(-5f),
                L.T("The car came right across the center line, hit them head on!"),
                L.F($"The driver isn't moving. I tried {CallerLines.His(driver)} door, it's jammed shut."));

            Game.DisplayHelp(L.T("Stabilize the vehicle with ~y~cribbing~s~, force or cut a door with the ~y~hydraulic tools~s~, extricate and treat the ~r~patient~s~."));
        }
    }

    [CalloutInfo("Minor Collision", CalloutProbability.High, Category = CalloutCategory.Traffic, Priority = CalloutPriority.Priority3, AllowAmbient = true)]
    internal class MinorCollision : RoadAccident
    {
        protected override string Title => L.T("Minor Collision");
        protected override string Advisory => L.T("Rear-end collision, one driver complaining of neck pain.");

        protected override void BuildScene(float heading)
        {
            Vehicle front = Wreck(CalloutPosition, heading, new Vector3(0f, -1f, 0f), 0.15f, 0.3f);
            Vehicle back = Wreck(front.GetOffsetPositionFront(-5f), heading, new Vector3(0f, 1f, 0f), 0.2f, 0.35f);

            Patient(front.GetOffsetPositionRight(2.5f), PatientCondition.Injured);
            Witness(back.GetOffsetPositionRight(3f),
                L.T("They stopped all of a sudden and I couldn't brake in time. I'm so sorry."),
                L.T("I'm fine, but the other driver says the neck hurts."));
            Game.DisplayHelp(L.T("Check the vehicles and treat the ~r~injured driver~s~."));
        }
    }

    [CalloutInfo("Vehicle Rollover", CalloutProbability.Medium, Category = CalloutCategory.Traffic, Priority = CalloutPriority.Priority1, AllowAmbient = true)]
    internal class VehicleRollover : RoadAccident
    {
        protected override string Title => L.T("Vehicle Rollover");
        protected override string Advisory => L.T("Single vehicle rolled over, driver ~r~trapped~s~ upside down.");

        protected override void BuildScene(float heading)
        {
            Vehicle vehicle = Wreck(CalloutPosition + new Vector3(0f, 0f, 1f), heading, new Vector3(0f, 0f, 1f), 0.5f, 0.9f);
            vehicle.Rotation = new Rotator(0f, 180f, heading);
            Ped driver = Trap(vehicle);

            Witness(vehicle.GetOffsetPositionRight(6f),
                L.T("It clipped the curb and just flipped, it rolled over twice!"),
                L.F($"The driver's still hanging upside down in there. {CallerLines.Capital(CallerLines.He(driver))} was talking a minute ago."));
            Game.DisplayHelp(L.T("The vehicle is on its roof: stabilize it with ~y~cribbing~s~ before cutting the ~r~driver~s~ out."));
        }
    }

    [CalloutInfo("Motorcycle Accident", CalloutProbability.Medium, Category = CalloutCategory.Traffic, Priority = CalloutPriority.Priority1, AllowAmbient = true)]
    internal class MotorcycleAccident : RoadAccident
    {
        protected override string Title => L.T("Motorcycle Accident");
        protected override string Advisory => L.T("Motorcyclist down after a collision with a car.");

        protected override void BuildScene(float heading)
        {
            Vehicle car = Wreck(CalloutPosition, heading, new Vector3(-1f, 0.5f, 0f), 0.3f, 0.6f);

            Vehicle bike = Scene.SpawnBike(car.GetOffsetPositionRight(-4f), heading + 70f);
            bike.Rotation = new Rotator(0f, 90f, heading + 70f);

            Ped rider = Patient(bike.GetOffsetPositionFront(3f), SeriousCondition());
            Witness(car.GetOffsetPositionRight(3f),
                L.F($"I pulled out, I just didn't see {CallerLines.Him(rider)} coming!"),
                L.F($"{CallerLines.Capital(CallerLines.He(rider))}'s not getting up. I didn't want to move {CallerLines.Him(rider)}, {CallerLines.He(rider)} still has the helmet on."));
            Game.DisplayHelp(L.T("Assess the ~r~motorcyclist~s~ and start treatment."));
        }
    }

    [CalloutInfo("Pedestrian Struck", CalloutProbability.Medium, Category = CalloutCategory.Traffic, Priority = CalloutPriority.Priority1, AllowAmbient = true)]
    internal class PedestrianStruck : RoadAccident
    {
        protected override string Title => L.T("Pedestrian Struck");
        protected override string Advisory => L.T("Pedestrian hit by a vehicle, driver stayed on scene.");

        protected override void BuildScene(float heading)
        {
            Vehicle car = Wreck(CalloutPosition, heading, new Vector3(0f, 1f, 0f), 0.15f, 0.3f);
            Ped pedestrian = Patient(car.GetOffsetPositionFront(5f), SeriousCondition());
            Witness(car.GetOffsetPositionRight(2.5f),
                L.F($"I didn't see {CallerLines.Him(pedestrian)}! {CallerLines.Capital(CallerLines.He(pedestrian))} stepped out between the parked cars!"),
                L.F($"I was only doing thirty. {CallerLines.Capital(CallerLines.He(pedestrian))} hit the windshield and landed over there."));
            Game.DisplayHelp(L.T("Treat the ~r~pedestrian~s~. Start CPR if there is no pulse."));
        }
    }

    [CalloutInfo("Multi-Vehicle Collision", CalloutProbability.Low, Category = CalloutCategory.Traffic, Priority = CalloutPriority.Priority1, AllowAmbient = true)]
    internal class MultiVehicleCollision : RoadAccident
    {
        private bool vehicleOnFire;

        protected override string Title => L.T("Multi-Vehicle Collision");
        protected override string Advisory => L.T("Pile-up with multiple patients and entrapment. ~o~Mass casualty incident~s~.");

        public override bool OnBeforeCalloutDisplayed()
        {
            AlarmLevel = 2;
            vehicleOnFire = MathHelper.GetRandomInteger(3) == 0;
            return base.OnBeforeCalloutDisplayed();
        }

        // A mass casualty incident: more ambulances on the first call.
        public override bool OnCalloutAccepted()
        {
            Functions.RequestBackup(LSFD.Apparatus.ApparatusType.Ambulance, CalloutPosition);
            Functions.RequestBackup(LSFD.Apparatus.ApparatusType.Ambulance, CalloutPosition);
            return true;
        }

        public override void Process()
        {
            // The call isn't over before every patient has been triaged.
            if (Patients.AreAllTriaged(PatientsOnScene.ToArray()))
                base.Process();
        }

        protected override void BuildScene(float heading)
        {
            Vehicle first = Wreck(CalloutPosition, heading, new Vector3(0f, 1f, 0f), 0.6f, 1f);
            Vehicle second = Wreck(first.GetOffsetPositionFront(5.5f), heading + 160f, new Vector3(0f, 1f, 0f), 0.6f, 1f);
            Vehicle third = Wreck(first.GetOffsetPositionFront(-6f) + first.RightVector * 2f, heading + 15f, new Vector3(0f, 1f, 0f), 0.4f, 0.8f);
            Vehicle fourth = Wreck(second.GetOffsetPositionRight(4f), heading + 120f, new Vector3(1f, 0f, 0f), 0.3f, 0.7f);

            Trap(first);
            Trap(second);
            Patient(third.GetOffsetPositionRight(3f), PatientCondition.Injured);
            Patient(fourth.GetOffsetPositionRight(-3f), SeriousCondition());

            int walking = MathHelper.GetRandomInteger(2, 4);
            for (int i = 0; i < walking; i++)
                WalkingWounded(CalloutPosition.Around2D(8f, 12f));

            if (vehicleOnFire)
                Fires.SetOnFire(fourth);

            Game.DisplayHelp(L.T("~o~Mass casualty incident~s~: ~y~triage~s~ every patient first (hold E), then treat ~r~red~s~, ~y~yellow~s~ and ~g~green~s~ in that order. Black means expectant: CPR only once the others are cared for."));
        }
    }
}
