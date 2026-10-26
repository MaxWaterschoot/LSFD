using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.VehicleRescue;
using Rage;
using Rage.Native;

namespace LSFD.LivingWorld
{
    /// <summary>
    /// Keeps AI traffic from driving through a scene: it slows down around the call and around parked apparatus with
    /// their lights on, and stops at the traffic cones the player places.
    /// </summary>
    internal class TrafficControl : IInteractionProvider
    {
        private const float SceneSpeed = 4f;
        private const float MaxSceneZoneRadius = 60f;
        private const float BlockingSpeed = 2f;
        private const uint StoppedBeforeBlocking = 3000;
        private const uint ScanInterval = 500;
        private const string ConeModel = "prop_roadcone02a";
        private const float ConeZoneRadius = 5f;
        private const float ConePickupDistance = 1.6f;
        private const int MaxCones = 12;

        // Cones the player drove away from are "picked up by a colleague".
        private const float ForgottenConeDistance = 300f;

        private class Blocker
        {
            public uint StoppedSince;
            public int? Zone;
        }

        private class Cone
        {
            public Object Prop;
            public int Zone;
        }

        private readonly CalloutManager callouts;
        private readonly ApparatusManager apparatus;
        private readonly ToolManager tools;
        private readonly Dictionary<FireApparatus, Blocker> blockers = new Dictionary<FireApparatus, Blocker>();
        private readonly List<Cone> cones = new List<Cone>();

        private Callout zonedCallout;
        private int? sceneZone;
        private uint lastScan;

        public TrafficControl(CalloutManager callouts, ApparatusManager apparatus, ToolManager tools)
        {
            this.callouts = callouts;
            this.apparatus = apparatus;
            this.tools = tools;
        }

        public void Process(bool onDuty)
        {
            if (!onDuty || !Settings.TrafficControl)
            {
                Clear();
                return;
            }

            UpdateSceneZone();

            if (Game.GameTime - lastScan < ScanInterval)
                return;
            lastScan = Game.GameTime;

            UpdateBlockers();
            PickUpForgottenCones();
        }

        public Interaction GetInteraction()
        {
            if (!Settings.TrafficControl || !tools.IsHolding(EquipmentType.TrafficCones))
                return null;

            Ped player = Game.LocalPlayer.Character;
            Cone near = cones.FirstOrDefault(c => c.Prop.Exists() && c.Prop.DistanceTo(player.Position) <= ConePickupDistance);
            if (near != null)
                return new Interaction("pick up the ~y~traffic cone", () => Remove(near));

            if (cones.Count >= MaxCones)
                return Interaction.Hint($"All {MaxCones} ~y~traffic cones~s~ are out. Pick one up to place it elsewhere.");

            ToolDefinition tool = tools.Held;
            return new Interaction("place a ~y~traffic cone~s~ (traffic stops here)", () => Place(player.GetOffsetPositionFront(0.9f)),
                tool.UseSeconds, "PLACING")
            {
                AnimationDictionary = tool.AnimDictionary,
                AnimationName = tool.AnimName,
            };
        }

        public void Clear()
        {
            RemoveSceneZone();
            zonedCallout = null;

            foreach (Blocker blocker in blockers.Values)
                Remove(blocker);
            blockers.Clear();

            foreach (Cone cone in cones.ToList())
                Remove(cone);
        }

        // Traffic crawls past the whole scene once the player is there.
        private void UpdateSceneZone()
        {
            Callout current = callouts.Current;
            Callout wanted = current != null && current.HasArrived && current.State == CalloutState.Accepted ? current : null;
            if (wanted == zonedCallout)
                return;

            RemoveSceneZone();
            zonedCallout = wanted;
            if (wanted != null)
                sceneZone = SpeedZone.Add(wanted.CalloutPosition, System.Math.Min(wanted.CalloutAreaRadius + 10f, MaxSceneZoneRadius), SceneSpeed);
        }

        private void RemoveSceneZone()
        {
            if (sceneZone.HasValue)
                SpeedZone.Remove(sceneZone.Value);
            sceneZone = null;
        }

        // An apparatus parked with its lights on blocks its lane; traffic slows down to get around it.
        private void UpdateBlockers()
        {
            foreach (FireApparatus unit in apparatus.All)
            {
                bool blocking = unit.IsValid && unit.Vehicle.IsSirenOn && unit.Vehicle.Speed < 0.5f;
                blockers.TryGetValue(unit, out Blocker blocker);

                if (!blocking)
                {
                    if (blocker != null)
                    {
                        Remove(blocker);
                        blockers.Remove(unit);
                    }
                    continue;
                }

                if (blocker == null)
                    blockers[unit] = blocker = new Blocker { StoppedSince = Game.GameTime };

                if (!blocker.Zone.HasValue && Game.GameTime - blocker.StoppedSince >= StoppedBeforeBlocking)
                    blocker.Zone = SpeedZone.Add(unit.Vehicle.Position, unit.Vehicle.Model.Dimensions.Y / 2f + 4f, BlockingSpeed);
            }

            foreach (FireApparatus gone in blockers.Keys.Where(u => !apparatus.All.Contains(u)).ToList())
            {
                Remove(blockers[gone]);
                blockers.Remove(gone);
            }
        }

        private void Place(Vector3 position)
        {
            Model model = ConeModel;
            model.LoadAndWait();
            var prop = new Object(model, position) { IsPersistent = true };
            NativeFunction.CallByName<uint>("PLACE_OBJECT_ON_GROUND_PROPERLY", prop);
            model.Dismiss();

            cones.Add(new Cone { Prop = prop, Zone = SpeedZone.Add(position, ConeZoneRadius, 0f) });
        }

        private void PickUpForgottenCones()
        {
            Vector3 player = Game.LocalPlayer.Character.Position;
            foreach (Cone cone in cones.Where(c => !c.Prop.Exists() || c.Prop.DistanceTo(player) > ForgottenConeDistance).ToList())
                Remove(cone);
        }

        private void Remove(Cone cone)
        {
            SpeedZone.Remove(cone.Zone);
            if (cone.Prop.Exists())
                cone.Prop.Delete();
            cones.Remove(cone);
        }

        private static void Remove(Blocker blocker)
        {
            if (blocker.Zone.HasValue)
                SpeedZone.Remove(blocker.Zone.Value);
            blocker.Zone = null;
        }
    }
}
