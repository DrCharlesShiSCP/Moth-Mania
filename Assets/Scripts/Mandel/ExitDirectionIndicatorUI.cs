using UnityEngine;

public class ExitDirectionIndicatorUI : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public Transform exitTarget;
    [Tooltip("Recommended: place at doorway threshold for accurate distance/hide.")]
    public Transform exitAnchor;
    public RectTransform arrowRect;

    [Header("Camera")]
    public Camera targetCamera;

    [Header("Distance")]
    [Tooltip("Ignore height and measure only on ground plane (XZ). Recommended.")]
    public bool groundDistanceOnly = true;

    [Tooltip("Convert Unity units to meters (usually 1.0).")]
    public float unitsToMeters = 1.0f;

    [Tooltip("Optional: offset from player pivot for distance calc (usually zero).")]
    public Vector3 playerDistanceOffset = Vector3.zero;

    [Header("Hide When Close")]
    public bool hideWhenCloseToExit = true;

    [Tooltip("Hide when within this many meters of the exit.")]
    public float hideDistanceMeters = 4f;

    [Header("Initial Show")]
    [Tooltip("When enabled, force-show the arrow for this many seconds so it doesn't blink off instantly.")]
    public float initialShowSeconds = 2f;

    [Header("Reminder Pulse")]
    [Tooltip("When hidden (inside hide radius), re-show every interval for a short duration.")]
    public bool enableReminderPulse = true;

    [Tooltip("How long to wait before re-showing the arrow.")]
    public float reminderIntervalSeconds = 10f;

    [Tooltip("How long the arrow stays visible when it reappears.")]
    public float reminderVisibleSeconds = 2f;

    [Header("UI Placement")]
    public float radiusFromCenter = 330f;
    public float edgePadding = 40f;

    [Header("Rotation")]
    [Tooltip("Rotate the arrow sprite so its TIP points at the exit. Try 0/90/-90/180.")]
    public float spriteUpOffsetDegrees = 0f;

    [Header("Pulse Animation")]
    public bool enablePulse = true;
    public float pulseSpeed = 2.5f;
    public float pulseScale = 0.15f;

    [Header("Debug")]
    public bool logDebug = false;

    public float CurrentDistanceMeters { get; private set; }

    bool _enabled = false;

    float _lastReminderTime = -999f;
    float _forceShowUntil = -999f;

    Vector3 _baseScale;
    bool _baseScaleCached = false;

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        if (arrowRect != null)
        {
            _baseScale = arrowRect.localScale;
            _baseScaleCached = true;
            arrowRect.gameObject.SetActive(false);
        }
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;

        // Force show immediately for initialShowSeconds to prevent the "blink"
        _forceShowUntil = enabled ? Time.time + Mathf.Max(0f, initialShowSeconds) : -999f;

        // Start reminder timer AFTER initial show ends
        _lastReminderTime = Time.time;

        if (arrowRect != null)
            arrowRect.gameObject.SetActive(_enabled);

        if (_enabled && arrowRect != null && _baseScaleCached)
            arrowRect.localScale = _baseScale;
    }

    float ComputeDistanceMeters(Vector3 a, Vector3 b)
    {
        if (groundDistanceOnly)
        {
            a.y = 0f;
            b.y = 0f;
        }

        return Vector3.Distance(a, b) * Mathf.Max(0.0001f, unitsToMeters);
    }

    Vector3 GetExitPoint()
    {
        if (exitAnchor != null) return exitAnchor.position;
        if (exitTarget != null) return exitTarget.position;
        return Vector3.zero;
    }

    void Update()
    {
        if (!_enabled) return;

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null || player == null || arrowRect == null) return;

        Vector3 exitPoint = GetExitPoint();
        if (exitPoint == Vector3.zero) return;

        Vector3 playerPos = player.position + playerDistanceOffset;

        // Distance updates every frame (used for hide + reminders)
        CurrentDistanceMeters = ComputeDistanceMeters(playerPos, exitPoint);

        bool withinHideRadius = hideWhenCloseToExit && CurrentDistanceMeters <= hideDistanceMeters;

        // Force-show window (initial or reminder)
        bool inForceShowWindow = Time.time < _forceShowUntil;

        // If we're hidden (inside radius) and not already force-showing, schedule reminder
        if (enableReminderPulse && withinHideRadius && !inForceShowWindow)
        {
            if (Time.time - _lastReminderTime >= reminderIntervalSeconds)
            {
                _forceShowUntil = Time.time + Mathf.Max(0f, reminderVisibleSeconds);
                _lastReminderTime = Time.time;
                inForceShowWindow = true;
            }
        }

        // Hide when close UNLESS force-show window is active
        bool shouldHide = withinHideRadius && !inForceShowWindow;

        arrowRect.gameObject.SetActive(!shouldHide);

        if (logDebug)
        {
            Debug.Log(
                $"[ExitIndicator] dist={CurrentDistanceMeters:0.00}m " +
                $"withinHide={withinHideRadius} forceShow={inForceShowWindow} hide={shouldHide}"
            );
        }

        if (shouldHide) return;

        // Arrow pointing
        Vector3 screenPos = targetCamera.WorldToScreenPoint(exitPoint);

        if (screenPos.z < 0f)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir = (Vector2)screenPos - screenCenter;

        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        Vector2 desired = screenCenter + dir * radiusFromCenter;
        desired.x = Mathf.Clamp(desired.x, edgePadding, Screen.width - edgePadding);
        desired.y = Mathf.Clamp(desired.y, edgePadding, Screen.height - edgePadding);

        arrowRect.position = desired;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrowRect.rotation = Quaternion.Euler(0f, 0f, angle + spriteUpOffsetDegrees);

        // Pulse animation
        if (enablePulse && _baseScaleCached)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
            arrowRect.localScale = _baseScale * pulse;
        }
        else if (_baseScaleCached)
        {
            arrowRect.localScale = _baseScale;
        }
    }
}
