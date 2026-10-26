using LSFD.API;
using LSFD.Core;
using Rage;

namespace LSFD.LivingWorld
{
    /// <summary>
    /// Now and then, while the player drives around on duty, an incident happens on the road ahead: a crash, a car fire,
    /// someone down. Any callout marked with <see cref="CalloutInfoAttribute.AllowAmbient"/> can be one.
    /// </summary>
    internal class AmbientIncidents
    {
        private const float MinDistance = 140f;
        private const float MaxDistance = 260f;

        // Out of view is best; on screen, only this far away where buildings and traffic hide it being set up.
        private const float MinVisibleDistance = 210f;
        private const float MaxAngle = 70f;
        private const float MinPlayerSpeed = 4f;
        private const int SpotAttempts = 8;
        private const int CalloutAttempts = 3;
        private const uint RetryDelay = 20000;
        private const uint NotDrivingDelay = 5000;
        private const uint AfterCallDelay = 60000;

        private readonly CalloutRegistry registry;
        private readonly CalloutManager callouts;
        private readonly DutyManager duty;
        private uint nextAt;

        public AmbientIncidents(CalloutRegistry registry, CalloutManager callouts, DutyManager duty)
        {
            this.registry = registry;
            this.callouts = callouts;
            this.duty = duty;
            Schedule();
        }

        public void Process()
        {
            if (!Settings.AmbientIncidents || !duty.IsOnDuty)
                return;

            // Not right after a call either.
            if (callouts.IsBusy)
            {
                Delay(AfterCallDelay);
                return;
            }

            if (Game.GameTime < nextAt)
                return;

            Ped player = Game.LocalPlayer.Character;
            if (!player.IsInAnyVehicle(false) || player.CurrentVehicle.Speed < MinPlayerSpeed)
            {
                nextAt = Game.GameTime + NotDrivingDelay;
                return;
            }

            if (TryStart())
                Schedule();
            else
                nextAt = Game.GameTime + RetryDelay;
        }

        /// <summary>Puts an incident on the road ahead right away; for testing from the console.</summary>
        public bool TryStart()
        {
            Ped player = Game.LocalPlayer.Character;
            if (callouts.IsBusy || !player.Exists())
                return false;

            Vector3? spot = FindSpot(player);
            if (!spot.HasValue)
                return false;

            for (int i = 0; i < CalloutAttempts; i++)
            {
                Callout callout = registry.CreateRandomAmbient();
                if (callout == null)
                    return false;
                if (callouts.DispatchAmbient(callout, spot.Value))
                    return true;
            }
            return false;
        }

        private static Vector3? FindSpot(Ped player)
        {
            Entity moving = player.IsInAnyVehicle(false) ? (Entity)player.CurrentVehicle : player;
            float heading = MathHelper.ConvertDirectionToHeading(moving.ForwardVector);
            Vector3? visible = null;

            for (int i = 0; i < SpotAttempts; i++)
            {
                Vector3 direction = MathHelper.ConvertHeadingToDirection(heading + MathHelper.GetRandomSingle(-MaxAngle, MaxAngle));
                Vector3 street = World.GetNextPositionOnStreet(player.Position + direction * MathHelper.GetRandomSingle(MinDistance, MaxDistance));
                float distance = street.DistanceTo(player.Position);
                if (distance < MinDistance || distance > MaxDistance + 60f)
                    continue;

                if (!Roads.IsOnScreen(street, 4f))
                    return street;
                if (distance >= MinVisibleDistance && !visible.HasValue)
                    visible = street;
            }
            return visible;
        }

        private void Schedule()
        {
            int seconds = MathHelper.GetRandomInteger(Settings.MinSecondsBetweenAmbientIncidents, Settings.MaxSecondsBetweenAmbientIncidents + 1);
            nextAt = Game.GameTime + (uint)(seconds * 1000);
        }

        private void Delay(uint milliseconds)
        {
            uint earliest = Game.GameTime + milliseconds;
            if (nextAt < earliest)
                nextAt = earliest;
        }
    }
}
