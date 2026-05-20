// LobbyManager is a scene-placed NetworkBehaviour that lives on a "LobbyManager"
// GameObject inside LobbyScene. It tracks per-side "ready" state and, when both
// host and client are ready, transitions to GameScene via NGO's networked scene
// manager. After GameScene finishes loading on all clients, it triggers
// GameManager.StartMatch() on the server.
//
// Setup checklist:
//   - Attach this script to a scene GameObject named "LobbyManager" in LobbyScene
//   - Add a NetworkObject component on the same GameObject (RequireComponent below
//     will add it automatically). Defaults are fine.
//   - NetworkManager must have "Enable Scene Management" ON.
//   - GameScene must be in Build Settings (index 1 per project convention).
//
// Because this is a scene NetworkObject (not a runtime-spawned prefab), it does
// NOT need to be added to the Network Prefab list.

using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Campbound.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public class LobbyManager : NetworkBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        public NetworkVariable<bool> HostReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> ClientReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private const string GameSceneName = "GameScene";
        private const float StartDelaySeconds = 1f;

        private bool startingMatch;
        private Coroutine startMatchRoutine;

        public override void OnNetworkSpawn()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[LobbyManager] Another instance already exists. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            HostReady.OnValueChanged += OnAnyReadyChanged;
            ClientReady.OnValueChanged += OnAnyReadyChanged;
        }

        public override void OnNetworkDespawn()
        {
            HostReady.OnValueChanged -= OnAnyReadyChanged;
            ClientReady.OnValueChanged -= OnAnyReadyChanged;

            if (startMatchRoutine != null)
            {
                StopCoroutine(startMatchRoutine);
                startMatchRoutine = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            if (senderId == NetworkManager.ServerClientId)
            {
                HostReady.Value = ready;
            }
            else
            {
                ClientReady.Value = ready;
            }
        }

        private void OnAnyReadyChanged(bool previousValue, bool newValue)
        {
            if (!IsServer) return;
            if (startingMatch) return;
            if (!HostReady.Value || !ClientReady.Value) return;

            startingMatch = true;
            startMatchRoutine = StartCoroutine(StartMatchAfterDelay());
        }

        private IEnumerator StartMatchAfterDelay()
        {
            yield return new WaitForSeconds(StartDelaySeconds);

            if (!IsServer)
            {
                startingMatch = false;
                startMatchRoutine = null;
                yield break;
            }

            if (!HostReady.Value || !ClientReady.Value)
            {
                startingMatch = false;
                startMatchRoutine = null;
                yield break;
            }

            var sceneManager = NetworkManager.SceneManager;
            if (sceneManager == null)
            {
                Debug.LogError("[LobbyManager] NetworkSceneManager is null. Is 'Enable Scene Management' on?");
                startingMatch = false;
                startMatchRoutine = null;
                yield break;
            }

            sceneManager.OnLoadEventCompleted += HandleGameSceneLoaded;

            var status = sceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[LobbyManager] LoadScene failed with status: {status}");
                sceneManager.OnLoadEventCompleted -= HandleGameSceneLoaded;
                startingMatch = false;
                startMatchRoutine = null;
            }
        }

        // Static handler: by the time this fires, LobbyManager has been despawned
        // along with LobbyScene (LoadSceneMode.Single), so we must not rely on
        // instance state.
        private static void HandleGameSceneLoaded(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (sceneName != GameSceneName) return;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SceneManager != null)
            {
                nm.SceneManager.OnLoadEventCompleted -= HandleGameSceneLoaded;
            }

            if (nm == null || !nm.IsServer) return;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartMatch();
            }
            else
            {
                Debug.LogWarning("[LobbyManager] GameScene loaded but GameManager.Instance is null. Did you forget to place a GameManager NetworkObject in GameScene?");
            }
        }
    }
}
