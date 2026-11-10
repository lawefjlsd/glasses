using System.Collections.Generic;
using UnityEngine;

/// <summary> Succeed Commit의 입력 전 StageBoardSnapshot만 보관하는 Undo Storage </summary>
public class UndoManager : MonoBehaviour
{
    public static UndoManager Instance { get; private set; }

    private readonly Stack<StageBoardSnapshot> committedHistory = new Stack<StageBoardSnapshot>();
    private StageBoardSnapshot pendingSnapshot;
    private StageTurnController stageTurnController;
    private bool isUndoRunning;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    private void Start() { stageTurnController = FindObjectOfType<StageTurnController>(); }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary> Commit 직전 이전 Input Snapshot 보관</summary>
    public bool BeginTurnSnapshot(StageBoardSnapshot snapshot)
    {
        if (snapshot == null || pendingSnapshot != null) return false;
        pendingSnapshot = snapshot.Clone();
        return true;
    }

    /// <summary> Succeed Commit의 임시 Snapshot을 Undo history에 확정</summary>
    public bool ConfirmTurnSnapshot()
    {
        if (pendingSnapshot == null) return false;
        committedHistory.Push(pendingSnapshot);
        pendingSnapshot = null;
        return true;
    }

    /// <summary> Failed Commit의 임시 Snapshot을 폐기</summary>
    public void DiscardPendingSnapshot(){ pendingSnapshot = null; }

    /// <summary> 가장 최근 Succeed turn의 입력 전 Snapshot이 undo 후보</summary>
    public bool TryBeginUndoSnapshot(out StageBoardSnapshot snapshot)
    {
        snapshot = null;
        if (isUndoRunning || pendingSnapshot != null || committedHistory.Count == 0) return false;
        snapshot = committedHistory.Peek().Clone();
        return true;
    }

    /// <summary> Succeed Undo 이후 history POP.</summary>
    public bool ConfirmUndoSnapshot()
    {
        if (committedHistory.Count == 0) return false;
        committedHistory.Pop();
        return true;
    }

    public void SetUndoRunning(bool running){ isUndoRunning = running; }

    /// HUD의 Undo 요청을 Single StageTurnController로 전달</summary>
    public void Undo()
    {
        if (stageTurnController == null) stageTurnController = FindObjectOfType<StageTurnController>();
        if (stageTurnController == null || !stageTurnController.RequestUndo())
            Debug.LogWarning("[UndoManager] Undo request was rejected by StageTurnController.");
    }

    public void ClearStack()
    {
        pendingSnapshot = null;
        committedHistory.Clear();
    }
}
