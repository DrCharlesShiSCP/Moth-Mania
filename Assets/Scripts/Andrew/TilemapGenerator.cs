using System.Collections.Generic;
using Unity.AI.Navigation; // package: com.unity.ai.navigation
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization; // for FormerlySerializedAs
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
    [Tooltip("Prefab to instantiate per occupied tile (e.g., a cube with a collider, set to a 'Ground' layer).")]
    public GameObject collisionPrefab;

    [Header("NavMesh")]
    [Tooltip("Create/find a NavMeshSurface here to bake walkable areas from generated tiles.")]
    public NavMeshSurface navSurface;

    [Tooltip("Which layers count as ground when baking the NavMesh.")]
    [FormerlySerializedAs("walkableLayers")]
    public LayerMask groundMask = ~0;

    [Tooltip("Collect only children of this GameObject (recommended if you parent tiles under this).")]
    public bool collectChildrenOnly = true;

    [Tooltip("Rebuild the NavMesh right after generating collisions (Play Mode).")]
    public bool rebuildNavMeshOnGenerate = true;

    [Header("NavMesh Rebuild Safety (Play Mode)")]
    [Tooltip("Disable all NavMeshAgents while rebuilding NavMesh to avoid 'agent not close enough' errors.")]
    public bool disableAgentsDuringRebuild = true;

    [Tooltip("After rebuilding, warp agents to the nearest NavMesh point if they are not on it.")]
    public bool warpAgentsToNavMeshAfterRebuild = true;

    [Tooltip("Search radius used to snap agents onto the NavMesh after rebuild.")]
    public float warpSampleRadius = 5f;

    private static Mesh cubeMesh;
    private static Material solidMaterial;
    private static Material lineMaterial;

    // Where we parent instantiated collision tiles so it stays tidy
    private Transform _collidersRoot;

    void OnEnable()
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

        EnsureRootsAndSurface();
    }

    // Optional: auto-default groundMask to the "Ground" layer if mask is 0
    void OnValidate()
    {
        if (groundMask == 0)
        {
            int idx = LayerMask.NameToLayer("Ground");
            if (idx >= 0) groundMask = 1 << idx;
        }
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            GenerateCollisions();
        }
    }

    void EnsureRootsAndSurface()
    {
        // Create/find a tidy parent for generated collision tiles
        var rootName = "CollidersRoot";
        var child = transform.Find(rootName);
        if (!child)
        {
            var go = new GameObject(rootName);
            go.transform.SetParent(transform, false);
            _collidersRoot = go.transform;
        }
        else _collidersRoot = child;

        // Ensure we have a NavMeshSurface somewhere sensible (default: this GO)
        if (navSurface == null)
            navSurface = GetComponent<NavMeshSurface>();
        if (navSurface == null)
            navSurface = gameObject.AddComponent<NavMeshSurface>();

        // Ensure nav surface settings are aligned with this generator
        navSurface.layerMask = groundMask;
        navSurface.collectObjects = collectChildrenOnly ? CollectObjects.Children : CollectObjects.All;

        // Tilemap collision prefabs are almost always best baked from colliders.
        // (This prevents many "navmesh height" / "no mesh" problems.)
        navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
    }

    [ContextMenu("Generate Collisions (Editor)")]
    public void GenerateCollisions()
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

        EnsureRootsAndSurface();

        // Clear previous generated children (Editor convenience)
        if (Application.isPlaying == false)
        {
            for (int i = _collidersRoot.childCount - 1; i >= 0; i--)
            {
                var c = _collidersRoot.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.DestroyObjectImmediate(c.gameObject);
                else Destroy(c.gameObject);
#else
                DestroyImmediate(c.gameObject);
#endif
            }
        }

        BoundsInt bounds = tilemap.cellBounds;
        int count = 0;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (tilemap.GetTile(cell) == null) continue;

                Vector3 worldPos = tilemap.GetCellCenterWorld(cell);
                Instantiate(collisionPrefab, worldPos, Quaternion.identity, _collidersRoot);
                count++;
            }
        }

        Debug.Log($"Tilemap collision generation complete. Spawned {count} tiles.");

        if (Application.isPlaying && rebuildNavMeshOnGenerate)
        {
            RebuildNavMeshNow();
        }
    }

    [ContextMenu("Rebuild NavMesh Now")]
    public void RebuildNavMeshNow()
    {
        EnsureRootsAndSurface();

        // Keep surface settings consistent
        navSurface.layerMask = groundMask;
        navSurface.collectObjects = collectChildrenOnly ? CollectObjects.Children : CollectObjects.All;
        navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        List<NavMeshAgent> disabledAgents = null;

        // 1) Disable agents to prevent errors during RemoveData/BuildNavMesh refresh
        if (Application.isPlaying && disableAgentsDuringRebuild)
        {
            var agents = FindObjectsOfType<NavMeshAgent>(true);
            disabledAgents = new List<NavMeshAgent>(agents.Length);

            foreach (var a in agents)
            {
                if (a == null) continue;
                if (!a.enabled) continue;

                a.enabled = false;
                disabledAgents.Add(a);
            }
        }

        // 2) Rebuild navmesh data cleanly
        navSurface.RemoveData();
        navSurface.BuildNavMesh();

        // 3) Re-enable and optionally warp agents onto navmesh
        if (Application.isPlaying && disabledAgents != null)
        {
            foreach (var a in disabledAgents)
            {
                if (a == null) continue;

                a.enabled = true;

                if (warpAgentsToNavMeshAfterRebuild && !a.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(a.transform.position, out var hit, warpSampleRadius, NavMesh.AllAreas))
                    {
                        a.Warp(hit.position);
                    }
                    // If sample fails, we leave it enabled; its own scripts should guard against isOnNavMesh.
                }
            }
        }

        Debug.Log("[TilemapGenerator] NavMesh rebuilt (agents safely handled).");
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

                // (Wireframe drawing omitted for brevity)
            }
        }

        GL.End();
        GL.PopMatrix();
    }
}
