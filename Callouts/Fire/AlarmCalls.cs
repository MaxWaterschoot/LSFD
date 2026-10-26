using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Buildings;
using LSFD.Core;
using LSFD.Utils;
using Rage;
using Rage.Native;

namespace LSFD.Callouts
{
    /// <summary>An investigation in a building from FireLocations.xml: mostly nothing, sometimes a real fire.</summary>
    internal abstract class BuildingInvestigation : Callout
    {
        protected readonly Scene Scene = new Scene();
        protected BuildingInfo Building { get; private set; }
        protected FireIncident Fire { get; private set; }

        protected abstract FireLocationKind[] Kinds { get; }
        protected abstract string Title { get; }
        protected abstract string Describe(BuildingInfo building);

        /// <summary>Set once the investigation is over; the call completes when any fire is out as well.</summary>
        protected bool IsResolved { get; set; }

        public override bool OnBeforeCalloutDisplayed()
        {
            Building = Interiors.GetRandomBuilding(300f, 2500f, Kinds);
            if (Building == null)
                return false;

            CalloutPosition = Building.Staging;
            CalloutMessage = Title;
            CalloutAdvisory = Describe(Building);
            CalloutAreaRadius = 30f;
            return true;
        }

        protected override void OnArrival()
        {
            Interiors.PrepareInterior(Building.Position);
            Investigate();
        }

        protected abstract void Investigate();

        /// <summary>It turned out to be a real fire: a small one, that grows like any other.</summary>
        protected void StartRealFire(Vector3 position, FireClass fireClass, Vector3? powerPanel = null)
        {
            Fire = StartFireIncident(position, new FireIncidentOptions
            {
                IsIndoor = true,
                InitialPoints = 1,
                InitialIntensity = 0.3f,
                SpreadRadius = 8f,
                SmokeColumnHeight = 8f,
                Class = fireClass,
                PowerPanel = powerPanel,
            });
            Priority = CalloutPriority.Priority1;
        }

        public override void Process()
        {
            if (HasArrived && IsResolved && (Fire == null || Fire.IsOut))
                Complete();
        }

        protected override void OnEnd()
        {
            Scene.Dismiss();
        }
    }

    [CalloutInfo("Automatic Fire Alarm", CalloutProbability.High, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority2)]
    internal class AutomaticFireAlarm : BuildingInvestigation
    {
        private enum Cause { FaultyDetector, BurntFood, RealFire }

        private Cause cause;
        private int alarmSound = -1;
        private PositionalSound alarm;

        protected override FireLocationKind[] Kinds => new[]
        {
            FireLocationKind.Store, FireLocationKind.Commercial, FireLocationKind.Office, FireLocationKind.Public, FireLocationKind.Industrial,
        };

        protected override string Title => L.T("Automatic Fire Alarm");

        protected override string Describe(BuildingInfo building) =>
            L.F($"~y~{building.Name}~s~. The fire alarm system went off, no caller on the line. Investigate.");

        public override bool OnBeforeCalloutDisplayed()
        {
            // Most automatic alarms are false: a faulty detector or someone's cooking.
            int roll = MathHelper.GetRandomInteger(100);
            cause = roll < 50 ? Cause.FaultyDetector : roll < 80 ? Cause.BurntFood : Cause.RealFire;
            return base.OnBeforeCalloutDisplayed();
        }

        protected override void Investigate()
        {
            Vector3 p = Building.Position;
            if (Settings.SceneAlarms)
                alarm = Sound.PlayAt("Alarms/SMOKE_DETECTOR.wav", p, Settings.AlarmVolume, 45f);
            if (alarm == null)
            {
                alarmSound = NativeFunction.CallByName<int>("GET_SOUND_ID");
                NativeFunction.CallByName<uint>("PLAY_SOUND_FROM_COORD", alarmSound, "Burglar_Bell", p.X, p.Y, p.Z, "Generic_Alarms", false, 0, false);
            }

            AddSceneAction(new SceneAction(p, L.T("check the ~y~fire alarm panel"), ReadPanel) { HoldSeconds = 5f, ProgressLabel = L.T("CHECKING") });
            Game.DisplayHelp(L.T("Go inside and check the ~y~fire alarm panel~s~ to find out which detector went off."));
        }

        private void ReadPanel()
        {
            switch (cause)
            {
                case Cause.FaultyDetector:
                    Game.DisplaySubtitle(L.T("Panel: ~y~detector fault~s~ in zone 3. No smoke, no fire."), 5000);
                    AddReset(Building.Position);
                    break;

                case Cause.BurntFood:
                    Game.DisplaySubtitle(L.T("Panel: smoke detector in the ~y~kitchen~s~. Somebody burnt their lunch."), 5000);
                    Vector3 kitchen = Building.Position.Around2D(2f, 4f);
                    AddSceneAction(new SceneAction(kitchen, L.T("~b~ventilate~s~ the kitchen"), () => AddReset(Building.Position))
                    {
                        HoldSeconds = 6f,
                        ProgressLabel = L.T("VENTILATING"),
                    });
                    break;

                case Cause.RealFire:
                    StopAlarm();
                    StartRealFire(Building.Position.Around2D(2f, 4f), FireClass.Ordinary);
                    Functions.DisplayNotification(L.T("~r~Working fire"), L.F($"Smoke and flames inside ~y~{Building.Name}~s~. Upgraded to a structure fire."));
                    IsResolved = true;
                    break;
            }
        }

        private void AddReset(Vector3 panel)
        {
            AddSceneAction(new SceneAction(panel, L.T("~g~reset~s~ the fire alarm"), () =>
            {
                StopAlarm();
                Game.DisplaySubtitle(L.T("Alarm ~g~reset~s~. Building handed back to the owner."), 4000);
                IsResolved = true;
            }) { HoldSeconds = 3f, ProgressLabel = L.T("RESETTING") });
        }

        private void StopAlarm()
        {
            alarm?.Stop();
            alarm = null;
            if (alarmSound < 0)
                return;

            NativeFunction.CallByName<uint>("STOP_SOUND", alarmSound);
            NativeFunction.CallByName<uint>("RELEASE_SOUND_ID", alarmSound);
            alarmSound = -1;
        }

        protected override void OnEnd()
        {
            StopAlarm();
            base.OnEnd();
        }
    }

    [CalloutInfo("Smell of Smoke", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority2)]
    internal class SmellOfSmoke : BuildingInvestigation
    {
        private readonly List<SceneAction> searches = new List<SceneAction>();
        private int sourceIndex;

        protected override FireLocationKind[] Kinds => new[]
        {
            FireLocationKind.Store, FireLocationKind.Commercial, FireLocationKind.Office, FireLocationKind.Public,
        };

        protected override string Title => L.T("Smell of Smoke");

        protected override string Describe(BuildingInfo building) =>
            L.F($"~y~{building.Name}~s~. Staff smell something burning, no visible smoke. Locate the source.");

        protected override void Investigate()
        {
            // Half the time it is overheated wiring behind a wall, otherwise nothing is found.
            sourceIndex = MathHelper.GetRandomInteger(2) == 0 ? MathHelper.GetRandomInteger(3) : -1;

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Vector3 position = Building.Position.Around2D(1.5f, 5f);
                searches.Add(AddSceneAction(new SceneAction(position, L.T("search for the ~y~source~s~ of the smell"), () => Searched(index, position))
                {
                    HoldSeconds = 4f,
                    ProgressLabel = L.T("SEARCHING"),
                    AnimationDictionary = "",
                }));
            }

            Game.DisplayHelp(L.T("Search the building. The ~y~thermal imaging camera~s~ shows heat behind walls."));
        }

        private void Searched(int index, Vector3 position)
        {
            if (index == sourceIndex)
            {
                Game.DisplaySubtitle(L.T("~r~Overheated wiring~s~ is smoldering behind the wall!"), 5000);
                StartRealFire(position, FireClass.Electrical, Building.Position);
                foreach (SceneAction search in searches)
                    search.Remove();
                IsResolved = true;
                Functions.DisplayNotification(L.T("~r~Electrical fire"), L.T("Isolate the power at the panel (~y~yellow arrow~s~) before you use water."));
                return;
            }

            Game.DisplaySubtitle(L.T("Nothing here."), 2000);
            if (searches.All(s => s.IsDone))
            {
                Game.DisplaySubtitle(L.T("Nothing found inside. Probably a barbecue next door."), 5000);
                IsResolved = true;
            }
        }
    }

    [CalloutInfo("Carbon Monoxide Alarm", CalloutProbability.Medium, Category = CalloutCategory.Hazmat, Priority = CalloutPriority.Priority2, RequiresOpenInteriors = true)]
    internal class CarbonMonoxideAlarm : BuildingInvestigation
    {
        private readonly List<Ped> occupants = new List<Ped>();
        private ToxicAtmosphere carbonMonoxide;
        private PositionalSound detector;
        private int occupantCount;

        protected override FireLocationKind[] Kinds => new[] { FireLocationKind.House, FireLocationKind.Apartment };
        protected override string Title => L.T("Carbon Monoxide Alarm");

        public override bool OnBeforeCalloutDisplayed()
        {
            occupantCount = MathHelper.GetRandomInteger(0, 3);
            return base.OnBeforeCalloutDisplayed();
        }

        protected override string Describe(BuildingInfo building) =>
            occupantCount > 0
                ? L.F($"~y~{building.Name}~s~. CO detector sounding, ~r~occupants feel dizzy~s~.")
                : L.F($"~y~{building.Name}~s~. CO detector sounding, occupants are outside.");

        protected override void Investigate()
        {
            carbonMonoxide = Hazards.AddToxicAtmosphere(Building.Position, 15f, 0.6f);
            if (Settings.SceneAlarms)
                detector = Sound.PlayAt("Alarms/CO_ALARM.wav", Building.Position, Settings.AlarmVolume, 30f);

            for (int i = 0; i < occupantCount; i++)
            {
                var condition = MathHelper.GetRandomInteger(2) == 0 ? PatientCondition.Unconscious : PatientCondition.Injured;
                occupants.Add(Patients.SpawnTrappedInBuilding(Building.Position.Around2D(1f, 4f), Building.Staging, condition));
            }

            AddSceneAction(new SceneAction(Building.Position, L.T("~y~measure~s~ the CO level"), Measured)
            {
                RequiredEquipment = EquipmentType.GasDetector,
                HoldSeconds = 4f,
                ProgressLabel = L.T("MEASURING"),
                AnimationDictionary = "",
            });
            Game.DisplayHelp(L.F($"Carbon monoxide is invisible: put your ~b~SCBA~s~ on ({Settings.MaskKey}) and measure with the ~y~gas detector~s~."));
        }

        private void Measured()
        {
            int ppm = MathHelper.GetRandomInteger(250, 600);
            Game.DisplaySubtitle(L.F($"Gas detector: ~r~CO {ppm} ppm~s~. Get everyone out and ventilate!"), 5000);

            AddSceneAction(new SceneAction(Building.Position.Around2D(2f, 4f), L.T("~b~ventilate~s~ (open windows and doors)"), Ventilated)
            {
                HoldSeconds = 8f,
                ProgressLabel = L.T("VENTILATING"),
            });
        }

        private void Ventilated()
        {
            carbonMonoxide.Strength = 0.1f;
            Game.DisplaySubtitle(L.T("Windows and doors open, the gas is clearing."), 4000);
            AddSceneAction(new SceneAction(Building.Position, L.T("~y~measure~s~ again"), () =>
            {
                carbonMonoxide.Remove();
                StopDetector();
                Game.DisplaySubtitle(L.T("Gas detector: ~g~CO 0 ppm~s~. Tell the occupants to have the boiler checked."), 5000);
                IsResolved = true;
            })
            {
                RequiredEquipment = EquipmentType.GasDetector,
                HoldSeconds = 4f,
                ProgressLabel = L.T("MEASURING"),
                AnimationDictionary = "",
            });
        }

        public override void Process()
        {
            if (HasArrived && IsResolved && Patients.AreAllHandled(occupants.ToArray()))
                Complete();
        }

        private void StopDetector()
        {
            detector?.Stop();
            detector = null;
        }

        protected override void OnEnd()
        {
            carbonMonoxide?.Remove();
            StopDetector();
            foreach (Ped occupant in occupants.Where(o => o.Exists()))
            {
                Patients.Release(occupant);
                occupant.Dismiss();
            }
            base.OnEnd();
        }
    }
}
