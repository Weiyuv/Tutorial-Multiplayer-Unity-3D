
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class MainMenuController : MonoBehaviour
{
    [Header("Configuração")]
    [SerializeField] private string gameSceneName = "Game";

    private UIDocument uiDocument;
    private VisualElement root;

    private Button playButton;
    private Button settingsButton;
    private Button quitButton;

    private VisualElement menuButtons;
    private Label title;
    private VisualElement settingsPanel;
    private Button backButton;

    private Slider volumeSlider;
    private Label volumeValue;

    private Button forwardBinding;
    private Button backwardBinding;
    private Button leftBinding;
    private Button rightBinding;
    private Button dashBinding;

    private string waitingForBinding;
    private Button waitingButton;

    private readonly string[] bindingNames =
    {
        "MoveForward",
        "MoveBackward",
        "MoveLeft",
        "MoveRight",
        "Dash"
    };

    private readonly Key[] defaultKeys =
    {
        Key.W,
        Key.S,
        Key.A,
        Key.D,
        Key.LeftShift
    };

    private const string VolumeKey = "Mecha_MasterVolume";

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
            uiDocument = FindFirstObjectByType<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[MainMenuController] Nenhum UIDocument encontrado.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError("[MainMenuController] Root Visual Element não encontrado.");
            enabled = false;
            return;
        }

        playButton = root.Q<Button>("PlayButton");
        settingsButton = root.Q<Button>("SettingsButton");
        quitButton = root.Q<Button>("QuitButton");

        title = root.Q<Label>("Title");
        menuButtons = root.Q<VisualElement>("MenuButtons");
        settingsPanel = root.Q<VisualElement>("SettingsPanel");
        backButton = root.Q<Button>("BackButton");

        volumeSlider = root.Q<Slider>("VolumeSlider");
        volumeValue = root.Q<Label>("VolumeValue");

        forwardBinding = root.Q<Button>("ForwardBinding");
        backwardBinding = root.Q<Button>("BackwardBinding");
        leftBinding = root.Q<Button>("LeftBinding");
        rightBinding = root.Q<Button>("RightBinding");
        dashBinding = root.Q<Button>("DashBinding");

        if (playButton != null)
            playButton.clicked += PlayGame;
        else
            Debug.LogError("[MainMenuController] PlayButton não encontrado.");

        if (settingsButton != null)
            settingsButton.clicked += OpenSettings;
        else
            Debug.LogError("[MainMenuController] SettingsButton não encontrado.");

        if (quitButton != null)
            quitButton.clicked += QuitGame;
        else
            Debug.LogError("[MainMenuController] QuitButton não encontrado.");

        if (settingsPanel != null)
            settingsPanel.style.display = DisplayStyle.None;

        if (menuButtons != null)
            menuButtons.style.display = DisplayStyle.Flex;

        if (backButton != null)
            backButton.clicked += CloseSettings;

        if (volumeSlider != null)
        {
            volumeSlider.lowValue = 0f;
            volumeSlider.highValue = 1f;
            volumeSlider.value = PlayerPrefs.GetFloat(VolumeKey, 1f);
            volumeSlider.RegisterValueChangedCallback(OnVolumeChanged);
            ApplyVolume(volumeSlider.value);
        }
        else
        {
            Debug.LogError("[MainMenuController] VolumeSlider não encontrado.");
        }

        RegisterBindingButton(forwardBinding, "MoveForward");
        RegisterBindingButton(backwardBinding, "MoveBackward");
        RegisterBindingButton(leftBinding, "MoveLeft");
        RegisterBindingButton(rightBinding, "MoveRight");
        RegisterBindingButton(dashBinding, "Dash");

        LoadBindings();
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(waitingForBinding))
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelBinding();
            return;
        }

        foreach (KeyControl keyControl in Keyboard.current.allKeys)
        {
            if (!keyControl.wasPressedThisFrame)
                continue;

            Key newKey = keyControl.keyCode;

            if (newKey == Key.None)
                continue;

            if (IsKeyUsedByAnotherBinding(newKey, waitingForBinding))
            {
                if (waitingButton != null)
                    waitingButton.text = "JÁ UTILIZADA";

                return;
            }

            PlayerPrefs.SetInt(GetPreferenceKey(waitingForBinding), (int)newKey);
            PlayerPrefs.Save();

            if (waitingButton != null)
                waitingButton.text = FormatKey(newKey);

            waitingForBinding = null;
            waitingButton = null;
            return;
        }
    }

    private void PlayGame()
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError("[MainMenuController] Nome da cena vazio.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError(
                "[MainMenuController] Cena '" + gameSceneName +
                "' não encontrada. Confira File > Build Profiles > Scene List."
            );
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    private void OpenSettings()
    {
        if (settingsPanel == null || menuButtons == null)
        {
            Debug.LogError(
                "[MainMenuController] SettingsPanel ou MenuButtons não encontrado no UXML."
            );
            return;
        }

        CancelBinding();

        if (title != null)
            title.style.display = DisplayStyle.None;

        menuButtons.style.display = DisplayStyle.None;
        settingsPanel.style.display = DisplayStyle.Flex;

        LoadBindings();
    }

    private void CloseSettings()
    {
        CancelBinding();

        if (settingsPanel != null)
            settingsPanel.style.display = DisplayStyle.None;

        if (menuButtons != null)
            menuButtons.style.display = DisplayStyle.Flex;

        if (title != null)
            title.style.display = DisplayStyle.Flex;
    }

    private void OnVolumeChanged(ChangeEvent<float> evt)
    {
        ApplyVolume(evt.newValue);
    }

    private void ApplyVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        AudioListener.volume = volume;

        PlayerPrefs.SetFloat(VolumeKey, volume);
        PlayerPrefs.Save();

        if (volumeValue != null)
            volumeValue.text = Mathf.RoundToInt(volume * 100f) + "%";
    }

    private void RegisterBindingButton(Button button, string bindingName)
    {
        if (button != null)
            button.clicked += () => StartBinding(button, bindingName);
    }

    private void StartBinding(Button button, string bindingName)
    {
        waitingForBinding = bindingName;
        waitingButton = button;
        waitingButton.text = "PRESSIONE...";
    }

    private void CancelBinding()
    {
        if (waitingButton != null && !string.IsNullOrEmpty(waitingForBinding))
            waitingButton.text = FormatKey(GetSavedKey(waitingForBinding));

        waitingForBinding = null;
        waitingButton = null;
    }

    private void LoadBindings()
    {
        SetBindingText(forwardBinding, "MoveForward");
        SetBindingText(backwardBinding, "MoveBackward");
        SetBindingText(leftBinding, "MoveLeft");
        SetBindingText(rightBinding, "MoveRight");
        SetBindingText(dashBinding, "Dash");
    }

    private void SetBindingText(Button button, string bindingName)
    {
        if (button != null)
            button.text = FormatKey(GetSavedKey(bindingName));
    }

    private Key GetSavedKey(string bindingName)
    {
        int index = Array.IndexOf(bindingNames, bindingName);

        if (index < 0)
            return Key.None;

        int savedValue = PlayerPrefs.GetInt(
            GetPreferenceKey(bindingName),
            (int)defaultKeys[index]
        );

        if (!Enum.IsDefined(typeof(Key), savedValue))
            return defaultKeys[index];

        return (Key)savedValue;
    }

    private bool IsKeyUsedByAnotherBinding(Key key, string currentBinding)
    {
        foreach (string bindingName in bindingNames)
        {
            if (bindingName == currentBinding)
                continue;

            if (GetSavedKey(bindingName) == key)
                return true;
        }

        return false;
    }

    private string GetPreferenceKey(string bindingName)
    {
        return "Mecha_Key_" + bindingName;
    }

    private string FormatKey(Key key)
    {
        if (key == Key.LeftShift || key == Key.RightShift)
            return "SHIFT";

        return key.ToString().ToUpperInvariant();
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (playButton != null)
            playButton.clicked -= PlayGame;

        if (settingsButton != null)
            settingsButton.clicked -= OpenSettings;

        if (quitButton != null)
            quitButton.clicked -= QuitGame;

        if (backButton != null)
            backButton.clicked -= CloseSettings;

        if (volumeSlider != null)
            volumeSlider.UnregisterValueChangedCallback(OnVolumeChanged);
    }
}
