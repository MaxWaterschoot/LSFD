using System;
using LSFD.Apparatus;
using LSFD.Core;
using Rage;

namespace LSFD.API
{
    /// <summary>
    /// Something the player does at a spot of the scene by holding the interact key: check an alarm panel, close a gas
    /// valve, spread absorbent... A yellow marker shows where. Add it with <see cref="Callout.AddSceneAction"/> (removed
    /// when the callout ends) or <see cref="SceneActions.Add"/>.
    /// </summary>
    public sealed class SceneAction
    {
        public SceneAction(Vector3 position, string prompt, Action completed)
        {
            Position = position;
            Prompt = prompt;
            Completed = completed;
        }

        public Vector3 Position { get; set; }

        /// <summary>Finishes "Hold E to ...", e.g. "check the ~y~fire alarm panel".</summary>
        public string Prompt { get; set; }

        public Action Completed { get; set; }

        public float HoldSeconds { get; set; } = 4f;
        public string ProgressLabel { get; set; } = L.T("WORKING");
        public float Radius { get; set; } = 1.6f;
        public bool ShowMarker { get; set; } = true;

        /// <summary>Equipment the player has to carry (as hand tool or weapon), or null.</summary>
        public EquipmentType? RequiredEquipment { get; set; }

        /// <summary>Extra condition; return false to show <see cref="RequirementHint"/> instead.</summary>
        public Func<bool> CanUse { get; set; }

        /// <summary>Shown when the player is at the spot but can't do it yet.</summary>
        public string RequirementHint { get; set; }

        public string AnimationDictionary { get; set; } = "mini@repair";
        public string AnimationName { get; set; } = "fixing_a_player";

        /// <summary>Stays available after it was done once.</summary>
        public bool Repeatable { get; set; }

        public bool IsDone { get; internal set; }
        public bool IsRemoved { get; private set; }

        public void Remove()
        {
            IsRemoved = true;
        }
    }

    public static class SceneActions
    {
        /// <summary>Adds an action that stays until <see cref="SceneAction.Remove"/> (or until it is done, if not repeatable).</summary>
        public static SceneAction Add(SceneAction action)
        {
            Lsfd.SceneActions.Add(action);
            return action;
        }
    }
}
