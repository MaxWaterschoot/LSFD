using System;

namespace LSFD.Core
{
    /// <summary>Something the player can do right now with the interact key, either with a single press or by holding it.</summary>
    internal sealed class Interaction
    {
        public string Prompt { get; }
        public Action Execute { get; }

        /// <summary>How long the key has to be held, 0 for a single press.</summary>
        public float HoldSeconds { get; }

        /// <summary>Label of the progress bar while holding, e.g. "CUTTING".</summary>
        public string ProgressLabel { get; }

        /// <summary>Animation the player plays while holding, or null.</summary>
        public string AnimationDictionary { get; set; }
        public string AnimationName { get; set; }

        /// <summary>Runs when the player starts holding, e.g. to animate the other person involved.</summary>
        public Action Started { get; set; }

        /// <summary>Runs when holding ends, whether it completed or was cancelled.</summary>
        public Action Stopped { get; set; }

        /// <summary>Only shows <see cref="Prompt"/> as a hint, without an action.</summary>
        public bool IsHint => Execute == null;

        public Interaction(string prompt, Action execute, float holdSeconds = 0f, string progressLabel = null)
        {
            Prompt = prompt;
            Execute = execute;
            HoldSeconds = holdSeconds;
            ProgressLabel = progressLabel ?? L.T("WORKING");
        }

        public static Interaction Hint(string text) => new Interaction(text, null);
    }

    internal interface IInteractionProvider
    {
        /// <summary>Returns the interaction available at the player's position, or null.</summary>
        Interaction GetInteraction();
    }
}
