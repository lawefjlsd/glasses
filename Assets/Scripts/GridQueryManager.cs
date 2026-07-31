using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 격자 좌표의 오브젝트와 이동 가능 여부를 조회합니다.
/// </summary>
public class GridQueryManager : MonoBehaviour
{
    public static GridQueryManager Instance { get; private set; }

    public enum ObjectType
    {
        Empty, Obstacle, Ground, ColorBlock, ColorBlockBackground, ColorBlockMerged
    }

    [Header("Layer Masks")]
    public LayerMask colorBlockLayer;
    public LayerMask colorBlockBackgroundLayer;
    public LayerMask colorBlockMergedLayer;

    private TilemapManager tilemapManager;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        tilemapManager = FindObjectOfType<TilemapManager>();
    }


    public string GetColorNameAtPosition(Vector3 position, ObjectType type, Tilemap tilemap)
    {
        switch (type)
        {
            case ObjectType.Ground:
                return tilemap?.GetComponent<ColorTilemap>()?.colorName;
            case ObjectType.ColorBlockMerged:
                return GetColliderAt(position, colorBlockMergedLayer)?.GetComponent<BlockMerged>()?.colorName;
            case ObjectType.ColorBlockBackground:
                return GetColliderAt(position, colorBlockBackgroundLayer)?.GetComponent<BlockBackground>()?.colorName;
            default:
                return null;
        }
    }


    public ObjectType GetObjectTypeAt(Vector3 position)
    {
        BlockMerged[] mergedBlocks = FindObjectsOfType<BlockMerged>();
        foreach (var merged in mergedBlocks)
        {
            if (merged.isInfinite) continue;
            if (merged.remainingTurns != 1) continue;


            if (Vector3.Distance(merged.originBlockPosition, position) < 0.01f)
            {
                Debug.LogWarning($"[GridQueryManager] {position} 자리는 다음 턴에 블록이 분리될 예정이라 진입 불가");
                return ObjectType.Obstacle;
            }


            foreach (var pathPos in merged.pendingSeparationPath)
            {
                if (Vector3.Distance(pathPos, position) < 0.01f)
                {
                    Debug.LogWarning($"[GridQueryManager] {position} 자리는 다음 턴 분리로 블록이 밀려올 예정이라 진입 불가");
                    return ObjectType.Obstacle;
                }
            }
        }

        if (GetColliderAt(position, colorBlockLayer) != null) return ObjectType.ColorBlock;

        Tilemap tilemap = tilemapManager.GetTilemapAtPosition(position);
        return GetGroundTypeAt(position, tilemap);
    }


    public ObjectType GetGroundTypeAt(Vector3 position, Tilemap tilemap)
    {
        if (GetColliderAt(position, colorBlockBackgroundLayer) != null) return ObjectType.ColorBlockBackground;
        if (GetColliderAt(position, colorBlockMergedLayer) != null) return ObjectType.ColorBlockMerged;

        if (tilemap == null) return ObjectType.Empty;
        ColorTilemap colorTilemap = tilemap.GetComponent<ColorTilemap>();
        if (colorTilemap == null) return ObjectType.Empty;
        if (colorTilemap.isObstacle) return ObjectType.Obstacle;
        if (colorTilemap.colorName == null) return ObjectType.Empty;

        return ObjectType.Ground;
    }
    public Collider2D GetColliderAt(Vector3 position, LayerMask layer)
    {
        return Physics2D.OverlapPoint(position, layer);
    }
    public bool CheckIfAnyBlockIsSeparating()
    {
        BlockMerged[] mergedBlocks = FindObjectsOfType<BlockMerged>();
        foreach (var merged in mergedBlocks)
        {
            if (merged.IsSeparating) return true;
        }
        return false;
    }


    public Tuple<bool, string, bool> GetInfoMoveToColorPosition(Vector3 position)
    {
        Tilemap tilemap = tilemapManager.GetTilemapAtPosition(position);
        ObjectType type = GetGroundTypeAt(position, tilemap);
        string colorName = GetColorNameAtPosition(position, type, tilemap);

        if (colorName == null)
        {
            return Tuple.Create<bool, string, bool>(false, null, false);
        }

        bool isExclusive = tilemap?.GetComponent<ColorTilemap>()?.isExclusive ?? false;
        bool canMove = GameManager.Instance.IsAvailableBlock(colorName, isExclusive);
        return Tuple.Create<bool, string, bool>(canMove, colorName, isExclusive);
    }


}