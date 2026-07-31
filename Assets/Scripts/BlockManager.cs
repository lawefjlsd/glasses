using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using GQObjectType = GridQueryManager.ObjectType;

public class BlockManager : MonoBehaviour
{
    public static BlockManager Instance { get; private set; }

    [Header("Settings")]
    public float blockSpeed = 8f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ── Push Logic ─────────────────────────────────────────────

    /// <summary>
    /// 재귀적으로 다음 칸들을 확인해, 블록들이 연쇄적으로 끝까지 밀릴 수 있는지 검사한다.
    /// (Obstacle/Empty를 만나면 false, 그 외에는 true)
    /// </summary>
    public bool CanPushBlockContinuously(Vector3 position, Vector3 input ,List<Vector3> path, bool isBeforeBlock = false)
    {
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
            case GQObjectType.ColorBlockBackground:
            case GQObjectType.ColorBlockMerged:
                path.Add(nextPosition);
                return true;
            default:
                Debug.Log("[Blockmanager] 밀기 불가: 정의되지 않은 구역");
                return false;
        }
    }
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
    /// CanPushChain으로 확보된 경로를 따라 체인의 모든 블록을 동시에 이동/병합시킨다.
    /// startPosition 자체의 블록도 포함해서 처리한다.
    /// </summary>
    public IEnumerator ExecutePushChain(Vector3 startPosition, Vector3 input, List<Vector3> path)
    {
        List<Coroutine> moves = new List<Coroutine>();

        // 체인의 끝(먼 곳)부터 옮겨야 서로 겹치지 않음
        for (int i = path.Count - 1; i >= 0; i--)
        {
            Vector3 currentPos = path[i];
            moves.Add(StartCoroutine(MoveOrMergeAt(currentPos, input)));
        }

        // 시작 위치의 블록(체인의 첫 블록)도 이동
        moves.Add(StartCoroutine(MoveOrMergeAt(startPosition, input)));

        foreach (var move in moves)
            yield return move;
    }

    // ── Merge Logic ────────────────────────────────────────────
    /// <summary>
    /// 블록을 배경 위로 밀어 넣었을 때 병합을 시도한다.
    /// 색이 맞지 않으면 병합 대신 단순 이동으로 처리한다.
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
                blockBackgroundScript.mergeDurationTurns
            );
            UndoManager.Instance.RegisterNewlyMergedObject(merged, mergedScript);
        }

        GameManager.Instance.UpdateCurrentColor(blockScript.colorName);
    }
}