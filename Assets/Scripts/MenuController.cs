
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    [Header("Configuração")]
    [SerializeField] private string gameSceneName = "Game";

    private UIDocument uiDocument;
    private Button playButton;
    private Button settingsButton;
    private Button quitButton;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            uiDocument = FindFirstObjectByType<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError(
                "[MainMenuController] Nenhum UIDocument encontrado. " +
                "Adicione um UIDocument ao objeto do menu."
            );
            enabled = false;
            return;
        }

        if (uiDocument.rootVisualElement == null)
        {
            Debug.LogError(
                "[MainMenuController] O Root Visual Element não foi encontrado."
            );
            enabled = false;
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;

        playButton = root.Q<Button>("PlayButton");
        settingsButton = root.Q<Button>("SettingsButton");
        quitButton = root.Q<Button>("QuitButton");

        if (playButton == null)
        {
            Debug.LogError(
                "[MainMenuController] Botão PlayButton não encontrado no UXML."
            );
        }
        else
        {
            playButton.clicked += PlayGame;
        }

        if (settingsButton == null)
        {
            Debug.LogError(
                "[MainMenuController] Botão SettingsButton não encontrado no UXML."
            );
        }
        else
        {
            settingsButton.clicked += OpenSettings;
        }

        if (quitButton == null)
        {
            Debug.LogError(
                "[MainMenuController] Botão QuitButton não encontrado no UXML."
            );
        }
        else
        {
            quitButton.clicked += QuitGame;
        }
    }

    private void OnDestroy()
    {
        if (playButton != null)
        {
            playButton.clicked -= PlayGame;
        }

        if (settingsButton != null)
        {
            settingsButton.clicked -= OpenSettings;
        }

        if (quitButton != null)
        {
            quitButton.clicked -= QuitGame;
        }
    }

    private void PlayGame()
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError(
                "[MainMenuController] O nome da cena do jogo está vazio."
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError(
                "[MainMenuController] A cena '" + gameSceneName +
                "' não foi encontrada. Confira o nome e adicione-a em " +
                "File > Build Profiles > Scene List."
            );
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    private void OpenSettings()
    {
        Debug.Log("[MainMenuController] Configurações selecionadas.");

        // A tela de configurações pode ser implementada aqui.
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
