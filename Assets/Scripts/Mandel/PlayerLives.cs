using UnityEditor;
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
    public string enemyTag = "Enemy"; // optional if you want tag-based hits

    [Header("Events")]
    public UnityEvent onLifeLost;
    public UnityEvent onDeath;
    public UnityEvent onLivesRefreshed;

    float _canBeHitAfter = 0f;

    void Awake()
    {
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
    /// Lose multiple lives, respecting iFrames (like a cluster of hits).
    /// </summary>
    /// 
    public void LoseLivesIgnoringIFrames(int amount)
    {
        ApplyDamage(amount, respectIFrames: false);
    }



    public void LoseLives(int amount)
    {
        ApplyDamage(amount, respectIFrames: true);
    }

    /// <summary>
    /// Lose multiple lives in a single hit, IGNORING existing iFrames.
    /// Good for big traps like the hydraulic press.
    /// </summary>


    /// <summary>
    /// Shared damage logic.
    /// </summary>
    /// 
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

        if (currentLives != oldLives)
            onLifeLost?.Invoke();

        if (oldLives > 0 && currentLives <= 0)
            onDeath?.Invoke();

        Debug.Log($"[PlayerLives] DAMAGE {amount} (respectIFrames={respectIFrames}). {oldLives} -> {currentLives}");
    }

   
    private void OnCollisionEnter(Collision c)
    {
        if (!enabled) return;

        // 1) SPECIAL CASE: Hydraulic press hits = big damage
        var press = c.collider.GetComponentInParent<HydraulicPressTrap>();
        if (press != null)
        {
            // Take 3 lives in one go, ignoring iFrames
            LoseLivesIgnoringIFrames(3);
            return;
        }

        // 2) NORMAL ENEMY COLLISION: 1 life, respects iFrames
        bool layerMatch = (enemyLayers.value & (1 << c.collider.gameObject.layer)) != 0;
        if (layerMatch)
        {
            LoseOneLife();
        }
    }


    // Helper if you want to reset all lives on respawn
    public void RefillLives()
    {
        currentLives = Mathf.Max(1, maxLives);
        onLivesRefreshed?.Invoke();
    }
}

// tiny attribute to gray-out currentLives in inspector
public class ReadOnlyInInspectorAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyInInspectorAttribute))]
public class ReadOnlyInInspectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false; EditorGUI.PropertyField(position, property, label); GUI.enabled = true;
    }
}
#endif
