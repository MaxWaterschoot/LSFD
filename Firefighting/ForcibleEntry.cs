using System.Collections.Generic;
using System.Linq;
using LSFD.Core;
using Rage;
using Rage.Native;

namespace LSFD.Firefighting
{
    /// <summary>
    /// Locked doors at a fire: frozen in place until a firefighter forces them with a halligan bar or an axe. The AI crew
    /// forces doors as well when they work the scene.
    /// </summary>
    internal class ForcibleEntry : IInteractionProvider
    {
        public const float ForceSeconds = 5f;
        private const float Reach = 1.8f;

        private static readonly uint[] EntryTools = { Game.GetHashKey("WEAPON_CROWBAR"), Game.GetHashKey("WEAPON_HATCHET") };

        private readonly List<LockedDoor> locked = new List<LockedDoor>();

        public int LockedCount => locked.Count;

        /// <summary>Locks doors around <paramref name="position"/>, each with the given chance. Returns how many were locked.</summary>
        public int LockDoorsNear(Vector3 position, float radius, float chance)
        {
            int count = 0;
            foreach (Object door in World.GetEntities(position, radius, GetEntitiesFlags.ConsiderAllObjects).OfType<Object>())
            {
                if (!IsDoor(door) || locked.Any(d => d.Door == door) || MathHelper.GetRandomSingle(0f, 1f) > chance)
                    continue;

                NativeFunction.CallByName<uint>("FREEZE_ENTITY_POSITION", door, true);
                locked.Add(new LockedDoor(door));
                count++;
            }
            return count;
        }

        /// <summary>Unlocks every door, e.g. when the call is over.</summary>
        public void ReleaseAll()
        {
            foreach (LockedDoor door in locked)
                Unfreeze(door.Door);
            locked.Clear();
        }

        public Interaction GetInteraction()
        {
            Ped player = Game.LocalPlayer.Character;
            if (player.IsInAnyVehicle(false))
                return null;

            LockedDoor door = locked.FirstOrDefault(d => d.Door.Exists() && d.ClaimedBy == null && d.Door.DistanceTo(player.Position) <= Reach);
            if (door == null)
                return null;

            uint weapon = NativeFunction.CallByName<uint>("GET_SELECTED_PED_WEAPON", player);
            if (!EntryTools.Contains(weapon))
                return Interaction.Hint("The door is ~r~locked~s~. Force it with a ~y~halligan bar~s~ or ~y~axe~s~ from the engine.");

            return new Interaction("~r~force~s~ the door", () => Force(door.Door), ForceSeconds, "FORCING")
            {
                AnimationDictionary = "mini@repair",
                AnimationName = "fixing_a_player",
            };
        }

        /// <summary>The nearest locked door within <paramref name="radius"/> that no crew member is working on.</summary>
        public Object FindUnclaimed(Vector3 position, float radius)
        {
            return locked
                .Where(d => d.Door.Exists() && d.ClaimedBy == null && d.Door.DistanceTo(position) <= radius)
                .OrderBy(d => d.Door.DistanceTo(position))
                .Select(d => d.Door)
                .FirstOrDefault();
        }

        public bool IsLocked(Object door) => locked.Any(d => d.Door == door);

        public void Claim(Object door, Ped by)
        {
            LockedDoor entry = locked.FirstOrDefault(d => d.Door == door);
            if (entry != null)
                entry.ClaimedBy = by;
        }

        public void Force(Object door)
        {
            locked.RemoveAll(d => d.Door == door);
            Unfreeze(door);
            NativeFunction.CallByName<uint>("PLAY_SOUND_FROM_ENTITY", -1, "Crash", door, "DLC_HEIST_HACKING_SNAKE_SOUNDS", false, 0);
            Game.DisplaySubtitle("Door ~g~forced~s~.", 2000);
        }

        // Doors are thin, upright and about a person wide; map doors are props like any other.
        private static bool IsDoor(Object entity)
        {
            if (!entity.Exists())
                return false;

            Vector3 size = entity.Model.Dimensions;
            float width = System.Math.Max(size.X, size.Y);
            float depth = System.Math.Min(size.X, size.Y);
            return size.Z >= 1.8f && size.Z <= 3.2f && width >= 0.7f && width <= 1.9f && depth <= 0.35f;
        }

        private static void Unfreeze(Object door)
        {
            if (door.Exists())
                NativeFunction.CallByName<uint>("FREEZE_ENTITY_POSITION", door, false);
        }

        private sealed class LockedDoor
        {
            public LockedDoor(Object door)
            {
                Door = door;
            }

            public Object Door { get; }
            public Ped ClaimedBy { get; set; }
        }
    }
}
