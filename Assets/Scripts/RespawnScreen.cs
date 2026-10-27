
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(UIDocument))]
public class RespawnScreen : MonoBehaviour
{
    private VisualElement overlay;
    private Button respawnButton;

    private bool isRequestingRespawn;

    private void Awake()
    {
        UIDocument document = GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;

        overlay = root.Q<VisualElement>("RespawnOverlay");
        respawnButton = root.Q<Button>("RespawnButton");

        if (overlay == null || respawnButton == null)
        {
            Debug.LogError(
                "[RESPAWN] O UXML precisa ter RespawnOverlay e RespawnButton."
            );

            enabled = false;
            return;
        }

        respawnButton.clicked += RequestRespawn;

        overlay.style.display = DisplayStyle.None;
        EnableRespawnButton();
    }

    public void Show()
    {
        if (overlay == null)
            return;

        isRequestingRespawn = false;

        overlay.style.display = DisplayStyle.Flex;

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        EnableRespawnButton();

        Debug.Log("[RESPAWN] Tela aberta.");
    }

    public void Hide()
    {
        if (overlay == null)
            return;

        overlay.style.display = DisplayStyle.None;

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    private void RequestRespawn()
    {
        if (isRequestingRespawn)
            return;

        isRequestingRespawn = true;
        respawnButton.SetEnabled(false);

        Debug.Log("[RESPAWN] Reiniciando a cena...");

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void EnableRespawnButton()
    {
        if (respawnButton == null)
            return;

        respawnButton.SetEnabled(true);
        isRequestingRespawn = false;
    }

    private void OnDestroy()
    {
        if (respawnButton != null)
            respawnButton.clicked -= RequestRespawn;
    }
}
