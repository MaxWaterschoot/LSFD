using LSFD.API;
using Rage;

namespace LSFD.Medical
{
    internal class Patient
    {
        public Ped Ped { get; }
        public PatientCondition Condition { get; set; }

        /// <summary>Inside a burning building; has to be carried out before treatment.</summary>
        public bool TrappedInBuilding { get; }

        /// <summary>Where rescued patients are brought (e.g. in front of the building).</summary>
        public Vector3 SafePosition { get; }

        public bool Rescued { get; set; }

        /// <summary>Who is carrying the patient, if anyone.</summary>
        public Ped Carrier { get; set; }

        /// <summary>The crew member or player working on this patient, so two people don't pick the same one.</summary>
        public Ped Caregiver { get; set; }

        public uint ConditionSince { get; set; } = Game.GameTime;
        public uint RegisteredAt { get; } = Game.GameTime;
        public int CprCycles { get; set; }

        /// <summary>True when the patient lies down after being resuscitated instead of sitting up.</summary>
        public bool WasResuscitated { get; set; }

        public bool NeedsRescue => TrappedInBuilding && !Rescued && Carrier == null;

        public bool NeedsCare => !NeedsRescue && Carrier == null
            && (Condition == PatientCondition.Injured || Condition == PatientCondition.Unconscious || Condition == PatientCondition.CardiacArrest);

        /// <summary>Nothing left to do: out of the building and either stable or beyond help.</summary>
        public bool IsHandled => (!TrappedInBuilding || Rescued)
            && (Condition == PatientCondition.Stable || Condition == PatientCondition.Deceased);

        public Patient(Ped ped, PatientCondition condition, bool trappedInBuilding, Vector3 safePosition)
        {
            Ped = ped;
            Condition = condition;
            TrappedInBuilding = trappedInBuilding;
            SafePosition = safePosition;
        }
    }
}
