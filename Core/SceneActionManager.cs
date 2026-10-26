using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.VehicleRescue;
using Rage;
using Rage.Native;

namespace LSFD.Core
{
    /// <summary>Runs the <see cref="SceneAction"/>s callouts add: draws their markers and offers them on the interact key.</summary>
    internal class SceneActionManager : IInteractionProvider
    {
        private const float MarkerDistance = 40f;

        private readonly ToolManager tools;
        private readonly List<SceneAction> actions = new List<SceneAction>();

        public SceneActionManager(ToolManager tools)
        {
            this.tools = tools;
        }

        public void Add(SceneAction action)
        {
            if (!actions.Contains(action))
                actions.Add(action);
        }

        public void Process()
        {
            actions.RemoveAll(a => a.IsRemoved || (a.IsDone && !a.Repeatable));

            Vector3 player = Game.LocalPlayer.Character.Position;
            foreach (SceneAction action in actions.Where(a => a.ShowMarker && a.Position.DistanceTo(player) <= MarkerDistance))
            {
                Vector3 p = action.Position;
                NativeFunction.CallByName<uint>("DRAW_MARKER", 2, p.X, p.Y, p.Z + 0.6f, 0f, 0f, 0f, 180f, 0f, 0f,
                    0.35f, 0.35f, 0.35f, 255, 200, 0, 190, true, true, 2, false, 0, 0, false);
            }
        }

        public void Clear()
        {
            actions.Clear();
        }

        public Interaction GetInteraction()
        {
            Ped player = Game.LocalPlayer.Character;
            if (player.IsInAnyVehicle(false))
                return null;

            SceneAction action = actions
                .Where(a => !a.IsRemoved && !(a.IsDone && !a.Repeatable) && a.Position.DistanceTo(player.Position) <= a.Radius)
                .OrderBy(a => a.Position.DistanceTo(player.Position))
                .FirstOrDefault();
            if (action == null)
                return null;

            if (action.RequiredEquipment.HasValue && !IsCarrying(action.RequiredEquipment.Value))
                return Interaction.Hint(action.RequirementHint ?? $"You need the ~y~{action.RequiredEquipment.Value.ToDisplayName()}~s~ from an engine for this.");
            if (action.CanUse != null && !action.CanUse())
                return Interaction.Hint(action.RequirementHint ?? "You can't do this yet.");

            return new Interaction(action.Prompt, () =>
            {
                action.IsDone = true;
                action.Completed?.Invoke();
            }, action.HoldSeconds, action.ProgressLabel)
            {
                AnimationDictionary = string.IsNullOrEmpty(action.AnimationDictionary) ? null : action.AnimationDictionary,
                AnimationName = action.AnimationName,
            };
        }

        /// <summary>Whether the player carries <paramref name="type"/>, as hand tool, weapon or camera.</summary>
        public bool IsCarrying(EquipmentType type)
        {
            if (tools.IsHolding(type))
                return true;
            if (type == EquipmentType.ThermalCamera)
                return Lsfd.ThermalCamera.IsCarried;

            string weapon = type.GetWeaponName();
            return weapon != null && NativeFunction.CallByName<bool>("HAS_PED_GOT_WEAPON", Game.LocalPlayer.Character, Game.GetHashKey(weapon), false);
        }
    }
}
