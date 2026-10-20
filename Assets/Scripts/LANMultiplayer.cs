
using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;

public class LANMultiplayer : MonoBehaviour
{
    public TMP_InputField ipInput;
    public TMP_InputField portInput;
    public TMP_Text statusText;

    private UnityTransport transport;
    private NetworkManager networkManager;

    private void Awake()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            statusText.text = "NetworkManager não encontrado.";
            return;
        }

        transport = networkManager.GetComponent<UnityTransport>();

        networkManager.OnClientConnectedCallback += OnConnected;
        networkManager.OnClientDisconnectCallback += OnDisconnected;
    }

    public void CriarPartida()
    {
        if (!ValidarComponentes() || networkManager.IsListening)
            return;

        if (!TryGetPort(out ushort port))
            return;

        transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");

        bool started = networkManager.StartHost();

        statusText.text = started
            ? "Partida criada na porta " + port + ". IP: " + ObterIPLocal()
            : "Não foi possível criar a partida.";
    }

    public void EntrarPorIP()
    {
        if (!ValidarComponentes() || networkManager.IsListening)
            return;

        if (!TryGetPort(out ushort port))
            return;

        string ip = ipInput.text.Trim();

        if (!IPAddress.TryParse(ip, out IPAddress endereco) ||
            endereco.AddressFamily != AddressFamily.InterNetwork)
        {
            statusText.text = "Digite um IPv4 válido.";
            return;
        }

        transport.SetConnectionData(ip, port);

        bool started = networkManager.StartClient();

        statusText.text = started
            ? "Conectando a " + ip + ":" + port + "..."
            : "Não foi possível iniciar a conexão.";
    }

    private bool ValidarComponentes()
    {
        if (networkManager == null || transport == null)
        {
            if (statusText != null)
                statusText.text = "Verifique o NetworkManager e o Unity Transport.";

            return false;
        }

        return true;
    }

    private bool TryGetPort(out ushort port)
    {
        port = 0;

        if (portInput == null ||
            !ushort.TryParse(portInput.text.Trim(), out port) ||
            port == 0)
        {
            statusText.text = "Digite uma porta entre 1 e 65535.";
            return false;
        }

        return true;
    }

    private void OnConnected(ulong clientId)
    {
        if (clientId == networkManager.LocalClientId)
            statusText.text = "Conectado à partida!";
    }

    private void OnDisconnected(ulong clientId)
    {
        if (clientId == networkManager.LocalClientId)
            statusText.text = "Desconectado da partida.";
    }

    private string ObterIPLocal()
    {
        foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
        {
            if (address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address))
            {
                return address.ToString();
            }
        }

        return "Consulte o ipconfig";
    }

    private void OnDestroy()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= OnConnected;
        networkManager.OnClientDisconnectCallback -= OnDisconnected;
    }
}
