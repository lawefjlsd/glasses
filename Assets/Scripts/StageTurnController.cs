using System.Collections;
using UnityEngine;

/// <summary>Scene의 동일 Controller lifecycle에서 초기 Merge 검사를 한 번 함</summary>
public sealed class InitialMergeCheckGuard
{
    private bool started;
    public int Count { get; private set; }

    public bool TryBegin()
    {
        if (started) return false;
        started = true;
        Count++;
        return true;
    }
}

public class StageTurnController : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SeparationManager separationManager;
    [SerializeField] private UndoManager undoManager;
    [SerializeField] private MovementHelper movementHelper;
    [SerializeField] private StageRuntimeCommitter runtimeCommitter;

    private bool isReady;
    private long currentTurnId;
    private bool initialMergeCheckCompleted;
    private readonly InitialMergeCheckGuard initialMergeCheckGuard = new InitialMergeCheckGuard();

    public bool IsProcessingTurn { get; private set; }

    public bool IsRuntimeActive => enabled && isReady && initialMergeCheckCompleted;
    public int InitialMergeCheckCount => initialMergeCheckGuard.Count;

    /// <summary> script & component들이 있는 지 검사. </summary>
    private void Awake()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (runtimeCommitter == null) runtimeCommitter = GetComponent<StageRuntimeCommitter>();
        if (separationManager == null) separationManager = FindObjectOfType<SeparationManager>();
        if (undoManager == null) undoManager = FindObjectOfType<UndoManager>();
        if (movementHelper == null) movementHelper = FindObjectOfType<MovementHelper>();

        isReady = gameManager != null
            && separationManager != null
            && undoManager != null
            && movementHelper != null
            && runtimeCommitter != null;
        if (!isReady)
        {
            Debug.LogError("[StageTurnController] Required Stage references are missing; Stage input is disabled.");
            enabled = false;
        }
    }
    private IEnumerator Start()
    {
        yield return null;
        yield return StartCoroutine(RunInitialMergeCheckOnce());
    }

    /// <summary> 입력과 Undo/Turn 소비 없이 초기 Board를 한 번 정규화</summary>
    private IEnumerator RunInitialMergeCheckOnce()
    {
        if (!TryBeginInitialMergeCheck()) yield break;

        IsProcessingTurn = true;
        bool succeeded = false;
        try
        {
            StageBoardSnapshot snapshot = StageBoard.Capture(currentTurnId, GameManager.currentColor).CreateSnapshot();
            Debug.Log($"[SeparationDebug] Initial Merge check start count={InitialMergeCheckCount} turn={snapshot.turnId}");

            TurnResult result = StageTurnResolver.ResolveInitialMerges(snapshot);
            if (!result.isValid)
            {
                Debug.LogError("[StageTurnController] Initial Merge check failed: " + result.invalidReason);
                yield break;
            }

            StageRuntimeCommitResult commit = null;
            yield return StartCoroutine(runtimeCommitter.Commit(snapshot, result, movementHelper, value => commit = value));
            if (commit == null || !commit.isSuccess)
            {
                Debug.LogError("[StageTurnController] Initial Merge commit failed: " + (commit == null ? "unknown error" : commit.error));
                yield break;
            }

            currentTurnId = result.committedBoard.turnId;
            gameManager.RestoreColor(result.committedBoard.currentColor);
            if (result.isGameOverCandidate) gameManager.TriggerGameOverFromTurn();

            succeeded = true;
            Debug.Log($"[SeparationDebug] Initial Merge check complete count={InitialMergeCheckCount} candidates={result.mergeBatch.candidateCount} created={result.mergeBatch.mergePlans.Count} currentColor={result.committedBoard.currentColor}");
        }
        finally
        {
            initialMergeCheckCompleted = succeeded;
            IsProcessingTurn = false;
            if (!succeeded) enabled = false;
        }
    }

    private bool TryBeginInitialMergeCheck()
    {
        if (!initialMergeCheckGuard.TryBegin())
        {
            Debug.Log($"[SeparationDebug] Initial Merge check skipped count={InitialMergeCheckCount}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Turn을 시작해도 되는지 판단 후 Turn시작
    /// </summary>
    public bool RequestMove(Vector3 direction)
    {
        Debug.Log($"[SeparationDebug] Input currentTurn={currentTurnId} direction={direction} ready={IsRuntimeActive} processing={IsProcessingTurn}");
        if (!IsRuntimeActive)
        {
            Debug.LogWarning("[StageTurnController] Move request rejected because the controller is not ready.");
            return false;
        }
        if (IsProcessingTurn)
        {
            Debug.LogWarning("[StageTurnController] Move request rejected while another turn is processing.");
            return false;
        }
        if (!IsCardinal(direction))
        {
            Debug.LogWarning("[StageTurnController] Move request rejected: direction must be cardinal.");
            return false;
        }

        StartCoroutine(ExecuteTurn(new TurnInput { direction = direction }));
        return true;
    }

    /// <summary> 최근 성공 Commit의 Snapshot Undo를 새 Runtime 경로로 시작 </summary>
    public bool RequestUndo()
    {
        if (!IsRuntimeActive || IsProcessingTurn)
        {
            Debug.LogWarning("[StageTurnController] Undo request rejected while the controller is unavailable or busy.");
            return false;
        }

        StartCoroutine(ExecuteUndo());
        return true;
    }

    /// <summary>확정된 9개 Turn Phase를 순서대로 실행 ; snapshot</summary>
    private IEnumerator ExecuteTurn(TurnInput input)
    {
        IsProcessingTurn = true;
        try
        {
            StageBoardSnapshot snapshot = StageBoard.Capture(currentTurnId, GameManager.currentColor).CreateSnapshot();
            input.actorEntityId = FindActorEntityId(snapshot);
            LogSnapshotDebug(snapshot, input.actorEntityId);
            if (string.IsNullOrEmpty(input.actorEntityId))
            {
                Debug.LogError("[StageTurnController] Turn rejected: no SelfMovable Player entity was captured.");
                yield break;
            }

            long targetTurn = snapshot.turnId + 1;
            SeparationSchedule schedule = separationManager.CreateSchedule(snapshot, targetTurn);
            LogScheduleDebug(snapshot, schedule);
            PlayerActionPlan actionPlan = PlayerActionResolver.Resolve(snapshot, schedule, input);
            Debug.Log($"[SeparationDebug] PlayerAction snapshotTurn={snapshot.turnId} actor={input.actorEntityId} outcome={actionPlan.outcome} valid={actionPlan.isValid} failureReason={actionPlan.failureReason} message={actionPlan.failureMessage}");
            if (!actionPlan.isValid || actionPlan.outcome != PlayerActionOutcome.Move)
            {
                Debug.LogWarning("[StageTurnController] Turn rejected: " + actionPlan.failureReason + " - " + actionPlan.failureMessage);
                yield break;
            }

            Debug.Log($"[SeparationDebug] Resolver enter snapshotTurn={snapshot.turnId} planCount={(schedule == null ? -1 : schedule.plans.Count)}");
            TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, actionPlan);
            LogResolverResultDebug(snapshot, schedule, result);
            if (!result.isValid)
            {
                Debug.LogError("[StageTurnController] Turn resolver rejected the board: " + result.invalidReason);
                yield break;
            }

            if (!undoManager.BeginTurnSnapshot(snapshot))
            {
                Debug.LogError("[StageTurnController] Could not reserve the input Snapshot for Undo.");
                yield break;
            }

            StageRuntimeCommitResult commit = null;
            Debug.Log($"[SeparationDebug] Commit enter snapshotTurn={snapshot.turnId} committedTurn={result.committedBoard.turnId}");
            yield return StartCoroutine(runtimeCommitter.Commit(snapshot, result, movementHelper, value => commit = value));
            Debug.Log($"[SeparationDebug] Commit exit snapshotTurn={snapshot.turnId} success={commit != null && commit.isSuccess} error={(commit == null ? "null-result" : commit.error)}");
            if (commit == null || !commit.isSuccess)
            {
                undoManager.DiscardPendingSnapshot();
                Debug.LogError("[StageTurnController] Commit failed: " + (commit == null ? "unknown error" : commit.error));
                yield break;
            }

            if (!undoManager.ConfirmTurnSnapshot())
            {
                Debug.LogError("[StageTurnController] Commit succeeded but Undo history could not be finalized.");
                yield break;
            }

            currentTurnId = result.committedBoard.turnId;
            gameManager.RestoreColor(result.committedBoard.currentColor);
            if (result.isGameOverCandidate) gameManager.TriggerGameOverFromTurn();
            else if (result.isStageClearCandidate) gameManager.TriggerStageClearFromTurn();
        }
        finally
        {
            IsProcessingTurn = false;
        }
    }

    /// <summary> 성공 Commit 전 Snapshot을 RuntimeCommitter로 완전 복원 </summary>
    private IEnumerator ExecuteUndo()
    {
        IsProcessingTurn = true;
        try
        {
            if (!undoManager.TryBeginUndoSnapshot(out StageBoardSnapshot target))
            {
                Debug.LogWarning("[StageTurnController] Undo rejected: no committed Snapshot is available.");
                yield break;
            }

            undoManager.SetUndoRunning(true);
            StageBoardSnapshot current = StageBoard.Capture(currentTurnId, GameManager.currentColor).CreateSnapshot();
            StageRuntimeCommitResult restore = null;
            yield return StartCoroutine(runtimeCommitter.RestoreSnapshot(current, target, movementHelper, value => restore = value));
            if (restore == null || !restore.isSuccess)
            {
                Debug.LogError("[StageTurnController] Undo restore failed: " + (restore == null ? "unknown error" : restore.error));
                yield break;
            }

            if (!undoManager.ConfirmUndoSnapshot())
            {
                Debug.LogError("[StageTurnController] Undo restore succeeded but history finalization failed.");
                yield break;
            }

            currentTurnId = target.turnId;
            gameManager.RestoreColor(target.currentColor);
            gameManager.ClearGameOverFromUndo();
        }
        finally
        {
            undoManager.SetUndoRunning(false);
            IsProcessingTurn = false;
        }
    }
    /// <summary> input direction이 xy상의 단위벡터인지 판단 </summary>
    private static bool IsCardinal(Vector3 direction)
    {
        return Mathf.Approximately(direction.z, 0f)
            && Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1f;
    }

    /// <summary> Profile 기반 SelfMovable Solid Top actor를 안정적으로 선택 </summary>
    private static string FindActorEntityId(StageBoardSnapshot snapshot)
    {
        string actorId = null;
        foreach (StageEntityState entity in snapshot.entities.Values)
        {
            if (!entity.isActive
                || entity.profile.zone != StageZone.Top
                || entity.profile.movement != MovementCapability.SelfMovable
                || entity.profile.occupancy != OccupancyType.Solid)
                continue;

            if (actorId != null)
            {
                Debug.LogError("[StageTurnController] Snapshot has multiple SelfMovable Solid Top actors.");
                return null;
            }

            actorId = entity.entityId;
        }

        return actorId;
    }

    private static void LogSnapshotDebug(StageBoardSnapshot snapshot, string actorEntityId)
    {
        string actorCell = "missing";
        if (!string.IsNullOrEmpty(actorEntityId)
            && snapshot.entities.TryGetValue(actorEntityId, out StageEntityState actor))
            actorCell = actor.cell.ToString();

        Debug.Log($"[SeparationDebug] Snapshot turn={snapshot.turnId} actor={actorEntityId ?? "null"} actorCell={actorCell} entityCount={snapshot.entities.Count}");
        foreach (StageEntityState entity in snapshot.entities.Values)
        {
            if (!entity.isActive || entity.kind != StageEntityKind.BlockMerged) continue;
            Debug.Log($"[SeparationDebug] Snapshot merged={entity.entityId} cell={entity.cell} origin={entity.originBlockCell} remaining={entity.remainingTurns} justCreated={entity.isJustCreated} createdTurn={entity.createdTurnId} dueTurn={entity.separationDueTurn} infinite={entity.isInfinite}");
            if (entity.remainingTurns != 0) continue;

            BlockMerged runtimeMerged = entity.runtimeObject == null
                ? null
                : entity.runtimeObject.GetComponent<BlockMerged>();
            if (runtimeMerged == null)
            {
                Debug.LogWarning($"[SeparationDebug] Due runtime fields merged={entity.entityId} runtimeComponent=null originBlockEntityId={entity.originBlockEntityId ?? "null"} originBlockKeyExists={HasEntityKey(snapshot, entity.originBlockEntityId)} originBackgroundEntityId={entity.originBackgroundEntityId ?? "null"} originBackgroundKeyExists={HasEntityKey(snapshot, entity.originBackgroundEntityId)}");
                continue;
            }

            Debug.Log($"[SeparationDebug] Due runtime fields merged={entity.entityId} originBlockPrefab=[{DescribeBlockReference(runtimeMerged.originBlockPrefab)}] originBackgroundObject=[{DescribeBackgroundReference(runtimeMerged.originBackgroundObject)}] originBlockPosition={runtimeMerged.originBlockPosition} remaining={runtimeMerged.remainingTurns} createdTurn={runtimeMerged.createdTurnId} dueTurn={runtimeMerged.separationDueTurn} originBlockEntityId={entity.originBlockEntityId ?? "null"} originBlockKeyExists={HasEntityKey(snapshot, entity.originBlockEntityId)} originBackgroundEntityId={entity.originBackgroundEntityId ?? "null"} originBackgroundKeyExists={HasEntityKey(snapshot, entity.originBackgroundEntityId)}");
        }
    }

    private static string DescribeBlockReference(GameObject gameObject)
    {
        if (gameObject == null)
            return "null=true name=null objectId=none blockComponentId=none expectedEntityKey=none";

        Block component = gameObject.GetComponent<Block>();
        string componentId = component == null ? "missing" : component.GetInstanceID().ToString();
        string expectedKey = component == null ? "missing" : "block:" + componentId;
        return $"null=false name=\"{gameObject.name}\" objectId={gameObject.GetInstanceID()} blockComponentId={componentId} expectedEntityKey={expectedKey}";
    }

    private static string DescribeBackgroundReference(GameObject gameObject)
    {
        if (gameObject == null)
            return "null=true name=null objectId=none backgroundComponentId=none expectedEntityKey=none";

        BlockBackground component = gameObject.GetComponent<BlockBackground>();
        string componentId = component == null ? "missing" : component.GetInstanceID().ToString();
        string expectedKey = component == null ? "missing" : "background:" + componentId;
        return $"null=false name=\"{gameObject.name}\" objectId={gameObject.GetInstanceID()} backgroundComponentId={componentId} expectedEntityKey={expectedKey}";
    }

    private static bool HasEntityKey(StageBoardSnapshot snapshot, string entityId)
    {
        return !string.IsNullOrEmpty(entityId) && snapshot.entities.ContainsKey(entityId);
    }

    private static void LogScheduleDebug(StageBoardSnapshot snapshot, SeparationSchedule schedule)
    {
        if (schedule == null)
        {
            Debug.LogWarning($"[SeparationDebug] Schedule missing snapshotTurn={snapshot.turnId}");
            return;
        }

        Debug.Log($"[SeparationDebug] Schedule snapshotTurn={snapshot.turnId} valid={schedule.isValid} planCount={schedule.plans.Count} invalidReason={schedule.invalidReason}");
        foreach (SeparationPlan plan in schedule.plans)
        {
            if (plan == null)
            {
                Debug.LogWarning("[SeparationDebug] Schedule contains null plan");
                continue;
            }

            snapshot.entities.TryGetValue(plan.mergedEntityId, out StageEntityState merged);
            Debug.Log($"[SeparationDebug] Schedule plan merged={plan.mergedEntityId} cell={(merged == null ? "missing" : merged.cell.ToString())} origin={plan.originBlockCell} remaining={(merged == null ? -999 : merged.remainingTurns)} justCreated={(merged != null && merged.isJustCreated)} createdTurn={(merged == null ? -999 : merged.createdTurnId)} dueTurn={(merged == null ? -999 : merged.separationDueTurn)} deferred={plan.isDeferred} deferReason={plan.deferReason} chainCount={plan.chainMovements.Count}");
        }

        foreach (StageEntityState entity in snapshot.entities.Values)
        {
            if (!entity.isActive || entity.kind != StageEntityKind.BlockMerged || entity.remainingTurns > 0) continue;
            bool included = false;
            foreach (SeparationPlan plan in schedule.plans)
            {
                if (plan != null && plan.mergedEntityId == entity.entityId)
                {
                    included = true;
                    break;
                }
            }

            Debug.Log($"[SeparationDebug] Due target merged={entity.entityId} snapshotTurn={snapshot.turnId} included={included} remaining={entity.remainingTurns} justCreated={entity.isJustCreated} createdTurn={entity.createdTurnId} dueTurn={entity.separationDueTurn}");
        }
    }

    private static void LogResolverResultDebug(StageBoardSnapshot snapshot, SeparationSchedule schedule, TurnResult result)
    {
        Debug.Log($"[SeparationDebug] Resolver exit snapshotTurn={snapshot.turnId} valid={result != null && result.isValid} invalidReason={(result == null ? "null-result" : result.invalidReason)} committedTurn={(result == null || result.committedBoard == null ? -1 : result.committedBoard.turnId)}");
        if (result == null || result.committedBoard == null || schedule == null) return;

        foreach (SeparationPlan plan in schedule.plans)
        {
            if (plan == null) continue;
            snapshot.entities.TryGetValue(plan.mergedEntityId, out StageEntityState before);
            result.committedBoard.entities.TryGetValue(plan.mergedEntityId, out StageEntityState after);
            bool separationApplied = before != null && before.isActive && after != null && !after.isActive;
            Debug.Log($"[SeparationDebug] Resolver plan merged={plan.mergedEntityId} deferred={plan.isDeferred} deferReason={plan.deferReason} separationApplied={separationApplied} remainingBefore={(before == null ? -999 : before.remainingTurns)} remainingAfter={(after == null ? -999 : after.remainingTurns)} activeAfter={(after != null && after.isActive)}");
        }
    }
}
