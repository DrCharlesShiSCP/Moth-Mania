using UnityEngine;

public class PhaseObject : MonoBehaviour
{
    [Header("Targets To Affect")]
    [Tooltip("If empty, will grab from this object + children.")]
    public Collider[] colliders;
    public Renderer[] renderers;

    void Awake()
    {
        if (colliders == null || colliders.Length == 0)
            colliders = GetComponentsInChildren<Collider>();

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();
    }

    public void SetSolid(bool solid)
    {
        if (colliders != null)
        {
            foreach (var col in colliders)
                if (col != null) col.enabled = solid;
        }

        if (renderers != null)
        {
            foreach (var rend in renderers)
                if (rend != null) rend.enabled = solid;
        }
    }
}
