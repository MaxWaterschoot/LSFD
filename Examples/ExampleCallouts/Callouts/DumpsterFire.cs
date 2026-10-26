using LSFD.API;
using Rage;

namespace ExampleCallouts.Callouts
{
    // Everything needed for a callout: inherit from Callout and add [CalloutInfo].
    // Build it, drop the DLL in GTA V/Plugins/LSFD/Callouts/ and LSFD picks it up on the next load.
    [CalloutInfo("Dumpster Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority3)]
    public class DumpsterFire : Callout
    {
        private Object dumpster;
        private int fire;

        public override bool OnBeforeCalloutDisplayed()
        {
            CalloutPosition = Functions.GetRandomCalloutLocation(200f, 700f);
            CalloutMessage = "Dumpster Fire";
            CalloutAdvisory = "A dumpster is on fire behind a building.";
            CalloutAreaRadius = 30f;
            return true;
        }

        public override bool OnCalloutAccepted()
        {
            Game.DisplaySubtitle("Respond to the ~r~dumpster fire~s~.");
            return true;
        }

        protected override void OnArrival()
        {
            dumpster = new Object("prop_dumpster_01a", CalloutPosition.Around2D(4f)) { IsPersistent = true };
            fire = Fires.StartFire(dumpster.Position, 5);
        }

        public override void Process()
        {
            if (HasArrived && Fires.CountFiresInRange(CalloutPosition, 20f) == 0)
                Complete();
        }

        protected override void OnEnd()
        {
            Fires.RemoveFire(fire);
            if (dumpster.Exists())
                dumpster.Dismiss();
        }
    }
}
