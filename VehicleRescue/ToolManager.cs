using LSFD.Apparatus;
using LSFD.Core;
using LSFD.UI;
using Rage;

namespace LSFD.VehicleRescue
{
    /// <summary>The hand tool the player is carrying, if any, with its prop.</summary>
    internal class ToolManager
    {
        private readonly ToolRepository repository = new ToolRepository();
        private Object prop;

        public ToolDefinition Held { get; private set; }

        /// <summary>The apparatus the held tool belongs to.</summary>
        public FireApparatus HeldFrom { get; private set; }

        public bool IsHolding(EquipmentType type) => Held != null && Held.Type == type;

        public void TakeTool(FireApparatus source, EquipmentType type)
        {
            StowTool();

            Held = repository.Get(type);
            HeldFrom = source;
            source.SetTaken(type, true);
            AttachProp();

            Notify.Info(L.F($"You are carrying the ~y~{type.ToDisplayName()}~s~."));
        }

        public void StowTool()
        {
            if (Held == null)
                return;

            if (prop.Exists())
                prop.Delete();
            prop = null;

            HeldFrom.SetTaken(Held.Type, false);
            Held = null;
            HeldFrom = null;
        }

        public void Process()
        {
            if (Held == null)
                return;

            Ped player = Game.LocalPlayer.Character;
            if (!player.Exists() || player.IsDead || player.IsInAnyVehicle(true) || !HeldFrom.Vehicle.Exists())
                StowTool();
        }

        private void AttachProp()
        {
            if (string.IsNullOrEmpty(Held.Prop))
                return;

            var model = new Model(Held.Prop);
            if (!model.IsValid)
            {
                Game.LogTrivial($"LSFD: tool prop '{Held.Prop}' does not exist, check Tools.xml.");
                return;
            }

            Ped player = Game.LocalPlayer.Character;
            model.LoadAndWait();
            prop = new Object(model, player.Position) { IsPersistent = true };
            prop.AttachTo(player, player.GetBoneIndex((PedBoneId)Held.Bone),
                new Vector3(Held.OffsetX, Held.OffsetY, Held.OffsetZ), new Rotator(Held.Pitch, Held.Roll, Held.Yaw));
            model.Dismiss();
        }
    }
}
