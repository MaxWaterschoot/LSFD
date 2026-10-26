namespace LSFD.API
{
    /// <summary>
    /// Optional entry point for a callout DLL. LSFD creates one instance of every non-abstract subclass it finds
    /// and calls <see cref="Initialize"/> after loading, e.g. to subscribe to <see cref="Functions.OnDutyStateChanged"/>.
    /// Callouts with a <see cref="CalloutInfoAttribute"/> are registered automatically, with or without a pack. When the DLL
    /// has a pack whose <see cref="ApiVersion"/> the installed LSFD doesn't support, none of its callouts are registered.
    /// </summary>
    public abstract class CalloutPack
    {
        /// <summary>
        /// The LSFD API version the pack was built for. Override it as <c>public override int ApiVersion =&gt; Functions.ApiVersion;</c>
        /// (the constant is baked in when you compile). LSFD skips a pack that needs a newer LSFD than the installed one,
        /// with a message in the log, instead of crashing halfway. Packs that don't override it count as version 1.
        /// </summary>
        public virtual int ApiVersion => 1;

        public abstract void Initialize();

        /// <summary>Called when LSFD unloads.</summary>
        public virtual void Finally()
        {
        }
    }
}
