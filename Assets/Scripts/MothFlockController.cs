using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MothFlockController : MonoBehaviour
{
    public enum FlockState { FollowLine, SeekLight }

    [Header("References")]
    public Transform player;
    public List<Transform> mothBabies = new List<Transform>();

    [Header("Headlight Control")]
    public bool controlHeadlightWithKey = true;
    public KeyCode toggleKey = KeyCode.G;
    public bool headlightOn = false;              // initial state

    [Header("Headlight UI (optional)")]
    public TMP_Text headlightStatusText;
    public string onText = "ON";
    public string offText = "OFF";

    [Header("Recall Rule")]
    [Tooltip("Player must be within this range of the nearest Lightpole to LATCH recall when headlight turns OFF.")]
    public float playerLightReturnRange = 6f;

    [Header("Follow Line Settings")]
    public float followGapDistance = 0.6f;
    public float followLerp = 10f;

    [Header("Seek Light Settings")]
    public float seekSpeed = 8f;
    public float arriveRadius = 0.3f;

    [Header("World Constraints")]
    public bool lockXToPlayer = true;

    [Header("Lookup")]
    public string lightpoleTag = "Lightpole";

    [Header("Debug (read-only)")]
    public FlockState state;

    // --- internals ---
    readonly List<Vector3> trail = new List<Vector3>();
    float gapMetersAccum;
    Vector3 lastTrailPos;
    float computedTrailLength;

    bool recallLatched = false;        // <-- stays true while headlight is OFF, once latched
    bool prevHeadlightOn;

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("[MothFlockController] Player is not assigned.");
            enabled = false; return;
        }

        prevHeadlightOn = headlightOn;
        UpdateHeadlightUI();

        // Initialize latch if we start OFF and near a pole
        Transform nearest = FindNearestLightTo(player.position, out float d);
        recallLatched = !headlightOn && nearest != null && d <= playerLightReturnRange;

        RebuildTrail();
    }

    void Update()
    {
        // Toggle headlight with G
        if (controlHeadlightWithKey && Input.GetKeyDown(toggleKey))
        {
            headlightOn = !headlightOn;
            UpdateHeadlightUI();
        }

        // Detect ON/OFF transitions to manage the latch
        if (headlightOn && !prevHeadlightOn)
        {
            // just turned ON ¡ú clear latch
            recallLatched = false;
        }
        else if (!headlightOn && prevHeadlightOn)
        {
            // just turned OFF ¡ú latch if within proximity NOW
            Transform nearest = FindNearestLightTo(player.position, out float dNow);
            if (nearest != null && dNow <= playerLightReturnRange)
                recallLatched = true;
            // else remain as-is (could already be latched from earlier)
        }
        prevHeadlightOn = headlightOn;

        // Decide behavior:
        // ON ¡ú always SeekLight
        // OFF ¡ú FollowLine if latched, otherwise SeekLight
        if (headlightOn)
        {
            if (state != FlockState.SeekLight) state = FlockState.SeekLight;
            SeekNearestLightpoles();
        }
        else if (recallLatched)
        {
            if (state != FlockState.FollowLine)
            {
                state = FlockState.FollowLine;
                RebuildTrail(); // snap formation when switching back
            }
            UpdateTrailFromPlayer();
            PlaceMothsAlongTrail();
        }
        else
        {
            if (state != FlockState.SeekLight) state = FlockState.SeekLight;
            SeekNearestLightpoles();
        }
    }

    // ---------- UI ----------
    void UpdateHeadlightUI()
    {
        if (headlightStatusText != null)
            headlightStatusText.text = headlightOn ? onText : offText;
    }

    // ---------- Light search ----------
    Transform FindNearestLightTo(Vector3 pos, out float distance)
    {
        distance = float.PositiveInfinity;
        Transform best = null;
        var lights = GameObject.FindGameObjectsWithTag(lightpoleTag);
        for (int i = 0; i < lights.Length; i++)
        {
            Vector3 lp = lights[i].transform.position;
            if (lockXToPlayer) lp.x = player.position.x;
            float d = Vector3.Distance(pos, lp);
            if (d < distance) { distance = d; best = lights[i].transform; }
        }
        return best;
    }

    // ---------- FOLLOW LINE ----------
    void UpdateTrailFromPlayer()
    {
        Vector3 p = player.position;
        if (lockXToPlayer) p.x = player.position.x;

        if (trail.Count == 0)
        {
            trail.Add(p);
            lastTrailPos = p;
            return;
        }

        float moved = Vector3.Distance(lastTrailPos, p);
        if (moved > 0.001f)
        {
            gapMetersAccum += moved;
            lastTrailPos = p;
        }

        const float segmentLen = 0.05f;
        while (gapMetersAccum >= segmentLen)
        {
            Vector3 prev = trail[trail.Count - 1];
            Vector3 dir = (p - prev);
            float len = dir.magnitude;
            if (len < 1e-4f) break;
            dir /= len;

            Vector3 nextPoint = prev + dir * segmentLen;
            trail.Add(nextPoint);
            gapMetersAccum -= segmentLen;
        }

        int neededPoints = Mathf.CeilToInt(computedTrailLength / 0.05f) + 5;
        while (trail.Count > neededPoints) trail.RemoveAt(0);
    }

    void PlaceMothsAlongTrail()
    {
        if (trail.Count < 2) return;
        float totalDist = (trail.Count - 1) * 0.05f;

        for (int i = 0; i < mothBabies.Count; i++)
        {
            Transform m = mothBabies[i];
            if (m == null) continue;

            float behind = Mathf.Min(followGapDistance * (i + 1), totalDist - 0.01f);
            Vector3 target = SampleTrailFromEnd(behind);
            if (lockXToPlayer) target.x = player.position.x;

            m.position = Vector3.Lerp(m.position, target, followLerp * Time.deltaTime);

            Vector3 vel = target - m.position; vel.y = 0f;
            if (vel.sqrMagnitude > 0.0001f)
                m.forward = Vector3.Lerp(m.forward, new Vector3(0, 0, Mathf.Sign(vel.z)), 12f * Time.deltaTime);
        }
    }

    Vector3 SampleTrailFromEnd(float distanceFromEnd)
    {
        float remain = distanceFromEnd;
        for (int i = trail.Count - 1; i > 0; i--)
        {
            Vector3 a = trail[i];
            Vector3 b = trail[i - 1];
            float seg = Vector3.Distance(a, b);
            if (remain <= seg) return Vector3.Lerp(a, b, remain / seg);
            remain -= seg;
        }
        return trail[0];
    }

    void RebuildTrail()
    {
        trail.Clear();
        gapMetersAccum = 0f;
        lastTrailPos = player.position;

        computedTrailLength = followGapDistance * (mothBabies.Count + 1);

        Vector3 dir = Vector3.back;
        Vector3 p = player.position;
        if (lockXToPlayer) p.x = player.position.x;

        const float segmentLen = 0.05f;
        int segments = Mathf.CeilToInt(computedTrailLength / segmentLen) + 10;
        for (int i = 0; i < segments; i++)
            trail.Add(p + dir * (i * segmentLen));
    }

    // ---------- SEEK LIGHT ----------
    void SeekNearestLightpoles()
    {
        GameObject[] lights = GameObject.FindGameObjectsWithTag(lightpoleTag);

        foreach (var m in mothBabies)
        {
            if (m == null) continue;

            Transform nearest = null;
            float bestSqr = float.MaxValue;
            Vector3 pos = m.position;

            for (int i = 0; i < lights.Length; i++)
            {
                Vector3 lp = lights[i].transform.position;
                if (lockXToPlayer) lp.x = player.position.x;
                float d2 = (lp - pos).sqrMagnitude;
                if (d2 < bestSqr) { bestSqr = d2; nearest = lights[i].transform; }
            }

            if (nearest == null) continue;

            Vector3 target = nearest.position;
            if (lockXToPlayer) target.x = player.position.x;

            Vector3 delta = target - pos;
            float dist = delta.magnitude;

            if (dist > arriveRadius)
            {
                Vector3 step = delta.normalized * seekSpeed * Time.deltaTime;
                if (step.magnitude > dist) step = delta;
                m.position += step;

                Vector3 fwd = step; fwd.y = 0;
                if (fwd.sqrMagnitude > 0.0001f)
                    m.forward = Vector3.Lerp(m.forward, new Vector3(0, 0, Mathf.Sign(fwd.z)), 10f * Time.deltaTime);
            }
        }
    }
}
