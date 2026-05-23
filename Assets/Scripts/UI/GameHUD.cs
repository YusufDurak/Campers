// GameHUD is a plain MonoBehaviour that mirrors the GameManager's
// NetworkVariables (FireHeat, MatchTimer) and the local player's
// PlayerInventory.WoodCount into UI elements every frame. No network logic
// here — everything is read-only on the client side.
//
// Manual setup (GameScene HUD Canvas):
//   1. Under HUD Canvas create an empty GameObject "GameplayHUD".
//   2. Under GameplayHUD:
//        * UI -> Slider, name "FireHeatSlider" (top center, ~700x40, Min=0,
//          Max=1, Whole Numbers OFF, Interactable OFF). Default value 0.6.
//        * TMP - Text named "TimerText" (top right or top center-right,
//          ~50pt, anchor top, default placeholder "3:00").
//        * TMP - Text named "WoodCountText" (bottom-left anchor, ~40pt,
//          default placeholder "Wood: 0/3").
//   3. Attach this script to the HUD Canvas (or any persistent GameObject
//      within GameScene). Wire the Inspector references:
//        - Fire Heat Slider -> FireHeatSlider
//        - Fire Heat Fill   -> FireHeatSlider/Fill Area/Fill (the Image
//                              component is the one that gets recolored)
//        - Timer Text       -> TimerText
//        - Wood Count Text  -> WoodCountText
//   4. Leave GameplayHUD active by default. Update() guards against missing
//      GameManager / player references during the LobbyScene phase, so it's
//      safe to keep enabled.

using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Campbound.Network;
using Campbound.Player;

namespace Campbound.UI
{
    public class GameHUD : MonoBehaviour
    {
        [Header("Fire Heat")]
        [SerializeField] private Slider fireHeatSlider;
        [SerializeField] private Image fireHeatFill;

        [Header("Timer")]
        [SerializeField] private TMP_Text timerText;

        [Header("Wood Count")]
        [SerializeField] private TMP_Text woodCountText;

        // FireHeat thresholds: > High -> green, > Mid -> yellow, otherwise red.
        private const float FireHeatHighThreshold = 60f;
        private const float FireHeatMidThreshold = 30f;
        private const float FireHeatMax = 100f;

        // Cache the PlayerInventory lookup so we aren't doing a GetComponent
        // every frame. Re-resolved when the local PlayerObject changes (e.g.
        // first time it spawns after entering GameScene).
        private NetworkObject _cachedPlayerObject;
        private PlayerInventory _cachedInventory;

        private void Update()
        {
            if (GameManager.Instance == null) return;

            UpdateFireHeat();
            UpdateTimer();
            UpdateWoodCount();
        }

        private void UpdateFireHeat()
        {
            float fireHeat = GameManager.Instance.FireHeat.Value;

            if (fireHeatSlider != null)
            {
                fireHeatSlider.value = Mathf.Clamp01(fireHeat / FireHeatMax);
            }

            if (fireHeatFill != null)
            {
                if (fireHeat > FireHeatHighThreshold)
                {
                    fireHeatFill.color = Color.green;
                }
                else if (fireHeat > FireHeatMidThreshold)
                {
                    fireHeatFill.color = Color.yellow;
                }
                else
                {
                    fireHeatFill.color = Color.red;
                }
            }
        }

        private void UpdateTimer()
        {
            if (timerText == null) return;

            float matchTimer = Mathf.Max(0f, GameManager.Instance.MatchTimer.Value);
            int minutes = Mathf.FloorToInt(matchTimer / 60f);
            int seconds = Mathf.FloorToInt(matchTimer % 60f);
            timerText.text = $"{minutes}:{seconds:00}";
        }

        private void UpdateWoodCount()
        {
            if (woodCountText == null) return;

            PlayerInventory inventory = ResolveLocalInventory();
            if (inventory != null)
            {
                woodCountText.text = $"Wood: {inventory.WoodCount.Value}/{PlayerInventory.MaxWood}";
            }
            else
            {
                woodCountText.text = "Wood: -/-";
            }
        }

        private PlayerInventory ResolveLocalInventory()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return null;

            NetworkClient localClient = nm.LocalClient;
            if (localClient == null) return null;

            NetworkObject playerObject = localClient.PlayerObject;
            if (playerObject == null)
            {
                _cachedPlayerObject = null;
                _cachedInventory = null;
                return null;
            }

            if (playerObject != _cachedPlayerObject)
            {
                _cachedPlayerObject = playerObject;
                _cachedInventory = playerObject.GetComponent<PlayerInventory>();
            }

            return _cachedInventory;
        }
    }
}
