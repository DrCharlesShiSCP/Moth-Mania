using UnityEngine;

[RequireComponent(typeof(PhaseObject))]
public class WallPhaseController : MonoBehaviour
{
    [Header("References")]
    public PhaseObject phaseObject;
    public MothFlockController mothController;

    [Header("Headlight Behavior")]
    [Tooltip("If true, wall is solid when headlight is ON.")]
    public bool solidWhenHeadlightOn = true;
    public bool respondToHeadlight = true;

    [Header("Pressure Plate Behavior")]
    [Tooltip("If true, when the plate is pressed, the wall will always be OFF (ghost).")]
    public bool crateDisablesWall = true;

    private bool _platePressed = false;

    void Awake()
    {
        if (phaseObject == null)
            phaseObject = GetComponent<PhaseObject>();

        if (mothController == null)
            mothController = MothFlockController.Instance;
    }

#if UNITY_EDITOR
    // Editor-only auto assignment
    void OnValidate()
    {
        if (mothController == null)
        {
            // Search ONLY in editor
            mothController = FindAnyObjectByType<MothFlockController>();
        }
    }
#endif

    public void SetPlatePressed(bool pressed)
    {
        _platePressed = pressed;
        UpdatePhase();
    }

    void Update()
    {
        UpdatePhase();
    }

    void UpdatePhase()
    {
        if (phaseObject == null)
            return;

        bool solid;

        if (crateDisablesWall && _platePressed)
        {
            solid = false;
            phaseObject.SetSolid(solid);
            return;
        }

        if (respondToHeadlight && mothController != null)
        {
            bool headlightOn = mothController.headlightOn;
            solid = solidWhenHeadlightOn ? headlightOn : !headlightOn;
        }
        else
        {
            solid = true;
        }

        phaseObject.SetSolid(solid);
    }

    public void SetActiveByPlate(bool active)
    {
        if (phaseObject != null)
            phaseObject.SetSolid(active);
    }
}
