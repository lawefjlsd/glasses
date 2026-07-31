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


    private void FinalizeObjectsState(TurnData previousTurn)
    {
        foreach (var state in previousTurn.savedState)
        {
            if (state.targetObject == null) continue;
            state.targetObject.transform.position = state.position;
            state.targetObject.SetActive(state.isActive);
        }
    }


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
            }


            state.targetObject.SetActive(state.isActive);
        }
    }


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