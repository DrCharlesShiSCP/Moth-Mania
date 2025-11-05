using UnityEngine;

public class PlayerPickupThrow_Radius : MonoBehaviour
{
    [Header("Inputs")]
    public KeyCode pickupKey = KeyCode.E;
    public KeyCode throwKey = KeyCode.Mouse0;  // Left click
    public KeyCode dropKey = KeyCode.R;       // Optional drop

    [Header("Pickup (Radius)")]
    [Tooltip("Origin for the pickup radius. If null, auto-creates at bottom of player's collider.")]
    public Transform feet;
    [Tooltip("Radius to search for Throwable-tagged objects.")]
    public float pickupRadius = 0.6f;
    [Tooltip("Only the closest valid Throwable in range will be picked up.")]
    public bool preferClosest = true;

    [Header("Holding")]
    [Tooltip("Where the held object should sit while carried.")]
    public Transform holdPoint;
    [Tooltip("How fast the held object snaps to the hold point.")]
    public float holdLerp = 20f;
    [Tooltip("Freeze object rotation while held.")]
    public bool freezeHeldRotation = true;

    [Header("Throwing")]
    [Tooltip("Forward speed given to the object when thrown.")]
    public float forwardThrowSpeed = 12f;
    [Tooltip("Upward impulse added to give a small arc.")]
    public float upwardThrowImpulse = 4f;
    [Tooltip("Inherit the player's current velocity when throwing.")]
    public bool inheritPlayerVelocity = true;

    // --- runtime ---
    Rigidbody heldRB;
    Collider[] heldColliders;
    bool originalUseGravity;
    Rigidbody playerRB;

    void Awake()
    {
        // Auto-setup feet (pickup origin) if not assigned
        if (feet == null)
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                GameObject temp = new GameObject("FeetAuto");
                temp.transform.SetParent(transform);
                temp.transform.position = new Vector3(col.bounds.center.x, col.bounds.min.y + 0.02f, col.bounds.center.z);
                feet = temp.transform;
            }
        }

        playerRB = GetComponent<Rigidbody>(); // optional, only for inheritPlayerVelocity
    }

    void Update()
    {
        if (heldRB != null)
        {
            // Keep held object at hold point
            heldRB.position = Vector3.Lerp(heldRB.position, holdPoint.position, holdLerp * Time.deltaTime);
            if (freezeHeldRotation) heldRB.rotation = holdPoint.rotation;

            if (Input.GetKeyDown(throwKey))
            {
                ThrowHeld();
            }
            else if (Input.GetKeyDown(dropKey))
            {
                ReleaseHeld(applyThrow: false);
            }

            return;
        }

        // Not holding: try to pick up something in radius
        if (Input.GetKeyDown(pickupKey))
        {
            TryPickupByRadius();
        }
    }

    void TryPickupByRadius()
    {
        Vector3 origin = GetPickupOrigin();
        // Query all colliders in radius, ignore triggers
        Collider[] hits = Physics.OverlapSphere(origin, pickupRadius, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        Collider best = null;
        float bestDist = float.MaxValue;

        foreach (var c in hits)
        {
            if (!c || !c.gameObject.activeInHierarchy) continue;
            if (!c.CompareTag("Throwable")) continue;

            var rb = c.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue; // must be dynamic to throw

            if (!preferClosest)
            {
                // First valid match
                TryBeginHold(rb);
                return;
            }

            float d = (c.ClosestPoint(origin) - origin).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }

        if (best != null && best.attachedRigidbody != null)
        {
            TryBeginHold(best.attachedRigidbody);
        }
    }

    bool TryBeginHold(Rigidbody rb)
    {
        heldRB = rb;

        // Cache & disable physics + colliders while held
        originalUseGravity = heldRB.useGravity;
        heldRB.useGravity = false;
        heldRB.isKinematic = true;

        heldColliders = heldRB.GetComponentsInChildren<Collider>();
        foreach (var c in heldColliders) c.enabled = false;

        // Snap to hold position/rotation
        if (holdPoint != null)
        {
            heldRB.position = holdPoint.position;
            heldRB.rotation = holdPoint.rotation;
        }

        return true;
    }

    void ThrowHeld()
    {
        if (heldRB == null) return;

        Vector3 baseVel = transform.forward * forwardThrowSpeed + Vector3.up * upwardThrowImpulse;

        if (inheritPlayerVelocity && playerRB != null)
        {
            baseVel += playerRB.linearVelocity;
        }

        ReleaseHeld(applyThrow: true, throwVelocity: baseVel);
    }

    void ReleaseHeld(bool applyThrow, Vector3 throwVelocity = default)
    {
        // Re-enable physics & colliders
        heldRB.isKinematic = false;
        heldRB.useGravity = originalUseGravity;

        foreach (var c in heldColliders) c.enabled = true;

        if (applyThrow)
        {
            // Clean throw
            heldRB.linearVelocity = Vector3.zero;
            heldRB.angularVelocity = Vector3.zero;
            heldRB.linearVelocity = throwVelocity; // or AddForce(..., VelocityChange)
        }

        heldRB = null;
        heldColliders = null;
    }

    Vector3 GetPickupOrigin()
    {
        return (feet != null) ? feet.position : transform.position;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = (feet != null) ? feet.position : transform.position;
        Gizmos.DrawWireSphere(origin, pickupRadius);
    }
}
