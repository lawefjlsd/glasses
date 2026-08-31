using UnityEngine;

/// <summary>새 Stage Object가 기본 adapter 대신 명시 Profile을 제공할 때 사용하는 선택적 Component입니다.</summary>
public class StageObjectProfileComponent : MonoBehaviour
{
    public StageZone zone = StageZone.Top;
    public MovementCapability movement = MovementCapability.Static;
    public OccupancyType occupancy = OccupancyType.Solid;
    public string colorName;
    public bool isExclusive;

    /// <summary>입력: 없음. 출력: StageObjectProfile. 역할: Inspector 설정을 논리 Board Profile로 반환합니다.</summary>
    public StageObjectProfile ToProfile() => new StageObjectProfile { zone = zone, movement = movement, occupancy = occupancy };
}
