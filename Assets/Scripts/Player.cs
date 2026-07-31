using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using GQObjectType = GridQueryManager.ObjectType;
/// <summary>
/// player의 move, push, merge를 담당하는 component.
/// move 여하는 GameManager의 currentColor
/// </summary>
public class Player : MonoBehaviour
{
    // ── State ──────────────────────────────────────────────────
    private bool isMoving = false;
    public bool IsMovingStatus() => isMoving;
    private Vector3 targetPosition;

    private float lastInputTime = 0f;
    private float keyHoldTime = 0f;
    private Vector3 lastInput = Vector3.zero;


    // ── Inspector ──────────────────────────────────────────────
    public Transform initialPosition;
    public GameObject clearUIPrefab;
    public float moveSpeed = 5f;

    // ── Input Delay ──────────────────────────────────────────────────

    public float inputCooldown = 0.2f;
    [SerializeField] private float inputHoldDelay = 0.3f;


    // ── Unity Lifecycle ────────────────────────────────────────
    private void Start()
    {
        transform.position = initialPosition.position;
        targetPosition = transform.position;
    }
    private void Update()
    {
        if (IsMovingStatus()) return;
        if (UndoManager.Instance != null && UndoManager.Instance.IsUndoRunning) return;
        if (GridQueryManager.Instance.CheckIfAnyBlockIsSeparating()) return;
        Move();
    }

    // ── Core Move Logic ────────────────────────────────────────

    /// <summary>
    /// 입력을 받아 플레이어를 이동시키거나, 막혀있는 칸이면 푸시/이동 판정을 분기한다.
    /// </summary>
    private void Move()
    {
        // input
        Vector3 input = GetInputDirection();
        if (input == Vector3.zero) return; //only input is nonzero

        Vector3 newPosition = targetPosition + input;
        GQObjectType typeAtNew = GridQueryManager.Instance.GetObjectTypeAt(newPosition);

        if (typeAtNew != GQObjectType.Obstacle && typeAtNew != GQObjectType.Empty)
            UndoManager.Instance.SaveCurrentTurn();

        switch (typeAtNew)
        {
            case GQObjectType.ColorBlock:
                StartCoroutine(HandleBlockPush(newPosition, input));
                break;

            case GQObjectType.Ground:
            case GQObjectType.ColorBlockMerged:
            case GQObjectType.ColorBlockBackground:
                TryMoveToColorPosition(newPosition);
                break;

            default:
                Debug.Log("[Player] 이동 불가: 막혀있거나 빈 칸");
                break;
        }

    }

    /// <summary>
    /// 블록이 없는 칸(Ground / Merged / Background)으로 플레이어를 이동시킨다.
    /// 색 조건을 만족하지 못하면 이동을 취소한다.
    /// </summary>
    private void TryMoveToColorPosition(Vector3 position)
    {
        var infoMove = GridQueryManager.Instance.GetInfoMoveToColorPosition(position);

        if (infoMove.Item1)
            StartCoroutine(MoveToPosition(position));
        else
            Debug.Log($"[Player] 이동 불가: 색상 불일치 ({infoMove.Item2})");
    }
    // ── Push Logic ─────────────────────────────────────────────
    /// <summary>
    /// 플레이어가 블록이 있는 칸으로 이동을 시도했을 때의 진입점.
    /// 1) 플레이어가 그 칸(블록이 사라진 뒤 바닥)에 설 수 있는지 확인하고
    /// 2) 가능하면 실제 푸시를 수행한다.
    /// 이동/푸시가 끝나기 전까지 isMoving을 잠궈서 중복 입력을 막는다.
    /// </summary>
    private IEnumerator HandleBlockPush(Vector3 blockPosition, Vector3 input)
    {
        Collider2D blockCollider = GridQueryManager.Instance.GetColliderAt(blockPosition, GridQueryManager.Instance.colorBlockLayer);
        if (blockCollider == null) yield break;

        if (!GridQueryManager.Instance.GetInfoMoveToColorPosition(blockPosition).Item1) yield break;

        List<Vector3> path = new List<Vector3>();
        if (!BlockManager.Instance.CanPushBlockContinuously(blockPosition, input, path)) yield break;

        isMoving = true;

        Coroutine blockChainMove = StartCoroutine(BlockManager.Instance.ExecutePushChain(blockPosition, input, path));
        Coroutine playerMove = StartCoroutine(MoveToPosition(blockPosition));

        yield return blockChainMove;
        yield return playerMove;

        isMoving = false;
    }


    /// <summary>
    /// ismoving = true
    /// player -> newposition
    /// ismoving = false
    /// </summary>
    private IEnumerator MoveToPosition(Vector3 newPosition)
    {
        isMoving = true;
        targetPosition = newPosition;

        yield return StartCoroutine(MovementHelper.Instance.SmoothMoveCoroutine(transform, targetPosition, moveSpeed));
        isMoving = false;

        //yield return new WaitForEndOfFrame();
        GameManager.Instance.OnTurnAdvanced();
    }

    // ── Input ──────────────────────────────────────────────────
    /// <summary>
    /// WASD 입력으로부터 이동 방향을 계산한다.
    /// 키를 처음 누른 순간은 즉시 반응하고, 계속 누르고 있으면 inputHoldDelay 이후부터
    /// inputCooldown 간격으로 반복 입력을 발생시킨다 (DAS/ARR 방식).
    /// </summary>
    private Vector3 GetInputDirection()
    {
        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) input = Vector3.up; // upward
        else if (Input.GetKey(KeyCode.S)) input = Vector3.down; // downward
        else if (Input.GetKey(KeyCode.A)) input = Vector3.left; // leftside
        else if (Input.GetKey(KeyCode.D)) input = Vector3.right; // rightside


        if (input == Vector3.zero)
        {
            keyHoldTime = 0f;
            lastInput = Vector3.zero;
            return Vector3.zero;
        }

        //새로운 방향키를 누른 순간 : 즉시 1회 반응
        if (input != lastInput)
        {
            lastInput = input;
            keyHoldTime = 0f;
            lastInputTime = Time.time;
            return input;
        }

        //같은 방향키를 계속 누르고 있는 경우 : 홀드 딜레이 후 쿨다운 간격으로 반복
        keyHoldTime += Time.deltaTime;
        if (keyHoldTime >= inputHoldDelay && Time.time - lastInputTime >= inputCooldown)
        {
            lastInputTime = Time.time;
            return input;
        }

        return Vector3.zero;
    }

    // ── For Undo ──────────────────────────────────────────────────

    /// <summary>
    /// 위치 강제 초기화(Undo)
    /// </summary>
    public void ResetTargetPosition(Vector3 pos) => targetPosition = pos;

    // ── Stage End ────────────────────────────────────────────────

    public void EndStage()
    {
        GameObject obj = Instantiate(clearUIPrefab);
        obj.GetComponent<ClearUI>().Show();
    }

}