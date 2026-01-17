using System.Collections.Generic;
using UnityEngine;

public class NightCrawlerPriorityRadius : MonoBehaviour
{
    [Header("Refs")]
    public MothFlockController flock;
    public Transform target;

    [Header("Priority Radius")]
    [Tooltip("True world-space radius around the NightCrawler target.")]
    public float radius = 6f;

    [Tooltip("How often to evaluate (lower = snappier, higher = cheaper).")]
    public float tickInterval = 0.1f;

    [Header("Debug")]
    public bool showRadiusAlways = true;
    public bool showRadiusWhenSelected = true;
    public Color radiusColor = new Color(0.7f, 0.2f, 1f, 0.8f);
    public Color radiusFillColor = new Color(0.7f, 0.2f, 1f, 0.15f);

    [Header("Advanced")]
    [Tooltip("If true, measures radius in full 3D. If false, ignores vertical (Y) difference.")]
    public bool ignoreY = false;

    private float _t;
    private readonly HashSet<Transform> _inside = new HashSet<Transform>();

    void Awake()
    {
        if (flock == null)
            flock = MothFlockController.Instance != null
                ? MothFlockController.Instance
                : FindObjectOfType<MothFlockController>();

        if (target == null)
            target = transform;
    }

    void Update()
    {
        if (flock == null || flock.player == null) return;

        _t += Time.deltaTime;
        if (_t < tickInterval) return;
        _t = 0f;

        float r2 = radius * radius;

        var moths = flock.mothBabies;
        for (int i = 0; i < moths.Count; i++)
        {
            var m = moths[i];
            if (m == null) continue;

            Vector3 mp = m.position;
            Vector3 tp = target.position;

            // IMPORTANT:
            // Do NOT lane-lock tp.x here.
            // This must be true distance to the NightCrawler or it will "force" moths across the whole lane,
            // breaking "headlight ON -> nearest lamp".
            if (ignoreY)
            {
                mp.y = 0f;
                tp.y = 0f;
            }

            bool nowInside = (mp - tp).sqrMagnitude <= r2;
            bool wasInside = _inside.Contains(m);

            if (nowInside && !wasInside)
            {
                _inside.Add(m);
                flock.ForceLightTarget(m, target);
            }
            else if (!nowInside && wasInside)
            {
                _inside.Remove(m);
                flock.ClearForcedLightTarget(m, target);
            }
        }
    }

    void OnDisable()
    {
        if (flock == null) return;

        foreach (var m in _inside)
            if (m != null)
                flock.ClearForcedLightTarget(m, target);

        _inside.Clear();
    }

    // =========================
    // VISUAL DEBUG (GIZMOS)
    // =========================

    void DrawRadiusGizmo()
    {
        if (target == null) return;

        Vector3 center = target.position;

        // Gizmo shows the TRUE world-space radius.
        // (No lane-locking here either, for the same reason.)
        if (ignoreY) center.y = 0f;

        Gizmos.color = radiusFillColor;
        Gizmos.DrawSphere(center, radius);

        Gizmos.color = radiusColor;
        Gizmos.DrawWireSphere(center, radius);
    }

    void OnDrawGizmos()
    {
        if (!showRadiusAlways) return;
        DrawRadiusGizmo();
    }

    void OnDrawGizmosSelected()
    {
        if (!showRadiusWhenSelected) return;
        DrawRadiusGizmo();
    }
}
