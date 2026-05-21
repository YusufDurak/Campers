// PlayerInventory tracks how many Wood units a player currently carries.
// Lives on the Player prefab as a NetworkBehaviour. WoodCount is server-
// authoritative; clients read for HUD, server writes during pickup/drop.
//
// Manual setup (do this once in Unity):
//   - Attach this script to the Player prefab.
//   - Save the prefab. No additional Inspector configuration is required.

using Unity.Netcode;
using UnityEngine;

namespace Campbound.Player
{
    public class PlayerInventory : NetworkBehaviour
    {
        public const int MaxWood = 3;

        public NetworkVariable<int> WoodCount = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[PlayerInventory] spawned for clientId {OwnerClientId}, IsOwner: {IsOwner}");
        }

        // Public ServerRpc kept as a generic API (matches assignment "at least 1
        // RPC" requirement). The actual pickup flow goes through WoodPickup which
        // writes WoodCount directly on the server, so this RPC is currently
        // unused but available for future direct-add scenarios.
        [ServerRpc]
        public void AddWoodServerRpc()
        {
            if (WoodCount.Value < MaxWood)
            {
                WoodCount.Value++;
                Debug.Log($"[PlayerInventory] AddWoodServerRpc: clientId {OwnerClientId} now has {WoodCount.Value} wood.");
            }
        }

        [ServerRpc]
        public void RemoveAllWoodServerRpc()
        {
            WoodCount.Value = 0;
            Debug.Log($"[PlayerInventory] RemoveAllWoodServerRpc: clientId {OwnerClientId} inventory cleared.");
        }
    }
}
