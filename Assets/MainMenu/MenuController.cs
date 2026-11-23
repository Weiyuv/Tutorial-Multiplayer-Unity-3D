
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
    private Button backButton;

    private VisualElement menuButtons;
    private VisualElement settingsPanel;
    private Label title;

    private Slider volumeSlider;
    private Label volumeValue;

    private Button forwardBinding;
    private Button backwardBinding;
    private Button leftBinding;
    private Button rightBinding;
    private Button dashBinding;
    private Button attackBinding;
    private Button pickupBinding;

    private string waitingForBinding;
    private Button waitingButton;

    private readonly string[] bindingNames =
    {
        "MoveForward",
        "MoveBackward",
        "MoveLeft",
        "MoveRight",
        "Dash",
        "Attack",
        "Pickup"
    };

    private readonly Key[] defaultKeys =
    {
        Key.W,
        Key.S,
        Key.A,
        Key.D,
        Key.LeftShift,
        Key.None,
        Key.E
    };

    private const string VolumeKey = "Mecha_MasterVolume";
    private const string MouseLeftBinding = "MOUSE 1";

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
        attackBinding = root.Q<Button>("AttackBinding");
        pickupBinding = root.Q<Button>("PickupBinding");

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
        RegisterBindingButton(attackBinding, "Attack");
        RegisterBindingButton(pickupBinding, "Pickup");

        LoadBindings();
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(waitingForBinding))
            return;

        if (Keyboard.current != null)
        {
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

                SaveBinding(waitingForBinding, (int)newKey);
                return;
            }
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                SaveBinding(waitingForBinding, -1);
                return;
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                SaveBinding(waitingForBinding, -2);
                return;
            }

            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                SaveBinding(waitingForBinding, -3);
                return;
            }
        }
    }

    private void SaveBinding(string bindingName, int bindingValue)
    {
        if (IsBindingUsedByAnother(bindingValue, bindingName))
        {
            if (waitingButton != null)
                waitingButton.text = "JÁ UTILIZADA";

            return;
        }

        PlayerPrefs.SetInt(GetPreferenceKey(bindingName), bindingValue);
        PlayerPrefs.Save();

        if (waitingButton != null)
            waitingButton.text = FormatBinding(bindingValue);

        waitingForBinding = null;
        waitingButton = null;
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
            Debug.LogError("[MainMenuController] Cena '" + gameSceneName + "' não encontrada.");
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    private void OpenSettings()
    {
        if (settingsPanel == null || menuButtons == null)
            return;

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
        else
            Debug.LogError("[MainMenuController] Botão não encontrado: " + bindingName);
    }

    private void StartBinding(Button button, string bindingName)
    {
        CancelBinding();

        waitingForBinding = bindingName;
        waitingButton = button;
        waitingButton.text = "PRESSIONE...";
    }

    private void CancelBinding()
    {
        if (waitingButton != null && !string.IsNullOrEmpty(waitingForBinding))
            waitingButton.text = FormatBinding(GetSavedBinding(waitingForBinding));

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
        SetBindingText(attackBinding, "Attack");
        SetBindingText(pickupBinding, "Pickup");
    }

    private void SetBindingText(Button button, string bindingName)
    {
        if (button != null)
            button.text = FormatBinding(GetSavedBinding(bindingName));
    }

    private int GetSavedBinding(string bindingName)
    {
        int index = Array.IndexOf(bindingNames, bindingName);

        if (index < 0)
            return 0;

        int defaultValue = (int)defaultKeys[index];

        if (bindingName == "Attack")
            defaultValue = -1;

        return PlayerPrefs.GetInt(GetPreferenceKey(bindingName), defaultValue);
    }

    private bool IsBindingUsedByAnother(int value, string currentBinding)
    {
        foreach (string bindingName in bindingNames)
        {
            if (bindingName == currentBinding)
                continue;

            if (GetSavedBinding(bindingName) == value)
                return true;
        }

        return false;
    }

    private bool IsKeyUsedByAnotherBinding(Key key, string currentBinding)
    {
        return IsBindingUsedByAnother((int)key, currentBinding);
    }

    private string GetPreferenceKey(string bindingName)
    {
        return "Mecha_Key_" + bindingName;
    }

    private string FormatBinding(int value)
    {
        switch (value)
        {
            case -1: return "MOUSE 1";
            case -2: return "MOUSE 2";
            case -3: return "MOUSE 3";
            default:
                if (!Enum.IsDefined(typeof(Key), value))
                    return "DESCONHECIDO";

                Key key = (Key)value;

                if (key == Key.LeftShift || key == Key.RightShift)
                    return "SHIFT";

                return key.ToString().ToUpperInvariant();
        }
    }

    public static int GetBindingValue(string bindingName, Key defaultKey)
    {
        int defaultValue = (int)defaultKey;

        if (bindingName == "Attack")
            defaultValue = -1;

        return PlayerPrefs.GetInt("Mecha_Key_" + bindingName, defaultValue);
    }

    public static bool WasBindingPressedThisFrame(string bindingName, Key defaultKey)
    {
        int value = GetBindingValue(bindingName, defaultKey);

        if (value == -1)
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (value == -2)
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        if (value == -3)
            return Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;

        if (Keyboard.current == null)
            return false;

        Key key = (Key)value;

        return key != Key.None && Keyboard.current[key].wasPressedThisFrame;
    }

    public static bool IsBindingPressed(string bindingName, Key defaultKey)
    {
        int value = GetBindingValue(bindingName, defaultKey);

        if (value == -1)
            return Mouse.current != null && Mouse.current.leftButton.isPressed;

        if (value == -2)
            return Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (value == -3)
            return Mouse.current != null && Mouse.current.middleButton.isPressed;

        if (Keyboard.current == null)
            return false;

        Key key = (Key)value;

        return key != Key.None && Keyboard.current[key].isPressed;
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
