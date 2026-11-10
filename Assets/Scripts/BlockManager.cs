using System.Collections.Generic;
using UnityEngine;

/// <summary> StageBoard에서 Chaind Push만 계산하는 Stage Component </summary>
public class BlockManager : MonoBehaviour
{
    /// <summary>Profile과 Scehduel만 사용해 cell scale Chaind Push 계산</summary>
    public static bool TryCreatePushPlanPure(StageBoardWorkingCopy board, StageCell start, StageCell direction, string ownerId, out PushPlan plan, out string failureReason)
    {
        return TryCreateChainPlan(board, start, direction, ownerId, null, out plan, out failureReason);
    }

    /// <summary> Separation Push Chain을 Player Cell에서 끝냄</summary>
    public static bool TryCreateSeparationPushPlanPure(StageBoardWorkingCopy board, StageCell start, StageCell direction, string ownerId, out PushPlan plan, out string failureReason)
    {
        return TryCreateChainPlan(board, start, direction, ownerId, IsSeparationPlayer, out plan, out failureReason);
    }

    /// <summary> Original Cell과 Chain 끝에서 동일한 Player 분류를 사용</summary>
    internal static bool IsSeparationPlayer(StageEntityState entity)
    {
        return entity != null
            && entity.kind == StageEntityKind.Player
            && entity.profile.zone == StageZone.Top
            && entity.profile.movement == MovementCapability.SelfMovable
            && entity.profile.occupancy == OccupancyType.Solid;
    }
    
    /// <summary> pair collision 검증 뒤, 먼 Block부터 push board를 만듦</summary>
    private static bool TryCreateChainPlan(StageBoardWorkingCopy board, StageCell start, StageCell direction, string ownerId,
        System.Predicate<StageEntityState> terminatesChain, out PushPlan plan, out string failureReason)
    {
        plan = new PushPlan();
        failureReason = null;

        if (board == null)
        {
            failureReason = "Board is missing.";
            return false;
        }

        if (!IsCardinal(direction))
        {
            failureReason = "Direction must be a cardinal unit.";
            return false;
        }

        List<StageEntityState> chain = new List<StageEntityState>();
        StageCell current = start;
        while (true)
        {
            CellQueryResult currentQuery = board.QueryCell(current);
            if (currentQuery.solidTopEntityIds.Count != 1
                || !board.State.entities.TryGetValue(currentQuery.solidTopEntityIds[0], out StageEntityState entity)
                || !IsPushableSolidTop(entity))
            {
                failureReason = "Push chain requires exactly one Pushable Solid Top entity per Cell.";
                return false;
            }

            if (!board.CanAccessEntity(entity.entityId, ownerId))
            {
                failureReason = "Push chain entity is reserved.";
                return false;
            }

            if (IsGroundOneWayExitBlocked(currentQuery, direction))
            {
                failureReason = "GroundOneWay exit direction is blocked.";
                return false;
            }

            chain.Add(entity);
            StageCell next = new StageCell(current.x + direction.x, current.y + direction.y);
            CellQueryResult nextQuery = board.QueryCell(next);

            if (!board.CanAccessCell(next, ownerId))
            {
                failureReason = "Push destination is reserved.";
                return false;
            }

            if (nextQuery.hasObstacle || !nextQuery.HasBottom)
            {
                failureReason = "Push destination is blocked or has no Bottom.";
                return false;
            }

            if (nextQuery.solidTopEntityIds.Count == 0) break;
            if (nextQuery.solidTopEntityIds.Count != 1
                || !board.State.entities.TryGetValue(nextQuery.solidTopEntityIds[0], out StageEntityState nextEntity))
            {
                failureReason = "Push destination has a non-pushable Solid Top entity.";
                return false;
            }

            // Player가 먼저 이 Cell에서 이탈
            if (terminatesChain != null && terminatesChain(nextEntity)) break;
            if (!IsPushableSolidTop(nextEntity))
            {
                failureReason = "Push destination has a non-pushable Solid Top entity.";
                return false;
            }

            current = next;
        }

        for (int index = chain.Count - 1; index >= 0; index--)
        {
            StageEntityState entity = chain[index];
            plan.movements.Add(new BoardMovement
            {
                entityId = entity.entityId,
                from = entity.cell,
                to = new StageCell(entity.cell.x + direction.x, entity.cell.y + direction.y)
            });
        }

        return true;
    }

    private static bool IsCardinal(StageCell direction)
    {
        return (direction.x == 0) != (direction.y == 0)
            && System.Math.Abs(direction.x) <= 1
            && System.Math.Abs(direction.y) <= 1;
    }

    private static bool IsPushableSolidTop(StageEntityState entity)
    {
        return entity.profile.zone == StageZone.Top
            && entity.profile.movement == MovementCapability.Pushable
            && entity.profile.occupancy == OccupancyType.Solid;
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
}
