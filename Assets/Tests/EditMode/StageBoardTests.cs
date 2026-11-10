using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public class StageBoardTests
{
    private GameObject playerObject;
    private GameObject blockObject;
    private GameObject backgroundObject;
    private GameObject profileOnlyObject;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 논리 보드 Snapshot 테스트에 필요한 동적 Stage 오브젝트를 생성합니다.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("Player");
        playerObject.AddComponent<Player>();
        playerObject.transform.position = Vector3.zero;

        blockObject = new GameObject("Block");
        blockObject.AddComponent<Block>();
        blockObject.transform.position = Vector3.right;

        backgroundObject = new GameObject("Background");
        backgroundObject.AddComponent<BlockBackground>();
        backgroundObject.transform.position = Vector3.up;
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 테스트가 생성한 동적 Stage 오브젝트를 제거합니다.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(backgroundObject);
        Object.DestroyImmediate(blockObject);
        Object.DestroyImmediate(playerObject);
        if (profileOnlyObject != null) Object.DestroyImmediate(profileOnlyObject);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Snapshot과 작업용 Board 사본이 Transform 변경과 서로의 상태 변경으로부터 독립적인지 검증합니다.
    /// </summary>
    [Test]
    public void Capture_CreatesIndependentSnapshotAndWorkingCopy()
    {
        StageBoard board = StageBoard.Capture(4, "Blue");
        StageBoardSnapshot snapshot = board.CreateSnapshot();
        StageBoardWorkingCopy workingCopy = board.CreateWorkingCopy();
        string blockId = "block:" + blockObject.GetComponent<Block>().GetInstanceID();

        blockObject.transform.position = new Vector3(4f, 4f, 0f);
        workingCopy.State.entities[blockId].cell = new StageCell(3, 3);

        Assert.That(snapshot.turnId, Is.EqualTo(4));
        Assert.That(snapshot.currentColor, Is.EqualTo("Blue"));
        Assert.That(snapshot.entities[blockId].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(workingCopy.State.entities[blockId].cell, Is.EqualTo(new StageCell(3, 3)));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Profile override가 없을 때 기존 Block의 기본 Pushable Top Profile을 사용하는지 검증합니다.</summary>
    [Test]
    public void Capture_BlockUsesDefaultProfileWithoutOverride()
    {
        StageEntityState block = StageBoard.Capture(0, "Black").CreateSnapshot().entities["block:" + blockObject.GetComponent<Block>().GetInstanceID()];
        Assert.That(block.profile.zone, Is.EqualTo(StageZone.Top));
        Assert.That(block.profile.movement, Is.EqualTo(MovementCapability.Pushable));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Block에 명시 Profile Component가 있으면 기본 Profile보다 우선하는지 검증합니다.</summary>
    [Test]
    public void Capture_BlockOverrideProfileTakesPrecedenceAndSurvivesClone()
    {
        StageObjectProfileComponent profile = blockObject.AddComponent<StageObjectProfileComponent>();
        profile.zone = StageZone.Bottom; profile.movement = MovementCapability.Static; profile.occupancy = OccupancyType.Surface;
        StageBoardSnapshot snapshot = StageBoard.Capture(0, "Black").CreateSnapshot();
        StageEntityState block = snapshot.entities["block:" + blockObject.GetComponent<Block>().GetInstanceID()];
        Assert.That(block.profile.zone, Is.EqualTo(StageZone.Bottom));
        Assert.That(snapshot.Clone().entities[block.entityId].profile.zone, Is.EqualTo(StageZone.Bottom));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 핵심 Component 없이 Profile만 가진 Object가 generic Entity로 Capture되는지 검증합니다.</summary>
    [Test]
    public void Capture_ProfileOnlyObjectCreatesGenericEntity()
    {
        profileOnlyObject = new GameObject("ProfileOnly");
        profileOnlyObject.AddComponent<StageObjectProfileComponent>().movement = MovementCapability.Pushable;
        StageBoardSnapshot snapshot = StageBoard.Capture(0, "Black").CreateSnapshot();
        Assert.That(snapshot.entities.ContainsKey("profile:" + profileOnlyObject.GetInstanceID()), Is.True);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 기존 Component와 Profile Component가 함께 있어도 Entity가 한 번만 Capture되는지 검증합니다.</summary>
    [Test]
    public void Capture_ComponentAndProfileDoesNotDuplicateEntity()
    {
        blockObject.AddComponent<StageObjectProfileComponent>();
        StageBoardSnapshot snapshot = StageBoard.Capture(0, "Black").CreateSnapshot();
        int count = 0; foreach (StageEntityState entity in snapshot.entities.Values) if (entity.runtimeObject == blockObject) count++;
        Assert.That(count, Is.EqualTo(1));
    }

    /// <summary>입력: 원본 Block/BG를 참조하는 BM. 출력: 없음. 역할: BM의 원본 Entity ID가 Snapshot의 Component 기반 Key와 일치하는지 검증합니다.</summary>
    [Test]
    public void Capture_MergedOriginIdsMatchComponentEntityKeys()
    {
        GameObject mergedObject = new GameObject("Merged");
        try
        {
            Block block = blockObject.GetComponent<Block>();
            BlockBackground background = backgroundObject.GetComponent<BlockBackground>();
            blockObject.SetActive(false);
            backgroundObject.SetActive(false);

            BlockMerged merged = mergedObject.AddComponent<BlockMerged>();
            merged.originBlockPrefab = blockObject;
            merged.originBackgroundObject = backgroundObject;
            merged.originBlockPosition = blockObject.transform.position;

            StageBoardSnapshot snapshot = StageBoard.Capture(0, "Black").CreateSnapshot();
            string mergedId = "merged:" + merged.GetInstanceID();
            string expectedBlockId = "block:" + block.GetInstanceID();
            string expectedBackgroundId = "background:" + background.GetInstanceID();
            StageEntityState mergedState = snapshot.entities[mergedId];

            Assert.That(mergedState.originBlockEntityId, Is.EqualTo(expectedBlockId));
            Assert.That(mergedState.originBackgroundEntityId, Is.EqualTo(expectedBackgroundId));
            Assert.That(snapshot.entities.ContainsKey(mergedState.originBlockEntityId), Is.True);
            Assert.That(snapshot.entities.ContainsKey(mergedState.originBackgroundEntityId), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(mergedObject);
        }
    }

    /// <summary>입력: Remove 효과 BRM과 Turn 7 due 상태. 출력: 없음. 역할: BRM도 Component 기반 원본 ID와 target turn 예약 규칙을 공유하는지 검증합니다.</summary>
    [Test]
    public void Capture_ReverseMergedUsesOriginComponentIdsAndTargetTurnSchedule()
    {
        GameObject mergedObject = new GameObject("ReverseMerged");
        GameObject managerObject = new GameObject("SeparationManagerForOriginIdTest");
        try
        {
            Block block = blockObject.GetComponent<Block>();
            BlockBackground background = backgroundObject.GetComponent<BlockBackground>();
            blockObject.SetActive(false);
            backgroundObject.SetActive(false);

            BlockMerged merged = mergedObject.AddComponent<BlockMerged>();
            merged.mergeColorEffect = MergeColorEffect.Remove;
            merged.remainingTurns = 1;
            merged.createdTurnId = 5;
            merged.separationDueTurn = 7;
            merged.originBlockPrefab = blockObject;
            merged.originBackgroundObject = backgroundObject;
            merged.originBlockPosition = Vector3.right;

            StageBoardSnapshot snapshot = StageBoard.Capture(6, "Red").CreateSnapshot();
            StageEntityState mergedState = snapshot.entities["merged:" + merged.GetInstanceID()];
            SeparationSchedule schedule = managerObject.AddComponent<SeparationManager>().CreateSchedule(snapshot, 7);

            Assert.That(mergedState.mergeColorEffect, Is.EqualTo(MergeColorEffect.Remove));
            Assert.That(mergedState.originBlockEntityId, Is.EqualTo("block:" + block.GetInstanceID()));
            Assert.That(mergedState.originBackgroundEntityId, Is.EqualTo("background:" + background.GetInstanceID()));
            Assert.That(schedule.isValid, Is.True, schedule.invalidReason);
            Assert.That(schedule.plans, Has.Count.EqualTo(1));
            Assert.That(schedule.plans[0].isDeferred, Is.False, schedule.plans[0].deferReason);
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(mergedObject);
        }
    }

    /// <summary>입력: Block Component가 없는 원본 GameObject. 출력: 없음. 역할: 잘못된 원본 참조를 명확한 누락 사유로 Deferred 처리하는지 검증합니다.</summary>
    [Test]
    public void Capture_MergedOriginWithoutRequiredComponentIsDeferredClearly()
    {
        GameObject mergedObject = new GameObject("MergedWithInvalidOrigin");
        GameObject invalidBlockOrigin = new GameObject("InvalidBlockOrigin");
        GameObject managerObject = new GameObject("SeparationManagerForMissingOriginTest");
        try
        {
            blockObject.SetActive(false);
            backgroundObject.SetActive(false);
            invalidBlockOrigin.SetActive(false);

            BlockMerged merged = mergedObject.AddComponent<BlockMerged>();
            merged.remainingTurns = 0;
            merged.createdTurnId = 0;
            merged.separationDueTurn = 1;
            merged.originBlockPrefab = invalidBlockOrigin;
            merged.originBackgroundObject = backgroundObject;
            merged.originBlockPosition = Vector3.right;

            StageBoardSnapshot snapshot = StageBoard.Capture(0, "Red").CreateSnapshot();
            StageEntityState mergedState = snapshot.entities["merged:" + merged.GetInstanceID()];
            SeparationSchedule schedule = managerObject.AddComponent<SeparationManager>().CreateSchedule(snapshot, 1);

            Assert.That(mergedState.originBlockEntityId, Is.Null);
            Assert.That(schedule.isValid, Is.True, schedule.invalidReason);
            Assert.That(schedule.plans, Has.Count.EqualTo(1));
            Assert.That(schedule.plans[0].isDeferred, Is.True);
            Assert.That(schedule.plans[0].deferReason, Does.Contain("origin entities are missing"));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(invalidBlockOrigin);
            Object.DestroyImmediate(mergedObject);
        }
    }

    [Test]
    public void Capture_AdjacentTilemapCellsRemainDistinctStageCells()
    {
        GameObject gridObject = new GameObject("TestGrid", typeof(Grid));
        GameObject tilemapObject = new GameObject("TestGround", typeof(Tilemap), typeof(TilemapRenderer), typeof(ColorTilemap));
        GameObject managerObject = new GameObject("TestTilemapManager", typeof(TilemapManager));
        Tile tile = ScriptableObject.CreateInstance<Tile>();

        try
        {
            tilemapObject.transform.SetParent(gridObject.transform, false);
            Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
            tilemapObject.GetComponent<ColorTilemap>().colorName = "Black";
            managerObject.GetComponent<TilemapManager>().tilemaps = new[] { tilemap };
            tilemap.SetTile(new Vector3Int(-2, -1, 0), tile);
            tilemap.SetTile(new Vector3Int(-2, 0, 0), tile);

            StageBoardSnapshot snapshot = StageBoard.Capture(0, "Black").CreateSnapshot();

            Assert.That(snapshot.staticCells.ContainsKey(new StageCell(-2, -1)), Is.True);
            Assert.That(snapshot.staticCells.ContainsKey(new StageCell(-2, 0)), Is.True);
            Assert.That(snapshot.staticCells[new StageCell(-2, -1)].hasBottomConflict, Is.False);
            Assert.That(snapshot.staticCells[new StageCell(-2, 0)].hasBottomConflict, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(tile);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(tilemapObject);
            Object.DestroyImmediate(gridObject);
        }
    }

    [Test]
    public void Capture_GroundAndGroundOneWayOnSameCellRemainConflict()
    {
        GameObject gridObject = new GameObject("TestGrid", typeof(Grid));
        GameObject tilemapObject = new GameObject("TestGround", typeof(Tilemap), typeof(TilemapRenderer), typeof(ColorTilemap));
        GameObject managerObject = new GameObject("TestTilemapManager", typeof(TilemapManager));
        GameObject oneWayObject = new GameObject("TestGroundOneWay", typeof(GroundOneWay));
        Tile tile = ScriptableObject.CreateInstance<Tile>();

        try
        {
            tilemapObject.transform.SetParent(gridObject.transform, false);
            Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
            tilemapObject.GetComponent<ColorTilemap>().colorName = "Blue";
            managerObject.GetComponent<TilemapManager>().tilemaps = new[] { tilemap };
            Vector3Int tileCell = new Vector3Int(1, 2, 0);
            tilemap.SetTile(tileCell, tile);
            oneWayObject.transform.position = tilemap.GetCellCenterWorld(tileCell);

            StageBoardSnapshot snapshot = StageBoard.Capture(0, "Blue").CreateSnapshot();

            Assert.That(snapshot.staticCells[new StageCell(1, 2)].hasBottomConflict, Is.True);
            Assert.That(StageBoard.FromSnapshot(snapshot).CreateWorkingCopy().ValidateBoard().isValid, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(tile);
            Object.DestroyImmediate(oneWayObject);
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(tilemapObject);
            Object.DestroyImmediate(gridObject);
        }
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 예약된 Cell과 Entity가 Working Copy 이동 및 조회에서 차단되는지 검증합니다.</summary>
    [Test]
    public void WorkingCopy_ReservationsBlockCellAndEntity()
    {
        StageBoardWorkingCopy workingCopy = StageBoard.Capture(0, "Black").CreateWorkingCopy();
        string blockId = "block:" + blockObject.GetComponent<Block>().GetInstanceID();
        StageCell destination = new StageCell(2, 0);
        workingCopy.ApplyReservations(new[] { destination }, new[] { blockId });

        Assert.That(workingCopy.QueryCell(destination).isReserved, Is.True);
        Assert.That(workingCopy.IsEntityReserved(blockId), Is.True);
        Assert.That(workingCopy.TryApply(new BoardMovement { entityId = blockId, from = new StageCell(1, 0), to = destination }), Is.False);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Separation 계획 소유자는 자기 예약을 적용할 수 있고 다른 계획은 차단되는지 검증합니다.</summary>
    [Test]
    public void WorkingCopy_ReservationOwnerCanApplyOwnPlanOnly()
    {
        StageBoardWorkingCopy workingCopy = StageBoard.Capture(0, "Black").CreateWorkingCopy();
        string blockId = "block:" + blockObject.GetComponent<Block>().GetInstanceID();
        StageCell destination = new StageCell(2, 0);
        workingCopy.ApplyReservations(new[] { destination }, new[] { blockId }, "separation:1");

        BoardMovement movement = new BoardMovement { entityId = blockId, from = new StageCell(1, 0), to = destination };
        Assert.That(workingCopy.TryApply(new[] { movement }, MovementIntent.Push, "player"), Is.False);
        Assert.That(workingCopy.TryApply(new[] { movement }, MovementIntent.Push, "separation:1"), Is.True);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 작업용 계획 적용이 원본 Snapshot을 바꾸지 않는지 검증합니다.</summary>
    [Test]
    public void WorkingCopy_ApplyDoesNotMutateSourceSnapshot()
    {
        StageBoard board = StageBoard.Capture(0, "Black");
        StageBoardSnapshot snapshot = board.CreateSnapshot();
        StageBoardWorkingCopy workingCopy = board.CreateWorkingCopy();
        string blockId = "block:" + blockObject.GetComponent<Block>().GetInstanceID();

        Assert.That(workingCopy.TryApply(new[] { new BoardMovement { entityId = blockId, from = new StageCell(1, 0), to = new StageCell(2, 0) } }, MovementIntent.Push), Is.True);
        Assert.That(snapshot.entities[blockId].cell, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(workingCopy.State.entities[blockId].cell, Is.EqualTo(new StageCell(2, 0)));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 다중 이동 중 하나가 실패하면 Working Copy 전체가 변경되지 않는지 검증합니다.</summary>
    [Test]
    public void WorkingCopy_BatchApplyIsAtomicWhenLaterMovementFails()
    {
        StageBoardWorkingCopy workingCopy = StageBoard.Capture(0, "Black").CreateWorkingCopy();
        string blockId = "block:" + blockObject.GetComponent<Block>().GetInstanceID();
        BoardMovement valid = new BoardMovement { entityId = blockId, from = new StageCell(1, 0), to = new StageCell(2, 0) };
        BoardMovement invalid = new BoardMovement { entityId = blockId, from = new StageCell(9, 9), to = new StageCell(3, 0) };

        Assert.That(workingCopy.TryApply(new[] { valid, invalid }, MovementIntent.Push), Is.False);
        Assert.That(workingCopy.State.entities[blockId].cell, Is.EqualTo(new StageCell(1, 0)));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 서로 다른 Top 엔티티가 같은 Cell을 점유하면 Commit 검증이 실패하는지 확인합니다.</summary>
    [Test]
    public void WorkingCopy_ValidateBoardMarksDuplicateSolidTopAsGameOverCandidate()
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot();
        snapshot.entities.Add("player", new StageEntityState { entityId = "player", kind = StageEntityKind.Player, cell = new StageCell(0, 0), isActive = true });
        snapshot.entities.Add("block", new StageEntityState { entityId = "block", kind = StageEntityKind.Block, cell = new StageCell(0, 0), isActive = true });
        StageBoardWorkingCopy workingCopy = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();

        StageBoardValidationResult result = workingCopy.ValidateBoard();
        Assert.That(result.isValid, Is.True);
        Assert.That(result.isGameOverCandidate, Is.True);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: bottom zone BlockMerged가 이동 계획으로 적용되지 않는지 검증합니다.</summary>
    [Test]
    public void WorkingCopy_RejectsBlockMergedMovement()
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot();
        snapshot.entities.Add("merged", new StageEntityState
        {
            entityId = "merged",
            kind = StageEntityKind.BlockMerged,
            cell = new StageCell(0, 0),
            isActive = true,
            profile = P(StageZone.Bottom, MovementCapability.Static, OccupancyType.Surface)
        });
        StageBoardWorkingCopy workingCopy = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();
        Assert.That(workingCopy.TryApply(new BoardMovement { entityId = "merged", from = new StageCell(0, 0), to = new StageCell(1, 0) }), Is.False);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 정적 GroundOneWay 정보와 중복 Top 점유가 Cell 조회 결과에 표현되는지 검증합니다.</summary>
    [Test]
    public void WorkingCopy_QueryCellReturnsStaticStateAndConflict()
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot();
        StageCell cell = new StageCell(2, 2);
        snapshot.staticCells.Add(cell, new StageCellStaticState { bottomGroundKind = StageBottomGroundKind.GroundOneWay, colorName = "Blue", exitDirection = GroundOneWay.ExitDirection.W });
        snapshot.entities.Add("block-a", new StageEntityState { entityId = "block-a", kind = StageEntityKind.Block, cell = cell, isActive = true });
        snapshot.entities.Add("block-b", new StageEntityState { entityId = "block-b", kind = StageEntityKind.Block, cell = cell, isActive = true });
        CellQueryResult query = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy().QueryCell(cell);

        Assert.That(query.staticState.bottomGroundKind, Is.EqualTo(StageBottomGroundKind.GroundOneWay));
        Assert.That(query.staticState.colorName, Is.EqualTo("Blue"));
        Assert.That(query.hasTopConflict, Is.True);
    }
    [Test] public void ZoneValidationCases() { Assert.That(V(Top("s"),Trig("t")).isValid,Is.True); Assert.That(V(Top("a"),Top("b")).isGameOverCandidate,Is.True); Assert.That(V(Bot("a"),Bot("b")).isValid,Is.False); Assert.That(V(new StageEntityState{entityId="x",isActive=true,cell=new StageCell(0,0),profile=P(StageZone.Bottom,MovementCapability.Pushable,OccupancyType.Surface)}).isValid,Is.False); }
    [Test] public void ZoneQueryCases() { StageBoardWorkingCopy b=B(Bot("bottom"),Top("z"),Top("a")); Assert.That(b.ValidateBoard().isValid,Is.True); CellQueryResult q=b.QueryCell(new StageCell(0,0)); Assert.That(q.bottomEntityId,Is.EqualTo("bottom")); Assert.That(q.solidTopEntityIds,Is.EqualTo(new[]{"a","z"})); b.State.staticCells.Add(new StageCell(0,0),new StageCellStaticState{hasFinale=true}); q=b.QueryCell(new StageCell(0,0)); Assert.That(q.hasFinale,Is.True); }
    [Test] public void StaticAndDynamicBottomConflictIsInvalid() { StageBoardWorkingCopy b=B(Bot("bottom")); b.State.staticCells.Add(new StageCell(0,0),new StageCellStaticState{bottomGroundKind=StageBottomGroundKind.Ground}); Assert.That(b.QueryCell(new StageCell(0,0)).hasBottomConflict,Is.True); Assert.That(b.ValidateBoard().isValid,Is.False); }
    [Test] public void MultipleStaticBottomConflictIsInvalid() { StageBoardSnapshot s=new StageBoardSnapshot(); s.staticCells.Add(new StageCell(0,0),new StageCellStaticState{bottomGroundKind=StageBottomGroundKind.GroundOneWay,hasBottomConflict=true}); StageBoardWorkingCopy b=StageBoard.FromSnapshot(s).CreateWorkingCopy(); Assert.That(b.QueryCell(new StageCell(0,0)).hasBottomConflict,Is.True); Assert.That(b.ValidateBoard().isValid,Is.False); }
    [Test] public void ObstacleCases() { StageBoardWorkingCopy b=B(Top("a")); b.State.staticCells.Add(new StageCell(0,0),new StageCellStaticState{hasObstacle=true}); Assert.That(b.ValidateBoard().isValid,Is.False); b=B(Bot("o")); b.State.staticCells.Add(new StageCell(0,0),new StageCellStaticState{hasObstacle=true}); Assert.That(b.ValidateBoard().isValid,Is.False); }
    private static StageObjectProfile P(StageZone z,MovementCapability m,OccupancyType o)=>new StageObjectProfile{zone=z,movement=m,occupancy=o};
    private static StageEntityState Top(string id)=>new StageEntityState{entityId=id,isActive=true,cell=new StageCell(0,0),profile=P(StageZone.Top,MovementCapability.Pushable,OccupancyType.Solid)};
    private static StageEntityState Trig(string id)=>new StageEntityState{entityId=id,isActive=true,cell=new StageCell(0,0),profile=P(StageZone.Top,MovementCapability.Static,OccupancyType.Trigger)};
    private static StageEntityState Bot(string id)=>new StageEntityState{entityId=id,isActive=true,cell=new StageCell(0,0),profile=P(StageZone.Bottom,MovementCapability.Static,OccupancyType.Surface)};
    private static StageBoardWorkingCopy B(params StageEntityState[] e){StageBoardSnapshot s=new StageBoardSnapshot();foreach(var x in e)s.entities.Add(x.entityId,x);return StageBoard.FromSnapshot(s).CreateWorkingCopy();}
    private static StageBoardValidationResult V(params StageEntityState[] e)=>B(e).ValidateBoard();
    [Test] public void ProfilePushCases(){StageBoardWorkingCopy b=PB(Push("a",0),Push("b",1)); Assert.That(BlockManager.TryCreatePushPlanPure(b,new StageCell(0,0),new StageCell(1,0),"p",out PushPlan p,out _),Is.True); Assert.That(p.movements.Count,Is.EqualTo(2)); Assert.That(BlockManager.TryCreatePushPlanPure(PB(Move("m")),new StageCell(0,0),new StageCell(1,0),"p",out _,out _),Is.False); Assert.That(BlockManager.TryCreatePushPlanPure(PB(Stat("s")),new StageCell(0,0),new StageCell(1,0),"p",out _,out _),Is.False);}
    [Test] public void MovementIntentCases(){StageBoardWorkingCopy b=PB(Move("m")); Assert.That(b.TryApply(new[]{new BoardMovement{entityId="m",from=new StageCell(0,0),to=new StageCell(1,0)}},MovementIntent.SelfMove),Is.True); b=PB(Push("p",0)); Assert.That(b.TryApply(new[]{new BoardMovement{entityId="p",from=new StageCell(0,0),to=new StageCell(1,0)}},MovementIntent.SelfMove),Is.False); b=PB(Stat("s")); Assert.That(b.TryApply(new[]{new BoardMovement{entityId="s",from=new StageCell(0,0),to=new StageCell(1,0)}},MovementIntent.SelfMove),Is.False); Assert.That(b.TryApply(new[]{new BoardMovement{entityId="s",from=new StageCell(0,0),to=new StageCell(1,0)}},MovementIntent.Push),Is.False);}
    [Test] public void ReservationOwnerAllowsOwnPush(){StageBoardWorkingCopy b=PB(Push("p",0)); b.ApplyReservations(null,new[]{"p"},"sep"); Assert.That(BlockManager.TryCreatePushPlanPure(b,new StageCell(0,0),new StageCell(1,0),"x",out _,out _),Is.False); Assert.That(BlockManager.TryCreatePushPlanPure(b,new StageCell(0,0),new StageCell(1,0),"sep",out _,out _),Is.True);}
    private static StageEntityState Push(string id,int x)=>new StageEntityState{entityId=id,isActive=true,cell=new StageCell(x,0),profile=P(StageZone.Top,MovementCapability.Pushable,OccupancyType.Solid)};
    private static StageEntityState Move(string id)=>new StageEntityState{entityId=id,isActive=true,cell=new StageCell(0,0),profile=P(StageZone.Top,MovementCapability.SelfMovable,OccupancyType.Solid)};
    private static StageEntityState Stat(string id)=>new StageEntityState{entityId=id,isActive=true,cell=new StageCell(0,0),profile=P(StageZone.Top,MovementCapability.Static,OccupancyType.Solid)};
    private static StageBoardWorkingCopy PB(params StageEntityState[] e){StageBoardSnapshot s=new StageBoardSnapshot();foreach(var x in e)s.entities.Add(x.entityId,x);s.staticCells.Add(new StageCell(2,0),new StageCellStaticState{bottomGroundKind=StageBottomGroundKind.Ground});s.staticCells.Add(new StageCell(1,0),new StageCellStaticState{bottomGroundKind=StageBottomGroundKind.Ground});return StageBoard.FromSnapshot(s).CreateWorkingCopy();}
    [Test] public void StageColorRules_UsesRgbContainmentAndExclusive(){Assert.That(StageColorRules.TryIsAvailable("Yellow","Red",false,out bool ok,out _),Is.True);Assert.That(ok,Is.True);StageColorRules.TryIsAvailable("Red","Blue",false,out ok,out _);Assert.That(ok,Is.False);StageColorRules.TryIsAvailable("Red","Blue",true,out ok,out _);Assert.That(ok,Is.True);StageColorRules.TryIsAvailable("Yellow","Green",false,out ok,out _);Assert.That(ok,Is.True);}
    [Test] public void StageColorRules_RejectsNullAndUnknown(){Assert.That(StageColorRules.TryIsAvailable(null,"Red",false,out _,out _),Is.False);Assert.That(StageColorRules.TryIsAvailable("Unknown","Red",false,out _,out _),Is.False);}

    [Test]
    public void PlayerActionResolver_DirectMoveCreatesPureMovementPlan()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");

        PlayerActionPlan result = PlayerActionResolver.ResolveDirectMove(
            snapshot,
            new SeparationSchedule(),
            new TurnInput { actorEntityId = "player", direction = Vector3.right });

        Assert.That(result.isValid, Is.True);
        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(result.actorMovement.from, Is.EqualTo(new StageCell(0, 0)));
        Assert.That(result.actorMovement.to, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(snapshot.entities["player"].cell, Is.EqualTo(new StageCell(0, 0)));
    }

    [Test]
    public void PlayerActionResolver_FinaleTriggerMarksClearWithoutBlockingMove()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        snapshot.staticCells[new StageCell(1, 0)].hasFinale = true;

        PlayerActionPlan result = ResolveRight(snapshot);

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(result.isStageClearCandidate, Is.True);
    }

    [Test]
    public void PlayerActionResolver_GroundOneWayRestrictsExit()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Blue");
        snapshot.staticCells[new StageCell(0, 0)] = new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.GroundOneWay,
            colorName = "Blue",
            exitDirection = GroundOneWay.ExitDirection.W
        };

        PlayerActionPlan blocked = ResolveRight(snapshot);
        Assert.That(blocked.failureReason, Is.EqualTo(PlayerActionFailureReason.ExitDirectionBlocked));
    }

    [Test]
    public void PlayerActionResolver_ExecutableSeparationReservationBlocksDestination()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        SeparationPlan separation = new SeparationPlan { ownerId = "separation:merged" };
        separation.reservedCells.Add(new StageCell(1, 0));
        SeparationSchedule schedule = new SeparationSchedule();
        schedule.plans.Add(separation);

        PlayerActionPlan result = PlayerActionResolver.ResolveDirectMove(
            snapshot,
            schedule,
            new TurnInput { actorEntityId = "player", direction = Vector3.right });

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Blocked));
        Assert.That(result.failureReason, Is.EqualTo(PlayerActionFailureReason.DestinationReserved));
    }

    [Test]
    public void PlayerActionResolver_DeferredSeparationDoesNotReserveDestination()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        SeparationPlan separation = new SeparationPlan { ownerId = "separation:merged", isDeferred = true };
        separation.reservedCells.Add(new StageCell(1, 0));
        SeparationSchedule schedule = new SeparationSchedule();
        schedule.plans.Add(separation);

        PlayerActionPlan result = PlayerActionResolver.ResolveDirectMove(
            snapshot,
            schedule,
            new TurnInput { actorEntityId = "player", direction = Vector3.right });

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
    }

    [Test]
    public void PlayerActionResolver_PushableSolidReturnsPushRequired()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        snapshot.entities.Add("block", new StageEntityState
        {
            entityId = "block",
            isActive = true,
            cell = new StageCell(1, 0),
            profile = P(StageZone.Top, MovementCapability.Pushable, OccupancyType.Solid)
        });

        PlayerActionPlan result = ResolveRight(snapshot);

        Assert.That(result.isValid, Is.True);
        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.PushRequired));
        Assert.That(result.failureReason, Is.EqualTo(PlayerActionFailureReason.PushRequired));
    }

    [Test]
    public void PlayerActionResolver_TopTriggerDoesNotBlockMove()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        snapshot.entities.Add("trigger", new StageEntityState
        {
            entityId = "trigger",
            isActive = true,
            cell = new StageCell(1, 0),
            profile = P(StageZone.Top, MovementCapability.Static, OccupancyType.Trigger)
        });

        PlayerActionPlan result = ResolveRight(snapshot);

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
    }

    [Test]
    public void PlayerActionResolver_DynamicBottomProvidesColorWithoutStaticGround()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Blue");
        snapshot.staticCells.Remove(new StageCell(1, 0));
        snapshot.entities.Add("destination-bottom", BottomSurface("destination-bottom", new StageCell(1, 0), "Blue"));

        PlayerActionPlan result = ResolveRight(snapshot);

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
    }

    [TestCase(0f, 0f)]
    [TestCase(1f, 1f)]
    [TestCase(2f, 0f)]
    [TestCase(0.5f, 0f)]
    public void PlayerActionResolver_RejectsNonCardinalUnitInput(float x, float y)
    {
        PlayerActionPlan result = PlayerActionResolver.ResolveDirectMove(
            ResolverBoard("Red"),
            new SeparationSchedule(),
            new TurnInput { actorEntityId = "player", direction = new Vector3(x, y, 0f) });

        Assert.That(result.isValid, Is.False);
        Assert.That(result.failureReason, Is.EqualTo(PlayerActionFailureReason.InvalidInput));
    }

    [Test]
    public void PlayerActionResolver_GroundOneWayAllowsMatchingExit()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Blue");
        snapshot.staticCells[new StageCell(0, 0)] = new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.GroundOneWay,
            colorName = "Blue",
            exitDirection = GroundOneWay.ExitDirection.D
        };

        Assert.That(ResolveRight(snapshot).outcome, Is.EqualTo(PlayerActionOutcome.Move));
    }

    [Test]
    public void PlayerActionResolver_RejectsObstacleAndMissingBottom()
    {
        StageBoardSnapshot obstacleBoard = ResolverBoard("Red");
        obstacleBoard.staticCells[new StageCell(1, 0)] = new StageCellStaticState { hasObstacle = true };
        Assert.That(ResolveRight(obstacleBoard).failureReason, Is.EqualTo(PlayerActionFailureReason.Obstacle));

        StageBoardSnapshot emptyBoard = ResolverBoard("Red");
        emptyBoard.staticCells.Remove(new StageCell(1, 0));
        Assert.That(ResolveRight(emptyBoard).failureReason, Is.EqualTo(PlayerActionFailureReason.MissingBottom));
    }

    [Test]
    public void PlayerActionResolver_AppliesGroundColorAndExclusiveRules()
    {
        StageBoardSnapshot unavailable = ResolverBoard("Red");
        unavailable.staticCells[new StageCell(1, 0)].colorName = "Blue";
        Assert.That(ResolveRight(unavailable).failureReason, Is.EqualTo(PlayerActionFailureReason.ColorBlocked));

        unavailable.staticCells[new StageCell(1, 0)].isExclusive = true;
        Assert.That(ResolveRight(unavailable).outcome, Is.EqualTo(PlayerActionOutcome.Move));
    }

    [TestCase(MovementCapability.Static)]
    [TestCase(MovementCapability.SelfMovable)]
    public void PlayerActionResolver_NonPushableSolidBlocksMove(MovementCapability movement)
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        snapshot.entities.Add("solid", new StageEntityState
        {
            entityId = "solid",
            isActive = true,
            cell = new StageCell(1, 0),
            profile = P(StageZone.Top, movement, OccupancyType.Solid)
        });

        PlayerActionPlan result = ResolveRight(snapshot);

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Blocked));
        Assert.That(result.failureReason, Is.EqualTo(PlayerActionFailureReason.SolidBlocked));
    }

    [Test]
    public void PlayerActionResolver_SolidConflictIsInvalid()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        snapshot.entities.Add("solid-a", SolidAt("solid-a", MovementCapability.Pushable));
        snapshot.entities.Add("solid-b", SolidAt("solid-b", MovementCapability.Pushable));

        PlayerActionPlan result = ResolveRight(snapshot);

        Assert.That(result.isValid, Is.False);
        Assert.That(result.failureReason, Is.EqualTo(PlayerActionFailureReason.SolidConflict));
    }

    [Test]
    public void PlayerActionResolver_GenericTriggerIsNotStageClear()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        snapshot.entities.Add("trigger", new StageEntityState
        {
            entityId = "trigger",
            isActive = true,
            cell = new StageCell(1, 0),
            profile = P(StageZone.Top, MovementCapability.Static, OccupancyType.Trigger)
        });

        PlayerActionPlan result = ResolveRight(snapshot);

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(result.isStageClearCandidate, Is.False);
    }

    [Test]
    public void PlayerActionResolver_InvalidScheduleAndReservedActorAreRejected()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        SeparationSchedule invalid = new SeparationSchedule { isValid = false, invalidReason = "invalid test schedule" };
        Assert.That(PlayerActionResolver.ResolveDirectMove(snapshot, invalid,
            new TurnInput { actorEntityId = "player", direction = Vector3.right }).failureReason,
            Is.EqualTo(PlayerActionFailureReason.InvalidSchedule));

        SeparationPlan plan = new SeparationPlan { ownerId = "separation:test" };
        plan.reservedEntities.Add("player");
        SeparationSchedule reserved = new SeparationSchedule();
        reserved.plans.Add(plan);
        Assert.That(PlayerActionResolver.ResolveDirectMove(snapshot, reserved,
            new TurnInput { actorEntityId = "player", direction = Vector3.right }).failureReason,
            Is.EqualTo(PlayerActionFailureReason.ActorReserved));
    }

    [Test]
    public void PlayerActionResolver_FailedActionDoesNotMutateSnapshot()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        snapshot.staticCells[new StageCell(1, 0)].colorName = "Blue";

        PlayerActionResolver.ResolveDirectMove(snapshot, new SeparationSchedule(),
            new TurnInput { actorEntityId = "player", direction = Vector3.right });

        Assert.That(snapshot.entities["player"].cell, Is.EqualTo(new StageCell(0, 0)));
        Assert.That(snapshot.staticCells[new StageCell(1, 0)].colorName, Is.EqualTo("Blue"));
    }

    [Test]
    public void PlayerActionResolver_ResolveCreatesSinglePushAndActorMovement()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        AddGround(snapshot, 2, "Red");
        snapshot.entities.Add("block", SolidAt("block", MovementCapability.Pushable));

        PlayerActionPlan result = ResolveRightFull(snapshot);

        Assert.That(result.isValid, Is.True);
        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(result.pushPlan, Is.Not.Null);
        Assert.That(result.pushPlan.movements.Count, Is.EqualTo(1));
        Assert.That(result.pushPlan.movements[0].from, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(result.pushPlan.movements[0].to, Is.EqualTo(new StageCell(2, 0)));
        Assert.That(result.actorMovement.to, Is.EqualTo(new StageCell(1, 0)));
        Assert.That(snapshot.entities["player"].cell, Is.EqualTo(new StageCell(0, 0)));
        Assert.That(snapshot.entities["block"].cell, Is.EqualTo(new StageCell(1, 0)));
    }

    [Test]
    public void PlayerActionResolver_ResolveCreatesDeterministicChainPush()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        AddGround(snapshot, 2, "Red");
        AddGround(snapshot, 3, "Red");
        snapshot.entities.Add("block-a", SolidAt("block-a", MovementCapability.Pushable));
        snapshot.entities.Add("block-b", new StageEntityState
        {
            entityId = "block-b",
            isActive = true,
            cell = new StageCell(2, 0),
            profile = P(StageZone.Top, MovementCapability.Pushable, OccupancyType.Solid)
        });

        PlayerActionPlan result = ResolveRightFull(snapshot);

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(result.pushPlan.movements.Count, Is.EqualTo(2));
        Assert.That(result.pushPlan.movements[0].entityId, Is.EqualTo("block-b"));
        Assert.That(result.pushPlan.movements[0].to, Is.EqualTo(new StageCell(3, 0)));
        Assert.That(result.pushPlan.movements[1].entityId, Is.EqualTo("block-a"));
        Assert.That(result.pushPlan.movements[1].to, Is.EqualTo(new StageCell(2, 0)));
    }

    [Test]
    public void PlayerActionResolver_PushFailsWhenLastBlockHasNoDestinationBottom()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        snapshot.entities.Add("block", SolidAt("block", MovementCapability.Pushable));

        PlayerActionPlan result = ResolveRightFull(snapshot);

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Blocked));
        Assert.That(result.failureReason, Is.EqualTo(PlayerActionFailureReason.PushBlocked));
    }

    [Test]
    public void PlayerActionResolver_PushedBlockObeysGroundOneWayExit()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        AddGround(snapshot, 2, "Red");
        snapshot.staticCells[new StageCell(1, 0)] = new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.GroundOneWay,
            colorName = "Red",
            exitDirection = GroundOneWay.ExitDirection.W
        };
        snapshot.entities.Add("block", SolidAt("block", MovementCapability.Pushable));

        Assert.That(ResolveRightFull(snapshot).failureReason, Is.EqualTo(PlayerActionFailureReason.PushBlocked));
    }

    [Test]
    public void PlayerActionResolver_GroundOneWayAndDynamicBottomIsInvalidBoard()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        snapshot.staticCells[new StageCell(1, 0)] = new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.GroundOneWay,
            colorName = "Red",
            exitDirection = GroundOneWay.ExitDirection.D
        };
        snapshot.entities.Add("bottom", BottomSurface("bottom", new StageCell(1, 0), "Red"));

        PlayerActionPlan result = ResolveRightFull(snapshot);

        Assert.That(result.isValid, Is.False);
        Assert.That(result.failureReason, Is.EqualTo(PlayerActionFailureReason.InvalidBoard));
    }

    [Test]
    public void PlayerActionResolver_ExecutableReservationBlocksPushChainEntity()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        AddGround(snapshot, 2, "Red");
        AddGround(snapshot, 3, "Red");
        snapshot.entities.Add("block-a", SolidAt("block-a", MovementCapability.Pushable));
        snapshot.entities.Add("block-b", new StageEntityState
        {
            entityId = "block-b",
            isActive = true,
            cell = new StageCell(2, 0),
            profile = P(StageZone.Top, MovementCapability.Pushable, OccupancyType.Solid)
        });
        SeparationPlan reservation = new SeparationPlan { ownerId = "separation:test" };
        reservation.reservedEntities.Add("block-b");
        SeparationSchedule schedule = new SeparationSchedule();
        schedule.plans.Add(reservation);

        PlayerActionPlan result = PlayerActionResolver.Resolve(snapshot, schedule,
            new TurnInput { actorEntityId = "player", direction = Vector3.right });

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Blocked));
        Assert.That(result.failureReason, Is.EqualTo(PlayerActionFailureReason.PushBlocked));
    }

    [Test]
    public void PlayerActionResolver_DeferredReservationDoesNotBlockPushChain()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        AddGround(snapshot, 2, "Red");
        snapshot.entities.Add("block", SolidAt("block", MovementCapability.Pushable));
        SeparationPlan reservation = new SeparationPlan { ownerId = "separation:test", isDeferred = true };
        reservation.reservedEntities.Add("block");
        reservation.reservedCells.Add(new StageCell(2, 0));
        SeparationSchedule schedule = new SeparationSchedule();
        schedule.plans.Add(reservation);

        PlayerActionPlan result = PlayerActionResolver.Resolve(snapshot, schedule,
            new TurnInput { actorEntityId = "player", direction = Vector3.right });

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
    }

    [Test]
    public void PlayerActionResolver_TriggerAtPushDestinationDoesNotBlockBlock()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        AddGround(snapshot, 2, "Red");
        snapshot.entities.Add("block", SolidAt("block", MovementCapability.Pushable));
        snapshot.entities.Add("trigger", new StageEntityState
        {
            entityId = "trigger",
            isActive = true,
            cell = new StageCell(2, 0),
            profile = P(StageZone.Top, MovementCapability.Static, OccupancyType.Trigger)
        });

        PlayerActionPlan result = ResolveRightFull(snapshot);

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
    }

    [Test]
    public void PlayerActionResolver_PushOntoFinaleMarksActorClearCandidate()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        AddGround(snapshot, 2, "Red");
        snapshot.staticCells[new StageCell(1, 0)].hasFinale = true;
        snapshot.entities.Add("block", SolidAt("block", MovementCapability.Pushable));

        PlayerActionPlan result = ResolveRightFull(snapshot);

        Assert.That(result.outcome, Is.EqualTo(PlayerActionOutcome.Move));
        Assert.That(result.isStageClearCandidate, Is.True);
    }

    [Test]
    public void PlayerActionResolver_ResolvedPushPlanCanBeAppliedWithoutInvalidBoard()
    {
        StageBoardSnapshot snapshot = ResolverBoard("Red");
        AddGround(snapshot, 2, "Red");
        snapshot.entities.Add("block", SolidAt("block", MovementCapability.Pushable));
        PlayerActionPlan result = ResolveRightFull(snapshot);
        StageBoardWorkingCopy board = StageBoard.FromSnapshot(snapshot).CreateWorkingCopy();

        Assert.That(board.TryApply(result.pushPlan.movements, MovementIntent.Push, "player-action:player"), Is.True);
        Assert.That(board.TryApply(new[] { result.actorMovement }, MovementIntent.SelfMove, "player-action:player"), Is.True);
        Assert.That(board.ValidateBoard().isValid, Is.True);
    }

    private static PlayerActionPlan ResolveRight(StageBoardSnapshot snapshot)
    {
        return PlayerActionResolver.ResolveDirectMove(
            snapshot,
            new SeparationSchedule(),
            new TurnInput { actorEntityId = "player", direction = Vector3.right });
    }

    private static PlayerActionPlan ResolveRightFull(StageBoardSnapshot snapshot)
    {
        return PlayerActionResolver.Resolve(
            snapshot,
            new SeparationSchedule(),
            new TurnInput { actorEntityId = "player", direction = Vector3.right });
    }

    private static StageBoardSnapshot ResolverBoard(string currentColor)
    {
        StageBoardSnapshot snapshot = new StageBoardSnapshot { currentColor = currentColor };
        snapshot.entities.Add("player", new StageEntityState
        {
            entityId = "player",
            isActive = true,
            cell = new StageCell(0, 0),
            profile = P(StageZone.Top, MovementCapability.SelfMovable, OccupancyType.Solid)
        });
        snapshot.staticCells.Add(new StageCell(0, 0), new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.Ground,
            colorName = currentColor
        });
        snapshot.staticCells.Add(new StageCell(1, 0), new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.Ground,
            colorName = currentColor
        });
        return snapshot;
    }

    private static StageEntityState BottomSurface(string id, StageCell cell, string colorName)
    {
        return new StageEntityState
        {
            entityId = id,
            isActive = true,
            cell = cell,
            colorName = colorName,
            profile = P(StageZone.Bottom, MovementCapability.Static, OccupancyType.Surface)
        };
    }

    private static StageEntityState SolidAt(string id, MovementCapability movement)
    {
        return new StageEntityState
        {
            entityId = id,
            isActive = true,
            cell = new StageCell(1, 0),
            profile = P(StageZone.Top, movement, OccupancyType.Solid)
        };
    }

    private static void AddGround(StageBoardSnapshot snapshot, int x, string colorName)
    {
        snapshot.staticCells.Add(new StageCell(x, 0), new StageCellStaticState
        {
            bottomGroundKind = StageBottomGroundKind.Ground,
            colorName = colorName
        });
    }
}
