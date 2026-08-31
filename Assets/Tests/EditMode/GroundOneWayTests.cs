using NUnit.Framework;
using UnityEngine;

/// <summary>
/// GroundOneWay 규칙이 Physics·Coroutine 없이 StageBoard 계획 계산에 적용되는지 검증합니다.
/// </summary>
public class GroundOneWayTests
{
    /// <summary>입력: 없음. 출력: 없음. 역할: 순수 색상 규칙이 GroundOneWay 진입 색상 포함 여부를 판정하는지 검증합니다.</summary>
    [Test]
    public void ColorRule_RequiresMatchingCurrentColor()
    {
        Assert.That(StageColorRules.TryIsAvailable("Black", "Blue", false, out bool unavailable, out _), Is.True);
        Assert.That(unavailable, Is.False);
        Assert.That(StageColorRules.TryIsAvailable("Blue", "Blue", false, out bool available, out _), Is.True);
        Assert.That(available, Is.True);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Pushable chain의 GO 이탈 방향 불일치가 전체 계획을 실패시키는지 검증합니다.</summary>
    [Test]
    public void PushPlan_FailsWhenSourceGroundOneWayExitIsBlocked()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        snapshot.entities.Add("block", Pushable("block", new StageCell(0, 0)));
        snapshot.staticCells[new StageCell(0, 0)] = GroundOneWayCell(GroundOneWay.ExitDirection.W);
        snapshot.staticCells.Add(new StageCell(1, 0), GroundCell());

        bool success = BlockManager.TryCreatePushPlanPure(
            StageBoard.FromSnapshot(snapshot).CreateWorkingCopy(),
            new StageCell(0, 0),
            new StageCell(1, 0),
            "player",
            out PushPlan plan,
            out _);

        Assert.That(success, Is.False);
        Assert.That(plan.movements, Is.Empty);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: due Separation이 GO 이탈 제한에 막히면 Deferred이며 예약을 남기지 않는지 검증합니다.</summary>
    [Test]
    public void CreateSchedule_DefersWhenChainCannotExitGroundOneWay()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        snapshot.entities.Add("merged", Merged("merged", new StageCell(0, 0), new StageCell(1, 0)));
        snapshot.entities.Add("origin-block", InactivePushable("origin-block", new StageCell(1, 0)));
        snapshot.entities.Add("origin-background", InactiveBackground("origin-background", new StageCell(0, 0)));
        snapshot.entities.Add("blocker", Pushable("blocker", new StageCell(1, 0)));
        snapshot.staticCells.Add(new StageCell(1, 0), GroundOneWayCell(GroundOneWay.ExitDirection.W));
        snapshot.staticCells.Add(new StageCell(2, 0), GroundCell());

        GameObject managerObject = new GameObject("SeparationManager");
        SeparationManager manager = managerObject.AddComponent<SeparationManager>();
        SeparationSchedule schedule = manager.CreateSchedule(snapshot, 1);

        Assert.That(schedule.isValid, Is.True);
        Assert.That(schedule.plans, Has.Count.EqualTo(1));
        Assert.That(schedule.plans[0].isDeferred, Is.True);
        Assert.That(schedule.reservedCells, Is.Empty);
        Assert.That(snapshot.entities["merged"].isActive, Is.True);
        Object.DestroyImmediate(managerObject);
    }

    /// <summary>확정된 remainingTurns가 0이면 절대 Turn 메타데이터가 달라도 Separation을 예약하는지 검증합니다.</summary>
    [Test]
    public void CreateSchedule_SchedulesWhenCommittedRemainingTurnsReachZero()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        StageEntityState merged = Merged("merged", new StageCell(0, 0), new StageCell(1, 0));
        merged.remainingTurns = 0;
        merged.isJustCreated = false;
        merged.separationDueTurn = 99;
        snapshot.entities.Add("merged", merged);
        snapshot.entities.Add("origin-block", InactivePushable("origin-block", new StageCell(1, 0)));
        snapshot.entities.Add("origin-background", InactiveBackground("origin-background", new StageCell(0, 0)));

        GameObject managerObject = new GameObject("SeparationManager");
        SeparationManager manager = managerObject.AddComponent<SeparationManager>();
        SeparationSchedule schedule = manager.CreateSchedule(snapshot, 1);

        Assert.That(schedule.isValid, Is.True);
        Assert.That(schedule.plans, Has.Count.EqualTo(1));
        Assert.That(schedule.plans[0].isDeferred, Is.False);
        Object.DestroyImmediate(managerObject);
    }

    /// <summary>생성 직후 merged는 remainingTurns가 0이어도 같은 Turn에 즉시 분리하지 않는지 검증합니다.</summary>
    [Test]
    public void CreateSchedule_DoesNotSeparateJustCreatedMerged()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        StageEntityState merged = Merged("merged", new StageCell(0, 0), new StageCell(1, 0));
        merged.remainingTurns = 0;
        merged.isJustCreated = true;
        merged.createdTurnId = 1;
        merged.separationDueTurn = 1;
        snapshot.entities.Add("merged", merged);
        snapshot.entities.Add("origin-block", InactivePushable("origin-block", new StageCell(1, 0)));
        snapshot.entities.Add("origin-background", InactiveBackground("origin-background", new StageCell(0, 0)));

        GameObject managerObject = new GameObject("SeparationManager");
        SeparationManager manager = managerObject.AddComponent<SeparationManager>();
        SeparationSchedule schedule = manager.CreateSchedule(snapshot, 1);

        Assert.That(schedule.isValid, Is.True);
        Assert.That(schedule.plans, Is.Empty);
        Object.DestroyImmediate(managerObject);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Separation owner는 자신의 예약만 통과하고 Player owner는 차단되는지 검증합니다.</summary>
    [Test]
    public void Reservations_AllowOnlySeparationOwner()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        snapshot.entities.Add("block", Pushable("block", new StageCell(0, 0)));
        snapshot.staticCells.Add(new StageCell(0, 0), GroundCell());
        snapshot.staticCells.Add(new StageCell(1, 0), GroundCell());
        StageBoardWorkingCopy board = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();
        board.ApplyReservations(new[] { new StageCell(1, 0) }, new[] { "block" }, "separation:merged");

        Assert.That(BlockManager.TryCreatePushPlanPure(board, new StageCell(0, 0), new StageCell(1, 0), "player", out _, out _), Is.False);
        Assert.That(BlockManager.TryCreatePushPlanPure(board, new StageCell(0, 0), new StageCell(1, 0), "separation:merged", out PushPlan plan, out _), Is.True);
        Assert.That(plan.movements, Has.Count.EqualTo(1));
    }

    private static StageBoardSnapshot BaseBoard()
    {
        return new StageBoardSnapshot { currentColor = "White" };
    }

    private static StageEntityState Pushable(string id, StageCell cell)
    {
        return new StageEntityState
        {
            entityId = id,
            kind = StageEntityKind.Block,
            cell = cell,
            isActive = true,
            profile = new StageObjectProfile { zone = StageZone.Top, movement = MovementCapability.Pushable, occupancy = OccupancyType.Solid }
        };
    }

    private static StageEntityState InactivePushable(string id, StageCell cell)
    {
        StageEntityState state = Pushable(id, cell);
        state.isActive = false;
        return state;
    }

    private static StageEntityState InactiveBackground(string id, StageCell cell)
    {
        return new StageEntityState
        {
            entityId = id,
            kind = StageEntityKind.BlockBackground,
            cell = cell,
            isActive = false,
            profile = new StageObjectProfile { zone = StageZone.Bottom, movement = MovementCapability.Static, occupancy = OccupancyType.Surface }
        };
    }

    private static StageEntityState Merged(string id, StageCell cell, StageCell origin)
    {
        return new StageEntityState
        {
            entityId = id,
            kind = StageEntityKind.BlockMerged,
            cell = cell,
            isActive = true,
            remainingTurns = 1,
            createdTurnId = 0,
            separationDueTurn = 1,
            originBlockCell = origin,
            originBlockEntityId = "origin-block",
            originBackgroundEntityId = "origin-background",
            profile = new StageObjectProfile { zone = StageZone.Bottom, movement = MovementCapability.Static, occupancy = OccupancyType.Surface }
        };
    }

    private static StageCellStaticState GroundCell()
    {
        return new StageCellStaticState { bottomGroundKind = StageBottomGroundKind.Ground, colorName = "White" };
    }

    private static StageCellStaticState GroundOneWayCell(GroundOneWay.ExitDirection direction)
    {
        return new StageCellStaticState { bottomGroundKind = StageBottomGroundKind.GroundOneWay, colorName = "White", exitDirection = direction };
    }
}
