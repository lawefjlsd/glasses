using UnityEngine.SceneManagement;
using UnityEngine;

/// <summary>  CC & Stage manage </summary>
public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }
    public static string currentColor { get; private set; }

    private bool isGameOver;
    public bool IsGameOver => isGameOver;
    
    private void Awake()
    {
        if (Instance == null){ Instance = this; }
        else{ Destroy(gameObject); }
    }

    private void Start()
    {
        currentColor = "Black";

        Debug.Log($"[GameManager] 초기 색상 설정: {currentColor}");
    }

    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsOverlayOpen) return;
        if (Input.GetKeyDown(KeyCode.R)) RestartCurrentStage();
    }

    public void RestartCurrentStage()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;
        GameHUDController.Instance?.ShowGameOver();
        Debug.Log("[GameManager] Game Over: ColorBlock overlap detected.");
    }

    public void TriggerGameOverFromTurn() => TriggerGameOver();
    public void TriggerStageClearFromTurn() { GameHUDController.Instance?.ShowClear(); }

    /// <summary> Undo로 Game Over 이전 Snapshot이 복원되면 Game Over 상태를 해제 </summary>
    public void ClearGameOverFromUndo()
    {
        if (!isGameOver) return;

        isGameOver = false;
        GameHUDController.Instance?.HideGameOver();
    }

    public void RestoreColor(string colorToRestore)
    {
        currentColor = colorToRestore;
        Debug.Log($"[GameManager] 색상 복원: {currentColor}");
    }
}
