using UnityEngine;

public enum MergeColorEffect
{
    Add,
    Remove
}

/// <summary>
/// 색상 블록의 병합 정보를 관리합니다.
/// </summary>
public class Block : MonoBehaviour
{
    public string colorName;
    public GameObject mergedBlockPrefab;
    public GameObject originBlockPrefab;
    public bool CanMerge => mergedBlockPrefab != null;

    /// <summary>
    /// 입력: 없음. 출력: MergeColorEffect. 역할: 이 Block이 병합 시 현재 색상에 적용할 기본 효과를 반환합니다.
    /// </summary>
    public virtual MergeColorEffect GetMergeColorEffect() => MergeColorEffect.Add;

    /// <summary>
    /// 입력: 없음. 출력: GameObject. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
    public GameObject GetMergedBlock() => mergedBlockPrefab;

}
