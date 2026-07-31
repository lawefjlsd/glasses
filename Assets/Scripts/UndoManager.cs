using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GQObjectType = GridQueryManager.ObjectType;

[System.Serializable]
public class ObjectState
{
    public GameObject targetObject;
    public GQObjectType objectType;
    public Vector3 position;
    public bool isActive;
    public int remainingTurns;
    public bool isNewMerged;
}

[System.Serializable]
public class TurnData
{
    public Vector3 playerPosition;
    public string systemColor;
    public List<ObjectState> savedState = new List<ObjectState>();
}
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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        player = FindObjectOfType<Player>();

        foreach (var b in FindObjectsOfType<Block>(true)) registryObjects.Add(b.gameObject);
        foreach (var bg in FindObjectsOfType<BlockBackground>(true)) registryObjects.Add(bg.gameObject);
    }

    private void Update()
    {
        if (player != null && player.IsMovingStatus()) return;
        if (isUndoAnimation) return;

        if (Input.GetKeyDown(KeyCode.Z))
        {
            ExecuteUndo();
        }
    }


    // ── Core Undo Logic ─────────────────────────────────────────────
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
    /// restore color -> save all coroutines in list -> "UndoMerge" -> Do coroutines -> "UndoSeparate"  -> Set on Grid
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

    // ── Undo Helpers ─────────────────────────────────────────────
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

    private IEnumerator ExecuteAllMovementsSimultaneously(List<Coroutine> moveCoroutines)
    {
        foreach (var coroutine in moveCoroutines) yield return coroutine;
    }


    /// <summary>
    /// Set the all objects on Grid
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
    // ── SetAcite manage ─────────────────────────────────────────────
    /// <summary>
    /// situation1 : move(merge) -> undo
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
    /// move(merge) -> separate -> undo
    /// </summary>
    private void UndoSeparate(TurnData previousTurn)
    {
        foreach (var state in previousTurn.savedState)
        {
            if (state.targetObject == null || state.isNewMerged) continue;

            BlockMerged mergedScript = state.targetObject.GetComponent<BlockMerged>();

            if (mergedScript != null)
            {
                // 상황 2: 분리되어 파괴/비활성화되었던 과거의 Merged 블록 데이터 및 데이터 완전 복구
                mergedScript.remainingTurns = state.remainingTurns;
                mergedScript.isJustCreated = false;
                state.targetObject.transform.position = state.position;
            }

            // 최종 On/Off 상태 장부대로 동기화 (상황 2로 인해 꺼져야 할 원래 블록들은 여기서 꺼짐)
            state.targetObject.SetActive(state.isActive);
        }
    }



    // ── Stack Manage ─────────────────────────────────────────────

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
            isNewMerged = mergedScript.isJustCreated
        });
    }

    public void RegisterDynamicObject(GameObject newObject)
    {
        if (!registryObjects.Contains(newObject)) registryObjects.Add(newObject);
    }

    ///<summary>
    ///Turn save (플레이어가 움직이기 직전에 호출)
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
                AddState(turn, currentObject, type, leftTurns, merged);

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

    private void AddState(TurnData turn, GameObject obj, GQObjectType type, int leftTurns = 0, bool merged = false)
    {
        if (obj == null) return;

        ObjectState state = new ObjectState
        {
            targetObject = obj,
            position = obj.transform.position,
            isActive = obj.activeSelf,
            remainingTurns = leftTurns,
            isNewMerged = merged
        };
        turn.savedState.Add(state);
        return;
    }

    public void ClearStack()
    {
        undoStack.Clear();
    }
}