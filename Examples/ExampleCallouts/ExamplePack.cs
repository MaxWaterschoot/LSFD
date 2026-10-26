using LSFD.API;
using Rage;

namespace ExampleCallouts
{
    // Optional: a CalloutPack runs code when LSFD loads your DLL, e.g. to react to duty changes and other events.
    public class ExamplePack : CalloutPack
    {
        // Baked in when this DLL is compiled, so LSFD knows which API it was built against.
        public override int ApiVersion => Functions.ApiVersion;

        public override void Initialize()
        {
            Functions.OnDutyStateChanged += OnDutyStateChanged;
            Events.OnCalloutEnded += OnCalloutEnded;
            Events.OnFireExtinguished += OnFireExtinguished;
            Game.LogTrivial("ExampleCallouts loaded.");
        }

        public override void Finally()
        {
            Functions.OnDutyStateChanged -= OnDutyStateChanged;
            Events.OnCalloutEnded -= OnCalloutEnded;
            Events.OnFireExtinguished -= OnFireExtinguished;
        }

        private static void OnDutyStateChanged(bool onDuty)
        {
            if (onDuty)
                Game.DisplayNotification("~r~ExampleCallouts~s~: Dumpster fires are now in rotation.");
        }

        private static void OnCalloutEnded(Callout callout)
        {
            Game.LogTrivial($"ExampleCallouts: '{callout.FriendlyName}' ended, {(callout.IsCompleted ? "handled" : "cancelled")}.");
        }

        private static void OnFireExtinguished(FireIncident incident)
        {
            Game.LogTrivial($"ExampleCallouts: a fire at {incident.Origin} is out after {(Game.GameTime - incident.StartedAt) / 1000} s.");
        }
    }
}
