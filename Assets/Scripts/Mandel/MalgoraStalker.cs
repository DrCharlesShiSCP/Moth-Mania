using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MalgoraStalker : MonoBehaviour
{
    [Header("Player Resolve")]
    [Tooltip("If set, Malgora will auto-find player by tag when player is null/stale.")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Fallback: find by layer name (your project uses PlayerEntity).")]
    [SerializeField] private string playerLayerName = "PlayerEntity";

    [Header("Movement")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private bool keepAltitude = true;
    [SerializeField] private float stopDistance = 0.25f;

    [Header("Damage")]
    [SerializeField] private Collider damageTrigger; // on Malgora, trigger
    [SerializeField] private int damage = 1;
    [SerializeField] private float hitCooldown = 0.75f;
    [SerializeField] private float activationGrace = 0.15f;

    [Header("Layers")]
    [SerializeField] private string stalkerLayerName = "MalgoraStalker";
    [SerializeField] private string playerEntityLayerName = "PlayerEntity";

    [Header("Rule")]
    [Tooltip("True = chase when headlight/nightvision is OFF.")]
    [SerializeField] private bool chaseWhenHeadlightOff = true;

    private Transform player;
    private int stalkerLayer;
    private int playerLayer;
    private Rigidbody rb;

    private bool lastHeadlightOn;
    private bool chasing;
    private float lastHitTime = -999f;
    private float chaseEnabledTime = -999f;

    private void Awake()
    {
        stalkerLayer = LayerMask.NameToLayer(stalkerLayerName);
        playerLayer = LayerMask.NameToLayer(playerEntityLayerName);

        if (stalkerLayer < 0 || playerLayer < 0)
        {
            Debug.LogError($"[MalgoraStalker] Missing layers. Need '{stalkerLayerName}' and '{playerEntityLayerName}'.");
            enabled = false;
            return;
        }

        gameObject.layer = stalkerLayer;

        if (damageTrigger == null) damageTrigger = GetComponent<Collider>();
        if (damageTrigger == null)
        {
            Debug.LogError("[MalgoraStalker] No collider found. Add Sphere/Capsule collider to Malgora.");
            enabled = false;
            return;
        }
        damageTrigger.isTrigger = true;

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Initialize headlight state
        lastHeadlightOn = GetHeadlightOnSafe();
        ApplyFromHeadlight(lastHeadlightOn, force: true);

        // Find player at startup
        ResolvePlayer(force: true);
    }

    private void Update()
    {
        // 1) Keep player reference valid (handles respawns / wrong assignment)
        ResolvePlayer(force: false);

        // 2) Detect nightvision/headlight toggle and apply immediately
        bool headlightOn = GetHeadlightOnSafe();
        if (headlightOn != lastHeadlightOn)
        {
            lastHeadlightOn = headlightOn;
            ApplyFromHeadlight(headlightOn, force: false);
        }

        // 3) Chase continuously (always uses CURRENT player.position)
        if (!chasing || player == null) return;

        Vector3 targetPos = player.position;
        if (keepAltitude) targetPos.y = transform.position.y;

        Vector3 toTarget = targetPos - transform.position;
        if (toTarget.sqrMagnitude <= stopDistance * stopDistance) return;

        Vector3 dir = toTarget.normalized;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

        transform.position += dir * (speed * Time.deltaTime);
    }

    private void ResolvePlayer(bool force)
    {
        // If we already have a valid player, keep it.
        if (!force && player != null && player.gameObject.activeInHierarchy) return;

        // Try tag first
        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null)
            {
                player = go.transform;
                // Debug.Log("[MalgoraStalker] Player resolved by tag.");
                return;
            }
        }

        // Fallback: find any object on PlayerEntity layer
        int pl = LayerMask.NameToLayer(playerLayerName);
        if (pl >= 0)
        {
            // This is heavier than tag; only runs when player is null/stale.
            var all = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.layer == pl)
                {
                    player = all[i];
                    // Debug.Log("[MalgoraStalker] Player resolved by layer.");
                    return;
                }
            }
        }

        // If we get here, we didn't find player. We'll try again next frame.
        player = null;
    }

    private bool GetHeadlightOnSafe()
    {
        if (MothFlockController.Instance == null) return false;
        return MothFlockController.Instance.headlightOn;
    }

    private void ApplyFromHeadlight(bool headlightOn, bool force)
    {
        bool shouldChase = chaseWhenHeadlightOff ? !headlightOn : headlightOn;
        if (!force && chasing == shouldChase) return;

        chasing = shouldChase;

        // When not chasing: no interaction with player
        Physics.IgnoreLayerCollision(stalkerLayer, playerLayer, !chasing);

        if (damageTrigger != null) damageTrigger.enabled = chasing;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;

        if (chasing) chaseEnabledTime = Time.time;

        Debug.Log($"[MalgoraStalker] headlightOn={headlightOn} chasing={chasing} player={(player ? player.name : "NULL")}");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!chasing) return;
        if (Time.time - chaseEnabledTime < activationGrace) return;
        if (Time.time - lastHitTime < hitCooldown) return;

        // Only damage the player layer
        if (other.gameObject.layer != playerLayer) return;

        // Find PlayerLives (on root or parent)
        PlayerLives lives = other.GetComponentInParent<PlayerLives>();
        if (lives == null) return;

        // Deal damage (1 life, respects iFrames)
        lives.LoseOneLife();

        lastHitTime = Time.time;

        Debug.Log("[MalgoraStalker] Hit player → LoseOneLife()");
    }

}

public interface IDamageable
{
    void TakeDamage(int amount);
}
