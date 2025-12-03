using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MothFlockController : MonoBehaviour
{
    public static MothFlockController Instance { get; private set; }

    public enum FlockState { Mixed, SeekLight, FollowLine }

    // ----------------- Inspector fields -----------------

    [Header("References")]
    public Transform player;
    public List<Transform> mothBabies = new List<Transform>();

    [Header("Headlight Control")]
    public bool controlHeadlightWithKey = true;
    public KeyCode toggleKey = KeyCode.G;
    [Tooltip("Initial headlight state at start.")]
    public bool headlightOn = false;

    [Tooltip("If false, player input cannot toggle the headlight. Used by zones, transmitters, etc.")]
    public bool canToggleHeadlight = true;

    [Header("Headlight UI (optional)")]
    public TMP_Text headlightStatusText;
    public string onText = "ON";
    public string offText = "OFF";

    [Header("Pickup / Recall")]
    [Tooltip("Player must be within this range of a pole to recall moths orbiting it.")]
    public float playerLightReturnRange = 5f;

    [Tooltip("Extra radius around the player to directly collect nearby moths when headlight is OFF.")]
    public float directPickupRadius = 3.5f;

    [Header("Follow Line Settings")]
    [Tooltip("Distance between followers in the line.")]
    public float followGapDistance = 2f;
    [Tooltip("How snappy followers move into their line positions.")]
    public float followLerp = 10f;

    [Header("Seek Light Settings")]
    [Tooltip("Meters/sec when moths slide toward nearest pole.")]
    public float seekSpeed = 8f;
    [Tooltip("Radius at which a moth starts orbiting the pole.")]
    public float orbitEnterRadius = 1.5f;

    [Header("Orbit Settings (RotateAround)")]
    [Tooltip("Min orbit radius around the lightpole (meters).")]
    public float orbitMinRadius = 0.2f;
    [Tooltip("Max orbit radius around the lightpole (meters).")]
    public float orbitMaxRadius = 0.7f;
    [Tooltip("Angular speed in degrees per second.")]
    public float orbitAngularSpeed = 30f;
    [Tooltip("If distance from pole exceeds this, break orbit.")]
    public float orbitBreakRadius = 1.3f;

    [Header("World Constraints")]
    [Tooltip("Lock moth X to player's X for a 2.5D lane.")]
    public bool lockXToPlayer = true;

    [Header("Roaming Collisions")]
    [Tooltip("If true, moths inside a RoamBox will not move through walls.")]
    public bool blockWallsInsideRoamBox = true;
    [Tooltip("Layers that count as walls for roaming moths.")]
    public LayerMask wallLayers;
    [Tooltip("Tag used by box triggers that mark 'no pass through walls / no follow' zones.")]
    public string roamBoxTag = "MothRoamBox";

    [Header("Lookup")]
    public string lightpoleTag = "Lightpole";

    [Header("Debug (read-only)")]
    public FlockState state;

    [Header("Headlight Object")]
    public GameObject nightVisionVolume;
    public GameObject headlampitselfRed;
    public GameObject globalLights;

    // ----------------- Internals -----------------

    class OrbitInfo
    {
        public Transform center;  // lightpole
        public float radius;      // orbit radius
        public int spinDir;       // +1 or -1 for rotation direction
    }

    readonly Dictionary<Transform, OrbitInfo> orbiting = new Dictionary<Transform, OrbitInfo>();
    readonly HashSet<Transform> recalledMoths = new HashSet<Transform>(); // followers while lamp OFF
    readonly Dictionary<Transform, bool> mothInsideRoamBox = new Dictionary<Transform, bool>();

    System.Random rng;
    bool prevHeadlightOn;

    // Track player movement along world Z (+Z vs -Z) for "behind"
    float lastPlayerZ;
    int zDirectionSign = 1; // +1 = moving +Z, -1 = moving -Z

    // ----------------- Unity lifecycle -----------------

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Auto-fill mothBabies if empty
        if (mothBabies == null || mothBabies.Count == 0)
        {
            mothBabies = new List<Transform>();
            GameObject[] foundBabies = GameObject.FindGameObjectsWithTag("babies");
            foreach (GameObject baby in foundBabies)
                mothBabies.Add(baby.transform);
        }

        if (player == null)
        {
            Debug.LogError("[MothFlockController] Player is not assigned.");
            enabled = false;
            return;
        }

        rng = new System.Random(gameObject.GetInstanceID());
        prevHeadlightOn = headlightOn;

        if (nightVisionVolume) nightVisionVolume.SetActive(headlightOn);
        if (globalLights) globalLights.SetActive(headlightOn);
        if (headlampitselfRed) headlampitselfRed.SetActive(!headlightOn);

        UpdateHeadlightUI();

        lastPlayerZ = player.position.z;
    }

    void Update()
    {
        // 1. Handle input
        if (controlHeadlightWithKey && canToggleHeadlight && Input.GetKeyDown(toggleKey))
        {
            headlightOn = !headlightOn;
        }

        // 2. Detect if the headlight state changed (INPUT or SCRIPT forced it)
        if (prevHeadlightOn != headlightOn)
        {
            OnHeadlightChanged();
            prevHeadlightOn = headlightOn;
        }
        TrackZDirection();
        UpdateStateMachine();
        UpdateHeadlightUI();
    }

    // ----------------- Public API (used by other scripts) -----------------

    public void SetMothInsideRoamBox(Transform moth, bool inside)
    {
        mothInsideRoamBox[moth] = inside;
    }

    // For HUD via reflection
    List<Transform> GetFollowersInOrder()
    {
        var list = new List<Transform>();
        foreach (var m in mothBabies)
        {
            if (m != null && recalledMoths.Contains(m))
                list.Add(m);
        }
        return list;
    }

    // ----------------- Headlight & UI -----------------

    public void SetHeadlight(bool on)
    {
        headlightOn = on;
    }
    void OnHeadlightChanged()
    {
        if (nightVisionVolume) nightVisionVolume.SetActive(headlightOn);
        if (globalLights) globalLights.SetActive(headlightOn);
        if (headlampitselfRed) headlampitselfRed.SetActive(!headlightOn);

        if (headlightOn)
        {
            // HEADLIGHT ON  → pure lamp mode, no followers
            // Drop any followers and let them reacquire poles
            recalledMoths.Clear();
            orbiting.Clear();
        }


        // When turning OFF we keep recalledMoths; OFF state machine
        // will decide who becomes a follower.
    }

    void UpdateHeadlightUI()
    {
        if (headlightStatusText != null)
            headlightStatusText.text = headlightOn ? onText : offText;
    }

    // ----------------- RoamBox helpers -----------------

    bool AnyRoamBoxActive()
    {
        if (!blockWallsInsideRoamBox) return false;

        var boxes = GameObject.FindGameObjectsWithTag(roamBoxTag);
        for (int i = 0; i < boxes.Length; i++)
        {
            if (boxes[i].activeInHierarchy)
                return true;
        }
        return false;
    }

    bool IsMothInsideRoamBox(Transform moth)
    {
        bool value;
        if (!mothInsideRoamBox.TryGetValue(moth, out value))
            return false;

        if (!value) return false;
        if (!AnyRoamBoxActive()) return false;

        return true;
    }

    // ----------------- Player Z-direction tracking -----------------

    void TrackZDirection()
    {
        float currentZ = player.position.z;
        float dz = currentZ - lastPlayerZ;

        const float threshold = 0.001f;
        if (dz > threshold) zDirectionSign = 1;
        else if (dz < -threshold) zDirectionSign = -1;

        lastPlayerZ = currentZ;
    }

    // ----------------- State machine -----------------

    void UpdateStateMachine()
    {
        if (headlightOn)
        {
            // HEADLIGHT ON  → moths do NOT follow, they just seek/orbit lamps
            state = FlockState.SeekLight;
            SeekNearestLightpolesAndOrbit(skipMoths: null);
            return;
        }

        // HEADLIGHT OFF → recall moths from nearby poles and make them follow

        // 1) Find poles close enough to the player
        var nearPoles = GetPolesWithinRange(player.position, playerLightReturnRange);

        if (nearPoles.Count > 0)
        {
            foreach (var kvp in orbiting)
            {
                var moth = kvp.Key;
                var info = kvp.Value;
                if (moth == null || info.center == null) continue;

                // If moth is locked in a RoamBox, it cannot be collected yet
                if (IsMothInsideRoamBox(moth))
                    continue;

                // If this moth orbits one of the nearby poles, recall it
                foreach (var np in nearPoles)
                {
                    if (np == info.center)
                    {
                        recalledMoths.Add(moth); // mark as follower
                        break;
                    }
                }
            }
        }

        // 1b) Direct pickup: any moth close to the player, even if not orbiting a pole yet
        foreach (var moth in mothBabies)
        {
            if (moth == null) continue;
            if (IsMothInsideRoamBox(moth)) continue;  // still respect RoamBox lock

            float distToPlayer = Vector3.Distance(player.position, moth.position);
            if (distToPlayer <= directPickupRadius)
            {
                recalledMoths.Add(moth);
            }
        }

        // 2) Followers: recalled moths → line behind player
        var followers = GetFollowersInOrder();

        if (followers.Count > 0)
        {
            PlaceFollowersBehindPlayer(followers);
            state = FlockState.Mixed;   // some follow, others still lamp-seeking
        }
        else
        {
            state = FlockState.SeekLight; // no followers yet
        }

        // 3) Non-followers keep seeking/orbiting
        SeekNearestLightpolesAndOrbit(skipMoths: recalledMoths);
    }

    // ----------------- Followers behind player (Z-based) -----------------

    void PlaceFollowersBehindPlayer(List<Transform> followers)
    {
        if (followers.Count == 0) return;

        // Behind relative to world Z movement
        Vector3 trailDir = new Vector3(0f, 0f, -zDirectionSign);
        Vector3 basePos = player.position;

        if (lockXToPlayer)
            basePos.x = player.position.x;

        for (int i = 0; i < followers.Count; i++)
        {
            Transform moth = followers[i];
            if (moth == null) continue;

            float offset = followGapDistance * (i + 1);
            Vector3 targetPos = basePos + trailDir * offset;

            if (lockXToPlayer)
                targetPos.x = player.position.x;

            // Smooth movement into position
            moth.position = Vector3.Lerp(moth.position, targetPos, followLerp * Time.deltaTime);

            // Face along movement direction (+Z or -Z)
            Vector3 lookDir = new Vector3(0f, 0f, zDirectionSign);
            if (lookDir.sqrMagnitude > 1e-4f)
                moth.forward = Vector3.Lerp(moth.forward, lookDir, 10f * Time.deltaTime);
        }
    }

    // ----------------- Lightpole helpers -----------------

    List<Transform> GetPolesWithinRange(Vector3 pos, float range)
    {
        float r2 = range * range;
        var results = new List<Transform>();
        var lights = GameObject.FindGameObjectsWithTag(lightpoleTag);
        for (int i = 0; i < lights.Length; i++)
        {
            Vector3 lp = lights[i].transform.position;
            if (lockXToPlayer) lp.x = player.position.x;
            if ((lp - pos).sqrMagnitude <= r2)
                results.Add(lights[i].transform);
        }
        return results;
    }

    // ----------------- Roaming / Orbiting with collisions -----------------

    Vector3 ConstrainMoveAgainstWalls(Transform moth, Vector3 from, Vector3 to)
    {
        if (!blockWallsInsideRoamBox) return to;
        if (!IsMothInsideRoamBox(moth)) return to;

        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 1e-4f) return to;

        dir /= dist;

        if (Physics.Raycast(from, dir, out RaycastHit hit, dist, wallLayers, QueryTriggerInteraction.Ignore))
        {
            // stop just before the wall
            return hit.point - dir * 0.02f;
        }

        return to;
    }

    void SeekNearestLightpolesAndOrbit(HashSet<Transform> skipMoths)
    {
        GameObject[] lights = GameObject.FindGameObjectsWithTag(lightpoleTag);
        float dt = Time.deltaTime;

        foreach (var m in mothBabies)
        {
            if (m == null) continue;
            if (skipMoths != null && skipMoths.Contains(m)) continue; // followers handled separately

            // find nearest lightpole
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

            // compute orbit center (snap X to player lane if desired)
            Vector3 center = nearest.position;
            if (lockXToPlayer) center.x = player.position.x;

            float dist = Vector3.Distance(pos, center);

            // Orbit logic
            OrbitInfo info;
            bool isOrbiting = orbiting.TryGetValue(m, out info) && info.center == nearest;

            if (isOrbiting)
            {
                // break orbit if we drift too far
                if (dist > orbitBreakRadius)
                {
                    orbiting.Remove(m);
                }
                else
                {
                    float signedSpeed = orbitAngularSpeed * info.spinDir;
                    m.RotateAround(center, Vector3.right, signedSpeed * dt);

                    if (lockXToPlayer)
                    {
                        Vector3 p = m.position;
                        p.x = center.x;
                        m.position = p;
                    }
                    continue;
                }
            }

            // Approach pole
            if (dist > orbitEnterRadius)
            {
                Vector3 toCenter = (center - pos);
                Vector3 step = toCenter.normalized * seekSpeed * dt;
                if (step.magnitude > toCenter.magnitude) step = toCenter;

                Vector3 desired = pos + step;
                Vector3 constrained = ConstrainMoveAgainstWalls(m, pos, desired);
                m.position = constrained;

                Vector3 fwd = constrained - pos;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 1e-4f)
                    m.forward = Vector3.Lerp(m.forward, fwd.normalized, 10f * dt);
            }
            else
            {
                // Enter orbit
                float r = Mathf.Lerp(orbitMinRadius, orbitMaxRadius, (float)rng.NextDouble());
                int dir = rng.NextDouble() < 0.5 ? 1 : -1;

                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float oy = Mathf.Sin(angle) * r;
                float oz = Mathf.Cos(angle) * r;
                m.position = new Vector3(center.x, center.y + oy, center.z + oz);

                orbiting[m] = new OrbitInfo { center = nearest, radius = r, spinDir = dir };
            }
        }
    }
}
