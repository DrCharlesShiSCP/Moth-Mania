using System.Collections;
using UnityEngine;

public class BuzzBladeTrap : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bladeVisual;      // the moving blade mesh/sprite
    [SerializeField] private Collider damageTrigger;     // trigger collider for damage (enabled only when exposed)

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Damage")]
    [SerializeField] private int damagePerTick = 1;
    [SerializeField] private float damageCooldown = 0.5f; // how often damage can apply while standing on it
    [SerializeField] private bool respectIFrames = true;

    [Header("Blade Motion (local Y)")]
    [SerializeField] private float hiddenY = -0.5f;      // below the floor
    [SerializeField] private float exposedY = 0.1f;      // above the floor
    [SerializeField] private float riseTime = 0.25f;
    [SerializeField] private float exposedTime = 1.0f;
    [SerializeField] private float retractTime = 0.25f;

    [Header("Cycle")]
    [SerializeField] private float cycleInterval = 4.0f; // time between rises

    [Header("Spin")]
    [SerializeField] private float spinSpeedDeg = 900f;

    private bool bladeUp = false;
    private float nextDamageTime = 0f;

    private void Awake()
    {
        // Safety: make sure damage is off at start
        SetBladeLocalY(hiddenY);
        SetDamageEnabled(false);
        bladeUp = false;
    }

    private void Start()
    {
        StartCoroutine(CycleLoop());
    }

    private void Update()
    {
        // Spin only while exposed
        if (bladeUp && bladeVisual != null)
        {
            bladeVisual.Rotate(0f, 0f, spinSpeedDeg * Time.deltaTime, Space.Self);
        }
    }

    private IEnumerator CycleLoop()
    {
        while (true)
        {
            // Wait until next rise
            yield return new WaitForSeconds(cycleInterval);

            // Rise (still safe until fully exposed)
            yield return MoveBladeLocalY(hiddenY, exposedY, riseTime);

            // Expose + enable damage
            bladeUp = true;
            SetDamageEnabled(true);

            // Danger window
            yield return new WaitForSeconds(exposedTime);

            // Retract: disable damage immediately
            bladeUp = false;
            SetDamageEnabled(false);

            yield return MoveBladeLocalY(exposedY, hiddenY, retractTime);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // IMPORTANT:
        // This OnTriggerStay will only fire if THIS script is on the same GameObject
        // as the damageTrigger collider OR the collider is on this GameObject.
        // Recommended: put the damageTrigger collider on the same object as this script.
        //
        // If your damageTrigger is on a child, Unity won't call this method here.
        // In that case, move the script onto the damageTrigger object, or use a child forwarder.

        if (!bladeUp) return; // extra safety
        if (!other.CompareTag(playerTag)) return;
        if (Time.time < nextDamageTime) return;

        var lives = other.GetComponentInParent<PlayerLives>();
        if (lives != null)
        {
            lives.ApplyDamage(damagePerTick, respectIFrames);
            nextDamageTime = Time.time + damageCooldown;
        }
    }

    private void SetDamageEnabled(bool enabled)
    {
        if (damageTrigger != null)
            damageTrigger.enabled = enabled;
    }

    private void SetBladeLocalY(float y)
    {
        if (bladeVisual == null) return;
        var p = bladeVisual.localPosition;
        bladeVisual.localPosition = new Vector3(p.x, y, p.z);
    }

    private IEnumerator MoveBladeLocalY(float fromY, float toY, float duration)
    {
        if (bladeVisual == null) yield break;

        float t = 0f;
        // Keep x/z stable
        var basePos = bladeVisual.localPosition;

        while (t < duration)
        {
            t += Time.deltaTime;
            float y = Mathf.Lerp(fromY, toY, Mathf.Clamp01(t / duration));
            bladeVisual.localPosition = new Vector3(basePos.x, y, basePos.z);
            yield return null;
        }

        bladeVisual.localPosition = new Vector3(basePos.x, toY, basePos.z);
    }
}
