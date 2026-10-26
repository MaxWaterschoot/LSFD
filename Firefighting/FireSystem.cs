using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Core;
using LSFD.Utils;
using Rage;
using Rage.Native;

namespace LSFD.Firefighting
{
    /// <summary>
    /// Simulates fire incidents on top of the GTA fires. GTA fires go out or move on their own and never grow, so
    /// every incident keeps its own fire points with an intensity. They grow over time, spread to nearby spots,
    /// escalate the alarm when left alone and leave hotspots that rekindle unless overhauled. The GTA fires are
    /// only the flames you see, relit as long as a point still burns.
    /// </summary>
    internal class FireSystem
    {
        // Points only get flames and are only placed on the floor when the player is this close, so the area is streamed in.
        private const float StreamDistance = 250f;

        private const float BaseGrowthPerSecond = 1f / 60f;
        private const float GasGrowthFactor = 1.5f;
        private const uint GrowthPauseAfterWater = 3000;
        private const uint RelightDelay = 2500;
        private const uint VisualCheckInterval = 1000;
        private const uint StatusInterval = 1000;
        private const int SizeHistorySeconds = 10;

        private const float MinPointSpacing = 2f;
        private const float SpreadMinIntensity = 0.6f;
        private const float NewPointIntensity = 0.2f;
        private const float RekindleIntensity = 0.35f;
        private const float RekindleDistance = 4f;
        private const int RekindleMinSeconds = 45;
        private const int RekindleMaxSeconds = 90;

        /// <summary>Water puts out GTA fires at 1 cooling, a fully burning point takes twice as long.</summary>
        private const float WaterCoolingFactor = 0.5f;
        private const float ExtinguisherCoolingPerSecond = 0.25f;
        private const float ExtinguisherReach = 6f;
        private const float ExtinguisherConeAngle = 35f;

        private const float HeatRadius = 4.5f;
        private const float GasHeatRadius = 6f;
        private const float IndoorSmokeMargin = 12f;
        private const float OutdoorSmokeRadius = 5f;

        private static readonly uint ExtinguisherWeapon = Game.GetHashKey("WEAPON_FIREEXTINGUISHER");

        private const TraceFlags FloorTraceFlags = TraceFlags.IntersectWorld;

        private readonly List<TrackedIncident> tracked = new List<TrackedIncident>();

        // GTA fires of this frame, looked up once when needed.
        private Fire[] frameFires;

        public IEnumerable<FireIncident> Incidents => tracked.Select(t => t.Incident);

        /// <summary>The incident went to a higher alarm on its own.</summary>
        public event Action<FireIncident> Escalated;

        public event Action<FireIncident, FireStatus> StatusChanged;

        /// <summary>A knocked down spot burst into flames again.</summary>
        public event Action<FireIncident> Rekindled;

        public FireIncident Start(Vector3 origin, FireIncidentOptions options, int alarmLevel, Callout owner)
        {
            var incident = new FireIncident(origin, options, alarmLevel, owner);
            options = incident.Options;

            incident.Points.Add(new FirePoint(origin, origin, options.InitialIntensity, false));
            for (int i = 1; i < options.InitialPoints; i++)
            {
                Vector3 position = origin.Around2D(MinPointSpacing, Math.Max(MinPointSpacing, Math.Min(4f, options.SpreadRadius)));
                incident.Points.Add(new FirePoint(position, origin, options.InitialIntensity, false));
            }

            incident.Size = incident.PeakSize = options.InitialPoints * options.InitialIntensity;
            tracked.Add(new TrackedIncident(incident) { NextSpreadAt = Game.GameTime + SpreadDelay(incident) });
            return incident;
        }

        public void Process(IEnumerable<Ped> extinguisherUsers)
        {
            frameFires = null;
            if (tracked.Count == 0)
                return;

            uint now = Game.GameTime;
            float delta = Game.FrameTime;
            Vector3 player = Game.LocalPlayer.Character.Position;

            foreach (Ped ped in extinguisherUsers)
                ApplyExtinguisher(ped, delta);

            foreach (TrackedIncident t in tracked.ToList())
            {
                FireIncident incident = t.Incident;
                if (incident.IsRemoved)
                {
                    Cleanup(t);
                    tracked.Remove(t);
                    continue;
                }

                if (incident.IsOut)
                    continue;

                bool streamed = player.DistanceTo(incident.Origin) < StreamDistance + incident.Options.SpreadRadius;
                foreach (FirePoint point in incident.Points)
                    UpdatePoint(t, point, delta, now, streamed);

                if (now >= t.NextSpreadAt)
                {
                    Spread(incident, streamed);
                    t.NextSpreadAt = now + SpreadDelay(incident);
                }

                if (now >= t.NextStatusAt)
                {
                    UpdateStatus(t);
                    t.NextStatusAt = now + StatusInterval;
                }

                UpdateSmokeColumn(t, player);
            }
        }

        /// <summary>Removes every incident, e.g. when LSFD unloads.</summary>
        public void RemoveAll()
        {
            foreach (TrackedIncident t in tracked)
                Cleanup(t);
            tracked.Clear();
        }

        /// <summary>The incident whose fire area <paramref name="position"/> is in or near, or null.</summary>
        public FireIncident FindNearest(Vector3 position, float margin)
        {
            return tracked.Select(t => t.Incident)
                .Where(i => !i.IsOut && i.Origin.DistanceTo(position) <= i.Options.SpreadRadius + margin)
                .OrderBy(i => i.Origin.DistanceTo(position))
                .FirstOrDefault();
        }

        /// <summary>Where firefighters should aim: the nearest flames, or else the nearest hotspot.</summary>
        public Vector3? FindNearestTarget(Vector3 position, float radius)
        {
            var points = HotPoints().Where(p => p.IsPlaced && p.Position.DistanceTo(position) <= radius).ToList();
            FirePoint target = points.Where(p => p.IsBurning).OrderBy(p => p.Position.DistanceTo(position)).FirstOrDefault()
                ?? points.OrderBy(p => p.Position.DistanceTo(position)).FirstOrDefault();
            return target?.Position;
        }

        /// <summary>Burning (true) and smoldering (false) spots near <paramref name="position"/>, for the thermal camera.</summary>
        public IEnumerable<(Vector3 position, bool burning, float heat)> GetHotSpots(Vector3 position, float radius)
        {
            return HotPoints()
                .Where(p => p.IsPlaced && p.Position.DistanceTo(position) <= radius)
                .Select(p => (p.Position, p.IsBurning, p.IsBurning ? p.Intensity : p.Heat));
        }

        /// <summary>Radiant heat at <paramref name="position"/>: about 1 right next to a fully burning spot.</summary>
        public float GetHeat(Vector3 position)
        {
            float heat = 0f;
            foreach (TrackedIncident t in tracked)
            {
                float radius = t.Incident.Options.IsGasFire ? GasHeatRadius : HeatRadius;
                foreach (FirePoint point in t.Incident.Points.Where(p => p.IsBurning && p.IsPlaced))
                {
                    float closeness = 1f - point.Position.DistanceTo(position) / radius;
                    if (closeness > 0f)
                        heat += point.Intensity * closeness * closeness;
                }
            }
            return Math.Min(2f, heat);
        }

        /// <summary>How thick the smoke is at <paramref name="position"/>, from 0 to 1.</summary>
        public float GetSmoke(Vector3 position, bool inInterior)
        {
            float smoke = 0f;
            foreach (FireIncident incident in tracked.Select(t => t.Incident).Where(i => !i.IsOut))
            {
                if (incident.Options.IsIndoor)
                {
                    // The whole interior around the fire fills with smoke; it lingers a bit after knockdown.
                    bool inside = inInterior
                        && position.DistanceTo2D(incident.Origin) <= incident.Options.SpreadRadius + IndoorSmokeMargin
                        && Math.Abs(position.Z - incident.Origin.Z) < 6f;
                    if (!inside)
                        continue;

                    float density = incident.Status == FireStatus.Knockdown
                        ? 0.2f
                        : 0.3f + incident.Size / Math.Max(1f, incident.Options.PointsPerAlarm);
                    smoke = Math.Max(smoke, Math.Min(1f, density));
                }
                else
                {
                    foreach (FirePoint point in incident.Points.Where(p => p.IsBurning && p.IsPlaced))
                    {
                        float closeness = 1f - point.Position.DistanceTo(position) / OutdoorSmokeRadius;
                        if (closeness > 0f)
                            smoke = Math.Max(smoke, point.Intensity * closeness * 0.8f);
                    }
                }
            }
            return smoke;
        }

        /// <summary>Cools every spot <paramref name="isHit"/> returns true for. Called every frame by the hose stream.</summary>
        public void ApplyWater(Func<Vector3, bool> isHit, float cooling)
        {
            foreach (FirePoint point in HotPoints().Where(p => p.IsPlaced && isHit(p.Position)).ToList())
                Cool(point, cooling * WaterCoolingFactor);
        }

        /// <summary>Cools every spot within <paramref name="radius"/>, e.g. for AI firefighters with an extinguisher.</summary>
        public void ApplyCooling(Vector3 position, float radius, float cooling)
        {
            foreach (FirePoint point in HotPoints().Where(p => p.IsPlaced && p.Position.DistanceTo(position) <= radius).ToList())
                Cool(point, cooling);
        }

        private void ApplyExtinguisher(Ped ped, float delta)
        {
            if (!ped.Exists() || !ped.IsShooting
                || NativeFunction.CallByName<uint>("GET_SELECTED_PED_WEAPON", ped) != ExtinguisherWeapon)
                return;

            Vector3 origin = ped.Position;
            Vector3 forward = ped.Direction;
            foreach (FirePoint point in HotPoints().Where(p => p.IsPlaced).ToList())
            {
                Vector3 toPoint = point.Position - origin;
                float distance = toPoint.Length();
                if (distance > ExtinguisherReach)
                    continue;

                float angle = distance < 1f ? 0f
                    : MathHelper.ConvertRadiansToDegrees((float)Math.Acos(Vector3.Dot(Vector3.Normalize(toPoint), forward)));
                if (angle <= ExtinguisherConeAngle)
                    Cool(point, ExtinguisherCoolingPerSecond * delta);
            }
        }

        private IEnumerable<FirePoint> HotPoints()
        {
            return tracked.Where(t => !t.Incident.IsOut).SelectMany(t => t.Incident.Points).Where(p => p.IsHot);
        }

        private void Cool(FirePoint point, float amount)
        {
            point.LastWaterAt = Game.GameTime;

            if (point.State == FirePointState.Burning)
            {
                point.Intensity -= amount;
                if (point.Intensity <= 0f)
                    KnockDown(point);
            }
            else if (point.State == FirePointState.Smoldering)
            {
                point.Heat -= amount;
                if (point.Heat <= 0f)
                    PutOut(point);
            }
        }

        private void UpdatePoint(TrackedIncident t, FirePoint point, float delta, uint now, bool streamed)
        {
            FireIncident incident = t.Incident;

            if (streamed && !point.IsPlaced && now >= point.NextVisualCheck)
                Place(incident, point);

            switch (point.State)
            {
                case FirePointState.Burning:
                    if (now - point.LastWaterAt > GrowthPauseAfterWater)
                        point.Intensity = Math.Min(1f, point.Intensity + delta * GrowthRate(incident));

                    if (streamed)
                        KeepFlamesLit(incident, point, now);
                    else
                        RemoveFlames(point);
                    break;

                case FirePointState.Smoldering:
                    if (now >= point.RekindleCheckAt)
                    {
                        if (MathHelper.GetRandomSingle(0f, 1f) < Settings.RekindleChance)
                        {
                            Rekindle(point);
                            Rekindled?.Invoke(incident);
                            break;
                        }
                        point.RekindleCheckAt = now + RekindleDelay();
                    }

                    if (streamed && point.IsPlaced && !point.HotspotSmoke.IsActive)
                        point.HotspotSmoke.Start(FireEffects.HotspotSmokeEffect, point.Position, Rotator.Zero, FireEffects.HotspotSmokeScale);
                    else if (!streamed)
                        point.HotspotSmoke.Stop();
                    break;
            }
        }

        private void KeepFlamesLit(FireIncident incident, FirePoint point, uint now)
        {
            if (!point.IsPlaced || now < point.NextVisualCheck)
                return;
            point.NextVisualCheck = now + VisualCheckInterval;

            // Give the water a moment: the hose deletes the GTA flames before the point itself is out.
            if (now - point.LastWaterAt < RelightDelay || HasFlamesNear(point.Position))
                return;

            RemoveFlames(point);
            int children = 1 + (int)(point.Intensity * 8f);
            point.ScriptFire = NativeFunction.CallByName<int>("START_SCRIPT_FIRE",
                point.Position.X, point.Position.Y, point.Position.Z, children, incident.Options.IsGasFire);
        }

        private bool HasFlamesNear(Vector3 position)
        {
            if (frameFires == null)
                frameFires = World.GetAllFires();
            return frameFires.Any(f => f.IsValid() && f.Position.DistanceTo(position) <= 1.5f);
        }

        private static void RemoveFlames(FirePoint point)
        {
            if (point.ScriptFire == 0)
                return;

            NativeFunction.CallByName<uint>("REMOVE_SCRIPT_FIRE", point.ScriptFire);
            point.ScriptFire = 0;
        }

        private void KnockDown(FirePoint point)
        {
            point.State = FirePointState.Smoldering;
            point.Intensity = 0f;
            point.Heat = 1f;
            point.RekindleCheckAt = Game.GameTime + RekindleDelay();
            RemoveFlames(point);
            NativeFunction.CallByName<uint>("STOP_FIRE_IN_RANGE", point.Position.X, point.Position.Y, point.Position.Z, 1.2f);
        }

        private static void PutOut(FirePoint point)
        {
            point.State = FirePointState.Out;
            point.Heat = 0f;
            point.HotspotSmoke.Stop();
        }

        private static void Rekindle(FirePoint point)
        {
            point.State = FirePointState.Burning;
            point.Intensity = RekindleIntensity;
            point.Heat = 0f;
            point.NextVisualCheck = 0;
            point.LastWaterAt = 0;
            point.HotspotSmoke.Stop();
        }

        private void Spread(FireIncident incident, bool streamed)
        {
            var sources = incident.Points.Where(p => p.IsBurning && p.Intensity >= SpreadMinIntensity).ToList();
            if (sources.Count == 0)
                return;

            FirePoint from = sources[MathHelper.GetRandomInteger(sources.Count)];

            // Knocked down spots next to open flames light up again first.
            FirePoint smoldering = incident.Points.FirstOrDefault(p => p.State == FirePointState.Smoldering && p.Position.DistanceTo(from.Position) <= RekindleDistance);
            if (smoldering != null)
            {
                Rekindle(smoldering);
                Rekindled?.Invoke(incident);
                return;
            }

            int maxPoints = incident.Options.PointsPerAlarm * (incident.AlarmLevel + 1);
            if (incident.BurningPoints >= maxPoints)
                return;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 candidate = from.Position.Around2D(MinPointSpacing + 0.5f, RekindleDistance);
                if (candidate.DistanceTo2D(incident.Origin) > incident.Options.SpreadRadius
                    || incident.Points.Any(p => p.Position.DistanceTo2D(candidate) < MinPointSpacing))
                    continue;

                if (!streamed)
                {
                    incident.Points.Add(new FirePoint(candidate, from.Position, NewPointIntensity, false));
                    return;
                }

                Vector3? floor = FindFloor(candidate, incident.Options.IsIndoor);
                if (floor.HasValue && (!incident.Options.IsIndoor || HasLineOfSight(from.Position, floor.Value)))
                {
                    incident.Points.Add(new FirePoint(floor.Value, from.Position, NewPointIntensity, true));
                    return;
                }
            }
        }

        /// <summary>Puts a point that was made while the area wasn't streamed in on the floor, not inside a wall.</summary>
        private void Place(FireIncident incident, FirePoint point)
        {
            point.NextVisualCheck = Game.GameTime + VisualCheckInterval;
            bool indoor = incident.Options.IsIndoor;

            Vector3? floor = FindFloor(point.Position, indoor);
            if (floor.HasValue && (!indoor || HasLineOfSight(point.SpreadFrom, floor.Value)))
            {
                point.Position = floor.Value;
                point.IsPlaced = true;
                return;
            }

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 candidate = point.SpreadFrom.Around2D(0.5f, MinPointSpacing + 1f);
                floor = FindFloor(candidate, indoor);
                if (floor.HasValue && (!indoor || HasLineOfSight(point.SpreadFrom, floor.Value)))
                {
                    point.Position = floor.Value;
                    point.IsPlaced = true;
                    return;
                }
            }

            // Nothing better found: burn where it spread from.
            point.Position = FindFloor(point.SpreadFrom, indoor) ?? point.SpreadFrom;
            point.IsPlaced = true;
        }

        private static Vector3? FindFloor(Vector3 position, bool indoor)
        {
            Vector3 from = position + new Vector3(0f, 0f, indoor ? 0.8f : 2f);
            Vector3 to = position - new Vector3(0f, 0f, indoor ? 3f : 8f);
            HitResult hit = World.TraceLine(from, to, FloorTraceFlags);
            return hit.Hit ? hit.HitPosition : (Vector3?)null;
        }

        private static bool HasLineOfSight(Vector3 a, Vector3 b)
        {
            Vector3 up = new Vector3(0f, 0f, 0.6f);
            return !World.TraceLine(a + up, b + up, FloorTraceFlags).Hit;
        }

        private void UpdateStatus(TrackedIncident t)
        {
            FireIncident incident = t.Incident;
            var burning = incident.Points.Where(p => p.IsBurning).ToList();

            float size = burning.Sum(p => p.Intensity);
            incident.Size = size;
            incident.PeakSize = Math.Max(incident.PeakSize, size);

            t.SizeHistory.Enqueue(size);
            float previous = t.SizeHistory.Count > SizeHistorySeconds ? t.SizeHistory.Dequeue() : t.SizeHistory.Peek();

            FireStatus status;
            if (burning.Count == 0)
            {
                status = incident.Points.Any(p => p.State == FirePointState.Smoldering) ? FireStatus.Knockdown : FireStatus.Out;
            }
            else
            {
                bool shrinking = size < previous - 0.05f;
                bool growing = size > previous + 0.05f;
                bool wasUnderControl = incident.Status == FireStatus.UnderControl;

                if ((shrinking && size <= incident.PeakSize * 0.5f) || (wasUnderControl && !growing && size <= incident.PeakSize * 0.75f))
                    status = FireStatus.UnderControl;
                else
                    status = growing ? FireStatus.Growing : FireStatus.Burning;
            }

            if (status != incident.Status)
            {
                incident.Status = status;
                if (status == FireStatus.Knockdown || status == FireStatus.Out)
                    ClearLeftoverFlames(incident);
                if (status == FireStatus.Out)
                    Cleanup(t);

                incident.RaiseStatusChanged();
                StatusChanged?.Invoke(incident, status);
            }

            CheckEscalation(incident);
        }

        private void CheckEscalation(FireIncident incident)
        {
            if (!Settings.FireEscalation || incident.AlarmLevel >= incident.Options.MaxAlarmLevel)
                return;
            if (incident.Status != FireStatus.Growing && incident.Status != FireStatus.Burning)
                return;
            if (incident.Size < incident.Options.PointsPerAlarm * incident.AlarmLevel * 0.75f)
                return;

            incident.AlarmLevel++;
            incident.Owner?.RaiseAlarmLevel(incident.AlarmLevel);
            Escalated?.Invoke(incident);
        }

        private static void UpdateSmokeColumn(TrackedIncident t, Vector3 player)
        {
            FireIncident incident = t.Incident;
            LoopedParticle column = t.SmokeColumn;

            if (!Settings.SmokeColumns || incident.IsOut || player.DistanceTo(incident.Origin) > FireEffects.SmokeColumnDrawDistance)
            {
                column.Stop();
                return;
            }

            // Steps of 0.25, so the effect only restarts when the fire really changed.
            float fraction = Math.Min(1f, incident.Size / Math.Max(1f, incident.Options.PointsPerAlarm * incident.AlarmLevel));
            float scale = incident.Status == FireStatus.Knockdown ? 0.5f : (float)Math.Round((0.5f + 1.5f * fraction) * 4f) / 4f;

            // Black smoke while it burns freely, grey once it is being controlled, white steam after knockdown.
            float shade = incident.Status == FireStatus.Knockdown ? 0.9f : incident.Status == FireStatus.UnderControl ? 0.45f : 0.12f;

            if (!column.IsActive || Math.Abs(column.Scale - scale) > 0.01f)
            {
                Vector3 position = incident.Origin + new Vector3(0f, 0f, incident.Options.SmokeColumnHeight);
                column.Start(FireEffects.SmokeColumnEffect, position, Rotator.Zero, scale);
                column.SetDrawDistance(FireEffects.SmokeColumnDrawDistance);
                t.ColumnShade = -1f;
            }

            if (column.IsActive && Math.Abs(t.ColumnShade - shade) > 0.01f)
            {
                column.SetColor(shade, shade, shade);
                t.ColumnShade = shade;
            }
        }

        // GTA spreads its own little fires around the script fires; they must not outlive the incident.
        private static void ClearLeftoverFlames(FireIncident incident)
        {
            Vector3 origin = incident.Origin;
            NativeFunction.CallByName<uint>("STOP_FIRE_IN_RANGE", origin.X, origin.Y, origin.Z, incident.Options.SpreadRadius + 6f);
        }

        private static void Cleanup(TrackedIncident t)
        {
            foreach (FirePoint point in t.Incident.Points)
            {
                RemoveFlames(point);
                point.HotspotSmoke.Stop();
            }

            t.SmokeColumn.Stop();
            ClearLeftoverFlames(t.Incident);
        }

        private static float GrowthRate(FireIncident incident)
        {
            return BaseGrowthPerSecond * Settings.FireGrowthMultiplier * (incident.Options.IsGasFire ? GasGrowthFactor : 1f);
        }

        private static uint SpreadDelay(FireIncident incident)
        {
            float seconds = incident.Options.SpreadSeconds * MathHelper.GetRandomSingle(0.8f, 1.2f)
                / Math.Max(0.1f, Settings.FireGrowthMultiplier) / (incident.Options.IsGasFire ? GasGrowthFactor : 1f);
            return (uint)(seconds * 1000f);
        }

        private static uint RekindleDelay()
        {
            return (uint)(MathHelper.GetRandomInteger(RekindleMinSeconds, RekindleMaxSeconds + 1) * 1000);
        }

        private sealed class TrackedIncident
        {
            public TrackedIncident(FireIncident incident)
            {
                Incident = incident;
            }

            public FireIncident Incident { get; }
            public uint NextSpreadAt { get; set; }
            public uint NextStatusAt { get; set; }
            public Queue<float> SizeHistory { get; } = new Queue<float>();
            public LoopedParticle SmokeColumn { get; } = new LoopedParticle(FireEffects.SmokeColumnAsset);
            public float ColumnShade { get; set; } = -1f;
        }
    }
}
