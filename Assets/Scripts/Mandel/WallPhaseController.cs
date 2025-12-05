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

    /// <summary>
    /// Called by PressurePlateTrigger to notify plate state.
    /// </summary>
    public void SetPlatePressed(bool pressed)
    {
        _platePressed = pressed;
        UpdatePhase();
    }

    void Update()
    {
        UpdatePhase();
    }

    /// <summary>
    /// Calculates the current solid/ghost state based on plate and headlight.
    /// </summary>
    void UpdatePhase()
    {
        if (phaseObject == null)
            return;

        bool solid;

        // 1) PRESSURE PLATE OVERRIDE (always highest priority)
        if (crateDisablesWall && _platePressed)
        {
            solid = false;  // plate forces wall OFF
            phaseObject.SetSolid(solid);
            return;
        }

        // 2) HEADLIGHT LOGIC (only applies if plate isn't pressing)
        if (respondToHeadlight && mothController != null)
        {
            bool headlightOn = mothController.headlightOn;
            solid = solidWhenHeadlightOn ? headlightOn : !headlightOn;
        }
        else
        {
            solid = true; // default fallback
        }

        phaseObject.SetSolid(solid);
    }

    /// <summary>
    /// External method to immediately set wall solid/ghost by pressure plate or other triggers.
    /// </summary>
    public void SetActiveByPlate(bool active)
    {
        if (phaseObject != null)
        {
            phaseObject.SetSolid(active); // true = solid/visible, false = ghost/invisible
        }
    }
}
