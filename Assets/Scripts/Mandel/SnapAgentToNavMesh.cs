using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SnapAgentToNavMesh : MonoBehaviour
{
    [Tooltip("How far to search for a NavMesh point near this object.")]
    public float sampleRadius = 5f;

    [Tooltip("How many frames to retry (runtime navmesh build can happen after Start).")]
    public int retryFrames = 10;

    private NavMeshAgent _agent;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    IEnumerator Start()
    {
        // Clamp so we never get "0 units"
        sampleRadius = Mathf.Max(0.5f, sampleRadius);

        // Wait a few frames for runtime NavMeshSurface.BuildNavMesh() to finish
        for (int i = 0; i < retryFrames; i++)
        {
            if (TrySnap())
                yield break;

            yield return null; // next frame
        }

        Debug.LogWarning($"No NavMesh found within {sampleRadius} units of {name}");
        // Optional: disable agent to prevent further errors
        // _agent.enabled = false;
    }

    private bool TrySnap()
    {
        if (_agent == null || !_agent.enabled) return false;

        // Already good
        if (_agent.isOnNavMesh) return true;

        if (NavMesh.SamplePosition(transform.position, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);
            return _agent.isOnNavMesh;
        }

        return false;
    }
}
