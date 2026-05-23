// EndPanelUI is a plain MonoBehaviour that lives on GameScene's HUD Canvas.
// GameManager.EndGameClientRpc fires on every client when the match ends and
// calls EndPanelUI.Instance.ShowResult(won) to display the result overlay.
//
// Manual setup (GameScene HUD / Canvas):
//   1. Under your HUD Canvas, create a Panel named "EndPanel".
//        - Anchor: stretch (full screen).
//        - Background image color: 0,0,0,180 (semi-transparent black).
//   2. Inside EndPanel:
//        - TextMeshPro - Text named "ResultText" (centered, large font ~80pt).
//        - Button - TextMeshPro named "ReturnButton" (anchored bottom center,
//          its child text reading "Quit").
//   3. Attach this script to the HUD Canvas (or any persistent GameObject in
//      GameScene). Wire the Inspector references:
//        - End Panel       -> EndPanel GameObject
//        - Result Text     -> ResultText (TMP_Text component)
//        - Return Button   -> ReturnButton
//   4. Disable EndPanel by default (uncheck its top-left tickbox in Inspector).
//      Start() also calls SetActive(false) defensively.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Campbound.UI
{
    public class EndPanelUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject endPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button returnButton;

        public static EndPanelUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[EndPanelUI] Another instance already exists. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (endPanel != null) endPanel.SetActive(false);

            if (returnButton != null)
            {
                returnButton.onClick.AddListener(OnReturnClicked);
            }
        }

        private void OnDestroy()
        {
            if (returnButton != null)
            {
                returnButton.onClick.RemoveListener(OnReturnClicked);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ShowResult(bool won)
        {
            if (endPanel == null)
            {
                Debug.LogWarning("[EndPanelUI] endPanel reference is missing; cannot show result.");
                return;
            }

            endPanel.SetActive(true);

            if (resultText != null)
            {
                resultText.text = won ? "You Survived!" : "Fire Died";
                resultText.color = won ? Color.green : Color.red;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log($"[EndPanelUI] ShowResult(won={won}).");
        }

        private void OnReturnClicked()
        {
            Debug.Log("[EndPanelUI] Return/Quit button clicked.");

#if UNITY_EDITOR
            // Application.Quit is a no-op in the Editor, so stop play mode for
            // a usable in-Editor experience.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
