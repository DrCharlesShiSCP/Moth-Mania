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
    [Tooltip("Seconds of invulnerability after getting hit.")]
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

    public void LoseOneLife()
    {
        if (Time.time < _canBeHitAfter) return;
        _canBeHitAfter = Time.time + iFrames;

        currentLives = Mathf.Max(0, currentLives - 1);
        onLifeLost?.Invoke();

        if (currentLives <= 0)
            onDeath?.Invoke();

        Debug.Log($"[PlayerLives] HIT at {Time.time:F2}. Lives before: {currentLives}");

    }



    void OnCollisionEnter(Collision c)
    {
        if (!enabled) return;

        bool layerMatch = (enemyLayers.value & (1 << c.collider.gameObject.layer)) != 0;
        if (layerMatch) LoseOneLife();
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
