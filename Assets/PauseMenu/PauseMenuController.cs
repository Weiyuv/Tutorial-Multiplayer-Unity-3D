
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class PauseMenuController : MonoBehaviour
{
    [Header("Interface")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Controles locais")]
    [SerializeField] private Behaviour[] localControlsToDisable;

    [Header("Cena")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private VisualElement root;

    private Button resumeButton;
    private Button mainMenuButton;
    private Button quitButton;

    private bool[] previousControlStates;

    private bool returningToMenu;

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[PauseMenu] UIDocument não encontrado.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        resumeButton = root.Q<Button>("ResumeButton");
        mainMenuButton = root.Q<Button>("MainMenuButton");
        quitButton = root.Q<Button>("QuitButton");

        if (resumeButton == null || mainMenuButton == null || quitButton == null)
        {
            Debug.LogError("[PauseMenu] Um ou mais botões não foram encontrados no UXML.");
            enabled = false;
            return;
        }

        resumeButton.clicked += ResumeGame;
        mainMenuButton.clicked += ReturnToMainMenu;
        quitButton.clicked += QuitGame;

        root.style.display = DisplayStyle.None;

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    private void Update()
    {
        if (returningToMenu)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    private void PauseGame()
    {
        if (IsPaused || returningToMenu)
            return;

        IsPaused = true;

        root.style.display = DisplayStyle.Flex;

        SetLocalControlsEnabled(false);

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    private void ResumeGame()
    {
        if (!IsPaused || returningToMenu)
            return;

        IsPaused = false;

        root.style.display = DisplayStyle.None;

        SetLocalControlsEnabled(true);

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    private void SetLocalControlsEnabled(bool enabledState)
    {
        if (localControlsToDisable == null)
            return;

        if (!enabledState)
        {
            previousControlStates = new bool[localControlsToDisable.Length];

            for (int i = 0; i < localControlsToDisable.Length; i++)
            {
                Behaviour control = localControlsToDisable[i];

                if (control == null)
                    continue;

                previousControlStates[i] = control.enabled;
                control.enabled = false;
            }
        }
        else
        {
            for (int i = 0; i < localControlsToDisable.Length; i++)
            {
                Behaviour control = localControlsToDisable[i];

                if (control == null)
                    continue;

                if (previousControlStates != null &&
                    i < previousControlStates.Length)
                {
                    control.enabled = previousControlStates[i];
                }
            }

            previousControlStates = null;
        }
    }

    private void ReturnToMainMenu()
    {
        if (returningToMenu)
            return;

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError("[PauseMenu] Cena não encontrada na Build Scene List: " + mainMenuSceneName);
            return;
        }

        returningToMenu = true;

        root.style.display = DisplayStyle.None;

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        StartCoroutine(DisconnectAndLoadMenu());
    }

    private IEnumerator DisconnectAndLoadMenu()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager != null && manager.IsListening)
        {
            manager.Shutdown();

            while (manager != null && manager.ShutdownInProgress)
            {
                yield return null;
            }
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void QuitGame()
    {
        if (returningToMenu)
            return;

        returningToMenu = true;

        NetworkManager manager = NetworkManager.Singleton;

        if (manager != null && manager.IsListening)
            manager.Shutdown();

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (resumeButton != null)
            resumeButton.clicked -= ResumeGame;

        if (mainMenuButton != null)
            mainMenuButton.clicked -= ReturnToMainMenu;

        if (quitButton != null)
            quitButton.clicked -= QuitGame;
    }
}
