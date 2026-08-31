using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 입력 전 StageBoard Snapshot만 사용해 분리 예약과 유예를 계산합니다.
/// </summary>
public class SeparationManager : MonoBehaviour
{
    /// <summary>입력: snapshot, targetTurn. 출력: SeparationSchedule. 역할: 이번 입력이 Commit할 Turn을 기준으로 due merged를 예약하거나 Deferred로 확정합니다.</summary>
    public SeparationSchedule CreateSchedule(StageBoardSnapshot snapshot, long targetTurn)
    {
        SeparationSchedule schedule = new SeparationSchedule();
        if (snapshot == null)
        {
            schedule.isValid = false;
            schedule.invalidReason = "Snapshot is missing.";
            return schedule;
        }

        StageBoardWorkingCopy board = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();
        StageBoardValidationResult validation = board.ValidateBoard();
        if (!validation.isValid)
        {
            schedule.isValid = false;
            schedule.invalidReason = validation.invalidReason;
            return schedule;
        }

        List<StageEntityState> dueMerged = new List<StageEntityState>();
        foreach (StageEntityState entity in snapshot.entities.Values)
        {
            if (entity.isActive
                && entity.profile.zone == StageZone.Bottom
                && entity.kind == StageEntityKind.BlockMerged
                && !entity.isInfinite
                && IsDue(entity, targetTurn))
                dueMerged.Add(entity);
        }

        // 정렬은 결과/로그의 표시 순서만 고정합니다. 예약 선착순이나 생성 순서 우선권은 없습니다.
        dueMerged.Sort((left, right) => string.CompareOrdinal(left.entityId, right.entityId));
        foreach (StageEntityState merged in dueMerged)
        {
            int geometryDistance = System.Math.Abs(merged.originBlockCell.x - merged.cell.x)
                + System.Math.Abs(merged.originBlockCell.y - merged.cell.y);
            if (geometryDistance > 1)
            {
                schedule.isValid = false;
                schedule.invalidReason = "Merged origin geometry must be equal or cardinal-adjacent.";
                return schedule;
            }

            // 모든 후보는 예약과 이동을 적용하지 않은 동일 Snapshot에서 계산합니다.
            schedule.plans.Add(CreatePlan(board, merged));
        }

        DeferConflictingPaths(board, schedule);
        foreach (SeparationPlan plan in schedule.plans)
        {
            if (plan.isDeferred) continue;
            schedule.reservedCells.UnionWith(plan.reservedCells);
            schedule.reservedEntities.UnionWith(plan.reservedEntities);
        }
        return schedule;
    }

    private static SeparationPlan CreatePlan(StageBoardWorkingCopy board, StageEntityState merged)
    {
        string ownerId = "separation:" + merged.entityId;
        SeparationPlan plan = new SeparationPlan
        {
            planId = ownerId,
            ownerId = ownerId,
            mergedEntityId = merged.entityId,
            originBlockEntityId = merged.originBlockEntityId,
            originBackgroundEntityId = merged.originBackgroundEntityId,
            originBlockCell = merged.originBlockCell
        };

        if (string.IsNullOrEmpty(plan.originBlockEntityId)
            || string.IsNullOrEmpty(plan.originBackgroundEntityId)
            || !board.State.entities.ContainsKey(plan.originBlockEntityId)
            || !board.State.entities.ContainsKey(plan.originBackgroundEntityId))
        {
            Defer(plan, "Separation origin entities are missing.");
            return plan;
        }

        int deltaX = plan.originBlockCell.x - merged.cell.x;
        int deltaY = plan.originBlockCell.y - merged.cell.y;
        int distance = System.Math.Abs(deltaX) + System.Math.Abs(deltaY);
        CellQueryResult origin = board.QueryCell(plan.originBlockCell);
        if (origin.solidTopEntityIds.Count > 1)
        {
            Defer(plan, "Separation origin contains multiple Solid Top entities.");
            return plan;
        }

        if (origin.solidTopEntityIds.Count == 1
            && board.State.entities.TryGetValue(origin.solidTopEntityIds[0], out StageEntityState occupant)
            && !BlockManager.IsSeparationPlayer(occupant))
        {
            // Player는 원본 Cell에서도 Chain 끝과 동일하게 탐색 종료 대상입니다.
            if (IsPushableSolidTop(occupant) && distance > 0)
            {
                if (!BlockManager.TryCreateSeparationPushPlanPure(
                        board,
                        plan.originBlockCell,
                        new StageCell(deltaX, deltaY),
                        plan.ownerId,
                        out PushPlan pushPlan,
                        out string pushFailure))
                {
                    Defer(plan, pushFailure);
                    return plan;
                }

                plan.chainMovements.AddRange(pushPlan.movements);
            }
            else if (!IsPushableSolidTop(occupant))
            {
                Defer(plan, "Separation origin is occupied by a non-pushable Solid Top entity.");
                return plan;
            }
        }

        plan.reservedEntities.Add(merged.entityId);
        plan.reservedEntities.Add(plan.originBlockEntityId);
        plan.reservedEntities.Add(plan.originBackgroundEntityId);
        plan.reservedCells.Add(merged.cell);
        plan.reservedCells.Add(plan.originBlockCell);
        foreach (BoardMovement movement in plan.chainMovements)
        {
            plan.reservedEntities.Add(movement.entityId);
            plan.reservedCells.Add(movement.from);
            plan.reservedCells.Add(movement.to);
        }

        return plan;
    }

    private static bool IsPushableSolidTop(StageEntityState entity)
    {
        return entity.profile.zone == StageZone.Top
            && entity.profile.movement == MovementCapability.Pushable
            && entity.profile.occupancy == OccupancyType.Solid;
    }

    private static void Defer(SeparationPlan plan, string reason)
    {
        plan.isDeferred = true;
        plan.deferReason = string.IsNullOrEmpty(reason) ? "Separation is deferred." : reason;
    }

    /// <summary>입력: 공통 Snapshot board와 후보 schedule. 출력: 없음. 역할: 모든 후보 쌍을 검사한 뒤 충돌한 양쪽을 함께 유예합니다.</summary>
    private static void DeferConflictingPaths(StageBoardWorkingCopy board, SeparationSchedule schedule)
    {
        List<SeparationPlan> candidates = new List<SeparationPlan>();
        Dictionary<SeparationPlan, List<BoardMovement>> paths = new Dictionary<SeparationPlan, List<BoardMovement>>();
        foreach (SeparationPlan plan in schedule.plans)
        {
            if (plan.isDeferred) continue;
            candidates.Add(plan);
            // Bottom 표면 전체가 아니라 실제 Top Block의 이동/복원 구간만 경로에 포함합니다.
            List<BoardMovement> path = new List<BoardMovement>(plan.chainMovements);
            path.Add(new BoardMovement
            {
                entityId = plan.originBlockEntityId,
                from = board.State.entities[plan.mergedEntityId].cell,
                to = plan.originBlockCell
            });
            paths.Add(plan, path);
        }

        HashSet<SeparationPlan> conflicts = new HashSet<SeparationPlan>();
        for (int left = 0; left < candidates.Count; left++)
        {
            for (int right = left + 1; right < candidates.Count; right++)
            {
                SeparationPlan first = candidates[left];
                SeparationPlan second = candidates[right];
                if (!PathsConflict(first, second, paths[first], paths[second])) continue;
                conflicts.Add(first);
                conflicts.Add(second);
            }
        }

        // 중간에 Deferred로 바꾸어 A-B 충돌 뒤 B-C 검사가 누락되지 않도록 마지막에 반영합니다.
        foreach (SeparationPlan plan in conflicts)
        {
            Defer(plan, "Separation paths overlap or intersect in the same Snapshot.");
            plan.reservedCells.Clear();
            plan.reservedEntities.Clear();
        }
    }

    /// <summary>입력: 두 Plan과 임시 Top 경로. 출력: bool. 역할: 공유 Entity/Cell과 직선 이동 구간 교차를 판정합니다.</summary>
    private static bool PathsConflict(SeparationPlan first, SeparationPlan second,
        List<BoardMovement> firstPath, List<BoardMovement> secondPath)
    {
        if (first.reservedEntities.Overlaps(second.reservedEntities)
            || first.reservedCells.Overlaps(second.reservedCells)) return true;

        foreach (BoardMovement left in firstPath)
            foreach (BoardMovement right in secondPath)
                if (SegmentsIntersect(left, right)) return true;
        return false;
    }

    /// <summary>입력: 두 cardinal 또는 제자리 이동. 출력: bool. 역할: 끝점/공선 구간을 포함한 축 정렬 선분 교차를 정수 Cell로 판정합니다.</summary>
    private static bool SegmentsIntersect(BoardMovement first, BoardMovement second)
    {
        return System.Math.Max(System.Math.Min(first.from.x, first.to.x), System.Math.Min(second.from.x, second.to.x))
                <= System.Math.Min(System.Math.Max(first.from.x, first.to.x), System.Math.Max(second.from.x, second.to.x))
            && System.Math.Max(System.Math.Min(first.from.y, first.to.y), System.Math.Min(second.from.y, second.to.y))
                <= System.Math.Min(System.Math.Max(first.from.y, first.to.y), System.Math.Max(second.from.y, second.to.y));
    }

    private static bool IsDue(StageEntityState merged, long targetTurn)
    {
        if (merged.isJustCreated
            && (merged.createdTurnId < 0 || targetTurn <= merged.createdTurnId))
            return false;

        // Commit된 카운트가 0이면 절대 Turn 메타데이터가 어긋나 있더라도 분리 대상이다.
        // 단, 같은 Turn에 생성된 merged는 Merge 직후 즉시 분리하지 않는다.
        if (merged.remainingTurns <= 0)
            return true;

        if (merged.createdTurnId >= 0 && merged.separationDueTurn >= merged.createdTurnId)
            return targetTurn >= merged.separationDueTurn;
        return !merged.isJustCreated && merged.remainingTurns <= 1;
    }

}
