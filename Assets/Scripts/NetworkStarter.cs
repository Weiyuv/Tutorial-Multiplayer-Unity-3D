using Unity.Netcode;
using UnityEngine;

public class NetworkStarter : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("[NETWORK] Tentando iniciar Host...");

        bool result = NetworkManager.Singleton.StartHost();

        Debug.Log("[NETWORK] StartHost retornou: " + result);
    }
}