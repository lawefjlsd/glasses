using UnityEngine;
public class BlockMerged : MonoBehaviour
{
    public string colorName;

    [HideInInspector] public MergeColorEffect mergeColorEffect = MergeColorEffect.Add;
    [HideInInspector] public long mergeBatchId;
    [HideInInspector] public long mergeEventOrder;

    [HideInInspector] public int remainingTurns;
    [HideInInspector] public bool isInfinite;
    [HideInInspector] public bool isJustCreated;
    [HideInInspector] public long createdTurnId = -1;
    [HideInInspector] public long separationDueTurn = -1;

    [HideInInspector] public Vector3 originBlockPosition;
    [HideInInspector] public GameObject originBlockPrefab;
    [HideInInspector] public GameObject originBackgroundObject;

    void Start()
    {
        if (GetComponent<RemainingTurnDisplay>() == null) gameObject.AddComponent<RemainingTurnDisplay>();
    }

    public void InitializeTimer(
        Vector3 blockPos,
        GameObject blockPrefab,
        GameObject backgroundObject,
        int durationTurns,
        MergeColorEffect colorEffect,
        long batchId,
        long eventOrder,
        long createdTurn = -1,
        long dueTurn = -1,
        bool infinite = false,
        bool justCreated = true)
    {
        originBlockPosition = blockPos;
        originBlockPrefab = blockPrefab;
        originBackgroundObject = backgroundObject;
        remainingTurns = durationTurns;
        mergeColorEffect = colorEffect;
        mergeBatchId = batchId;
        mergeEventOrder = eventOrder;

        isJustCreated = justCreated;
        createdTurnId = createdTurn;
        separationDueTurn = dueTurn >= createdTurn ? dueTurn : durationTurns <= 0 ? -1 : durationTurns;
        isInfinite = infinite || durationTurns <= 0;
    }

    /// <summary>Commit된 logic를 런타임 BlockMerged 컴포넌트에 sync</summary>
    public void ApplyCommittedState(
        StageEntityState state,
        Vector3 originBlockWorldPosition,
        GameObject blockObject,
        GameObject backgroundObject)
    {
        if (state == null) return;

        colorName = state.colorName;
        mergeColorEffect = state.mergeColorEffect;
        mergeBatchId = state.mergeBatchId;
        mergeEventOrder = state.mergeEventOrder;
        remainingTurns = state.remainingTurns;
        isInfinite = state.isInfinite;
        isJustCreated = state.isJustCreated;
        createdTurnId = state.createdTurnId;
        separationDueTurn = state.separationDueTurn;
        originBlockPosition = originBlockWorldPosition;

        if (blockObject != null) originBlockPrefab = blockObject;
        if (backgroundObject != null) originBackgroundObject = backgroundObject;
    }

}
