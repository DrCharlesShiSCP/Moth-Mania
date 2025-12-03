using UnityEngine;

public class HeadlightPhaseObject : MonoBehaviour
{
    [Header("Headlight Source")]
    [Tooltip("If left empty, will use MothFlockController.Instance.")]
    public MothFlockController mothController;

    [Header("Targets To Affect")]
    [Tooltip("If empty, will grab colliders/renderers from this object and its children.")]
    public Collider[] colliders;
    public Renderer[] renderers;

    [Header("Behavior")]
    [Tooltip("If true: solid when headlightOn == true.\nIf false: solid when headlightOn == false.")]
    public bool solidWhenHeadlightOn = true;

    void Awake()
    {
        // Auto-fill if nothing assigned
        if (colliders == null || colliders.Length == 0)
            colliders = GetComponentsInChildren<Collider>();

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        // 1) Get controller
        var controller = mothController != null
            ? mothController
            : MothFlockController.Instance;

        if (controller == null)
            return;

        // 2) Decide if object should be solid or ghost
        bool headlightOn = controller.headlightOn;
        bool shouldBeSolid = solidWhenHeadlightOn ? headlightOn : !headlightOn;

        // 3) Toggle colliders (collision on/off)
        if (colliders != null)
        {
            foreach (var col in colliders)
            {
                if (col == null) continue;
                if (col.enabled != shouldBeSolid)
                    col.enabled = shouldBeSolid;
            }
        }

        // 4) Toggle renderers (visuals on/off, optional)
        if (renderers != null)
        {
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                if (rend.enabled != shouldBeSolid)
                    rend.enabled = shouldBeSolid;
            }
        }
    }
}
