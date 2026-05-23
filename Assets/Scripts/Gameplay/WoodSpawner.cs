// WoodSpawner is a scene-placed NetworkBehaviour responsible for populating
// GameScene with Wood pickups and respawning them after they're collected.
// All spawn/despawn logic runs server-side; clients only receive replication.
//
// Manual setup (GameScene):
//   - Create an empty GameObject named "WoodSpawner".
//   - Add a NetworkObject component (RequireComponent below adds it
//     automatically when this script is attached, but configure it as a
//     scene NetworkObject — defaults are fine).
//   - Attach this script.
//   - In the Inspector:
//       * woodPrefab     -> drag the Wood prefab (with WoodPickup +
//                           NetworkObject). The prefab must also be in the
//                           NetworkManager's Network Prefabs List.
//       * spawnPoints    -> drag all WoodSpawnPoint_X scene transforms.
//       * respawnDelay   -> tune as desired (default 15s).
//
// Spawn lifecycle:
//   - On server spawn, schedule a delayed initial sweep (initialSpawnDelay)
//     so remote clients have time to finish synchronizing GameScene before
//     server-spawned Wood NetworkObjects start arriving. Spawning during
//     OnNetworkSpawn directly was found to race ahead of client scene
//     synchronization, causing the very first batch of Woods to be invisible
//     on the client; only later respawns (after the scene was fully synced)
//     would appear.
//   - Each Update (server only, during Playing state) detect Wood that have
//     been despawned (by WoodPickup.TryPickup), start a per-slot respawn
//     timer, then re-spawn the Wood when the timer elapses.

using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Campbound.Gameplay
{
    using Campbound.Network;

    [RequireComponent(typeof(NetworkObject))]
    public class WoodSpawner : NetworkBehaviour
    {
        [Header("Wood Spawning")]
        [SerializeField] private GameObject woodPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float respawnDelay = 15f;

        [Tooltip("Delay before the first wave of Woods is spawned, giving remote clients time to finish synchronizing GameScene. If the initial Woods are still invisible on the client, raise this value.")]
        [SerializeField] private float initialSpawnDelay = 1.5f;

        // index -> currently spawned Wood NetworkObject at that spawn point
        private readonly Dictionary<int, NetworkObject> activeWoods = new Dictionary<int, NetworkObject>();
        // index -> remaining time until respawn at that spawn point
        private readonly Dictionary<int, float> respawnTimers = new Dictionary<int, float>();

        private Coroutine initialSpawnRoutine;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (woodPrefab == null)
            {
                Debug.LogError("[WoodSpawner] woodPrefab is not assigned. Aborting spawn.");
                return;
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("[WoodSpawner] spawnPoints array is empty. No wood will be spawned.");
                return;
            }

            // Defer the initial spawn so clients can finish synchronizing
            // GameScene first. If we Spawn() during OnNetworkSpawn the spawn
            // messages can outrun the client's scene load and be silently
            // discarded — only later respawns would then become visible.
            initialSpawnRoutine = StartCoroutine(SpawnInitialWoodsDelayed());

            Debug.Log($"[WoodSpawner] OnNetworkSpawn (server): scheduled initial wood spawn in {initialSpawnDelay:F2}s ({spawnPoints.Length} points).");
        }

        public override void OnNetworkDespawn()
        {
            if (initialSpawnRoutine != null)
            {
                StopCoroutine(initialSpawnRoutine);
                initialSpawnRoutine = null;
            }
        }

        private IEnumerator SpawnInitialWoodsDelayed()
        {
            yield return new WaitForSeconds(initialSpawnDelay);

            // Connection / role can change while we're waiting (host disconnect,
            // shutdown, etc.) so re-check before mutating network state.
            if (!IsServer) yield break;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) yield break;
            if (spawnPoints == null) yield break;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnWoodAt(i);
            }

            Debug.Log($"[WoodSpawner] Initial spawn complete: {spawnPoints.Length} wood spawned (delay was {initialSpawnDelay:F2}s).");
            initialSpawnRoutine = null;
        }

        private void SpawnWoodAt(int index)
        {
            if (!IsServer) return;
            if (index < 0 || index >= spawnPoints.Length) return;
            if (spawnPoints[index] == null)
            {
                Debug.LogWarning($"[WoodSpawner] spawnPoints[{index}] is null.");
                return;
            }

            GameObject obj = Instantiate(woodPrefab, spawnPoints[index].position, Quaternion.identity);
            NetworkObject networkObject = obj.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("[WoodSpawner] woodPrefab has no NetworkObject component.");
                Destroy(obj);
                return;
            }

            networkObject.Spawn(destroyWithScene: true);
            activeWoods[index] = networkObject;

            Debug.Log($"[WoodSpawner] Spawned wood at index {index} (NetworkObjectId {networkObject.NetworkObjectId}).");
        }

        private void Update()
        {
            if (!IsServer) return;
            if (spawnPoints == null || spawnPoints.Length == 0) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentGameState.Value != GameState.Playing) return;

            float dt = Time.deltaTime;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                // Slot has a live wood -> nothing to do.
                if (activeWoods.TryGetValue(i, out NetworkObject wood) && wood != null && wood.IsSpawned)
                {
                    continue;
                }

                // Slot previously had a wood that has now been despawned/destroyed:
                // start a respawn timer for it.
                if (activeWoods.ContainsKey(i))
                {
                    activeWoods.Remove(i);
                    respawnTimers[i] = respawnDelay;
                    Debug.Log($"[WoodSpawner] Wood at index {i} was picked up. Respawn in {respawnDelay:F1}s.");
                    continue;
                }

                // Slot is in respawn cooldown.
                if (respawnTimers.TryGetValue(i, out float remaining))
                {
                    remaining -= dt;
                    if (remaining <= 0f)
                    {
                        respawnTimers.Remove(i);
                        SpawnWoodAt(i);
                    }
                    else
                    {
                        respawnTimers[i] = remaining;
                    }
                }
            }
        }
    }
}
