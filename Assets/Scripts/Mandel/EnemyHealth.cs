using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 1;                // 1 = dies on first valid hit
    public int currentHealth = 1;

    [Header("Hit Detection")]
    [Tooltip("Tag on objects that can kill/damage this enemy.")]
    public string throwableTag = "Throwable";

    [Tooltip("If > 0, require the collision's relative speed to be at least this to count as a hit.")]
    public float minImpactSpeed = 1.0f;

    [Tooltip("Damage applied per valid hit. Set to maxHealth for one-hit kills.")]
    public int damagePerHit = 1;

    [Header("Death")]
    [Tooltip("Optional delay before destroying (lets VFX/SFX play).")]
    public float destroyDelay = 0.05f;
    public GameObject deathVFX;              // optional (instantiate at hit point)
    public AudioClip deathSFX;               // optional
    public bool disableAgentOnDeath = true;  // turn off NavMeshAgent first (prevents errors)
    public bool disableCollidersOnDeath = true;

    AudioSource _audio;
    NavMeshAgent _agent;
    bool _dead;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _agent = GetComponent<NavMeshAgent>();
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    // --- Non-trigger collisions (e.g., rigidbody vs rigidbody) ---
    void OnCollisionEnter(Collision c)
    {
        if (_dead) return;
        if (!c.collider.CompareTag(throwableTag)) return;

        if (minImpactSpeed > 0f && c.relativeVelocity.magnitude < minImpactSpeed) return;

        Vector3 hitPoint = c.contacts.Length > 0 ? c.contacts[0].point : transform.position;
        ApplyHit(hitPoint);
    }

    // --- Trigger collisions (e.g., throwable has isTrigger = true) ---
    void OnTriggerEnter(Collider other)
    {
        if (_dead) return;
        if (!other.CompareTag(throwableTag)) return;

        // Optional speed check if the throwable has a Rigidbody
        if (minImpactSpeed > 0f)
        {
            var rb = other.attachedRigidbody;
            if (rb && rb.linearVelocity.magnitude < minImpactSpeed) return;
        }

        ApplyHit(other.ClosestPoint(transform.position));
    }

    void ApplyHit(Vector3 fxPoint)
    {
        if (_dead) return;

        currentHealth -= Mathf.Max(1, damagePerHit);
        if (currentHealth > 0) return;

        // Die
        _dead = true;

        // Optional: stop AI movement safely
        if (disableAgentOnDeath && _agent)
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.enabled = false;
        }

        // Optional: disable colliders so the corpse doesn�t keep interacting
        if (disableCollidersOnDeath)
        {
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        }

        // VFX/SFX
        if (deathVFX) Instantiate(deathVFX, fxPoint, Quaternion.identity);
        if (deathSFX)
        {
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.PlayOneShot(deathSFX);
        }

        // Finally destroy
        Destroy(gameObject, destroyDelay);
    }
}
