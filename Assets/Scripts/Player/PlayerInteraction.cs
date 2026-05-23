// PlayerInteraction lives on the Player prefab. The owning client polls input
// each frame, finds the closest interactable in interactionRange via
// Physics.OverlapSphere, and sends a ServerRpc to perform the actual mutation
// server-side. Pickup has priority over Campfire: if any WoodPickup is within
// range, pressing the interact key picks it up; otherwise, if a Campfire is
// near, it attempts to add wood to the fire.
//
// Manual setup (Player prefab):
//   - Attach this script.
//   - Ensure the Player prefab has a CapsuleCollider (for physics; not strictly
//     required by this script, but typical for a player). Default Unity
//     capsule collider settings are fine.
//   - Player prefab must also have PlayerInventory (added separately).

using Unity.Netcode;
using UnityEngine;

namespace Campbound.Player
{
    using Campbound.Gameplay;
    using Campbound.Network;

    public class PlayerInteraction : NetworkBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private float interactionRange = 2f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        // Server-side max distance for the Campfire check. Slightly larger than
        // interactionRange so the interaction feels forgiving but still rejects
        // obvious cheats (player half a map away calling AddWoodToFireServerRpc).
        private const float CampfireServerRangeSqr = 3f * 3f;

        // Single global prompt set by the owning local player every frame and
        // read by the InteractionPrompt UI script. PlayerInteraction.Update is
        // disabled on non-owners (see OnNetworkSpawn), so only the local
        // player's instance ever writes to this static.
        public static string CurrentPrompt { get; private set; } = "";

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            Debug.Log($"[PlayerInteraction] enabled for owning client (OwnerClientId {OwnerClientId}).");
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                CurrentPrompt = "";
            }
        }

        private void Update()
        {
            if (GameManager.Instance == null)
            {
                CurrentPrompt = "";
                return;
            }

            if (GameManager.Instance.CurrentGameState.Value != GameState.Playing)
            {
                CurrentPrompt = "";
                return;
            }

            FindNearestInteractables(out WoodPickup nearestWood, out Campfire nearestCampfire);

            UpdatePrompt(nearestWood, nearestCampfire);

            if (!Input.GetKeyDown(interactKey)) return;

            if (nearestWood != null)
            {
                Debug.Log($"[PlayerInteraction] Sending PickupWoodServerRpc for NetworkObjectId {nearestWood.NetworkObjectId}.");
                PickupWoodServerRpc(nearestWood.NetworkObjectId);
            }
            else if (nearestCampfire != null)
            {
                Debug.Log("[PlayerInteraction] Sending AddWoodToFireServerRpc.");
                AddWoodToFireServerRpc();
            }
        }

        private void UpdatePrompt(WoodPickup nearestWood, Campfire nearestCampfire)
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            int woodCount = inventory != null ? inventory.WoodCount.Value : 0;
            bool inventoryFull = woodCount >= PlayerInventory.MaxWood;
            bool hasWood = woodCount > 0;

            if (nearestWood != null && !inventoryFull)
            {
                CurrentPrompt = "Press E to pick up wood";
            }
            else if (nearestCampfire != null && hasWood)
            {
                CurrentPrompt = "Press E to add wood to fire";
            }
            else if (nearestCampfire != null)
            {
                CurrentPrompt = "Need wood first";
            }
            else
            {
                CurrentPrompt = "";
            }
        }

        private void FindNearestInteractables(out WoodPickup nearestWood, out Campfire nearestCampfire)
        {
            nearestWood = null;
            nearestCampfire = null;

            Collider[] hits = Physics.OverlapSphere(transform.position, interactionRange);
            float bestWoodDistSqr = float.MaxValue;
            float bestCampfireDistSqr = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null) continue;

                WoodPickup wood = col.GetComponentInParent<WoodPickup>();
                if (wood != null)
                {
                    float d = (wood.transform.position - transform.position).sqrMagnitude;
                    if (d < bestWoodDistSqr)
                    {
                        bestWoodDistSqr = d;
                        nearestWood = wood;
                    }
                    continue;
                }

                Campfire fire = col.GetComponentInParent<Campfire>();
                if (fire != null)
                {
                    float d = (fire.transform.position - transform.position).sqrMagnitude;
                    if (d < bestCampfireDistSqr)
                    {
                        bestCampfireDistSqr = d;
                        nearestCampfire = fire;
                    }
                }
            }
        }

        [ServerRpc]
        private void PickupWoodServerRpc(ulong woodNetworkObjectId)
        {
            var spawnManager = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager : null;
            if (spawnManager == null)
            {
                Debug.LogWarning("[PlayerInteraction] SpawnManager is null on server.");
                return;
            }

            if (!spawnManager.SpawnedObjects.TryGetValue(woodNetworkObjectId, out NetworkObject networkObject) || networkObject == null)
            {
                Debug.Log($"[PlayerInteraction] PickupWoodServerRpc: NetworkObjectId {woodNetworkObjectId} not found (already picked up?).");
                return;
            }

            WoodPickup wood = networkObject.GetComponent<WoodPickup>();
            if (wood == null) return;

            PlayerInventory inventory = GetComponent<PlayerInventory>();
            wood.TryPickup(inventory);
        }

        [ServerRpc]
        private void AddWoodToFireServerRpc()
        {
            Campfire campfire = FindFirstObjectByType<Campfire>();
            if (campfire == null)
            {
                Debug.LogWarning("[PlayerInteraction] No Campfire found in scene.");
                return;
            }

            float distSqr = (campfire.transform.position - transform.position).sqrMagnitude;
            if (distSqr > CampfireServerRangeSqr)
            {
                Debug.Log($"[PlayerInteraction] AddWoodToFireServerRpc denied: player too far from Campfire (dist={Mathf.Sqrt(distSqr):F2}).");
                return;
            }

            PlayerInventory inventory = GetComponent<PlayerInventory>();
            campfire.TryAddWood(inventory);
        }
    }
}
