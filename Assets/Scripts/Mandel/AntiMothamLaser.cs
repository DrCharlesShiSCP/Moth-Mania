using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class AntiMothamLaser : MonoBehaviour
{
    public enum BarrelAxis
    {
        ForwardZ,
        UpY,
        RightX,
        BackwardNegZ,
        DownNegY,
        LeftNegX
    }

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private LayerMask playerLayerMask = ~0; // set to PlayerEntity layer if you want
    [SerializeField] private bool useTrigger = true;
    [SerializeField] private bool useOverlapFallback = true;

    [Header("Rotation + Beam")]
    [SerializeField] private Transform laserMuzzle;     // rotates
    [SerializeField] private Transform muzzlePoint;     // beam origin (tip). if null, uses laserMuzzle
    [SerializeField] private float turnSpeedDeg = 360f;

    [Header("Firing Axis (match your model)")]
    [Tooltip("Set to UpY if your barrel points along local Y (green arrow).")]
    [SerializeField] private BarrelAxis barrelAxis = BarrelAxis.UpY;

    [Header("Aim")]
    [SerializeField] private float aimHeight = 1.2f;

    [Header("Laser")]
    [SerializeField] private LineRenderer line;
    [SerializeField] private float laserRange = 40f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Materials (lock + fire)")]
    [SerializeField] private Material lockMaterial;
    [SerializeField] private Material fireMaterial;
    [SerializeField] private float lockWidth = 0.05f;
    [SerializeField] private float fireWidth = 0.08f;

    [Header("Fire Timing")]
    [SerializeField] private float fireInterval = 4f;
    [SerializeField] private float fireFlashTime = 0.15f;

    [Header("Lock-On SFX (Loop)")]
    [Tooltip("AudioSource used for the continuous lock-on loop. Put this on the turret root for 3D spatial audio.")]
    [SerializeField] private AudioSource lockLoopSource;
    [SerializeField] private AudioClip lockLoopClip;
    [Range(0f, 1f)][SerializeField] private float lockLoopVolume = 0.8f;
    [SerializeField] private bool fadeLockLoop = true;
    [SerializeField] private float lockFadeTime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;
    [SerializeField] private bool drawDebug = true;

    private Transform _target;
    private Coroutine _fireLoop;
    private SphereCollider _trigger;

    private Coroutine _lockFadeRoutine;
    private bool _lockPlaying;

    private void Awake()
    {
        _trigger = GetComponent<SphereCollider>();
        _trigger.isTrigger = true;

        if (muzzlePoint == null) muzzlePoint = laserMuzzle;

        if (line != null)
        {
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.enabled = false;
            ApplyLockStyle();
        }

        SetupLockAudio();

        if (logDebug)
        {
            Debug.Log($"[Turret] Awake. Trigger radius={_trigger.radius}, isTrigger={_trigger.isTrigger}", this);
            if (laserMuzzle == null) Debug.LogError("[Turret] laserMuzzle is NOT assigned!", this);
            if (line == null) Debug.LogError("[Turret] LineRenderer is NOT assigned!", this);
        }
    }

    private void SetupLockAudio()
    {
        if (lockLoopSource == null) return;

        lockLoopSource.playOnAwake = false;
        lockLoopSource.loop = true;

        // Only set clip if provided (so you can reuse AudioSource for other things if you want)
        if (lockLoopClip != null)
            lockLoopSource.clip = lockLoopClip;

        // Start silent; we fade up on lock.
        lockLoopSource.volume = 0f;
    }

    private void Update()
    {
        // Fallback detection (works even if triggers never fire)
        if (useOverlapFallback)
            OverlapDetect();

        if (_target == null || laserMuzzle == null || line == null) return;

        AimMuzzleAtTarget();
        UpdateLockBeam();

        if (drawDebug)
        {
            Debug.DrawRay(transform.position, Vector3.up * 2f, Color.yellow);
        }
    }

    // --- Trigger Detection ---
    private void OnTriggerEnter(Collider other)
    {
        if (!useTrigger) return;

        Transform t = ResolvePlayerTransformFromCollider(other);
        if (t == null) return;

        SetTarget(t);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useTrigger) return;
        if (_target == null) return;

        Transform root = other.transform.root;
        if (root == _target || other.transform.IsChildOf(_target))
        {
            ClearTarget();
        }
    }

    // --- Overlap fallback ---
    private void OverlapDetect()
    {
        if (_target != null) return;

        Vector3 center = transform.position;
        float radius = _trigger != null
            ? _trigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z)
            : 10f;

        Collider[] hits = Physics.OverlapSphere(center, radius, playerLayerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = ResolvePlayerTransformFromCollider(hits[i]);
            if (t != null)
            {
                SetTarget(t);
                return;
            }
        }
    }

    private Transform ResolvePlayerTransformFromCollider(Collider col)
    {
        if (col == null) return null;

        if (col.CompareTag(playerTag)) return col.transform;
        if (col.transform.root.CompareTag(playerTag)) return col.transform.root;

        Transform parentTagged = col.GetComponentInParent<Transform>();
        if (parentTagged != null && parentTagged.CompareTag(playerTag)) return parentTagged;

        return null;
    }

    private void SetTarget(Transform t)
    {
        if (_target == t) return;

        _target = t;

        if (logDebug)
            Debug.Log($"[Turret] Target acquired: {_target.name}", this);

        if (line != null)
        {
            line.enabled = true;
            ApplyLockStyle();
        }

        StartLockLoopSFX();

        if (_fireLoop == null)
            _fireLoop = StartCoroutine(FireLoop());
    }

    private void ClearTarget()
    {
        if (logDebug)
            Debug.Log("[Turret] Target lost.", this);

        _target = null;

        StopLockLoopSFX();

        if (_fireLoop != null) StopCoroutine(_fireLoop);
        _fireLoop = null;

        if (line != null) line.enabled = false;
    }

    // --- Aiming + Beam ---
    private void AimMuzzleAtTarget()
    {
        Vector3 aimPoint = _target.position + Vector3.up * aimHeight;
        Vector3 toTarget = aimPoint - laserMuzzle.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Vector3 barrelWorld = GetAxisWorld(laserMuzzle, barrelAxis);
        Quaternion fromTo = Quaternion.FromToRotation(barrelWorld, toTarget.normalized);
        Quaternion desired = fromTo * laserMuzzle.rotation;

        laserMuzzle.rotation = Quaternion.RotateTowards(
            laserMuzzle.rotation,
            desired,
            turnSpeedDeg * Time.deltaTime
        );

        if (drawDebug)
            Debug.DrawLine(laserMuzzle.position, aimPoint, Color.green);
    }

    private void UpdateLockBeam()
    {
        Transform originT = muzzlePoint ? muzzlePoint : laserMuzzle;
        Vector3 origin = originT.position;

        Vector3 dir = GetAxisWorld(originT, barrelAxis);

        Vector3 end = origin + dir * laserRange;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, laserRange, hitMask, QueryTriggerInteraction.Ignore))
            end = hit.point;

        line.SetPosition(0, origin);
        line.SetPosition(1, end);

        if (drawDebug)
        {
            Debug.DrawLine(origin, end, Color.red);
            Debug.DrawRay(origin, dir * 2f, Color.cyan);
        }
    }

    // --- Fire ---
    private IEnumerator FireLoop()
    {
        while (_target != null)
        {
            yield return new WaitForSeconds(fireInterval);
            if (_target == null) yield break;

            yield return StartCoroutine(FireFlash());
        }
    }

    private IEnumerator FireFlash()
    {
        ApplyFireStyle();
        TryHitPlayer();
        yield return new WaitForSeconds(fireFlashTime);
        ApplyLockStyle();
    }

    private void TryHitPlayer()
    {
        Transform originT = muzzlePoint ? muzzlePoint : laserMuzzle;
        Vector3 origin = originT.position;
        Vector3 dir = GetAxisWorld(originT, barrelAxis);

        if (Physics.Raycast(origin, dir, out RaycastHit hit, laserRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag(playerTag) || hit.collider.transform.root.CompareTag(playerTag))
            {
                ToggleMothVisionViaController();
            }
        }
    }

    private static Vector3 GetAxisWorld(Transform t, BarrelAxis axis)
    {
        switch (axis)
        {
            case BarrelAxis.ForwardZ: return t.forward;
            case BarrelAxis.UpY: return t.up;
            case BarrelAxis.RightX: return t.right;
            case BarrelAxis.BackwardNegZ: return -t.forward;
            case BarrelAxis.DownNegY: return -t.up;
            case BarrelAxis.LeftNegX: return -t.right;
            default: return t.forward;
        }
    }

    // --- Styles ---
    private void ApplyLockStyle()
    {
        if (line == null) return;
        if (lockMaterial != null) line.sharedMaterial = lockMaterial;
        line.startWidth = lockWidth;
        line.endWidth = lockWidth;
    }

    private void ApplyFireStyle()
    {
        if (line == null) return;
        if (fireMaterial != null) line.sharedMaterial = fireMaterial;
        line.startWidth = fireWidth;
        line.endWidth = fireWidth;
    }

    // --- Lock-on audio helpers ---
    private void StartLockLoopSFX()
    {
        if (lockLoopSource == null) return;

        if (lockLoopClip != null && lockLoopSource.clip != lockLoopClip)
            lockLoopSource.clip = lockLoopClip;

        if (!lockLoopSource.isPlaying)
            lockLoopSource.Play();

        if (fadeLockLoop)
            FadeLockLoopTo(lockLoopVolume);
        else
            lockLoopSource.volume = lockLoopVolume;

        _lockPlaying = true;
    }

    private void StopLockLoopSFX()
    {
        if (lockLoopSource == null) return;

        if (fadeLockLoop)
        {
            FadeLockLoopTo(0f, stopWhenSilent: true);
        }
        else
        {
            lockLoopSource.Stop();
            lockLoopSource.volume = 0f;
        }

        _lockPlaying = false;
    }

    private void FadeLockLoopTo(float targetVol, bool stopWhenSilent = false)
    {
        if (_lockFadeRoutine != null) StopCoroutine(_lockFadeRoutine);
        _lockFadeRoutine = StartCoroutine(FadeRoutine(targetVol, stopWhenSilent));
    }

    private IEnumerator FadeRoutine(float targetVol, bool stopWhenSilent)
    {
        if (lockLoopSource == null) yield break;

        float start = lockLoopSource.volume;
        float t = 0f;
        float dur = Mathf.Max(0.01f, lockFadeTime);

        while (t < dur)
        {
            t += Time.deltaTime;
            lockLoopSource.volume = Mathf.Lerp(start, targetVol, t / dur);
            yield return null;
        }

        lockLoopSource.volume = targetVol;

        if (stopWhenSilent && targetVol <= 0.0001f)
        {
            lockLoopSource.Stop();
            lockLoopSource.volume = 0f;
        }
    }

    private void OnDisable()
    {
        // Safety: stop sound + coroutines if turret is disabled
        if (_fireLoop != null) StopCoroutine(_fireLoop);
        _fireLoop = null;

        if (_lockFadeRoutine != null) StopCoroutine(_lockFadeRoutine);
        _lockFadeRoutine = null;

        if (lockLoopSource != null)
        {
            lockLoopSource.Stop();
            lockLoopSource.volume = 0f;
        }

        _target = null;
    }

    private void ToggleMothVisionViaController()
    {
        if (MothFlockController.Instance != null)
            MothFlockController.Instance.ToggleHeadlightExternal();
        else
            MothFlockController.ToggleHeadlightExternalStatic();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var sc = GetComponent<SphereCollider>();
        float r = sc ? sc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) : 10f;
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, r);
    }
#endif
}
