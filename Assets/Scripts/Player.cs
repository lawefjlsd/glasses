using UnityEngine;

/// <summary>
/// StageTurnController에 방향 입력만 전달하는 플레이어 표현 컴포넌트입니다.
/// </summary>
public class Player : MonoBehaviour
{
    private float lastInputTime;
    private float keyHoldTime;
    private Vector3 lastInput = Vector3.zero;
    private StageTurnController stageTurnController;

    public Transform initialPosition;
    public GameObject clearUIPrefab;
    public float moveSpeed = 5f;
    public float inputCooldown = 0.2f;
    [SerializeField] private float inputHoldDelay = 0.3f;

    public bool IsMovingStatus() => stageTurnController != null && stageTurnController.IsProcessingTurn;

    private void Start()
    {
        if (initialPosition != null) transform.position = initialPosition.position;
        stageTurnController = FindObjectOfType<StageTurnController>();
        if (stageTurnController == null)
        {
            Debug.LogError("[Player] StageTurnController is required for Stage input.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (stageTurnController == null || !stageTurnController.IsRuntimeActive) return;
        if (UIManager.Instance != null && UIManager.Instance.IsOverlayOpen) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (stageTurnController.IsProcessingTurn) return;

        if (Input.GetKeyDown(KeyCode.Z))
        {
            stageTurnController.RequestUndo();
            return;
        }

        Vector3 input = GetInputDirection();
        if (input != Vector3.zero) stageTurnController.RequestMove(input);
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
}
