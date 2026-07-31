using UnityEngine;
/// <summary>
/// 블록 배경의 수용 색상과 병합 지속 턴을 관리합니다.
/// </summary>
public class BlockBackground : MonoBehaviour
{
    public string colorName;
    public string acceptedColorName;

    [Header("Nturn")]
    public int mergeDurationTurns = 0;
}