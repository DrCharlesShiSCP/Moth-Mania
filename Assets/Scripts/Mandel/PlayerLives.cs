// PlayerLives.cs
// Adds hit sound + screen shake when a life is actually lost.
// Source: :contentReference[oaicite:0]{index=0}

#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerLives : MonoBehaviour
{
    [Header("Lives")]
    public int maxLives = 3;
    public int currentLives;

    [Header("Damage")]
    [Tooltip("Seconds of invulnerability after getting hit (when using iFrame-respecting damage).")]
    public float iFrames = 0.75f;
    public LayerMask enemyLayers; // optional if you want layer-based hits
    public string enemyTag = "Enemy"; // optional if you want tag-based hits (not currently used)

    [Header("Audio")]
    [Tooltip("AudioSource used to play hit sounds. If left empty, will try GetComponent<AudioSource>() in Awake.")]
    public AudioSource audioSource;
    public AudioClip hitClip;
    [Range(0f, 1f)] public float hitVolume = 1f;
    [Tooltip("Small pitch variation helps avoid repetitive SFX.")]
    public Vector2 hitPitchRange = new Vector2(0.95f, 1.05f);

    [Header("Screen Shake (No Cinemachine)")]
    [Tooltip("What transform to shake (usually the camera transform, or a camera parent). If empty, will try Camera.main.")]
    public Transform shakeTarget;

    [Tooltip("How long the shake lasts (seconds).")]
    public float shakeDuration = 0.12f;

    [Tooltip("How far the camera is displaced at peak shake (world units). Start small like 0.05.")]
    public float shakeAmplitude = 0.06f;

    [Tooltip("How fast the shake jitters (higher = more vibration).")]
    public float shakeFrequency = 30f;

    [Tooltip("If true, shake uses localPosition (recommended if camera is parented).")]
    public bool shakeLocalPosition = true;

    [Header("Events")]
    public UnityEvent onLifeLost;
    public UnityEvent onDeath;
    public UnityEvent onLivesRefreshed;

    private float _canBeHitAfter = 0f;

    private Coroutine _shakeRoutine;
    private Vector3 _shakeStartPos;
    private bool _shakeHasStartPos = false;

    void Awake()
    {
        // Auto-wire AudioSource if not assigned
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Auto-wire shake target if not assigned
        if (shakeTarget == null && Camera.main != null)
            shakeTarget = Camera.main.transform;

        currentLives = Mathf.Max(1, maxLives);
        onLivesRefreshed?.Invoke();
    }

    /// <summary>
    /// Old API: lose exactly 1 life and respect iFrames.
    /// </summary>
    public void LoseOneLife()
    {
        ApplyDamage(1, respectIFrames: true);
    }

    /// <summary>
    /// Lose multiple lives, IGNORING iFrames (good for big traps).
    /// </summary>
    public void LoseLivesIgnoringIFrames(int amount)
    {
        ApplyDamage(amount, respectIFrames: false);
    }

    /// <summary>
    /// Lose multiple lives, respecting iFrames.
    /// </summary>
    public void LoseLives(int amount)
    {
        ApplyDamage(amount, respectIFrames: true);
    }

    /// <summary>
    /// Shared damage logic.
    /// Plays hit sound + shake ONLY if lives actually decreased.
    /// </summary>
    public void ApplyDamage(int amount, bool respectIFrames = true)
    {
        if (amount <= 0) return;

        // Respect iFrames if requested
        if (respectIFrames && Time.time < _canBeHitAfter)
            return;

        // Set new iFrame window
        _canBeHitAfter = Time.time + iFrames;

        int oldLives = currentLives;
        currentLives = Mathf.Max(0, currentLives - amount);

        bool lifeActuallyLost = currentLives != oldLives;

        if (lifeActuallyLost)
        {
            PlayHitSound();
            TriggerScreenShake();
            onLifeLost?.Invoke();
        }

        if (oldLives > 0 && currentLives <= 0)
            onDeath?.Invoke();

        Debug.Log($"[PlayerLives] DAMAGE {amount} (respectIFrames={respectIFrames}). {oldLives} -> {currentLives}");
    }

    private void PlayHitSound()
    {
        if (audioSource == null || hitClip == null) return;

        float prevPitch = audioSource.pitch;
        audioSource.pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
        audioSource.PlayOneShot(hitClip, hitVolume);
        audioSource.pitch = prevPitch;
    }

    private void TriggerScreenShake()
    {
        if (shakeTarget == null) return;
        if (shakeDuration <= 0f || shakeAmplitude <= 0f) return;

        // Restart shake cleanly if we get hit again mid-shake
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            RestoreShakeTargetPosition();
        }

        _shakeRoutine = StartCoroutine(ScreenShakeRoutine());
    }

    private IEnumerator ScreenShakeRoutine()
    {
        CacheShakeStartPosIfNeeded();

        float t = 0f;
        float seed = Random.value * 999f;

        while (t < shakeDuration)
        {
            t += Time.deltaTime;

            // 0..1
            float n = Mathf.Clamp01(t / shakeDuration);
            // Ease-out amplitude so it settles smoothly
            float damper = 1f - (n * n);

            // Pseudo-random jitter using Perlin
            float px = Mathf.PerlinNoise(seed, Time.time * shakeFrequency) * 2f - 1f;
            float py = Mathf.PerlinNoise(seed + 13.37f, Time.time * shakeFrequency) * 2f - 1f;

            Vector3 offset = new Vector3(px, py, 0f) * (shakeAmplitude * damper);

            ApplyShakeOffset(offset);

            yield return null;
        }

        RestoreShakeTargetPosition();
        _shakeRoutine = null;
    }

    private void CacheShakeStartPosIfNeeded()
    {
        if (_shakeHasStartPos) return;

        _shakeStartPos = shakeLocalPosition ? shakeTarget.localPosition : shakeTarget.position;
        _shakeHasStartPos = true;
    }

    private void ApplyShakeOffset(Vector3 offset)
    {
        if (shakeLocalPosition)
            shakeTarget.localPosition = _shakeStartPos + offset;
        else
            shakeTarget.position = _shakeStartPos + offset;
    }

    private void RestoreShakeTargetPosition()
    {
        if (shakeTarget == null) return;

        CacheShakeStartPosIfNeeded();

        if (shakeLocalPosition)
            shakeTarget.localPosition = _shakeStartPos;
        else
            shakeTarget.position = _shakeStartPos;
    }

    private void OnCollisionEnter(Collision c)
    {
        if (!enabled) return;

        // 1) SPECIAL CASE: Press kill zone only
        PressKillZone killZone = c.collider.GetComponent<PressKillZone>();
        if (killZone != null)
        {
            // Apply multi-life damage, ignoring iFrames
            LoseLivesIgnoringIFrames(killZone.damage);
            return;
        }

        // 2) NORMAL ENEMY COLLISION (layer-based)
        bool layerMatch = (enemyLayers.value & (1 << c.collider.gameObject.layer)) != 0;
        if (layerMatch)
        {
            LoseOneLife();
        }

        // Optional tag-based hit:
        // if (c.collider.CompareTag(enemyTag)) LoseOneLife();
    }

    // Helper if you want to reset all lives on respawn
    public void RefillLives()
    {
        currentLives = Mathf.Max(1, maxLives);
        onLivesRefreshed?.Invoke();
    }
}

// tiny attribute to gray-out fields in inspector (not currently used by currentLives)
public class ReadOnlyInInspectorAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyInInspectorAttribute))]
public class ReadOnlyInInspectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;
    }
}
#endif
