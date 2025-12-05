using UnityEngine;

public class MoveUpOnStart : MonoBehaviour
{
    public int tilesUp = 3;   // number of tiles to move
    public float tileSize = 1f; // size of each tile in world units

    void Start()
    {
        Vector3 pos = transform.position;
        pos += Vector3.up * tilesUp * tileSize;
        transform.position = pos;
    }
}
