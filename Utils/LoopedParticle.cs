using Rage;
using Rage.Native;

namespace LSFD.Utils
{
    /// <summary>A looped particle effect placed in the world that can be moved every frame.</summary>
    internal class LoopedParticle
    {
        private readonly string asset;
        private int handle;

        public string EffectName { get; private set; }
        public float Scale { get; private set; }
        public bool IsActive => handle != 0;

        public LoopedParticle(string asset)
        {
            this.asset = asset;
        }

        public void Start(string effectName, Vector3 position, Rotator rotation, float scale)
        {
            Stop();

            NativeFunction.CallByName<uint>("REQUEST_NAMED_PTFX_ASSET", asset);
            if (!NativeFunction.CallByName<bool>("HAS_NAMED_PTFX_ASSET_LOADED", asset))
                return;

            NativeFunction.CallByName<uint>("USE_PARTICLE_FX_ASSET", asset);
            handle = NativeFunction.CallByName<int>("START_PARTICLE_FX_LOOPED_AT_COORD", effectName,
                position.X, position.Y, position.Z, rotation.Pitch, rotation.Roll, rotation.Yaw, scale, false, false, false, false);

            EffectName = effectName;
            Scale = scale;
        }

        public void Move(Vector3 position, Rotator rotation)
        {
            if (IsActive)
                NativeFunction.CallByName<uint>("SET_PARTICLE_FX_LOOPED_OFFSETS", handle, position.X, position.Y, position.Z, rotation.Pitch, rotation.Roll, rotation.Yaw);
        }

        /// <summary>Tints the effect; 1, 1, 1 is its normal color.</summary>
        public void SetColor(float r, float g, float b)
        {
            if (IsActive)
                NativeFunction.CallByName<uint>("SET_PARTICLE_FX_LOOPED_COLOUR", handle, r, g, b, false);
        }

        /// <summary>How far away the effect is still drawn, in meters.</summary>
        public void SetDrawDistance(float distance)
        {
            if (IsActive)
                NativeFunction.CallByName<uint>("SET_PARTICLE_FX_LOOPED_FAR_CLIP_DIST", handle, distance);
        }

        public void Stop()
        {
            if (!IsActive)
                return;

            NativeFunction.CallByName<uint>("STOP_PARTICLE_FX_LOOPED", handle, false);
            handle = 0;
        }
    }
}
