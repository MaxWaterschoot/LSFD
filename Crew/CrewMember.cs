using LSFD.Apparatus;
using LSFD.Medical;
using Rage;

namespace LSFD.Crew
{
    public enum CrewOrder
    {
        /// <summary>Stay with the apparatus.</summary>
        Idle,
        FollowPlayer,
        HoldPosition,
        /// <summary>Grab an extinguisher and put out the nearest fires.</summary>
        Extinguish,
        /// <summary>Carry victims out of buildings and extricate people trapped in vehicles.</summary>
        RescueVictims,
        /// <summary>CPR and first aid for every patient on scene.</summary>
        TreatPatients,
        /// <summary>Do whatever is most urgent: CPR, rescues, extrication, treatment, then firefighting.</summary>
        WorkScene,
        ReturnToApparatus,
    }

    internal enum CrewJob
    {
        None,
        Extinguish,
        Carry,
        Extricate,
        Cpr,
        Treat,
    }

    /// <summary>An AI firefighter riding on an apparatus.</summary>
    internal class CrewMember
    {
        public Ped Ped { get; }
        public FireApparatus Apparatus { get; }
        public int Seat { get; }
        public string Name { get; }

        public bool IsDriver => Seat == -1;

        public CrewOrder Order { get; set; } = CrewOrder.Idle;

        /// <summary>The order the current game task was given for, so tasks are only (re)issued when needed.</summary>
        public CrewOrder? TaskedFor { get; set; }

        public uint NextThink { get; set; }

        /// <summary>When the crew member was last told to get out, so the task isn't restarted every think.</summary>
        public uint LeaveVehicleIssuedAt { get; set; }

        public CrewJob Job { get; set; }
        public Patient JobPatient { get; set; }
        public Ped JobTrapped { get; set; }
        public int JobPhase { get; set; }
        public uint JobPhaseStartedAt { get; set; }
        public Vector3? NavigatingTo { get; set; }

        public bool IsValid => Ped.Exists() && !Ped.IsDead;

        public CrewMember(Ped ped, FireApparatus apparatus, int seat, string name)
        {
            Ped = ped;
            Apparatus = apparatus;
            Seat = seat;
            Name = name;
        }

        public void StartJob(CrewJob job)
        {
            Job = job;
            JobPhase = 0;
            JobPhaseStartedAt = Game.GameTime;
            NavigatingTo = null;
        }

        public void NextPhase()
        {
            JobPhase++;
            JobPhaseStartedAt = Game.GameTime;
            NavigatingTo = null;
        }
    }
}
