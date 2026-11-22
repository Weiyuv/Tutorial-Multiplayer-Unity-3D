
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class HUDController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Configuracao")]
    [SerializeField] private bool showEnemyPanelAtStart = false;

    private VisualElement root;
    private VisualElement playerHealthFill;
    private VisualElement enemyPanel;
    private VisualElement enemyHealthFill;

    private Label enemyHealthText;
    private Label dashText;

    private MechaHealth localPlayerHealth;

    private float dashCooldownRemaining;
    private bool isDashCoolingDown;

    public float DashCooldownRemaining => dashCooldownRemaining;
    public bool IsDashCoolingDown => isDashCoolingDown;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[HUD] UIDocument nao encontrado.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError("[HUD] Root Visual Element nao encontrado.");
            enabled = false;
            return;
        }

        root.pickingMode = PickingMode.Ignore;
        IgnorarCliquesNosElementosVisuais(root);

        playerHealthFill = root.Q<VisualElement>("PlayerHealthFill");
        enemyPanel = root.Q<VisualElement>("EnemyPanel");
        enemyHealthFill = root.Q<VisualElement>("EnemyHealthFill");
        enemyHealthText = root.Q<Label>("EnemyHealthText");
        dashText = root.Q<Label>("DashText");

        if (playerHealthFill == null ||
            enemyPanel == null ||
            enemyHealthFill == null ||
            enemyHealthText == null ||
            dashText == null)
        {
            Debug.LogError(
                "[HUD] Elementos ausentes no UXML. Verifique os nomes: " +
                "PlayerHealthFill, EnemyPanel, EnemyHealthFill, " +
                "EnemyHealthText e DashText."
            );

            enabled = false;
            return;
        }

        enemyPanel.style.display = showEnemyPanelAtStart
            ? DisplayStyle.Flex
            : DisplayStyle.None;

        SetPlayerHealth(100f, 100f);
        SetEnemyHealth(100f, 100f);
        HideEnemyHealth();
        SetDashReady();
    }

    private void OnEnable()
    {
        TryConnectPlayerHealth();
    }

    private void OnDisable()
    {
        DisconnectPlayerHealth();
    }

    private void Update()
    {
        // Tenta encontrar o jogador local até o Netcode terminar
        // de criar e associar o PlayerObject.
        if (localPlayerHealth == null)
            TryConnectPlayerHealth();

        UpdateDashCooldown();
    }

    private void TryConnectPlayerHealth()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsClient)
            return;

        if (NetworkManager.Singleton.LocalClient == null)
            return;

        NetworkObject playerObject =
            NetworkManager.Singleton.LocalClient.PlayerObject;

        if (playerObject == null)
            return;

        MechaHealth health =
            playerObject.GetComponent<MechaHealth>();

        if (health == null)
        {
            Debug.LogError(
                "[HUD] MechaHealth nao encontrado no PlayerObject."
            );
            return;
        }

        if (localPlayerHealth == health)
            return;

        DisconnectPlayerHealth();

        localPlayerHealth = health;

        localPlayerHealth.HealthChanged += OnPlayerHealthChanged;

        // Atualiza imediatamente com a vida atual.
        SetPlayerHealth(
            localPlayerHealth.CurrentHealth,
            localPlayerHealth.MaxHealth
        );

        Debug.Log("[HUD] Conectado ao MechaHealth do jogador local.");
    }

    private void DisconnectPlayerHealth()
    {
        if (localPlayerHealth == null)
            return;

        localPlayerHealth.HealthChanged -= OnPlayerHealthChanged;
        localPlayerHealth = null;
    }

    private void OnPlayerHealthChanged(
        float currentHealth,
        float maxHealth)
    {
        SetPlayerHealth(currentHealth, maxHealth);
    }

    private void IgnorarCliquesNosElementosVisuais(
        VisualElement elemento)
    {
        if (elemento == null)
            return;

        elemento.pickingMode = PickingMode.Ignore;

        foreach (VisualElement filho in elemento.Children())
        {
            IgnorarCliquesNosElementosVisuais(filho);
        }
    }

    private void UpdateDashCooldown()
    {
        if (!isDashCoolingDown)
            return;

        dashCooldownRemaining -= Time.deltaTime;

        if (dashCooldownRemaining <= 0f)
        {
            SetDashReady();
            return;
        }

        dashText.text =
            "DASH " + dashCooldownRemaining.ToString("F1") + "s";
    }

    public void SetPlayerHealth(float currentHealth, float maxHealth)
    {
        if (playerHealthFill == null)
            return;

        float percentage = maxHealth > 0f
            ? Mathf.Clamp01(currentHealth / maxHealth)
            : 0f;

        playerHealthFill.style.width =
            Length.Percent(percentage * 100f);
    }

    public void SetEnemyHealth(float currentHealth, float maxHealth)
    {
        if (enemyHealthFill == null || enemyHealthText == null)
            return;

        float percentage = maxHealth > 0f
            ? Mathf.Clamp01(currentHealth / maxHealth)
            : 0f;

        enemyHealthFill.style.width =
            Length.Percent(percentage * 100f);

        enemyHealthText.text =
            Mathf.CeilToInt(percentage * 100f) + "%";

        enemyPanel.style.display = DisplayStyle.Flex;
    }

    public void SetEnemyTarget(
        string targetName,
        float currentHealth,
        float maxHealth)
    {
        SetEnemyHealth(currentHealth, maxHealth);
    }

    public void HideEnemyHealth()
    {
        if (enemyPanel != null)
            enemyPanel.style.display = DisplayStyle.None;
    }

    public void ClearEnemyTarget()
    {
        HideEnemyHealth();
    }

    public void StartDashCooldown(float duration)
    {
        if (dashText == null)
            return;

        if (duration <= 0f)
        {
            SetDashReady();
            return;
        }

        dashCooldownRemaining = duration;
        isDashCoolingDown = true;

        dashText.RemoveFromClassList("ready");
        dashText.AddToClassList("cooldown");

        dashText.text =
            "DASH " + dashCooldownRemaining.ToString("F1") + "s";
    }

    public void SetDashReady()
    {
        dashCooldownRemaining = 0f;
        isDashCoolingDown = false;

        if (dashText == null)
            return;

        dashText.RemoveFromClassList("cooldown");
        dashText.AddToClassList("ready");
        dashText.text = "DASH READY";
    }
}
