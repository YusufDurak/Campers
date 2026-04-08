using Unity.Netcode;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    [SerializeField] private GameObject connectionUI;

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        HideUI();
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        HideUI();
    }

    private void HideUI()
    {
        if (connectionUI != null)
            connectionUI.SetActive(false);
    }
}
