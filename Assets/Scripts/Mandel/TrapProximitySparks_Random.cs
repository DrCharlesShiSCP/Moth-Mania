using System.Collections;
using UnityEngine;

public class TrapProximitySparks_Random : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private ParticleSystem sparks;

    [Header("Burst Randomness")]
    [SerializeField] private Vector2 intervalRange = new Vector2(0.25f, 0.9f);
    [SerializeField] private Vector2Int burstCountRange = new Vector2Int(5, 12);

    [Header("Sound (synced to bursts)")]
    [SerializeField] private AudioClip sparkClip;
    [Range(0f, 1f)][SerializeField] private float volume = 0.9f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.1f);

    [Tooltip("Chance a burst is silent (0 = always play, 0.2 = 20% silent bursts)")]
    [Range(0f, 1f)][SerializeField] private float silentBurstChance = 0.1f;

    private int insideCount;
    private Coroutine loopCo;

    private void Awake()
    {
        if (sparks != null)
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        insideCount++;
        if (insideCount != 1) return;

        loopCo = StartCoroutine(BurstLoop());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        insideCount = Mathf.Max(0, insideCount - 1);
        if (insideCount != 0) return;

        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = null;

        if (sparks != null)
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private IEnumerator BurstLoop()
    {
        if (sparks != null) sparks.Play(true);

        while (true)
        {
            // 1) Emit sparks
            int count = Random.Range(burstCountRange.x, burstCountRange.y + 1);
            if (sparks != null) sparks.Emit(count);

            // 2) Play a sound at the SAME moment (synced)
            if (sparkClip != null && Random.value > silentBurstChance)
            {
                // PlayClipAtPoint can't change pitch, so we spawn a tiny one-shot AudioSource.
                float pitch = Random.Range(pitchRange.x, pitchRange.y);

                var go = new GameObject("SparkOneShot");
                go.transform.position = transform.position;

                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1f;       // 3D
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.volume = volume;
                src.pitch = pitch;
                src.clip = sparkClip;
                src.Play();

                Destroy(go, sparkClip.length / Mathf.Max(0.01f, pitch));
            }

            // 3) Wait a random interval, then repeat
            float wait = Random.Range(intervalRange.x, intervalRange.y);
            yield return new WaitForSeconds(wait);
        }
    }
}
