using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Buildings;
using LSFD.Core;
using LSFD.UI;
using LSFD.VehicleRescue;
using Rage;

namespace LSFD.Medical
{
    /// <summary>Injured, unconscious and cardiac arrest patients: CPR, treatment and carrying people out of buildings.</summary>
    internal class PatientManager : IInteractionProvider
    {
        public const float TreatSeconds = 8f;
        public const float CprCycleSeconds = 10f;
        public const float CarryPickUpSeconds = 2f;

        private const float Reach = 1.6f;
        private const float SafeDistance = 10f;
        private const uint CardiacArrestSurvivalTime = 4 * 60 * 1000;
        private const uint SmokeInhalationTime = 5 * 60 * 1000;

        private readonly ToolManager tools;
        private readonly List<Patient> patients = new List<Patient>();

        public IReadOnlyList<Patient> All => patients;

        public event Action<Ped, PatientCondition> ConditionChanged;

        public PatientManager(ToolManager tools)
        {
            this.tools = tools;
        }

        public Patient Register(Ped ped, PatientCondition condition, bool trappedInBuilding = false, Vector3? safePosition = null)
        {
            Release(ped);

            ped.IsPersistent = true;
            ped.BlockPermanentEvents = true;
            ped.IsInvincible = true;
            ped.Tasks.ClearImmediately();

            var patient = new Patient(ped, condition, trappedInBuilding, safePosition ?? ped.Position);
            patients.Add(patient);
            ApplyPose(patient);
            return patient;
        }

        public Patient Find(Ped ped) => patients.FirstOrDefault(p => p.Ped == ped);

        public void Release(Ped ped)
        {
            Patient patient = Find(ped);
            if (patient == null)
                return;

            if (patient.Carrier != null)
                Drop(patient);
            if (ped.Exists())
                ped.IsInvincible = false;
            patients.Remove(patient);
        }

        /// <summary>The closest patient within <paramref name="radius"/> that matches <paramref name="filter"/> and nobody is working on.</summary>
        public Patient FindNearest(Vector3 position, float radius, Func<Patient, bool> filter)
        {
            return patients
                .Where(p => p.Ped.Exists() && p.Caregiver == null && filter(p) && p.Ped.DistanceTo(position) <= radius)
                .OrderBy(p => p.Ped.DistanceTo(position))
                .FirstOrDefault();
        }

        public void Process()
        {
            patients.RemoveAll(p => !p.Ped.Exists());

            foreach (Patient patient in patients)
            {
                uint inCondition = Game.GameTime - patient.ConditionSince;

                if (patient.Condition == PatientCondition.CardiacArrest && patient.Caregiver == null && inCondition > CardiacArrestSurvivalTime)
                    SetCondition(patient, PatientCondition.Deceased);
                else if (patient.NeedsRescue && patient.Condition != PatientCondition.CardiacArrest && Game.GameTime - patient.RegisteredAt > SmokeInhalationTime)
                    SetCondition(patient, PatientCondition.CardiacArrest);
            }
        }

        public Interaction GetInteraction()
        {
            Ped player = Game.LocalPlayer.Character;
            if (player.IsInAnyVehicle(false))
                return null;

            Patient carried = patients.FirstOrDefault(p => p.Carrier == player);
            if (carried != null)
                return new Interaction(IsSafe(carried) ? "put the patient down" : "put the patient down (~o~still in danger~s~)", () => Drop(carried));

            Patient patient = patients
                .Where(p => p.Ped.Exists() && p.Carrier == null && p.Ped.DistanceTo(player.Position) <= Reach)
                .OrderBy(p => p.Ped.DistanceTo(player.Position))
                .FirstOrDefault();
            if (patient == null)
                return null;

            if (patient.NeedsRescue)
                return new Interaction("carry the ~r~victim~s~ out", () => StartCarry(patient, player), CarryPickUpSeconds, "LIFTING");

            switch (patient.Condition)
            {
                case PatientCondition.CardiacArrest when tools.IsHolding(EquipmentType.AED):
                    return CareInteraction(patient, "shock the patient with the ~y~AED", tools.Held.UseSeconds, "ANALYZING",
                        "amb@medic@standing@tendtodead@idle_a", "idle_a", () => PerformCprCycle(patient, true));

                case PatientCondition.CardiacArrest:
                    return CareInteraction(patient, "perform ~r~CPR", CprCycleSeconds, "CPR",
                        "mini@cpr@char_a@cpr_str", "cpr_pumpchest", () => PerformCprCycle(patient, false), cpr: true);

                case PatientCondition.Injured:
                case PatientCondition.Unconscious:
                    return CareInteraction(patient, "treat the ~r~patient", TreatSeconds, "TREATING",
                        "amb@medic@standing@tendtodead@idle_a", "idle_a", () => Treat(patient));

                case PatientCondition.Stable:
                    return Interaction.Hint("The patient is ~g~stable~s~ and ready for transport.");

                default:
                    return null;
            }
        }

        /// <summary>One round of CPR (or an AED shock); returns true when the patient got a pulse back.</summary>
        public bool PerformCprCycle(Patient patient, bool withAed)
        {
            if (patient.Condition != PatientCondition.CardiacArrest)
                return false;

            patient.CprCycles++;
            // Keeping the CPR going keeps the patient alive, so the survival clock restarts.
            patient.ConditionSince = Game.GameTime;

            float chance = Math.Min(0.15f + 0.07f * patient.CprCycles + (withAed ? 0.3f : 0f), 0.85f);
            if (MathHelper.GetRandomSingle(0f, 1f) > chance)
            {
                Game.DisplaySubtitle(withAed ? "~o~No shock advised.~s~ Continue CPR." : "~o~Still no pulse.~s~ Keep going.", 2500);
                return false;
            }

            patient.WasResuscitated = true;
            SetCondition(patient, PatientCondition.Stable);
            Notify.Info("~g~Pulse is back!~s~ The patient was resuscitated.");
            return true;
        }

        public void Treat(Patient patient)
        {
            if (patient.Condition != PatientCondition.Injured && patient.Condition != PatientCondition.Unconscious)
                return;

            SetCondition(patient, PatientCondition.Stable);
            Game.DisplaySubtitle("Patient ~g~treated~s~ and stable.", 2500);
        }

        /// <summary>Picks the patient up in a fireman's carry.</summary>
        public void StartCarry(Patient patient, Ped carrier)
        {
            patient.Carrier = carrier;
            patient.Ped.Tasks.ClearImmediately();
            patient.Ped.AttachTo(carrier, 0, new Vector3(0.27f, 0.15f, 0.63f), new Rotator(0.5f, 0.5f, 180f));
            patient.Ped.Tasks.PlayAnimation("nm", "firemans_carry", 8f, AnimationFlags.Loop);
            carrier.Tasks.PlayAnimation("missfinale_c2mcs_1", "fin_c2_mcs_1_camman", 8f, AnimationFlags.Loop | AnimationFlags.UpperBodyOnly | AnimationFlags.SecondaryTask);
        }

        public void Drop(Patient patient)
        {
            Ped carrier = patient.Carrier;
            patient.Carrier = null;

            patient.Ped.Detach();
            if (carrier.Exists())
            {
                carrier.Tasks.Clear();
                patient.Ped.Position = carrier.GetOffsetPositionFront(0.8f);
            }

            if (patient.TrappedInBuilding && !patient.Rescued && IsSafe(patient))
            {
                patient.Rescued = true;
                Notify.Info("Victim ~g~rescued~s~ from the building. Treat them for smoke inhalation.");
            }

            ApplyPose(patient);
        }

        private bool IsSafe(Patient patient)
        {
            Vector3 position = patient.Ped.Position;
            return position.DistanceTo(patient.SafePosition) <= SafeDistance
                || (InteriorSupport.GetInteriorAt(position) == 0 && Fires.CountFiresInRange(position, SafeDistance) == 0);
        }

        private Interaction CareInteraction(Patient patient, string prompt, float seconds, string label, string dictionary, string animation,
            Action complete, bool cpr = false)
        {
            Ped player = Game.LocalPlayer.Character;
            return new Interaction(prompt, complete, seconds, label)
            {
                AnimationDictionary = dictionary,
                AnimationName = animation,
                Started = () =>
                {
                    patient.Caregiver = player;
                    if (cpr)
                        patient.Ped.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.Loop);
                },
                Stopped = () =>
                {
                    patient.Caregiver = null;
                    ApplyPose(patient);
                },
            };
        }

        private void SetCondition(Patient patient, PatientCondition condition)
        {
            if (patient.Condition == condition)
                return;

            patient.Condition = condition;
            patient.ConditionSince = Game.GameTime;
            if (patient.Carrier == null)
                ApplyPose(patient);

            if (condition == PatientCondition.Deceased)
                Notify.Warning("A patient has ~r~died~s~.");

            ConditionChanged?.Invoke(patient.Ped, condition);
        }

        /// <summary>Plays the animation that fits the patient's condition.</summary>
        public void ApplyPose(Patient patient)
        {
            if (!patient.Ped.Exists() || patient.Carrier != null)
                return;

            switch (patient.Condition)
            {
                case PatientCondition.Injured:
                    patient.Ped.Tasks.PlayAnimation("combat@damage@writhe", "writhe_loop", 8f, AnimationFlags.Loop);
                    break;
                case PatientCondition.Stable when !patient.WasResuscitated:
                    patient.Ped.Tasks.PlayAnimation("amb@world_human_picnic@male@base", "base", 8f, AnimationFlags.Loop);
                    break;
                default:
                    patient.Ped.Tasks.PlayAnimation("dead", "dead_a", 8f, AnimationFlags.Loop);
                    break;
            }
        }
    }
}
