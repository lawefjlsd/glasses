using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>  확정된 Snapshot, Separation Schedule, Player Action Plan을 순수 TurnResult로 결합 </summary>
public static class StageTurnResolver
{
    /// <summary> Turn을 진행하지 않고 일반 Merge/CC 규칙을 한 번 적용 </summary>
    public static TurnResult ResolveInitialMerges(StageBoardSnapshot snapshot)
    {
        TurnResult result = new TurnResult
        {
            separationSchedule = new SeparationSchedule(),
            mergeBatch = new MergeBatchResult(),
            animationBatch = new AnimationBatch()
        };

        if (snapshot == null) return Invalid(result, "Initial Merge snapshot is missing.");

        StageBoardWorkingCopy board = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();
        StageBoardValidationResult initialValidation = board.ValidateBoard();
        if (!initialValidation.isValid) return Invalid(result, initialValidation.invalidReason);

        result.isGameOverCandidate = initialValidation.isGameOverCandidate;
        if (!result.isGameOverCandidate
            && !ApplyMerges(board, snapshot.turnId, result.mergeBatch, out string mergeFailure))
            return Invalid(result, mergeFailure);

        if (!RefreshCurrentColor(board.State, out string colorFailure)) return Invalid(result, colorFailure);
        result.mergeBatch.resultingColor = board.State.currentColor;
        board.State.turnId = snapshot.turnId;

        StageBoardValidationResult finalValidation = board.ValidateBoard();
        if (!finalValidation.isValid) return Invalid(result, finalValidation.invalidReason);

        result.isValid = true;
        result.isGameOverCandidate |= finalValidation.isGameOverCandidate;
        result.committedBoard = board.State.Clone();
        return result;
    }

    public static TurnResult Resolve(
        StageBoardSnapshot snapshot,
        SeparationSchedule schedule,
        PlayerActionPlan actionPlan)
    {
        Debug.Log($"[SeparationDebug] Resolver internal start snapshotTurn={(snapshot == null ? -1 : snapshot.turnId)} scheduleValid={schedule != null && schedule.isValid} planCount={(schedule == null ? -1 : schedule.plans.Count)} actionOutcome={(actionPlan == null ? PlayerActionOutcome.Invalid : actionPlan.outcome)}");
        TurnResult result = new TurnResult
        {
            separationSchedule = schedule,
            actionPlan = actionPlan,
            mergeBatch = new MergeBatchResult(),
            animationBatch = new AnimationBatch()
        };

        if (snapshot == null) return Invalid(result, "Snapshot is missing.");
        if (schedule == null || !schedule.isValid) return Invalid(result, schedule == null ? "Separation schedule is missing." : schedule.invalidReason);
        if (actionPlan == null || !actionPlan.isValid || actionPlan.outcome != PlayerActionOutcome.Move) return Invalid(result, actionPlan == null ? "Player action is missing." : actionPlan.failureMessage);

        StageBoardWorkingCopy board = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();
        StageBoardValidationResult initialValidation = board.ValidateBoard();
        if (!initialValidation.isValid) return Invalid(result, initialValidation.invalidReason);

        if (!ApplyScheduleReservations(board, schedule, out string reservationFailure)) return Invalid(result, reservationFailure);

        if (!IsCardinal(new StageCell(actionPlan.actorMovement.to.x - actionPlan.actorMovement.from.x,
            actionPlan.actorMovement.to.y - actionPlan.actorMovement.from.y)))
            return Invalid(result, "Internal Player Action mismatch: planned movement must be a cardinal unit.");

        string actionOwnerId = "player-action:" + actionPlan.actorMovement.entityId;
        if (actionPlan.pushPlan != null)
        {
            if (!board.TryApply(actionPlan.pushPlan.movements, MovementIntent.Push, actionOwnerId)) return Invalid(result, "Player Push plan could not be applied.");
            RecordSuccessfulBlockMoves(board, actionPlan.pushPlan.movements);
            result.animationBatch.movements.AddRange(actionPlan.pushPlan.movements);
        }
        if (!board.TryApply(new[] { actionPlan.actorMovement }, MovementIntent.SelfMove, actionOwnerId)) return Invalid(result, "Player movement could not be applied.");
        result.animationBatch.movements.Add(actionPlan.actorMovement);

        if (!ApplySeparations(ref board, schedule, actionPlan.actorMovement, result.animationBatch, out string separationFailure)) return Invalid(result, separationFailure);

        StageBoardValidationResult postSeparation = board.ValidateBoard();
        if (!postSeparation.isValid) return Invalid(result, postSeparation.invalidReason);
        result.isGameOverCandidate = postSeparation.isGameOverCandidate;

        long committedTurnId = snapshot.turnId + 1;
        if (!result.isGameOverCandidate)
        {
            if (!ApplyMerges(board, committedTurnId, result.mergeBatch, out string mergeFailure)) return Invalid(result, mergeFailure);
        }
        if (!RefreshCurrentColor(board.State, out string colorFailure)) return Invalid(result, colorFailure);
        result.mergeBatch.resultingColor = board.State.currentColor;

        UpdateRemainingTurns(board.State, snapshot, schedule, committedTurnId);
        board.State.turnId = committedTurnId;

        StageBoardValidationResult finalValidation = board.ValidateBoard();
        if (!finalValidation.isValid) return Invalid(result, finalValidation.invalidReason);

        result.isValid = true;
        result.isGameOverCandidate |= finalValidation.isGameOverCandidate;
        result.isStageClearCandidate = actionPlan.isStageClearCandidate;
        result.committedBoard = board.State.Clone();
        return result;
    }

    private static bool ApplyScheduleReservations(StageBoardWorkingCopy board, SeparationSchedule schedule, out string failureReason)
    {
        foreach (SeparationPlan plan in schedule.plans)
        {
            if (plan == null || plan.isDeferred) continue;
            if (string.IsNullOrEmpty(plan.ownerId)) { failureReason = "Executable Separation plan requires an owner ID."; return false; }
            board.ApplyReservations(plan.reservedCells, plan.reservedEntities, plan.ownerId);
        }
        failureReason = null;
        return true;
    }

    private static bool ApplySeparations(ref StageBoardWorkingCopy board, SeparationSchedule schedule, BoardMovement actorMovement, AnimationBatch animationBatch, out string failureReason)
    {
        if (!board.State.entities.TryGetValue(actorMovement.entityId, out StageEntityState actor)
            || !actor.isActive
            || !actor.cell.Equals(actorMovement.to))
        {
            failureReason = "Internal Player Action mismatch: actor did not complete its planned movement before Separation.";
            return false;
        }

        foreach (SeparationPlan plan in schedule.plans)
        {
            if (plan == null) continue;
            if (plan.isDeferred)
            {
                board.State.entities.TryGetValue(plan.mergedEntityId, out StageEntityState deferred);
                Debug.LogWarning($"[SeparationDebug] Separation skipped merged={plan.mergedEntityId} deferred=true deferReason={plan.deferReason} cell={(deferred == null ? "missing" : deferred.cell.ToString())} origin={plan.originBlockCell} remaining={(deferred == null ? -999 : deferred.remainingTurns)}");
                continue;
            }

            // Player Action은 예약 Cell에 진입하지 않아야 함.
            if (ContainsActivePlayer(board, plan.originBlockCell))
            {
                failureReason = "Internal Player Action/Separation mismatch: Player occupies the scheduled restoration Cell.";
                return false;
            }
            foreach (BoardMovement movement in plan.chainMovements)
            {
                if (movement.entityId == actorMovement.entityId
                    || (board.State.entities.TryGetValue(movement.entityId, out StageEntityState chainEntity)
                        && BlockManager.IsSeparationPlayer(chainEntity))
                    || ContainsActivePlayer(board, movement.to))
                {
                    failureReason = "Internal Player Action/Separation mismatch: Player participates in the scheduled Chain or occupies its destination.";
                    return false;
                }
            }
            if (!TryValidateSeparationPlan(board, plan, out string deferReason))
            {
                Defer(plan, deferReason);
                Debug.LogWarning($"[SeparationDebug] Separation deferred during resolve merged={plan.mergedEntityId} deferReason={plan.deferReason} origin={plan.originBlockCell}");
                continue;
            }

            {
                StageEntityState scheduledMerged = board.State.entities[plan.mergedEntityId];
                Debug.Log($"[SeparationDebug] Separation applying merged={scheduledMerged.entityId} cell={scheduledMerged.cell} origin={plan.originBlockCell} remaining={scheduledMerged.remainingTurns} justCreated={scheduledMerged.isJustCreated} createdTurn={scheduledMerged.createdTurnId} dueTurn={scheduledMerged.separationDueTurn} chainCount={plan.chainMovements.Count}");
            }

            // Plan 전체를 별도 Board에서 검증.
            StageBoardWorkingCopy stagedBoard = StageBoard.FromSnapshot(board.State).CreateWorkingCopy();
            if (!ApplyScheduleReservations(stagedBoard, schedule, out failureReason)) return false;
            if (plan.chainMovements.Count > 0)
            {
                if (!stagedBoard.TryApply(plan.chainMovements, MovementIntent.Push, plan.ownerId))
                {
                    Defer(plan, "Separation Push plan no longer matches the working Board.");
                    Debug.LogWarning($"[SeparationDebug] Separation chain deferred merged={plan.mergedEntityId} deferReason={plan.deferReason}");
                    continue;
                }
            }
            if (!TryGetCurrentSeparationEntities(stagedBoard, plan, out StageEntityState merged, out StageEntityState block, out StageEntityState background))
            {
                failureReason = "Separation entities are missing after Chain Push application.";
                return false;
            }

            StageCell mergedCell = merged.cell;
            merged.isActive = false;
            block.cell = plan.originBlockCell;
            block.isActive = true;
            background.cell = mergedCell;
            background.isActive = true;
            StageBoardValidationResult validation = stagedBoard.ValidateBoard();
            if (!validation.isValid) { failureReason = validation.invalidReason; return false; }

            board = stagedBoard;
            BoardMovement restoration = new BoardMovement { entityId = block.entityId, from = mergedCell, to = plan.originBlockCell };
            RecordSuccessfulBlockMoves(board, plan.chainMovements);
            RecordSuccessfulBlockMoves(board, new[] { restoration });
            animationBatch.movements.AddRange(plan.chainMovements);
            animationBatch.movements.Add(restoration);
            Debug.Log($"[SeparationDebug] Separation applied merged={merged.entityId} activeAfter={merged.isActive} block={block.entityId} blockCell={block.cell} background={background.entityId} backgroundCell={background.cell} gameOverCandidate={validation.isGameOverCandidate}");
        }

        failureReason = null;
        return true;
    }

    private static bool TryGetCurrentSeparationEntities(
        StageBoardWorkingCopy board,
        SeparationPlan plan,
        out StageEntityState merged,
        out StageEntityState block,
        out StageEntityState background)
    {
        merged = null;
        block = null;
        background = null;
        return board.State.entities.TryGetValue(plan.mergedEntityId, out merged)
            && board.State.entities.TryGetValue(plan.originBlockEntityId, out block)
            && board.State.entities.TryGetValue(plan.originBackgroundEntityId, out background);
    }

    private static bool TryValidateSeparationPlan(
        StageBoardWorkingCopy board,
        SeparationPlan plan,
        out string failureReason)
    {
        if (!board.State.entities.TryGetValue(plan.mergedEntityId, out StageEntityState merged)
            || !merged.isActive)
        {
            failureReason = "Scheduled merged entity is missing or inactive.";
            return false;
        }

        if (!board.State.entities.TryGetValue(plan.originBlockEntityId, out StageEntityState block)
            || !board.State.entities.TryGetValue(plan.originBackgroundEntityId, out StageEntityState background))
        {
            failureReason = "Separation origin Block or Background is missing.";
            return false;
        }

        if (block.isActive || background.isActive)
        {
            failureReason = "Separation origin entities are already active.";
            return false;
        }

        StageCell expectedDirection = new StageCell(
            plan.originBlockCell.x - merged.cell.x,
            plan.originBlockCell.y - merged.cell.y);
        HashSet<string> movementEntities = new HashSet<string>();
        foreach (BoardMovement movement in plan.chainMovements)
        {
            StageCell direction = new StageCell(movement.to.x - movement.from.x, movement.to.y - movement.from.y);
            if (!movementEntities.Add(movement.entityId)
                || !board.State.entities.TryGetValue(movement.entityId, out StageEntityState entity)
                || !entity.isActive
                || !entity.cell.Equals(movement.from)
                || !direction.Equals(expectedDirection)
                || entity.profile.zone != StageZone.Top
                || entity.profile.movement != MovementCapability.Pushable
                || entity.profile.occupancy != OccupancyType.Solid
                || !plan.reservedEntities.Contains(movement.entityId)
                || !plan.reservedCells.Contains(movement.from)
                || !plan.reservedCells.Contains(movement.to)
                || !board.CanAccessEntity(movement.entityId, plan.ownerId)
                || !board.CanAccessCell(movement.to, plan.ownerId))
            {
                failureReason = "Separation Chain no longer matches its scheduled reservation.";
                return false;
            }

            if (!IsCardinal(direction) || IsGroundOneWayExitBlocked(board.QueryCell(movement.from), direction))
            {
                failureReason = "Separation Chain violates its GroundOneWay exit condition.";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    private static bool ContainsActivePlayer(StageBoardWorkingCopy board, StageCell cell)
    {
        CellQueryResult query = board.QueryCell(cell);
        foreach (string entityId in query.solidTopEntityIds)
        {
            if (board.State.entities.TryGetValue(entityId, out StageEntityState entity)
                && BlockManager.IsSeparationPlayer(entity)
                && entity.isActive)
                return true;
        }

        return false;
    }

    private static bool IsCardinal(StageCell direction)
    {
        return (direction.x == 0) != (direction.y == 0)
            && Math.Abs(direction.x) <= 1
            && Math.Abs(direction.y) <= 1;
    }

    private static bool IsGroundOneWayExitBlocked(CellQueryResult query, StageCell direction)
    {
        if (!string.IsNullOrEmpty(query.bottomEntityId)
            || query.staticState == null
            || query.staticState.bottomGroundKind != StageBottomGroundKind.GroundOneWay)
            return false;

        GroundOneWay.ExitDirection exitDirection = direction.y > 0
            ? GroundOneWay.ExitDirection.W
            : direction.x < 0
                ? GroundOneWay.ExitDirection.A
                : direction.y < 0
                    ? GroundOneWay.ExitDirection.S
                    : GroundOneWay.ExitDirection.D;
        return query.staticState.exitDirection != exitDirection;
    }

    private static void Defer(SeparationPlan plan, string reason)
    {
        plan.isDeferred = true;
        plan.deferReason = string.IsNullOrEmpty(reason) ? "Separation is deferred." : reason;
    }

    /// <summary> Clone 교체 후 현재 Entity에 valid move start만 기록 </summary>
    private static void RecordSuccessfulBlockMoves(StageBoardWorkingCopy board, IEnumerable<BoardMovement> movements)
    {
        foreach (BoardMovement movement in movements)
        {
            if (movement.from.Equals(movement.to)) continue;
            StageEntityState entity = board.State.entities[movement.entityId];
            if (entity.profile.zone == StageZone.Top
                && entity.profile.movement == MovementCapability.Pushable
                && entity.profile.occupancy == OccupancyType.Solid)
                entity.lastMoveFromCell = movement.from;
        }
    }

    private static bool ApplyMerges(StageBoardWorkingCopy board, long committedTurnId, MergeBatchResult batch, out string failureReason)
    {
        List<Tuple<StageEntityState, StageEntityState>> candidates = new List<Tuple<StageEntityState, StageEntityState>>();
        foreach (StageEntityState block in board.State.entities.Values)
        {
            if (!block.isActive || block.profile.zone != StageZone.Top || block.profile.movement != MovementCapability.Pushable || block.profile.occupancy != OccupancyType.Solid || block.mergedPrefab == null) continue;
            CellQueryResult query = board.QueryCell(block.cell);
            if (string.IsNullOrEmpty(query.bottomEntityId) || !board.State.entities.TryGetValue(query.bottomEntityId, out StageEntityState background) || background.kind != StageEntityKind.BlockBackground) continue;
            if (!StageColorRules.TryCanMerge(block.colorName, background.acceptedColorName, out bool canMerge, out failureReason)) return false;
            if (canMerge) candidates.Add(Tuple.Create(block, background));
        }

        batch.candidateCount = candidates.Count;

        candidates.Sort((left, right) =>
        {
            int effect = left.Item1.mergeColorEffect.CompareTo(right.Item1.mergeColorEffect);
            return effect != 0 ? effect : string.CompareOrdinal(left.Item1.entityId, right.Item1.entityId);
        });

        long nextBatchId = 0;
        long nextEventOrder = 0;
        foreach (StageEntityState entity in board.State.entities.Values)
        {
            if (entity.kind != StageEntityKind.BlockMerged) continue;
            nextBatchId = Math.Max(nextBatchId, entity.mergeBatchId + 1);
            nextEventOrder = Math.Max(nextEventOrder, entity.mergeEventOrder + 1);
        }

        foreach (Tuple<StageEntityState, StageEntityState> candidate in candidates)
        {
            StageEntityState block = candidate.Item1;
            StageEntityState background = candidate.Item2;
            if (!block.isActive || !background.isActive) continue;

            string mergedEntityId = "planned-merged:" + committedTurnId + ":" + nextBatchId + ":" + nextEventOrder + ":" + block.entityId;
            StageCell originBlockCell = block.lastMoveFromCell ?? block.cell;
            StageEntityState merged = new StageEntityState
            {
                entityId = mergedEntityId,
                kind = StageEntityKind.BlockMerged,
                cell = block.cell,
                isActive = true,
                mergedPrefab = block.mergedPrefab,
                colorName = block.colorName,
                mergeColorEffect = block.mergeColorEffect,
                remainingTurns = background.remainingTurns,
                isInfinite = background.remainingTurns <= 0,
                isJustCreated = true,
                createdTurnId = committedTurnId,
                separationDueTurn = background.remainingTurns <= 0 ? -1 : committedTurnId + background.remainingTurns,
                mergeBatchId = nextBatchId,
                mergeEventOrder = nextEventOrder,
                originBlockCell = originBlockCell,
                originBlockEntityId = block.entityId,
                originBackgroundEntityId = background.entityId,
                profile = new StageObjectProfile { zone = StageZone.Bottom, movement = MovementCapability.Static, occupancy = OccupancyType.Surface }
            };

            block.isActive = false;
            background.isActive = false;
            board.State.entities.Add(mergedEntityId, merged);
            batch.mergePlans.Add(new MergePlan { blockEntityId = block.entityId, backgroundEntityId = background.entityId, mergedEntityId = mergedEntityId, originBlockCell = originBlockCell, colorEffect = block.mergeColorEffect, mergeBatchId = nextBatchId, mergeEventOrder = nextEventOrder });
            nextEventOrder++;
        }

        StageBoardValidationResult validation = board.ValidateBoard();
        if (!validation.isValid) { failureReason = validation.invalidReason; return false; }
        failureReason = null;
        return true;
    }

    private static bool RefreshCurrentColor(StageBoardSnapshot board, out string failureReason)
    {
        List<StageEntityState> merged = new List<StageEntityState>();
        foreach (StageEntityState entity in board.entities.Values) if (entity.isActive && entity.kind == StageEntityKind.BlockMerged) merged.Add(entity);
        merged.Sort((left, right) =>
        {
            int batch = left.mergeBatchId.CompareTo(right.mergeBatchId); if (batch != 0) return batch;
            int effect = left.mergeColorEffect.CompareTo(right.mergeColorEffect); if (effect != 0) return effect;
            int order = left.mergeEventOrder.CompareTo(right.mergeEventOrder); if (order != 0) return order;
            return string.CompareOrdinal(left.entityId, right.entityId);
        });

        string color = "Black";
        foreach (StageEntityState entity in merged)
        {
            if (!StageColorRules.TryApplyMergeEffect(color, entity.colorName, entity.mergeColorEffect, out color, out failureReason)) return false;
        }
        board.currentColor = color;
        failureReason = null;
        return true;
    }

    private static void UpdateRemainingTurns(StageBoardSnapshot board, StageBoardSnapshot source, SeparationSchedule schedule, long committedTurnId)
    {
        HashSet<string> deferredMergedIds = new HashSet<string>();
        foreach (SeparationPlan plan in schedule.plans) if (plan != null && plan.isDeferred && !string.IsNullOrEmpty(plan.mergedEntityId)) deferredMergedIds.Add(plan.mergedEntityId);
        foreach (StageEntityState entity in board.entities.Values)
        {
            if (!entity.isActive || entity.kind != StageEntityKind.BlockMerged || entity.isInfinite) continue;
            if (!source.entities.ContainsKey(entity.entityId)) continue;
            int before = entity.remainingTurns;
            bool isDeferred = deferredMergedIds.Contains(entity.entityId);
            if (isDeferred)
            {
                Debug.LogWarning($"[SeparationDebug] Remaining unchanged merged={entity.entityId} committedTurn={committedTurnId} deferred=true before={before} after={entity.remainingTurns} justCreated={entity.isJustCreated} createdTurn={entity.createdTurnId} dueTurn={entity.separationDueTurn}");
                continue;
            }
            else if (entity.createdTurnId >= 0 && entity.separationDueTurn >= entity.createdTurnId) entity.remainingTurns = Math.Max(0, (int)(entity.separationDueTurn - committedTurnId));
            else if (entity.isJustCreated) entity.isJustCreated = false;
            else entity.remainingTurns = Math.Max(1, entity.remainingTurns - 1);
            if (entity.isJustCreated && entity.createdTurnId >= 0 && committedTurnId > entity.createdTurnId) entity.isJustCreated = false;
            Debug.Log($"[SeparationDebug] Remaining update merged={entity.entityId} committedTurn={committedTurnId} deferred=false before={before} after={entity.remainingTurns} justCreated={entity.isJustCreated} createdTurn={entity.createdTurnId} dueTurn={entity.separationDueTurn}");
        }
    }

    private static TurnResult Invalid(TurnResult result, string reason)
    {
        result.isValid = false;
        result.invalidReason = string.IsNullOrEmpty(reason) ? "Turn plan is invalid." : reason;
        return result;
    }
}
