using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Firefighting;
using LSFD.UI;
using LSFD.Utils;
using Rage;
using Rage.Native;

namespace LSFD.Water
{
    /// <summary>The attack hose: taking it from an apparatus, aiming and adjusting the nozzle, and spraying.</summary>
    internal class HoseManager
    {
        // The fire extinguisher gives the player a fitting aiming pose, so it doubles as the nozzle.
        private const string NozzleWeapon = "WEAPON_FIREEXTINGUISHER";
        private const float LengthWarningMargin = 4f;
        private const float LengthSnapMargin = 2f;

        private static readonly GameControl[] BlockedControls =
        {
            GameControl.Attack, GameControl.Attack2, GameControl.SelectWeapon,
            GameControl.WeaponWheelNext, GameControl.WeaponWheelPrev, GameControl.SelectNextWeapon, GameControl.SelectPrevWeapon,
        };

        private readonly WaterStream stream;
        private HoseRope rope;
        private bool gaveNozzleWeapon;

        public HoseManager(FireSystem fire)
        {
            stream = new WaterStream(fire);
        }

        public Nozzle Nozzle { get; } = new Nozzle();

        /// <summary>The apparatus the hose in the player's hands is connected to, or null.</summary>
        public FireApparatus Source { get; private set; }

        public bool IsHolding => Source != null;
        public bool IsSpraying { get; private set; }
        public bool IsTankEmpty { get; private set; }

        public void TakeHose(FireApparatus source)
        {
            if (IsHolding)
                StowHose();

            Ped player = Game.LocalPlayer.Character;
            uint nozzleHash = Game.GetHashKey(NozzleWeapon);
            gaveNozzleWeapon = !NativeFunction.CallByName<bool>("HAS_PED_GOT_WEAPON", player, nozzleHash, false);
            if (gaveNozzleWeapon)
                player.Inventory.GiveNewWeapon(NozzleWeapon, 1, true);
            NativeFunction.CallByName<uint>("SET_CURRENT_PED_WEAPON", player, nozzleHash, true);

            Source = source;
            source.SetTaken(EquipmentType.AttackHose, true);
        }

        public void StowHose()
        {
            if (!IsHolding)
                return;

            StopSpraying();
            rope?.Delete();
            rope = null;

            Ped player = Game.LocalPlayer.Character;
            if (gaveNozzleWeapon && player.Exists())
                NativeFunction.CallByName<uint>("REMOVE_WEAPON_FROM_PED", player, Game.GetHashKey(NozzleWeapon));
            gaveNozzleWeapon = false;

            Source.SetTaken(EquipmentType.AttackHose, false);
            Source = null;
        }

        public void Process()
        {
            if (!IsHolding)
                return;

            Ped player = Game.LocalPlayer.Character;
            if (!Source.IsValid || !player.Exists() || player.IsDead || player.IsInAnyVehicle(true))
            {
                StowHose();
                return;
            }

            if (!CheckHoseLength(player))
                return;

            ProcessRope(player);

            foreach (GameControl control in BlockedControls)
                Game.DisableControlAction(0, control, true);

            ProcessNozzleControls();

            if (NativeFunction.CallByName<bool>("IS_DISABLED_CONTROL_PRESSED", 0, (int)GameControl.Attack))
                Spray(player);
            else
                StopSpraying();
        }

        // The hose ends at the nozzle in the player's right hand. The nozzle weapon only exists once it is
        // equipped, so the rope is made as soon as it does (or on the player after a moment as a fallback).
        private void ProcessRope(Ped player)
        {
            Object nozzle = player.Inventory.EquippedWeaponObject;
            if (nozzle.Exists() && Settings.HideNozzleWeapon)
                NativeFunction.CallByName<uint>("SET_ENTITY_VISIBLE", nozzle, false, false);

            if (rope == null)
            {
                Entity end = nozzle.Exists() ? (Entity)nozzle : player;
                rope = new HoseRope(Source.Vehicle, end, Source.Definition.HoseLength);
            }

            rope.Process(Source.RearPosition, GetNozzlePosition(player));
        }

        private bool CheckHoseLength(Ped player)
        {
            float distance = player.DistanceTo(Source.RearPosition);
            float length = Source.Definition.HoseLength;

            if (distance > length + LengthSnapMargin)
            {
                Notify.Warning("The hose was pulled out of your hands, it doesn't reach any further.");
                StowHose();
                return false;
            }

            if (distance > length - LengthWarningMargin)
                Game.DisplaySubtitle("~o~The hose is almost fully stretched out.", 100);

            return true;
        }

        private void ProcessNozzleControls()
        {
            if (Game.IsKeyDown(Settings.NozzlePatternKey))
                Nozzle.TogglePattern();

            if (Game.IsKeyDown(Settings.FoamKey))
            {
                if (Source.Definition.FoamCapacity <= 0f)
                    Game.DisplaySubtitle($"~o~The {Source.Definition.Name} carries no foam.", 2500);
                else
                    Nozzle.Agent = Nozzle.IsFoam ? ExtinguishingAgent.Water : ExtinguishingAgent.Foam;
            }

            int wheel = Game.GetMouseWheelDelta();
            if (Game.IsKeyDown(Settings.PressureUpKey) || wheel > 0)
                Nozzle.ChangePressure(1);
            else if (Game.IsKeyDown(Settings.PressureDownKey) || wheel < 0)
                Nozzle.ChangePressure(-1);
        }

        private void Spray(Ped player)
        {
            if (!Source.PumpEngaged)
            {
                StopSpraying();
                Game.DisplaySubtitle($"~o~The pump of the {Source.Definition.Name} is disengaged.~s~ Engage it on the tablet.", 100);
                return;
            }

            Nozzle.PumpPressure = Source.PumpPressure;
            float needed = Source.Definition.PumpRate * Nozzle.FlowFactor * Nozzle.EffectivePressure * Game.FrameTime;
            IsTankEmpty = Source.DrawWater(needed) <= 0f;

            if (Nozzle.IsFoam && Source.DrawFoam(Nozzle.FoamUse * Game.FrameTime) <= 0f)
            {
                Nozzle.Agent = ExtinguishingAgent.Water;
                Notify.Warning($"The {Source.Definition.Name} is ~o~out of foam~s~, back to water.");
            }
            if (IsTankEmpty)
            {
                StopSpraying();
                Game.DisplaySubtitle($"~r~The {Source.Definition.Name} is out of water.~s~ Connect it to a hydrant.", 100);
                return;
            }

            // Face where the camera looks, so the hose points the same way as the stream.
            player.Heading = GameplayCamera.Rotation.Yaw;

            stream.Spray(player, GetNozzlePosition(player), GameplayCamera.Direction, Nozzle);
            IsSpraying = true;
        }

        private void StopSpraying()
        {
            if (IsSpraying)
                stream.Stop();
            IsSpraying = false;
        }

        private static Vector3 GetNozzlePosition(Ped player)
        {
            Object nozzle = player.Inventory.EquippedWeaponObject;
            return nozzle.Exists() ? nozzle.Position : player.GetBonePosition(PedBoneId.RightHand);
        }
    }
}
