// WoodPickup lives on the Wood prefab. It is a NetworkObject spawned at runtime
// by WoodSpawner. Pickup is server-authoritative: PlayerInteraction sends a
// ServerRpc that calls TryPickup here, which mutates the player's inventory
// and despawns this NetworkObject.
//
// Manual setup (Wood prefab):
//   - Cube mesh (scale ~0.5, 0.3, 0.5, brown material) for visuals.
//   - SphereCollider, Is Trigger = true, Radius = 1.5 — used by
//     PlayerInteraction's OverlapSphere search. (NOT auto-added; configure
//     this in the prefab Inspector.)
//   - NetworkObject component.
//   - This WoodPickup script.
//   - Register the Wood prefab in NetworkManager's Network Prefabs List so
//     WoodSpawner can spawn it across the network.

using Unity.Netcode;
using UnityEngine;

namespace Campbound.Gameplay
{
    using Campbound.Player;

    [RequireComponent(typeof(NetworkObject))]
    public class WoodPickup : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            Debug.Log($"[WoodPickup] spawned with NetworkObjectId {NetworkObjectId} at {transform.position}. IsServer: {IsServer}");
        }

        // Server-only. Called from PlayerInteraction.PickupWoodServerRpc after
        // resolving this object from the NetworkObjectId the client sent.
        public void TryPickup(PlayerInventory inventory)
        {
            if (!IsServer) return;
            if (inventory == null) return;

            if (inventory.WoodCount.Value >= PlayerInventory.MaxWood)
            {
                Debug.Log($"[WoodPickup] Pickup denied: clientId {inventory.OwnerClientId} inventory full.");
                return;
            }

            inventory.WoodCount.Value++;
            Debug.Log($"[WoodPickup] clientId {inventory.OwnerClientId} picked up wood (NetworkObjectId {NetworkObjectId}). New WoodCount: {inventory.WoodCount.Value}");

            // Despawn AND destroy the GameObject on all clients.
            NetworkObject networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn(true);
            }
        }
    }
}
