using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GroundOneWayTests
{
    private GameObject gameManagerObject;
    private GameObject tilemapManagerObject;
    private GameObject gridQueryObject;
    private GameObject blockManagerObject;
    private GameObject separationManagerObject;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 테스트마다 GameManager와 이동 규칙 관리자 오브젝트를 준비합니다.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        gameManagerObject = new GameObject("GameManagerTest");
        GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
        typeof(GameManager).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gameManager, null);

        tilemapManagerObject = new GameObject("TilemapManagerTest");
        TilemapManager tilemapManager = tilemapManagerObject.AddComponent<TilemapManager>();
        tilemapManager.tilemaps = new UnityEngine.Tilemaps.Tilemap[0];

        gridQueryObject = new GameObject("GridQueryManagerTest");
        GridQueryManager gridQueryManager = gridQueryObject.AddComponent<GridQueryManager>();
        gridQueryManager.groundOneWayLayer = 1 << LayerMask.NameToLayer(GroundOneWay.LayerName);
        typeof(GridQueryManager).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gridQueryManager, null);

        blockManagerObject = new GameObject("BlockManagerTest");
        blockManagerObject.AddComponent<BlockManager>();

        separationManagerObject = new GameObject("SeparationManagerTest");
        separationManagerObject.AddComponent<SeparationManager>();
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 테스트 중 생성한 오브젝트를 제거합니다.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(separationManagerObject);
        Object.DestroyImmediate(blockManagerObject);
        Object.DestroyImmediate(gridQueryObject);
        Object.DestroyImmediate(tilemapManagerObject);
        Object.DestroyImmediate(gameManagerObject);

        foreach (GroundOneWay groundOneWay in Object.FindObjectsOfType<GroundOneWay>())
            Object.DestroyImmediate(groundOneWay.gameObject);

        foreach (Block block in Object.FindObjectsOfType<Block>())
            Object.DestroyImmediate(block.gameObject);

        foreach (BlockMerged merged in Object.FindObjectsOfType<BlockMerged>())
            Object.DestroyImmediate(merged.gameObject);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 현재 색상에 포함된 GroundOneWay만 플레이어 진입을 허용하는지 검증합니다.
    /// </summary>
    [Test]
    public void CanPlayerEnter_RequiresMatchingCurrentColor()
    {
        GroundOneWay groundOneWay = CreateGroundOneWay(Vector3.zero, "Blue", GroundOneWay.ExitDirection.W);

        Assert.That(GridQueryManager.Instance.GetInfoMoveToColorPosition(Vector3.zero).Item1, Is.False);

        GameManager.Instance.UpdateCurrentColor("Blue");

        Assert.That(GridQueryManager.Instance.GetInfoMoveToColorPosition(Vector3.zero).Item1, Is.True);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: GroundOneWay가 지정 방향 이탈만 허용하는지 검증합니다.
    /// </summary>
    [Test]
    public void CanExit_OnlyAllowsConfiguredDirection()
    {
        GroundOneWay groundOneWay = CreateGroundOneWay(Vector3.zero, "Blue", GroundOneWay.ExitDirection.W);

        Assert.That(groundOneWay.CanExit(Vector3.up), Is.True);
        Assert.That(groundOneWay.CanExit(Vector3.right), Is.False);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Chain Push에 이탈 불가 GroundOneWay 블록이 있으면 전체 경로가 실패하는지 검증합니다.
    /// </summary>
    [Test]
    public void CanPushBlockContinuously_FailsWithoutPartialPathWhenBlockCannotExitGroundOneWay()
    {
        CreateGroundOneWay(Vector3.zero, "Blue", GroundOneWay.ExitDirection.W);
        CreateBlock(Vector3.zero);
        Physics2D.SyncTransforms();

        var path = new System.Collections.Generic.List<Vector3>();
        bool canPush = BlockManager.Instance.CanPushBlockContinuously(Vector3.zero, Vector3.right, path);

        Assert.That(canPush, Is.False);
        Assert.That(path, Is.Empty);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Separation 압력이 GroundOneWay 이탈 규칙에 막히면 remainingTurns가 1로 유지되는지 검증합니다.
    /// </summary>
    [Test]
    public void TickMergedBlocksTurn_DefersSeparationWhenGroundOneWayBlocksPushDirection()
    {
        CreateGroundOneWay(Vector3.right, "Blue", GroundOneWay.ExitDirection.W);
        CreateBlock(Vector3.right);

        GameObject mergedObject = new GameObject("MergedBlockTest");
        BlockMerged merged = mergedObject.AddComponent<BlockMerged>();
        merged.remainingTurns = 1;
        merged.originBlockPosition = Vector3.right;
        merged.isJustCreated = false;
        Physics2D.SyncTransforms();

        IEnumerator tick = SeparationManager.Instance.TickMergedBlocksTurn();
        while (tick.MoveNext()) { }

        Assert.That(merged.remainingTurns, Is.EqualTo(1));
    }

    /// <summary>
    /// 입력: position, colorName, exitDirection. 출력: GroundOneWay. 역할: 전용 Layer와 Trigger Collider를 갖는 GroundOneWay 테스트 오브젝트를 생성합니다.
    /// </summary>
    private GroundOneWay CreateGroundOneWay(Vector3 position, string colorName, GroundOneWay.ExitDirection exitDirection)
    {
        GameObject groundOneWayObject = new GameObject("GroundOneWayTest");
        groundOneWayObject.layer = LayerMask.NameToLayer(GroundOneWay.LayerName);
        groundOneWayObject.transform.position = position;

        BoxCollider2D collider = groundOneWayObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        GroundOneWay groundOneWay = groundOneWayObject.AddComponent<GroundOneWay>();
        groundOneWay.colorName = colorName;
        groundOneWay.exitDirection = exitDirection;
        return groundOneWay;
    }

    /// <summary>
    /// 입력: position. 출력: Block. 역할: ColorBlock Layer와 Collider를 갖는 테스트 블록을 생성합니다.
    /// </summary>
    private Block CreateBlock(Vector3 position)
    {
        GameObject blockObject = new GameObject("BlockTest");
        blockObject.layer = LayerMask.NameToLayer("ColorBlock");
        blockObject.transform.position = position;
        blockObject.AddComponent<BoxCollider2D>();
        return blockObject.AddComponent<Block>();
    }
}
