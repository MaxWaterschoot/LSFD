using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using Rage;

namespace LSFD.Callouts.Xml
{
    /// <summary>
    /// Runs a callout from an <see cref="XmlCalloutDefinition"/>. It completes on its own once the fire is out, every
    /// trapped driver is extricated, every patient is handled and every task is done.
    /// </summary>
    internal class XmlCallout : Callout
    {
        // Gives GTA a moment to spawn the vehicle fires before they are counted.
        private const uint ArrivalGrace = 3000;

        private readonly XmlCalloutDefinition definition;
        private readonly Scene scene = new Scene();
        private readonly List<Ped> patients = new List<Ped>();
        private readonly List<(Ped ped, Vehicle vehicle)> trapped = new List<(Ped, Vehicle)>();
        private readonly List<SceneAction> tasks = new List<SceneAction>();
        private readonly List<ToxicAtmosphere> toxicAtmospheres = new List<ToxicAtmosphere>();
        private FireIncident fire;
        private bool hasVehicleFire;
        private float heading;
        private uint arrivedAt;

        public XmlCallout(XmlCalloutDefinition definition)
        {
            this.definition = definition;
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            XmlLocation location = definition.Location;
            if (location.Positions.Count > 0)
            {
                XmlPoint point = location.Positions[MathHelper.GetRandomInteger(location.Positions.Count)];
                CalloutPosition = point.Position;
                heading = point.Heading;
            }
            else
            {
                CalloutPosition = Functions.GetRandomCalloutLocation(location.MinDistance, location.MaxDistance);
                heading = MathHelper.GetRandomSingle(0f, 360f);
            }

            XmlAlarm alarm = definition.Alarm;
            if (alarm != null)
                AlarmLevel = alarm.SecondAlarmChance > 0 || alarm.ThirdAlarmChance > 0
                    ? RollAlarmLevel(alarm.SecondAlarmChance, alarm.ThirdAlarmChance)
                    : alarm.Level;
            else if (definition.Fire != null)
                AlarmLevel = 1;

            CalloutMessage = string.IsNullOrWhiteSpace(definition.Message) ? definition.Name : definition.Message;
            CalloutAdvisory = definition.Advisory;
            CalloutAreaRadius = location.AreaRadius;
            return true;
        }

        public override bool OnCalloutAccepted()
        {
            // Started now, like the built-in fires: the longer the response, the bigger the fire.
            XmlFire options = definition.Fire;
            if (options != null)
            {
                fire = StartFireIncident(CalloutPosition, new FireIncidentOptions
                {
                    InitialPoints = options.Points,
                    InitialIntensity = options.Intensity,
                    SpreadRadius = options.SpreadRadius,
                    SpreadSeconds = options.SpreadSeconds,
                    PointsPerAlarm = options.PointsPerAlarm,
                    MaxAlarmLevel = options.MaxAlarm,
                    Class = options.Class,
                    IsGasFire = options.Gas,
                    PowerPanel = options.PowerPanelDistance >= 0f
                        ? CalloutPosition.Around2D(options.PowerPanelDistance)
                        : (Vector3?)null,
                });
            }

            foreach (XmlBackup backup in definition.Backup)
            {
                for (int i = 0; i < backup.Count; i++)
                    Functions.RequestBackup(backup.Type, StagingPosition);
            }
            return true;
        }

        protected override void OnArrival()
        {
            arrivedAt = Game.GameTime;

            foreach (XmlVehicle vehicle in definition.Vehicles)
                SpawnVehicle(vehicle);

            foreach (XmlPatient patient in definition.Patients)
            {
                for (int i = 0; i < patient.Count; i++)
                {
                    Vector3 position = CalloutPosition.Around2D(1f, System.Math.Max(1f, patient.Distance));
                    Ped ped = patient.Minor
                        ? Patients.SpawnWalkingWounded(position)
                        : Patients.Spawn(position, patient.Condition, MathHelper.GetRandomSingle(0f, 360f), patient.Injury);
                    patients.Add(scene.Track(ped));
                }
            }

            foreach (XmlBystander bystander in definition.Bystanders)
            {
                for (int i = 0; i < bystander.Count; i++)
                    scene.SpawnBystander(CalloutPosition.Around2D(bystander.Distance * 0.5f, bystander.Distance));
            }

            foreach (XmlToxicAtmosphere zone in definition.ToxicAtmospheres)
                toxicAtmospheres.Add(Hazards.AddToxicAtmosphere(CalloutPosition, zone.Radius, zone.Strength));

            foreach (XmlTask task in definition.Tasks)
                AddTask(task);

            if (!string.IsNullOrWhiteSpace(definition.ArrivalHelp))
                Game.DisplayHelp(definition.ArrivalHelp);
        }

        public override void Process()
        {
            if (!HasArrived || Game.GameTime - arrivedAt < ArrivalGrace)
                return;

            bool fireOut = (fire == null || fire.IsOut)
                && (!hasVehicleFire || Fires.CountFiresInRange(CalloutPosition, CalloutAreaRadius) == 0);
            bool everyoneOut = trapped.All(t => Rescue.IsExtricated(t.ped) || !t.ped.Exists());

            if (fireOut && everyoneOut && Patients.AreAllHandled(patients.ToArray()) && tasks.All(t => t.IsDone))
            {
                Functions.DisplayNotification("~g~Scene handled",
                    string.IsNullOrWhiteSpace(definition.CompletedMessage) ? "Everything is taken care of. Good work." : definition.CompletedMessage);
                Complete();
            }
        }

        protected override void OnEnd()
        {
            foreach (var (ped, vehicle) in trapped)
                Rescue.Release(ped, vehicle);
            foreach (Ped ped in patients)
                Patients.Release(ped);
            foreach (ToxicAtmosphere zone in toxicAtmospheres)
                zone.Remove();
            scene.Dismiss();
        }

        private void SpawnVehicle(XmlVehicle data)
        {
            Vector3 position = data.Distance > 0f
                ? World.GetNextPositionOnStreet(CalloutPosition.Around2D(data.Distance))
                : CalloutPosition;
            float vehicleHeading = heading + MathHelper.GetRandomSingle(-30f, 30f);

            Vehicle vehicle = data.IsRandomModel ? scene.SpawnCar(position, vehicleHeading) : scene.SpawnVehicle(data.Model, position, vehicleHeading);
            if (data.Crash > 0f)
                Rescue.CrashVehicle(vehicle, new Vector3(0f, 1f, 0f), MathHelper.Clamp(data.Crash, 0f, 1f));

            if (data.TrapDriver)
            {
                var driver = scene.Track(new Ped(vehicle.Position + new Vector3(0f, 0f, 2f)));
                Rescue.TrapInVehicle(driver, vehicle);
                trapped.Add((driver, vehicle));
                // Once extricated, the driver still needs to be treated.
                patients.Add(driver);
            }

            if (data.OnFire)
            {
                vehicle.EngineHealth = 50f;
                Fires.SetOnFire(vehicle);
                Fires.StartFire(vehicle.GetOffsetPositionFront(2.5f), 10);
                hasVehicleFire = true;
            }
        }

        private void AddTask(XmlTask data)
        {
            Vector3 position = CalloutPosition + new Vector3(data.OffsetX, data.OffsetY, data.OffsetZ);
            tasks.Add(AddSceneAction(new SceneAction(position, data.Text, () => OnTaskDone(data))
            {
                HoldSeconds = data.Seconds,
                RequiredEquipment = data.RequiredEquipment,
            }));
        }

        private void OnTaskDone(XmlTask data)
        {
            if (data.ClearsToxicAtmosphere)
            {
                foreach (ToxicAtmosphere zone in toxicAtmospheres)
                    zone.Remove();
                toxicAtmospheres.Clear();
            }

            if (!string.IsNullOrWhiteSpace(data.DoneMessage))
                Game.DisplaySubtitle(data.DoneMessage, 4000);
        }
    }
}
