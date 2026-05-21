// PlayerSpawnManager is a scene-placed NetworkBehaviour in GameScene. It is
// responsible for relocating players to their correct slot once they enter
// GameScene (either because they migrated here from LobbyScene, or because
// they connected directly in-progress).
//
// Why this exists:
//   The default NetworkManager player spawn happens at connection time, while
//   the host/client are still in LobbyScene. Their NetworkObjects then migrate
//   to GameScene at whatever transform they ended up at in the lobby. This
//   manager teleports them to the designated PlayerSpawnPoint_Host /
//   PlayerSpawnPoint_Client transforms after GameScene is up.
//
// Manual setup (GameScene):
//   - Create two empty GameObjects: "PlayerSpawnPoint_Host" and
//     "PlayerSpawnPoint_Client". Place them where you want the players to
//     start. (No components needed; just transforms.)
//   - Create an empty GameObject: "PlayerSpawnManager".
//       * Attach this script. RequireComponent below adds NetworkObject.
//       * In the Inspector, drag the two PlayerSpawnPoint transforms into
//         hostSpawn / clientSpawn.
//   - Because this is a scene NetworkObject, no Network Prefab registration
//     is required. Defaults on NetworkObject are fine.
//
// ClientNetworkTransform handling:
//   Player movement uses a client-authoritative NetworkTransform
//   (ClientNetworkTransform), so a server-side transform write on the
//   non-host client's player is immediately overridden by the owning
//   client's authoritative position. To work around this, the server
//   sends ApplyTeleportClientRpc targeted only at the owning client. The
//   owner then performs the actual transform write, which ClientNetworkTransform
//   replicates back to the server and other peers normally.
//
// CharacterController handling:
//   PlayerMovement uses a CharacterController. CharacterController caches an
//   internal capsule position that can ignore a raw transform write, so the
//   owner-side RPC handler briefly disables it, writes the new transform, then
//   re-enables it.

using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Campbound.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerSpawnManager : NetworkBehaviour
    {
        [Header("Spawn Slots")]
        [SerializeField] private Transform hostSpawn;
        [SerializeField] private Transform clientSpawn;

        [Header("Timing")]
        [Tooltip("Delay before retroactively teleporting players that were already connected when GameScene loaded.")]
        [SerializeField] private float initialTeleportDelay = 1f;

        private bool subscribedToConnect;
        private Coroutine initialTeleportRoutine;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                subscribedToConnect = true;
            }

            // Players are very likely already spawned and migrated when this
            // scene NetworkObject comes online (LobbyScene -> GameScene). Their
            // OnClientConnectedCallback has long since fired. Sweep all
            // currently-connected clients after a short delay so NGO has a
            // chance to finish the scene migration before we move them.
            initialTeleportRoutine = StartCoroutine(TeleportAllAfterDelay());

            Debug.Log("[PlayerSpawnManager] OnNetworkSpawn (server): subscribed to OnClientConnectedCallback and scheduled initial sweep.");
        }

        public override void OnNetworkDespawn()
        {
            if (subscribedToConnect && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                subscribedToConnect = false;
            }

            if (initialTeleportRoutine != null)
            {
                StopCoroutine(initialTeleportRoutine);
                initialTeleportRoutine = null;
            }
        }

        private IEnumerator TeleportAllAfterDelay()
        {
            yield return new WaitForSeconds(initialTeleportDelay);

            if (!IsServer) yield break;
            if (NetworkManager.Singleton == null) yield break;

            var connectedIds = NetworkManager.Singleton.ConnectedClientsIds;
            Debug.Log($"[PlayerSpawnManager] Initial sweep: {connectedIds.Count} connected client(s).");

            foreach (ulong clientId in connectedIds)
            {
                TeleportClient(clientId);
            }

            initialTeleportRoutine = null;
        }

        private void HandleClientConnected(ulong clientId)
        {
            // Covers join-in-progress: a client that connects after GameScene
            // is already up will be placed at the correct slot immediately.
            Debug.Log($"[PlayerSpawnManager] HandleClientConnected: clientId {clientId}.");
            TeleportClient(clientId);
        }

        private void TeleportClient(ulong clientId)
        {
            if (!IsServer) return;

            var spawnManager = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager : null;
            if (spawnManager == null)
            {
                Debug.LogWarning("[PlayerSpawnManager] SpawnManager is null.");
                return;
            }

            NetworkObject playerObject = spawnManager.GetPlayerNetworkObject(clientId);
            if (playerObject == null)
            {
                Debug.LogWarning($"[PlayerSpawnManager] No player NetworkObject found for clientId {clientId} (not yet spawned?).");
                return;
            }

            bool isHost = (clientId == NetworkManager.ServerClientId);
            Transform target = isHost ? hostSpawn : clientSpawn;

            if (target == null)
            {
                Debug.LogError($"[PlayerSpawnManager] {(isHost ? "hostSpawn" : "clientSpawn")} reference is not assigned in the Inspector.");
                return;
            }

            // Send the teleport request to the owning client only. The owner is
            // authoritative for ClientNetworkTransform, so only they can produce
            // a position that won't be overwritten by the next client tick.
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };

            ApplyTeleportClientRpc(playerObject.NetworkObjectId, target.position, target.rotation, rpcParams);

            Debug.Log($"[PlayerSpawnManager] Sent ApplyTeleportClientRpc to clientId {clientId} ({(isHost ? "HOST" : "CLIENT")}) -> '{target.name}' at {target.position}.");
        }

        [ClientRpc]
        private void ApplyTeleportClientRpc(ulong playerNetworkObjectId, Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
        {
            var spawnManager = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager : null;
            if (spawnManager == null) return;

            if (!spawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerObject) || playerObject == null)
            {
                Debug.LogWarning($"[PlayerSpawnManager] ApplyTeleportClientRpc: cannot resolve NetworkObjectId {playerNetworkObjectId} on this client.");
                return;
            }

            // Extra safety: RPC was targeted at the owning client, but if it
            // somehow reaches a non-owner instance, do nothing.
            if (!playerObject.IsOwner) return;

            // CharacterController caches its own position; raw transform writes
            // can be ignored or clamped to the cached value. Toggle the
            // component off, write the transform, then turn it back on so the
            // next Move() starts from the new pose.
            CharacterController controller = playerObject.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            playerObject.transform.SetPositionAndRotation(position, rotation);

            if (controller != null) controller.enabled = true;

            Debug.Log($"[PlayerSpawnManager] Owner ({playerObject.OwnerClientId}) applied teleport for NetworkObjectId {playerNetworkObjectId} -> {position}.");
        }
    }
}
