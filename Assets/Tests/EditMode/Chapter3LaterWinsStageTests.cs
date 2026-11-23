using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 3-2(가안) "나중이 이긴다" 배치를 순수 Turn 로직으로 검증합니다.
/// 배치(y 위가 큼):
///   y2:  #   bgKK0  #   #     #      #   #
///   y1:  K   K:B_b  K   K:BR_b bgKK0 b   b:F
///   y0:  K:P K      K   K     #      #   #
/// </summary>
public class Chapter3LaterWinsStageTests
{
    private const int MaxSearchStates = 20000;

    private GameObject mergedPrefab;
    private GameObject managerObject;
    private SeparationManager separationManager;

    [SetUp]
    public void SetUp()
    {
        mergedPrefab = new GameObject("MergedPrefabForLaterWinsTest");
        managerObject = new GameObject("SeparationManagerForLaterWinsTest");
        separationManager = managerObject.AddComponent<SeparationManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(mergedPrefab);
        Object.DestroyImmediate(managerObject);
    }

    /// <summary>의도한 해법: Remove(BR)를 먼저, Add(B)를 나중에 Merge하면 CC=Blue로 F에 도달합니다.</summary>
    [Test]
    public void IntendedSolution_RemoveFirstThenAdd_ClearsIn13Moves()
    {
        StageBoardSnapshot board = InitialBoard();

        board = Step(board, Vector3.right, out _);
        board = Step(board, Vector3.right, out _);
        board = Step(board, Vector3.up, out _);
        board = Step(board, Vector3.right, out TurnResult brPush);
        Assert.That(brPush.mergeBatch.mergePlans, Has.Count.EqualTo(1));
        Assert.That(brPush.mergeBatch.mergePlans[0].colorEffect, Is.EqualTo(MergeColorEffect.Remove));
        Assert.That(board.currentColor, Is.EqualTo("Black"), "Remove on Black has no visible effect.");

        board = Step(board, Vector3.down, out _);
        board = Step(board, Vector3.left, out _);
        board = Step(board, Vector3.left, out _);
        board = Step(board, Vector3.up, out TurnResult bPush);
        Assert.That(bPush.mergeBatch.mergePlans, Has.Count.EqualTo(1));
        Assert.That(bPush.mergeBatch.mergePlans[0].colorEffect, Is.EqualTo(MergeColorEffect.Add));
        Assert.That(board.currentColor, Is.EqualTo("Blue"), "Later Add batch must win over earlier Remove.");

        board = Step(board, Vector3.right, out _);
        board = Step(board, Vector3.right, out _);
        board = Step(board, Vector3.right, out _);
        Assert.That(board.entities["player"].cell, Is.EqualTo(new StageCell(4, 1)), "Player walks onto BRM(Blue).");
        board = Step(board, Vector3.right, out _);
        board = Step(board, Vector3.right, out TurnResult last);

        Assert.That(board.entities["player"].cell, Is.EqualTo(new StageCell(6, 1)));
        Assert.That(last.isStageClearCandidate, Is.True);
        Assert.That(last.isGameOverCandidate, Is.False);
        Assert.That(board.turnId, Is.EqualTo(13));
    }

    /// <summary>함정: Add를 먼저 하면 나중 Remove가 이겨 CC=Black이 되고 BRM(Blue)에 진입할 수 없습니다.</summary>
    [Test]
    public void Trap_AddFirstThenRemove_LeavesBlackAndBlocksBrm()
    {
        StageBoardSnapshot board = InitialBoard();

        board = Step(board, Vector3.right, out _);
        board = Step(board, Vector3.up, out _);
        Assert.That(board.currentColor, Is.EqualTo("Blue"));
        board = Step(board, Vector3.right, out _);
        board = Step(board, Vector3.right, out _);
        Assert.That(board.currentColor, Is.EqualTo("Black"), "Later Remove batch must win over earlier Add.");

        PlayerActionPlan blocked = PlayerActionResolver.Resolve(board, Schedule(board), Input(Vector3.right));

        Assert.That(blocked.outcome, Is.EqualTo(PlayerActionOutcome.Blocked));
        Assert.That(blocked.failureReason, Is.EqualTo(PlayerActionFailureReason.ColorBlocked));
        Assert.That(Search(board, out _), Is.False, "Add-first state must be a dead end (Undo only).");
    }

    /// <summary>전체 탐색: 최단 해법이 의도한 13수이며 더 짧은 우회 해법이 없습니다.</summary>
    [Test]
    public void Search_ShortestSolutionIsIntended13Moves()
    {
        bool solved = Search(InitialBoard(), out int shortest);

        Assert.That(solved, Is.True);
        Assert.That(shortest, Is.EqualTo(13));
    }

    private StageBoardSnapshot InitialBoard()
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot { turnId = 0, currentColor = "Black" };
        snapshot.entities.Add("player", new StageEntityState
        {
            entityId = "player", kind = StageEntityKind.Player, isActive = true, cell = new StageCell(0, 0),
            profile = Profile(StageZone.Top, MovementCapability.SelfMovable, OccupancyType.Solid)
        });

        for (int x = 0; x <= 3; x++) Ground(snapshot, new StageCell(x, 0), "Black");
        for (int x = 4; x <= 6; x++) Obstacle(snapshot, new StageCell(x, 0));
        for (int x = 0; x <= 3; x++) Ground(snapshot, new StageCell(x, 1), "Black");
        Ground(snapshot, new StageCell(5, 1), "Blue");
        Ground(snapshot, new StageCell(6, 1), "Blue");
        snapshot.staticCells[new StageCell(6, 1)].hasFinale = true;
        Obstacle(snapshot, new StageCell(0, 2));
        for (int x = 2; x <= 6; x++) Obstacle(snapshot, new StageCell(x, 2));

        Block(snapshot, "block-blue-add", new StageCell(1, 1), MergeColorEffect.Add);
        Block(snapshot, "block-blue-remove", new StageCell(3, 1), MergeColorEffect.Remove);
        Background(snapshot, "bg-top", new StageCell(1, 2));
        Background(snapshot, "bg-right", new StageCell(4, 1));

        TurnResult initial = StageTurnResolver.ResolveInitialMerges(snapshot);
        Assert.That(initial.isValid, Is.True, initial.invalidReason);
        Assert.That(initial.mergeBatch.mergePlans, Is.Empty);
        return initial.committedBoard;
    }

    private StageBoardSnapshot Step(StageBoardSnapshot board, Vector3 direction, out TurnResult result)
    {
        SeparationSchedule schedule = Schedule(board);
        PlayerActionPlan action = PlayerActionResolver.Resolve(board, schedule, Input(direction));
        Assert.That(action.outcome, Is.EqualTo(PlayerActionOutcome.Move), $"turn {board.turnId + 1} {direction}: {action.failureMessage}");
        result = StageTurnResolver.Resolve(board, schedule, action);
        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(result.isGameOverCandidate, Is.False);
        return result.committedBoard;
    }

    /// <summary>입력: 시작 Board. 출력: Clear 가능 여부와 최단 수. 역할: 실제 Turn 규칙으로 BFS합니다.</summary>
    private bool Search(StageBoardSnapshot start, out int shortest)
    {
        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right };
        Queue<KeyValuePair<StageBoardSnapshot, int>> queue = new Queue<KeyValuePair<StageBoardSnapshot, int>>();
        HashSet<string> visited = new HashSet<string> { Key(start) };
        queue.Enqueue(new KeyValuePair<StageBoardSnapshot, int>(start, 0));

        while (queue.Count > 0)
        {
            KeyValuePair<StageBoardSnapshot, int> current = queue.Dequeue();
            foreach (Vector3 direction in directions)
            {
                SeparationSchedule schedule = Schedule(current.Key);
                PlayerActionPlan action = PlayerActionResolver.Resolve(current.Key, schedule, Input(direction));
                if (action.outcome != PlayerActionOutcome.Move) continue;
                TurnResult result = StageTurnResolver.Resolve(current.Key, schedule, action);
                if (!result.isValid || result.isGameOverCandidate) continue;
                if (result.isStageClearCandidate) { shortest = current.Value + 1; return true; }
                if (!visited.Add(Key(result.committedBoard))) continue;
                Assert.That(visited.Count, Is.LessThan(MaxSearchStates), "Search exploded.");
                queue.Enqueue(new KeyValuePair<StageBoardSnapshot, int>(result.committedBoard, current.Value + 1));
            }
        }

        shortest = -1;
        return false;
    }

    private static string Key(StageBoardSnapshot board)
    {
        List<string> parts = new List<string>();
        foreach (StageEntityState entity in board.entities.Values)
        {
            if (!entity.isActive) continue;
            parts.Add($"{entity.kind}:{entity.colorName}:{entity.mergeColorEffect}:{entity.mergeBatchId}:{entity.cell.x},{entity.cell.y}");
        }
        parts.Sort(System.StringComparer.Ordinal);
        return board.currentColor + "|" + string.Join("|", parts);
    }

    private SeparationSchedule Schedule(StageBoardSnapshot board) => separationManager.CreateSchedule(board, board.turnId + 1);

    private void Block(StageBoardSnapshot snapshot, string id, StageCell cell, MergeColorEffect effect)
    {
        snapshot.entities.Add(id, new StageEntityState
        {
            entityId = id, kind = StageEntityKind.Block, isActive = true, cell = cell,
            colorName = "Blue", mergeColorEffect = effect, mergedPrefab = mergedPrefab,
            profile = Profile(StageZone.Top, MovementCapability.Pushable, OccupancyType.Solid)
        });
    }

    private static void Background(StageBoardSnapshot snapshot, string id, StageCell cell)
    {
        snapshot.entities.Add(id, new StageEntityState
        {
            entityId = id, kind = StageEntityKind.BlockBackground, isActive = true, cell = cell,
            colorName = "Black", acceptedColorName = "Black", remainingTurns = 0,
            profile = Profile(StageZone.Bottom, MovementCapability.Static, OccupancyType.Surface)
        });
    }

    private static TurnInput Input(Vector3 direction) => new TurnInput { actorEntityId = "player", direction = direction };
    private static StageObjectProfile Profile(StageZone zone, MovementCapability movement, OccupancyType occupancy) => new StageObjectProfile { zone = zone, movement = movement, occupancy = occupancy };
    private static void Ground(StageBoardSnapshot snapshot, StageCell cell, string color) => snapshot.staticCells.Add(cell, new StageCellStaticState { bottomGroundKind = StageBottomGroundKind.Ground, colorName = color });
    private static void Obstacle(StageBoardSnapshot snapshot, StageCell cell) => snapshot.staticCells.Add(cell, new StageCellStaticState { hasObstacle = true });
}
