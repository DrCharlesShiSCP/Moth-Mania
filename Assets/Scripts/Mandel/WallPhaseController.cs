using UnityEngine;

[RequireComponent(typeof(PhaseObject))]
public class WallPhaseController : MonoBehaviour
{
    [Header("References")]
    public PhaseObject phaseObject;
    public MothFlockController mothController;   // Flock&GoggleControl

    [Header("Headlight Behavior")]
    [Tooltip("If true, wall is solid when headlight is ON.")]
    public bool solidWhenHeadlightOn = true;
    public bool respondToHeadlight = true;

    [Header("Pressure Plate Behavior")]
    [Tooltip("If true, when the plate is pressed, the wall will always be OFF (ghost).")]
    public bool crateDisablesWall = true;

    bool _platePressed = false;

    void Awake()
    {
        if (phaseObject == null)
            phaseObject = GetComponent<PhaseObject>();

        // Try to auto-link if not assigned in Inspector
        if (mothController == null)
            mothController = MothFlockController.Instance;
    }

    // Called by PressurePlatePhase
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

        bool solid = true; // default base state

        // 1) HEADLIGHT CONTROL
        if (respondToHeadlight)
        {
            if (mothController == null)
            {
                Debug.LogWarning("WallPhaseController: no MothFlockController assigned, headlight will not affect wall.");
            }
            else
            {
                bool headlightOn = mothController.headlightOn;
                solid = solidWhenHeadlightOn ? headlightOn : !headlightOn;
            }
        }

        // 2) PRESSURE PLATE OVERRIDE
        if (crateDisablesWall && _platePressed)
        {
            // Crate on plate → wall always OFF/ghost
            solid = false;
        }

        phaseObject.SetSolid(solid);
    }
}
