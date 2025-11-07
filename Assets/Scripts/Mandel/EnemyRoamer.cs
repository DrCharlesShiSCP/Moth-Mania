using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyRoamer : MonoBehaviour
{
    [Header("Roam")]
    [Tooltip("How far from the start (or from current) the enemy will choose random points.")]
    public float roamRadius = 12f;

    [Tooltip("Minimum time to wait after reaching a point before picking the next.")]
    public Vector2 idleTimeRange = new Vector2(0.5f, 2.0f);

    [Tooltip("How close is 'good enough' to count as reached.")]
    public float arriveDistance = 0.5f;

    [Tooltip("How many attempts to find a safe point before giving up this frame.")]
    public int maxPickTries = 12;

    [Header("Edge Safety")]
    [Tooltip("Reject destinations that are within this distance of a NavMesh edge/ledge.")]
    public float edgeBuffer = 0.8f;

    [Tooltip("While moving, if our forward probe finds no ground within this distance, stop & repick.")]
    public float forwardLedgeProbeDistance = 0.8f;

    [Tooltip("Downward ray distance for ledge probing.")]
    public float ledgeProbeDown = 2.0f;

    [Header("Path Quality")]
    [Tooltip("Require a complete path; partial paths are rejected.")]
    public bool requireCompletePath = true;

    [Tooltip("If true, the next point is chosen around our current position; if false, around spawn.")]
    public bool roamFromCurrent = true;

    [Header("NavMesh Placement (robust start-up)")]
    [Tooltip("Max distance to search when snapping the agent onto the NavMesh at start or after rebakes.")]
    public float initialSnapDistance = 3f;

    [Tooltip("If the NavMesh isn’t ready yet, how long between retries.")]
    public float navmeshRetryInterval = 0.25f;

    private NavMeshAgent _agent;
    private Vector3 _home;
    private float _idleUntil = 0f;
    private bool _hasDestination;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _home = transform.position;
        _agent.autoBraking = true;       // smoother stop near points
        _agent.autoRepath = true;
        _agent.updateRotation = true;
        _agent.updateUpAxis = true;
    }

    void OnEnable()
    {
        // Ensure we're placed on the NavMesh before any CalculatePath calls
        StartCoroutine(EnsureOnNavMeshRoutine());
    }

    IEnumerator EnsureOnNavMeshRoutine()
    {
        // Keep trying until a mesh exists and we can snap to it
        while (!TrySnapToNavMesh(initialSnapDistance))
            yield return new WaitForSeconds(navmeshRetryInterval);
    }

    bool TrySnapToNavMesh(float maxDistance)
    {
        if (NavMesh.SamplePosition(transform.position, out var hit, maxDistance, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position); // warp avoids invalid transient paths
            return true;
        }
        return false;
    }

    void Update()
    {
        // If we’re not on a NavMesh yet, skip (coroutine will place us when possible)
        if (!_agent.isOnNavMesh)
        {
            // Opportunistic quick resnap in case mesh appeared this frame
            TrySnapToNavMesh(initialSnapDistance);
            return;
        }

        // If we're idling, just wait.
        if (Time.time < _idleUntil) return;

        // If we don't have a destination, pick one.
        if (!_hasDestination)
        {
            _hasDestination = TrySetRandomSafeDestination();
            if (!_hasDestination)
            {
                // Couldn’t find anything safe this frame; wait briefly then try again.
                _idleUntil = Time.time + 0.25f;
            }
            return;
        }

        // Actively moving: ledge safety probe in front of us.
        if (forwardLedgeProbeDistance > 0f)
        {
            Vector3 forward = _agent.velocity.sqrMagnitude > 0.01f ? _agent.velocity.normalized : transform.forward;
            Vector3 probeStart = transform.position + forward * forwardLedgeProbeDistance + Vector3.up * 0.1f;

            // If there is no ground beneath the probe point, abort this path and repick.
            if (!GroundBelow(probeStart, ledgeProbeDown))
            {
                _agent.ResetPath();
                _hasDestination = false;
                _idleUntil = Time.time + 0.25f;
                return;
            }
        }

        // Done travelling?
        if (!_agent.pathPending && _agent.remainingDistance <= Mathf.Max(arriveDistance, _agent.stoppingDistance))
        {
            _hasDestination = false;
            _idleUntil = Time.time + Random.Range(idleTimeRange.x, idleTimeRange.y);
        }
        else
        {
            // If the path becomes partial/invalid while moving, repick.
            if (requireCompletePath && _agent.pathStatus != NavMeshPathStatus.PathComplete && !_agent.pathPending)
            {
                _agent.ResetPath();
                _hasDestination = false;
            }
        }
    }

    bool TrySetRandomSafeDestination()
    {
        if (!_agent.isOnNavMesh) return false; // absolute guard

        Vector3 center = roamFromCurrent ? transform.position : _home;

        for (int i = 0; i < maxPickTries; i++)
        {
            Vector3 candidate = RandomPointInCircleOnXZ(center, roamRadius);

            // Snap candidate to nearest NavMesh
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                continue;

            Vector3 dest = hit.position;

            // 1) Wall check: ensure a clear NavMesh ray between us and the dest.
            if (NavMesh.Raycast(transform.position, dest, out NavMeshHit wall, NavMesh.AllAreas))
                continue; // blocked by some wall/obstacle

            // 2) Edge buffer: avoid picking points too close to mesh edges (ledges/gaps).
            if (NavMesh.FindClosestEdge(dest, out NavMeshHit edge, NavMesh.AllAreas))
            {
                if (edge.distance < edgeBuffer) continue; // too close to a drop or boundary
            }

            // 3) Validate full path (optional).
            if (requireCompletePath)
            {
                var path = new NavMeshPath();
                if (!_agent.CalculatePath(dest, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;
            }

            _agent.SetDestination(dest);
            return true;
        }

        return false;
    }

    static Vector3 RandomPointInCircleOnXZ(Vector3 center, float radius)
    {
        // Uniform disc sample
        float t = 2f * Mathf.PI * Random.value;
        float u = Random.value + Random.value;
        float r = (u > 1f) ? 2f - u : u;
        r *= radius;
        return new Vector3(center.x + r * Mathf.Cos(t), center.y, center.z + r * Mathf.Sin(t));
    }

    bool GroundBelow(Vector3 origin, float downDistance)
    {
        // Physics ground check: if nothing is below, it's a ledge
        return Physics.Raycast(origin, Vector3.down, out _, downDistance, ~0, QueryTriggerInteraction.Ignore);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 c = Application.isPlaying ? (roamFromCurrent ? transform.position : _home) : transform.position;
        Gizmos.DrawWireSphere(c, roamRadius);

        Gizmos.color = Color.yellow;
        Vector3 forward = Application.isPlaying && _agent
            ? (_agent.velocity.sqrMagnitude > 0.01f ? _agent.velocity.normalized : transform.forward)
            : transform.forward;
        Vector3 probeStart = transform.position + forward * forwardLedgeProbeDistance + Vector3.up * 0.1f;
        Gizmos.DrawLine(probeStart, probeStart + Vector3.down * ledgeProbeDown);
    }
#endif
}
