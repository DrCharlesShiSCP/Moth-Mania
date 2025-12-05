using UnityEngine;

public class MothamGearParticleDisruptionTransmitter : MonoBehaviour
{
    [Header("References")]
    public HeadlightLockZone fieldZone;   // Drag your HeadlightLockZone here

    [Header("Settings")]
    public string playerTag = "Player";
    public string crateTag = "Crate";

    [Tooltip("Key the player presses to interact (e.g. E, F, Space).")]
    public KeyCode interactKey = KeyCode.E;

    private bool _used = false;

    void Awake()
    {
        // Just a safety check
        var col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("[Transmitter] No Collider found on this object.");
        }
    }

    // --- PLAYER INTERACTION (trigger) ---

    private void OnTriggerStay(Collider other)
    {
        if (_used) return;

        if (other.CompareTag(playerTag))
        {
            if (Input.GetKeyDown(interactKey))
            {
                Debug.Log("[Transmitter] Player pressed interact key inside trigger. Using transmitter.");
                UseTransmitter();
            }
        }

        // Optional: also let crate work via trigger, in case your crate uses triggers
        if (other.CompareTag(crateTag))
        {
            Debug.Log("[Transmitter] Crate entered trigger. Using transmitter.");
            UseTransmitter();
        }
    }

    // --- CRATE BREAK (non-trigger collision) ---

    private void OnCollisionEnter(Collision collision)
    {
        if (_used) return;

        if (collision.collider.CompareTag(crateTag))
        {
            Debug.Log("[Transmitter] Crate collided with transmitter. Using transmitter.");
            UseTransmitter();
        }
    }

    // --- CORE LOGIC ---

    private void UseTransmitter()
    {
        if (_used) return;
        _used = true;

        Debug.Log("[Transmitter] Transmitter activated. Disabling field.");

        if (fieldZone != null)
        {
            fieldZone.DisableField();
        }
        else
        {
            Debug.LogWarning("[Transmitter] fieldZone reference is NOT set in the inspector.");
        }

        // Optional: VFX / SFX / disable mesh or collider
        // GetComponent<Collider>().enabled = false;
        // GetComponentInChildren<ParticleSystem>()?.Stop();
        // gameObject.SetActive(false);
    }
}