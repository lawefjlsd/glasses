using NUnit.Framework;
using UnityEngine;

public class StageTurnResolverTests
{
    private GameObject mergedPrefab;

    [SetUp]
    public void SetUp() => mergedPrefab = new GameObject("MergedPrefabForPureTest");

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(mergedPrefab);

    [Test]
    public void Resolve_CommitsDirectPlayerMovementWithoutMutatingSnapshot()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, new SeparationSchedule(), InputRight());

        TurnResult result = StageTurnResolver.Resolve(snapshot, new SeparationSchedule(), action);

        Assert.That(result.isValid, Is.True);
        Assert.That(result.committedBoard.turnId, Is.EqualTo(1));
        Assert.That(result.committedBoard.entities["player"].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(snapshot.entities["player"].cell, Is.EqualTo(new StageCell(0, 0)));
    }

    [Test]
    public void Resolve_SeparationAtomicallyReplacesMergedWithBlockAndBackground()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddSeparationEntities(snapshot, false);
        SeparationSchedule schedule = SeparationAt(new StageCell(2, 0));
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());

        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(result.isValid, Is.True);
        Assert.That(result.committedBoard.entities["merged"].isActive, Is.False);
        Assert.That(result.committedBoard.entities["origin-block"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["origin-block"].cell, Is.EqualTo(new StageCell(2, 0)));
        Assert.That(result.committedBoard.entities["origin-background"].isActive, Is.True);
        Assert.That(StageBoard.FromSnapshot(result.committedBoard).CreateWorkingCopy().ValidateBoard().isValid, Is.True);
    }

    [Test]
    public void Resolve_SeparationSolidOverlapBecomesGameOverCandidate()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddSeparationEntities(snapshot, true);
        SeparationSchedule schedule = SeparationAt(new StageCell(2, 0));
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());

        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(result.isValid, Is.True);
        Assert.That(result.isGameOverCandidate, Is.True);
        Assert.That(result.mergeBatch.mergePlans, Is.Empty);
    }

    [Test]
    public void Resolve_MergeReplacesBlockAndBackgroundWithSingleBottom()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddMergePair(snapshot, "block", "background", new StageCell(3, 0), "Red", "Red", MergeColorEffect.Add);
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, new SeparationSchedule(), InputRight());

        TurnResult result = StageTurnResolver.Resolve(snapshot, new SeparationSchedule(), action);

        Assert.That(result.isValid, Is.True);
        Assert.That(result.mergeBatch.mergePlans.Count, Is.EqualTo(1));
        MergePlan merge = result.mergeBatch.mergePlans[0];
        Assert.That(result.committedBoard.entities["block"].isActive, Is.False);
        Assert.That(result.committedBoard.entities["background"].isActive, Is.False);
        Assert.That(result.committedBoard.entities[merge.mergedEntityId].isActive, Is.True);
        Assert.That(result.committedBoard.currentColor, Is.EqualTo("Red"));
        Assert.That(StageBoard.FromSnapshot(result.committedBoard).CreateWorkingCopy().QueryCell(new StageCell(3, 0)).bottomEntityIds.Count, Is.EqualTo(1));
    }

    [Test]
    public void ResolveInitialMerges_MergesEligiblePairOnceWithoutAdvancingTurn()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddMergePair(snapshot, "initial-block", "initial-background", new StageCell(3, 0), "Red", "Red", MergeColorEffect.Add);

        TurnResult result = StageTurnResolver.ResolveInitialMerges(snapshot);

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(result.committedBoard.turnId, Is.Zero);
        Assert.That(result.mergeBatch.candidateCount, Is.EqualTo(1));
        Assert.That(result.mergeBatch.mergePlans, Has.Count.EqualTo(1));
        Assert.That(result.committedBoard.entities["initial-block"].isActive, Is.False);
        Assert.That(result.committedBoard.entities["initial-background"].isActive, Is.False);
        StageEntityState merged = result.committedBoard.entities[result.mergeBatch.mergePlans[0].mergedEntityId];
        Assert.That(merged.isActive, Is.True);
        Assert.That(merged.remainingTurns, Is.EqualTo(2));
        Assert.That(merged.createdTurnId, Is.Zero);
        Assert.That(merged.separationDueTurn, Is.EqualTo(2));
        Assert.That(merged.isJustCreated, Is.True);
        Assert.That(merged.originBlockEntityId, Is.EqualTo("initial-block"));
        Assert.That(merged.originBackgroundEntityId, Is.EqualTo("initial-background"));
        Assert.That(result.committedBoard.currentColor, Is.EqualTo("Red"));
        Assert.That(snapshot.entities["initial-block"].isActive, Is.True);
    }

    [Test]
    public void ResolveInitialMerges_SecondPassDoesNotDuplicateMergedEntity()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddMergePair(snapshot, "initial-block", "initial-background", new StageCell(3, 0), "Red", "Red", MergeColorEffect.Add);
        TurnResult first = StageTurnResolver.ResolveInitialMerges(snapshot);

        TurnResult second = StageTurnResolver.ResolveInitialMerges(first.committedBoard);

        Assert.That(first.isValid, Is.True, first.invalidReason);
        Assert.That(second.isValid, Is.True, second.invalidReason);
        Assert.That(second.mergeBatch.candidateCount, Is.Zero);
        Assert.That(second.mergeBatch.mergePlans, Is.Empty);
        Assert.That(second.committedBoard.entities.Count, Is.EqualTo(first.committedBoard.entities.Count));
        Assert.That(second.committedBoard.currentColor, Is.EqualTo("Red"));
    }

    [Test]
    public void ResolveInitialMerges_IneligiblePairIsCheckedWithoutStateChange()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddMergePair(snapshot, "initial-block", "initial-background", new StageCell(3, 0), "Red", "Blue", MergeColorEffect.Add);

        TurnResult result = StageTurnResolver.ResolveInitialMerges(snapshot);

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(result.mergeBatch.candidateCount, Is.Zero);
        Assert.That(result.mergeBatch.mergePlans, Is.Empty);
        Assert.That(result.committedBoard.entities["initial-block"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["initial-background"].isActive, Is.True);
        Assert.That(result.committedBoard.currentColor, Is.EqualTo("Black"));
    }

    [Test]
    public void ResolveInitialMerges_DoesNotPreventLaterNormalTurnMerge()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddMergePair(snapshot, "initial-block", "initial-background", new StageCell(3, 0), "Red", "Red", MergeColorEffect.Add);
        TurnResult initial = StageTurnResolver.ResolveInitialMerges(snapshot);
        StageBoardSnapshot nextTurn = initial.committedBoard.Clone();
        nextTurn.staticCells[new StageCell(1, 0)].colorName = "Red";
        AddMergePair(nextTurn, "later-block", "later-background", new StageCell(4, 0), "Blue", "Blue", MergeColorEffect.Add);

        PlayerActionPlan action = PlayerActionResolver.Resolve(nextTurn, new SeparationSchedule(), InputRight());
        TurnResult normal = StageTurnResolver.Resolve(nextTurn, new SeparationSchedule(), action);

        Assert.That(action.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(normal.isValid, Is.True, normal.invalidReason);
        Assert.That(normal.mergeBatch.candidateCount, Is.EqualTo(1));
        Assert.That(normal.mergeBatch.mergePlans, Has.Count.EqualTo(1));
        Assert.That(normal.committedBoard.currentColor, Is.EqualTo("Magenta"));
    }

    [Test]
    public void ResolveInitialMerges_NewMergedIsNotDueDuringInitializationTurn()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddMergePair(snapshot, "initial-block", "initial-background", new StageCell(3, 0), "Red", "Red", MergeColorEffect.Add);
        snapshot.entities["initial-background"].remainingTurns = 1;
        TurnResult initial = StageTurnResolver.ResolveInitialMerges(snapshot);
        GameObject managerObject = new GameObject("SeparationManagerForInitialMergeTest");

        try
        {
            SeparationSchedule schedule = managerObject.AddComponent<SeparationManager>().CreateSchedule(initial.committedBoard, initial.committedBoard.turnId);
            Assert.That(initial.isValid, Is.True, initial.invalidReason);
            Assert.That(schedule.isValid, Is.True, schedule.invalidReason);
            Assert.That(schedule.plans, Is.Empty);
            StageEntityState merged = initial.committedBoard.entities[initial.mergeBatch.mergePlans[0].mergedEntityId];
            Assert.That(merged.isJustCreated, Is.True);
            Assert.That(merged.createdTurnId, Is.Zero);
            Assert.That(merged.separationDueTurn, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void InitialMergeCheckGuard_AllowsExactlyOneCheck()
    {
        InitialMergeCheckGuard guard = new InitialMergeCheckGuard();

        Assert.That(guard.TryBegin(), Is.True);
        Assert.That(guard.TryBegin(), Is.False);
        Assert.That(guard.Count, Is.EqualTo(1));
    }

    [Test]
    public void InitialMergeResultCanBeStoredAsFirstTurnUndoBaseline()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddMergePair(snapshot, "initial-block", "initial-background", new StageCell(3, 0), "Red", "Red", MergeColorEffect.Add);
        TurnResult initial = StageTurnResolver.ResolveInitialMerges(snapshot);
        GameObject undoObject = new GameObject("UndoManagerForInitialMergeTest");

        try
        {
            UndoManager undo = undoObject.AddComponent<UndoManager>();
            Assert.That(initial.isValid, Is.True, initial.invalidReason);
            Assert.That(undo.BeginTurnSnapshot(initial.committedBoard), Is.True);
            Assert.That(undo.ConfirmTurnSnapshot(), Is.True);
            Assert.That(undo.TryBeginUndoSnapshot(out StageBoardSnapshot restored), Is.True);
            Assert.That(restored.currentColor, Is.EqualTo("Red"));
            Assert.That(restored.turnId, Is.Zero);
            Assert.That(restored.entities["initial-block"].isActive, Is.False);
            Assert.That(restored.entities["initial-background"].isActive, Is.False);
            Assert.That(restored.entities[initial.mergeBatch.mergePlans[0].mergedEntityId].isActive, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(undoObject);
        }
    }

    [Test]
    public void Resolve_SimultaneousRemoveIsAppliedAfterAdd()
    {
        StageBoardSnapshot snapshot = BaseBoard();
        AddMergePair(snapshot, "add-block", "add-background", new StageCell(3, 0), "Yellow", "Yellow", MergeColorEffect.Add);
        AddMergePair(snapshot, "remove-block", "remove-background", new StageCell(4, 0), "Red", "Red", MergeColorEffect.Remove);
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, new SeparationSchedule(), InputRight());

        TurnResult result = StageTurnResolver.Resolve(snapshot, new SeparationSchedule(), action);

        Assert.That(result.isValid, Is.True);
        Assert.That(result.mergeBatch.mergePlans.Count, Is.EqualTo(2));
        Assert.That(result.mergeBatch.mergePlans[0].colorEffect, Is.EqualTo(MergeColorEffect.Add));
        Assert.That(result.mergeBatch.mergePlans[1].colorEffect, Is.EqualTo(MergeColorEffect.Remove));
        Assert.That(result.committedBoard.currentColor, Is.EqualTo("Green"));
    }

    [Test]
    public void StageColorRules_RemoveYellowClearsRedAndGreen()
    {
        Assert.That(StageColorRules.TryApplyMergeEffect("White", "Yellow", MergeColorEffect.Remove, out string color, out _), Is.True);
        Assert.That(color, Is.EqualTo("Blue"));
    }

    [Test]
    public void Resolve_PlayerVacatesDueSeparationOriginAndSeparatesInSameTurn()
    {
        StageBoardSnapshot snapshot = PlayerVacateBoard();
        SeparationSchedule schedule = CreateSchedule(snapshot);

        Assert.That(schedule.plans, Has.Count.EqualTo(1));
        Assert.That(schedule.plans[0].isDeferred, Is.False);
        Assert.That(schedule.plans[0].chainMovements, Is.Empty);
        Assert.That(schedule.plans[0].reservedEntities, Does.Not.Contain("player"));

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(action.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(result.isValid, Is.True);
        Assert.That(result.committedBoard.entities["player"].cell, Is.EqualTo(new StageCell(2, 0)));
        Assert.That(result.committedBoard.entities["merged"].isActive, Is.False);
        Assert.That(result.committedBoard.entities["origin-block"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["origin-block"].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(result.committedBoard.entities["origin-background"].isActive, Is.True);
        Assert.That(snapshot.entities["merged"].remainingTurns, Is.Zero);
    }

    [Test]
    public void CreateSchedule_TargetTurnIncludesRemainingOneDueAtSeven()
    {
        StageBoardSnapshot snapshot = PlayerVacateBoard();
        snapshot.turnId = 6;
        snapshot.entities["merged"].remainingTurns = 1;
        snapshot.entities["merged"].createdTurnId = 5;
        snapshot.entities["merged"].separationDueTurn = 7;

        SeparationSchedule schedule = CreateSchedule(snapshot);

        Assert.That(schedule.isValid, Is.True, schedule.invalidReason);
        Assert.That(schedule.plans, Has.Count.EqualTo(1));
        Assert.That(schedule.plans[0].isDeferred, Is.False, schedule.plans[0].deferReason);
    }

    [Test]
    public void Resolve_TargetTurnBoundaryVacateAppliesSeparationInSameInputTurn()
    {
        StageBoardSnapshot snapshot = PlayerVacateBoard();
        snapshot.turnId = 6;
        snapshot.entities["merged"].remainingTurns = 1;
        snapshot.entities["merged"].createdTurnId = 5;
        snapshot.entities["merged"].separationDueTurn = 7;
        SeparationSchedule schedule = CreateSchedule(snapshot);

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(action.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(result.committedBoard.turnId, Is.EqualTo(7));
        Assert.That(result.committedBoard.entities["merged"].isActive, Is.False);
        Assert.That(result.committedBoard.entities["origin-block"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["origin-background"].isActive, Is.True);
    }

    [Test]
    public void CreateSchedule_DoesNotSeparateMergedCreatedOnTargetTurn()
    {
        StageBoardSnapshot snapshot = PlayerVacateBoard();
        snapshot.turnId = 6;
        snapshot.entities["merged"].remainingTurns = 0;
        snapshot.entities["merged"].isJustCreated = true;
        snapshot.entities["merged"].createdTurnId = 7;
        snapshot.entities["merged"].separationDueTurn = 7;

        SeparationSchedule schedule = CreateSchedule(snapshot);

        Assert.That(schedule.isValid, Is.True, schedule.invalidReason);
        Assert.That(schedule.plans, Is.Empty);
    }

    [Test]
    public void PlayerVacateBlockedInputDoesNotCommitOrChangeDueTimer()
    {
        StageBoardSnapshot snapshot = PlayerVacateBoard();
        snapshot.staticCells.Remove(new StageCell(2, 0));
        snapshot.staticCells.Add(new StageCell(2, 0), new StageCellStaticState { hasObstacle = true });
        SeparationSchedule schedule = CreateSchedule(snapshot);

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());

        Assert.That(action.outcome, Is.EqualTo(PlayerActionOutcome.Blocked));
        Assert.That(action.failureReason, Is.EqualTo(PlayerActionFailureReason.Obstacle));
        Assert.That(snapshot.turnId, Is.EqualTo(5));
        Assert.That(snapshot.entities["player"].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(snapshot.entities["merged"].remainingTurns, Is.Zero);
    }

    [Test]
    public void PlayerCannotEnterReservedSeparationOrigin()
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot { turnId = 5, currentColor = "Red" };
        AddPlayer(snapshot, new StageCell(0, 0));
        AddGround(snapshot, new StageCell(0, 0), "White");
        AddGround(snapshot, new StageCell(1, 0), "White");
        AddDueSeparation(snapshot, "first", new StageCell(2, 0), new StageCell(1, 0), 1);
        SeparationSchedule schedule = CreateSchedule(snapshot);

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());

        Assert.That(action.outcome, Is.EqualTo(PlayerActionOutcome.Blocked));
        Assert.That(action.failureReason, Is.EqualTo(PlayerActionFailureReason.DestinationReserved));
        Assert.That(snapshot.entities["first-merged"].remainingTurns, Is.Zero);
    }

    [Test]
    public void Resolve_DueSeparationPreservesPushableChain()
    {
        StageBoardSnapshot snapshot = RemotePlayerBoard();
        AddDueSeparation(snapshot, "first", new StageCell(0, 0), new StageCell(1, 0), 1);
        snapshot.entities.Add("chain-block", Pushable("chain-block", new StageCell(1, 0)));
        AddGround(snapshot, new StageCell(1, 0), "White");
        AddGround(snapshot, new StageCell(2, 0), "White");
        SeparationSchedule schedule = CreateSchedule(snapshot);

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(schedule.plans[0].chainMovements, Has.Count.EqualTo(1));
        Assert.That(result.isValid, Is.True);
        Assert.That(result.committedBoard.entities["chain-block"].cell, Is.EqualTo(new StageCell(2, 0)));
        Assert.That(result.committedBoard.entities["first-merged"].isActive, Is.False);
        Assert.That(result.committedBoard.entities["first-merged"].remainingTurns, Is.Zero);
        Assert.That(result.committedBoard.entities["first-origin-block"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["first-origin-block"].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(result.committedBoard.entities["first-origin-background"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["first-origin-background"].cell, Is.EqualTo(new StageCell(0, 0)));
        Assert.That(result.committedBoard.currentColor, Is.EqualTo("Black"));
    }

    [Test]
    public void Resolve_MultipleExecutableSeparationsRefreshEachPlansCurrentEntities()
    {
        StageBoardSnapshot snapshot = RemotePlayerBoard();
        AddDueSeparation(snapshot, "first", new StageCell(0, 0), new StageCell(1, 0), 1);
        AddDueSeparation(snapshot, "second", new StageCell(3, 0), new StageCell(4, 0), 2);
        AddGround(snapshot, new StageCell(1, 0), "White");
        AddGround(snapshot, new StageCell(4, 0), "White");
        SeparationSchedule schedule = CreateSchedule(snapshot);

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(schedule.plans, Has.Count.EqualTo(2));
        Assert.That(schedule.plans[0].isDeferred, Is.False);
        Assert.That(schedule.plans[1].isDeferred, Is.False);
        Assert.That(result.isValid, Is.True, result.invalidReason);
        AssertSeparated(result.committedBoard, "first", new StageCell(1, 0), new StageCell(0, 0));
        AssertSeparated(result.committedBoard, "second", new StageCell(4, 0), new StageCell(3, 0));
    }

    [Test]
    public void Resolve_ReverseMergedWithChainSeparatesInSameTurn()
    {
        StageBoardSnapshot snapshot = RemotePlayerBoard();
        AddDueSeparation(snapshot, "reverse", new StageCell(0, 0), new StageCell(1, 0), 1);
        snapshot.entities["reverse-merged"].mergeColorEffect = MergeColorEffect.Remove;
        snapshot.entities.Add("reverse-chain", Pushable("reverse-chain", new StageCell(1, 0)));
        AddGround(snapshot, new StageCell(1, 0), "White");
        AddGround(snapshot, new StageCell(2, 0), "White");
        SeparationSchedule schedule = CreateSchedule(snapshot);

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(result.committedBoard.entities["reverse-chain"].cell, Is.EqualTo(new StageCell(2, 0)));
        AssertSeparated(result.committedBoard, "reverse", new StageCell(1, 0), new StageCell(0, 0));
    }

    [Test]
    public void Resolve_GroundOneWayRevalidationDefersWithoutIncreasingRemainingTurns()
    {
        StageBoardSnapshot snapshot = RemotePlayerBoard();
        AddDueSeparation(snapshot, "first", new StageCell(0, 0), new StageCell(1, 0), 1);
        snapshot.entities.Add("chain-block", Pushable("chain-block", new StageCell(1, 0)));
        AddGround(snapshot, new StageCell(1, 0), "White");
        AddGround(snapshot, new StageCell(2, 0), "White");
        SeparationSchedule schedule = CreateSchedule(snapshot);
        snapshot.staticCells[new StageCell(1, 0)] = new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.GroundOneWay,
            colorName = "White",
            exitDirection = GroundOneWay.ExitDirection.W
        };

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(result.isValid, Is.True);
        Assert.That(schedule.plans[0].isDeferred, Is.True);
        Assert.That(schedule.plans[0].deferReason, Does.Contain("GroundOneWay"));
        Assert.That(result.committedBoard.entities["first-merged"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["first-merged"].remainingTurns, Is.Zero);
        Assert.That(result.committedBoard.entities["first-origin-block"].isActive, Is.False);
        Assert.That(result.committedBoard.entities["first-origin-background"].isActive, Is.False);
        Assert.That(result.committedBoard.entities["chain-block"].cell, Is.EqualTo(new StageCell(1, 0)));
    }

    [Test]
    public void PlayerVacateObeysGroundOneWayExitDirection()
    {
        StageBoardSnapshot allowedSnapshot = PlayerVacateBoard();
        allowedSnapshot.staticCells[new StageCell(1, 0)] = new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.GroundOneWay,
            colorName = "White",
            exitDirection = GroundOneWay.ExitDirection.D
        };
        SeparationSchedule allowedSchedule = CreateSchedule(allowedSnapshot);
        PlayerActionPlan allowed = PlayerActionResolver.Resolve(allowedSnapshot, allowedSchedule, InputRight());

        StageBoardSnapshot blockedSnapshot = PlayerVacateBoard();
        blockedSnapshot.staticCells[new StageCell(1, 0)] = new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.GroundOneWay,
            colorName = "White",
            exitDirection = GroundOneWay.ExitDirection.W
        };
        SeparationSchedule blockedSchedule = CreateSchedule(blockedSnapshot);
        PlayerActionPlan blocked = PlayerActionResolver.Resolve(blockedSnapshot, blockedSchedule, InputRight());

        Assert.That(allowed.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(blocked.outcome, Is.EqualTo(PlayerActionOutcome.Blocked));
        Assert.That(blocked.failureReason, Is.EqualTo(PlayerActionFailureReason.ExitDirectionBlocked));
    }

    [Test]
    public void UndoSnapshotRestoresCompleteStateAfterPlayerVacateSeparation()
    {
        StageBoardSnapshot snapshot = PlayerVacateBoard();
        SeparationSchedule schedule = CreateSchedule(snapshot);
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);
        GameObject undoObject = new GameObject("UndoManagerForSeparationTest");
        UndoManager undo = undoObject.AddComponent<UndoManager>();

        Assert.That(result.isValid, Is.True);
        Assert.That(undo.BeginTurnSnapshot(snapshot), Is.True);
        Assert.That(undo.ConfirmTurnSnapshot(), Is.True);
        Assert.That(undo.TryBeginUndoSnapshot(out StageBoardSnapshot restored), Is.True);
        Assert.That(restored.entities["player"].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(restored.entities["merged"].isActive, Is.True);
        Assert.That(restored.entities["merged"].remainingTurns, Is.Zero);
        Assert.That(restored.entities["origin-block"].isActive, Is.False);
        Assert.That(restored.entities["origin-background"].isActive, Is.False);
        Assert.That(restored.currentColor, Is.EqualTo("Red"));
        Object.DestroyImmediate(undoObject);
    }

    [Test]
    public void UndoSnapshotRestoresChainPositionAndSeparationStates()
    {
        StageBoardSnapshot snapshot = RemotePlayerBoard();
        AddDueSeparation(snapshot, "first", new StageCell(0, 0), new StageCell(1, 0), 1);
        snapshot.entities.Add("chain-block", Pushable("chain-block", new StageCell(1, 0)));
        AddGround(snapshot, new StageCell(1, 0), "White");
        AddGround(snapshot, new StageCell(2, 0), "White");
        SeparationSchedule schedule = CreateSchedule(snapshot);
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);
        GameObject undoObject = new GameObject("UndoManagerForChainSeparationTest");

        try
        {
            UndoManager undo = undoObject.AddComponent<UndoManager>();
            Assert.That(result.isValid, Is.True, result.invalidReason);
            Assert.That(result.committedBoard.entities["chain-block"].cell, Is.EqualTo(new StageCell(2, 0)));
            Assert.That(undo.BeginTurnSnapshot(snapshot), Is.True);
            Assert.That(undo.ConfirmTurnSnapshot(), Is.True);
            Assert.That(undo.TryBeginUndoSnapshot(out StageBoardSnapshot restored), Is.True);
            Assert.That(restored.entities["first-merged"].isActive, Is.True);
            Assert.That(restored.entities["first-merged"].remainingTurns, Is.Zero);
            Assert.That(restored.entities["first-origin-block"].isActive, Is.False);
            Assert.That(restored.entities["first-origin-background"].isActive, Is.False);
            Assert.That(restored.entities["chain-block"].cell, Is.EqualTo(new StageCell(1, 0)));
            Assert.That(restored.currentColor, Is.EqualTo("Red"));
        }
        finally
        {
            Object.DestroyImmediate(undoObject);
        }
    }

    [Test]
    public void Resolve_ConflictingSeparationsBothDeferWithoutChangingTimers()
    {
        StageBoardSnapshot snapshot = RemotePlayerBoard();
        AddDueSeparation(snapshot, "first", new StageCell(0, 0), new StageCell(1, 0), 1);
        AddDueSeparation(snapshot, "second", new StageCell(2, 0), new StageCell(1, 0), 2);
        AddGround(snapshot, new StageCell(1, 0), "White");
        SeparationSchedule schedule = CreateSchedule(snapshot);

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(schedule.plans, Has.Count.EqualTo(2));
        Assert.That(schedule.plans[0].mergedEntityId, Is.EqualTo("first-merged"));
        Assert.That(schedule.plans[0].isDeferred, Is.True);
        Assert.That(schedule.plans[1].mergedEntityId, Is.EqualTo("second-merged"));
        Assert.That(schedule.plans[1].isDeferred, Is.True);
        Assert.That(result.isValid, Is.True);
        Assert.That(result.committedBoard.entities["first-merged"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["second-merged"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["second-merged"].remainingTurns, Is.Zero);
        Assert.That(result.committedBoard.entities["first-merged"].remainingTurns, Is.Zero);
        Assert.That(schedule.reservedCells, Is.Empty);
        Assert.That(schedule.reservedEntities, Is.Empty);
    }

    /// <summary>입력: Chain 길이와 Merge 효과. 출력: 없음. 역할: Player에서 Chain을 끝내고 동일 due Turn에 이동과 원본 복원을 모두 적용하는지 검증합니다.</summary>
    [TestCase(0, MergeColorEffect.Add)]
    [TestCase(1, MergeColorEffect.Add)]
    [TestCase(2, MergeColorEffect.Add)]
    [TestCase(0, MergeColorEffect.Remove)]
    [TestCase(1, MergeColorEffect.Remove)]
    [TestCase(2, MergeColorEffect.Remove)]
    public void Resolve_SeparationStopsAtPlayerAndRestoresInSameTurn(int chainLength, MergeColorEffect effect)
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(chainLength, effect);
        SeparationSchedule schedule = CreateSchedule(snapshot);
        SeparationPlan plan = schedule.plans[0];

        Assert.That(schedule.isValid, Is.True, schedule.invalidReason);
        Assert.That(plan.isDeferred, Is.False, plan.deferReason);
        Assert.That(plan.chainMovements, Has.Count.EqualTo(chainLength));
        Assert.That(plan.reservedEntities, Does.Not.Contain("player"));
        Assert.That(plan.reservedEntities, Does.Not.Contain("beyond"));
        for (int i = 0; i < chainLength; i++)
            Assert.That(plan.chainMovements[i].entityId, Is.EqualTo("chain-" + (chainLength - i)));

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputUp());
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(action.outcome, Is.EqualTo(PlayerActionOutcome.Move), action.failureMessage);
        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(result.isGameOverCandidate, Is.False);
        Assert.That(result.committedBoard.turnId, Is.EqualTo(7));
        Assert.That(result.committedBoard.entities["player"].cell, Is.EqualTo(new StageCell(chainLength + 1, 1)));
        AssertSeparated(result.committedBoard, "first", new StageCell(1, 0), new StageCell(0, 0));
        Assert.That(result.committedBoard.entities["first-merged"].remainingTurns, Is.EqualTo(1));
        Assert.That(result.committedBoard.currentColor, Is.EqualTo("Black"));
        Assert.That(result.committedBoard.entities["beyond"].cell, Is.EqualTo(new StageCell(chainLength + 2, 0)));
        for (int i = 1; i <= chainLength; i++)
        {
            Assert.That(result.committedBoard.entities["chain-" + i].cell, Is.EqualTo(new StageCell(i + 1, 0)));
            Assert.That(snapshot.entities["chain-" + i].cell, Is.EqualTo(new StageCell(i, 0)));
        }
        Assert.That(result.animationBatch.movements.FindAll(m => m.entityId == "player"), Has.Count.EqualTo(1));
        Assert.That(snapshot.entities["first-merged"].isActive, Is.True);
        Assert.That(snapshot.entities["first-origin-block"].isActive, Is.False);
        Assert.That(snapshot.entities["first-origin-background"].isActive, Is.False);
        Assert.That(snapshot.entities["player"].cell, Is.EqualTo(new StageCell(chainLength + 1, 0)));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: owner 이름과 무관하게 일반 Push는 Player에서 성공 종료하지 않는지 검증합니다.</summary>
    [Test]
    public void NormalPushDoesNotUseSeparationPlayerTerminalRule()
    {
        StageBoardWorkingCopy board = StageBoard.FromSnapshot(PlayerTerminalBoard(2, MergeColorEffect.Add)).CreateWorkingCopy();

        Assert.That(BlockManager.TryCreatePushPlanPure(board, new StageCell(1, 0), new StageCell(1, 0),
            "separation:first-merged", out PushPlan normal, out string failure), Is.False);
        Assert.That(normal.movements, Is.Empty);
        Assert.That(failure, Does.Contain("non-pushable"));
        Assert.That(BlockManager.TryCreateSeparationPushPlanPure(board, new StageCell(1, 0), new StageCell(1, 0),
            "arbitrary-owner", out PushPlan separation, out _), Is.True);
        Assert.That(separation.movements, Has.Count.EqualTo(2));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Player가 예약 출발 Cell에서 일반 Push로 이탈한 뒤 Separation Chain이 따라오는지 검증합니다.</summary>
    [Test]
    public void PlayerCanLeaveChainTerminalUsingNormalPush()
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(1, MergeColorEffect.Add);
        AddGround(snapshot, new StageCell(4, 0), "Black");
        SeparationSchedule schedule = CreateSchedule(snapshot);
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());

        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(action.pushPlan.movements, Has.Count.EqualTo(1));
        Assert.That(action.pushPlan.movements[0].entityId, Is.EqualTo("beyond"));
        Assert.That(schedule.plans[0].chainMovements, Has.Count.EqualTo(1));
        Assert.That(schedule.plans[0].chainMovements[0].entityId, Is.EqualTo("chain-1"));
        Assert.That(result.committedBoard.entities["player"].cell, Is.EqualTo(new StageCell(3, 0)));
        Assert.That(result.committedBoard.entities["beyond"].cell, Is.EqualTo(new StageCell(4, 0)));
        Assert.That(result.committedBoard.entities["chain-1"].cell, Is.EqualTo(new StageCell(2, 0)));
        AssertSeparated(result.committedBoard, "first", new StageCell(1, 0), new StageCell(0, 0));
    }

    /// <summary>입력: non-Player 이동 능력. 출력: 없음. 역할: 다른 Solid Top을 Player처럼 통과하지 않고 부분 예약 없이 유예하는지 검증합니다.</summary>
    [TestCase(MovementCapability.Static)]
    [TestCase(MovementCapability.SelfMovable)]
    public void SeparationOtherNonPushableSolidStillDefers(MovementCapability movement)
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(1, MergeColorEffect.Add);
        StageEntityState occupant = snapshot.entities["player"];
        occupant.kind = StageEntityKind.Block;
        occupant.profile = Profile(StageZone.Top, movement, OccupancyType.Solid);

        SeparationSchedule schedule = CreateSchedule(snapshot);

        Assert.That(schedule.plans[0].isDeferred, Is.True);
        Assert.That(schedule.plans[0].deferReason, Does.Contain("non-pushable"));
        Assert.That(schedule.reservedCells, Is.Empty);
        Assert.That(schedule.reservedEntities, Is.Empty);
        Assert.That(schedule.plans[0].chainMovements, Is.Empty);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Player 이탈 실패는 Turn을 Commit하지 않으며 Separation을 Player 사유로 Deferred하지 않는지 검증합니다.</summary>
    [Test]
    public void PlayerAtChainEndBlockedActionDoesNotCommitOrDeferSeparation()
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(1, MergeColorEffect.Add);
        snapshot.staticCells[new StageCell(2, 1)] = new StageCellStaticState { hasObstacle = true };
        SeparationSchedule schedule = CreateSchedule(snapshot);
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputUp());

        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(action.outcome, Is.EqualTo(PlayerActionOutcome.Blocked));
        Assert.That(action.failureReason, Is.EqualTo(PlayerActionFailureReason.Obstacle));
        Assert.That(result.isValid, Is.False);
        Assert.That(result.committedBoard, Is.Null);
        Assert.That(schedule.plans[0].isDeferred, Is.False);
        Assert.That(snapshot.turnId, Is.EqualTo(6));
        Assert.That(snapshot.entities["first-merged"].remainingTurns, Is.EqualTo(1));
    }

    /// <summary>입력: GO 이탈 방향. 출력: 없음. 역할: 예약 출발 Cell을 나가는 Player도 자신의 GO 조건을 지키는지 검증합니다.</summary>
    [TestCase(GroundOneWay.ExitDirection.W, PlayerActionOutcome.Move)]
    [TestCase(GroundOneWay.ExitDirection.D, PlayerActionOutcome.Blocked)]
    public void PlayerAtChainEndObeysItsOwnGroundOneWay(GroundOneWay.ExitDirection exit, PlayerActionOutcome expected)
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(1, MergeColorEffect.Add);
        snapshot.staticCells[new StageCell(2, 0)].bottomGroundKind = StageBottomGroundKind.GroundOneWay;
        snapshot.staticCells[new StageCell(2, 0)].exitDirection = exit;
        SeparationSchedule schedule = CreateSchedule(snapshot);

        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputUp());

        Assert.That(schedule.plans[0].isDeferred, Is.False);
        Assert.That(action.outcome, Is.EqualTo(expected), action.failureMessage);
        if (expected == PlayerActionOutcome.Blocked)
            Assert.That(action.failureReason, Is.EqualTo(PlayerActionFailureReason.ExitDirectionBlocked));
    }

    /// <summary>입력: GO 이탈 방향. 출력: 없음. 역할: Player 직전 Block의 GO 제한과 유예 시 0 유지 정책을 검증합니다.</summary>
    [TestCase(GroundOneWay.ExitDirection.D, false)]
    [TestCase(GroundOneWay.ExitDirection.W, true)]
    public void ChainEndingAtPlayerStillObeysGroundOneWay(GroundOneWay.ExitDirection exit, bool deferred)
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(2, MergeColorEffect.Add);
        snapshot.entities["first-merged"].remainingTurns = 0;
        snapshot.staticCells[new StageCell(2, 0)].bottomGroundKind = StageBottomGroundKind.GroundOneWay;
        snapshot.staticCells[new StageCell(2, 0)].exitDirection = exit;
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, PlayerActionResolver.Resolve(snapshot, schedule, InputUp()));

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(schedule.plans[0].isDeferred, Is.EqualTo(deferred));
        Assert.That(result.committedBoard.entities["first-merged"].remainingTurns, Is.Zero);
        Assert.That(result.committedBoard.entities["first-merged"].isActive, Is.EqualTo(deferred));
        if (deferred)
        {
            Assert.That(schedule.reservedCells, Is.Empty);
            Assert.That(schedule.reservedEntities, Is.Empty);
            Assert.That(result.committedBoard.entities["chain-1"].cell, Is.EqualTo(new StageCell(1, 0)));
        }
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Player Cell 도달 전 Bottom/Obstacle/다른 owner 예약을 계속 검사하는지 검증합니다.</summary>
    [Test]
    public void SeparationPushChecksDestinationBeforePlayerTerminal()
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(1, MergeColorEffect.Add);
        StageCell terminal = new StageCell(2, 0);
        snapshot.staticCells.Remove(terminal);
        Assert.That(BlockManager.TryCreateSeparationPushPlanPure(StageBoard.FromSnapshot(snapshot).CreateWorkingCopy(),
            new StageCell(1, 0), new StageCell(1, 0), "owner", out _, out string missing), Is.False);
        Assert.That(missing, Does.Contain("Bottom"));

        snapshot.staticCells[terminal] = new StageCellStaticState { hasObstacle = true };
        Assert.That(BlockManager.TryCreateSeparationPushPlanPure(StageBoard.FromSnapshot(snapshot).CreateWorkingCopy(),
            new StageCell(1, 0), new StageCell(1, 0), "owner", out _, out _), Is.False);

        snapshot.staticCells[terminal] = new StageCellStaticState { bottomGroundKind = StageBottomGroundKind.Ground, colorName = "Black" };
        StageBoardWorkingCopy board = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();
        board.ApplyReservations(new[] { terminal }, null, "earlier-owner");
        Assert.That(BlockManager.TryCreateSeparationPushPlanPure(board, new StageCell(1, 0), new StageCell(1, 0),
            "owner", out _, out string reserved), Is.False);
        Assert.That(reserved, Does.Contain("reserved"));
        Assert.That(BlockManager.TryCreateSeparationPushPlanPure(board, new StageCell(1, 0), new StageCell(1, 0),
            "earlier-owner", out _, out _), Is.True);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Player가 서 있던 Chain 목적지도 다른 Separation과 예약을 공유하지 않는지 검증합니다.</summary>
    [Test]
    public void PlayerTerminalDoesNotBypassMutualSeparationConflict()
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(1, MergeColorEffect.Add);
        AddDueSeparation(snapshot, "second", new StageCell(2, -1), new StageCell(2, 0), 2);
        snapshot.entities["second-merged"].separationDueTurn = 7;
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, PlayerActionResolver.Resolve(snapshot, schedule, InputUp()));

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(schedule.plans[0].mergedEntityId, Is.EqualTo("first-merged"));
        Assert.That(schedule.plans[0].isDeferred, Is.True);
        Assert.That(schedule.plans[1].isDeferred, Is.True);
        Assert.That(schedule.plans[1].reservedCells, Is.Empty);
        Assert.That(schedule.plans[1].reservedEntities, Is.Empty);
        Assert.That(result.committedBoard.entities["second-merged"].remainingTurns, Is.Zero);
        Assert.That(result.committedBoard.entities["first-merged"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["first-merged"].remainingTurns, Is.EqualTo(1));
        Assert.That(result.committedBoard.entities["chain-1"].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(result.committedBoard.entities["player"].cell, Is.EqualTo(new StageCell(2, 1)));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 예약 경로로 돌아가는 이동은 거절하고 Player 정상 이동을 되돌리는 계획 오류는 유예하지 않는지 검증합니다.</summary>
    [Test]
    public void PlayerCannotEnterChainReservationAndMalformedActionIsInternalError()
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(1, MergeColorEffect.Add);
        SeparationSchedule schedule = CreateSchedule(snapshot);
        PlayerActionPlan blocked = PlayerActionResolver.Resolve(snapshot, schedule,
            new TurnInput { actorEntityId = "player", direction = Vector3.left });
        Assert.That(blocked.failureReason, Is.EqualTo(PlayerActionFailureReason.DestinationReserved));

        PlayerActionPlan malformed = PlayerActionResolver.Resolve(snapshot, schedule, InputUp());
        malformed.actorMovement.to = malformed.actorMovement.from;
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, malformed);
        Assert.That(result.isValid, Is.False);
        Assert.That(schedule.plans[0].isDeferred, Is.False);
        Assert.That(result.committedBoard, Is.Null);
        Assert.That(result.invalidReason, Does.Contain("Internal Player Action mismatch"));
    }

    /// <summary>입력: Merge 효과. 출력: 없음. 역할: Player 종료 Chain 분리 후 Undo Snapshot에 위치/활성/타이머/CC가 완전하게 보존되는지 검증합니다.</summary>
    [TestCase(MergeColorEffect.Add)]
    [TestCase(MergeColorEffect.Remove)]
    public void UndoRestoresPlayerTerminalChainSeparation(MergeColorEffect effect)
    {
        StageBoardSnapshot snapshot = PlayerTerminalBoard(2, effect);
        SeparationSchedule schedule = CreateSchedule(snapshot);
        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, PlayerActionResolver.Resolve(snapshot, schedule, InputUp()));
        GameObject undoObject = new GameObject("UndoForPlayerTerminalChain");
        try
        {
            UndoManager undo = undoObject.AddComponent<UndoManager>();
            Assert.That(result.isValid, Is.True, result.invalidReason);
            Assert.That(undo.BeginTurnSnapshot(snapshot), Is.True);
            Assert.That(undo.ConfirmTurnSnapshot(), Is.True);
            Assert.That(undo.TryBeginUndoSnapshot(out StageBoardSnapshot restored), Is.True);
            Assert.That(restored.turnId, Is.EqualTo(6));
            Assert.That(restored.currentColor, Is.EqualTo(snapshot.currentColor));
            foreach (StageEntityState original in snapshot.entities.Values)
            {
                StageEntityState entity = restored.entities[original.entityId];
                Assert.That(entity.cell, Is.EqualTo(original.cell), original.entityId);
                Assert.That(entity.isActive, Is.EqualTo(original.isActive), original.entityId);
                Assert.That(entity.remainingTurns, Is.EqualTo(original.remainingTurns), original.entityId);
                Assert.That(entity.createdTurnId, Is.EqualTo(original.createdTurnId), original.entityId);
                Assert.That(entity.separationDueTurn, Is.EqualTo(original.separationDueTurn), original.entityId);
                Assert.That(entity.mergeColorEffect, Is.EqualTo(original.mergeColorEffect), original.entityId);
            }
        }
        finally
        {
            Object.DestroyImmediate(undoObject);
        }
    }

    /// <summary>입력: 두 Merge 효과. 출력: 없음. 역할: 같은 빈 복원 Cell을 요구하는 BM/BRM을 우선순위 없이 모두 유예합니다.</summary>
    [TestCase(MergeColorEffect.Add, MergeColorEffect.Add)]
    [TestCase(MergeColorEffect.Add, MergeColorEffect.Remove)]
    [TestCase(MergeColorEffect.Remove, MergeColorEffect.Add)]
    [TestCase(MergeColorEffect.Remove, MergeColorEffect.Remove)]
    public void SameEmptyRestorationCellDefersBothPlans(MergeColorEffect first, MergeColorEffect second)
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        AddConflictCandidate(snapshot, "a", new StageCell(0, 0), new StageCell(1, 0), first);
        AddConflictCandidate(snapshot, "b", new StageCell(2, 0), new StageCell(1, 0), second);
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = ResolveConflictTurn(snapshot, schedule);

        AssertBothDeferred(snapshot, schedule, result);
        Assert.That(result.animationBatch.movements, Has.Count.EqualTo(1));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 수평/수직 Chain이 중간 Block에서 교차하면 양쪽 Chain 전체를 유예합니다.</summary>
    [Test]
    public void PerpendicularChainsSharingMiddleBlockBothDeferAtomically()
    {
        StageBoardSnapshot snapshot = CrossingChainsBoard();
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = ResolveConflictTurn(snapshot, schedule);

        Assert.That(schedule.plans[0].chainMovements, Has.Count.EqualTo(2));
        Assert.That(schedule.plans[1].chainMovements, Has.Count.EqualTo(2));
        Assert.That(schedule.plans[0].chainMovements.Exists(m => m.entityId == "center"), Is.True);
        Assert.That(schedule.plans[1].chainMovements.Exists(m => m.entityId == "center"), Is.True);
        AssertBothDeferred(snapshot, schedule, result);
        Assert.That(result.animationBatch.movements, Has.Count.EqualTo(1));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 다른 방향에서 같은 Chain Entity를 이동시키는 두 계획을 모두 유예합니다.</summary>
    [Test]
    public void OpposingChainsSharingEntityBothDefer()
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        AddConflictCandidate(snapshot, "a", new StageCell(0, 0), new StageCell(1, 0));
        AddConflictCandidate(snapshot, "b", new StageCell(2, 0), new StageCell(1, 0));
        snapshot.entities.Add("shared", Pushable("shared", new StageCell(1, 0)));
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = ResolveConflictTurn(snapshot, schedule);

        Assert.That(schedule.plans[0].chainMovements[0].entityId, Is.EqualTo("shared"));
        Assert.That(schedule.plans[1].chainMovements[0].entityId, Is.EqualTo("shared"));
        Assert.That(schedule.plans[0].chainMovements[0].to, Is.Not.EqualTo(schedule.plans[1].chainMovements[0].to));
        AssertBothDeferred(snapshot, schedule, result);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Chain 목적지와 다른 origin 복원 Cell의 충돌은 공유 Block이 없어도 양쪽을 유예합니다.</summary>
    [Test]
    public void ChainDestinationOverlappingOtherRestorationCellDefersBoth()
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        AddConflictCandidate(snapshot, "a", new StageCell(0, 0), new StageCell(1, 0));
        AddConflictCandidate(snapshot, "b", new StageCell(2, 1), new StageCell(2, 0));
        snapshot.entities.Add("chain", Pushable("chain", new StageCell(1, 0)));
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = ResolveConflictTurn(snapshot, schedule);

        Assert.That(schedule.plans[0].chainMovements[0].to, Is.EqualTo(schedule.plans[1].originBlockCell));
        Assert.That(schedule.plans[1].chainMovements, Is.Empty);
        AssertBothDeferred(snapshot, schedule, result);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 복원 Block이 실제로 출발하는 merged Cell도 Top 이동 구간 충돌에 포함합니다.</summary>
    [Test]
    public void RestorationStartIntersectingChainIsNotIgnoredAsBottomOnly()
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        AddConflictCandidate(snapshot, "a", new StageCell(2, 2), new StageCell(3, 2));
        AddConflictCandidate(snapshot, "b", new StageCell(2, 0), new StageCell(2, 1));
        snapshot.entities.Add("chain", Pushable("chain", new StageCell(2, 1)));
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = ResolveConflictTurn(snapshot, schedule);

        Assert.That(schedule.plans[1].chainMovements[0].to, Is.EqualTo(new StageCell(2, 2)));
        AssertBothDeferred(snapshot, schedule, result);
    }

    /// <summary>입력: 두 번째 경로 시작 위치. 출력: 없음. 역할: 평행 또는 인접하되 점유가 분리된 Chain은 동시에 실행합니다.</summary>
    [TestCase(0, 1)]
    [TestCase(3, 0)]
    public void DisjointParallelOrAdjacentChainsExecuteTogether(int secondX, int secondY)
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        AddConflictCandidate(snapshot, "a", new StageCell(0, 0), new StageCell(1, 0));
        AddConflictCandidate(snapshot, "b", new StageCell(secondX, secondY), new StageCell(secondX + 1, secondY), MergeColorEffect.Remove);
        snapshot.entities.Add("a-chain", Pushable("a-chain", new StageCell(1, 0)));
        snapshot.entities.Add("b-chain", Pushable("b-chain", new StageCell(secondX + 1, secondY)));
        EnsureConflictSurface(snapshot, new StageCell(2, 0));
        EnsureConflictSurface(snapshot, new StageCell(secondX + 2, secondY));
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = ResolveConflictTurn(snapshot, schedule);

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(schedule.plans.TrueForAll(p => !p.isDeferred), Is.True);
        AssertSeparated(result.committedBoard, "a", new StageCell(1, 0), new StageCell(0, 0));
        AssertSeparated(result.committedBoard, "b", new StageCell(secondX + 1, secondY), new StageCell(secondX, secondY));
        Assert.That(result.committedBoard.entities["a-chain"].cell, Is.EqualTo(new StageCell(2, 0)));
        Assert.That(result.committedBoard.entities["b-chain"].cell, Is.EqualTo(new StageCell(secondX + 2, secondY)));
        Assert.That(result.animationBatch.movements, Has.Count.EqualTo(5));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 이동 경로가 아닌 별도 Bottom과 Trigger의 합법적 공존은 경로 충돌로 취급하지 않습니다.</summary>
    [Test]
    public void UnrelatedBottomAndTriggerDoNotBlockSeparationPath()
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        AddConflictCandidate(snapshot, "a", new StageCell(0, 0), new StageCell(1, 0));
        AddConflictCandidate(snapshot, "b", new StageCell(0, 2), new StageCell(1, 2));
        snapshot.entities.Add("chain", Pushable("chain", new StageCell(1, 0)));
        snapshot.entities.Add("surface", new StageEntityState
        {
            entityId = "surface", kind = StageEntityKind.BlockBackground, isActive = true, cell = new StageCell(2, 0),
            profile = Profile(StageZone.Bottom, MovementCapability.Static, OccupancyType.Surface)
        });
        snapshot.entities.Add("trigger", new StageEntityState
        {
            entityId = "trigger", kind = StageEntityKind.Block, isActive = true, cell = new StageCell(2, 0),
            profile = Profile(StageZone.Top, MovementCapability.Static, OccupancyType.Trigger)
        });
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = ResolveConflictTurn(snapshot, schedule);

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(schedule.plans.TrueForAll(p => !p.isDeferred), Is.True);
        Assert.That(result.committedBoard.entities["chain"].cell, Is.EqualTo(new StageCell(2, 0)));
        Assert.That(result.committedBoard.entities["surface"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["trigger"].isActive, Is.True);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Dictionary/생성/계획 적용 순서를 뒤집어도 충돌 및 독립 실행 결과가 동일합니다.</summary>
    [Test]
    public void ConflictOutcomeIsIndependentOfDictionaryCreationAndExecutionOrder()
    {
        StageBoardSnapshot snapshot = CrossingChainsBoard();
        AddConflictCandidate(snapshot, "independent", new StageCell(6, 0), new StageCell(7, 0));
        StageBoardSnapshot reversed = snapshot.Clone();
        var entries = new System.Collections.Generic.List<StageEntityState>(reversed.entities.Values);
        entries.Reverse();
        reversed.entities.Clear();
        foreach (StageEntityState entity in entries)
        {
            entity.mergeBatchId = 100 - entity.mergeBatchId;
            entity.mergeEventOrder = 100 - entity.mergeEventOrder;
            reversed.entities.Add(entity.entityId, entity);
        }
        SeparationSchedule first = CreateSchedule(snapshot);
        SeparationSchedule second = CreateSchedule(reversed);
        second.plans.Reverse();

        TurnResult firstResult = ResolveConflictTurn(snapshot, first);
        TurnResult secondResult = ResolveConflictTurn(reversed, second);

        Assert.That(firstResult.isValid, Is.True, firstResult.invalidReason);
        Assert.That(secondResult.isValid, Is.True, secondResult.invalidReason);
        foreach (SeparationPlan plan in first.plans)
            Assert.That(second.plans.Find(p => p.mergedEntityId == plan.mergedEntityId).isDeferred, Is.EqualTo(plan.isDeferred));
        foreach (StageEntityState entity in firstResult.committedBoard.entities.Values)
        {
            StageEntityState other = secondResult.committedBoard.entities[entity.entityId];
            Assert.That(other.cell, Is.EqualTo(entity.cell), entity.entityId);
            Assert.That(other.isActive, Is.EqualTo(entity.isActive), entity.entityId);
            Assert.That(other.remainingTurns, Is.EqualTo(entity.remainingTurns), entity.entityId);
        }
        Assert.That(secondResult.committedBoard.currentColor, Is.EqualTo(firstResult.committedBoard.currentColor));
        Assert.That(firstResult.committedBoard.entities["independent-merged"].isActive, Is.False);
        Assert.That(firstResult.committedBoard.entities["a-merged"].isActive, Is.True);
        Assert.That(firstResult.committedBoard.entities["b-merged"].isActive, Is.True);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: A-B와 B-C가 충돌하면 B를 먼저 유예해 C 검사가 누락되지 않도록 검증합니다.</summary>
    [Test]
    public void AllPairsAreCheckedEvenWhenMiddlePlanAlreadyConflicts()
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        AddConflictCandidate(snapshot, "a", new StageCell(1, 1), new StageCell(1, 0));
        AddConflictCandidate(snapshot, "b", new StageCell(0, 0), new StageCell(1, 0));
        AddConflictCandidate(snapshot, "c", new StageCell(3, 1), new StageCell(3, 0));
        snapshot.entities.Add("chain-1", Pushable("chain-1", new StageCell(1, 0)));
        snapshot.entities.Add("chain-2", Pushable("chain-2", new StageCell(2, 0)));
        EnsureConflictSurface(snapshot, new StageCell(2, 0));
        EnsureConflictSurface(snapshot, new StageCell(1, -1));
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = ResolveConflictTurn(snapshot, schedule);

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(schedule.plans, Has.Count.EqualTo(3));
        Assert.That(schedule.plans.TrueForAll(p => p.isDeferred && p.deferReason.Contains("overlap")), Is.True);
        Assert.That(schedule.reservedCells, Is.Empty);
        Assert.That(result.committedBoard.entities["chain-2"].cell, Is.EqualTo(new StageCell(2, 0)));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Schedule은 Player Action 전에 고정되고 충돌 Plan은 같은 Turn에 다시 계산하지 않습니다.</summary>
    [Test]
    public void PlayerCanMoveAfterConflictScheduleAndUndoRestoresEntireSnapshot()
    {
        StageBoardSnapshot snapshot = CrossingChainsBoard();
        AddConflictCandidate(snapshot, "independent", new StageCell(6, 0), new StageCell(7, 0));
        SeparationSchedule schedule = CreateSchedule(snapshot);
        TurnResult result = ResolveConflictTurn(snapshot, schedule);
        GameObject undoObject = new GameObject("UndoForMutualSeparationConflict");
        try
        {
            Assert.That(result.isValid, Is.True, result.invalidReason);
            Assert.That(result.committedBoard.entities["player"].cell, Is.EqualTo(new StageCell(-9, -10)));
            Assert.That(result.committedBoard.entities["independent-merged"].isActive, Is.False);
            Assert.That(schedule.plans.Find(p => p.mergedEntityId == "a-merged").isDeferred, Is.True);
            UndoManager undo = undoObject.AddComponent<UndoManager>();
            Assert.That(undo.BeginTurnSnapshot(snapshot), Is.True);
            Assert.That(undo.ConfirmTurnSnapshot(), Is.True);
            Assert.That(undo.TryBeginUndoSnapshot(out StageBoardSnapshot restored), Is.True);
            Assert.That(restored.turnId, Is.EqualTo(snapshot.turnId));
            Assert.That(restored.currentColor, Is.EqualTo(snapshot.currentColor));
            foreach (StageEntityState entity in snapshot.entities.Values)
            {
                Assert.That(restored.entities[entity.entityId].cell, Is.EqualTo(entity.cell), entity.entityId);
                Assert.That(restored.entities[entity.entityId].isActive, Is.EqualTo(entity.isActive), entity.entityId);
                Assert.That(restored.entities[entity.entityId].remainingTurns, Is.EqualTo(entity.remainingTurns), entity.entityId);
            }
        }
        finally { Object.DestroyImmediate(undoObject); }
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Player가 충돌 Cell의 Block을 밀어도 입력 전 확정한 두 Deferred Plan을 재계산하지 않습니다.</summary>
    [Test]
    public void PlayerPushDoesNotRecalculateConflictingSeparationPlans()
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        snapshot.entities["player"].cell = new StageCell(1, -1);
        EnsureConflictSurface(snapshot, new StageCell(1, -1));
        EnsureConflictSurface(snapshot, new StageCell(1, 1));
        AddConflictCandidate(snapshot, "a", new StageCell(0, 0), new StageCell(1, 0));
        AddConflictCandidate(snapshot, "b", new StageCell(2, 0), new StageCell(1, 0));
        snapshot.entities.Add("shared", Pushable("shared", new StageCell(1, 0)));
        SeparationSchedule schedule = CreateSchedule(snapshot);
        StageCell firstPlannedDestination = schedule.plans[0].chainMovements[0].to;
        StageCell secondPlannedDestination = schedule.plans[1].chainMovements[0].to;
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputUp());

        TurnResult result = StageTurnResolver.Resolve(snapshot, schedule, action);

        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(result.committedBoard.entities["player"].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(result.committedBoard.entities["shared"].cell, Is.EqualTo(new StageCell(1, 1)));
        Assert.That(schedule.plans.TrueForAll(p => p.isDeferred), Is.True);
        Assert.That(schedule.plans[0].chainMovements[0].to, Is.EqualTo(firstPlannedDestination));
        Assert.That(schedule.plans[1].chainMovements[0].to, Is.EqualTo(secondPlannedDestination));
        Assert.That(result.committedBoard.entities["a-merged"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["b-merged"].isActive, Is.True);
        Assert.That(result.committedBoard.entities["a-merged"].remainingTurns, Is.EqualTo(1));
        Assert.That(result.committedBoard.entities["b-merged"].remainingTurns, Is.EqualTo(1));
        Assert.That(result.animationBatch.movements, Has.Count.EqualTo(2));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Chain 성공 뒤 원본 복원이 InvalidBoard이면 Chain 애니메이션/Commit을 부분 반환하지 않습니다.</summary>
    [Test]
    public void InvalidRestorationAfterChainDoesNotPublishPartialSeparation()
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        AddConflictCandidate(snapshot, "a", new StageCell(0, 0), new StageCell(1, 0));
        snapshot.entities.Add("chain", Pushable("chain", new StageCell(1, 0)));
        EnsureConflictSurface(snapshot, new StageCell(2, 0));
        snapshot.entities["a-origin-background"].profile = Profile(StageZone.Top, MovementCapability.Static, OccupancyType.Surface);
        SeparationSchedule schedule = CreateSchedule(snapshot);

        TurnResult result = ResolveConflictTurn(snapshot, schedule);

        Assert.That(result.isValid, Is.False);
        Assert.That(result.committedBoard, Is.Null);
        Assert.That(result.animationBatch.movements.Exists(m => m.entityId == "chain" || m.entityId == "a-origin-block"), Is.False);
        Assert.That(snapshot.entities["chain"].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(snapshot.entities["a-merged"].isActive, Is.True);
        Assert.That(snapshot.entities["a-origin-block"].isActive, Is.False);
    }

    /// <summary>입력: 없음. 출력: Snapshot. 역할: 두 Chain이 중앙 Entity에서 교차하는 공통 입력 Board를 구성합니다.</summary>
    private static StageBoardSnapshot CrossingChainsBoard()
    {
        StageBoardSnapshot snapshot = ConflictBoard();
        AddConflictCandidate(snapshot, "a", new StageCell(0, 2), new StageCell(1, 2));
        AddConflictCandidate(snapshot, "b", new StageCell(2, 0), new StageCell(2, 1));
        snapshot.entities.Add("a-chain", Pushable("a-chain", new StageCell(1, 2)));
        snapshot.entities.Add("b-chain", Pushable("b-chain", new StageCell(2, 1)));
        snapshot.entities.Add("center", Pushable("center", new StageCell(2, 2)));
        EnsureConflictSurface(snapshot, new StageCell(2, 2));
        EnsureConflictSurface(snapshot, new StageCell(3, 2));
        EnsureConflictSurface(snapshot, new StageCell(2, 3));
        return snapshot;
    }

    /// <summary>입력: 없음. 출력: Snapshot. 역할: 충돌 공간 밖에서 Player가 이동할 수 있는 Turn 6 Board를 만듭니다.</summary>
    private static StageBoardSnapshot ConflictBoard()
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot { turnId = 6, currentColor = "White" };
        AddPlayer(snapshot, new StageCell(-10, -10));
        AddGround(snapshot, new StageCell(-10, -10), "Black");
        AddGround(snapshot, new StageCell(-9, -10), "Black");
        return snapshot;
    }

    /// <summary>입력: Snapshot, ID, Cell, 효과. 출력: 없음. 역할: remaining=1, due=7인 분리 후보와 복원 Cell의 표면을 만듭니다.</summary>
    private static void AddConflictCandidate(StageBoardSnapshot snapshot, string prefix, StageCell merged, StageCell origin, MergeColorEffect effect = MergeColorEffect.Add)
    {
        AddDueSeparation(snapshot, prefix, merged, origin, snapshot.entities.Count);
        StageEntityState entity = snapshot.entities[prefix + "-merged"];
        entity.remainingTurns = 1;
        entity.createdTurnId = 5;
        entity.separationDueTurn = 7;
        entity.mergeColorEffect = effect;
        EnsureConflictSurface(snapshot, origin);
    }

    /// <summary>입력: Snapshot과 Cell. 출력: 없음. 역할: 기존 Bottom과 중복되지 않는 테스트 Ground만 준비합니다.</summary>
    private static void EnsureConflictSurface(StageBoardSnapshot snapshot, StageCell cell)
    {
        if (snapshot.staticCells.ContainsKey(cell)) return;
        foreach (StageEntityState entity in snapshot.entities.Values)
            if (entity.isActive && entity.profile.zone == StageZone.Bottom && entity.cell.Equals(cell)) return;
        AddGround(snapshot, cell, "Black");
    }

    /// <summary>입력: Snapshot과 확정 Schedule. 출력: TurnResult. 역할: 동일 Player Action을 적용하고 분리 결과를 계산합니다.</summary>
    private static TurnResult ResolveConflictTurn(StageBoardSnapshot snapshot, SeparationSchedule schedule)
    {
        PlayerActionPlan action = PlayerActionResolver.Resolve(snapshot, schedule, InputRight());
        Assert.That(action.outcome, Is.EqualTo(PlayerActionOutcome.Move), action.failureMessage);
        return StageTurnResolver.Resolve(snapshot, schedule, action);
    }

    /// <summary>입력: 전후 Snapshot과 Schedule. 출력: 없음. 역할: 두 충돌 Plan의 예약/부분 실행이 없고 모든 비Player Entity가 그대로인지 검사합니다.</summary>
    private static void AssertBothDeferred(StageBoardSnapshot snapshot, SeparationSchedule schedule, TurnResult result)
    {
        Assert.That(result.isValid, Is.True, result.invalidReason);
        Assert.That(schedule.plans, Has.Count.EqualTo(2));
        Assert.That(schedule.plans.TrueForAll(p => p.isDeferred && p.deferReason.Contains("overlap")), Is.True);
        Assert.That(schedule.reservedCells, Is.Empty);
        Assert.That(schedule.reservedEntities, Is.Empty);
        foreach (SeparationPlan plan in schedule.plans)
        {
            Assert.That(plan.reservedCells, Is.Empty);
            Assert.That(plan.reservedEntities, Is.Empty);
        }
        foreach (StageEntityState entity in snapshot.entities.Values)
        {
            if (entity.entityId == "player") continue;
            StageEntityState actual = result.committedBoard.entities[entity.entityId];
            Assert.That(actual.cell, Is.EqualTo(entity.cell), entity.entityId);
            Assert.That(actual.isActive, Is.EqualTo(entity.isActive), entity.entityId);
            Assert.That(actual.remainingTurns, Is.EqualTo(entity.remainingTurns), entity.entityId);
        }
    }

    /// <summary>입력: Chain 길이와 효과. 출력: Snapshot. 역할: BM, Chain, Player, 너머 Block 및 Player 이탈 Cell을 구성합니다.</summary>
    private static StageBoardSnapshot PlayerTerminalBoard(int chainLength, MergeColorEffect effect)
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot { turnId = 6, currentColor = effect == MergeColorEffect.Add ? "Red" : "Black" };
        AddDueSeparation(snapshot, "first", new StageCell(0, 0), new StageCell(1, 0), 1);
        snapshot.entities["first-merged"].remainingTurns = 1;
        snapshot.entities["first-merged"].createdTurnId = 5;
        snapshot.entities["first-merged"].separationDueTurn = 7;
        snapshot.entities["first-merged"].mergeColorEffect = effect;
        snapshot.entities["first-origin-block"].mergeColorEffect = effect;
        for (int i = 1; i <= chainLength; i++) snapshot.entities.Add("chain-" + i, Pushable("chain-" + i, new StageCell(i, 0)));
        AddPlayer(snapshot, new StageCell(chainLength + 1, 0));
        snapshot.entities.Add("beyond", Pushable("beyond", new StageCell(chainLength + 2, 0)));
        for (int i = 1; i <= chainLength + 2; i++) AddGround(snapshot, new StageCell(i, 0), "Black");
        AddGround(snapshot, new StageCell(chainLength + 1, 1), "Black");
        return snapshot;
    }

    private static TurnInput InputUp() => new TurnInput { actorEntityId = "player", direction = Vector3.up };

    private StageBoardSnapshot BaseBoard()
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot { turnId = 0, currentColor = "White" };
        snapshot.entities.Add("player", new StageEntityState
        {
            entityId = "player", isActive = true, cell = new StageCell(0, 0),
            profile = Profile(StageZone.Top, MovementCapability.SelfMovable, OccupancyType.Solid)
        });
        AddGround(snapshot, new StageCell(0, 0), "White");
        AddGround(snapshot, new StageCell(1, 0), "White");
        AddGround(snapshot, new StageCell(2, 0), "White");
        return snapshot;
    }

    private static StageBoardSnapshot PlayerVacateBoard()
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot { turnId = 5, currentColor = "Red" };
        AddPlayer(snapshot, new StageCell(1, 0));
        AddGround(snapshot, new StageCell(1, 0), "Black");
        AddGround(snapshot, new StageCell(2, 0), "Black");
        AddDueSeparation(snapshot, string.Empty, new StageCell(0, 0), new StageCell(1, 0), 1);
        return snapshot;
    }

    private static StageBoardSnapshot RemotePlayerBoard()
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot { turnId = 5, currentColor = "Red" };
        AddPlayer(snapshot, new StageCell(-2, 0));
        AddGround(snapshot, new StageCell(-2, 0), "Black");
        AddGround(snapshot, new StageCell(-1, 0), "Black");
        return snapshot;
    }

    private static void AddPlayer(StageBoardSnapshot snapshot, StageCell cell)
    {
        snapshot.entities.Add("player", new StageEntityState
        {
            entityId = "player",
            kind = StageEntityKind.Player,
            isActive = true,
            cell = cell,
            profile = Profile(StageZone.Top, MovementCapability.SelfMovable, OccupancyType.Solid)
        });
    }

    private static void AddDueSeparation(
        StageBoardSnapshot snapshot,
        string prefix,
        StageCell mergedCell,
        StageCell originCell,
        long mergeBatchId)
    {
        string separator = string.IsNullOrEmpty(prefix) ? string.Empty : prefix + "-";
        string mergedId = separator + "merged";
        string blockId = separator + "origin-block";
        string backgroundId = separator + "origin-background";
        snapshot.entities.Add(mergedId, new StageEntityState
        {
            entityId = mergedId,
            kind = StageEntityKind.BlockMerged,
            isActive = true,
            cell = mergedCell,
            colorName = "Red",
            mergeColorEffect = MergeColorEffect.Add,
            remainingTurns = 0,
            createdTurnId = 0,
            separationDueTurn = 5,
            mergeBatchId = mergeBatchId,
            originBlockCell = originCell,
            originBlockEntityId = blockId,
            originBackgroundEntityId = backgroundId,
            profile = Profile(StageZone.Bottom, MovementCapability.Static, OccupancyType.Surface)
        });
        snapshot.entities.Add(blockId, new StageEntityState
        {
            entityId = blockId,
            kind = StageEntityKind.Block,
            isActive = false,
            cell = mergedCell,
            profile = Profile(StageZone.Top, MovementCapability.Pushable, OccupancyType.Solid)
        });
        snapshot.entities.Add(backgroundId, new StageEntityState
        {
            entityId = backgroundId,
            kind = StageEntityKind.BlockBackground,
            isActive = false,
            cell = mergedCell,
            profile = Profile(StageZone.Bottom, MovementCapability.Static, OccupancyType.Surface)
        });
    }

    private static StageEntityState Pushable(string entityId, StageCell cell)
    {
        return new StageEntityState
        {
            entityId = entityId,
            kind = StageEntityKind.Block,
            isActive = true,
            cell = cell,
            profile = Profile(StageZone.Top, MovementCapability.Pushable, OccupancyType.Solid)
        };
    }

    private static SeparationSchedule CreateSchedule(StageBoardSnapshot snapshot)
    {
        GameObject managerObject = new GameObject("SeparationManagerForResolverTest");
        SeparationManager manager = managerObject.AddComponent<SeparationManager>();
        SeparationSchedule schedule = manager.CreateSchedule(snapshot, snapshot.turnId + 1);
        Object.DestroyImmediate(managerObject);
        return schedule;
    }

    private static void AssertSeparated(StageBoardSnapshot board, string prefix, StageCell blockCell, StageCell backgroundCell)
    {
        Assert.That(board.entities[prefix + "-merged"].isActive, Is.False);
        Assert.That(board.entities[prefix + "-origin-block"].isActive, Is.True);
        Assert.That(board.entities[prefix + "-origin-block"].cell, Is.EqualTo(blockCell));
        Assert.That(board.entities[prefix + "-origin-background"].isActive, Is.True);
        Assert.That(board.entities[prefix + "-origin-background"].cell, Is.EqualTo(backgroundCell));
    }

    private void AddMergePair(StageBoardSnapshot snapshot, string blockId, string backgroundId, StageCell cell, string blockColor, string acceptedColor, MergeColorEffect effect)
    {
        snapshot.entities.Add(blockId, new StageEntityState
        {
            entityId = blockId, kind = StageEntityKind.Block, isActive = true, cell = cell,
            colorName = blockColor, mergeColorEffect = effect, mergedPrefab = mergedPrefab,
            profile = Profile(StageZone.Top, MovementCapability.Pushable, OccupancyType.Solid)
        });
        snapshot.entities.Add(backgroundId, new StageEntityState
        {
            entityId = backgroundId, kind = StageEntityKind.BlockBackground, isActive = true, cell = cell,
            colorName = "Black", acceptedColorName = acceptedColor, remainingTurns = 2,
            profile = Profile(StageZone.Bottom, MovementCapability.Static, OccupancyType.Surface)
        });
    }

    private static void AddSeparationEntities(StageBoardSnapshot snapshot, bool addBlocker)
    {
        snapshot.entities.Add("merged", new StageEntityState
        {
            entityId = "merged", kind = StageEntityKind.BlockMerged, isActive = true, cell = new StageCell(3, 0),
            colorName = "Red", remainingTurns = 1,
            profile = Profile(StageZone.Bottom, MovementCapability.Static, OccupancyType.Surface)
        });
        snapshot.entities.Add("origin-block", new StageEntityState
        {
            entityId = "origin-block", kind = StageEntityKind.Block, isActive = false, cell = new StageCell(3, 0),
            profile = Profile(StageZone.Top, MovementCapability.Pushable, OccupancyType.Solid)
        });
        snapshot.entities.Add("origin-background", new StageEntityState
        {
            entityId = "origin-background", kind = StageEntityKind.BlockBackground, isActive = false, cell = new StageCell(3, 0),
            profile = Profile(StageZone.Bottom, MovementCapability.Static, OccupancyType.Surface)
        });
        if (addBlocker) snapshot.entities.Add("blocker", new StageEntityState
        {
            entityId = "blocker", kind = StageEntityKind.Block, isActive = true, cell = new StageCell(2, 0),
            profile = Profile(StageZone.Top, MovementCapability.Pushable, OccupancyType.Solid)
        });
    }

    private static SeparationSchedule SeparationAt(StageCell origin)
    {
        SeparationPlan plan = new SeparationPlan
        {
            planId = "separation:merged", ownerId = "separation:merged", mergedEntityId = "merged",
            originBlockEntityId = "origin-block", originBackgroundEntityId = "origin-background", originBlockCell = origin
        };
        plan.reservedCells.Add(origin);
        plan.reservedEntities.Add("merged");
        plan.reservedEntities.Add("origin-block");
        plan.reservedEntities.Add("origin-background");
        SeparationSchedule schedule = new SeparationSchedule();
        schedule.plans.Add(plan);
        return schedule;
    }

    private static TurnInput InputRight() => new TurnInput { actorEntityId = "player", direction = Vector3.right };
    private static StageObjectProfile Profile(StageZone zone, MovementCapability movement, OccupancyType occupancy) => new StageObjectProfile { zone = zone, movement = movement, occupancy = occupancy };
    private static void AddGround(StageBoardSnapshot snapshot, StageCell cell, string color) => snapshot.staticCells.Add(cell, new StageCellStaticState { bottomGroundKind = StageBottomGroundKind.Ground, colorName = color });
}
