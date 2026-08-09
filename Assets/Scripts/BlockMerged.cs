using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 병합 블록의 상태와 분리 정보를 관리합니다.
/// </summary>
public class BlockMerged : MonoBehaviour
{
    public string colorName;

    [HideInInspector] public MergeColorEffect mergeColorEffect = MergeColorEffect.Add;
    [HideInInspector] public long mergeBatchId;
    [HideInInspector] public long mergeEventOrder;

    [HideInInspector] public int remainingTurns;
    [HideInInspector] public bool isInfinite;
    [HideInInspector] public bool isJustCreated;
    [HideInInspector] public bool isPendingSeparationPrepared = false;
    [HideInInspector] public Vector3 originBlockPosition;
    [HideInInspector] public GameObject originBlockPrefab;
    [HideInInspector] public GameObject originBackgroundObject;
    [HideInInspector] public List<Vector3> pendingSeparationPath = new List<Vector3>();

    public List<GameObject> originBlocks = new List<GameObject>();
    public List<GameObject> originBackgrounds = new List<GameObject>();

    private bool isSeparating = false;
    public bool IsSeparating => isSeparating;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    public void BeginSeparation() => isSeparating = true;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    public void EndSeparation() => isSeparating = false;

    void Start()
    {
        UndoManager.Instance.RegisterDynamicObject(this.gameObject);

        if (GetComponent<RemainingTurnDisplay>() == null)
            gameObject.AddComponent<RemainingTurnDisplay>();
    }
    /// <summary>
    /// 입력: blockPos, blockPrefab, backgroundObject, durationTurns. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    public void InitializeTimer(
        Vector3 blockPos,
        GameObject blockPrefab,
        GameObject backgroundObject,
        int durationTurns,
        MergeColorEffect colorEffect,
        long batchId,
        long eventOrder)
    {
        originBlockPosition = blockPos;
        originBlockPrefab = blockPrefab;
        originBackgroundObject = backgroundObject;
        remainingTurns = durationTurns;
        mergeColorEffect = colorEffect;
        mergeBatchId = batchId;
        mergeEventOrder = eventOrder;

        isJustCreated = true;

        if (durationTurns <= 0)
            isInfinite = true;

        Debug.Log($"[BlockMerged] InitializeTimer 호출됨 - isJustCreated: {isJustCreated}");
    }

}
