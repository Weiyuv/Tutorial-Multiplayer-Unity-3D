
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class RespawnScreen : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement overlay;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();

        VisualElement root = uiDocument.rootVisualElement;

        overlay = new VisualElement();
        overlay.name = "RespawnOverlay";

        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;

        overlay.style.backgroundColor =
            new Color(0.02f, 0.02f, 0.04f, 0.92f);

        overlay.style.justifyContent = Justify.Center;
        overlay.style.alignItems = Align.Center;
        overlay.style.display = DisplayStyle.None;

        Label title = new Label("VOCÊ FOI DERROTADO");

        title.style.color = new Color(1f, 0.2f, 0.2f);
        title.style.fontSize = 36;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;

        Label subtitle = new Label("AGUARDANDO RESPAWN...");

        subtitle.style.color = Color.white;
        subtitle.style.fontSize = 18;
        subtitle.style.marginTop = 16;
        subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;

        overlay.Add(title);
        overlay.Add(subtitle);

        root.Add(overlay);
    }

    public void Show()
    {
        if (overlay != null)
            overlay.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (overlay != null)
            overlay.style.display = DisplayStyle.None;
    }

    private void OnDestroy()
    {
        if (overlay != null)
            overlay.RemoveFromHierarchy();
    }
}
