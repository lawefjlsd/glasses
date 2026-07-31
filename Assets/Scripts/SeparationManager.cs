using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    /// <summary>
    /// each turn -> left turn calculate + trigger separation
    /// </summary>
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
            //remainingTurns == 1일때 separate될 경로 + 그에 맞춰 밀리는 block들의 경로는 obstacle로 간주

            merged.remainingTurns--;
            Debug.Log($"[SeparationManager] {merged.name} 남은 턴: {merged.remainingTurns}");

            if(merged.remainingTurns == 1)
                PrepareSeparationPath(merged);

            if (merged.remainingTurns <= 0)
                ProcessSeparation(merged);
        }
    }

    /// <summary>
    /// 분리 시 밀려날 경로를 미리 계산해 pendingSeparationPath에 저장한다 (가상 벽 판정용).
    /// 요구사항 3: 끝까지 밀 수 없으면 remainingTurns를 1로 되돌려 분리를 유예한다.
    /// </summary>
    private void PrepareSeparationPath(BlockMerged merged)
    {
        merged.pendingSeparationPath.Clear();

        Collider2D standingBlock = Physics2D.OverlapPoint(merged.originBlockPosition, GridQueryManager.Instance.colorBlockLayer);
        if (standingBlock == null) return; // 밀 블록 자체가 없으면 경로 불필요

        Vector3 pushDirection = (merged.originBlockPosition - merged.transform.position).normalized;
        List<Vector3> path = new List<Vector3>();

        bool canPush = BlockManager.Instance.CanPushBlockContinuously(merged.originBlockPosition, pushDirection, path);

        if (!canPush)
        {
            merged.remainingTurns = 1; // 요구사항 3: 유예 유지
            Debug.LogWarning($"[SeparationManager] {merged.name} 분리 경로가 막혀 있어 분리를 유예함");
            return;
        }

        merged.pendingSeparationPath = path;
    }

    /// <summary>
    /// 요구사항 1: 분리 시, origin 자리에 블록이 있으면 CanPushChain/ExecutePushChain으로
    /// player가 미는 것과 동일한 방식으로 체인 전체를 민다.
    /// </summary>
    private void ProcessSeparation(BlockMerged merged)
    {
        Collider2D standingBlock = Physics2D.OverlapPoint(merged.originBlockPosition, GridQueryManager.Instance.colorBlockLayer);

        if (standingBlock != null)
        {
            Vector3 pushDirection = (merged.originBlockPosition - merged.transform.position).normalized;

            // 이미 PrepareSeparationPath에서 검증된 경로를 그대로 사용
            if (merged.pendingSeparationPath.Count > 0 || BlockManager.Instance.CanPushBlockContinuously(merged.originBlockPosition, pushDirection, merged.pendingSeparationPath))
            {
                StartCoroutine(BlockManager.Instance.ExecutePushChain(merged.originBlockPosition, pushDirection, merged.pendingSeparationPath));
                Debug.Log($"[SeparationManager] {merged.name} 분리 압력으로 체인 밀기 실행");
            }
            else
            {
                merged.remainingTurns = 1; // 방어: 혹시 그 사이 상황이 바뀌었다면 다시 유예
                Debug.LogWarning($"[SeparationManager] {merged.name} 분리 직전 경로가 막혀 유예로 되돌림");
                return;
            }
        }

        // 원본 배경/블록 복구 (기존 로직 그대로)
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