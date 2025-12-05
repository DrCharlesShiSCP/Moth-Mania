using System.Collections;
using UnityEngine;

public class HeadlightLockZone : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Tag of the player object that enters/leaves this field.")]
    public string playerTag = "Player";

    [Tooltip("If true, this field actively blocks headlight toggling.")]
    public bool fieldActive = true;

    // Tracks if the player is currently inside the trigger
    private bool playerInside = false;

    [Header("Headlight Control")]
    [Tooltip("If true, the zone forces the headlight ON. If false, it forces the headlight OFF.")]
    public bool forceHeadlightOn = false;

    [Tooltip("If true, the zone enforces the forced headlight state while the player is inside.")]
    public bool enforceForcedState = true;

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
                // Lock toggle ability
                MothFlockController.Instance.canToggleHeadlight = false;

                // Enforce forced state if enabled
                if (enforceForcedState)
                {
                    MothFlockController.Instance.SetHeadlight(forceHeadlightOn);
                }
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
                // Restore toggle ability
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
        if (!fieldActive) return;

        fieldActive = false;

        // Restore toggle ability if player is inside
        if (playerInside && MothFlockController.Instance != null)
        {
            MothFlockController.Instance.canToggleHeadlight = true;
        }

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

        // Begin fade-out
        if (renderersToFade != null && renderersToFade.Length > 0 && fadeDuration > 0f)
        {
            StartCoroutine(FadeRenderersOut());
        }
        else if (disableCollidersAfterFade)
        {
            DisableAllColliders();
        }
    }

    private IEnumerator FadeRenderersOut()
    {
        var allMaterials = new System.Collections.Generic.List<Material[]>();

        // Cache materials
        foreach (var mr in renderersToFade)
        {
            if (mr != null)
            {
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

        // Ensure final alpha is 0
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
