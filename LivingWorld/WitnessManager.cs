using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LSFD.Core;
using Rage;

namespace LSFD.LivingWorld
{
    /// <summary>
    /// Callers and witnesses on scene with something to tell: the player walks up and talks to them, and what they say
    /// can point to the victims ("My son is still inside, second floor!").
    /// </summary>
    internal class WitnessManager : IInteractionProvider
    {
        private const float TalkDistance = 2.5f;
        private const float WalkAwayDistance = 10f;
        private const uint LineTime = 3800;

        private class Witness
        {
            public Ped Ped;
            public string Label;
            public string[] Lines;
            public Action Heard;
            public Blip Blip;
            public bool HasTalked;
        }

        private readonly List<Witness> witnesses = new List<Witness>();
        private Witness talking;
        private int line;
        private uint nextLineAt;

        /// <summary>Makes <paramref name="ped"/> someone to talk to; <c>heard</c> runs once, after the player heard all the lines.</summary>
        public void Add(Ped ped, string label, string[] lines, Action heard)
        {
            if (!ped.Exists() || lines == null || lines.Length == 0)
                return;

            Remove(ped);
            var blip = new Blip(ped) { Color = Color.Gold, Scale = 0.6f, Name = label };
            witnesses.Add(new Witness { Ped = ped, Label = label, Lines = lines, Heard = heard, Blip = blip });
        }

        public void Remove(Ped ped)
        {
            foreach (Witness witness in witnesses.Where(w => w.Ped == ped).ToList())
                Remove(witness);
        }

        public bool HasTalkedTo(Ped ped) => witnesses.Any(w => w.Ped == ped && w.HasTalked);

        public void Clear()
        {
            foreach (Witness witness in witnesses.ToList())
                Remove(witness);
        }

        public void Process()
        {
            foreach (Witness gone in witnesses.Where(w => !w.Ped.Exists() || w.Ped.IsDead).ToList())
                Remove(gone);

            if (talking == null || Game.GameTime < nextLineAt)
                return;

            Ped player = Game.LocalPlayer.Character;
            if (player.DistanceTo(talking.Ped) > WalkAwayDistance)
            {
                talking = null;
                return;
            }

            if (line >= talking.Lines.Length)
            {
                Finish(talking);
                return;
            }

            Game.DisplaySubtitle($"~y~{talking.Label}:~s~ \"{talking.Lines[line]}\"", (int)LineTime);
            line++;
            nextLineAt = Game.GameTime + LineTime;
        }

        public Interaction GetInteraction()
        {
            if (talking != null)
                return null;

            Ped player = Game.LocalPlayer.Character;
            if (player.IsInAnyVehicle(false))
                return null;

            Witness near = witnesses
                .Where(w => w.Ped.DistanceTo(player) <= TalkDistance)
                .OrderBy(w => w.Ped.DistanceTo(player))
                .FirstOrDefault();
            if (near == null)
                return null;

            return new Interaction(near.HasTalked ? L.F($"ask the ~y~{near.Label.ToLowerInvariant()}~s~ again") : L.F($"talk to the ~y~{near.Label.ToLowerInvariant()}"),
                () => Talk(near));
        }

        private void Talk(Witness witness)
        {
            talking = witness;
            line = 0;
            nextLineAt = Game.GameTime;

            Ped player = Game.LocalPlayer.Character;
            witness.Ped.Tasks.Clear();
            witness.Ped.Tasks.AchieveHeading(MathHelper.ConvertDirectionToHeading(player.Position - witness.Ped.Position));
        }

        private void Finish(Witness witness)
        {
            talking = null;
            bool first = !witness.HasTalked;
            witness.HasTalked = true;
            if (witness.Blip.Exists())
                witness.Blip.Delete();

            if (first)
            {
                try
                {
                    witness.Heard?.Invoke();
                }
                catch (Exception e)
                {
                    Game.LogTrivial($"LSFD: a witness callback threw an exception: {e}");
                }
            }
        }

        private void Remove(Witness witness)
        {
            if (talking == witness)
                talking = null;
            if (witness.Blip.Exists())
                witness.Blip.Delete();
            witnesses.Remove(witness);
        }
    }
}
