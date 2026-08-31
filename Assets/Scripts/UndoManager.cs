using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성공 Commit의 입력 전 StageBoardSnapshot만 보관하는 Undo 저장소입니다.
/// </summary>
public class UndoManager : MonoBehaviour
{
    public static UndoManager Instance { get; private set; }

    [Header("Undo Speed Settings")]
    public float undoSpeed = 8f;

    private readonly Stack<StageBoardSnapshot> committedHistory = new Stack<StageBoardSnapshot>();
    private StageBoardSnapshot pendingSnapshot;
    private StageTurnController stageTurnController;
    private bool isUndoRunning;

    /// <summary>입력: 없음. 출력: bool. 역할: Controller가 Undo Commit을 처리 중인지 반환합니다.</summary>
    public bool IsUndoRunning => isUndoRunning;

    /// <summary>입력: 없음. 출력: 없음. 역할: Scene의 단일 UndoManager 참조를 준비합니다.</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: UI 요청을 Controller로 전달할 참조를 한 번만 준비합니다.</summary>
    private void Start()
    {
        stageTurnController = FindObjectOfType<StageTurnController>();
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 현재 singleton을 해제합니다.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>입력: snapshot. 출력: bool. 역할: Commit 직전 입력 전 Snapshot을 임시 보관합니다.</summary>
    public bool BeginTurnSnapshot(StageBoardSnapshot snapshot)
    {
        if (snapshot == null || pendingSnapshot != null) return false;
        pendingSnapshot = snapshot.Clone();
        return true;
    }

    /// <summary>입력: 없음. 출력: bool. 역할: 성공 Commit의 임시 Snapshot을 Undo history에 확정합니다.</summary>
    public bool ConfirmTurnSnapshot()
    {
        if (pendingSnapshot == null) return false;
        committedHistory.Push(pendingSnapshot);
        pendingSnapshot = null;
        return true;
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 실패한 Commit의 임시 Snapshot을 폐기합니다.</summary>
    public void DiscardPendingSnapshot()
    {
        pendingSnapshot = null;
    }

    /// <summary>입력: out snapshot. 출력: bool. 역할: 가장 최근 성공 턴의 입력 전 Snapshot을 복원 후보로 읽습니다.</summary>
    public bool TryBeginUndoSnapshot(out StageBoardSnapshot snapshot)
    {
        snapshot = null;
        if (isUndoRunning || pendingSnapshot != null || committedHistory.Count == 0) return false;
        snapshot = committedHistory.Peek().Clone();
        return true;
    }

    /// <summary>입력: 없음. 출력: bool. 역할: 성공한 Undo 복원 뒤에만 history 최상단을 제거합니다.</summary>
    public bool ConfirmUndoSnapshot()
    {
        if (committedHistory.Count == 0) return false;
        committedHistory.Pop();
        return true;
    }

    /// <summary>입력: running. 출력: 없음. 역할: Controller가 Undo 실행 상태를 표시합니다.</summary>
    public void SetUndoRunning(bool running)
    {
        isUndoRunning = running;
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: HUD의 Undo 요청을 유일한 StageTurnController로 전달합니다.</summary>
    public void Undo()
    {
        if (stageTurnController == null) stageTurnController = FindObjectOfType<StageTurnController>();
        if (stageTurnController == null || !stageTurnController.RequestUndo())
            Debug.LogWarning("[UndoManager] Undo request was rejected by StageTurnController.");
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 저장된 Stage history를 초기화합니다.</summary>
    public void ClearStack()
    {
        pendingSnapshot = null;
        committedHistory.Clear();
    }
}
