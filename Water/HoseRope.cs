using LSFD.Core;
using LSFD.Utils;
using Rage;
using Rage.Native;

namespace LSFD.Water
{
    /// <summary>
    /// A hose between two entities, made with the game's rope physics so it sags and lies on the ground.
    /// Falls back to a drawn line if the rope can't be created.
    /// </summary>
    internal class HoseRope
    {
        private const uint TextureLoadTimeout = 3000;

        private readonly Entity from;
        private readonly Entity to;
        private readonly float length;
        private readonly uint createdAt = Game.GameTime;

        private int handle;
        private bool failed;

        public HoseRope(Entity from, Entity to, float length)
        {
            this.from = from;
            this.to = to;
            this.length = length;
        }

        /// <summary>Call every frame with the current attachment points in world space.</summary>
        public void Process(Vector3 fromPosition, Vector3 toPosition)
        {
            if (handle == 0 && !failed)
                TryCreate(fromPosition, toPosition);

            if (failed)
                Draw.Hose(fromPosition, toPosition);
        }

        public void Delete()
        {
            if (handle == 0)
                return;

            using (var pointer = new NativePointer())
            {
                pointer.SetValue(handle);
                NativeFunction.CallByName<uint>("DELETE_ROPE", pointer);
            }
            handle = 0;
        }

        private void TryCreate(Vector3 fromPosition, Vector3 toPosition)
        {
            if (!from.Exists() || !to.Exists())
                return;

            NativeFunction.CallByName<uint>("ROPE_LOAD_TEXTURES");
            if (!NativeFunction.CallByName<bool>("ROPE_ARE_TEXTURES_LOADED"))
            {
                failed = Game.GameTime - createdAt > TextureLoadTimeout;
                return;
            }

            using (var unused = new NativePointer())
            {
                handle = NativeFunction.CallByName<int>("ADD_ROPE",
                    fromPosition.X, fromPosition.Y, fromPosition.Z, 0f, 0f, 0f,
                    length, Settings.HoseRopeType, length, 0.5f, 0.5f,
                    false, false, false, 1f, false, unused);
            }

            if (handle == 0)
            {
                failed = true;
                Game.LogTrivial("LSFD: could not create a hose rope, drawing hoses as lines instead.");
                return;
            }

            NativeFunction.CallByName<uint>("ATTACH_ENTITIES_TO_ROPE", handle, from, to,
                fromPosition.X, fromPosition.Y, fromPosition.Z, toPosition.X, toPosition.Y, toPosition.Z,
                length, false, false, 0, 0);
        }
    }
}
