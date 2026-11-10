using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>StageBoard Commit의 succeed·fail을 반환</summary>
public sealed class StageRuntimeCommitResult
{
    public bool isSuccess;
    public string error;
}

/// <summary> 확정된 StageBoard 결과를 런타임 오브젝트에 반영하고 AnimationBatch를 병렬 재생 </summary>
public class StageRuntimeCommitter : MonoBehaviour
{
    private readonly HashSet<GameObject> dynamicObjects = new HashSet<GameObject>();

    /// <summary> TurnResult를 한 번만 Scene 상태로 반영 </summary>
    public IEnumerator Commit(StageBoardSnapshot before, TurnResult result, MovementHelper movementHelper, Action<StageRuntimeCommitResult> completed)
    {
        if (before == null || result == null || !result.isValid || result.committedBoard == null)
        {
            completed?.Invoke(new StageRuntimeCommitResult { error = "Invalid commit input." });
            yield break;
        }

        yield return StartCoroutine(ApplySnapshot(
            before,
            result.committedBoard,
            result.animationBatch,
            movementHelper,
            true,
            completed));
    }

    /// <summary> Undo가 보관한 BoardSnapshot을 애니메이션으로 병렬 재생 </summary>
    public IEnumerator RestoreSnapshot(StageBoardSnapshot current, StageBoardSnapshot target, MovementHelper movementHelper, Action<StageRuntimeCommitResult> completed)
    {
        if (current == null || target == null)
        {
            completed?.Invoke(new StageRuntimeCommitResult { error = "Undo snapshots are missing." });
            yield break;
        }

        AnimationBatch batch = BuildRestoreAnimationBatch(current, target);
        yield return StartCoroutine(ApplySnapshot(current, target, batch, movementHelper, false, completed));
    }

    private IEnumerator ApplySnapshot(
        StageBoardSnapshot before,
        StageBoardSnapshot committed,
        AnimationBatch batch,
        MovementHelper movementHelper,
        bool createPlannedMerged,
        Action<StageRuntimeCommitResult> completed)
    {
        StageRuntimeCommitResult commit = new StageRuntimeCommitResult();
        Tilemap referenceTilemap = StageGridCoordinates.FindReferenceTilemap();
        if (movementHelper == null)
        {
            commit.error = "MovementHelper is missing.";
            completed?.Invoke(commit);
            yield break;
        }

        if (!PreparePlannedMerged(before, committed, createPlannedMerged, out string preparationError))
        {
            commit.error = preparationError;
            completed?.Invoke(commit);
            yield break;
        }

        List<Coroutine> animations = new List<Coroutine>();
        if (batch != null)
        {
            foreach (BoardMovement movement in batch.movements)
            {
                if (!TryGetRuntimeObject(before, committed, movement.entityId, out GameObject runtimeObject, out StageEntityState source))
                {
                    commit.error = "Animation entity is missing: " + movement.entityId;
                    completed?.Invoke(commit);
                    yield break;
                }

                if (!runtimeObject.activeSelf)
                {
                    runtimeObject.transform.position = StageGridCoordinates.CellToWorld(movement.from, referenceTilemap);
                    runtimeObject.SetActive(true);
                }

                animations.Add(StartCoroutine(movementHelper.SmoothMoveCoroutine(
                    runtimeObject.transform,
                    StageGridCoordinates.CellToWorld(movement.to, referenceTilemap),
                    GetMoveSpeed(source, movementHelper))));
            }
        }

        foreach (Coroutine animation in animations)
            yield return animation;

        if (!ApplyFinalState(before, committed, referenceTilemap, out string finalStateError))
        {
            commit.error = finalStateError;
            completed?.Invoke(commit);
            yield break;
        }

        commit.isSuccess = true;
        completed?.Invoke(commit);
    }

    private bool PreparePlannedMerged(
        StageBoardSnapshot before,
        StageBoardSnapshot committed,
        bool createPlannedMerged,
        out string error)
    {
        error = null;
        foreach (StageEntityState state in committed.entities.Values)
        {
            if (!IsPlannedMerged(state)) continue;
            if (!createPlannedMerged)
            {
                if (state.runtimeObject == null)
                {
                    error = "Undo target contains an unresolved planned merged entity.";
                    return false;
                }

                continue;
            }

            if (state.runtimeObject != null) continue;
            if (!CreateMerged(before, state, out error)) return false;
        }

        return true;
    }

    private bool ApplyFinalState(StageBoardSnapshot before, StageBoardSnapshot committed, Tilemap referenceTilemap, out string error)
    {
        error = null;
        HashSet<GameObject> targetObjects = new HashSet<GameObject>();
        foreach (StageEntityState state in committed.entities.Values)
        {
            if (!TryResolveRuntimeObject(before, state, out GameObject runtimeObject))
            {
                if (state.isActive)
                {
                    error = "Active committed entity has no runtime object: " + state.entityId;
                    return false;
                }

                continue;
            }

            state.runtimeObject = runtimeObject;
            targetObjects.Add(runtimeObject);
            runtimeObject.transform.position = StageGridCoordinates.CellToWorld(state.cell, referenceTilemap);
            ApplyCommittedComponentState(before, committed, state, runtimeObject, referenceTilemap);
            runtimeObject.SetActive(state.isActive);
        }

        foreach (StageEntityState oldState in before.entities.Values)
        {
            if (oldState.runtimeObject != null && !targetObjects.Contains(oldState.runtimeObject))
                oldState.runtimeObject.SetActive(false);
        }

        foreach (GameObject dynamicObject in dynamicObjects)
        {
            if (dynamicObject != null && !targetObjects.Contains(dynamicObject))
                dynamicObject.SetActive(false);
        }

        return true;
    }

    private static void ApplyCommittedComponentState(
        StageBoardSnapshot before,
        StageBoardSnapshot committed,
        StageEntityState state,
        GameObject runtimeObject,
        Tilemap referenceTilemap)
    {
        if (state.kind == StageEntityKind.Block)
        {
            Block block = runtimeObject.GetComponent<Block>();
            if (block != null) block.RestoreLastMoveFromCell(state.lastMoveFromCell);
        }
        if (state.kind != StageEntityKind.BlockMerged) return;

        BlockMerged merged = runtimeObject.GetComponent<BlockMerged>();
        if (merged == null) return;

        GameObject originBlock = ResolveEntityRuntimeObject(before, committed, state.originBlockEntityId);
        GameObject originBackground = ResolveEntityRuntimeObject(before, committed, state.originBackgroundEntityId);
        merged.ApplyCommittedState(
            state,
            StageGridCoordinates.CellToWorld(state.originBlockCell, referenceTilemap),
            originBlock,
            originBackground);
    }

    private static GameObject ResolveEntityRuntimeObject(
        StageBoardSnapshot before,
        StageBoardSnapshot committed,
        string entityId)
    {
        if (string.IsNullOrEmpty(entityId)) return null;
        if (committed.entities.TryGetValue(entityId, out StageEntityState committedState)
            && committedState.runtimeObject != null)
            return committedState.runtimeObject;
        if (before.entities.TryGetValue(entityId, out StageEntityState beforeState))
            return beforeState.runtimeObject;
        return null;
    }

    private static AnimationBatch BuildRestoreAnimationBatch(StageBoardSnapshot current, StageBoardSnapshot target)
    {
        AnimationBatch batch = new AnimationBatch();
        foreach (StageEntityState targetState in target.entities.Values)
        {
            if (!targetState.isActive) continue;
            if (!current.entities.TryGetValue(targetState.entityId, out StageEntityState currentState)) continue;
            if (!currentState.cell.Equals(targetState.cell))
            {
                batch.movements.Add(new BoardMovement
                {
                    entityId = targetState.entityId,
                    from = currentState.cell,
                    to = targetState.cell
                });
            }
        }

        return batch;
    }

    private static bool TryGetRuntimeObject(
        StageBoardSnapshot before,
        StageBoardSnapshot committed,
        string entityId,
        out GameObject runtimeObject,
        out StageEntityState source)
    {
        runtimeObject = null;
        source = null;
        if (before.entities.TryGetValue(entityId, out source)) runtimeObject = source.runtimeObject;
        if (runtimeObject == null && committed.entities.TryGetValue(entityId, out StageEntityState target))
        {
            source = target;
            runtimeObject = target.runtimeObject;
        }

        return runtimeObject != null;
    }

    private static bool TryResolveRuntimeObject(StageBoardSnapshot before, StageEntityState state, out GameObject runtimeObject)
    {
        runtimeObject = state.runtimeObject;
        if (runtimeObject == null && before.entities.TryGetValue(state.entityId, out StageEntityState oldState))
            runtimeObject = oldState.runtimeObject;
        return runtimeObject != null;
    }

    private bool CreateMerged(StageBoardSnapshot before, StageEntityState state, out string error)
    {
        error = null;
        if (state.mergedPrefab == null)
        {
            error = "Planned merged prefab is missing.";
            return false;
        }

        Tilemap referenceTilemap = StageGridCoordinates.FindReferenceTilemap();
        GameObject mergedObject = Instantiate(state.mergedPrefab, StageGridCoordinates.CellToWorld(state.cell, referenceTilemap), Quaternion.identity);
        if (mergedObject == null)
        {
            error = "Merged instantiate failed.";
            return false;
        }

        BlockMerged merged = mergedObject.GetComponent<BlockMerged>();
        if (merged == null)
        {
            mergedObject.SetActive(false);
            error = "Merged prefab has no BlockMerged component.";
            return false;
        }

        before.entities.TryGetValue(state.originBlockEntityId, out StageEntityState block);
        before.entities.TryGetValue(state.originBackgroundEntityId, out StageEntityState background);
        if (block == null || background == null || block.runtimeObject == null || background.runtimeObject == null)
        {
            mergedObject.SetActive(false);
            error = "Merged origin runtime objects are missing.";
            return false;
        }

        merged.InitializeTimer(
            StageGridCoordinates.CellToWorld(state.originBlockCell, referenceTilemap),
            block.runtimeObject,
            background.runtimeObject,
            state.remainingTurns,
            state.mergeColorEffect,
            state.mergeBatchId,
            state.mergeEventOrder,
            state.createdTurnId,
            state.separationDueTurn,
            state.isInfinite,
            state.isJustCreated);
        merged.colorName = state.colorName;
        state.runtimeObject = mergedObject;
        dynamicObjects.Add(mergedObject);
        mergedObject.SetActive(false);
        return true;
    }

    private static bool IsPlannedMerged(StageEntityState state)
    {
        return state != null && state.entityId != null && state.entityId.StartsWith("planned-merged:", StringComparison.Ordinal);
    }

    private static float GetMoveSpeed(StageEntityState state, MovementHelper movementHelper)
    {
        return movementHelper.defaultMoveSpeed > 0f ? movementHelper.defaultMoveSpeed : 8f;
    }
}
