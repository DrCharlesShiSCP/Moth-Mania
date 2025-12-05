using UnityEngine;

public class PhaseObject : MonoBehaviour
{
    [Header("Targets To Affect")]
    [Tooltip("If empty, PhaseObject will automatically scan for colliders in this object and its children.")]
    public Collider[] colliders;

    [Tooltip("If empty, PhaseObject will automatically scan for renderers in this object and its children.")]
    public Renderer[] renderers;

    void Awake()
    {
        RefreshTargets();
    }

    /// <summary>
    /// Refreshes internal collider and renderer lists.
    /// This ensures the object always affects the correct components.
    /// </summary>
    public void RefreshTargets()
    {
        // Only autofill if user did not manually assign references
        if (colliders == null || colliders.Length == 0)
            colliders = GetComponentsInChildren<Collider>(true);

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>
    /// Enables or disables ALL colliders and renderers this object controls.
    /// </summary>
    public void SetSolid(bool solid)
    {
        // Ensure arrays are valid (safety check)
        if (colliders == null || colliders.Length == 0 ||
            renderers == null || renderers.Length == 0)
        {
            RefreshTargets();
        }

        // Toggle colliders
        if (colliders != null)
        {
            foreach (var col in colliders)
            {
                if (col != null)
                    col.enabled = solid;
            }
        }

        // Toggle renderers
        if (renderers != null)
        {
            foreach (var rend in renderers)
            {
                if (rend != null)
                    rend.enabled = solid;
            }
        }
    }
}
