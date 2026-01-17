using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NightCrawlerPatrol : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform pointA;
    public Transform pointB;

    [Header("Arrival")]
    public float arriveDistance = 0.25f;
    public float waitTimeAtPoint = 0f;

    [Header("NavMesh Placement")]
    [Tooltip("How far to search for a NavMesh under/near the agent before giving up.")]
    public float navMeshSampleRadius = 2f;

    private NavMeshAgent _agent;
    private Transform _target;
    private float _waitTimer;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    void OnEnable()
    {
        _target = (pointA != null) ? pointA : pointB;
        _waitTimer = 0f;

        // Don't SetDestination yet. We'll do it once the agent is placed.
        TryPlaceOnNavMeshAndStart();
    }

    void Update()
    {
        if (pointA == null || pointB == null) return;
        if (!_agent.enabled) return;

        // If we're not on NavMesh yet, keep trying (handles runtime baking)
        if (!_agent.isOnNavMesh)
        {
            TryPlaceOnNavMeshAndStart();
            return;
        }

        if (_agent.pathPending) return;

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f) GoToOtherPoint();
            return;
        }

        // Arrived?
        if (_agent.remainingDistance <= Mathf.Max(arriveDistance, _agent.stoppingDistance))
        {
            // Ensure we've actually stopped
            if (_agent.hasPath && _agent.velocity.sqrMagnitude > 0.01f) return;

            if (waitTimeAtPoint > 0f)
            {
                _waitTimer = waitTimeAtPoint;
                _agent.ResetPath();
            }
            else
            {
                GoToOtherPoint();
            }
        }
    }

    private void TryPlaceOnNavMeshAndStart()
    {
        if (_agent == null || !_agent.enabled) return;
        if (_agent.isOnNavMesh)
        {
            // Already placed; ensure we have a destination
            if (_target != null && (!_agent.hasPath || _agent.remainingDistance <= 0.01f))
                _agent.SetDestination(_target.position);
            return;
        }

        // Try snap to nearest NavMesh point
        if (NavMesh.SamplePosition(transform.position, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);

            // Only now is it safe to set destination
            if (_target != null && _agent.isOnNavMesh)
                _agent.SetDestination(_target.position);
        }
        // else: NavMesh not built yet or too far away. We'll try again next frame.
    }

    private void GoToOtherPoint()
    {
        _target = (_target == pointA) ? pointB : pointA;
        if (_target == null) return;

        if (_agent.enabled && _agent.isOnNavMesh)
            _agent.SetDestination(_target.position);
    }
}
