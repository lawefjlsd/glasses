using UnityEngine;

/// <summary> Snapshot, Separation 예약, 입력만으로 player move result 계산 </summary>
public static class PlayerActionResolver
{
    /// <summary> Move, Push chain을 포함한 player move result </summary>
    public static PlayerActionPlan Resolve(StageBoardSnapshot snapshot, SeparationSchedule schedule, TurnInput input)
    {
        PlayerActionPlan directPlan = ResolveDirectMove(snapshot, schedule, input);
        if (directPlan.outcome != PlayerActionOutcome.PushRequired)
            return directPlan;

        return ResolvePush(snapshot, schedule, input);
    }

    /// <summary> Push가 필요하지 않은 player move 여부와 Stage Clear candidate를 계산 </summary>
    public static PlayerActionPlan ResolveDirectMove(StageBoardSnapshot snapshot, SeparationSchedule schedule, TurnInput input)
    {
        if (snapshot == null)
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.InvalidInput, "Snapshot is missing.");

        if (schedule == null || !schedule.isValid)
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.InvalidSchedule,
                schedule == null ? "Separation schedule is missing." : schedule.invalidReason);

        if (!TryGetDirection(input.direction, out StageCell direction))
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.InvalidInput, "Direction must be cardinal.");

        if (string.IsNullOrEmpty(input.actorEntityId)
            || !snapshot.entities.TryGetValue(input.actorEntityId, out StageEntityState actor)
            || !actor.isActive)
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.ActorMissing, "Actor is missing or inactive.");

        if (actor.profile.zone != StageZone.Top
            || actor.profile.movement != MovementCapability.SelfMovable
            || actor.profile.occupancy != OccupancyType.Solid)
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.ActorProfileInvalid, "Actor profile cannot perform a player move.");

        StageBoardWorkingCopy board = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();
        StageBoardValidationResult boardValidation = board.ValidateBoard();
        if (!boardValidation.isValid)
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.InvalidBoard, boardValidation.invalidReason);

        if (!TryApplyScheduleReservations(board, schedule, out string reservationFailure))
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.InvalidSchedule, reservationFailure);

        const string actionOwnerPrefix = "player-action:";
        string ownerId = actionOwnerPrefix + actor.entityId;
        if (!board.CanAccessEntity(actor.entityId, ownerId))
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.ActorReserved, "Actor is reserved by Separation.");

        if (IsGroundOneWayExitBlocked(board, actor.cell, direction))
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.ExitDirectionBlocked, "GroundOneWay exit direction does not match.");

        StageCell destination = new StageCell(actor.cell.x + direction.x, actor.cell.y + direction.y);
        CellQueryResult destinationQuery = board.QueryCell(destination);

        if (!board.CanAccessCell(destination, ownerId))
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.DestinationReserved, "Destination is reserved by Separation.");

        if (destinationQuery.hasObstacle)
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.Obstacle, "Destination contains an obstacle.");

        if (!TryGetBottom(board, destinationQuery, out string requiredColor, out bool isExclusive))
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.MissingBottom, "Destination has no Bottom surface.");

        if (!StageColorRules.TryIsAvailable(snapshot.currentColor, requiredColor, isExclusive, out bool isAvailable, out string colorFailure)
            || !isAvailable)
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.ColorBlocked,
                string.IsNullOrEmpty(colorFailure) ? "Destination color is unavailable." : colorFailure);

        if (destinationQuery.solidTopEntityIds.Count > 1)
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.SolidConflict, "Destination contains multiple Solid Top entities.");

        if (destinationQuery.solidTopEntityIds.Count == 1)
        {
            string solidId = destinationQuery.solidTopEntityIds[0];
            if (!board.State.entities.TryGetValue(solidId, out StageEntityState solid))
                return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.SolidConflict, "Destination Solid Top entity is missing.");

            if (solid.profile.zone == StageZone.Top
                && solid.profile.movement == MovementCapability.Pushable
                && solid.profile.occupancy == OccupancyType.Solid)
                return Fail(PlayerActionOutcome.PushRequired, PlayerActionFailureReason.PushRequired, "Destination requires a Push plan.");

            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.SolidBlocked, "Destination Solid Top cannot be pushed.");
        }

        BoardMovement movement = new BoardMovement { entityId = actor.entityId, from = actor.cell, to = destination };
        if (!board.TryApply(new[] { movement }, MovementIntent.SelfMove, ownerId))
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.SolidBlocked, "Movement failed board validation.");

        return new PlayerActionPlan
        {
            isValid = true,
            outcome = PlayerActionOutcome.Move,
            failureReason = PlayerActionFailureReason.None,
            actorMovement = movement,
            playerMovement = movement,
            isStageClearCandidate = destinationQuery.hasFinale
        };
    }

    /// <summary>Pushable Solid Top부터 시작하는 전체 Push와 뒤따르는 move를 cell scale로 검증 </summary>
    private static PlayerActionPlan ResolvePush(StageBoardSnapshot snapshot, SeparationSchedule schedule, TurnInput input)
    {
        if (!TryGetDirection(input.direction, out StageCell direction)
            || !snapshot.entities.TryGetValue(input.actorEntityId, out StageEntityState actor))
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.InvalidInput, "Push input is invalid.");

        StageBoardWorkingCopy board = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();
        if (!TryApplyScheduleReservations(board, schedule, out string reservationFailure))
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.InvalidSchedule, reservationFailure);

        string ownerId = "player-action:" + actor.entityId;
        StageCell blockCell = new StageCell(actor.cell.x + direction.x, actor.cell.y + direction.y);

        if (!BlockManager.TryCreatePushPlanPure(
            board,
            blockCell,
            direction,
            ownerId,
            out PushPlan pushPlan,
            out string pushFailure))
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.PushBlocked, pushFailure);

        if (!board.TryApply(pushPlan.movements, MovementIntent.Push, ownerId))
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.PushBlocked, "Push plan failed board validation.");

        BoardMovement actorMovement = new BoardMovement
        {
            entityId = actor.entityId,
            from = actor.cell,
            to = blockCell
        };

        if (!board.TryApply(new[] { actorMovement }, MovementIntent.SelfMove, ownerId))
            return Fail(PlayerActionOutcome.Blocked, PlayerActionFailureReason.PushBlocked, "Actor movement after Push failed board validation.");

        StageBoardValidationResult validation = board.ValidateBoard();
        if (!validation.isValid)
            return Fail(PlayerActionOutcome.Invalid, PlayerActionFailureReason.PushBlocked, validation.invalidReason);

        return new PlayerActionPlan
        {
            isValid = true,
            outcome = PlayerActionOutcome.Move,
            failureReason = PlayerActionFailureReason.None,
            actorMovement = actorMovement,
            playerMovement = actorMovement,
            pushPlan = pushPlan,
            isStageClearCandidate = board.QueryCell(blockCell).hasFinale
        };
    }

    private static bool TryApplyScheduleReservations(StageBoardWorkingCopy board, SeparationSchedule schedule, out string failureReason)
    {
        foreach (SeparationPlan plan in schedule.plans)
        {
            if (plan == null || plan.isDeferred) continue;
            if (string.IsNullOrEmpty(plan.ownerId))
            {
                failureReason = "Executable Separation plan requires an owner ID.";
                return false;
            }

            board.ApplyReservations(plan.reservedCells, plan.reservedEntities, plan.ownerId);
        }

        failureReason = null;
        return true;
    }

    private static bool IsGroundOneWayExitBlocked(StageBoardWorkingCopy board, StageCell source, StageCell direction)
    {
        CellQueryResult query = board.QueryCell(source);
        if (!string.IsNullOrEmpty(query.bottomEntityId)) return false;
        if (query.staticState == null || query.staticState.bottomGroundKind != StageBottomGroundKind.GroundOneWay) return false;
        return query.staticState.exitDirection != ToExitDirection(direction);
    }

    private static bool TryGetBottom(StageBoardWorkingCopy board, CellQueryResult query, out string colorName, out bool isExclusive)
    {
        if (!string.IsNullOrEmpty(query.bottomEntityId)
            && board.State.entities.TryGetValue(query.bottomEntityId, out StageEntityState entity))
        {
            colorName = entity.colorName;
            isExclusive = entity.isExclusive;
            return true;
        }

        if (query.staticState != null && query.staticState.bottomGroundKind != StageBottomGroundKind.Empty)
        {
            colorName = query.staticState.colorName;
            isExclusive = query.staticState.isExclusive;
            return true;
        }

        colorName = null;
        isExclusive = false;
        return false;
    }

    private static bool TryGetDirection(Vector3 input, out StageCell direction)
    {
        direction = new StageCell(Mathf.RoundToInt(input.x), Mathf.RoundToInt(input.y));
        return Mathf.Approximately(input.z, 0f)
            && Mathf.Approximately(input.x, direction.x)
            && Mathf.Approximately(input.y, direction.y)
            && Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1;
    }

    private static GroundOneWay.ExitDirection ToExitDirection(StageCell direction)
    {
        if (direction.y > 0) return GroundOneWay.ExitDirection.W;
        if (direction.x < 0) return GroundOneWay.ExitDirection.A;
        if (direction.y < 0) return GroundOneWay.ExitDirection.S;
        return GroundOneWay.ExitDirection.D;
    }

    private static PlayerActionPlan Fail(PlayerActionOutcome outcome, PlayerActionFailureReason reason, string message)
    {
        return new PlayerActionPlan
        {
            isValid = outcome != PlayerActionOutcome.Invalid,
            outcome = outcome,
            failureReason = reason,
            failureMessage = message
        };
    }
}
