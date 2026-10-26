using System;
using System.Linq;
using LSFD.Core;
using LSFD.Medical;
using Rage;

namespace LSFD.API
{
    /// <summary>Patients for callouts: injured people, cardiac arrests and victims trapped in burning buildings.</summary>
    public static class Patients
    {
        private static readonly string[] CivilianModels =
        {
            "a_m_m_business_01", "a_f_y_business_01", "a_m_y_hipster_01", "a_f_y_hipster_02", "a_m_m_eastsa_01",
            "a_f_m_downtown_01", "a_m_y_genstreet_01", "a_f_y_genhot_01", "a_m_o_genstreet_01", "a_f_m_bevhills_01",
        };

        /// <summary>Raised when a patient's condition changes (e.g. stable after treatment, or deceased).</summary>
        public static event Action<Ped, PatientCondition> OnConditionChanged
        {
            add => Lsfd.Patients.ConditionChanged += value;
            remove => Lsfd.Patients.ConditionChanged -= value;
        }

        /// <summary>Spawns a civilian at <paramref name="position"/> and makes them a patient.</summary>
        /// <remarks>Without care, patients get worse: injured → unconscious → cardiac arrest → deceased; smoke and heat speed that up.</remarks>
        public static Ped Spawn(Vector3 position, PatientCondition condition, float heading = 0f, InjuryType injury = InjuryType.Trauma)
        {
            Model model = CivilianModels[MathHelper.GetRandomInteger(CivilianModels.Length)];
            var ped = new Ped(model, position, heading);
            Lsfd.Patients.Register(ped, condition, injury: injury);
            return ped;
        }

        /// <summary>A walking wounded patient (green): minor injuries that don't get worse, sitting at the side.</summary>
        public static Ped SpawnWalkingWounded(Vector3 position)
        {
            Model model = CivilianModels[MathHelper.GetRandomInteger(CivilianModels.Length)];
            var ped = new Ped(model, position, MathHelper.GetRandomSingle(0f, 360f));
            Lsfd.Patients.Register(ped, PatientCondition.Injured, minor: true);
            return ped;
        }

        /// <summary>Makes an existing ped a patient that the player (or AI crew) has to take care of.</summary>
        public static void MakePatient(Ped ped, PatientCondition condition, InjuryType injury = InjuryType.Trauma) =>
            Lsfd.Patients.Register(ped, condition, injury: injury);

        public static void SetInjury(Ped ped, InjuryType injury)
        {
            Patient patient = Lsfd.Patients.Find(ped);
            if (patient != null)
                patient.Injury = injury;
        }

        public static TriageTag GetTriage(Ped ped) => Lsfd.Patients.Find(ped)?.Triage ?? TriageTag.None;

        /// <summary>Whether every one of these patients has a triage tag (or isn't a patient anymore).</summary>
        public static bool AreAllTriaged(params Ped[] peds) => peds.All(p => Lsfd.Patients.Find(p) is Patient patient
            ? patient.Triage != TriageTag.None || patient.Transport != TransportState.None
            : true);

        /// <summary>
        /// Spawns a victim inside a burning building. They have to be carried out to <paramref name="safePosition"/>
        /// (or anywhere outside away from fire) and are then treated. They go into cardiac arrest if left too long.
        /// </summary>
        public static Ped SpawnTrappedInBuilding(Vector3 position, Vector3 safePosition, PatientCondition condition = PatientCondition.Unconscious)
        {
            Model model = CivilianModels[MathHelper.GetRandomInteger(CivilianModels.Length)];
            var ped = new Ped(model, position, MathHelper.GetRandomSingle(0f, 360f));
            Lsfd.Patients.Register(ped, condition, true, safePosition, InjuryType.SmokeInhalation);
            return ped;
        }

        public static PatientCondition? GetCondition(Ped ped) => Lsfd.Patients.Find(ped)?.Condition;

        /// <summary>Whether a building victim has been brought outside.</summary>
        public static bool IsRescued(Ped ped) => Lsfd.Patients.Find(ped)?.Rescued ?? false;

        /// <summary>Nothing left to do for these patients: rescued (if trapped) and stable or deceased.</summary>
        public static bool AreAllHandled(params Ped[] peds) => peds.All(p => Lsfd.Patients.Find(p)?.IsHandled ?? true);

        /// <summary>Stops tracking a patient; call this in OnEnd.</summary>
        public static void Release(Ped ped) => Lsfd.Patients.Release(ped);
    }
}
