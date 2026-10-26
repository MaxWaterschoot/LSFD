using System;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.UI;
using Rage;
using Rage.Native;

namespace LSFD.Firefighting
{
    /// <summary>
    /// Positive pressure ventilation: a fan set up in the doorway of a burning building, blowing in. It pushes the smoke
    /// out, so you can see and breathe better inside, but the fresh air also feeds the fire.
    /// </summary>
    internal class Ventilation : IInteractionProvider
    {
        private const float Reach = 1.6f;
        private const float MaxDistanceFromFire = 15f;
        private const float MaxAimAngle = 70f;
        private const string FallbackFanModel = "prop_generator_03b";

        private readonly FireSystem fire;
        private Rage.Object fan;

        public Ventilation(FireSystem fire)
        {
            this.fire = fire;
        }

        public FireApparatus Source { get; private set; }
        public bool IsCarried { get; private set; }
        public bool IsPlaced => fan.Exists() && !IsCarried;

        /// <summary>Takes the fan out of the compartment; set it up with the interact key.</summary>
        public void Take(FireApparatus source)
        {
            Stow();
            if (!SpawnFan())
                return;

            Source = source;
            source.SetTaken(EquipmentType.PPVFan, true);
            Carry();
            Notify.Info($"Carrying the ~b~PPV fan~s~. Press ~b~{Settings.InteractKey}~s~ in the doorway, facing in, to set it up.");
        }

        /// <summary>Puts the fan back in the compartment.</summary>
        public void Stow()
        {
            if (IsCarried)
                Game.LocalPlayer.Character.Tasks.Clear();
            if (fan.Exists())
                fan.Delete();
            fan = null;
            IsCarried = false;

            Source?.SetTaken(EquipmentType.PPVFan, false);
            Source = null;
            UpdateIncidents();
        }

        public Interaction GetInteraction()
        {
            Ped player = Game.LocalPlayer.Character;
            if (!fan.Exists() || player.IsInAnyVehicle(false))
                return null;

            if (IsCarried)
                return new Interaction("set up the ~b~PPV fan~s~ here", Place);
            if (fan.DistanceTo(player.Position) <= Reach)
                return new Interaction("pick up the ~b~PPV fan", Carry);
            return null;
        }

        private bool SpawnFan()
        {
            Model model = Settings.FanModel;
            if (!model.IsValid)
            {
                Game.LogTrivial($"LSFD: fan model '{Settings.FanModel}' does not exist, using {FallbackFanModel}.");
                model = FallbackFanModel;
            }
            if (!model.IsValid)
                return false;

            model.LoadAndWait();
            fan = new Rage.Object(model, Game.LocalPlayer.Character.Position) { IsPersistent = true };
            model.Dismiss();
            return true;
        }

        private void Carry()
        {
            Ped player = Game.LocalPlayer.Character;
            NativeFunction.CallByName<uint>("FREEZE_ENTITY_POSITION", fan, false);
            fan.AttachTo(player, 0, new Vector3(0f, 0.5f, -0.4f), new Rotator(0f, 0f, Settings.FanHeading));
            player.Tasks.PlayAnimation("anim@heists@box_carry@", "idle", 4f, AnimationFlags.Loop | AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);
            IsCarried = true;
            UpdateIncidents();
        }

        /// <summary>Sets the fan down in front of the player, blowing the way they face.</summary>
        private void Place()
        {
            Ped player = Game.LocalPlayer.Character;
            fan.Detach();
            player.Tasks.Clear();

            Vector3 position = player.GetOffsetPositionFront(0.8f);
            float? ground = World.GetGroundZ(position + new Vector3(0f, 0f, 1f), false, false);
            fan.Position = new Vector3(position.X, position.Y, ground ?? position.Z - 1f);
            fan.Heading = player.Heading + Settings.FanHeading;
            NativeFunction.CallByName<uint>("PLACE_OBJECT_ON_GROUND_PROPERLY", fan);
            NativeFunction.CallByName<uint>("FREEZE_ENTITY_POSITION", fan, true);
            IsCarried = false;

            UpdateIncidents();
            bool helping = fire.Incidents.Any(i => i.IsVentilated);
            Game.DisplaySubtitle(helping
                ? "~b~PPV fan~s~ running: the smoke is being pushed out. Mind the fire, it gets more air too."
                : "~b~PPV fan~s~ running, but it isn't blowing into a burning building.", 4000);
        }

        public void Process()
        {
            Ped player = Game.LocalPlayer.Character;
            if (Source != null && (!fan.Exists() || !Source.IsValid || (IsCarried && (player.IsDead || player.IsInAnyVehicle(true)))))
                Stow();
            UpdateIncidents();
        }

        private void UpdateIncidents()
        {
            foreach (FireIncident incident in fire.Incidents.Where(i => i.Options.IsIndoor))
                incident.IsVentilated = IsPlaced && IsBlowingInto(incident);
        }

        private bool IsBlowingInto(FireIncident incident)
        {
            Vector3 toFire = incident.Origin - fan.Position;
            if (toFire.Length() > incident.Options.SpreadRadius + MaxDistanceFromFire)
                return false;

            // The fan must stand outside, blowing in.
            if (NativeFunction.CallByName<int>("GET_INTERIOR_AT_COORDS", fan.Position.X, fan.Position.Y, fan.Position.Z) != 0)
                return false;

            Vector3 forward = fan.ForwardVector;
            toFire.Z = 0f;
            forward.Z = 0f;
            float angle = MathHelper.ConvertRadiansToDegrees((float)Math.Acos(Vector3.Dot(Vector3.Normalize(toFire), Vector3.Normalize(forward))));
            return angle <= MaxAimAngle;
        }
    }
}
