using UnityEngine;
/// <summary>
/// 색상 블록의 병합 정보를 관리합니다.
/// </summary>
public class Block : MonoBehaviour
{
    public string colorName;
    public GameObject mergedBlockPrefab;
    public GameObject originBlockPrefab;
    public bool CanMerge => mergedBlockPrefab != null;
    public GameObject GetMergedBlock() => mergedBlockPrefab;

}