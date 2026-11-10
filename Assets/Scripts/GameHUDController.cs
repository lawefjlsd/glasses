using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Stage GameCanvas의 HUD, Clear, Game Over를 직렬화된 Panel로 표시</summary>
public class GameHUDController : MonoBehaviour
{
    public static GameHUDController Instance { get; private set; }
    [SerializeField] private TextMeshProUGUI currentColorLabel;
    [SerializeField] private TextMeshProUGUI remainingTurnsLabel;
    [SerializeField] private HierarchyClearUIController clearPanel;
    [SerializeField] private GameOverUIController gameOverPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button transparencyButton;
    private bool areBlocksTransparent;
    private readonly Dictionary<SpriteRenderer, float> originalBlockAlphas = new Dictionary<SpriteRenderer, float>();
    private void Awake() 
    { 
        Instance = this;
        restartButton.onClick.AddListener(Restart);
        undoButton.onClick.AddListener(Undo);
        transparencyButton.onClick.AddListener(ToggleTransparency);
        clearPanel.Hide();
        gameOverPanel.Hide(); 
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Update() 
    {
        if (currentColorLabel != null) currentColorLabel.text = $"Current Color: {GameManager.currentColor}";
        if (remainingTurnsLabel != null) remainingTurnsLabel.text = "Remaining Turns: see merged blocks";
    }

    public void ShowClear() { if (!clearPanel.gameObject.activeSelf) StartCoroutine(ClearRoutine()); }
    public void ShowGameOver() => gameOverPanel.Show();
    public void HideGameOver() => gameOverPanel.Hide();

    private IEnumerator ClearRoutine() 
    {
        clearPanel.Show();
        yield return new WaitForSeconds(3f);
        if (SceneFlowManager.Instance == null || !SceneFlowManager.Instance.HandleStageClear())
            Debug.LogError("[GameHUDController] Stage clear could not be handled: current Scene is not registered in ProgressCatalog."); 
    }

    private void Restart() => GameManager.Instance?.RestartCurrentStage();
    private void Undo() => UndoManager.Instance?.Undo();
    private void ToggleTransparency()
    {
        areBlocksTransparent = !areBlocksTransparent;
        foreach (Block block in FindObjectsOfType<Block>(true)) ApplyTransparency(block.gameObject);
        foreach (BlockMerged merged in FindObjectsOfType<BlockMerged>(true)) ApplyTransparency(merged.gameObject);
    }
    private void ApplyTransparency(GameObject blockObject)
    {
        foreach (SpriteRenderer renderer in blockObject.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!originalBlockAlphas.ContainsKey(renderer)) originalBlockAlphas[renderer] = renderer.color.a;
            Color color = renderer.color;
            color.a = areBlocksTransparent ? originalBlockAlphas[renderer] * .35f : originalBlockAlphas[renderer];
            renderer.color = color;
        }
    }
}
