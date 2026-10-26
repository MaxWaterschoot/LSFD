using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSFD.API;
using LSFD.Core;
using Rage;
using Rage.Native;

namespace LSFD.LivingWorld
{
    /// <summary>
    /// AI police for the scene: a patrol car comes to crashes, hazmat calls and bigger fires, parks across the road
    /// upstream of the scene with its lights on, puts out cones and an officer directs traffic.
    /// </summary>
    internal class PoliceSupport
    {
        private const float MinSpawnDistance = 200f;
        private const float MaxSpawnDistance = 280f;
        private const float MinSpawnDistanceToPlayer = 120f;

        // The car blocks the road this far from the scene, on the side it comes from.
        private const float BlockDistance = 22f;
        private const float TrafficPostDistance = 9f;
        private const float BlockAngle = 35f;
        private const float ParkDistance = 10f;
        private const uint GiveUpDrivingAfter = 120000;
        private const uint ParkingTime = 1500;

        // Only moved into the neat blocking position when the player can't see it happen.
        private const float SnapDistanceToPlayer = 60f;

        private const float ClosureRadius = 8f;
        private const float SlowTrafficSpeed = 2f;
        private const uint WalkTimeout = 20000;
        private const uint BoardingTimeout = 15000;
        private const string ConeModel = "prop_roadcone02a";
        private const string TrafficScenario = "WORLD_HUMAN_CAR_PARK_ATTENDANT";
        private const string StandbyScenario = "WORLD_HUMAN_COP_IDLES";

        private enum PoliceState
        {
            Responding,
            Parking,
            OnScene,
            Leaving,
        }

        private class Officer
        {
            public Ped Ped;
            public Vector3 Post;
            public float Heading;
            public string Scenario;
            public uint WalkingSince;
            public bool Posted;
        }

        private class PoliceUnit
        {
            public Vehicle Car;
            public Blip Blip;
            public readonly List<Officer> Officers = new List<Officer>();
            public readonly List<Object> Cones = new List<Object>();
            public Vector3 BlockPosition;
            public Vector3 Upstream;
            public float BlockHeading;
            public bool CloseRoad;
            public PoliceState State;
            public uint StateSince;
            public int? Zone;

            public Ped Driver => Officers.Count > 0 ? Officers[0].Ped : null;
        }

        private readonly List<PoliceUnit> units = new List<PoliceUnit>();

        /// <summary>Sends a patrol car when the call needs one.</summary>
        public void Respond(Callout callout)
        {
            if (!Settings.PoliceAtScene || !NeedsPolice(callout))
                return;

            // A serious crash closes the road; for the rest, traffic is let past slowly.
            bool closeRoad = callout.Category == CalloutCategory.Traffic && callout.Priority == CalloutPriority.Priority1;
            Spawn(callout.CalloutPosition, closeRoad);
        }

        public void Process()
        {
            foreach (PoliceUnit unit in units.ToList())
                Update(unit);
        }

        /// <summary>The call is over: the officers pick up, get back in and drive off.</summary>
        public void ReleaseAll()
        {
            foreach (PoliceUnit unit in units.Where(u => u.State != PoliceState.Leaving).ToList())
            {
                if (unit.Car.Exists())
                    Leave(unit);
                else
                    Remove(unit, false);
            }
        }

        /// <summary>Removes the police right away, e.g. when the plugin unloads.</summary>
        public void RemoveAll()
        {
            foreach (PoliceUnit unit in units.ToList())
                Remove(unit, true);
        }

        private static bool NeedsPolice(Callout callout)
        {
            return callout.Category == CalloutCategory.Traffic || callout.Category == CalloutCategory.Hazmat || callout.AlarmLevel >= 2;
        }

        private void Spawn(Vector3 scene, bool closeRoad)
        {
            bool county = Roads.IsCounty(scene);
            Model carModel = county ? Settings.SheriffVehicle : Settings.PoliceVehicle;
            Model officerModel = county ? Settings.SheriffOfficerModel : Settings.PoliceOfficerModel;
            if (!carModel.IsValid || !officerModel.IsValid)
            {
                Game.LogTrivial($"LSFD: police model '{carModel.Name}' or '{officerModel.Name}' does not exist, check the [World] section of LSFD.ini.");
                return;
            }

            Vector3 start = FindSpawn(scene);
            if (!Roads.TryGetRoad(scene, out Vector3 node, out Vector3 direction))
            {
                node = scene;
                direction = (start - scene).ToNormalized();
            }

            // Block the road on the side the car comes from, angled across the lane, facing the scene.
            Vector3 upstream = Vector3.Dot(start - node, direction) >= 0f ? direction : -direction;
            var unit = new PoliceUnit
            {
                BlockPosition = node + upstream * BlockDistance,
                Upstream = upstream,
                BlockHeading = MathHelper.ConvertDirectionToHeading(-upstream) + BlockAngle,
                CloseRoad = closeRoad,
                State = PoliceState.Responding,
                StateSince = Game.GameTime,
            };

            carModel.LoadAndWait();
            officerModel.LoadAndWait();
            unit.Car = new Vehicle(carModel, start, MathHelper.ConvertDirectionToHeading(scene - start)) { IsPersistent = true, IsSirenOn = true };
            for (int seat = -1; seat <= 0; seat++)
            {
                var ped = new Ped(officerModel, start + new Vector3(0f, 0f, 3f), 0f) { IsPersistent = true, BlockPermanentEvents = true };
                ped.WarpIntoVehicle(unit.Car, seat);
                unit.Officers.Add(new Officer { Ped = ped });
            }
            carModel.Dismiss();
            officerModel.Dismiss();

            unit.Blip = unit.Car.AttachBlip();
            unit.Blip.Color = Color.DodgerBlue;
            unit.Blip.Scale = 0.7f;
            unit.Blip.Name = county ? "Sheriff" : "Police";

            unit.Driver.Tasks.DriveToPosition(unit.Car, unit.BlockPosition, 22f, VehicleDrivingFlags.Emergency, ParkDistance);
            units.Add(unit);
            Game.LogTrivial($"LSFD: {(county ? "sheriff" : "police")} unit responding to the scene{(closeRoad ? " to close the road" : "")}.");
        }

        private static Vector3 FindSpawn(Vector3 scene)
        {
            Vector3 player = Game.LocalPlayer.Character.Position;
            Vector3 position = Vector3.Zero;
            for (int i = 0; i < 5; i++)
            {
                position = World.GetNextPositionOnStreet(scene.Around2D(MinSpawnDistance, MaxSpawnDistance));
                if (position.DistanceTo(player) >= MinSpawnDistanceToPlayer && !Roads.IsOnScreen(position, 3f))
                    break;
            }
            return position;
        }

        private void Update(PoliceUnit unit)
        {
            if (!unit.Car.Exists() || unit.Driver == null || !unit.Driver.Exists())
            {
                Remove(unit, false);
                return;
            }

            switch (unit.State)
            {
                case PoliceState.Responding:
                    if (unit.Car.DistanceTo(unit.BlockPosition) <= ParkDistance + 2f || Game.GameTime - unit.StateSince > GiveUpDrivingAfter)
                    {
                        unit.Driver.Tasks.PerformDrivingManeuver(VehicleManeuver.Wait);
                        SetState(unit, PoliceState.Parking);
                    }
                    break;

                case PoliceState.Parking:
                    if (Game.GameTime - unit.StateSince >= ParkingTime)
                        SetUpClosure(unit);
                    break;

                case PoliceState.OnScene:
                    foreach (Officer officer in unit.Officers)
                        UpdateOfficer(officer);
                    break;

                case PoliceState.Leaving:
                    bool boarded = unit.Officers.All(o => !o.Ped.Exists() || o.Ped.CurrentVehicle == unit.Car);
                    if (boarded || Game.GameTime - unit.StateSince > BoardingTimeout)
                    {
                        if (unit.Driver.CurrentVehicle == unit.Car)
                            unit.Driver.Tasks.CruiseWithVehicle(unit.Car, 15f, VehicleDrivingFlags.Normal);
                        Remove(unit, false);
                    }
                    break;
            }
        }

        private void SetUpClosure(PoliceUnit unit)
        {
            Vehicle car = unit.Car;
            if (Game.LocalPlayer.Character.DistanceTo(unit.BlockPosition) > SnapDistanceToPlayer && !Roads.IsOnScreen(car.Position, 3f))
            {
                car.Position = unit.BlockPosition;
                car.Heading = unit.BlockHeading;
            }

            // Lights on, siren off.
            car.IsSirenOn = true;
            car.IsSirenSilent = true;
            unit.Zone = SpeedZone.Add(car.Position, ClosureRadius, unit.CloseRoad ? 0f : SlowTrafficSpeed);

            // A taper of cones in front of the car, from the lane edge towards its middle.
            Model cone = ConeModel;
            cone.LoadAndWait();
            for (int i = 0; i < 3; i++)
            {
                Vector3 position = car.Position + unit.Upstream * (5f + 3f * i) + car.RightVector * (1.5f - 0.6f * i);
                var prop = new Object(cone, position) { IsPersistent = true };
                NativeFunction.CallByName<uint>("PLACE_OBJECT_ON_GROUND_PROPERLY", prop);
                unit.Cones.Add(prop);
            }
            cone.Dismiss();

            // One officer directs the oncoming traffic, the other stands by the car.
            float facingTraffic = MathHelper.ConvertDirectionToHeading(unit.Upstream);
            Officer traffic = unit.Officers[0];
            traffic.Post = car.Position + unit.Upstream * TrafficPostDistance;
            traffic.Heading = facingTraffic;
            traffic.Scenario = TrafficScenario;

            if (unit.Officers.Count > 1)
            {
                Officer standby = unit.Officers[1];
                standby.Post = car.Position - unit.Upstream * 3f + car.RightVector * 2f;
                standby.Heading = facingTraffic + 180f;
                standby.Scenario = StandbyScenario;
            }

            foreach (Officer officer in unit.Officers.Where(o => o.Ped.Exists()))
            {
                officer.Ped.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
                officer.WalkingSince = 0;
            }
            SetState(unit, PoliceState.OnScene);
        }

        private static void UpdateOfficer(Officer officer)
        {
            Ped ped = officer.Ped;
            if (officer.Posted || !ped.Exists() || ped.IsInAnyVehicle(false) || officer.Scenario == null)
                return;

            if (officer.WalkingSince == 0)
            {
                ped.Tasks.FollowNavigationMeshToPosition(officer.Post, officer.Heading, 1.2f, 1f);
                officer.WalkingSince = Game.GameTime;
                return;
            }

            if (ped.DistanceTo(officer.Post) > 1.5f && Game.GameTime - officer.WalkingSince < WalkTimeout)
                return;

            ped.Heading = officer.Heading;
            NativeFunction.CallByName<uint>("TASK_START_SCENARIO_IN_PLACE", ped, officer.Scenario, 0, true);
            officer.Posted = true;
        }

        private void Leave(PoliceUnit unit)
        {
            if (unit.Zone.HasValue)
                SpeedZone.Remove(unit.Zone.Value);
            unit.Zone = null;
            foreach (Object cone in unit.Cones.Where(c => c.Exists()))
                cone.Dismiss();
            unit.Cones.Clear();

            unit.Car.IsSirenOn = false;
            for (int i = 0; i < unit.Officers.Count; i++)
            {
                Ped ped = unit.Officers[i].Ped;
                if (ped.Exists() && ped.CurrentVehicle != unit.Car)
                    ped.Tasks.EnterVehicle(unit.Car, i - 1);
            }
            SetState(unit, PoliceState.Leaving);
        }

        /// <summary>Stops tracking the unit; <paramref name="delete"/> removes it, otherwise GTA cleans it up once out of sight.</summary>
        private void Remove(PoliceUnit unit, bool delete)
        {
            units.Remove(unit);

            if (unit.Zone.HasValue)
                SpeedZone.Remove(unit.Zone.Value);
            if (unit.Blip.Exists())
                unit.Blip.Delete();

            var entities = new List<Entity>(unit.Cones);
            entities.AddRange(unit.Officers.Select(o => o.Ped));
            entities.Add(unit.Car);
            foreach (Entity entity in entities.Where(e => e.Exists()))
            {
                if (delete)
                {
                    entity.Delete();
                    continue;
                }

                if (entity is Ped ped)
                    ped.BlockPermanentEvents = false;
                entity.Dismiss();
            }
        }

        private static void SetState(PoliceUnit unit, PoliceState state)
        {
            unit.State = state;
            unit.StateSince = Game.GameTime;
        }
    }
}
