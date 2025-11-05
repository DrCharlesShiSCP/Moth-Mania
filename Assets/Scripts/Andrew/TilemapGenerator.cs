using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class TilemapGenerator : MonoBehaviour
{
    [Header("Tilemap Settings")]
    public Tilemap tilemap;

    [Header("Visualization")]
    public bool showSolidBlocks = true;
    public bool showWireframe = true;
    public Color blockColor = new Color(0f, 1f, 0f, 1f);
    public Color wireColor = Color.black;
    public Vector3 blockScale = Vector3.one;

    [Header("Collision Prefab (Optional)")]
    public GameObject collisionPrefab;

    private static Mesh cubeMesh;
    private static Material solidMaterial;
    private static Material lineMaterial;

    private void OnEnable()
    {
        if (cubeMesh == null)
            cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        if (solidMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            solidMaterial = new Material(shader);
            solidMaterial.SetColor("_BaseColor", blockColor);
        }

        if (lineMaterial == null)
        {
            Shader lineShader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(lineShader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_ZWrite", 1);
            lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        }
    }

    private void Start()
    {
        if (Application.isPlaying)
            GenerateCollisions();
    }

    void GenerateCollisions()
    {
        if (tilemap == null)
        {
            Debug.LogError("Tilemap not assigned!");
            return;
        }

        if (collisionPrefab == null)
        {
            Debug.LogError("Collision prefab not assigned!");
            return;
        }

        BoundsInt bounds = tilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                if (tilemap.GetTile(new Vector3Int(x, y, 0)) != null)
                {
                    Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
                    Instantiate(collisionPrefab, worldPos, Quaternion.identity);
                }
            }
        }

        Debug.Log("Tilemap collision generation complete.");
    }

    private void OnDrawGizmos()
    {
        if (!showSolidBlocks || tilemap == null || cubeMesh == null || solidMaterial == null)
            return;

        solidMaterial.SetColor("_BaseColor", blockColor);
        BoundsInt bounds = tilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                if (tilemap.GetTile(new Vector3Int(x, y, 0)) == null) continue;

                Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
                Matrix4x4 matrix = Matrix4x4.TRS(worldPos, Quaternion.identity, blockScale);
                solidMaterial.SetPass(0);
                Graphics.DrawMeshNow(cubeMesh, matrix);
            }
        }
    }

    private void OnRenderObject()
    {
        if (!showWireframe || tilemap == null || cubeMesh == null || lineMaterial == null)
            return;

        lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINES);
        GL.Color(wireColor);

        BoundsInt bounds = tilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (tilemap.GetTile(pos) == null) continue;

                bool hasLeft = tilemap.GetTile(pos + Vector3Int.left) != null;
                bool hasRight = tilemap.GetTile(pos + Vector3Int.right) != null;
                bool hasUp = tilemap.GetTile(pos + Vector3Int.up) != null;
                bool hasDown = tilemap.GetTile(pos + Vector3Int.down) != null;

                Vector3 worldPos = tilemap.GetCellCenterWorld(pos);
                //DrawExposedEdges(worldPos, blockScale, hasLeft, hasRight, hasUp, hasDown);
            }
        }

        GL.End();
        GL.PopMatrix();
    }

    /// <summary>
    /// Draw only cube edges that are exposed (no neighboring tile).
    /// </summary>
    /*private void DrawExposedEdges(Vector3 center, Vector3 size, bool hasLeft, bool hasRight, bool hasUp, bool hasDown)
    {
        Vector3 h = size * 0.5f;

        // Cube vertices
        Vector3[] v =
        {
            center + new Vector3(-h.x, -h.y, -h.z),
            center + new Vector3( h.x, -h.y, -h.z),
            center + new Vector3( h.x,  h.y, -h.z),
            center + new Vector3(-h.x,  h.y, -h.z),
            center + new Vector3(-h.x, -h.y,  h.z),
            center + new Vector3( h.x, -h.y,  h.z),
            center + new Vector3( h.x,  h.y,  h.z),
            center + new Vector3(-h.x,  h.y,  h.z)
        };

*//*        // Draw only outer edges
        if (!hasDown)
        {
            DrawEdge(v[0], v[1]);
            DrawEdge(v[1], v[5]);
            DrawEdge(v[5], v[4]);
            DrawEdge(v[4], v[0]);
        }

        if (!hasUp)
        {
            DrawEdge(v[3], v[2]);
            DrawEdge(v[2], v[6]);
            DrawEdge(v[6], v[7]);
            DrawEdge(v[7], v[3]);
        }

        if (!hasLeft)
        {
            DrawEdge(v[0], v[3]);
            DrawEdge(v[3], v[7]);
            DrawEdge(v[7], v[4]);
            DrawEdge(v[4], v[0]);
        }

        if (!hasRight)
        {
            DrawEdge(v[1], v[2]);
            DrawEdge(v[2], v[6]);
            DrawEdge(v[6], v[5]);
            DrawEdge(v[5], v[1]);
        }*//*
    }

    private void DrawEdge(Vector3 a, Vector3 b)
    {
        GL.Vertex(a);
        GL.Vertex(b);
    }*/
}
