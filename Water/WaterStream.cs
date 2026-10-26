using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.Utils;
using Rage;
using Rage.Native;

namespace LSFD.Water
{
    /// <summary>Simulates the water coming out of a nozzle: its arc, what it hits, the effect and putting out fires.</summary>
    internal class WaterStream
    {
        private const float Gravity = 9.81f;
        private const float SimulationStep = 0.04f;
        private const int MaxSimulationSteps = 60;
        private const float PathHitRadius = 0.8f;
        private const uint ImpactPushInterval = 150;

        // Hydrant explosions push peds and objects around without damaging them.
        private const int WaterExplosionType = 13;

        private const TraceFlags StreamTraceFlags =
            TraceFlags.IntersectWorld | TraceFlags.IntersectVehicles | TraceFlags.IntersectObjects | TraceFlags.IntersectPeds;

        private readonly LoopedParticle particle = new LoopedParticle("core");
        private readonly Dictionary<(int, int, int), float> fireCooling = new Dictionary<(int, int, int), float>();
        private readonly Dictionary<uint, float> entityCooling = new Dictionary<uint, float>();
        private readonly List<Vector3> path = new List<Vector3>();
        private uint lastImpactPush;

        /// <summary>Where the stream lands, while spraying.</summary>
        public Vector3? Impact { get; private set; }

        public void Spray(Ped shooter, Vector3 nozzlePosition, Vector3 direction, Nozzle nozzle)
        {
            Vector3 impact = SimulatePath(shooter, nozzlePosition, direction, nozzle);
            Impact = impact;

            UpdateEffect(nozzlePosition, direction, nozzle);
            CoolFires(nozzlePosition, direction, impact, nozzle);
            CoolBurningEntities(impact, nozzle);

            if (nozzle.IsStraight && nozzle.EffectivePressure >= 0.5f && Game.GameTime - lastImpactPush > ImpactPushInterval)
            {
                World.SpawnExplosion(impact, WaterExplosionType, 0f, false, true, 0f);
                lastImpactPush = Game.GameTime;
            }
        }

        public void Stop()
        {
            particle.Stop();
            fireCooling.Clear();
            entityCooling.Clear();
            Impact = null;
        }

        private Vector3 SimulatePath(Ped shooter, Vector3 origin, Vector3 direction, Nozzle nozzle)
        {
            path.Clear();
            path.Add(origin);

            Vector3 position = origin;
            Vector3 velocity = direction * nozzle.LaunchSpeed;
            // A fog pattern breaks up quickly, so it falls short much sooner than a straight stream.
            float drag = nozzle.IsStraight ? 0.05f : 1.6f;

            for (int i = 0; i < MaxSimulationSteps; i++)
            {
                velocity -= velocity * drag * SimulationStep;
                velocity.Z -= Gravity * SimulationStep;
                Vector3 next = position + velocity * SimulationStep;

                HitResult hit = World.TraceLine(position, next, StreamTraceFlags, shooter);
                if (hit.Hit)
                {
                    path.Add(hit.HitPosition);
                    return hit.HitPosition;
                }

                path.Add(next);
                position = next;

                if (velocity.Length() < 1f)
                    break;
            }

            return position;
        }

        private void UpdateEffect(Vector3 nozzlePosition, Vector3 direction, Nozzle nozzle)
        {
            Rotator rotation = direction.ToRotator();

            if (!particle.IsActive || particle.EffectName != nozzle.EffectName || Math.Abs(particle.Scale - nozzle.EffectScale) > 0.01f)
                particle.Start(nozzle.EffectName, nozzlePosition, rotation, nozzle.EffectScale);
            else
                particle.Move(nozzlePosition, rotation);
        }

        private void CoolFires(Vector3 origin, Vector3 direction, Vector3 impact, Nozzle nozzle)
        {
            float cooling = nozzle.CoolingPerSecond * Game.FrameTime;

            foreach (Fire fire in World.GetAllFires())
            {
                if (!fire.IsValid() || !IsInStream(fire.Position, origin, direction, impact, nozzle))
                    continue;

                var key = ((int)(fire.Position.X * 2f), (int)(fire.Position.Y * 2f), (int)(fire.Position.Z * 2f));
                fireCooling.TryGetValue(key, out float total);
                total += cooling;

                if (total >= 1f)
                {
                    fire.Delete();
                    fireCooling.Remove(key);
                }
                else
                {
                    fireCooling[key] = total;
                }
            }
        }

        private void CoolBurningEntities(Vector3 impact, Nozzle nozzle)
        {
            float cooling = nozzle.CoolingPerSecond * Game.FrameTime;
            var flags = GetEntitiesFlags.ConsiderAllPeds | GetEntitiesFlags.ConsiderAllVehicles | GetEntitiesFlags.ConsiderAllObjects;

            foreach (Entity entity in World.GetEntities(impact, nozzle.ImpactRadius + 1f, flags).Where(e => e.Exists() && e.IsOnFire))
            {
                uint handle = entity.Handle;
                entityCooling.TryGetValue(handle, out float total);
                total += cooling;

                if (total >= 1f)
                {
                    NativeFunction.CallByName<uint>("STOP_ENTITY_FIRE", entity);
                    entityCooling.Remove(handle);
                }
                else
                {
                    entityCooling[handle] = total;
                }
            }
        }

        private bool IsInStream(Vector3 position, Vector3 origin, Vector3 direction, Vector3 impact, Nozzle nozzle)
        {
            if (position.DistanceTo(impact) <= nozzle.ImpactRadius)
                return true;

            if (nozzle.IsStraight)
            {
                for (int i = 1; i < path.Count; i++)
                {
                    if (DistanceToSegment(position, path[i - 1], path[i]) <= PathHitRadius)
                        return true;
                }
                return false;
            }

            // Fog: everything inside the cone up to where the spray falls down.
            Vector3 toTarget = position - origin;
            float distance = toTarget.Length();
            if (distance > origin.DistanceTo(impact) + nozzle.ImpactRadius)
                return false;

            float angle = MathHelper.ConvertRadiansToDegrees((float)Math.Acos(Vector3.Dot(Vector3.Normalize(toTarget), direction)));
            return angle <= nozzle.ConeAngle;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSquared = ab.LengthSquared();
            if (lengthSquared < 0.0001f)
                return point.DistanceTo(a);

            float t = Math.Max(0f, Math.Min(1f, Vector3.Dot(point - a, ab) / lengthSquared));
            return point.DistanceTo(a + ab * t);
        }
    }
}
