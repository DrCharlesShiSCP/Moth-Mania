using System.Collections;
using UnityEngine;

public class HeadlightLockZone : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag of the player object that enters/leaves this field.")]
    public string playerTag = "Player";

    [Tooltip("If true, this field actively blocks headlight toggling.")]
    public bool fieldActive = true;

    // Track if the player is inside so we know whether to lock/unlock
    private bool playerInside = false;

    [Header("VFX")]
    [Tooltip("Optional spark effect that plays when the field is disabled.")]
    public ParticleSystem sparkEffect;

    [Tooltip("Mesh renderers that will fade out when the field is disabled.")]
    public MeshRenderer[] renderersToFade;

    [Tooltip("How long the fade-out should last (seconds).")]
    public float fadeDuration = 1.0f;

    [Tooltip("Disable colliders once fade has completed.")]
    public bool disableCollidersAfterFade = true;

    [Tooltip("Disable the GameObject after fade completes.")]
    public bool disableObjectAfterFade = false;

    private bool hasShutdownVFXPlayed = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!fieldActive) return;

        if (other.CompareTag(playerTag))
        {
            playerInside = true;

            if (MothFlockController.Instance != null)
            {
                // 1) Lock toggle
                MothFlockController.Instance.canToggleHeadlight = false;

                // 2) Force headlight OFF while in the field
                MothFlockController.Instance.SetHeadlight(false);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!fieldActive) return;

        if (other.CompareTag(playerTag))
        {
            playerInside = false;

            if (MothFlockController.Instance != null)
            {
                MothFlockController.Instance.canToggleHeadlight = true;
            }
        }
    }

    /// <summary>
    /// Call this from the Particle Field Transmitter
    /// when the player activates it or the crate breaks it.
    /// </summary>
    public void DisableField()
    {
        // Already disabled? Do nothing.
        if (!fieldActive) return;

        fieldActive = false;

        // If the player is currently inside the zone,
        // immediately restore their ability to toggle the headlight.
        if (playerInside && MothFlockController.Instance != null)
        {
            MothFlockController.Instance.canToggleHeadlight = true;
        }

        // Trigger spark and fade VFX once
        PlayShutdownVFX();
    }

    private void PlayShutdownVFX()
    {
        if (hasShutdownVFXPlayed) return;
        hasShutdownVFXPlayed = true;

        // Play spark effect
        if (sparkEffect != null)
        {
            sparkEffect.Play();
        }

        // Start fade-out on mesh renderers
        if (renderersToFade != null && renderersToFade.Length > 0 && fadeDuration > 0f)
        {
            StartCoroutine(FadeRenderersOut());
        }
        else if (disableCollidersAfterFade)
        {
            // If no fade, we can just immediately disable colliders
            DisableAllColliders();
        }
    }

    private IEnumerator FadeRenderersOut()
    {
        // Cache original colors per material
        var allMaterials = new System.Collections.Generic.List<Material[]>();
        foreach (var mr in renderersToFade)
        {
            if (mr != null)
            {
                // We clone this so we don't permanently modify shared materials across instances
                var mats = mr.materials;
                allMaterials.Add(mats);
            }
            else
            {
                allMaterials.Add(null);
            }
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float alpha = 1f - t;

            // Apply alpha to all materials
            for (int i = 0; i < renderersToFade.Length; i++)
            {
                var mr = renderersToFade[i];
                var mats = allMaterials[i];
                if (mr == null || mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;

                    if (mats[m].HasProperty("_Color"))
                    {
                        Color c = mats[m].color;
                        c.a = alpha;
                        mats[m].color = c;
                    }
                }
            }

            yield return null;
        }

        // Ensure alpha is fully 0 at the end
        for (int i = 0; i < renderersToFade.Length; i++)
        {
            var mr = renderersToFade[i];
            var mats = allMaterials[i];
            if (mr == null || mats == null) continue;

            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;

                if (mats[m].HasProperty("_Color"))
                {
                    Color c = mats[m].color;
                    c.a = 0f;
                    mats[m].color = c;
                }
            }

            // Optionally just disable the renderer entirely
            mr.enabled = false;
        }

        if (disableCollidersAfterFade)
        {
            DisableAllColliders();
        }

        if (disableObjectAfterFade)
        {
            gameObject.SetActive(false);
        }
    }

    private void DisableAllColliders()
    {
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
    }
}
