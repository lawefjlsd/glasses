using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GQObjectType = GridQueryManager.ObjectType;

[System.Serializable]
/// <summary>
/// 되돌리기에 필요한 오브젝트 상태를 보관합니다.
/// </summary>
public class ObjectState
{
    public GameObject targetObject;
    public GQObjectType objectType;
    public Vector3 position;
    public bool isActive;
    public int remainingTurns;
    public bool isNewMerged;
    public MergeColorEffect mergeColorEffect;
    public long mergeBatchId;
    public long mergeEventOrder;
    public List<Vector3> pendingSeparationPath = new List<Vector3>();
}

[System.Serializable]
/// <summary>
/// 되돌리기에 필요한 한 턴의 상태를 보관합니다.
/// </summary>
public class TurnData
{
    public Vector3 playerPosition;
    public string systemColor;
    public List<ObjectState> savedState = new List<ObjectState>();
}
/// <summary>
/// 턴 단위 되돌리기를 저장하고 복원합니다.
/// </summary>
public class UndoManager : MonoBehaviour
{
    public static UndoManager Instance { get; private set; }

    [Header("Undo Speed Settings")]
    public float undoSpeed = 8f;

    private Stack<TurnData> undoStack = new Stack<TurnData>();
    private List<GameObject> registryObjects = new List<GameObject>();
    private Player player;
    private bool isUndoAnimation = false;
    public bool IsUndoRunning => isUndoAnimation;

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Unity 초기화 단계에서 싱글턴 또는 필수 참조를 준비합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Unity 시작 단계에서 런타임 상태와 참조를 초기화합니다.
    /// </summary>
    private void Start()
    {
        player = FindObjectOfType<Player>();

        foreach (var b in FindObjectsOfType<Block>(true)) registryObjects.Add(b.gameObject);
        foreach (var bg in FindObjectsOfType<BlockBackground>(true)) registryObjects.Add(bg.gameObject);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: 매 프레임 입력과 화면 또는 게임 상태를 갱신합니다.
    /// </summary>
    private void Update()
    {
        if (player != null && player.IsMovingStatus()) return;
        if (UIManager.Instance != null && UIManager.Instance.IsOverlayOpen) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (isUndoAnimation) return;

        if (Input.GetKeyDown(KeyCode.Z))
        {
            ExecuteUndo();
        }
    }


    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private void ExecuteUndo()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("[UndoManager] 되돌릴 턴이 없음");
            return;
        }

        TurnData previousTurn = undoStack.Pop();

        StartCoroutine(AnimateUndo(previousTurn));
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: UI 등 외부 명령에서 현재 턴을 되돌립니다.
    /// </summary>
    public void Undo()
    {
        if (player != null && player.IsMovingStatus()) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (isUndoAnimation) return;
        ExecuteUndo();
    }


    /// <summary>
    /// 입력: previousTurn. 출력: 코루틴 완료. 역할: 저장된 이전 턴으로 오브젝트와 색상을 복원하는 애니메이션을 실행합니다.
    /// </summary>
    private IEnumerator AnimateUndo(TurnData previousTurn)
    {
        isUndoAnimation = true;

        GameManager.Instance.RestoreColor(previousTurn.systemColor);

        List<Coroutine> moveCoroutines = new List<Coroutine>();
        PrepareUndoMovement(previousTurn, moveCoroutines);

        UndoMerge(previousTurn);
        yield return null;

        yield return StartCoroutine(ExecuteAllMovementsSimultaneously(moveCoroutines));

        UndoSeparate(previousTurn);

        FinalizeObjectsState(previousTurn);

        isUndoAnimation = false;
        Debug.Log("[UndoManager] Undo 연출 완료");
    }


    /// <summary>
    /// 입력: previousTurn, moveCoroutines. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private void PrepareUndoMovement(TurnData previousTurn, List<Coroutine> moveCoroutines)
    {
        player.ResetTargetPosition(previousTurn.playerPosition);
        moveCoroutines.Add(StartCoroutine(MovementHelper.Instance.SmoothMoveCoroutine(player.transform, previousTurn.playerPosition, undoSpeed)));

        foreach (var state in previousTurn.savedState)
        {
            if (state.targetObject == null || state.isNewMerged) continue;
            float distance = Vector3.Distance(state.targetObject.transform.position, state.position);
            if (distance > 0.01f)
            {
                var c = StartCoroutine(MovementHelper.Instance.SmoothMoveCoroutine(state.targetObject.transform, state.position, undoSpeed));
                moveCoroutines.Add(c);
            }
        }
    }

    /// <summary>
    /// 입력: moveCoroutines. 출력: 코루틴 완료. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private IEnumerator ExecuteAllMovementsSimultaneously(List<Coroutine> moveCoroutines)
    {
        foreach (var coroutine in moveCoroutines) yield return coroutine;
    }


    /// <summary>
    /// 입력: previousTurn. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    private void FinalizeObjectsState(TurnData previousTurn)
    {
        foreach (var state in previousTurn.savedState)
        {
            if (state.targetObject == null) continue;
            state.targetObject.transform.position = state.position;
            state.targetObject.SetActive(state.isActive);
        }
    }


    /// <summary>
    /// 입력: previousTurn. 출력: 없음. 역할: Undo 상태 복원 절차를 처리합니다.
    /// </summary>
    private void UndoMerge(TurnData previousTurn)
    {
        foreach (var state in previousTurn.savedState)
        {
            if (state.targetObject == null) continue;

            Debug.Log($"{state.isNewMerged}");
            if (state.isNewMerged)
            {
                Destroy(state.targetObject);
            }
            else if (state.targetObject.GetComponent<BlockMerged>() == null && state.isActive)
            {
                state.targetObject.SetActive(true);
            }
        }
    }


    /// <summary>
    /// 입력: previousTurn. 출력: 없음. 역할: Undo 상태 복원 절차를 처리합니다.
    /// </summary>
    private void UndoSeparate(TurnData previousTurn)
    {
        foreach (var state in previousTurn.savedState)
        {
            if (state.targetObject == null || state.isNewMerged) continue;

            BlockMerged mergedScript = state.targetObject.GetComponent<BlockMerged>();

            if (mergedScript != null)
            {

                mergedScript.remainingTurns = state.remainingTurns;
                mergedScript.isJustCreated = false;
                state.targetObject.transform.position = state.position;
                mergedScript.pendingSeparationPath = state.pendingSeparationPath == null
                    ? new List<Vector3>()
                    : new List<Vector3>(state.pendingSeparationPath);
                mergedScript.mergeColorEffect = state.mergeColorEffect;
                mergedScript.mergeBatchId = state.mergeBatchId;
                mergedScript.mergeEventOrder = state.mergeEventOrder;
            }


            state.targetObject.SetActive(state.isActive);
        }
    }


    /// <summary>
    /// 입력: mergedObject, mergedScript. 출력: 없음. 역할: 런타임 데이터를 관리 목록에 등록합니다.
    /// </summary>
    public void RegisterNewlyMergedObject(GameObject mergedObject, BlockMerged mergedScript)
    {
        if (undoStack.Count == 0) return;

        TurnData currentTurn = (undoStack.Peek());
        currentTurn.savedState.Add(new ObjectState
        {
            targetObject = mergedObject,
            objectType = GQObjectType.ColorBlockMerged,
            position = mergedObject.transform.position,
            isActive = mergedObject.activeSelf,
            remainingTurns = mergedScript.remainingTurns,
            isNewMerged = mergedScript.isJustCreated,
            mergeColorEffect = mergedScript.mergeColorEffect,
            mergeBatchId = mergedScript.mergeBatchId,
            mergeEventOrder = mergedScript.mergeEventOrder,
            pendingSeparationPath = new List<Vector3>(mergedScript.pendingSeparationPath)
        });
    }

    /// <summary>
    /// 입력: newObject. 출력: 없음. 역할: 런타임 데이터를 관리 목록에 등록합니다.
    /// </summary>
    public void RegisterDynamicObject(GameObject newObject)
    {
        if (!registryObjects.Contains(newObject)) registryObjects.Add(newObject);
    }


    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    public void SaveCurrentTurn()
    {

        TurnData turn = new TurnData
        {
            playerPosition = player.transform.position,
            systemColor = GameManager.currentColor,
            savedState = new List<ObjectState>()
        };

        foreach (var currentObject in registryObjects)
        {
            if (currentObject == null) continue;

            BlockMerged mergedScript = currentObject.GetComponent<BlockMerged>();
            if (mergedScript != null)
            {
                GQObjectType type = GQObjectType.ColorBlockMerged;
                int leftTurns = mergedScript.remainingTurns;
                bool merged = mergedScript.isJustCreated;
                AddState(
                    turn,
                    currentObject,
                    type,
                    leftTurns,
                    merged,
                    mergedScript.pendingSeparationPath,
                    mergedScript.mergeColorEffect,
                    mergedScript.mergeBatchId,
                    mergedScript.mergeEventOrder);

                Debug.Log($"[UndoManager] SaveCurrentTurn - isJustCreated: {merged}, mergedScript 존재: {mergedScript != null}");
            }
            else
            {
                GQObjectType type = GridQueryManager.Instance.GetObjectTypeAt(currentObject.transform.position);
                AddState(turn, currentObject, type);
                Debug.Log($"[UndoManager] SaveCurrentTurn - type: {type}, mergedScript 존재: {mergedScript != null}");
            }

        }

        undoStack.Push(turn);
    }

    /// <summary>
    /// 입력: turn, obj, type, leftTurns, merged, pendingSeparationPath. 출력: 없음. 역할: 관리 데이터에 항목을 추가합니다.
    /// </summary>
    private void AddState(
        TurnData turn,
        GameObject obj,
        GQObjectType type,
        int leftTurns = 0,
        bool merged = false,
        List<Vector3> pendingSeparationPath = null,
        MergeColorEffect mergeColorEffect = MergeColorEffect.Add,
        long mergeBatchId = 0,
        long mergeEventOrder = 0)
    {
        if (obj == null) return;

        ObjectState state = new ObjectState
        {
            targetObject = obj,
            position = obj.transform.position,
            isActive = obj.activeSelf,
            remainingTurns = leftTurns,
            isNewMerged = merged,
            mergeColorEffect = mergeColorEffect,
            mergeBatchId = mergeBatchId,
            mergeEventOrder = mergeEventOrder,
            pendingSeparationPath = pendingSeparationPath == null
                ? new List<Vector3>()
                : new List<Vector3>(pendingSeparationPath)
        };
        turn.savedState.Add(state);
        return;
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할:  필요한 게임 로직을 처리합니다.
    /// </summary>
    public void ClearStack()
    {
        undoStack.Clear();
    }
}
