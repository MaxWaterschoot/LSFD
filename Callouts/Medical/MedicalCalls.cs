using LSFD.API;
using Rage;

namespace LSFD.Callouts
{
    /// <summary>A single patient on the street or sidewalk with a worried bystander.</summary>
    internal abstract class MedicalCall : Callout
    {
        private readonly Scene scene = new Scene();
        private Ped patient;
        private PatientCondition condition;
        private bool deathReported;

        protected abstract string Title { get; }
        protected abstract string Advisory { get; }

        /// <summary>Read once per call, so it may be random.</summary>
        protected abstract PatientCondition InitialCondition { get; }

        protected virtual InjuryType Injury => InjuryType.Medical;

        public override bool OnBeforeCalloutDisplayed()
        {
            // Off the road a bit, so the patient isn't lying in traffic.
            Vector3 street = Functions.GetRandomCalloutLocation(250f, 900f);
            CalloutPosition = World.GetNextPositionOnStreet(street.Around2D(15f));
            CalloutMessage = Title;
            CalloutAdvisory = Advisory;
            CalloutAreaRadius = 25f;
            condition = InitialCondition;
            return true;
        }

        protected override void OnArrival()
        {
            Vector3 position = CalloutPosition.Around2D(3f, 5f);
            patient = scene.Track(Patients.Spawn(position, condition, 0f, Injury));
            scene.SpawnBystander(position.Around2D(1.5f, 2.5f));
            Game.DisplayHelp(condition == PatientCondition.CardiacArrest
                ? "The patient has ~r~no pulse~s~: start ~r~CPR~s~ right away and use an ~y~AED~s~ if you have one."
                : "Assess and treat the ~r~patient~s~.");
        }

        public override void Process()
        {
            if (!HasArrived || !patient.Exists())
                return;

            PatientCondition? current = Patients.GetCondition(patient);
            if (current == PatientCondition.Deceased)
            {
                if (deathReported)
                    return;
                deathReported = true;
                Functions.DisplayNotification("~r~Patient deceased", "The patient could not be saved. Clear the call when you are ready.");
            }
            else if (Patients.AreAllHandled(patient))
            {
                Complete();
            }
        }

        protected override void OnEnd()
        {
            Patients.Release(patient);
            scene.Dismiss();
        }
    }

    [CalloutInfo("Cardiac Arrest", CalloutProbability.Medium, Category = CalloutCategory.Medical, Priority = CalloutPriority.Priority1)]
    internal class CardiacArrest : MedicalCall
    {
        protected override string Title => "Cardiac Arrest";
        protected override string Advisory => "Person collapsed, ~r~not breathing~s~. Bystander is on the phone.";
        protected override PatientCondition InitialCondition => PatientCondition.CardiacArrest;
    }

    [CalloutInfo("Unconscious Person", CalloutProbability.Medium, Category = CalloutCategory.Medical, Priority = CalloutPriority.Priority2)]
    internal class UnconsciousPerson : MedicalCall
    {
        protected override string Title => "Unconscious Person";
        protected override string Advisory => "Person found unresponsive on the sidewalk, breathing status unknown.";
        protected override PatientCondition InitialCondition =>
            MathHelper.GetRandomInteger(4) == 0 ? PatientCondition.CardiacArrest : PatientCondition.Unconscious;
    }

    [CalloutInfo("Injured Person", CalloutProbability.High, Category = CalloutCategory.Medical, Priority = CalloutPriority.Priority2)]
    internal class InjuredPerson : MedicalCall
    {
        private static readonly string[] Reasons =
        {
            "Person fell from a ladder, possible leg fracture.",
            "Cyclist fell, head injury reported.",
            "Assault victim with lacerations, police are on scene.",
            "Elderly person fell down the stairs.",
        };

        protected override string Title => "Injured Person";
        protected override string Advisory => Reasons[MathHelper.GetRandomInteger(Reasons.Length)];
        protected override PatientCondition InitialCondition => PatientCondition.Injured;
        protected override InjuryType Injury => InjuryType.Trauma;
    }
}
