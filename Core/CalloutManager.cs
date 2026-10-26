using System;
using System.Linq;
using LSFD.API;
using LSFD.UI;
using Rage;

namespace LSFD.Core
{
    /// <summary>Dispatches callouts while the player is on duty and runs the accepted one.</summary>
    internal class CalloutManager
    {
        private const int DispatchAttempts = 3;

        // A callout may nudge the ambient spot a little (e.g. onto the sidewalk), but not move somewhere else.
        private const float AmbientTolerance = 40f;

        private readonly CalloutRegistry registry;
        private readonly DutyManager duty;

        private uint offeredAt;
        private uint nextCalloutAt;

        /// <summary>The callout waiting to be accepted.</summary>
        public Callout Offered { get; private set; }

        /// <summary>The accepted callout the player is working on.</summary>
        public Callout Current { get; private set; }

        public bool IsBusy => Offered != null || Current != null;

        public event Action<Callout> CalloutCompleted;

        /// <summary>Raised when a call is offered to the player, dispatched or on view.</summary>
        public event Action<Callout> CalloutOffered;

        /// <summary>Raised when the player accepts a call.</summary>
        public event Action<Callout> CalloutAccepted;

        /// <summary>Raised when an accepted call ends, completed or not.</summary>
        public event Action<Callout> CalloutEnded;

        /// <summary>Raised when the player declines an offered call or lets it time out.</summary>
        public event Action<Callout> CalloutDeclined;

        public CalloutManager(CalloutRegistry registry, DutyManager duty)
        {
            this.registry = registry;
            this.duty = duty;
            ScheduleNext();
        }

        public void Process()
        {
            if (!duty.IsOnDuty)
            {
                if (IsBusy)
                    EndCurrent(L.T("Call cancelled, you went off duty."));
                return;
            }

            if (Current != null)
                ProcessCurrent();
            else if (Offered != null)
                ProcessOffered();
            else if (Settings.CalloutsEnabled && Game.GameTime >= nextCalloutAt)
                DispatchRandom();
        }

        public bool DispatchRandom()
        {
            for (int i = 0; i < DispatchAttempts; i++)
            {
                Callout callout = registry.CreateRandom();
                if (callout != null && Dispatch(callout))
                    return true;
            }

            ScheduleNext();
            return false;
        }

        public bool Dispatch(string name)
        {
            Callout callout = registry.Create(name);
            return callout != null && Dispatch(callout);
        }

        public void EndCurrent(string message)
        {
            if (Offered != null)
                Safely(Offered, Offered.Decline);
            Callout ended = Current;
            if (ended != null)
                Safely(ended, ended.End);

            Offered = null;
            Current = null;
            ScheduleNext();

            if (message != null)
                Notify.Info(message);
            if (ended != null)
                CalloutEnded?.Invoke(ended);
        }

        private bool Dispatch(Callout callout)
        {
            if (IsBusy)
                return false;

            bool display = false;
            Safely(callout, () => display = callout.OnBeforeCalloutDisplayed());
            if (!display)
                return false;

            Offer(callout, $"{callout.Priority.ToCode()} {callout.CalloutMessage ?? callout.FriendlyName}",
                L.F($"Press ~b~{Settings.AcceptCalloutKey}~s~ or use the tablet to respond."));
            return true;
        }

        /// <summary>
        /// Offers an incident the player comes across at <paramref name="spot"/> on the road ahead. Returns false when
        /// the callout cancelled itself or chose a location of its own.
        /// </summary>
        public bool DispatchAmbient(Callout callout, Vector3 spot)
        {
            if (IsBusy)
                return false;

            bool display = false;
            Functions.AmbientLocation = spot;
            try
            {
                Safely(callout, () => display = callout.OnBeforeCalloutDisplayed());
            }
            finally
            {
                Functions.AmbientLocation = null;
            }

            if (!display || callout.State == CalloutState.Ended)
                return false;
            if (callout.CalloutPosition.DistanceTo2D(spot) > AmbientTolerance)
            {
                Safely(callout, callout.Decline);
                return false;
            }

            callout.IsAmbient = true;
            Offer(callout, L.F($"~y~On view:~s~ {callout.CalloutMessage ?? callout.FriendlyName}"),
                L.F($"Press ~b~{Settings.AcceptCalloutKey}~s~ to stop and handle it yourself."));
            return true;
        }

        private void Offer(Callout callout, string title, string howToRespond)
        {
            callout.Display();
            Offered = callout;
            offeredAt = Game.GameTime;

            Vector3 position = callout.CalloutPosition;
            float distance = Game.LocalPlayer.Character.DistanceTo(position);
            Notify.Dispatch(title,
                $"{Describe(callout)}~n~~y~{World.GetStreetName(position)}~s~ ({distance:0} m)~n~{callout.CalloutAdvisory}~n~{howToRespond}");
            CalloutOffered?.Invoke(callout);
        }

        public void AcceptOffered()
        {
            if (Offered == null)
                return;

            Callout callout = Offered;
            Offered = null;
            Current = callout;

            bool accepted = false;
            Safely(callout, () => accepted = callout.Accept());
            if (!accepted)
            {
                EndCurrent(null);
                return;
            }

            // The player is driving up to it, so the scene has to be there already.
            if (callout.IsAmbient)
                Safely(callout, callout.Arrive);

            Game.LogTrivial($"LSFD: accepted callout '{callout.FriendlyName}'{(callout.IsAmbient ? " (on view)" : "")}.");
            CalloutAccepted?.Invoke(callout);
        }

        public void DeclineOffered()
        {
            Callout declined = Offered;
            if (declined == null)
                return;

            EndCurrent(null);
            CalloutDeclined?.Invoke(declined);
        }

        private void ProcessOffered()
        {
            if (Game.IsKeyDown(Settings.AcceptCalloutKey))
            {
                AcceptOffered();
            }
            else if (Game.GameTime - offeredAt > Settings.AcceptTimeoutSeconds * 1000)
            {
                DeclineOffered();
            }
        }

        private void ProcessCurrent()
        {
            Callout callout = Current;

            if (Game.LocalPlayer.IsDead)
            {
                EndCurrent(L.T("You were incapacitated, another unit is taking over."));
                return;
            }

            Safely(callout, callout.Tick);

            if (callout.State != CalloutState.Ended)
                return;

            Current = null;
            ScheduleNext();

            if (callout.IsCompleted)
                CalloutCompleted?.Invoke(callout);
            CalloutEnded?.Invoke(callout);
        }

        /// <summary>E.g. "Fire · 2nd Alarm · Code 3".</summary>
        public static string Describe(Callout callout)
        {
            string alarm = CalloutPriorityExtensions.AlarmToDisplay(callout.AlarmLevel);
            return string.Join(" · ", new[] { callout.Category.ToDisplayName(), alarm, callout.Priority.ToResponse() }.Where(s => s != ""));
        }

        private void ScheduleNext()
        {
            int seconds = MathHelper.GetRandomInteger(Settings.MinSecondsBetweenCallouts, Settings.MaxSecondsBetweenCallouts + 1);
            nextCalloutAt = Game.GameTime + (uint)(seconds * 1000);
        }

        // Callouts can come from other people's DLLs, so a crash in one must not take LSFD down.
        private static void Safely(Callout callout, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Game.LogTrivial($"LSFD: callout '{callout.FriendlyName}' ({callout.GetType().FullName}) threw an exception and was ended: {e}");
                try
                {
                    callout.End();
                }
                catch (Exception cleanup)
                {
                    Game.LogTrivial($"LSFD: cleaning up '{callout.FriendlyName}' failed as well: {cleanup.Message}");
                }
            }
        }
    }
}
