using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Buildings;
using LSFD.Core;
using LSFD.Firefighting;
using LSFD.UI;
using LSFD.Utils;
using LSFD.VehicleRescue;
using Rage;
using Rage.Native;

namespace LSFD.Medical
{
    /// <summary>Things worth a line in the player's statistics.</summary>
    internal enum PatientMilestone
    {
        Rescued,
        Treated,
        Resuscitated,
        Transported,
    }

    /// <summary>
    /// Injured, unconscious and cardiac arrest patients: CPR, treatment, carrying people out of buildings and triage.
    /// Patients get worse without care (faster in smoke and heat): injured → unconscious → cardiac arrest → deceased.
    /// </summary>
    internal class PatientManager : IInteractionProvider
    {
        public const float TreatSeconds = 8f;
        public const float CprCycleSeconds = 10f;
        public const float CarryPickUpSeconds = 2f;

        private const string CprMetronomeSound = "Effects/AED_METRONOME.wav";

        private const float Reach = 1.6f;
        private const float SafeDistance = 10f;
        private const uint CardiacArrestSurvivalTime = 4 * 60 * 1000;
        private const float TriageSeconds = 2.5f;
        private const float MassCasualtyRadius = 60f;
        private const int MassCasualtyPatients = 3;
        private const float TriageMarkerDistance = 40f;
        private const float WarningDistance = 150f;
        private const uint ExposureInterval = 1000;

        // Smoke and heat speed up how fast patients get worse, and cause smoke inhalation and burns.
        private const float SmokeExposureFactor = 1.5f;
        private const float HeatExposureFactor = 3f;
        private const float BurnHeat = 0.4f;
        private const float InhalationSmoke = 0.2f;

        private readonly ToolManager tools;
        private readonly FireSystem fire;
        private uint nextExposureCheck;
        private readonly List<Patient> patients = new List<Patient>();

        public IReadOnlyList<Patient> All => patients;

        /// <summary>Whether stable patients must be taken away by an ambulance before a call is over.</summary>
        public bool TransportRequired { get; set; }

        public event Action<Ped, PatientCondition> ConditionChanged;

        public event Action<PatientMilestone, Ped> Milestone;

        public void ReportTransported(Ped patient) => Milestone?.Invoke(PatientMilestone.Transported, patient);

        public PatientManager(ToolManager tools, FireSystem fire)
        {
            this.tools = tools;
            this.fire = fire;
        }

        public Patient Register(Ped ped, PatientCondition condition, bool trappedInBuilding = false, Vector3? safePosition = null,
            InjuryType injury = InjuryType.Trauma, bool minor = false)
        {
            Release(ped);

            ped.IsPersistent = true;
            ped.BlockPermanentEvents = true;
            ped.IsInvincible = true;
            ped.Tasks.ClearImmediately();

            var patient = new Patient(ped, condition, trappedInBuilding, safePosition ?? ped.Position)
            {
                RequiresTransport = TransportRequired,
                Injury = injury,
                IsMinor = minor,
            };
            ResetDeterioration(patient);
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
            // With triage tags, the most urgent patient goes first.
            return patients
                .Where(p => p.Ped.Exists() && p.Caregiver == null && filter(p) && p.Ped.DistanceTo(position) <= radius)
                .OrderBy(p => p.TriagePriority)
                .ThenBy(p => p.Ped.DistanceTo(position))
                .FirstOrDefault();
        }

        /// <summary>More patients than a crew can handle at once: triage first.</summary>
        public bool IsMassCasualty(Vector3 position)
        {
            return patients.Count(p => p.Ped.Exists() && p.Condition != PatientCondition.Deceased && !p.IsHandled
                && p.Ped.DistanceTo(position) <= MassCasualtyRadius) >= MassCasualtyPatients;
        }

        public float GetTreatSeconds(Patient patient)
        {
            if (patient.IsMinor)
                return TreatSeconds * 0.6f;
            return patient.Injury == InjuryType.Burns ? TreatSeconds * 1.5f : TreatSeconds;
        }

        /// <summary>Tags the patient using START: breathing, conscious, able to walk.</summary>
        public void ApplyTriage(Patient patient)
        {
            patient.Triage = Assess(patient);
        }

        private static TriageTag Assess(Patient patient)
        {
            switch (patient.Condition)
            {
                case PatientCondition.Deceased:
                case PatientCondition.CardiacArrest:
                    return TriageTag.Black;
                case PatientCondition.Unconscious:
                    return TriageTag.Red;
                case PatientCondition.Stable:
                    return patient.WasResuscitated ? TriageTag.Red : patient.IsMinor ? TriageTag.Green : TriageTag.Yellow;
                default:
                    return patient.IsMinor ? TriageTag.Green : patient.Injury == InjuryType.Burns ? TriageTag.Red : TriageTag.Yellow;
            }
        }

        public static string TagToDisplay(TriageTag tag)
        {
            switch (tag)
            {
                case TriageTag.Red: return L.T("~r~RED~s~ (immediate)");
                case TriageTag.Yellow: return L.T("~y~YELLOW~s~ (delayed)");
                case TriageTag.Green: return L.T("~g~GREEN~s~ (minor)");
                case TriageTag.Black: return L.T("~c~BLACK~s~ (expectant)");
                default: return "untagged";
            }
        }

        public static string InjuryToDisplay(InjuryType injury)
        {
            switch (injury)
            {
                case InjuryType.Burns: return L.T("burns");
                case InjuryType.SmokeInhalation: return L.T("smoke inhalation");
                case InjuryType.Medical: return L.T("medical");
                default: return L.T("trauma");
            }
        }

        public void Process()
        {
            patients.RemoveAll(p => !p.Ped.Exists());

            if (Game.GameTime >= nextExposureCheck)
            {
                nextExposureCheck = Game.GameTime + ExposureInterval;
                foreach (Patient patient in patients)
                    UpdateExposure(patient);
            }

            float delta = Game.FrameTime;
            foreach (Patient patient in patients.ToList())
            {
                if (patient.Condition == PatientCondition.CardiacArrest)
                {
                    if (patient.Caregiver == null && Game.GameTime - patient.ConditionSince > CardiacArrestSurvivalTime)
                        SetCondition(patient, PatientCondition.Deceased);
                    continue;
                }

                // Care stops the clock; walking wounded and stable patients don't get worse.
                bool deteriorates = patient.Condition == PatientCondition.Injured || patient.Condition == PatientCondition.Unconscious;
                if (!deteriorates || patient.IsMinor || patient.Caregiver != null || patient.Transport != TransportState.None)
                    continue;

                patient.DeteriorationLeft -= delta * patient.ExposureRate;
                if (patient.DeteriorationLeft > 0f)
                    continue;

                PatientCondition worse = patient.Condition == PatientCondition.Injured ? PatientCondition.Unconscious : PatientCondition.CardiacArrest;
                SetCondition(patient, worse);
                if (patient.Ped.DistanceTo(Game.LocalPlayer.Character) <= WarningDistance)
                    Notify.Warning(worse == PatientCondition.CardiacArrest
                        ? L.T("A patient went into ~r~cardiac arrest~s~! Start CPR.")
                        : L.T("A patient is ~o~deteriorating~s~ and lost consciousness."));
            }

            DrawTriageTags();
        }

        // Smoke and heat at the patient: they get worse faster, inhale smoke and get burned.
        private void UpdateExposure(Patient patient)
        {
            if (patient.Condition == PatientCondition.Deceased || patient.Transport != TransportState.None)
            {
                patient.ExposureRate = 1f;
                return;
            }

            Vector3 position = patient.Ped.Position;
            bool inside = NativeFunction.CallByName<int>("GET_INTERIOR_FROM_ENTITY", patient.Ped) != 0;
            float smoke = fire.GetSmoke(position, inside);
            float heat = fire.GetHeat(position);
            patient.ExposureRate = 1f + SmokeExposureFactor * smoke + HeatExposureFactor * heat;

            if (heat >= BurnHeat && patient.Injury != InjuryType.Burns)
            {
                patient.Injury = InjuryType.Burns;
                patient.IsMinor = false;
                if (patient.Condition == PatientCondition.Stable)
                    SetCondition(patient, PatientCondition.Injured);
            }
            else if (smoke >= InhalationSmoke && (patient.Injury == InjuryType.Trauma || patient.Injury == InjuryType.Medical))
            {
                patient.Injury = InjuryType.SmokeInhalation;
            }
        }

        private void DrawTriageTags()
        {
            Vector3 player = Game.LocalPlayer.Character.Position;
            foreach (Patient patient in patients.Where(p => p.Triage != TriageTag.None && p.Transport == TransportState.None
                && p.Ped.DistanceTo(player) <= TriageMarkerDistance))
            {
                int r = 20, g = 20, b = 20;
                switch (patient.Triage)
                {
                    case TriageTag.Red: r = 230; g = 30; b = 30; break;
                    case TriageTag.Yellow: r = 240; g = 200; b = 0; break;
                    case TriageTag.Green: r = 40; g = 200; b = 60; break;
                }

                Vector3 p = patient.Ped.Position + new Vector3(0f, 0f, 1.1f);
                NativeFunction.CallByName<uint>("DRAW_MARKER", 28, p.X, p.Y, p.Z, 0f, 0f, 0f, 0f, 0f, 0f,
                    0.12f, 0.12f, 0.12f, r, g, b, 220, false, false, 2, false, 0, 0, false);
            }
        }

        private static void ResetDeterioration(Patient patient)
        {
            patient.DeteriorationLeft = patient.Condition == PatientCondition.Injured
                ? MathHelper.GetRandomSingle(300f, 420f)
                : MathHelper.GetRandomSingle(240f, 360f);
        }

        public Interaction GetInteraction()
        {
            Ped player = Game.LocalPlayer.Character;
            if (player.IsInAnyVehicle(false))
                return null;

            Patient carried = patients.FirstOrDefault(p => p.Carrier == player);
            if (carried != null)
                return new Interaction(IsSafe(carried) ? L.T("put the patient down") : L.T("put the patient down (~o~still in danger~s~)"), () => Drop(carried));

            Patient patient = patients
                .Where(p => p.Ped.Exists() && p.Carrier == null && p.Transport == TransportState.None && p.Ped.DistanceTo(player.Position) <= Reach)
                .OrderBy(p => p.Ped.DistanceTo(player.Position))
                .FirstOrDefault();
            if (patient == null)
                return null;

            if (patient.NeedsRescue)
                return new Interaction(L.T("carry the ~r~victim~s~ out"), () => StartCarry(patient, player), CarryPickUpSeconds, L.T("LIFTING"));

            if (patient.NeedsTriage && IsMassCasualty(patient.Ped.Position))
            {
                return CareInteraction(patient, L.T("~y~triage~s~ the patient"), TriageSeconds, L.T("TRIAGE"),
                    "amb@medic@standing@tendtodead@idle_a", "idle_a", () =>
                    {
                        ApplyTriage(patient);
                        Game.DisplaySubtitle(L.F($"Triage: {TagToDisplay(patient.Triage)} · {InjuryToDisplay(patient.Injury)}"), 4000);
                    });
            }

            switch (patient.Condition)
            {
                case PatientCondition.CardiacArrest when tools.IsHolding(EquipmentType.AED):
                    return CareInteraction(patient, L.T("shock the patient with the ~y~AED"), tools.Held.UseSeconds, L.T("ANALYZING"),
                        "amb@medic@standing@tendtodead@idle_a", "idle_a", () => PerformCprCycle(patient, true));

                case PatientCondition.CardiacArrest:
                    return CareInteraction(patient, L.T("perform ~r~CPR"), CprCycleSeconds, L.T("CPR"),
                        "mini@cpr@char_a@cpr_str", "cpr_pumpchest", () => PerformCprCycle(patient, false), cpr: true);

                case PatientCondition.Injured:
                case PatientCondition.Unconscious:
                    return CareInteraction(patient, L.F($"treat the ~r~patient~s~ ({InjuryToDisplay(patient.Injury)})"), GetTreatSeconds(patient), "TREATING",
                        "amb@medic@standing@tendtodead@idle_a", "idle_a", () => Treat(patient));

                case PatientCondition.Stable:
                    return Interaction.Hint(patient.RequiresTransport
                        ? L.T("The patient is ~g~stable~s~. The ~b~ambulance~s~ crew will take them to hospital.")
                        : L.T("The patient is ~g~stable~s~."));

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
                Game.DisplaySubtitle(withAed ? L.T("~o~No shock advised.~s~ Continue CPR.") : L.T("~o~Still no pulse.~s~ Keep going."), 2500);
                return false;
            }

            patient.WasResuscitated = true;
            SetCondition(patient, PatientCondition.Stable);
            Milestone?.Invoke(PatientMilestone.Resuscitated, patient.Ped);
            Notify.Info(L.T("~g~Pulse is back!~s~ The patient was resuscitated."));
            return true;
        }

        public void Treat(Patient patient)
        {
            if (patient.Condition != PatientCondition.Injured && patient.Condition != PatientCondition.Unconscious)
                return;

            SetCondition(patient, PatientCondition.Stable);
            Game.DisplaySubtitle(L.T("Patient ~g~treated~s~ and stable."), 2500);
            Milestone?.Invoke(PatientMilestone.Treated, patient.Ped);
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
                Milestone?.Invoke(PatientMilestone.Rescued, patient.Ped);
                Notify.Info(L.T("Victim ~g~rescued~s~ from the building. Treat them for smoke inhalation."));
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
            SoundHandle metronome = null;
            return new Interaction(prompt, complete, seconds, label)
            {
                AnimationDictionary = dictionary,
                AnimationName = animation,
                Started = () =>
                {
                    patient.Caregiver = player;
                    if (cpr)
                    {
                        patient.Ped.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 8f, AnimationFlags.Loop);
                        // 110 compressions a minute.
                        if (Settings.CprMetronome)
                            metronome = Sound.PlayLooped(CprMetronomeSound, Settings.AlarmVolume);
                    }
                },
                Stopped = () =>
                {
                    metronome?.Stop();
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
            ResetDeterioration(patient);

            // A tagged patient who gets worse is re-tagged; a resuscitated one comes back from black.
            if (patient.Triage != TriageTag.None)
            {
                TriageTag tag = Assess(patient);
                if (tag > patient.Triage || patient.Triage == TriageTag.Black)
                    patient.Triage = tag;
            }
            if (patient.Carrier == null)
                ApplyPose(patient);

            if (condition == PatientCondition.Deceased)
                Notify.Warning(L.T("A patient has ~r~died~s~."));

            ConditionChanged?.Invoke(patient.Ped, condition);
        }

        /// <summary>Plays the animation that fits the patient's condition.</summary>
        public void ApplyPose(Patient patient)
        {
            if (!patient.Ped.Exists() || patient.Carrier != null || patient.Transport != TransportState.None)
                return;

            switch (patient.Condition)
            {
                case PatientCondition.Injured when patient.IsMinor:
                    // Walking wounded sit at the side of the road.
                    patient.Ped.Tasks.PlayAnimation("amb@world_human_picnic@male@base", "base", 8f, AnimationFlags.Loop);
                    break;
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
