using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Firefighting;
using LSFD.Utils;
using Rage;
using Rage.Native;

namespace LSFD.Callouts
{
    /// <summary>
    /// Shared by gas and fuel calls: a cordon of cones to set up and the risk that the leak finds an ignition source,
    /// which is far higher while traffic and onlookers are still close.
    /// </summary>
    internal abstract class HazmatCall : Callout
    {
        private const string ConeModel = "prop_roadcone02a";

        protected readonly Scene Scene = new Scene();
        private readonly List<SceneAction> cordon = new List<SceneAction>();
        private uint nextIgnitionCheck;

        protected FireIncident Fire { get; private set; }
        protected bool IsCordoned => cordon.All(c => c.IsDone);
        protected bool HasIgnited => Fire != null;

        /// <summary>Chance per second of ignition with and without a cordon.</summary>
        protected abstract float IgnitionChance { get; }
        protected abstract float CordonedIgnitionChance { get; }

        /// <summary>Whether there is still something that can ignite.</summary>
        protected abstract bool IsLeaking { get; }

        protected abstract void Ignite();

        protected void AddCordon(Vector3 center, float radius, int cones)
        {
            float start = MathHelper.GetRandomSingle(0f, 360f);
            for (int i = 0; i < cones; i++)
            {
                float heading = start + 360f / cones * i;
                Vector3 offset = MathHelper.ConvertHeadingToDirection(heading) * radius;
                Vector3 position = World.GetNextPositionOnStreet(center + offset);
                cordon.Add(AddSceneAction(new SceneAction(position, L.T("place ~y~traffic cones~s~ for the cordon"), () => PlaceCone(position))
                {
                    RequiredEquipment = EquipmentType.TrafficCones,
                    HoldSeconds = 1.5f,
                    ProgressLabel = L.T("PLACING"),
                    Radius = 2.5f,
                }));
            }
        }

        private void PlaceCone(Vector3 position)
        {
            Model model = ConeModel;
            if (model.IsValid)
            {
                model.LoadAndWait();
                var cone = new Rage.Object(model, position) { IsPersistent = true };
                NativeFunction.CallByName<uint>("PLACE_OBJECT_ON_GROUND_PROPERLY", cone);
                Scene.Track(cone);
            }

            if (IsCordoned)
            {
                Game.DisplaySubtitle(L.T("Cordon ~g~in place~s~. Onlookers are moving back."), 4000);
                OnCordoned();
            }
        }

        protected virtual void OnCordoned()
        {
        }

        protected void StartFire(Vector3 position, bool gas)
        {
            Fire = StartFireIncident(position, new FireIncidentOptions
            {
                Class = FireClass.Flammable,
                IsGasFire = true,
                InitialPoints = gas ? 3 : 2,
                InitialIntensity = 0.8f,
                SpreadRadius = 8f,
                MaxAlarmLevel = 2,
            });
            Priority = CalloutPriority.Priority1;
        }

        public override void Process()
        {
            if (!HasArrived || HasIgnited || !IsLeaking || Game.GameTime < nextIgnitionCheck)
                return;
            nextIgnitionCheck = Game.GameTime + 1000;

            float chance = IsCordoned ? CordonedIgnitionChance : IgnitionChance;
            if (MathHelper.GetRandomSingle(0f, 1f) < chance || Fires.CountFiresInRange(CalloutPosition, 10f) > 0)
                Ignite();
        }

        protected override void OnEnd()
        {
            Scene.Dismiss();
        }
    }

    [CalloutInfo("Gas Leak", CalloutProbability.Medium, Category = CalloutCategory.Hazmat, Priority = CalloutPriority.Priority1)]
    internal class GasLeak : HazmatCall
    {
        private const float CloudScale = 0.35f;

        private readonly LoopedParticle cloud = new LoopedParticle(FireEffects.HotspotSmokeAsset);
        private readonly List<Ped> onlookers = new List<Ped>();
        private Vector3 leak;
        private bool valveClosed;
        private bool allClear;

        protected override float IgnitionChance => 0.006f;
        protected override float CordonedIgnitionChance => 0.001f;
        protected override bool IsLeaking => !valveClosed;

        public override bool OnBeforeCalloutDisplayed()
        {
            CalloutPosition = Functions.GetRandomCalloutLocation();
            CalloutMessage = L.T("Gas Leak");
            CalloutAdvisory = L.T("Construction crew hit a gas main, ~o~strong smell of gas~s~. Approach from upwind, no ignition sources.");
            CalloutAreaRadius = 40f;
            return true;
        }

        protected override void OnArrival()
        {
            leak = CalloutPosition.Around2D(3f, 6f);
            cloud.Start(FireEffects.HotspotSmokeEffect, leak, Rotator.Zero, CloudScale);
            cloud.SetColor(1.5f, 1.5f, 1.5f);

            Model barrier = "prop_barrier_work05";
            if (barrier.IsValid)
                Scene.Track(new Rage.Object(barrier, leak.Around2D(1.5f)) { IsPersistent = true });
            onlookers.Add(Scene.SpawnCaller(leak.Around2D(6f, 12f), "Foreman", new[]
            {
                L.T("We were digging for a water line and the excavator went right through the gas main."),
                L.T("The shut-off valve is under a cover in the street, a few meters from the hole. You need a bar to turn it."),
            }));
            for (int i = 0; i < 2; i++)
                onlookers.Add(Scene.SpawnBystander(leak.Around2D(6f, 12f)));

            AddCordon(leak, 20f, 3);
            Vector3 valve = World.GetNextPositionOnStreet(leak.Around2D(6f, 9f));
            AddSceneAction(new SceneAction(valve, L.T("close the ~y~gas valve~s~"), CloseValve)
            {
                RequiredEquipment = EquipmentType.Crowbar,
                RequirementHint = L.T("The valve needs a ~y~halligan bar~s~ from the engine as a key."),
                HoldSeconds = 6f,
                ProgressLabel = L.T("CLOSING VALVE"),
            });

            Game.DisplayHelp(L.T("Set up a ~y~cordon~s~ with traffic cones, then close the ~y~gas valve~s~. Any spark can ignite the gas."));
        }

        protected override void OnCordoned()
        {
            foreach (Ped ped in onlookers.Where(p => p.Exists()))
                ped.Tasks.FollowNavigationMeshToPosition(leak + Vector3.Normalize(ped.Position - leak) * 40f, 0f, 1f);
        }

        private void CloseValve()
        {
            valveClosed = true;
            cloud.Stop();
            Game.DisplaySubtitle(L.T("Valve ~g~closed~s~. Let the gas clear and check the area with the gas detector."), 5000);
            AddSceneAction(new SceneAction(leak, L.T("~y~measure~s~ for gas"), () =>
            {
                allClear = true;
                Game.DisplaySubtitle(L.T("Gas detector: ~g~0% LEL~s~. The gas company takes over."), 5000);
            })
            {
                RequiredEquipment = EquipmentType.GasDetector,
                HoldSeconds = 4f,
                ProgressLabel = L.T("MEASURING"),
                AnimationDictionary = "",
            });
        }

        protected override void Ignite()
        {
            cloud.Stop();
            World.SpawnExplosion(leak, 9, 4f, true, false, 1f);
            StartFire(leak, gas: true);
            Functions.DisplayNotification(L.T("~r~Gas ignited!"), L.T("The leak caught fire. Close the valve and protect the surroundings; don't put it out before the gas is off."));
        }

        public override void Process()
        {
            base.Process();
            if (HasArrived && valveClosed && allClear && (Fire == null || Fire.IsOut))
                Complete();
            else if (HasArrived && valveClosed && HasIgnited && Fire.IsOut && !allClear)
                allClear = true;
        }

        protected override void OnEnd()
        {
            cloud.Stop();
            base.OnEnd();
        }
    }

    [CalloutInfo("Fuel Spill", CalloutProbability.Medium, Category = CalloutCategory.Hazmat, Priority = CalloutPriority.Priority2)]
    internal class FuelSpill : HazmatCall
    {
        private readonly List<SceneAction> puddles = new List<SceneAction>();
        private Vehicle vehicle;

        protected override float IgnitionChance => 0.003f;
        protected override float CordonedIgnitionChance => 0.0005f;
        protected override bool IsLeaking => puddles.Any(p => !p.IsDone);

        public override bool OnBeforeCalloutDisplayed()
        {
            CalloutPosition = Functions.GetRandomCalloutLocation();
            CalloutMessage = L.T("Fuel Spill");
            CalloutAdvisory = L.T("Vehicle with a ruptured fuel tank, ~o~fuel all over the roadway~s~. No injuries reported.");
            return true;
        }

        protected override void OnArrival()
        {
            vehicle = Scene.SpawnCar(CalloutPosition, MathHelper.GetRandomSingle(0f, 360f));
            Rescue.CrashVehicle(vehicle, new Vector3(0f, -1f, 0f), 0.4f);
            Scene.SpawnCaller(vehicle.GetOffsetPositionRight(5f), "Driver", new[]
            {
                L.T("I hit something on the road and the tank split open. It's gasoline, it's everywhere."),
                L.T("Nobody's hurt. I turned the engine off and got out."),
            });

            for (int i = 0; i < 4; i++)
            {
                Vector3 spot = vehicle.GetOffsetPositionFront(-vehicle.Model.Dimensions.Y / 2f - 1f - 1.8f * i) + vehicle.RightVector * MathHelper.GetRandomSingle(-1f, 1f);
                float ground = World.GetGroundZ(spot + new Vector3(0f, 0f, 2f), false, false) ?? spot.Z;
                spot = new Vector3(spot.X, spot.Y, ground);
                NativeFunction.CallByName<uint>("ADD_PETROL_DECAL", spot.X, spot.Y, spot.Z, ground, 2.5f, 1f);

                Vector3 puddle = spot;
                puddles.Add(AddSceneAction(new SceneAction(puddle, L.T("spread ~y~absorbent~s~ on the fuel"), () =>
                {
                    NativeFunction.CallByName<uint>("REMOVE_DECALS_IN_RANGE", puddle.X, puddle.Y, puddle.Z, 2f);
                    if (!IsLeaking)
                        Game.DisplaySubtitle(L.T("All fuel ~g~absorbed~s~. The road can be swept."), 4000);
                })
                {
                    RequiredEquipment = EquipmentType.Absorbent,
                    HoldSeconds = 4f,
                    ProgressLabel = L.T("ABSORBING"),
                    AnimationName = "fixing_a_ped",
                    Radius = 2f,
                }));
            }

            AddCordon(CalloutPosition, 18f, 2);
            Game.DisplayHelp(L.T("Cordon off the road with ~y~traffic cones~s~ and soak up the fuel with ~y~absorbent~s~ before it ignites."));
        }

        protected override void Ignite()
        {
            Vector3 spot = puddles.First(p => !p.IsDone).Position;
            StartFire(spot, gas: false);
            if (vehicle.Exists())
                Fires.SetOnFire(vehicle);
            Functions.DisplayNotification(L.T("~r~Fuel ignited!"), L.T("The spill caught fire. Use ~y~foam~s~ on the burning fuel."));
        }

        public override void Process()
        {
            base.Process();
            if (HasArrived && !IsLeaking && IsCordoned && (Fire == null || Fire.IsOut))
                Complete();
        }

        protected override void OnEnd()
        {
            foreach (SceneAction puddle in puddles)
                NativeFunction.CallByName<uint>("REMOVE_DECALS_IN_RANGE", puddle.Position.X, puddle.Position.Y, puddle.Position.Z, 2f);
            base.OnEnd();
        }
    }
}
