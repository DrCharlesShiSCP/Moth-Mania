using System.Collections;
using UnityEngine;

public class ProximitySparks : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private ParticleSystem sparks;

    [Header("Burst Timing")]
    [SerializeField] private Vector2 burstInterval = new Vector2(0.15f, 0.6f);
    [SerializeField] private Vector2Int burstCount = new Vector2Int(2, 6);

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private int insideCount;
    private Coroutine burstRoutine;

    private void Awake()
    {
        if (sparks == null)
        {
            Debug.LogError("[TrapProximitySparks_Bursty] Sparks reference is not assigned.", this);
            return;
        }

        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (logDebug) Debug.Log("[TrapProximitySparks_Bursty] Player entered radius", this);

        insideCount++;
        if (insideCount != 1) return;

        burstRoutine = StartCoroutine(BurstLoop());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (logDebug) Debug.Log("[TrapProximitySparks_Bursty] Player exited radius", this);

        insideCount = Mathf.Max(0, insideCount - 1);
        if (insideCount != 0) return;

        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
            burstRoutine = null;
        }

        if (sparks != null)
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private IEnumerator BurstLoop()
    {
        // Make sure the system is "alive"
        sparks.Play(true);

        while (true)
        {
            int count = Random.Range(burstCount.x, burstCount.y + 1);
            if (logDebug) Debug.Log($"[TrapProximitySparks_Bursty] Emit {count}", this);

            sparks.Emit(count);

            float wait = Random.Range(burstInterval.x, burstInterval.y);
            yield return new WaitForSeconds(wait);
        }
    }
}
