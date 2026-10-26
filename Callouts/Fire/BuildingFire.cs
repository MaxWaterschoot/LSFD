using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSFD.API;
using LSFD.Buildings;
using LSFD.Core;
using LSFD.Utils;
using Rage;

namespace LSFD.Callouts
{
    /// <summary>
    /// A fire inside a building from FireLocations.xml. The alarm level decides how big the fire starts and how many
    /// occupants are trapped; they have to be carried out and treated before the call is over. The fire starts burning
    /// when the call is accepted, so a slow response means a bigger fire and maybe a higher alarm. The caller waits in
    /// front: they know who is still inside and where, and that is marked on the map once the player has talked to them.
    /// </summary>
    internal abstract class BuildingFire : Callout
    {
        private const float ReportedAreaRadius = 5f;

        private readonly List<Ped> victims = new List<Ped>();
        private readonly Dictionary<Ped, Blip> reportedVictims = new Dictionary<Ped, Blip>();
        private readonly Scene scene = new Scene();

        private BuildingInfo building;
        private FireIncident fire;
        private PositionalSound smokeDetector;
        private int trappedCount;

        protected abstract FireLocationKind[] Kinds { get; }
        protected abstract string Title { get; }

        /// <summary>Most occupants that can be trapped at a 1st alarm; more at higher alarms.</summary>
        protected virtual int BaseOccupants => 1;

        protected virtual bool IsGasFire => false;

        /// <summary>Homes have smoke detectors that keep sounding until the fire is out.</summary>
        protected virtual bool HasSmokeDetectors => false;

        public override bool OnBeforeCalloutDisplayed()
        {
            building = Interiors.GetRandomBuilding(300f, 3000f, Kinds);
            if (building == null)
                return false;

            AlarmLevel = RollAlarmLevel();
            trappedCount = MathHelper.GetRandomInteger(0, BaseOccupants + AlarmLevel);

            CalloutPosition = building.Staging;
            CalloutMessage = Title;
            CalloutAreaRadius = 30f + 10f * AlarmLevel;
            CalloutAdvisory = $"~y~{building.Name}~s~. " + (trappedCount > 0
                ? (trappedCount == 1 ? L.T("Caller reports ~r~1 occupant trapped~s~ inside.") : L.F($"Caller reports ~r~{trappedCount} occupants trapped~s~ inside."))
                : L.T("Occupants are reported out of the building."));
            return true;
        }

        public override bool OnCalloutAccepted()
        {
            fire = StartFireIncident(building.Position, new FireIncidentOptions
            {
                IsIndoor = true,
                IsGasFire = IsGasFire,
                InitialPoints = 1 + AlarmLevel,
                SpreadRadius = 6f + 3f * AlarmLevel,
                SmokeColumnHeight = 8f,
            });
            return true;
        }

        protected override void OnArrival()
        {
            Interiors.PrepareInterior(building.Position);
            // Closed doors between the street and the fire have to be forced.
            Interiors.LockDoors(building.Position, 20f, 0.6f);
            if (HasSmokeDetectors && Settings.SceneAlarms)
                smokeDetector = Sound.PlayAt("Alarms/SMOKE_DETECTOR.wav", building.Position, Settings.AlarmVolume, 35f);

            for (int i = 0; i < trappedCount; i++)
            {
                var condition = MathHelper.GetRandomInteger(4) == 0 ? PatientCondition.Injured : PatientCondition.Unconscious;
                victims.Add(Patients.SpawnTrappedInBuilding(building.Position.Around2D(1f, 4f), building.Staging, condition));
            }

            scene.SpawnCaller(building.Staging.Around2D(3f, 6f), L.T("Caller"), CallerStatement(), MarkReportedVictims);
            int bystanders = MathHelper.GetRandomInteger(0, 3);
            for (int i = 0; i < bystanders; i++)
                scene.SpawnBystander(building.Staging.Around2D(4f, 9f));

            Game.DisplayHelp((trappedCount > 0
                ? L.T("Talk to the ~y~caller~s~, search the building and ~r~carry the victims out~s~, then extinguish the ~r~fire~s~. Your crew can help from the tablet.")
                : L.T("Stretch a ~b~hose line~s~ into the building and extinguish the ~r~fire~s~. The ~y~caller~s~ is waiting out front."))
                + L.F($" Put your ~b~SCBA~s~ face piece on ({Settings.MaskKey}) before you go into the smoke."));
        }

        // Who is still inside and where, and how it started.
        private string[] CallerStatement()
        {
            var lines = new List<string>();
            if (victims.Count == 0)
                lines.Add(CallerLines.Pick(L.T("Everybody's out, I counted twice."), L.T("We all got out, thank God. Nobody's inside.")));

            for (int i = 0; i < victims.Count && i < 3; i++)
            {
                Ped victim = victims[i];
                string where = CallerLines.WhereInBuilding(victim.Position, building.Position, building.Staging);
                lines.Add(i == 0
                    ? L.F($"Please, help! My {CallerLines.Relation(victim)} is still inside, {where}!")
                    : L.F($"And my {CallerLines.Relation(victim)}, {where}. I think."));

                if (i == 0)
                {
                    string he = CallerLines.Capital(CallerLines.He(victim));
                    lines.Add(Patients.GetCondition(victim) == PatientCondition.Unconscious
                        ? L.F($"{he}'s not answering when I call {CallerLines.Him(victim)}!")
                        : L.F($"{he} was screaming for help, now I can't hear {CallerLines.Him(victim)} anymore!"));
                }
            }
            if (victims.Count > 3)
                lines.Add(L.T("There's more people in there, I don't know where!"));

            lines.Add(IsGasFire
                ? L.T("It smelled like gas right before it went up.")
                : CallerLines.Pick(L.T("It started in the kitchen, it spread so fast."), L.T("I heard a bang and then there was smoke everywhere."),
                    L.T("The smoke alarm woke me up, the hallway was already full of smoke.")));
            return lines.ToArray();
        }

        // What the caller said goes on the map: a search area around every victim they know about.
        private void MarkReportedVictims()
        {
            foreach (Ped victim in victims.Take(3).Where(v => v.Exists() && !Patients.IsRescued(v)))
                reportedVictims[victim] = new Blip(victim.Position.Around2D(0f, 2.5f), ReportedAreaRadius) { Color = Color.FromArgb(150, 255, 200, 0), Name = L.T("Reported victim") };

            if (reportedVictims.Count > 0)
                Functions.DisplayNotification(L.T("~y~Caller"), L.F($"Where the caller last saw {(reportedVictims.Count == 1 ? L.T("the victim") : L.T("the victims"))} is marked on your map."));
        }

        public override void Process()
        {
            foreach (Ped found in reportedVictims.Keys.Where(v => !v.Exists() || Patients.IsRescued(v)).ToList())
                RemoveReported(found);

            if (fire.IsOut)
                StopSmokeDetector();

            if (HasArrived && fire.IsOut && Patients.AreAllHandled(victims.ToArray()))
                Complete();
        }

        private void StopSmokeDetector()
        {
            smokeDetector?.Stop();
            smokeDetector = null;
        }

        private void RemoveReported(Ped victim)
        {
            if (reportedVictims[victim].Exists())
                reportedVictims[victim].Delete();
            reportedVictims.Remove(victim);
        }

        protected override void OnEnd()
        {
            foreach (Ped victim in reportedVictims.Keys.ToList())
                RemoveReported(victim);
            StopSmokeDetector();

            foreach (Ped victim in victims.Where(v => v.Exists()))
            {
                Patients.Release(victim);
                victim.Dismiss();
            }
            scene.Dismiss();
        }
    }

    [CalloutInfo("Commercial Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1)]
    internal class CommercialFire : BuildingFire
    {
        protected override FireLocationKind[] Kinds => new[] { FireLocationKind.Store, FireLocationKind.Commercial, FireLocationKind.Office, FireLocationKind.Public };
        protected override string Title => L.T("Commercial Structure Fire");
    }

    [CalloutInfo("Apartment Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1, RequiresOpenInteriors = true)]
    internal class ApartmentFire : BuildingFire
    {
        protected override FireLocationKind[] Kinds => new[] { FireLocationKind.Apartment };
        protected override string Title => L.T("Apartment Fire");
        protected override int BaseOccupants => 2;
        protected override bool HasSmokeDetectors => true;
    }

    [CalloutInfo("House Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1, RequiresOpenInteriors = true)]
    internal class HouseFire : BuildingFire
    {
        protected override FireLocationKind[] Kinds => new[] { FireLocationKind.House };
        protected override string Title => L.T("Residential Structure Fire");
        protected override int BaseOccupants => 2;
        protected override bool HasSmokeDetectors => true;
    }

    [CalloutInfo("Industrial Building Fire", CalloutProbability.Low, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority1, RequiresOpenInteriors = true)]
    internal class IndustrialBuildingFire : BuildingFire
    {
        protected override FireLocationKind[] Kinds => new[] { FireLocationKind.Industrial };
        protected override string Title => L.T("Industrial Building Fire");
        protected override bool IsGasFire => true;
    }
}
