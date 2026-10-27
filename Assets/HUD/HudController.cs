
using UnityEngine;
using UnityEngine.UIElements;

public class HUDController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Configuracao")]
    [SerializeField] private bool showEnemyPanelAtStart = false;

    private VisualElement playerHealthFill;
    private VisualElement enemyPanel;
    private VisualElement enemyHealthFill;
    private Label enemyHealthText;
    private Label dashText;

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

        VisualElement root = uiDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError("[HUD] Root Visual Element nao encontrado.");
            enabled = false;
            return;
        }

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

    private void Update()
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
