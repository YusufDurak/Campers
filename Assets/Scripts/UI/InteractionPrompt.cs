// InteractionPrompt is a plain MonoBehaviour that mirrors the static
// PlayerInteraction.CurrentPrompt into a TMP_Text every frame. The text is
// written by the local player's PlayerInteraction.Update based on whatever
// is in interactionRange and inventory state.
//
// Manual setup (GameScene HUD Canvas):
//   1. Under HUD Canvas (or the GameplayHUD GameObject if you have one) add
//      a "TextMeshPro - Text (UI)" element named "InteractionPrompt".
//        - Anchor: bottom center.
//        - Position: a bit above the Wood counter / center of the screen.
//        - Font Size ~40, color white, Alignment center+middle.
//        - Default placeholder text can be left empty (script overwrites it).
//   2. Attach this script to the HUD Canvas (or any persistent GameObject
//      in GameScene). Wire the Inspector reference:
//        - Prompt Text -> InteractionPrompt's TMP_Text component.
//   3. Leave InteractionPrompt active by default; the Update writes an empty
//      string when there is nothing to interact with, so the UI is silent
//      until the player walks into range.

using TMPro;
using UnityEngine;
using Campbound.Player;

namespace Campbound.UI
{
    public class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] private TMP_Text promptText;

        private void Update()
        {
            if (promptText == null) return;
            promptText.text = PlayerInteraction.CurrentPrompt;
        }
    }
}
