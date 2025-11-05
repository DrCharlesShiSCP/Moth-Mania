using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapGenerator : MonoBehaviour
{
    public Tilemap tilemap; // Assign your Tilemap here
    public GameObject collisionPrefab; // Prefab with Collider2D

    void Start()
    {
        GenerateCollisions();
    }

    void GenerateCollisions()
    {
        if (tilemap == null)
        {
            Debug.LogError("Tilemap is not assigned!");
            return;
        }

        if (collisionPrefab == null)
        {
            Debug.LogError("Collision prefab is not assigned!");
            return;
        }

        BoundsInt bounds = tilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int tilePosition = new Vector3Int(x, y, 0);
                TileBase tile = tilemap.GetTile(tilePosition);

                if (tile != null) // Only place colliders where there are tiles
                {
                    Vector3 worldPosition = tilemap.GetCellCenterWorld(tilePosition);
                    Instantiate(collisionPrefab, worldPosition, Quaternion.identity);
                }
            }
        }

        Debug.Log("Tilemap collision generation complete.");
    }
}
