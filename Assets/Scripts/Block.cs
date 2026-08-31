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
    [SerializeField] private MergeColorEffect colorEffect = MergeColorEffect.Add;
    public MergeColorEffect ColorEffect => colorEffect;
}
