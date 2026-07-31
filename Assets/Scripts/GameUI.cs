using System.Collections.Generic;
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
    private TextMeshProUGUI currentColorLabel;
    private TextMeshProUGUI transparencyButtonLabel;
    private bool areBlocksTransparent;
    private readonly Dictionary<SpriteRenderer, float> originalBlockAlphas = new Dictionary<SpriteRenderer, float>();

    /// <summary>
    /// 입력: manager. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    public void Initialize(GameManager manager)
    {
        gameManager = manager;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        currentColorLabel = CreateLabel(transform, "Current Color: Black", new Vector2(0f, 1f), new Vector2(112f, -24f), 24f);
        CreateRestartButton(transform, "Restart", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f));
        CreateTransparencyButton(transform);

        gameOverPanel = CreatePanel(transform);
        gameOverPanel.SetActive(false);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 매 프레임 입력과 화면 또는 게임 상태를 갱신합니다.
    /// </summary>
    private void Update()
    {
        if (currentColorLabel != null)
            currentColorLabel.text = $"Current Color: {GameManager.currentColor}";

        if (areBlocksTransparent)
            ApplyBlockTransparency();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 지정한 UI 또는 정보를 화면에 표시합니다.
    /// </summary>
    public void ShowGameOver()
    {
        gameOverPanel.SetActive(true);
    }

    /// <summary>
    /// 입력: parent. 출력: GameObject. 역할: 런타임 UI 또는 오브젝트를 생성합니다.
    /// </summary>
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

    /// <summary>
    /// 입력: parent, label, anchor, pivot, position. 출력: 없음. 역할: 런타임 UI 또는 오브젝트를 생성합니다.
    /// </summary>
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

    /// <summary>
    /// 입력: parent. 출력: 없음. 역할: 런타임 UI 또는 오브젝트를 생성합니다.
    /// </summary>
    private void CreateTransparencyButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("TransparencyButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
        buttonTransform.anchorMin = new Vector2(1f, 1f);
        buttonTransform.anchorMax = new Vector2(1f, 1f);
        buttonTransform.pivot = new Vector2(1f, 1f);
        buttonTransform.anchoredPosition = new Vector2(-24f, -76f);
        buttonTransform.sizeDelta = new Vector2(172f, 44f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.16f, 0.16f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(ToggleBlockTransparency);

        transparencyButtonLabel = CreateLabel(buttonObject.transform, "Block Alpha: Normal", new Vector2(0.5f, 0.5f), Vector2.zero, 20f);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private void ToggleBlockTransparency()
    {
        areBlocksTransparent = !areBlocksTransparent;
        ApplyBlockTransparency();
        transparencyButtonLabel.text = areBlocksTransparent ? "Block Alpha: Transparent" : "Block Alpha: Normal";
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private void ApplyBlockTransparency()
    {
        foreach (Block block in FindObjectsOfType<Block>(true))
            ApplyBlockTransparency(block.gameObject);

        foreach (BlockMerged mergedBlock in FindObjectsOfType<BlockMerged>(true))
            ApplyBlockTransparency(mergedBlock.gameObject);
    }

    /// <summary>
    /// 입력: blockObject. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private void ApplyBlockTransparency(GameObject blockObject)
    {
        foreach (SpriteRenderer spriteRenderer in blockObject.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!originalBlockAlphas.ContainsKey(spriteRenderer))
                originalBlockAlphas[spriteRenderer] = spriteRenderer.color.a;

            Color color = spriteRenderer.color;
            color.a = areBlocksTransparent ? originalBlockAlphas[spriteRenderer] * 0.35f : originalBlockAlphas[spriteRenderer];
            spriteRenderer.color = color;
        }
    }

    /// <summary>
    /// 입력: parent, text, anchor, position, fontSize. 출력: TextMeshProUGUI. 역할: 런타임 UI 또는 오브젝트를 생성합니다.
    /// </summary>
    private TextMeshProUGUI CreateLabel(Transform parent, string text, Vector2 anchor, Vector2 position, float fontSize)
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
        return label;
    }
}
