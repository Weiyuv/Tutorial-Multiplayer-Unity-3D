using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;

public class LANMultiplayer : MonoBehaviour
{
    [Header("Interface")]
    public Canvas menuCanvas;
    public TMP_InputField ipInput;
    public TMP_InputField portInput;
    public TMP_Text statusText;

    [Header("Movimento do jogador")]
    public MonoBehaviour[] scriptsMovimentoDoPlayer;

    [Header("Configuração")]
    public ushort portaPadrao = 7777;

    private NetworkManager networkManager;
    private UnityTransport transport;
    private bool eventosRegistrados;
    private bool movimentoLiberado;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BloquearMovimento();
        LocalizarNetworkManager();
    }

    private void Start()
    {
        if (portInput != null && string.IsNullOrWhiteSpace(portInput.text))
            portInput.text = portaPadrao.ToString();

        if (menuCanvas != null)
            menuCanvas.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (networkManager == null)
            AtualizarStatus("NetworkManager não encontrado na cena.");
        else
            AtualizarStatus("Digite o IP e a porta ou crie uma sala.");
    }

    private void Update()
    {
        if (menuCanvas != null && menuCanvas.gameObject.activeInHierarchy)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (movimentoLiberado)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void BloquearMovimento()
    {
        movimentoLiberado = false;

        if (scriptsMovimentoDoPlayer == null)
            return;

        foreach (MonoBehaviour script in scriptsMovimentoDoPlayer)
        {
            if (script != null && script != this)
                script.enabled = false;
        }
    }

    private void LiberarMovimento()
    {
        movimentoLiberado = true;

        if (scriptsMovimentoDoPlayer != null)
        {
            foreach (MonoBehaviour script in scriptsMovimentoDoPlayer)
            {
                if (script != null && script != this)
                    script.enabled = true;
            }
        }

        FecharMenu();
        AtualizarStatus("Conectado à sala!");
    }

    private void LocalizarNetworkManager()
    {
        if (networkManager != null && transport != null)
            return;

        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            networkManager = FindObjectOfType<NetworkManager>();

        if (networkManager == null)
            return;

        transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            AtualizarStatus("Adicione Unity Transport ao NetworkManager.");
            return;
        }

        if (!eventosRegistrados)
        {
            networkManager.OnClientConnectedCallback += AoConectarCliente;
            networkManager.OnClientDisconnectCallback += AoDesconectarCliente;
            eventosRegistrados = true;
        }
    }

    public void CriarPartida()
    {
        if (!ValidarComponentes())
            return;

        if (!TryGetPort(out ushort porta))
            return;

        transport.SetConnectionData("0.0.0.0", porta, "0.0.0.0");

        AtualizarStatus("Criando sala...");

        bool iniciado = networkManager.StartHost();

        if (iniciado)
        {
            LiberarMovimento();
        }
        else
        {
            AtualizarStatus("Falha ao criar a sala. Verifique o Console.");
        }
    }

    public void EntrarPorIP()
    {
        if (!ValidarComponentes())
            return;

        string ip = ipInput != null ? ipInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(ip))
        {
            AtualizarStatus("Digite o IP do anfitrião.");
            return;
        }

        if (!IPAddress.TryParse(ip, out IPAddress endereco) ||
            endereco.AddressFamily != AddressFamily.InterNetwork)
        {
            AtualizarStatus("Digite um endereço IPv4 válido.");
            return;
        }

        if (!TryGetPort(out ushort porta))
            return;

        transport.SetConnectionData(ip, porta);

        AtualizarStatus("Conectando ao anfitrião...");

        bool iniciado = networkManager.StartClient();

        if (!iniciado)
        {
            AtualizarStatus("Falha ao iniciar a conexão. Verifique o Console.");
        }
        else
        {
            AtualizarStatus("Aguardando conexão com o anfitrião...");
        }
    }

    private bool ValidarComponentes()
    {
        LocalizarNetworkManager();

        if (networkManager == null)
        {
            AtualizarStatus("NetworkManager não encontrado na cena.");
            return false;
        }

        if (transport == null)
        {
            AtualizarStatus("Unity Transport não encontrado no NetworkManager.");
            return false;
        }

        if (networkManager.IsListening)
        {
            AtualizarStatus("O NetworkManager já está em execução.");
            return false;
        }

        return true;
    }

    private bool TryGetPort(out ushort porta)
    {
        porta = portaPadrao;

        string texto = portInput != null ? portInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(texto))
            texto = portaPadrao.ToString();

        if (!ushort.TryParse(texto, out porta) || porta == 0)
        {
            AtualizarStatus("Digite uma porta entre 1 e 65535.");
            return false;
        }

        return true;
    }

    private void AoConectarCliente(ulong clientId)
    {
        if (networkManager == null)
            return;

        if (clientId == networkManager.LocalClientId)
        {
            LiberarMovimento();
        }
    }

    private void AoDesconectarCliente(ulong clientId)
    {
        if (networkManager == null)
            return;

        if (clientId == networkManager.LocalClientId)
        {
            BloquearMovimento();

            if (menuCanvas != null)
                menuCanvas.gameObject.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            AtualizarStatus("Desconectado. Tente entrar novamente.");
        }
    }

    private void FecharMenu()
    {
        if (menuCanvas == null)
        {
            Debug.LogError("Arraste o Canvas do menu para o campo Menu Canvas no Inspector.");
            return;
        }

        menuCanvas.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void AtualizarStatus(string mensagem)
    {
        Debug.Log(mensagem);

        if (statusText != null)
            statusText.text = mensagem;
    }

    public string ObterIPLocal()
    {
        try
        {
            foreach (IPAddress ip in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch (SocketException)
        {
            return "Não foi possível obter o IP.";
        }

        return "Não foi possível obter o IP.";
    }

    private void OnDestroy()
    {
        if (networkManager != null && eventosRegistrados)
        {
            networkManager.OnClientConnectedCallback -= AoConectarCliente;
            networkManager.OnClientDisconnectCallback -= AoDesconectarCliente;
        }
    }
}