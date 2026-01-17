// HeadlightGlitchBurst.cs
// Fix: safely handle cases where the coroutine host GameObject is inactive.
// - If the object is inactive, we activate it before starting the coroutine.
// - We also ensure the overlay+material are re-hooked on enable/start.
// - We keep the overlay disabled when intensity is 0, so leaving this object active is cheap.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HeadlightGlitchBurst : MonoBehaviour
{
    [Header("UI Overlay")]
    public RawImage overlay;                 // full-screen RawImage
    public Material glitchMat;               // material using UI/GlitchOverlay_NoTint
    public string intensityProp = "_Intensity";

    [Header("Burst Shape")]
    public float peak = 1f;
    public float duration = 0.12f;

    Coroutine _co;

    void Awake()
    {
        EnsureHooked();
        SetIntensity(0f);
    }

    void OnEnable()
    {
        // If something re-enabled us mid-game, make sure we're wired.
        EnsureHooked();
        SetIntensity(0f);
    }

    void EnsureHooked()
    {
        if (overlay != null && glitchMat != null)
        {
            // Always (re)assign the material so it doesn't get lost if UI gets rebuilt.
            overlay.material = glitchMat;
        }
    }

    /// <summary>
    /// Triggers a short glitch pulse. Safe to call even if this GameObject was disabled.
    /// </summary>
    public void Burst()
    {
        // Critical fix: coroutines cannot start on inactive GameObjects.
        // If someone disabled the host, we re-enable it.
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        // If the component itself was disabled, enable it too.
        if (!enabled)
            enabled = true;

        EnsureHooked();

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(BurstRoutine());
    }

    IEnumerator BurstRoutine()
    {
        float t = 0f;

        // quick ramp up + down (triangle pulse)
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // ignore timescale if you pause game
            float x = Mathf.Clamp01(t / duration);
            float tri = 1f - Mathf.Abs(x * 2f - 1f); // 0->1->0
            SetIntensity(tri * peak);
            yield return null;
        }

        SetIntensity(0f);
        _co = null;
    }

    void SetIntensity(float v)
    {
        if (glitchMat != null)
            glitchMat.SetFloat(intensityProp, v);

        if (overlay != null)
            overlay.enabled = v > 0.001f;
    }
}
