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
        public static Ped Spawn(Vector3 position, PatientCondition condition, float heading = 0f)
        {
            Model model = CivilianModels[MathHelper.GetRandomInteger(CivilianModels.Length)];
            var ped = new Ped(model, position, heading);
            Lsfd.Patients.Register(ped, condition);
            return ped;
        }

        /// <summary>Makes an existing ped a patient that the player (or AI crew) has to take care of.</summary>
        public static void MakePatient(Ped ped, PatientCondition condition) => Lsfd.Patients.Register(ped, condition);

        /// <summary>
        /// Spawns a victim inside a burning building. They have to be carried out to <paramref name="safePosition"/>
        /// (or anywhere outside away from fire) and are then treated. They go into cardiac arrest if left too long.
        /// </summary>
        public static Ped SpawnTrappedInBuilding(Vector3 position, Vector3 safePosition, PatientCondition condition = PatientCondition.Unconscious)
        {
            Model model = CivilianModels[MathHelper.GetRandomInteger(CivilianModels.Length)];
            var ped = new Ped(model, position, MathHelper.GetRandomSingle(0f, 360f));
            Lsfd.Patients.Register(ped, condition, true, safePosition);
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
