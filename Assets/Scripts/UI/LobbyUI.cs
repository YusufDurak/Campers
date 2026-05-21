// LobbyUI is a plain MonoBehaviour (NOT a NetworkBehaviour). It lives on the
// LobbyScene's Canvas and wires up the connection / ready flow:
//   - Initially shows ConnectionPanel (Host/Client buttons handled by NetworkUI)
//   - When the local client connects, swaps to ReadyPanel (cursor stays locked
//     since ready toggling is keyboard-driven via readyKey)
//   - Toggles a local "isReady" bool on each press of readyKey (or the Ready
//     button, if clicked) and reports the new value to the server via
//     LobbyManager.SetReadyServerRpc
//   - Mirrors LobbyManager.HostReady / ClientReady NetworkVariables into a
//     human-readable status text
//
// LobbyManager.Instance becomes non-null only after the scene NetworkObject
// spawns, which happens after host/client startup. We therefore subscribe to its
// NetworkVariables inside a coroutine that waits for Instance.

using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Campbound.Network;

namespace Campbound.UI
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Buttons & Text")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private TMP_Text statusText;

        [Header("Panels")]
        [SerializeField] private GameObject readyPanel;
        [SerializeField] private GameObject connectionPanel;

        [Header("Input")]
        [SerializeField] private KeyCode readyKey = KeyCode.R;

        private bool isReady;
        private bool subscribedToLobby;
        private Coroutine waitForLobbyRoutine;

        private void Start()
        {
            if (readyPanel != null) readyPanel.SetActive(false);
            if (connectionPanel != null) connectionPanel.SetActive(true);

            if (readyButtonText != null) readyButtonText.text = $"Press {readyKey} to Ready";
            if (statusText != null) statusText.text = string.Empty;

            if (readyButton != null)
            {
                readyButton.onClick.AddListener(OnReadyButtonClicked);
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
        }

        private void OnDestroy()
        {
            if (readyButton != null)
            {
                readyButton.onClick.RemoveListener(OnReadyButtonClicked);
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }

            if (waitForLobbyRoutine != null)
            {
                StopCoroutine(waitForLobbyRoutine);
                waitForLobbyRoutine = null;
            }

            UnsubscribeFromLobby();
        }

        private void Update()
        {
            if (readyPanel == null || !readyPanel.activeSelf) return;

            if (Input.GetKeyDown(readyKey))
            {
                OnReadyButtonClicked();
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            if (connectionPanel != null) connectionPanel.SetActive(false);
            if (readyPanel != null) readyPanel.SetActive(true);

            // ReadyPanel is now keyboard-driven (readyKey toggles ready state),
            // so we no longer need the cursor visible. Lock it for consistency
            // with the in-game state.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (waitForLobbyRoutine != null) StopCoroutine(waitForLobbyRoutine);
            waitForLobbyRoutine = StartCoroutine(WaitForLobbyManager());
        }

        private IEnumerator WaitForLobbyManager()
        {
            while (LobbyManager.Instance == null)
            {
                yield return null;
            }

            SubscribeToLobby();
            UpdateStatusText();
            waitForLobbyRoutine = null;
        }

        private void SubscribeToLobby()
        {
            if (subscribedToLobby) return;
            if (LobbyManager.Instance == null) return;

            LobbyManager.Instance.HostReady.OnValueChanged += OnAnyReadyChanged;
            LobbyManager.Instance.ClientReady.OnValueChanged += OnAnyReadyChanged;
            subscribedToLobby = true;
        }

        private void UnsubscribeFromLobby()
        {
            if (!subscribedToLobby) return;

            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.HostReady.OnValueChanged -= OnAnyReadyChanged;
                LobbyManager.Instance.ClientReady.OnValueChanged -= OnAnyReadyChanged;
            }

            subscribedToLobby = false;
        }

        private void OnAnyReadyChanged(bool previousValue, bool newValue)
        {
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            if (statusText == null) return;
            if (LobbyManager.Instance == null)
            {
                statusText.text = string.Empty;
                return;
            }

            bool hostReady = LobbyManager.Instance.HostReady.Value;
            bool clientReady = LobbyManager.Instance.ClientReady.Value;

            if (hostReady && clientReady)
            {
                statusText.text = "Both ready! Starting...";
            }
            else if (hostReady || clientReady)
            {
                statusText.text = "Waiting for other player...";
            }
            else
            {
                statusText.text = $"Press {readyKey} when ready";
            }
        }

        private void OnReadyButtonClicked()
        {
            if (LobbyManager.Instance == null)
            {
                Debug.LogWarning("[LobbyUI] LobbyManager is not spawned yet; ignoring ready press.");
                return;
            }

            isReady = !isReady;
            LobbyManager.Instance.SetReadyServerRpc(isReady);

            if (readyButtonText != null)
            {
                readyButtonText.text = isReady
                    ? $"Ready ({readyKey} to cancel)"
                    : $"Press {readyKey} to Ready";
            }
        }
    }
}
