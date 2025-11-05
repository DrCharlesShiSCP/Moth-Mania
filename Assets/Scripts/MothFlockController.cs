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
    [Tooltip("Initial headlight state at start.")]
    public bool headlightOn = false;

    [Header("Headlight UI (optional)")]
    public TMP_Text headlightStatusText;
    public string onText = "ON";
    public string offText = "OFF";

    [Header("Recall Rule (sticky while OFF)")]
    [Tooltip("When headlight turns OFF, if the player is within this range of the nearest Lightpole, recall latches and babies keep following even after leaving that range. Turning ON clears the latch.")]
    public float playerLightReturnRange = 6f;

    [Header("Follow Line Settings")]
    [Tooltip("Distance (meters) between babies in the line.")]
    public float followGapDistance = 0.6f;
    [Tooltip("How tightly babies snap to their trail points.")]
    public float followLerp = 10f;

    [Header("Seek Light Settings")]
    [Tooltip("Approach speed toward poles.")]
    public float seekSpeed = 8f;
    [Tooltip("Radius at which a moth is considered to have 'reached' the pole and starts orbiting.")]
    public float orbitEnterRadius = 0.35f;

    [Header("Orbit Settings (RotateAround)")]
    [Tooltip("Min orbit radius around the lightpole (meters).")]
    public float orbitMinRadius = 0.6f;
    [Tooltip("Max orbit radius around the lightpole (meters).")]
    public float orbitMaxRadius = 1.2f;
    [Tooltip("Angular speed in degrees per second.")]
    public float orbitAngularSpeed = 90f;
    [Tooltip("If distance from pole exceeds this, break orbit (hysteresis). Should be > orbitEnterRadius.")]
    public float orbitBreakRadius = 0.6f;

    [Header("World Constraints")]
    [Tooltip("Lock moth X to player's X for 2.5D side-scroller.")]
    public bool lockXToPlayer = true;

    [Header("Lookup")]
    public string lightpoleTag = "Lightpole";

    [Header("Debug (read-only)")]
    public FlockState state;

    [Header("Headlight Object")]
    public GameObject nightVisionVolume;
    public GameObject headlampitselfRed;
    public GameObject globalLights;

    // ---------------- internals ----------------
    readonly List<Vector3> trail = new List<Vector3>();
    float gapMetersAccum;
    Vector3 lastTrailPos;
    float computedTrailLength;

    bool recallLatched = false; // stays true while headlight is OFF once latched
    bool prevHeadlightOn;

    class OrbitInfo
    {
        public Transform center;  // lightpole
        public float radius;      // fixed orbit radius
        public int spinDir;       // +1 or -1 for CW/CCW variety
    }
    readonly Dictionary<Transform, OrbitInfo> orbiting = new Dictionary<Transform, OrbitInfo>();
    System.Random rng;

    void Start()
    {
        globalLights.SetActive(headlightOn);
        //headlampitselfRed.SetActive(headlightOn);
        nightVisionVolume.SetActive(headlightOn);
        if (player == null)
        {
            Debug.LogError("[MothFlockController] Player is not assigned.");
            enabled = false; return;
        }

        rng = new System.Random(gameObject.GetInstanceID());
        prevHeadlightOn = headlightOn;
        UpdateHeadlightUI();

        // Initialize latch if we start OFF and near a pole
        Transform nearest = FindNearestLightTo(player.position, out float d);
        recallLatched = !headlightOn && nearest != null && d <= playerLightReturnRange;

        RebuildTrail();
    }

    void Update()
    {
        // Toggle headlight with G (if enabled)
        if (controlHeadlightWithKey && Input.GetKeyDown(toggleKey))
        {
            headlightOn = !headlightOn;
            UpdateHeadlightUI();
            headlampitselfRed.SetActive(!headlightOn);
        }

        // Manage sticky recall latch on transitions
        if (headlightOn && !prevHeadlightOn)
        {
            // Just turned ON ¡ú clear latch and orbits (they'll re-acquire poles)
            recallLatched = false;
        }
        else if (!headlightOn && prevHeadlightOn)
        {
            // Just turned OFF ¡ú latch if within proximity NOW
            Transform nearest = FindNearestLightTo(player.position, out float dNow);
            if (nearest != null && dNow <= playerLightReturnRange)
                recallLatched = true;
        }
        prevHeadlightOn = headlightOn;

        // Decide behavior
        if (headlightOn)
        {
            if (state != FlockState.SeekLight) state = FlockState.SeekLight;
            SeekNearestLightpolesAndOrbit();
        }
        else if (recallLatched)
        {
            // Recall follow mode while OFF (sticky)
            if (state != FlockState.FollowLine)
            {
                state = FlockState.FollowLine;
                orbiting.Clear();   // stop orbiting on recall
                RebuildTrail();     // snap formation
            }
            UpdateTrailFromPlayer();
            PlaceMothsAlongTrail();
        }
        else
        {
            if (state != FlockState.SeekLight) state = FlockState.SeekLight;
            SeekNearestLightpolesAndOrbit();
        }
    }

    // ---------------- UI ----------------
    void UpdateHeadlightUI()
    {
        if (headlightStatusText != null)
            headlightStatusText.text = headlightOn ? onText : offText;

        if (nightVisionVolume != null)
            nightVisionVolume.SetActive(headlightOn); // night vision ON when lamp OFF
        if (globalLights != null)
            globalLights.SetActive(headlightOn); // global lights ON when lamp ON

    }

    // ---------------- Light search ----------------
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

    // ---------------- FOLLOW LINE ----------------
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
        while (trail.Count > neededPoints)
            trail.RemoveAt(0);
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

            // face along Z (optional)
            Vector3 vel = target - m.position; vel.y = 0f;
            if (vel.sqrMagnitude > 0.0001f)
                m.forward = Vector3.Lerp(m.forward, new Vector3(0, 0, Mathf.Sign(vel.z == 0 ? 1f : vel.z)), 12f * Time.deltaTime);
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

    // ---------------- SEEK LIGHT + ORBIT (RotateAround) ----------------
    void SeekNearestLightpolesAndOrbit()
    {
        GameObject[] lights = GameObject.FindGameObjectsWithTag(lightpoleTag);
        float dt = Time.deltaTime;

        foreach (var m in mothBabies)
        {
            if (m == null) continue;

            // find nearest lightpole
            Transform nearest = null;
            float bestSqr = float.MaxValue;
            Vector3 pos = m.position;

            for (int i = 0; i < lights.Length; i++)
            {
                Vector3 lp = lights[i].transform.position;
                if (lockXToPlayer) lp.x = player.position.x; // keep lane
                float d2 = (lp - pos).sqrMagnitude;
                if (d2 < bestSqr) { bestSqr = d2; nearest = lights[i].transform; }
            }
            if (nearest == null) continue;

            // compute orbit center (snap X to player lane if desired)
            Vector3 center = nearest.position;
            if (lockXToPlayer) center.x = player.position.x;

            float dist = Vector3.Distance(pos, center);

            // If already orbiting this pole, rotate; otherwise approach until close enough
            OrbitInfo info;
            bool isOrbiting = orbiting.TryGetValue(m, out info) && info.center == nearest;

            if (isOrbiting)
            {
                // break orbit if we drift too far (prevents threshold jitter)
                if (dist > orbitBreakRadius)
                {
                    orbiting.Remove(m);
                    // fall through to approach behavior
                }
                else
                {
                    // rotate around X-axis ¡ú circle in Y¨CZ plane
                    float signedSpeed = orbitAngularSpeed * info.spinDir;
                    m.RotateAround(center, Vector3.right, signedSpeed * dt);

                    // enforce X lane (safety)
                    if (lockXToPlayer)
                    {
                        Vector3 p = m.position;
                        p.x = center.x;
                        m.position = p;
                    }

                    // face along tangent (¡ÀZ)
                    Vector3 tangent = Vector3.Cross(Vector3.right, (m.position - center)).normalized; // tangent in YZ
                    if (tangent.sqrMagnitude > 1e-4f)
                        m.forward = Vector3.Lerp(m.forward, new Vector3(0f, 0f, Mathf.Sign(tangent.z == 0 ? 1f : tangent.z)), 10f * dt);

                    continue; // done
                }
            }

            // Not orbiting ¡ú approach pole
            if (dist > orbitEnterRadius)
            {
                Vector3 toCenter = (center - pos);
                Vector3 step = toCenter.normalized * seekSpeed * dt;
                if (step.magnitude > toCenter.magnitude) step = toCenter; // no overshoot
                m.position += step;

                Vector3 fwd = step; fwd.y = 0f;
                if (fwd.sqrMagnitude > 1e-4f)
                    m.forward = Vector3.Lerp(m.forward, new Vector3(0, 0, Mathf.Sign(fwd.z == 0 ? 1f : fwd.z)), 10f * dt);
            }
            else
            {
                // close enough ¡ú initialize orbit once
                float r = Mathf.Lerp(orbitMinRadius, orbitMaxRadius, (float)rng.NextDouble());
                int dir = rng.NextDouble() < 0.5 ? 1 : -1; // CW/CCW variety

                // place moth exactly on its orbit ring at a random phase
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float oy = Mathf.Sin(angle) * r;
                float oz = Mathf.Cos(angle) * r;
                m.position = new Vector3(center.x, center.y + oy, center.z + oz);

                // remember orbit params
                orbiting[m] = new OrbitInfo { center = nearest, radius = r, spinDir = dir };
            }
        }
    }
}
