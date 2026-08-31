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
}
