using LSFD.API;
using Rage;

namespace LSFD.Callouts
{
    /// <summary>A fire in the open: spread out over an area, bigger with each alarm, and it keeps spreading until it is fought.</summary>
    internal abstract class OutdoorFire : Callout
    {
        private readonly Scene scene = new Scene();
        private Vector3 center;
        private FireIncident fire;

        protected abstract string Title { get; }
        protected abstract string Advisory { get; }
        protected virtual bool IsGasFire => false;
        protected virtual int SecondAlarmChance => 20;

        /// <summary>The fire never escalates beyond this alarm.</summary>
        protected virtual int MaxAlarmLevel => 3;

        /// <summary>Seconds between spreads; vegetation goes quicker than a trash pile.</summary>
        protected virtual float SpreadSeconds => 40f;

        /// <summary>Where the fire is; return null to cancel the call.</summary>
        protected virtual Vector3? FindLocation() => Functions.GetRandomCalloutLocation();

        /// <summary>Extra props or people at the scene.</summary>
        protected virtual void DressScene(Scene scene, Vector3 center)
        {
        }

        // Room to grow into a higher alarm.
        private float MaxSpread => 4f + 6f * MaxAlarmLevel;

        public override bool OnBeforeCalloutDisplayed()
        {
            Vector3? location = FindLocation();
            if (!location.HasValue)
                return false;

            center = location.Value;
            AlarmLevel = RollAlarmLevel(SecondAlarmChance, SecondAlarmChance / 3);
            CalloutPosition = center;
            CalloutAreaRadius = 25f + 15f * AlarmLevel;
            CalloutMessage = Title;
            CalloutAdvisory = Advisory;
            return true;
        }

        public override bool OnCalloutAccepted()
        {
            fire = StartFireIncident(center, new FireIncidentOptions
            {
                IsGasFire = IsGasFire,
                InitialPoints = 1 + 2 * AlarmLevel,
                SpreadRadius = MaxSpread,
                SpreadSeconds = SpreadSeconds,
                PointsPerAlarm = 5,
                MaxAlarmLevel = MaxAlarmLevel,
            });
            return true;
        }

        protected override void OnArrival()
        {
            DressScene(scene, center);
            Game.DisplayHelp($"Extinguish all the ~r~fires~s~ in the area ({CalloutPriorityExtensions.AlarmToDisplay(AlarmLevel)}).");
        }

        public override void Process()
        {
            if (HasArrived && fire.IsOut)
                Complete();
        }

        protected override void OnEnd()
        {
            scene.Dismiss();
        }
    }

    [CalloutInfo("Brush Fire", CalloutProbability.High, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority2)]
    internal class BrushFire : OutdoorFire
    {
        protected override string Title => "Brush Fire";
        protected override string Advisory => "Vegetation fire spreading along the road. Protect nearby structures.";
        protected override int SecondAlarmChance => 30;
        protected override float SpreadSeconds => 25f;
    }

    [CalloutInfo("Trash Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority3)]
    internal class TrashFire : OutdoorFire
    {
        protected override string Title => "Rubbish Fire";
        protected override string Advisory => "Dumpster and trash fire behind a building, no exposures reported.";
        protected override int SecondAlarmChance => 0;
        protected override int MaxAlarmLevel => 1;
        protected override float SpreadSeconds => 60f;

        protected override void DressScene(Scene scene, Vector3 center)
        {
            Model dumpster = "prop_dumpster_01a";
            if (dumpster.IsValid)
                scene.Track(new Object(dumpster, center) { IsPersistent = true });
        }
    }

    [CalloutInfo("Industrial Fire", CalloutProbability.Low, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1)]
    internal class IndustrialFire : OutdoorFire
    {
        // Rough outlines of the industrial areas: the port (Elysian Island / Terminal), Cypress Flats, El Burro oil fields.
        private static readonly (float minX, float minY, float maxX, float maxY)[] IndustrialAreas =
        {
            (-400f, -3300f, 1200f, -2600f),
            (700f, -2500f, 1100f, -1900f),
            (1300f, -2400f, 1800f, -1800f),
        };

        protected override string Title => "Industrial Fire";
        protected override string Advisory => "Fire at an industrial site, ~o~possible hazardous materials~s~. Approach from upwind.";
        protected override bool IsGasFire => true;
        protected override int SecondAlarmChance => 40;

        protected override Vector3? FindLocation()
        {
            var area = IndustrialAreas[MathHelper.GetRandomInteger(IndustrialAreas.Length)];
            var point = new Vector3(MathHelper.GetRandomSingle(area.minX, area.maxX), MathHelper.GetRandomSingle(area.minY, area.maxY), 30f);
            return World.GetNextPositionOnStreet(point);
        }

        protected override void DressScene(Scene scene, Vector3 center)
        {
            scene.SpawnBystander(center.Around2D(15f, 20f));
        }
    }
}
