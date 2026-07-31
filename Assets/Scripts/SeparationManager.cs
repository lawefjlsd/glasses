using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 병합 블록의 턴 경과와 분리를 처리합니다.
/// </summary>
public class SeparationManager : MonoBehaviour
{
    public static SeparationManager Instance { get; private set; }

    [Header("Separation Settings")]
    public float separateSpeed = 8f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }


    public void TickMergedBlocksTurn()
    {
        BlockMerged[] mergedBlocks = FindObjectsOfType<BlockMerged>();

        for (int i = mergedBlocks.Length - 1; i >= 0; i--)
        {
            BlockMerged merged = mergedBlocks[i];
            if (merged == null || merged.isInfinite) continue;

            if (merged.isJustCreated)
            {
                merged.isJustCreated = false;
                Debug.Log($"[SeparationManager] {merged.name} 방금 생성됨 - 남은 턴: {merged.remainingTurns}");
                continue;
            }


            if (merged.remainingTurns <= 1)
            {
                if (!PrepareSeparationPath(merged))
                {
                    merged.remainingTurns = 1;
                    Debug.LogWarning($"[SeparationManager] {merged.name} 분리 경로가 막혀 분리를 유예함");
                    continue;
                }

                ProcessSeparation(merged);
                continue;
            }

            merged.remainingTurns--;
            Debug.Log($"[SeparationManager] {merged.name} 남은 턴: {merged.remainingTurns}");

            if (merged.remainingTurns == 1 && !PrepareSeparationPath(merged))
            {
                merged.remainingTurns = 1;
                Debug.LogWarning($"[SeparationManager] {merged.name} 분리 경로가 막혀 분리를 유예함");
            }
        }
    }


    private bool PrepareSeparationPath(BlockMerged merged)
    {
        merged.pendingSeparationPath.Clear();

        Collider2D standingBlock = Physics2D.OverlapPoint(merged.originBlockPosition, GridQueryManager.Instance.colorBlockLayer);
        if (standingBlock == null) return true;

        Vector3 pushDirection = (merged.originBlockPosition - merged.transform.position).normalized;
        List<Vector3> path = new List<Vector3>();

        bool canPush = BlockManager.Instance.CanPushBlockContinuously(merged.originBlockPosition, pushDirection, path);

        if (!canPush)
        {
            merged.pendingSeparationPath = path;
            return false;
        }

        merged.pendingSeparationPath = path;
        return true;
    }


    private void ProcessSeparation(BlockMerged merged)
    {
        Collider2D standingBlock = Physics2D.OverlapPoint(merged.originBlockPosition, GridQueryManager.Instance.colorBlockLayer);

        if (standingBlock != null)
        {
            Vector3 pushDirection = (merged.originBlockPosition - merged.transform.position).normalized;


            List<Vector3> path = new List<Vector3>();
            if (!BlockManager.Instance.CanPushBlockContinuously(merged.originBlockPosition, pushDirection, path))
            {
                merged.remainingTurns = 1;
                Debug.LogWarning($"[SeparationManager] {merged.name} 분리 직전 경로가 막혀 유예로 되돌림");
                return;
            }

            merged.pendingSeparationPath = path;
            StartCoroutine(BlockManager.Instance.ExecutePushChain(merged.originBlockPosition, pushDirection, path));
            Debug.Log($"[SeparationManager] {merged.name} 분리 압력으로 체인 밀기 실행");
        }


        if (merged.originBackgroundObject != null)
            merged.originBackgroundObject.SetActive(true);

        merged.gameObject.SetActive(false);

        if (merged.originBlockPrefab != null)
        {
            GameObject originalBlock = merged.originBlockPrefab;
            originalBlock.SetActive(true);
            StartCoroutine(MovementHelper.Instance.SmoothMoveCoroutine(originalBlock.transform, merged.originBlockPosition, separateSpeed));
            Debug.Log($"[SeparationManager] {originalBlock.name} 원본 오브젝트 재활성화");
        }

        merged.pendingSeparationPath.Clear();
        Debug.Log($"[SeparationManager] {merged.name} 분리 완료");
    }


}
