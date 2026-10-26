namespace LSFD.API
{
    public enum PatientCondition
    {
        /// <summary>Conscious but hurt; needs to be treated.</summary>
        Injured,
        /// <summary>Unresponsive but breathing; needs to be treated.</summary>
        Unconscious,
        /// <summary>Not breathing, no pulse; needs CPR (and ideally an AED) before it is too late.</summary>
        CardiacArrest,
        /// <summary>Treated or resuscitated; ready for transport.</summary>
        Stable,
        Deceased,
    }

    /// <summary>What is wrong with a patient; decides how long treatment takes.</summary>
    public enum InjuryType
    {
        /// <summary>An illness, e.g. a heart problem.</summary>
        Medical,
        /// <summary>Fractures, wounds and head injuries from a fall or collision.</summary>
        Trauma,
        /// <summary>From flames or radiant heat; cooling and dressings take longer.</summary>
        Burns,
        /// <summary>Breathed in smoke or toxic gas; needs oxygen.</summary>
        SmokeInhalation,
    }

    /// <summary>START triage tag, used when there are more patients than rescuers.</summary>
    public enum TriageTag
    {
        None,
        /// <summary>Walking wounded, minor injuries: can wait.</summary>
        Green,
        /// <summary>Serious but stable enough to wait a little: delayed.</summary>
        Yellow,
        /// <summary>Life-threatening: immediate care and first transport.</summary>
        Red,
        /// <summary>Not breathing / deceased (expectant): no resources until the others are cared for.</summary>
        Black,
    }
}
