// MothFlockController.cs
// Added:
// 1) Headlight ON/OFF toggle sounds (one-shot, only on transitions)
// 2) Headlight glitch burst (UI fullscreen overlay) triggered only on transitions
//
// Requires you to have a HeadlightGlitchBurst component in your scene (Option A),
// and assign it in the inspector to headlightGlitchBurst.
//
// Original file reference: :contentReference[oaicite:0]{index=0}

using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MothFlockController : MonoBehaviour
{
    public static MothFlockController Instance { get; private set; }

    public enum FlockState { Mixed, SeekLight, FollowLine }

    [Header("References")]
    public Transform player;
    public List<Transform> mothBabies = new List<Transform>();

    [Header("Headlight Control")]
    public bool controlHeadlightWithKey = true;
    public KeyCode toggleKey = KeyCode.G;
    public bool headlightOn = false;
    public bool canToggleHeadlight = true;

    [Header("Headlight UI (optional)")]
    public TMP_Text headlightStatusText;
    public string onText = "ON";
    public string offText = "OFF";

    [Header("Headlight Audio (NEW)")]
    [Tooltip("AudioSource used to play headlight toggle sounds. If empty, will try GetComponent<AudioSource>() on Start.")]
    public AudioSource headlightAudio;
    public AudioClip headlightOnClip;
    public AudioClip headlightOffClip;
    [Range(0f, 1f)] public float headlightVolume = 0.85f;
    [Tooltip("Small pitch variation for a less 'robotic' toggle sound.")]
    public Vector2 headlightPitchRange = new Vector2(0.97f, 1.03f);

    [Header("Headlight Glitch (NEW - Option A UI Overlay)")]
    [Tooltip("Assign your HeadlightGlitchBurst (UI fullscreen overlay) here to flash a neutral glitch on state change.")]
    public HeadlightGlitchBurst headlightGlitchBurst;

    [Header("Player Follow Acquire Range")]
    [Tooltip("When headlight is OFF, moths only become followers if within this distance to the player.")]
    public float followAcquireRadius = 6f;

    [Header("Follow Line Settings")]
    public float followGapDistance = 2f;
    public float followLerp = 10f;

    [Header("Lamp Seek Settings")]
    [Tooltip("Top speed toward the lamp while seeking.")]
    public float seekSpeed = 30f;

    [Tooltip("Distance threshold to allow orbiting (works best with seek hysteresis below).")]
    public float orbitEnterRadius = 1.8f;

    [Header("Seek → Orbit Hysteresis (IMPORTANT)")]
    [Tooltip("Moth must be seeking this many seconds (per target) before it is allowed to enter orbit.")]
    public float minSeekTimeBeforeOrbit = 0.6f;

    [Tooltip("If true, allow orbit immediately if already very close (prevents edge cases).")]
    public bool allowImmediateOrbitIfVeryClose = true;

    [Tooltip("Distance considered 'very close' for immediate orbit (only if the toggle above is true).")]
    public float veryCloseOrbitRadius = 0.6f;

    [Header("Lamp Orbit Settings (Smooth Orbit)")]
    public float orbitMinRadius = 0.2f;
    public float orbitMaxRadius = 0.7f;
    [Tooltip("Degrees per second.")]
    public float orbitAngularSpeed = 12f;
    public float orbitBreakRadius = 1.8f;

    [Header("Smooth Lamp Seek/Orbit")]
    public float orbitSmoothTime = 0.20f;
    public float seekSmoothTime = 0.55f;

    [Header("Seek Speed Safety")]
    [Tooltip("If enabled, seeking movement will never exceed seekSpeed per second (hard cap).")]
    public bool hardCapSeekSpeed = true;

    [Header("Forced Follow (NightCrawler)")]
    public float forcedFollowDistance = 2.2f;
    public float forcedSideSpread = 0.6f;
    public float forcedHoverY = 0.4f;
    public float forcedFollowSmoothTime = 0.45f;

    [Header("World Constraints")]
    [Tooltip("Lock moth X to player X for 2.5D lane behavior.")]
    public bool lockXToPlayer = true;

    [Header("Roaming Collisions")]
    public bool blockWallsInsideRoamBox = true;
    public LayerMask wallLayers;
    public string roamBoxTag = "MothRoamBox";

    [Header("Lookup (LAMPS ONLY)")]
    [Tooltip("Tag for actual lamps/poles. NightCrawler should NOT use this tag.")]
    public string lampTag = "Lightpole";

    [Header("Forced Targeting (NightCrawler priority)")]
    [Tooltip("If true, forced targets override follow behavior (NightCrawler steals moths while in radius).")]
    public bool forcedTargetsOverrideRecall = true;

    [Header("Headlight Objects (optional)")]
    public GameObject nightVisionVolume;
    public GameObject headlampitselfRed;
    public GameObject globalLights;

    [Header("Debug (read-only)")]
    public FlockState state;

    // -------------------- Internals --------------------

    class OrbitInfo
    {
        public Transform center;
        public float radius;
        public int spinDir;
        public float angleRad;
    }

    readonly Dictionary<Transform, OrbitInfo> orbiting = new Dictionary<Transform, OrbitInfo>();
    readonly Dictionary<Transform, Vector3> orbitVel = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Vector3> seekVel = new Dictionary<Transform, Vector3>();

    readonly Dictionary<Transform, Transform> currentSeekTarget = new Dictionary<Transform, Transform>();
    readonly Dictionary<Transform, float> seekTimer = new Dictionary<Transform, float>();

    readonly HashSet<Transform> followers = new HashSet<Transform>();

    readonly Dictionary<Transform, bool> mothInsideRoamBox = new Dictionary<Transform, bool>();

    readonly Dictionary<Transform, Transform> forcedTarget = new Dictionary<Transform, Transform>();

    readonly Dictionary<Transform, Vector3> forcedVel = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Vector3> forcedOffset = new Dictionary<Transform, Vector3>();

    System.Random rng;
    bool prevHeadlightOn;

    float lastPlayerZ;
    int zDirectionSign = 1;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        mothBabies = new List<Transform>();
        var found = GameObject.FindGameObjectsWithTag("babies");
        foreach (var go in found)
        {
            if (go != null)
                mothBabies.Add(go.transform);
        }

        if (player == null)
        {
            Debug.LogError("[MothFlockController] Player not assigned.");
            enabled = false;
            return;
        }

        // NEW: auto-pick an AudioSource if you didn't assign one
        if (headlightAudio == null)
            headlightAudio = GetComponent<AudioSource>();

        rng = new System.Random(gameObject.GetInstanceID());
        prevHeadlightOn = headlightOn; // no sound/glitch on start

        ApplyHeadlightVisuals();
        UpdateHeadlightUI();

        lastPlayerZ = player.position.z;
    }

    void Update()
    {
        if (controlHeadlightWithKey && canToggleHeadlight && Input.GetKeyDown(toggleKey))
            headlightOn = !headlightOn;

        // Transition detection (THIS is where we trigger sound + glitch)
        if (prevHeadlightOn != headlightOn)
        {
            ApplyHeadlightVisuals();

            // NEW: audio
            PlayHeadlightToggleSound(headlightOn);

            // NEW: neutral glitch flash (UI overlay)
            if (headlightGlitchBurst != null)
                headlightGlitchBurst.Burst();

            if (headlightOn) followers.Clear();

            for (int i = 0; i < mothBabies.Count; i++)
                ResetMothMotionState(mothBabies[i]);

            prevHeadlightOn = headlightOn;
        }

        TrackZDirection();
        UpdateStateMachine();
        UpdateHeadlightUI();
    }

    // -------------------- Public API --------------------

    public void SetMothInsideRoamBox(Transform moth, bool inside)
    {
        if (moth == null) return;
        mothInsideRoamBox[moth] = inside;
    }

    public void ToggleHeadlightExternal() => headlightOn = !headlightOn;

    public static void ToggleHeadlightExternalStatic()
    {
        if (Instance != null)
            Instance.headlightOn = !Instance.headlightOn;
    }

    public void SetHeadlight(bool on) => headlightOn = on;

    /// <summary>
    /// Call from external scripts when a moth transitions states (prevents SmoothDamp burst).
    /// </summary>
    public void ResetMothFromExternal(Transform moth)
    {
        ResetMothMotionState(moth);
    }

    // NightCrawlerPriorityRadius uses these:
    public void ForceLightTarget(Transform moth, Transform target)
    {
        if (moth == null) return;
        if (target == null) forcedTarget.Remove(moth);
        else forcedTarget[moth] = target;
    }

    public void ClearForcedLightTarget(Transform moth, Transform target = null)
    {
        if (moth == null) return;

        if (target == null)
        {
            forcedTarget.Remove(moth);
            forcedVel.Remove(moth);
            forcedOffset.Remove(moth);
            return;
        }

        if (forcedTarget.TryGetValue(moth, out var current) && current == target)
        {
            forcedTarget.Remove(moth);
            forcedVel.Remove(moth);
            forcedOffset.Remove(moth);
        }
    }

    // -------------------- Headlight visuals/UI/audio --------------------

    void ApplyHeadlightVisuals()
    {
        if (nightVisionVolume) nightVisionVolume.SetActive(headlightOn);
        if (globalLights) globalLights.SetActive(headlightOn);
        if (headlampitselfRed) headlampitselfRed.SetActive(!headlightOn);
    }

    void UpdateHeadlightUI()
    {
        if (headlightStatusText != null)
            headlightStatusText.text = headlightOn ? onText : offText;
    }

    // NEW
    void PlayHeadlightToggleSound(bool turnedOn)
    {
        if (headlightAudio == null) return;

        AudioClip clip = turnedOn ? headlightOnClip : headlightOffClip;
        if (clip == null) return;

        float prevPitch = headlightAudio.pitch;
        float pitch = UnityEngine.Random.Range(headlightPitchRange.x, headlightPitchRange.y);
        headlightAudio.pitch = pitch;

        headlightAudio.PlayOneShot(clip, headlightVolume);

        headlightAudio.pitch = prevPitch;
    }

    // -------------------- Motion-state reset --------------------

    void ResetMothMotionState(Transform moth)
    {
        if (moth == null) return;

        orbiting.Remove(moth);
        orbitVel.Remove(moth);

        seekVel.Remove(moth);
        currentSeekTarget.Remove(moth);
        seekTimer.Remove(moth);

        forcedVel.Remove(moth);
        // keep forcedOffset stable
    }

    // -------------------- RoamBox helpers --------------------

    bool AnyRoamBoxActive()
    {
        if (!blockWallsInsideRoamBox) return false;

        var boxes = GameObject.FindGameObjectsWithTag(roamBoxTag);
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i] != null && boxes[i].activeInHierarchy)
                return true;

        return false;
    }

    bool IsMothInsideRoamBox(Transform moth)
    {
        if (moth == null) return false;
        if (!mothInsideRoamBox.TryGetValue(moth, out bool inside) || !inside) return false;
        if (!AnyRoamBoxActive()) return false;
        return true;
    }

    Vector3 ConstrainMoveAgainstWalls(Transform moth, Vector3 from, Vector3 to)
    {
        if (!blockWallsInsideRoamBox) return to;
        if (!IsMothInsideRoamBox(moth)) return to;

        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return to;

        dir /= dist;

        if (Physics.Raycast(from, dir, out RaycastHit hit, dist, wallLayers, QueryTriggerInteraction.Ignore))
            return hit.point - dir * 0.05f;

        return to;
    }

    // -------------------- Z Tracking --------------------

    void TrackZDirection()
    {
        float zNow = player.position.z;
        float dz = zNow - lastPlayerZ;

        if (Mathf.Abs(dz) > 0.0005f)
            zDirectionSign = (dz >= 0f) ? 1 : -1;

        lastPlayerZ = zNow;
    }

    // -------------------- Forced helpers --------------------

    bool HasForcedTarget(Transform moth, out Transform target)
    {
        if (moth != null &&
            forcedTarget.TryGetValue(moth, out target) &&
            target != null &&
            target.gameObject.activeInHierarchy)
        {
            return true;
        }

        target = null;
        if (moth != null) forcedTarget.Remove(moth);
        return false;
    }

    Vector3 GetForcedOffsetFor(Transform moth)
    {
        if (forcedOffset.TryGetValue(moth, out var off))
            return off;

        int h = moth.GetInstanceID();
        float side = ((h % 100) / 99f) * 2f - 1f;              // -1..1
        float backJitter = (((h / 100) % 100) / 99f) * 0.6f;   // 0..0.6

        off = new Vector3(
            side * forcedSideSpread,
            forcedHoverY,
            -(forcedFollowDistance + backJitter)
        );

        forcedOffset[moth] = off;
        return off;
    }

    void FollowForcedTargetLikePlayer(Transform moth, Transform target, float dt)
    {
        Vector3 offset = GetForcedOffsetFor(moth);
        Vector3 desired = target.TransformPoint(offset);

        if (lockXToPlayer && player != null)
            desired.x = player.position.x;

        if (!forcedVel.TryGetValue(moth, out var vel))
            vel = Vector3.zero;

        Vector3 next = Vector3.SmoothDamp(
            moth.position,
            desired,
            ref vel,
            Mathf.Max(0.01f, forcedFollowSmoothTime)
        );
        forcedVel[moth] = vel;

        next = ConstrainMoveAgainstWalls(moth, moth.position, next);
        moth.position = next;

        Vector3 fwd = desired - moth.position;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 1e-4f)
            moth.forward = Vector3.Lerp(moth.forward, fwd.normalized, 10f * dt);

        currentSeekTarget.Remove(moth);
        seekTimer.Remove(moth);
    }

    // -------------------- State machine --------------------

    void UpdateStateMachine()
    {
        if (headlightOn)
        {
            state = FlockState.SeekLight;
            SeekForcedOrNearestLamp(skipFollowers: null);
            return;
        }

        followers.Clear();
        float r2 = followAcquireRadius * followAcquireRadius;

        for (int i = 0; i < mothBabies.Count; i++)
        {
            var moth = mothBabies[i];
            if (moth == null) continue;
            if (IsMothInsideRoamBox(moth)) continue;

            if (forcedTargetsOverrideRecall && HasForcedTarget(moth, out _))
                continue;

            Vector3 mp = moth.position;
            Vector3 pp = player.position;
            if (lockXToPlayer) mp.x = pp.x;

            if ((mp - pp).sqrMagnitude <= r2)
                followers.Add(moth);
        }

        var followerList = GetFollowersInOrder();
        if (followerList.Count > 0)
        {
            PlaceFollowersBehindPlayer(followerList);
            state = FlockState.Mixed;
        }
        else
        {
            state = FlockState.SeekLight;
        }

        SeekForcedOrNearestLamp(skipFollowers: followers);
    }

    List<Transform> GetFollowersInOrder()
    {
        var list = new List<Transform>();
        for (int i = 0; i < mothBabies.Count; i++)
        {
            var m = mothBabies[i];
            if (m != null && followers.Contains(m))
                list.Add(m);
        }
        return list;
    }

    void PlaceFollowersBehindPlayer(List<Transform> followerList)
    {
        if (followerList == null || followerList.Count == 0) return;

        Vector3 trailDir = new Vector3(0f, 0f, -zDirectionSign);
        Vector3 basePos = player.position;
        if (lockXToPlayer) basePos.x = player.position.x;

        for (int i = 0; i < followerList.Count; i++)
        {
            Transform moth = followerList[i];
            if (moth == null) continue;

            if (forcedTargetsOverrideRecall && HasForcedTarget(moth, out _))
                continue;

            float offset = followGapDistance * (i + 1);
            Vector3 targetPos = basePos + trailDir * offset;
            if (lockXToPlayer) targetPos.x = player.position.x;

            moth.position = Vector3.Lerp(moth.position, targetPos, followLerp * Time.deltaTime);

            Vector3 lookDir = new Vector3(0f, 0f, zDirectionSign);
            if (lookDir.sqrMagnitude > 1e-4f)
                moth.forward = Vector3.Lerp(moth.forward, lookDir, 10f * Time.deltaTime);
        }
    }

    // -------------------- Core: Forced-follow OR lamp seek/orbit --------------------

    void SeekForcedOrNearestLamp(HashSet<Transform> skipFollowers)
    {
        GameObject[] lamps = GameObject.FindGameObjectsWithTag(lampTag);
        float dt = Time.deltaTime;

        for (int mi = 0; mi < mothBabies.Count; mi++)
        {
            Transform moth = mothBabies[mi];
            if (moth == null) continue;

            bool isFollower = (skipFollowers != null && skipFollowers.Contains(moth));
            bool hasForced = HasForcedTarget(moth, out Transform forced);

            if (isFollower && !(forcedTargetsOverrideRecall && hasForced))
                continue;

            // PRIORITY 1: forced follow (NightCrawler)
            if (hasForced)
            {
                if (orbiting.ContainsKey(moth))
                {
                    orbiting.Remove(moth);
                    orbitVel.Remove(moth);
                }

                FollowForcedTargetLikePlayer(moth, forced, dt);
                continue;
            }

            // PRIORITY 2: nearest lamp
            Transform nearestLamp = null;
            float bestSqr = float.MaxValue;
            Vector3 mp = moth.position;

            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null) continue;

                Vector3 lp = lamps[i].transform.position;
                if (lockXToPlayer) lp.x = player.position.x;

                float d2 = (lp - mp).sqrMagnitude;
                if (d2 < bestSqr)
                {
                    bestSqr = d2;
                    nearestLamp = lamps[i].transform;
                }
            }

            if (nearestLamp == null)
                continue;

            if (!currentSeekTarget.TryGetValue(moth, out var t) || t != nearestLamp)
            {
                currentSeekTarget[moth] = nearestLamp;
                seekTimer[moth] = 0f;

                orbiting.Remove(moth);
                orbitVel.Remove(moth);
                seekVel.Remove(moth);
            }

            Vector3 center = nearestLamp.position;
            if (lockXToPlayer) center.x = player.position.x;

            Vector3 mothPos = moth.position;
            float dist = Vector3.Distance(mothPos, center);

            // ORBIT
            if (orbiting.TryGetValue(moth, out OrbitInfo info) && info != null && info.center == nearestLamp)
            {
                if (dist > orbitBreakRadius)
                {
                    orbiting.Remove(moth);
                    orbitVel.Remove(moth);
                    seekTimer[moth] = 0f;
                    seekVel.Remove(moth);
                }
                else
                {
                    info.angleRad += info.spinDir * orbitAngularSpeed * dt * Mathf.Deg2Rad;

                    Vector3 desired = center + new Vector3(
                        0f,
                        Mathf.Sin(info.angleRad) * info.radius,
                        Mathf.Cos(info.angleRad) * info.radius
                    );

                    if (lockXToPlayer) desired.x = center.x;

                    if (!orbitVel.TryGetValue(moth, out Vector3 vel))
                        vel = Vector3.zero;

                    Vector3 next = Vector3.SmoothDamp(
                        moth.position,
                        desired,
                        ref vel,
                        Mathf.Max(0.01f, orbitSmoothTime)
                    );
                    orbitVel[moth] = vel;

                    next = ConstrainMoveAgainstWalls(moth, moth.position, next);
                    moth.position = next;

                    Vector3 fwd = desired - moth.position;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude > 1e-4f)
                        moth.forward = Vector3.Lerp(moth.forward, fwd.normalized, 10f * dt);

                    orbiting[moth] = info;
                    continue;
                }
            }

            // SEEK with hysteresis
            if (!seekTimer.TryGetValue(moth, out float timer))
                timer = 0f;

            bool canEnterOrbit = timer >= minSeekTimeBeforeOrbit;
            if (allowImmediateOrbitIfVeryClose && dist <= veryCloseOrbitRadius)
                canEnterOrbit = true;

            if (!canEnterOrbit || dist > orbitEnterRadius)
            {
                timer += dt;
                seekTimer[moth] = timer;

                Vector3 desired = Vector3.MoveTowards(mothPos, center, seekSpeed * dt);
                desired = ConstrainMoveAgainstWalls(moth, mothPos, desired);

                if (!seekVel.TryGetValue(moth, out Vector3 vSeek))
                    vSeek = Vector3.zero;

                Vector3 next = Vector3.SmoothDamp(
                    moth.position,
                    desired,
                    ref vSeek,
                    Mathf.Max(0.01f, seekSmoothTime)
                );
                seekVel[moth] = vSeek;

                if (hardCapSeekSpeed)
                {
                    Vector3 delta = next - moth.position;
                    float maxStep = seekSpeed * dt;
                    float mag = delta.magnitude;
                    if (mag > maxStep && mag > 1e-6f)
                        next = moth.position + (delta / mag) * maxStep;
                }

                moth.position = next;

                Vector3 fwd = desired - mothPos;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 1e-4f)
                    moth.forward = Vector3.Lerp(moth.forward, fwd.normalized, 10f * dt);

                continue;
            }

            // ENTER ORBIT
            seekTimer[moth] = 0f;

            float r = Mathf.Lerp(orbitMinRadius, orbitMaxRadius, (float)rng.NextDouble());
            int dirSpin = rng.NextDouble() < 0.5 ? 1 : -1;

            Vector3 offset = moth.position - center;
            float angle = (offset.sqrMagnitude < 1e-6f)
                ? (float)rng.NextDouble() * Mathf.PI * 2f
                : Mathf.Atan2(offset.y, offset.z);

            orbiting[moth] = new OrbitInfo
            {
                center = nearestLamp,
                radius = r,
                spinDir = dirSpin,
                angleRad = angle
            };

            if (!orbitVel.ContainsKey(moth)) orbitVel[moth] = Vector3.zero;
        }
    }
}
