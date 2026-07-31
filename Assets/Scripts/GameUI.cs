using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재시작 버튼과 Game Over 화면을 표시합니다.
/// </summary>
public class GameUI : MonoBehaviour
{
    private GameManager gameManager;
    private GameObject gameOverPanel;

    public void Initialize(GameManager manager)
    {
        gameManager = manager;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        CreateRestartButton(transform, "Restart", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f));

        gameOverPanel = CreatePanel(transform);
        gameOverPanel.SetActive(false);
    }

    public void ShowGameOver()
    {
        gameOverPanel.SetActive(true);
    }

    private GameObject CreatePanel(Transform parent)
    {
        GameObject panel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform panelTransform = panel.GetComponent<RectTransform>();
        panelTransform.anchorMin = Vector2.zero;
        panelTransform.anchorMax = Vector2.one;
        panelTransform.offsetMin = Vector2.zero;
        panelTransform.offsetMax = Vector2.zero;

        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        CreateLabel(panel.transform, "Game Over", new Vector2(0.5f, 0.5f), new Vector2(0f, 48f), 48f);
        CreateRestartButton(panel.transform, "Restart", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f));

        return panel;
    }

    private void CreateRestartButton(Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 position)
    {
        GameObject buttonObject = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
        buttonTransform.anchorMin = anchor;
        buttonTransform.anchorMax = anchor;
        buttonTransform.pivot = pivot;
        buttonTransform.anchoredPosition = position;
        buttonTransform.sizeDelta = new Vector2(132f, 44f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.16f, 0.16f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(gameManager.RestartCurrentStage);

        CreateLabel(buttonObject.transform, label, new Vector2(0.5f, 0.5f), Vector2.zero, 24f);
    }

    private void CreateLabel(Transform parent, string text, Vector2 anchor, Vector2 position, float fontSize)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
        labelTransform.anchorMin = anchor;
        labelTransform.anchorMax = anchor;
        labelTransform.pivot = new Vector2(0.5f, 0.5f);
        labelTransform.anchoredPosition = position;
        labelTransform.sizeDelta = new Vector2(300f, 64f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
    }
}
