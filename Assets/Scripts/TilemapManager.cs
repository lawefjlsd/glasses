using UnityEngine;
using UnityEngine.Tilemaps;
/// <summary>
/// 타일맵 조회 기능을 제공합니다.
/// </summary>
public class TilemapManager : MonoBehaviour
{
    public Tilemap[] tilemaps;

    /// <summary>
    /// 입력: name. 출력: Tilemap. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
    public Tilemap GetTilemapByName(string name)
    {
        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap.name == name)
            {
                return tilemap;
            }
        }
        Debug.LogError($"[TilemapManager] 이름에 해당하는 타일맵을 찾을 수 없음: {name}");
        return null;
    }

    /// <summary>
    /// 입력: position. 출력: Tilemap. 역할: 지정한 조건에 맞는 정보를 조회해 반환합니다.
    /// </summary>
    public Tilemap GetTilemapAtPosition(Vector3 position)
    {
        foreach (Tilemap tilemap in tilemaps)
        {
            Vector3Int tilePosition = tilemap.WorldToCell(position);
            TileBase tile = tilemap.GetTile(tilePosition);
            if (tile != null)
            {
                return tilemap;
            }
        }
        return null;
    }
}
