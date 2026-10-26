namespace LSFD.API
{
    /// <summary>
    /// Optional entry point for a callout DLL. LSFD creates one instance of every non-abstract subclass it finds
    /// and calls <see cref="Initialize"/> after loading, e.g. to subscribe to <see cref="Functions.OnDutyStateChanged"/>.
    /// Callouts with a <see cref="CalloutInfoAttribute"/> are registered automatically, with or without a pack.
    /// </summary>
    public abstract class CalloutPack
    {
        public abstract void Initialize();

        /// <summary>Called when LSFD unloads.</summary>
        public virtual void Finally()
        {
        }
    }
}
