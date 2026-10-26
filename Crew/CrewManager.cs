using System;
using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Medical;
using LSFD.UI;
using LSFD.VehicleRescue;
using Rage;
using Rage.Native;

namespace LSFD.Crew
{
    /// <summary>The AI firefighters on all apparatus: spawning them, driving AI units to calls and carrying out orders.</summary>
    internal class CrewManager
    {
        private const string FirefighterModel = "s_m_y_fireman_01";
        private const string ExtinguisherWeapon = "WEAPON_FIREEXTINGUISHER";
        private const uint ThinkInterval = 500;
        private const float SceneRadius = 80f;
        private const float WorkDistance = 1.3f;
        private const float ExtinguishDistance = 3.5f;
        private const int SprayMilliseconds = 2500;
        private const uint FullAutoFiringPattern = 0xC6EE6B4C;
        private const uint LeaveVehicleTimeout = 4000;
        private const uint ExtricateMilliseconds = 15000;
        private const float ArrivalDistance = 30f;

        // AI can't use doors that teleport (e.g. into high-rise interiors), so after this long they "find a way in".
        private const uint NavigationGiveUpTime = 25000;

        private static readonly string[] LastNames =
        {
            "Alvarez", "Brooks", "Carter", "Dunn", "Evans", "Fischer", "Garcia", "Hughes", "Jensen", "Kowalski",
            "Lopez", "Miller", "Nguyen", "O'Brien", "Patel", "Reyes", "Schmidt", "Turner", "Walsh", "Young",
        };

        private readonly PatientManager patients;
        private readonly ExtricationManager extrication;
        private readonly List<CrewMember> crew = new List<CrewMember>();

        public IReadOnlyList<CrewMember> Members => crew;

        public CrewManager(PatientManager patients, ExtricationManager extrication)
        {
            this.patients = patients;
            this.extrication = extrication;
        }

        public void SpawnCrew(FireApparatus apparatus)
        {
            ApparatusDefinition definition = apparatus.Definition;
            int seats = NativeFunction.CallByName<int>("GET_VEHICLE_MAX_NUMBER_OF_PASSENGERS", apparatus.Vehicle);
            int count = Math.Min(seats, MathHelper.GetRandomInteger(definition.MinCrew, definition.MaxCrew + 1));

            // AI units need someone behind the wheel as well.
            int firstSeat = apparatus.IsAiUnit ? -1 : 0;
            if (count <= 0 && firstSeat == 0)
                return;

            Model model = FirefighterModel;
            model.LoadAndWait();

            for (int seat = firstSeat; seat < count; seat++)
            {
                var ped = new Ped(model, apparatus.Vehicle.Position + new Vector3(0f, 0f, 3f), 0f)
                {
                    IsPersistent = true,
                    BlockPermanentEvents = true,
                    RelationshipGroup = Game.LocalPlayer.Character.RelationshipGroup,
                };
                ped.WarpIntoVehicle(apparatus.Vehicle, seat);
                NativeFunction.CallByName<uint>("SET_PED_CAN_BE_DRAGGED_OUT", ped, false);

                string name = "FF " + LastNames[MathHelper.GetRandomInteger(LastNames.Length)];
                crew.Add(new CrewMember(ped, apparatus, seat, name));
            }

            model.Dismiss();
            if (!apparatus.IsAiUnit)
                Notify.Info($"~y~{definition.Name}~s~ crew: {count} firefighter{(count == 1 ? "" : "s")} on board.");
        }

        /// <summary>Gives every crew member the same order.</summary>
        public void Command(CrewOrder order)
        {
            foreach (CrewMember member in crew)
                Command(member, order);

            Game.DisplaySubtitle($"~b~Crew:~s~ {OrderToRadio(order)}", 3000);
        }

        public void Command(CrewMember member, CrewOrder order)
        {
            StopJob(member);
            member.Order = order;
            member.TaskedFor = null;
            member.NextThink = 0;
        }

        public void Process()
        {
            crew.RemoveAll(m => !m.Ped.Exists());

            foreach (FireApparatus unit in crew.Select(m => m.Apparatus).Distinct().Where(a => a.IsAiUnit && a.ResponseDestination.HasValue).ToList())
                ProcessResponse(unit);

            Ped player = Game.LocalPlayer.Character;
            foreach (CrewMember member in crew.Where(m => m.IsValid && Game.GameTime >= m.NextThink))
            {
                member.NextThink = Game.GameTime + ThinkInterval;
                Think(member, player);
            }
        }

        public void Dismiss(FireApparatus apparatus)
        {
            foreach (CrewMember member in crew.Where(m => m.Apparatus == apparatus))
            {
                StopJob(member);
                DismissPed(member.Ped);
            }
            crew.RemoveAll(m => m.Apparatus == apparatus);
        }

        public static string OrderToDisplayName(CrewOrder order)
        {
            switch (order)
            {
                case CrewOrder.FollowPlayer: return "Following";
                case CrewOrder.HoldPosition: return "Holding";
                case CrewOrder.Extinguish: return "Firefighting";
                case CrewOrder.RescueVictims: return "Search & rescue";
                case CrewOrder.TreatPatients: return "Patient care";
                case CrewOrder.WorkScene: return "Working the scene";
                case CrewOrder.ReturnToApparatus: return "Returning";
                default: return "With apparatus";
            }
        }

        public static string JobToDisplayName(CrewJob job)
        {
            switch (job)
            {
                case CrewJob.Extinguish: return "Extinguishing";
                case CrewJob.Carry: return "Rescuing victim";
                case CrewJob.Extricate: return "Extricating";
                case CrewJob.Cpr: return "Performing CPR";
                case CrewJob.Treat: return "Treating patient";
                default: return null;
            }
        }

        // AI units drive themselves to the call and start working the scene once they are there.
        private void ProcessResponse(FireApparatus unit)
        {
            Vehicle vehicle = unit.Vehicle;
            Vector3 destination = unit.ResponseDestination.Value;
            CrewMember driver = crew.FirstOrDefault(m => m.Apparatus == unit && m.IsDriver);

            if (!vehicle.Exists() || driver == null || !driver.IsValid)
            {
                unit.ResponseDestination = null;
                return;
            }

            if (vehicle.DistanceTo(destination) > ArrivalDistance)
            {
                if (driver.TaskedFor == null)
                {
                    driver.Ped.Tasks.DriveToPosition(vehicle, destination, 20f, VehicleDrivingFlags.Emergency, ArrivalDistance - 5f);
                    driver.TaskedFor = CrewOrder.Idle;
                }
                return;
            }

            unit.ResponseDestination = null;
            vehicle.IsSirenSilent = true;
            foreach (CrewMember member in crew.Where(m => m.Apparatus == unit))
                Command(member, CrewOrder.WorkScene);
        }

        private void Think(CrewMember member, Ped player)
        {
            Vehicle vehicle = member.Apparatus.Vehicle;

            // An AI unit on its way stays seated.
            if (member.Apparatus.IsAiUnit && member.Apparatus.ResponseDestination.HasValue)
                return;

            // Everyone mounts up when the player gets behind the wheel, unless they are busy.
            bool playerInApparatus = vehicle.Exists() && player.CurrentVehicle == vehicle;
            if (playerInApparatus && member.Job == CrewJob.None && member.Order != CrewOrder.HoldPosition)
                member.Order = CrewOrder.ReturnToApparatus;

            if (member.Job != CrewJob.None)
            {
                ProcessJob(member);
                return;
            }

            switch (member.Order)
            {
                case CrewOrder.FollowPlayer:
                    if (!GetOut(member))
                        TaskOnce(member, () => FollowPlayer(member, player));
                    break;

                case CrewOrder.HoldPosition:
                    TaskOnce(member, () => member.Ped.Tasks.StandStill(-1));
                    break;

                case CrewOrder.ReturnToApparatus:
                    if (member.Ped.CurrentVehicle == vehicle)
                        member.Order = CrewOrder.Idle;
                    else if (vehicle.Exists())
                        TaskOnce(member, () => member.Ped.Tasks.EnterVehicle(vehicle, member.Seat));
                    break;

                case CrewOrder.Extinguish:
                case CrewOrder.RescueVictims:
                case CrewOrder.TreatPatients:
                case CrewOrder.WorkScene:
                    if (GetOut(member))
                        break;
                    if (!PickJob(member))
                        NothingToDo(member);
                    break;
            }
        }

        private bool PickJob(CrewMember member)
        {
            Vector3 position = member.Ped.Position;
            bool rescue = member.Order == CrewOrder.RescueVictims || member.Order == CrewOrder.WorkScene;
            bool care = member.Order == CrewOrder.TreatPatients || member.Order == CrewOrder.WorkScene;
            bool fire = member.Order == CrewOrder.Extinguish || member.Order == CrewOrder.WorkScene;

            if (care && TryClaimPatient(member, p => p.NeedsCare && p.Condition == PatientCondition.CardiacArrest, CrewJob.Cpr))
                return true;
            if (rescue && TryClaimPatient(member, p => p.NeedsRescue, CrewJob.Carry))
                return true;

            if (rescue)
            {
                Ped trapped = extrication.FindUnclaimedTrapped(position, SceneRadius);
                if (trapped != null)
                {
                    extrication.Claim(trapped, member.Ped);
                    member.JobTrapped = trapped;
                    member.StartJob(CrewJob.Extricate);
                    return true;
                }
            }

            if (care && TryClaimPatient(member, p => p.NeedsCare, CrewJob.Treat))
                return true;

            if (fire && FindFire(position) != null)
            {
                member.StartJob(CrewJob.Extinguish);
                return true;
            }

            return false;
        }

        private bool TryClaimPatient(CrewMember member, Func<Patient, bool> filter, CrewJob job)
        {
            Patient patient = patients.FindNearest(member.Ped.Position, SceneRadius, filter);
            if (patient == null)
                return false;

            patient.Caregiver = member.Ped;
            member.JobPatient = patient;
            member.StartJob(job);
            return true;
        }

        private void NothingToDo(CrewMember member)
        {
            // Player crews come back to their officer; AI units wait on scene.
            if (member.Order == CrewOrder.WorkScene || member.Apparatus.IsAiUnit)
            {
                TaskOnce(member, () => member.Ped.Tasks.StandStill(-1));
                return;
            }

            Game.DisplaySubtitle($"~b~{member.Name}:~s~ Nothing left to do here, coming to you.", 3000);
            Command(member, CrewOrder.FollowPlayer);
        }

        private void ProcessJob(CrewMember member)
        {
            switch (member.Job)
            {
                case CrewJob.Extinguish: DoExtinguish(member); break;
                case CrewJob.Carry: DoCarry(member); break;
                case CrewJob.Extricate: DoExtricate(member); break;
                case CrewJob.Cpr: DoCpr(member); break;
                case CrewJob.Treat: DoTreat(member); break;
            }
        }

        private void DoExtinguish(CrewMember member)
        {
            Ped ped = member.Ped;
            Fire fire = FindFire(ped.Position);
            if (fire == null)
            {
                StopJob(member);
                return;
            }

            uint extinguisher = Game.GetHashKey(ExtinguisherWeapon);
            if (!NativeFunction.CallByName<bool>("HAS_PED_GOT_WEAPON", ped, extinguisher, false))
                ped.Inventory.GiveNewWeapon(ExtinguisherWeapon, 2000, true);
            NativeFunction.CallByName<uint>("SET_CURRENT_PED_WEAPON", ped, extinguisher, true);

            Vector3 target = fire.Position;
            if (ped.DistanceTo(target) > ExtinguishDistance)
                ped.Tasks.FollowNavigationMeshToPosition(target, 0f, 2f, ExtinguishDistance - 0.5f);
            else
                NativeFunction.CallByName<uint>("TASK_SHOOT_AT_COORD", ped, target.X, target.Y, target.Z, SprayMilliseconds, FullAutoFiringPattern);
        }

        private void DoCarry(CrewMember member)
        {
            Patient patient = member.JobPatient;
            if (!patient.Ped.Exists() || (member.JobPhase == 0 && !patient.NeedsRescue))
            {
                StopJob(member);
                return;
            }

            if (member.JobPhase == 0)
            {
                if (!MoveTo(member, patient.Ped.Position, WorkDistance))
                    return;
                patients.StartCarry(patient, member.Ped);
                member.NextPhase();
            }
            else if (MoveTo(member, patient.SafePosition, 2f))
            {
                patients.Drop(patient);
                Game.DisplaySubtitle($"~b~{member.Name}:~s~ Victim is out!", 3000);
                StopJob(member);
            }
        }

        private void DoExtricate(CrewMember member)
        {
            Ped trapped = member.JobTrapped;
            if (!trapped.Exists() || !extrication.IsTrapped(trapped))
            {
                StopJob(member);
                return;
            }

            if (member.JobPhase == 0)
            {
                if (!MoveTo(member, extrication.GetWorkPosition(trapped), 0.8f))
                    return;
                member.Ped.Tasks.PlayAnimation("mini@repair", "fixing_a_player", 4f, AnimationFlags.Loop);
                member.NextPhase();
            }
            else if (Game.GameTime - member.JobPhaseStartedAt > ExtricateMilliseconds)
            {
                extrication.ExtricateByCrew(trapped);
                Game.DisplaySubtitle($"~b~{member.Name}:~s~ Patient is out of the vehicle.", 3000);
                StopJob(member);
            }
        }

        private void DoCpr(CrewMember member)
        {
            Patient patient = member.JobPatient;
            if (!patient.Ped.Exists() || patient.Condition != PatientCondition.CardiacArrest)
            {
                StopJob(member);
                return;
            }

            if (member.JobPhase == 0)
            {
                if (!MoveTo(member, patient.Ped.Position, WorkDistance))
                    return;
                member.Ped.Tasks.PlayAnimation("mini@cpr@char_a@cpr_str", "cpr_pumpchest", 4f, AnimationFlags.Loop);
                patient.Ped.Tasks.PlayAnimation("mini@cpr@char_b@cpr_str", "cpr_pumpchest", 4f, AnimationFlags.Loop);
                member.NextPhase();
            }
            else if (Game.GameTime - member.JobPhaseStartedAt > PatientManager.CprCycleSeconds * 1000)
            {
                bool hasAed = member.Apparatus.Definition.Carries(EquipmentType.AED);
                if (patients.PerformCprCycle(patient, hasAed))
                    StopJob(member);
                else
                    member.JobPhaseStartedAt = Game.GameTime;
            }
        }

        private void DoTreat(CrewMember member)
        {
            Patient patient = member.JobPatient;
            if (!patient.Ped.Exists() || !patient.NeedsCare)
            {
                StopJob(member);
                return;
            }

            if (member.JobPhase == 0)
            {
                if (!MoveTo(member, patient.Ped.Position, WorkDistance))
                    return;
                member.Ped.Tasks.PlayAnimation("amb@medic@standing@tendtodead@idle_a", "idle_a", 4f, AnimationFlags.Loop);
                member.NextPhase();
            }
            else if (Game.GameTime - member.JobPhaseStartedAt > PatientManager.TreatSeconds * 1000)
            {
                patients.Treat(patient);
                StopJob(member);
            }
        }

        /// <summary>Walks (runs) to <paramref name="target"/>; returns true once there.</summary>
        private static bool MoveTo(CrewMember member, Vector3 target, float distance)
        {
            Ped ped = member.Ped;
            if (ped.DistanceTo(target) <= distance + 0.3f)
                return true;

            if (!member.NavigatingTo.HasValue || member.NavigatingTo.Value.DistanceTo(target) > 1f)
            {
                ped.Tasks.FollowNavigationMeshToPosition(target, 0f, 2f, distance);
                member.NavigatingTo = target;
            }
            else if (Game.GameTime - member.JobPhaseStartedAt > NavigationGiveUpTime)
            {
                ped.Position = target.Around2D(distance * 0.5f);
                member.NavigatingTo = null;
                return true;
            }

            return false;
        }

        private void StopJob(CrewMember member)
        {
            if (member.JobPatient != null)
            {
                if (member.JobPatient.Carrier == member.Ped)
                    patients.Drop(member.JobPatient);
                if (member.JobPatient.Caregiver == member.Ped)
                    member.JobPatient.Caregiver = null;
                patients.ApplyPose(member.JobPatient);
            }
            if (member.JobTrapped != null)
                extrication.Claim(member.JobTrapped, null);

            member.Job = CrewJob.None;
            member.JobPatient = null;
            member.JobTrapped = null;
            member.TaskedFor = null;
            if (member.Ped.Exists())
                member.Ped.Tasks.Clear();
        }

        private static Fire FindFire(Vector3 position)
        {
            return World.GetAllFires()
                .Where(f => f.IsValid() && f.Position.DistanceTo(position) <= SceneRadius)
                .OrderBy(f => f.Position.DistanceTo(position))
                .FirstOrDefault();
        }

        private static void TaskOnce(CrewMember member, Action giveTask)
        {
            if (member.TaskedFor == member.Order)
                return;

            giveTask();
            member.TaskedFor = member.Order;
        }

        /// <summary>Makes the crew member leave their vehicle; returns true while they are still in it.</summary>
        private static bool GetOut(CrewMember member)
        {
            if (!member.Ped.IsInAnyVehicle(false))
                return false;

            if (Game.GameTime - member.LeaveVehicleIssuedAt > LeaveVehicleTimeout)
            {
                member.Ped.Tasks.LeaveVehicle(LeaveVehicleFlags.None);
                member.LeaveVehicleIssuedAt = Game.GameTime;
                member.TaskedFor = null;
            }
            return true;
        }

        private static void FollowPlayer(CrewMember member, Ped player)
        {
            // Spread the crew out behind the player instead of stacking them on one spot.
            int slot = member.Seat + 1;
            float side = (slot % 2 == 0 ? -1f : 1f) * (1f + slot / 2);
            NativeFunction.CallByName<uint>("TASK_FOLLOW_TO_OFFSET_OF_ENTITY", member.Ped, player, side, -2f - slot / 2, 0f, 2f, -1, 2f, true);
        }

        private static void DismissPed(Ped ped)
        {
            if (!ped.Exists())
                return;

            ped.BlockPermanentEvents = false;
            ped.Dismiss();
        }

        private static string OrderToRadio(CrewOrder order)
        {
            switch (order)
            {
                case CrewOrder.FollowPlayer: return "On you, chief.";
                case CrewOrder.HoldPosition: return "Holding position.";
                case CrewOrder.Extinguish: return "Grabbing extinguishers, attacking the fire.";
                case CrewOrder.RescueVictims: return "Starting search and rescue.";
                case CrewOrder.TreatPatients: return "Taking care of the patients.";
                case CrewOrder.WorkScene: return "Copy, working the scene.";
                case CrewOrder.ReturnToApparatus: return "Returning to the apparatus.";
                default: return "Copy.";
            }
        }
    }
}
