using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using GQObjectType = GridQueryManager.ObjectType;

/// <summary>
/// 블록의 연쇄 밀기와 병합을 처리합니다.
/// </summary>
public class BlockManager : MonoBehaviour
{
    public static BlockManager Instance { get; private set; }

    [Header("Settings")]
    public float blockSpeed = 8f;
    private long activeMergeBatchId = -1;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Unity 초기화 단계에서 싱글턴 또는 필수 참조를 준비합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    /// <summary>
    /// 입력: position, input, path, isBeforeBlock. 출력: bool. 역할: 지정 방향으로 블록 연쇄 밀기가 가능한지와 경로를 계산합니다.
    /// </summary>
    public bool CanPushBlockContinuously(Vector3 position, Vector3 input ,List<Vector3> path, bool isBeforeBlock = false)
    {
        if (!GridQueryManager.Instance.CanExitGroundOneWay(position, input))
            return false;

        Vector3 nextPosition = position + input;
        GQObjectType objectType = GridQueryManager.Instance.GetObjectTypeAt(nextPosition);

        switch (objectType)
        {
            case GQObjectType.Obstacle:
            case GQObjectType.Empty:
                Debug.Log("[Blockmanager] 이동 불가: 막혀있거나 빈 칸");
                return false;

            case GQObjectType.ColorBlock:
                path.Add(nextPosition);
                return CanPushBlockContinuously(nextPosition, input, path, true);

            case GQObjectType.Ground:
            case GQObjectType.GroundOneWay:
            case GQObjectType.ColorBlockBackground:
            case GQObjectType.ColorBlockMerged:
                path.Add(nextPosition);
                return true;
            default:
                Debug.Log("[Blockmanager] 밀기 불가: 정의되지 않은 구역");
                return false;
        }
    }
    /// <summary>
    /// 입력: currentPos, input. 출력: 코루틴 완료. 역할: 이동 또는 이동 완료 후속 처리를 수행합니다.
    /// </summary>
    private IEnumerator MoveOrMergeAt(Vector3 currentPos, Vector3 input)
    {
        Collider2D blockCollider = GridQueryManager.Instance.GetColliderAt(currentPos, GridQueryManager.Instance.colorBlockLayer);
        if (blockCollider == null) yield break;

        Vector3 targetPos = currentPos + input;
        GridQueryManager.ObjectType targetType = GridQueryManager.Instance.GetObjectTypeAt(targetPos);

        if (targetType == GridQueryManager.ObjectType.ColorBlockBackground)
            yield return StartCoroutine(TryMergeBlock(blockCollider, targetPos));
        else
            yield return StartCoroutine(MovementHelper.Instance.SmoothMoveCoroutine(blockCollider.transform, targetPos, blockSpeed));
    }


    /// <summary>
    /// 입력: startPosition, input, path. 출력: 코루틴 완료. 역할: 계산된 경로의 블록들을 순서대로 밀어냅니다.
    /// </summary>
    public IEnumerator ExecutePushChain(Vector3 startPosition, Vector3 input, List<Vector3> path)
    {
        long previousMergeBatchId = activeMergeBatchId;
        activeMergeBatchId = GameManager.Instance.GetNextMergeBatchId();

        List<Coroutine> moves = new List<Coroutine>();


        for (int i = path.Count - 1; i >= 0; i--)
        {
            Vector3 currentPos = path[i];
            moves.Add(StartCoroutine(MoveOrMergeAt(currentPos, input)));
        }


        moves.Add(StartCoroutine(MoveOrMergeAt(startPosition, input)));

        foreach (var move in moves)
            yield return move;

        activeMergeBatchId = previousMergeBatchId;
    }


    /// <summary>
    /// 입력: 없음. 출력: 코루틴 완료. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    public IEnumerator ResolveMergesOnBoard()
    {
        long previousMergeBatchId = activeMergeBatchId;
        activeMergeBatchId = GameManager.Instance.GetNextMergeBatchId();

        List<Block> blocks = new List<Block>(FindObjectsOfType<Block>());
        blocks.Sort((left, right) => left.ColorEffect.CompareTo(right.ColorEffect));

        foreach (Block block in blocks)
        {
            if (block == null || !block.isActiveAndEnabled) continue;

            Collider2D blockCollider = block.GetComponent<Collider2D>();
            if (blockCollider == null) continue;

            Collider2D backgroundCollider = GridQueryManager.Instance.GetColliderAt(
                block.transform.position,
                GridQueryManager.Instance.colorBlockBackgroundLayer);

            if (backgroundCollider != null)
                yield return StartCoroutine(TryMergeBlock(blockCollider, block.transform.position));
        }

        activeMergeBatchId = previousMergeBatchId;
    }


    /// <summary>
    /// 입력: blockCollider, blockTargetPosition. 출력: 코루틴 완료. 역할: 블록과 배경의 병합 가능 여부를 검사하고 병합을 수행합니다.
    /// </summary>
    private IEnumerator TryMergeBlock(Collider2D blockCollider, Vector3 blockTargetPosition)
    {
        Block blockScript = blockCollider.GetComponent<Block>();
        Collider2D backgroundCollider = GridQueryManager.Instance.GetColliderAt(blockTargetPosition, GridQueryManager.Instance.colorBlockBackgroundLayer);
        BlockBackground blockBackgroundScript = backgroundCollider?.GetComponent<BlockBackground>();

        if (blockScript == null || blockBackgroundScript == null) yield break;

        bool canMerge = blockScript.CanMerge && GameManager.Instance.CanMergeWith(blockScript.colorName, blockBackgroundScript.acceptedColorName);

        if (!canMerge)
        {
            yield return StartCoroutine(MovementHelper.Instance.SmoothMoveCoroutine(blockCollider.transform, blockTargetPosition, blockSpeed));
            yield break;
        }

        GameObject mergedPrefab = blockScript.GetMergedBlock();
        Vector3 blockCurrentPosition = blockCollider.transform.position;

        yield return StartCoroutine(MovementHelper.Instance.SmoothMoveCoroutine(blockCollider.transform, blockTargetPosition, blockSpeed));

        backgroundCollider.gameObject.SetActive(false);
        blockCollider.gameObject.SetActive(false);

        GameObject merged = Instantiate(mergedPrefab, blockTargetPosition, Quaternion.identity);
        merged.SetActive(true);

        BlockMerged mergedScript = merged.GetComponent<BlockMerged>();
        if (mergedScript != null)
        {
            mergedScript.InitializeTimer(
                blockCurrentPosition,
                blockScript.gameObject,
                backgroundCollider.gameObject,
                blockBackgroundScript.mergeDurationTurns,
                blockScript.ColorEffect,
                GetActiveMergeBatchId(),
                GameManager.Instance.GetNextMergeEventOrder()
            );
            UndoManager.Instance.RegisterNewlyMergedObject(merged, mergedScript);
        }

        GameManager.Instance.RefreshCurrentColorFromMergedBlocks();
    }

    /// <summary>
    /// 입력: 없음. 출력: long. 역할: 실행 중인 병합 batch를 반환하고 독립 병합에는 새 batch를 할당합니다.
    /// </summary>
    private long GetActiveMergeBatchId()
    {
        return activeMergeBatchId >= 0
            ? activeMergeBatchId
            : GameManager.Instance.GetNextMergeBatchId();
    }
}
