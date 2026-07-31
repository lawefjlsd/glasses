using UnityEngine;
using UnityEngine.Tilemaps;
public class TilemapManager : MonoBehaviour
{
    public Tilemap[] tilemaps;

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