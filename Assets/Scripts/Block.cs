using UnityEngine;
public class Block : MonoBehaviour
{
    public string colorName;
    public GameObject mergedBlockPrefab; // 각 블록이 어떤 mergedBlock이 될지 지정
    public GameObject originBlockPrefab;
    public bool CanMerge => mergedBlockPrefab != null; // 병합 가능 여부
    public GameObject GetMergedBlock() => mergedBlockPrefab;

}