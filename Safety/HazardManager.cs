using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Core;
using LSFD.Firefighting;
using LSFD.Water;
using Rage;
using Rage.Native;

namespace LSFD.Safety
{
    /// <summary>
    /// What a fire does to the player: smoke that blinds and chokes without a mask, and heat near the flames that
    /// turnout gear only protects against up to a point.
    /// </summary>
    internal class HazardManager
    {
        private const float SmokeThreshold = 0.15f;
        private const float SmokeDamagePerSecond = 6f;
        private const float HeatProtection = 0.6f;
        private const float HeatDamagePerSecond = 30f;
        // A fog pattern puts a wall of water between the firefighter and the fire.
        private const float FogHeatShield = 0.5f;
        private const float HeatOverlayStart = 0.3f;
        private const float ShockDamagePerSecond = 25f;
        private const uint ShockWindow = 300;

        private const int SmokeAlpha = 215;
        private const int SmokeAlphaWithCamera = 70;
        private const uint WarningInterval = 4000;
        private const uint CoughInterval = 6000;

        private const string CoughDictionary = "timetable@gardener@smoking_joint";
        private const string CoughAnimation = "idle_cough";

        private readonly FireSystem fire;
        private readonly BreathingApparatus scba;
        private readonly ThermalCamera camera;
        private readonly HoseManager hose;

        private readonly List<ToxicAtmosphere> toxicAtmospheres = new List<ToxicAtmosphere>();
        private float pendingDamage;
        private uint nextWarningAt;
        private uint nextCoughAt;
        private bool madeFireProof;

        public HazardManager(FireSystem fire, BreathingApparatus scba, ThermalCamera camera, HoseManager hose)
        {
            this.fire = fire;
            this.scba = scba;
            this.camera = camera;
            this.hose = hose;
        }

        public void AddToxicAtmosphere(ToxicAtmosphere zone)
        {
            toxicAtmospheres.Add(zone);
        }

        /// <summary>Toxic gas (like CO) around the player, 0 to 1; invisible, unlike smoke.</summary>
        public float Toxic { get; private set; }

        /// <summary>Smoke around the player, 0 to 1.</summary>
        public float Smoke { get; private set; }

        /// <summary>Heat at the player's position after the fog shield; above 0.6 it hurts.</summary>
        public float Heat { get; private set; }

        public bool IsBreathingSmoke { get; private set; }
        public bool IsInDangerousHeat => Heat > HeatProtection;

        public void Process(bool onDuty, bool uiOpen)
        {
            Ped player = Game.LocalPlayer.Character;
            if (!onDuty || !player.Exists() || player.IsDead)
            {
                Reset();
                return;
            }

            // The model changes when going on duty, which resets the proofs, so keep setting them.
            if (Settings.TurnoutGearFireProof && !player.IsFireProof)
            {
                player.IsFireProof = true;
                madeFireProof = true;
            }

            bool inInterior = NativeFunction.CallByName<int>("GET_INTERIOR_FROM_ENTITY", player) != 0;
            Smoke = fire.GetSmoke(player.Position, inInterior);

            float heat = fire.GetHeat(player.Position);
            if (hose.IsSpraying && !hose.Nozzle.IsStraight)
                heat *= FogHeatShield;
            Heat = heat;

            toxicAtmospheres.RemoveAll(z => z.IsRemoved);
            Toxic = toxicAtmospheres.Where(z => z.Position.DistanceTo(player.Position) <= z.Radius).Select(z => z.Strength).DefaultIfEmpty(0f).Max();

            IsBreathingSmoke = Math.Max(Smoke, Toxic) >= SmokeThreshold && !scba.IsSupplying;

            if (Settings.SmokeAndHeatDamage)
                ApplyDamage(player);
            if (Settings.SmokeScreenEffect && !uiOpen)
                DrawScreenEffects();
        }

        /// <summary>Takes the turnout gear off again, e.g. when going off duty.</summary>
        public void Reset()
        {
            Smoke = 0f;
            Heat = 0f;
            IsBreathingSmoke = false;
            pendingDamage = 0f;

            Ped player = Game.LocalPlayer.Character;
            if (madeFireProof && player.Exists())
                player.IsFireProof = false;
            madeFireProof = false;
        }

        private void ApplyDamage(Ped player)
        {
            float delta = Game.FrameTime;

            if (IsBreathingSmoke)
            {
                pendingDamage += SmokeDamagePerSecond * Math.Max(Smoke, Toxic) * delta;
                Warn(scba.IsMaskOn
                    ? L.T("~r~Your cylinder is empty!~s~ Get out of the smoke.")
                    : L.F($"~r~You are breathing smoke!~s~ Put your face piece on with ~b~{Settings.MaskKey}~s~."));
                Cough(player);
            }

            // Water conducts: spraying a fire with live electrics shocks whoever holds the nozzle.
            if (hose.IsSpraying && Game.GameTime - fire.LastEnergizedHitAt < ShockWindow)
            {
                pendingDamage += ShockDamagePerSecond * delta;
                Warn(L.T("~r~Electric shock!~s~ Isolate the power first, or use an extinguisher."));
                NativeFunction.CallByName<uint>("SHAKE_GAMEPLAY_CAM", "SMALL_EXPLOSION_SHAKE", 0.05f);
            }

            if (IsInDangerousHeat)
            {
                pendingDamage += HeatDamagePerSecond * (Heat - HeatProtection) * delta;
                Warn(L.T("~r~Too hot!~s~ Back off, or protect yourself with a ~b~fog~s~ pattern."));
            }

            if (pendingDamage < 1f)
                return;

            int damage = (int)pendingDamage;
            pendingDamage -= damage;
            player.Health = Math.Max(0, player.Health - damage);
        }

        private void Warn(string text)
        {
            if (Game.GameTime < nextWarningAt)
                return;

            nextWarningAt = Game.GameTime + WarningInterval;
            Game.DisplaySubtitle(text, 3000);
        }

        private void Cough(Ped player)
        {
            if (Game.GameTime < nextCoughAt || player.IsInAnyVehicle(false) || hose.IsHolding)
                return;

            nextCoughAt = Game.GameTime + CoughInterval;
            player.Tasks.PlayAnimation(CoughDictionary, CoughAnimation, 4f, AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);
        }

        private void DrawScreenEffects()
        {
            if (Smoke > 0.05f)
            {
                int alpha = (int)(Math.Min(1f, Smoke) * (camera.IsActive ? SmokeAlphaWithCamera : SmokeAlpha));
                DrawFullScreen(45, 42, 40, alpha);
            }

            if (Heat > HeatOverlayStart)
                DrawFullScreen(200, 60, 0, (int)Math.Min(110f, (Heat - HeatOverlayStart) * 120f));
        }

        private static void DrawFullScreen(int r, int g, int b, int alpha)
        {
            NativeFunction.CallByName<uint>("DRAW_RECT", 0.5f, 0.5f, 1f, 1f, r, g, b, alpha, false);
        }
    }
}
