using UnityEngine;

public enum MergeColorEffect
{
    Add,
    Remove
}

public class Block : MonoBehaviour
{
    public string colorName;
    public GameObject mergedBlockPrefab;
    [SerializeField] private MergeColorEffect colorEffect = MergeColorEffect.Add;

    public MergeColorEffect ColorEffect => colorEffect;

    /// <summary>valid move의 initial Cell입니다. invalid move면 null.</summary>
    public StageCell? LastMoveFromCell { get; private set; }

    /// <summary> Commit/Undo 상태를 복원</summary>
    public void RestoreLastMoveFromCell(StageCell? cell){ LastMoveFromCell = cell; }
}
