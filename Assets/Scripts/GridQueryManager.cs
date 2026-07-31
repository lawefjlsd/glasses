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

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Unity 초기화 단계에서 싱글턴 또는 필수 참조를 준비합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Unity 시작 단계에서 런타임 상태와 참조를 초기화합니다.
    /// </summary>
    private void Start()
    {
        tilemapManager = FindObjectOfType<TilemapManager>();
    }


    /// <summary>
    /// 입력: position, type, tilemap. 출력: string. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
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


    /// <summary>
    /// 입력: position. 출력: ObjectType. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
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


    /// <summary>
    /// 입력: position, tilemap. 출력: ObjectType. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
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
    /// <summary>
    /// 입력: position, layer. 출력: Collider2D. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
    public Collider2D GetColliderAt(Vector3 position, LayerMask layer)
    {
        return Physics2D.OverlapPoint(position, layer);
    }
    /// <summary>
    /// 입력: 없음. 출력: bool. 역할: 지정한 조건 또는 현재 상태를 판정합니다.
    /// </summary>
    public bool CheckIfAnyBlockIsSeparating()
    {
        BlockMerged[] mergedBlocks = FindObjectsOfType<BlockMerged>();
        foreach (var merged in mergedBlocks)
        {
            if (merged.IsSeparating) return true;
        }
        return false;
    }


    /// <summary>
    /// 입력: position. 출력: Tuple<bool, string, bool>. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
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
