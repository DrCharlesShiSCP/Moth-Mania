using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class HydraulicPressTrap : MonoBehaviour
{
    [Header("References")]
    public Transform pressHead;
    public Transform startPoint;
    public Transform endPoint;

    [Header("Cycle Timing")]
    public float idleTime = 1.5f;
    public float telegraphTime = 0.35f;
    public float extendTime = 0.12f;
    public float holdTime = 0.15f;
    public float retractTime = 0.45f;

    [Header("Telegraph / Bounce")]
    public float bounceDistance = 0.12f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip warnClip;
    public AudioClip slamClip;
    public AudioClip riseClip;
    [Range(0f, 1f)] public float warnVolume = 1f;
    [Range(0f, 1f)] public float slamVolume = 1f;
    [Range(0f, 1f)] public float riseVolume = 1f;

    [Header("Proximity Audio Trigger")]
    [Tooltip("Center point for proximity detection (can be offset).")]
    public Transform proximityOrigin;

    [Tooltip("Player must be within this radius for sounds to play.")]
    public float proximityRadius = 10f;

    [Tooltip("Optional looping ambience while player is nearby.")]
    public AudioClip proximityLoop;
    [Range(0f, 1f)] public float proximityLoopVolume = 0.5f;

    [Header("Screenshake (on slam)")]
    public string playerTag = "Player";
    public float shakeRadius = 8f;
    public float slamShakeAmplitude = 0.25f;
    public float slamShakeDuration = 0.18f;

    [Header("Debug")]
    public bool drawGizmos = true;

    Vector3 _pullbackPos;
    Transform _player;
    bool _playerInProximity;

    Coroutine _loop;

    void Awake()
    {
        if (!pressHead) pressHead = transform;
        if (!proximityOrigin) proximityOrigin = transform;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p) _player = p.transform;

        if (startPoint)
            pressHead.position = startPoint.position;

        RecomputePullback();
        _loop = StartCoroutine(PressLoop());
    }

    void Update()
    {
        UpdateProximity();
    }

    void UpdateProximity()
    {
        if (!_player || !proximityOrigin) return;

        float dist = Vector3.Distance(_player.position, proximityOrigin.position);
        bool inside = dist <= proximityRadius;

        if (inside == _playerInProximity)
            return;

        _playerInProximity = inside;

        // Start / stop looping ambience
        if (audioSource && proximityLoop)
        {
            if (_playerInProximity)
            {
                audioSource.clip = proximityLoop;
                audioSource.volume = proximityLoopVolume;
                audioSource.loop = true;
                audioSource.Play();
            }
            else
            {
                if (audioSource.clip == proximityLoop)
                    audioSource.Stop();
            }
        }
    }

    IEnumerator PressLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(idleTime);

            // WARNING
            PlayIfNearby(warnClip, warnVolume);
            yield return MoveOverTime(pressHead.position, _pullbackPos, telegraphTime);

            // SLAM
            yield return MoveOverTime(pressHead.position, endPoint.position, extendTime);
            PlayIfNearby(slamClip, slamVolume);
            ShakeIfPlayerNearby();

            yield return new WaitForSeconds(holdTime);

            // RESET
            PlayIfNearby(riseClip, riseVolume);
            yield return MoveOverTime(pressHead.position, startPoint.position, retractTime);
        }
    }

    void PlayIfNearby(AudioClip clip, float volume)
    {
        if (!_playerInProximity) return;
        if (!audioSource || !clip) return;

        audioSource.PlayOneShot(clip, volume);
    }

    void ShakeIfPlayerNearby()
    {
        if (!_player || SimpleCameraShake.Instance == null) return;

        float dist = Vector3.Distance(_player.position, pressHead.position);
        if (dist > shakeRadius) return;

        SimpleCameraShake.Instance.Shake(slamShakeAmplitude, slamShakeDuration);
    }

    void RecomputePullback()
    {
        if (!startPoint || !endPoint) return;
        Vector3 dir = (endPoint.position - startPoint.position).normalized;
        _pullbackPos = startPoint.position - dir * bounceDistance;
    }

    IEnumerator MoveOverTime(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            pressHead.position = to;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            pressHead.position = Vector3.Lerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }

        pressHead.position = to;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !proximityOrigin) return;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(proximityOrigin.position, proximityRadius);
    }
}
