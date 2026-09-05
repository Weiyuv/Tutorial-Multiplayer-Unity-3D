using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    public TMP_InputField ipInput;
    public TMP_InputField portInput;

    public void Connect()
    {
        string ip = ipInput.text;

        if (!ushort.TryParse(portInput.text, out ushort port))
        {
            Debug.LogError("Porta inválida!");
            return;
        }

        UnityTransport transport =
            NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData(ip, port);

        NetworkManager.Singleton.StartClient();
    }

    public void Host()
    {
        NetworkManager.Singleton.StartHost();
    }
}