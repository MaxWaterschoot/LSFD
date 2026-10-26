using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Stations;
using LSFD.UI;
using Rage;
using Rage.Native;

namespace LSFD.LivingWorld
{
    /// <summary>A unit of another station that is committed to an <see cref="OtherIncident"/>.</summary>
    internal class CommittedUnit
    {
        public string Name;
        public FireStation Station;
        public ApparatusType Type;
        public uint ArrivesAt;
        public bool OnScene;

        /// <summary>The vehicle in the world when the unit responds near the player; null while it is only simulated.</summary>
        public FireApparatus Vehicle;
    }

    /// <summary>A call handled by other AI units, somewhere else in the city.</summary>
    internal class OtherIncident
    {
        public string Name;
        public Vector3 Position;
        public uint OnSceneTime;
        public uint? ClearsAt;
        public Blip Blip;
        public readonly List<CommittedUnit> Units = new List<CommittedUnit>();

        public string Street => World.GetStreetName(Position);
        public string UnitNames => string.Join(", ", Units.Select(u => u.Name));
    }

    /// <summary>
    /// The rest of the department: other stations handle their own calls. Far from the player these are simulated
    /// (travel time and time on scene); near the player the units really drive there with lights and sirens.
    /// A busy unit isn't sent as the player's backup, and calls the player declines are taken over.
    /// </summary>
    internal class OtherUnits
    {
        // Average speed through traffic and the time to get going, for simulated units.
        private const float ResponseSpeed = 14f;
        private const uint TurnoutTime = 45000;

        // A real unit that hasn't made it after this much extra time is counted as there.
        private const uint StuckTime = 120000;

        // A station or incident this close to the player gets a real vehicle on the road.
        private const float RealUnitRange = 600f;
        private const float MinIncidentDistance = 400f;
        private const float MaxIncidentDistance = 3000f;
        private const float MinDistanceToPlayerCall = 300f;
        private const uint UpdateInterval = 1000;

        private class IncidentType
        {
            public string Name;
            public int MinMinutes;
            public int MaxMinutes;
            public ApparatusType[] Units;

            public IncidentType(string name, int minMinutes, int maxMinutes, params ApparatusType[] units)
            {
                Name = name;
                MinMinutes = minMinutes;
                MaxMinutes = maxMinutes;
                Units = units;
            }
        }

        private static readonly IncidentType[] Types =
        {
            new IncidentType("Structure Fire", 6, 12, ApparatusType.Engine, ApparatusType.Engine, ApparatusType.Ladder),
            new IncidentType("Vehicle Fire", 3, 6, ApparatusType.Engine),
            new IncidentType("Brush Fire", 5, 10, ApparatusType.Engine, ApparatusType.Engine),
            new IncidentType("Automatic Fire Alarm", 2, 4, ApparatusType.Engine),
            new IncidentType("Smoke Investigation", 2, 4, ApparatusType.Engine),
            new IncidentType("Medical Aid", 4, 8, ApparatusType.Ambulance),
            new IncidentType("Cardiac Arrest", 5, 9, ApparatusType.Engine, ApparatusType.Ambulance),
            new IncidentType("Traffic Collision", 4, 8, ApparatusType.Engine, ApparatusType.Ambulance),
            new IncidentType("Elevator Rescue", 3, 6, ApparatusType.Ladder),
            new IncidentType("Gas Leak", 4, 8, ApparatusType.Engine),
        };

        private readonly ApparatusManager apparatus;
        private readonly FireStationManager stations;
        private readonly CalloutManager callouts;
        private readonly Action<FireApparatus> returnApparatus;
        private readonly List<OtherIncident> incidents = new List<OtherIncident>();
        private uint nextIncidentAt;
        private uint lastUpdate;

        public IReadOnlyList<OtherIncident> Incidents => incidents;

        public OtherUnits(ApparatusManager apparatus, FireStationManager stations, CalloutManager callouts, Action<FireApparatus> returnApparatus)
        {
            this.apparatus = apparatus;
            this.stations = stations;
            this.callouts = callouts;
            this.returnApparatus = returnApparatus;
            Schedule();
        }

        public void Process(bool onDuty)
        {
            if (!onDuty)
            {
                Clear();
                return;
            }

            if (Game.GameTime - lastUpdate < UpdateInterval)
                return;
            lastUpdate = Game.GameTime;

            if (Settings.OtherUnitsIncidents && Game.GameTime >= nextIncidentAt)
            {
                StartRandom();
                Schedule();
            }

            foreach (OtherIncident incident in incidents.ToList())
                Update(incident);
        }

        /// <summary>Whether the <paramref name="type"/> of this station is out on another call.</summary>
        public bool IsBusy(FireStation station, ApparatusType type)
        {
            return incidents.Any(i => i.Units.Any(u => u.Station == station && u.Type == type));
        }

        /// <summary>The nearest station whose unit of this type is available, or null when they are all busy.</summary>
        public FireStation FindAvailableStation(ApparatusType type, Vector3 position)
        {
            return stations.Stations.OrderBy(s => s.Position.DistanceTo(position)).FirstOrDefault(s => !IsBusy(s, type));
        }

        /// <summary>The player declined or ignored a call: another station takes it.</summary>
        public void TakeOver(Callout declined)
        {
            if (Settings.OtherUnitsIncidents)
            {
                var units = new List<ApparatusType>();
                for (int i = 0; i < Math.Max(1, declined.AlarmLevel); i++)
                    units.Add(ApparatusType.Engine);
                if (declined.Category == CalloutCategory.Medical || declined.Category == CalloutCategory.Traffic)
                    units.Add(ApparatusType.Ambulance);

                OtherIncident incident = Start(declined.CalloutMessage ?? declined.FriendlyName, declined.CalloutPosition, units, 4, 10);
                if (incident != null)
                {
                    Notify.Dispatch("~o~Reassigned", $"~y~{incident.UnitNames}~s~ took the call.");
                    return;
                }
            }

            Notify.Dispatch("~o~Reassigned", "The call was given to another unit.");
        }

        /// <summary>Starts a random incident for other units; for testing from the console.</summary>
        public bool StartRandom()
        {
            IncidentType type = Types[MathHelper.GetRandomInteger(Types.Length)];
            Vector3 player = Game.LocalPlayer.Character.Position;
            Vector3 position = World.GetNextPositionOnStreet(player.Around2D(MinIncidentDistance, MaxIncidentDistance));

            Callout current = callouts.Current;
            if (current != null && position.DistanceTo(current.CalloutPosition) < MinDistanceToPlayerCall)
                return false;

            OtherIncident incident = Start(type.Name, position, type.Units, type.MinMinutes, type.MaxMinutes);
            if (incident == null)
                return false;

            if (Settings.OtherUnitsNotifications)
                Notify.Dispatch("~b~Other units", $"~y~{incident.UnitNames}~s~ responding to ~o~{incident.Name}~s~, {incident.Street}.");
            return true;
        }

        /// <summary>Ends all incidents, e.g. when the player goes off duty.</summary>
        public void Clear()
        {
            foreach (OtherIncident incident in incidents.ToList())
                Finish(incident);
        }

        private OtherIncident Start(string name, Vector3 position, IEnumerable<ApparatusType> types, int minMinutes, int maxMinutes)
        {
            var incident = new OtherIncident
            {
                Name = name,
                Position = position,
                OnSceneTime = (uint)(MathHelper.GetRandomInteger(minMinutes, maxMinutes + 1) * 60000),
            };
            Vector3 player = Game.LocalPlayer.Character.Position;

            foreach (ApparatusType type in types)
            {
                ApparatusDefinition definition = apparatus.FindDefinition(type);
                FireStation station = stations.Stations
                    .OrderBy(s => s.Position.DistanceTo(position))
                    .FirstOrDefault(s => !IsBusy(s, type) && !incident.Units.Any(u => u.Station == s && u.Type == type));
                if (definition == null || station == null)
                    continue;

                var unit = new CommittedUnit
                {
                    Name = $"{ShortName(station)} {type}",
                    Station = station,
                    Type = type,
                    ArrivesAt = Game.GameTime + TurnoutTime + (uint)(station.Position.DistanceTo(position) / ResponseSpeed * 1000f),
                };

                if (player.DistanceTo(station.Position) <= RealUnitRange || player.DistanceTo(position) <= RealUnitRange)
                {
                    unit.Vehicle = apparatus.SpawnResponding(definition, station, position, announce: false);
                    if (unit.Vehicle != null)
                        unit.Vehicle.IsCommitted = true;
                }
                incident.Units.Add(unit);
            }

            if (incident.Units.Count == 0)
                return null;

            incident.Blip = new Blip(position) { Color = Color.Orange, Scale = 0.6f, Name = $"{name} ({incident.UnitNames})" };
            NativeFunction.CallByName<uint>("SET_BLIP_AS_SHORT_RANGE", (uint)incident.Blip.Handle, true);
            incidents.Add(incident);
            Game.LogTrivial($"LSFD: {incident.UnitNames} responding to '{name}' at {incident.Street}.");
            return incident;
        }

        private void Update(OtherIncident incident)
        {
            foreach (CommittedUnit unit in incident.Units.Where(u => !u.OnScene))
            {
                bool vehicleThere = unit.Vehicle != null && (!unit.Vehicle.IsValid || !unit.Vehicle.ResponseDestination.HasValue);
                bool simulatedThere = unit.Vehicle == null && Game.GameTime >= unit.ArrivesAt;
                unit.OnScene = vehicleThere || simulatedThere || Game.GameTime >= unit.ArrivesAt + StuckTime;
            }

            if (!incident.ClearsAt.HasValue && incident.Units.Any(u => u.OnScene))
                incident.ClearsAt = Game.GameTime + incident.OnSceneTime;

            if (incident.ClearsAt.HasValue && Game.GameTime >= incident.ClearsAt.Value)
                Finish(incident);
        }

        private void Finish(OtherIncident incident)
        {
            incidents.Remove(incident);
            if (incident.Blip.Exists())
                incident.Blip.Delete();

            foreach (CommittedUnit unit in incident.Units.Where(u => u.Vehicle != null && apparatus.All.Contains(u.Vehicle)))
                returnApparatus(unit.Vehicle);
        }

        private void Schedule()
        {
            int seconds = MathHelper.GetRandomInteger(Settings.MinSecondsBetweenOtherIncidents, Settings.MaxSecondsBetweenOtherIncidents + 1);
            nextIncidentAt = Game.GameTime + (uint)(seconds * 1000);
        }

        // "Davis Fire Station" -> "Davis".
        private static string ShortName(FireStation station)
        {
            string name = station.Name;
            foreach (string suffix in new[] { " Fire Station", " Station" })
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return name.Substring(0, name.Length - suffix.Length);
            }
            return name;
        }
    }
}
