// GameManager is a scene-placed NetworkBehaviour that lives on the "GameManager"
// GameObject inside GameScene. Because it is a scene object (not a runtime-spawned
// prefab), it does NOT need to be registered in the NetworkManager's Network Prefab
// list. It does, however, require a NetworkObject component on the same GameObject.
//
// Recommended NetworkObject settings for this scene object:
//   - "Always Replicate As Root": true (it's a root scene object)
//   - "Synchronize Transform": false (this object never moves)
//   - "Active Scene Synchronization": false (we don't move it between scenes)
//   - "Scene Migration Synchronization": true (default; lets NGO migrate its scene
//     ownership correctly when GameScene is loaded as the active networked scene)
//   - "Spawn With Observers": true (default)
//   - "Dont Destroy With Owner": false (default)
//
// Since this is a scene NetworkObject, it will be spawned automatically by NGO when
// the server loads GameScene through NetworkSceneManager. Make sure Enable Scene
// Management is ON in NetworkManager and that GameScene is in Build Settings.

using Unity.Netcode;
using UnityEngine;
using Campbound.UI;

namespace Campbound.Network
{
    public enum GameState
    {
        Lobby,
        Playing,
        Won,
        Lost
    }

    [RequireComponent(typeof(NetworkObject))]
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        public NetworkVariable<float> FireHeat = new NetworkVariable<float>(
            60f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<float> MatchTimer = new NetworkVariable<float>(
            180f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<GameState> CurrentGameState = new NetworkVariable<GameState>(
            GameState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private const float MaxFireHeat = 100f;
        private const float WoodHeatBonus = 20f;
        private const float DefaultStartFireHeat = 60f;
        private const float DefaultMatchDuration = 180f;

        public override void OnNetworkSpawn()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] Another instance already exists. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!IsServer) return;
            if (CurrentGameState.Value != GameState.Playing) return;

            FireHeat.Value -= Time.deltaTime;
            MatchTimer.Value -= Time.deltaTime;

            if (FireHeat.Value <= 0f)
            {
                FireHeat.Value = 0f;
                CurrentGameState.Value = GameState.Lost;
                EndGameClientRpc(false);
                return;
            }

            if (MatchTimer.Value <= 0f)
            {
                MatchTimer.Value = 0f;
                CurrentGameState.Value = GameState.Won;
                EndGameClientRpc(true);
            }
        }

        public void StartMatch()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[GameManager] StartMatch can only be called on the server.");
                return;
            }

            FireHeat.Value = DefaultStartFireHeat;
            MatchTimer.Value = DefaultMatchDuration;
            CurrentGameState.Value = GameState.Playing;
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddWoodServerRpc()
        {
            if (CurrentGameState.Value != GameState.Playing) return;

            FireHeat.Value = Mathf.Min(FireHeat.Value + WoodHeatBonus, MaxFireHeat);
            WoodAddedToFireClientRpc();
        }

        // Server-only entry point used by Campfire interaction. Unlike the RPC
        // above, this is called directly from server code (PlayerInteraction's
        // ServerRpc -> Campfire.TryAddWood -> here) so it doesn't need its own
        // RPC plumbing. AddWoodServerRpc is kept above as the public RPC API.
        public void AddWoodToFire()
        {
            if (!IsServer) return;
            if (CurrentGameState.Value != GameState.Playing) return;

            FireHeat.Value = Mathf.Min(FireHeat.Value + WoodHeatBonus, MaxFireHeat);
            WoodAddedToFireClientRpc();
        }

        [ClientRpc]
        public void WoodAddedToFireClientRpc()
        {
            Debug.Log("Wood added to fire!");
        }

        [ClientRpc]
        public void EndGameClientRpc(bool won)
        {
            Debug.Log($"Game ended. Won: {won}");

            if (EndPanelUI.Instance != null)
            {
                EndPanelUI.Instance.ShowResult(won);
            }
            else
            {
                Debug.LogWarning("[GameManager] EndPanelUI.Instance is null; end-of-match panel will not be shown on this client.");
            }
        }
    }
}
