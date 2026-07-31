using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using GQObjectType = GridQueryManager.ObjectType;


/// <summary>
/// 플레이어 입력, 이동, 블록 밀기를 처리합니다.
/// </summary>
public class Player : MonoBehaviour
{

    private bool isMoving = false;
    public bool IsMovingStatus() => isMoving;
    private Vector3 targetPosition;

    private float lastInputTime = 0f;
    private float keyHoldTime = 0f;
    private Vector3 lastInput = Vector3.zero;


    public Transform initialPosition;
    public GameObject clearUIPrefab;
    public float moveSpeed = 5f;


    public float inputCooldown = 0.2f;
    [SerializeField] private float inputHoldDelay = 0.3f;


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


    private void Move()
    {

        Vector3 input = GetInputDirection();
        if (input == Vector3.zero) return;

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


    private void TryMoveToColorPosition(Vector3 position)
    {
        var infoMove = GridQueryManager.Instance.GetInfoMoveToColorPosition(position);

        if (infoMove.Item1)
            StartCoroutine(MoveToPositionAndAdvanceTurn(position));
        else
            Debug.Log($"[Player] 이동 불가: 색상 불일치 ({infoMove.Item2})");
    }


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

        yield return StartCoroutine(GameManager.Instance.OnTurnAdvanced());

        isMoving = false;
    }


    private IEnumerator MoveToPosition(Vector3 newPosition)
    {
        targetPosition = newPosition;

        yield return StartCoroutine(MovementHelper.Instance.SmoothMoveCoroutine(transform, targetPosition, moveSpeed));
    }


    private IEnumerator MoveToPositionAndAdvanceTurn(Vector3 newPosition)
    {
        isMoving = true;
        yield return StartCoroutine(MoveToPosition(newPosition));
        yield return StartCoroutine(GameManager.Instance.OnTurnAdvanced());
        isMoving = false;
    }


    private Vector3 GetInputDirection()
    {
        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) input = Vector3.up;
        else if (Input.GetKey(KeyCode.S)) input = Vector3.down;
        else if (Input.GetKey(KeyCode.A)) input = Vector3.left;
        else if (Input.GetKey(KeyCode.D)) input = Vector3.right;


        if (input == Vector3.zero)
        {
            keyHoldTime = 0f;
            lastInput = Vector3.zero;
            return Vector3.zero;
        }


        if (input != lastInput)
        {
            lastInput = input;
            keyHoldTime = 0f;
            lastInputTime = Time.time;
            return input;
        }


        keyHoldTime += Time.deltaTime;
        if (keyHoldTime >= inputHoldDelay && Time.time - lastInputTime >= inputCooldown)
        {
            lastInputTime = Time.time;
            return input;
        }

        return Vector3.zero;
    }


    public void ResetTargetPosition(Vector3 pos) => targetPosition = pos;


    public void EndStage()
    {
        GameObject obj = Instantiate(clearUIPrefab);
        obj.GetComponent<ClearUI>().Show();
    }

}
