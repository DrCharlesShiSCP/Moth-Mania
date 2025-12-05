using UnityEngine;

public class PlayerPickupThrow : MonoBehaviour
{
    [Header("Inputs")]
    [Tooltip("E = pick up when empty, drop when holding")]
    public KeyCode pickupKey = KeyCode.E;

    [Tooltip("R = throw while holding")]
    public KeyCode throwKey = KeyCode.R;

    [Header("Pickup (Radius)")]
    public Transform feet;             // origin for the radius
    public float pickupRadius = 0.6f;
    public bool preferClosest = true;

    [Header("Holding (two Z positions)")]
    [Tooltip("Used when last input was D (front, +Z). Place in front of the player on +Z.")]
    public Transform holdPointFrontZ;
    [Tooltip("Used when last input was A (back, -Z). Place behind the player on -Z.")]
    public Transform holdPointBackZ;
    public float holdLerp = 20f;
    public bool freezeHeldRotation = true;
    [Tooltip("If true, object slides between hold points when A/D changes while holding.")]
    public bool liveFlipWhileHolding = true;

    [Header("Throwing (along Z)")]
    [Tooltip("Z-axis throw speed: +Z when D was last, -Z when A was last.")]
    public float zThrowSpeed = 12f;
    [Tooltip("Upward impulse for a small arc.")]
    public float upwardThrowImpulse = 4f;
    [Tooltip("Inherit player's current Z velocity when throwing.")]
    public bool inheritPlayerZVelocity = true;

    [Header("Funni")]
    [Tooltip("loud = funny")]
    public AudioClip metaldrop;
    public AudioSource sourceOnPlayer;

    // runtime
    private Rigidbody heldRB;
    private Collider[] heldColliders;
    private bool originalUseGravity;
    private Rigidbody playerRB;

    // last Z intent: -1 (A/back/-Z), +1 (D/front/+Z)
    private int lastZDir = +1; // default “front/+Z”

    void Awake()
    {
        if (feet == null)
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                var temp = new GameObject("FeetAuto");
                temp.transform.SetParent(transform);
                temp.transform.position = new Vector3(
                    col.bounds.center.x,
                    col.bounds.min.y + 0.02f,
                    col.bounds.center.z
                );
                feet = temp.transform;
            }
        }

        playerRB = GetComponent<Rigidbody>(); // optional, for inheritPlayerZVelocity
    }

    void Update()
    {
        // Track last A/D input for Z direction
        if (Input.GetKeyDown(KeyCode.A)) lastZDir = -1; // back
        if (Input.GetKeyDown(KeyCode.D)) lastZDir = +1; // front

        // Also honor Horizontal axis if you’re feeding A/D into it
        float axis = Input.GetAxisRaw("Horizontal");
        if (axis > 0.5f) lastZDir = +1;   // treat right as front (+Z)
        else if (axis < -0.5f) lastZDir = -1; // left as back (-Z)

        // =====================================================
        // ===  When holding an object =========================
        // =====================================================
        if (heldRB != null)
        {
            var target = CurrentHoldPoint();
            if (target != null && (liveFlipWhileHolding || !HasSignChangedThisFrame()))
            {
                heldRB.position = Vector3.Lerp(
                    heldRB.position,
                    target.position,
                    holdLerp * Time.deltaTime
                );

                if (freezeHeldRotation)
                    heldRB.rotation = target.rotation;
            }

            // Throw on R
            if (Input.GetKeyDown(throwKey))
            {
                ThrowHeld();
                return;
            }

            // Drop on E (toggle behavior)
            if (Input.GetKeyDown(pickupKey))
            {
                ReleaseHeld(applyThrow: false);
                return;
            }

            return;
        }

        // =====================================================
        // ===  Not holding anything — E picks up  =============
        // =====================================================
        if (Input.GetKeyDown(pickupKey))
        {
            TryPickupByRadius();
        }
    }

    void TryPickupByRadius()
    {
        Vector3 origin = GetPickupOrigin();
        Collider[] hits = Physics.OverlapSphere(origin, pickupRadius, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        Collider best = null;
        float bestDist = float.MaxValue;

        foreach (var c in hits)
        {
            if (!c || !c.gameObject.activeInHierarchy) continue;
            if (!c.CompareTag("Throwable")) continue;

            var rb = c.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;

            if (!preferClosest)
            {
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
            TryBeginHold(best.attachedRigidbody);
    }

    bool TryBeginHold(Rigidbody rb)
    {
        heldRB = rb;

        originalUseGravity = heldRB.useGravity;
        heldRB.useGravity = false;
        heldRB.isKinematic = true;

        heldColliders = heldRB.GetComponentsInChildren<Collider>();
        foreach (var c in heldColliders) c.enabled = false;

        var hp = CurrentHoldPoint();
        if (hp != null)
        {
            heldRB.position = hp.position;
            heldRB.rotation = hp.rotation;
        }
        return true;
    }

    void ThrowHeld()
    {
        if (heldRB == null) return;

        // +Z if D was last; -Z if A was last
        Vector3 dir = (lastZDir >= 0) ? Vector3.forward : Vector3.back;
        Vector3 vel = dir * zThrowSpeed + Vector3.up * upwardThrowImpulse;

        if (inheritPlayerZVelocity && playerRB != null)
            vel.z += playerRB.linearVelocity.z; // keeping your original use of linearVelocity

        ReleaseHeld(applyThrow: true, throwVelocity: vel);
    }

    void ReleaseHeld(bool applyThrow, Vector3 throwVelocity = default)
    {
        if (heldRB == null) return;

        heldRB.isKinematic = false;
        heldRB.useGravity = originalUseGravity;

        if (heldColliders != null)
        {
            foreach (var c in heldColliders) c.enabled = true;
        }

        if (applyThrow)
        {
            heldRB.linearVelocity = Vector3.zero;
            heldRB.angularVelocity = Vector3.zero;
            heldRB.linearVelocity = throwVelocity; // or AddForce(..., VelocityChange)
        }

        heldRB = null;
        heldColliders = null;

        if (sourceOnPlayer != null && metaldrop != null)
        {
            sourceOnPlayer.PlayOneShot(metaldrop);
        }

        Debug.Log("DroppedPipe");
    }

    Transform CurrentHoldPoint()
    {
        return (lastZDir >= 0) ? holdPointFrontZ : holdPointBackZ;
    }

    Vector3 GetPickupOrigin()
    {
        return (feet != null) ? feet.position : transform.position;
    }

    // Reserved if you later want to detect within-frame flips; unused now.
    bool HasSignChangedThisFrame() { return false; }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = (feet != null) ? feet.position : transform.position;
        Gizmos.DrawWireSphere(origin, pickupRadius);

        Gizmos.color = Color.green;
        if (holdPointFrontZ != null) Gizmos.DrawWireSphere(holdPointFrontZ.position, 0.05f);
        Gizmos.color = Color.magenta;
        if (holdPointBackZ != null) Gizmos.DrawWireSphere(holdPointBackZ.position, 0.05f);
    }
}
