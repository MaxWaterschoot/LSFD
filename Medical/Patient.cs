using LSFD.API;
using Rage;

namespace LSFD.Medical
{
    internal enum TransportState
    {
        None,
        /// <summary>Paramedics are putting the patient on the stretcher and wheeling them to the ambulance.</summary>
        BeingLoaded,
        /// <summary>In the ambulance.</summary>
        Loaded,
    }

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

        /// <summary>Stable patients have to be taken to hospital by an ambulance before the call is over.</summary>
        public bool RequiresTransport { get; set; }

        public TransportState Transport { get; set; }

        public InjuryType Injury { get; set; } = InjuryType.Trauma;

        /// <summary>Walking wounded: minor injuries that don't get worse.</summary>
        public bool IsMinor { get; set; }

        public TriageTag Triage { get; set; }

        /// <summary>Seconds (at normal pace) until the patient gets worse without care.</summary>
        public float DeteriorationLeft { get; set; }

        /// <summary>How much faster than normal the patient gets worse, from smoke and heat around them.</summary>
        public float ExposureRate { get; set; } = 1f;

        public bool NeedsTriage => Triage == TriageTag.None && !NeedsRescue && Carrier == null && Transport == TransportState.None
            && Condition != PatientCondition.Deceased;

        /// <summary>Lower goes first: red, untagged, yellow, green, black.</summary>
        public int TriagePriority
        {
            get
            {
                switch (Triage)
                {
                    case TriageTag.Red: return 0;
                    case TriageTag.None: return 1;
                    case TriageTag.Yellow: return 2;
                    case TriageTag.Green: return 3;
                    default: return 4;
                }
            }
        }

        public bool NeedsRescue => TrappedInBuilding && !Rescued && Carrier == null;

        public bool NeedsCare => !NeedsRescue && Carrier == null
            && (Condition == PatientCondition.Injured || Condition == PatientCondition.Unconscious || Condition == PatientCondition.CardiacArrest);

        /// <summary>Stable, out of danger and waiting for an ambulance to take them.</summary>
        public bool AwaitsTransport => RequiresTransport && Condition == PatientCondition.Stable && Transport == TransportState.None
            && Carrier == null && Caregiver == null && (!TrappedInBuilding || Rescued);

        /// <summary>Nothing left to do: out of the building and either beyond help, or stable (and in the ambulance when transport is required).</summary>
        public bool IsHandled => (!TrappedInBuilding || Rescued)
            && (Condition == PatientCondition.Deceased
                || (Condition == PatientCondition.Stable && (!RequiresTransport || Transport == TransportState.Loaded)));

        public Patient(Ped ped, PatientCondition condition, bool trappedInBuilding, Vector3 safePosition)
        {
            Ped = ped;
            Condition = condition;
            TrappedInBuilding = trappedInBuilding;
            SafePosition = safePosition;
        }
    }
}
