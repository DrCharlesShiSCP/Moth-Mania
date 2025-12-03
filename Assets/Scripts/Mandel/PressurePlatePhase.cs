using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PressurePlatePhase : MonoBehaviour
{
    [Header("Linked Wall Controller")]
    public WallPhaseController wallController;

    [Header("Detection Settings")]
    [Tooltip("Center of detection box. If null, uses this transform.")]
    public Transform detectionCenter;

    [Tooltip("Half-size of the box used to detect crates on the plate.")]
    public Vector3 halfExtents = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("Layers that count as crates pressing the plate.")]
    public LayerMask crateLayers;

    bool _isPressed = false;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        detectionCenter = transform;
    }

    void Update()
    {
        if (detectionCenter == null)
            detectionCenter = transform;

        // Check if any crate collider is overlapping this box
        bool pressedNow = Physics.CheckBox(
            detectionCenter.position,
            halfExtents,
            Quaternion.identity,
            crateLayers
        );

        if (pressedNow != _isPressed)
        {
            _isPressed = pressedNow;

            if (wallController != null)
                wallController.SetPlatePressed(_isPressed);

            // Optional debug
            // Debug.Log($"Plate pressed: {_isPressed}");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (detectionCenter == null)
            detectionCenter = transform;

        Gizmos.matrix = detectionCenter.localToWorldMatrix;
        Gizmos.color = _isPressed ? Color.red : Color.green;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
    }
}
