using LSFD.Core;
using Rage;
using Rage.Native;

namespace LSFD.LivingWorld
{
    /// <summary>
    /// Turns GTA's own fire trucks and ambulances off while the player is on duty, so they don't race LSFD to every
    /// fire and wreck. They come back when the player goes off duty.
    /// </summary>
    internal class GameDispatch
    {
        // eDispatchType values.
        private const int FireDepartment = 3;
        private const int AmbulanceDepartment = 5;

        // Other mods may turn them back on, so it is reapplied every now and then.
        private const uint ReapplyInterval = 5000;

        private bool disabled;
        private uint lastApplied;

        public void Process(bool onDuty)
        {
            bool disable = onDuty && Settings.DisableGameFireDispatch;
            if (disable == disabled && (!disabled || Game.GameTime - lastApplied < ReapplyInterval))
                return;

            SetEnabled(!disable);
            disabled = disable;
            lastApplied = Game.GameTime;
        }

        public void Restore()
        {
            if (!disabled)
                return;

            SetEnabled(true);
            disabled = false;
        }

        private static void SetEnabled(bool enabled)
        {
            NativeFunction.CallByName<uint>("ENABLE_DISPATCH_SERVICE", FireDepartment, enabled);
            NativeFunction.CallByName<uint>("ENABLE_DISPATCH_SERVICE", AmbulanceDepartment, enabled);
        }
    }
}
