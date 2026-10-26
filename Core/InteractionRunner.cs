using System.Linq;
using Rage;

namespace LSFD.Core
{
    /// <summary>Picks the interaction available to the player, shows its prompt and runs it on a press or hold of the interact key.</summary>
    internal class InteractionRunner
    {
        private readonly IInteractionProvider[] providers;

        private Interaction holding;
        private float heldSeconds;

        /// <summary>Progress of the current hold from 0 to 1, or null when not holding.</summary>
        public float? Progress => holding == null ? (float?)null : heldSeconds / holding.HoldSeconds;
        public string ProgressLabel => holding?.ProgressLabel;

        /// <param name="providers">In order of priority: the first one with an interaction wins.</param>
        public InteractionRunner(params IInteractionProvider[] providers)
        {
            this.providers = providers;
        }

        public void Process()
        {
            if (holding != null)
            {
                ContinueHold();
                return;
            }

            Interaction interaction = providers.Select(p => p.GetInteraction()).FirstOrDefault(i => i != null);
            if (interaction == null)
                return;

            if (interaction.IsHint)
            {
                Game.DisplayHelp(interaction.Prompt, 100);
                return;
            }

            string verb = interaction.HoldSeconds > 0f ? "Hold" : "Press";
            Game.DisplayHelp($"{verb} ~b~{Settings.InteractKey}~s~ to {interaction.Prompt}.", 100);

            if (!Game.IsKeyDown(Settings.InteractKey))
                return;

            if (interaction.HoldSeconds > 0f)
                StartHold(interaction);
            else
                interaction.Execute();
        }

        public void Cancel()
        {
            if (holding == null)
                return;

            Interaction stopped = holding;
            holding = null;
            Game.LocalPlayer.Character.Tasks.Clear();
            stopped.Stopped?.Invoke();
        }

        private void StartHold(Interaction interaction)
        {
            holding = interaction;
            heldSeconds = 0f;

            if (interaction.AnimationDictionary != null)
                Game.LocalPlayer.Character.Tasks.PlayAnimation(interaction.AnimationDictionary, interaction.AnimationName, 2f, AnimationFlags.Loop);
            interaction.Started?.Invoke();
        }

        private void ContinueHold()
        {
            Ped player = Game.LocalPlayer.Character;
            if (!Game.IsKeyDownRightNow(Settings.InteractKey) || player.IsDead || player.IsInAnyVehicle(true))
            {
                Cancel();
                return;
            }

            heldSeconds += Game.FrameTime;
            if (heldSeconds < holding.HoldSeconds)
                return;

            Interaction done = holding;
            Cancel();
            done.Execute();
        }
    }
}
