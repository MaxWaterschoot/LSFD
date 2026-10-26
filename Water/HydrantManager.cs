using System.Collections.Generic;
using System.Linq;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.UI;
using Rage;
using Rage.Native;

namespace LSFD.Water
{
    /// <summary>Supply lines: running one from an apparatus to a fire hydrant to refill and supply the tank.</summary>
    internal class HydrantManager : IInteractionProvider
    {
        private const float HydrantReach = 2.5f;

        private static readonly Model[] HydrantModels =
        {
            "prop_fire_hydrant_1", "prop_fire_hydrant_2", "prop_fire_hydrant_2_l1", "prop_fire_hydrant_4",
        };

        private readonly ApparatusManager apparatus;
        private readonly Dictionary<FireApparatus, HoseRope> connectedLines = new Dictionary<FireApparatus, HoseRope>();
        private HoseRope carriedLine;

        /// <summary>The apparatus whose supply line the player is carrying, or null.</summary>
        public FireApparatus CarryingFrom { get; private set; }

        public bool IsCarrying => CarryingFrom != null;

        public HydrantManager(ApparatusManager apparatus)
        {
            this.apparatus = apparatus;
        }

        public void TakeSupplyLine(FireApparatus source)
        {
            Disconnect(source, false);
            ReturnSupplyLine();

            CarryingFrom = source;
            source.SetTaken(EquipmentType.SupplyLine, true);
            carriedLine = new HoseRope(source.Vehicle, Game.LocalPlayer.Character, source.Definition.SupplyLineLength);
            Notify.Info(L.F($"Bring the supply line of the ~y~{source.Definition.Name}~s~ to a ~b~fire hydrant~s~ (max {source.Definition.SupplyLineLength:0} m)."));
        }

        public void ReturnSupplyLine()
        {
            if (!IsCarrying)
                return;

            carriedLine.Delete();
            carriedLine = null;
            CarryingFrom.SetTaken(EquipmentType.SupplyLine, false);
            CarryingFrom = null;
        }

        public void Disconnect(FireApparatus source, bool notify = true)
        {
            if (connectedLines.TryGetValue(source, out HoseRope line))
            {
                line.Delete();
                connectedLines.Remove(source);
            }

            if (!source.IsSupplied)
                return;

            source.DisconnectHydrant();
            source.SetTaken(EquipmentType.SupplyLine, false);
            if (notify)
                Notify.Info(L.F($"Supply line of the ~y~{source.Definition.Name}~s~ disconnected and stowed."));
        }

        public void DisconnectAll()
        {
            ReturnSupplyLine();
            foreach (FireApparatus source in connectedLines.Keys.ToList())
                Disconnect(source, false);
        }

        public Interaction GetInteraction()
        {
            Ped player = Game.LocalPlayer.Character;
            if (player.IsInAnyVehicle(false))
                return null;

            if (IsCarrying)
            {
                Object hydrant = FindHydrant(player.Position, HydrantReach);
                return hydrant == null ? null : new Interaction(L.T("connect the supply line to the ~b~hydrant"), () => Connect(hydrant), 2f, L.T("CONNECTING"));
            }

            FireApparatus connected = apparatus.All.FirstOrDefault(a => a.IsSupplied && a.ConnectedHydrant.Value.DistanceTo(player.Position) <= HydrantReach);
            return connected == null ? null : new Interaction(L.T("disconnect the supply line"), () => Disconnect(connected), 1.5f, L.T("DISCONNECTING"));
        }

        public void Process()
        {
            Ped player = Game.LocalPlayer.Character;

            if (IsCarrying)
            {
                if (!CarryingFrom.IsValid || player.IsDead || player.IsInAnyVehicle(true))
                {
                    ReturnSupplyLine();
                }
                else if (player.DistanceTo(CarryingFrom.RearPosition) > CarryingFrom.Definition.SupplyLineLength)
                {
                    Notify.Warning(L.T("The supply line doesn't reach that far, move the apparatus closer to a hydrant."));
                    ReturnSupplyLine();
                }
                else
                {
                    carriedLine.Process(CarryingFrom.RearPosition, player.GetBonePosition(PedBoneId.RightHand));
                }
            }

            foreach (var pair in connectedLines.ToList())
            {
                FireApparatus source = pair.Key;
                // The apparatus drove off and the line snapped, or the vehicle is gone.
                if (!source.IsSupplied || !source.Vehicle.Exists())
                {
                    pair.Value.Delete();
                    connectedLines.Remove(source);
                    continue;
                }

                pair.Value.Process(source.RearPosition, source.ConnectedHydrant.Value + new Vector3(0f, 0f, 0.5f));
            }
        }

        private void Connect(Object hydrant)
        {
            FireApparatus source = CarryingFrom;
            ReturnSupplyLine();

            source.ConnectHydrant(hydrant.Position);
            source.SetTaken(EquipmentType.SupplyLine, true);
            connectedLines[source] = new HoseRope(source.Vehicle, hydrant, source.Definition.SupplyLineLength);

            Notify.Info(L.F($"~b~Hydrant connected.~s~ The ~y~{source.Definition.Name}~s~ is now being supplied with water."));
        }

        private static Object FindHydrant(Vector3 position, float radius)
        {
            foreach (Model model in HydrantModels)
            {
                uint handle = NativeFunction.CallByName<uint>("GET_CLOSEST_OBJECT_OF_TYPE",
                    position.X, position.Y, position.Z, radius, model.Hash, false, false, false);
                Object hydrant = handle == 0 ? null : World.GetEntityByHandle<Object>(handle);
                if (hydrant.Exists())
                    return hydrant;
            }
            return null;
        }
    }
}
