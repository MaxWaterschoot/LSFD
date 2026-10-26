using LSFD.API;
using Rage;

namespace ExampleCallouts
{
    // Optional: a CalloutPack runs code when LSFD loads your DLL, e.g. to react to duty changes.
    public class ExamplePack : CalloutPack
    {
        public override void Initialize()
        {
            Functions.OnDutyStateChanged += OnDutyStateChanged;
            Game.LogTrivial("ExampleCallouts loaded.");
        }

        public override void Finally()
        {
            Functions.OnDutyStateChanged -= OnDutyStateChanged;
        }

        private static void OnDutyStateChanged(bool onDuty)
        {
            if (onDuty)
                Game.DisplayNotification("~r~ExampleCallouts~s~: Dumpster fires are now in rotation.");
        }
    }
}
