// Campfire is a plain MonoBehaviour (NOT a NetworkBehaviour). The Campfire
// GameObject in GameScene is a scene object; interaction logic is server-
// authoritative because PlayerInteraction sends a ServerRpc that calls
// TryAddWood here on the server side.
//
// Manual setup (GameScene's "Campfire" GameObject):
//   - Existing SphereCollider (Is Trigger = true, radius 2) is fine as-is.
//   - Attach this Campfire script.
//   - No NetworkObject is required on the Campfire (interaction goes through
//     the player's PlayerInteraction ServerRpc, which already runs on server).

using UnityEngine;

namespace Campbound.Gameplay
{
    using Campbound.Network;
    using Campbound.Player;

    public class Campfire : MonoBehaviour
    {
        // Server-only. Called from PlayerInteraction.AddWoodToFireServerRpc
        // after the server has verified the player is close enough.
        public void TryAddWood(PlayerInventory inventory)
        {
            if (inventory == null) return;

            if (inventory.WoodCount.Value <= 0)
            {
                Debug.Log($"[Campfire] AddWood denied: clientId {inventory.OwnerClientId} has no wood.");
                return;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[Campfire] GameManager.Instance is null; cannot add wood to fire.");
                return;
            }

            inventory.WoodCount.Value--;
            GameManager.Instance.AddWoodToFire();

            Debug.Log($"[Campfire] clientId {inventory.OwnerClientId} added wood to fire. Remaining WoodCount: {inventory.WoodCount.Value}, FireHeat: {GameManager.Instance.FireHeat.Value:F1}");
        }
    }
}
