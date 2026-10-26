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
}
