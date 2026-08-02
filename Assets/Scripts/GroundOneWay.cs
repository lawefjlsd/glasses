using UnityEngine;

/// <summary>
/// 독립 GameObject로 배치되는 단방향 색상 지면의 진입·이탈 규칙을 관리합니다.
/// </summary>
public class GroundOneWay : MonoBehaviour
{
    public const string LayerName = "GroundOneWay";

    public enum ExitDirection
    {
        W,
        A,
        S,
        D
    }

    public string colorName;
    public ExitDirection exitDirection;

    /// <summary>
    /// 입력: movementDirection. 출력: bool. 역할: 이동 방향이 Inspector에 지정한 유일한 이탈 방향과 일치하는지 반환합니다.
    /// </summary>
    public bool CanExit(Vector3 movementDirection)
    {
        return movementDirection == GetExitVector();
    }

    /// <summary>
    /// 입력: 없음. 출력: bool. 역할: 현재 색상이 이 GroundOneWay의 색상을 포함해 플레이어가 진입 가능한지 반환합니다.
    /// </summary>
    public bool CanPlayerEnter()
    {
        return GameManager.Instance != null && GameManager.Instance.IsAvailableBlock(colorName, false);
    }

    /// <summary>
    /// 입력: 없음. 출력: Vector3. 역할: W/A/S/D 이탈 설정을 격자 이동 벡터로 변환합니다.
    /// </summary>
    private Vector3 GetExitVector()
    {
        switch (exitDirection)
        {
            case ExitDirection.W:
                return Vector3.up;
            case ExitDirection.A:
                return Vector3.left;
            case ExitDirection.S:
                return Vector3.down;
            case ExitDirection.D:
                return Vector3.right;
            default:
                return Vector3.zero;
        }
    }
}
