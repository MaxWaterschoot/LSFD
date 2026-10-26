using System;
using LSFD.Core;
using Rage;

namespace LSFD.API
{
    /// <summary>
    /// People on scene with a story: the player walks up, presses the interact key and hears what they have to say,
    /// line by line. Use it to point the player to the victims: "My son is still inside, second floor!"
    /// </summary>
    public static class Witnesses
    {
        /// <summary>Makes <paramref name="ped"/> the caller: marked on the map, and talking to them shows <paramref name="statement"/>.</summary>
        public static void AddCaller(Ped ped, params string[] statement) => Add(ped, L.T("Caller"), null, statement);

        /// <summary>Like <see cref="AddCaller"/>, for someone who saw what happened.</summary>
        public static void AddWitness(Ped ped, params string[] statement) => Add(ped, L.T("Witness"), null, statement);

        /// <param name="ped">Who is talking.</param>
        /// <param name="label">Shown before every line and on the map, e.g. "Caller" or "Neighbor".</param>
        /// <param name="onHeard">Runs once after the player heard the whole statement, e.g. to mark where the victims are; may be null.</param>
        /// <param name="statement">The lines, shown one after the other.</param>
        public static void Add(Ped ped, string label, Action onHeard, params string[] statement) =>
            Lsfd.Witnesses.Add(ped, label, statement, onHeard);

        /// <summary>Whether the player has heard this person's whole statement.</summary>
        public static bool HasTalkedTo(Ped ped) => Lsfd.Witnesses.HasTalkedTo(ped);

        /// <summary>Stops the ped being a witness; call this in OnEnd for peds you don't dismiss with the scene.</summary>
        public static void Remove(Ped ped) => Lsfd.Witnesses.Remove(ped);
    }
}
