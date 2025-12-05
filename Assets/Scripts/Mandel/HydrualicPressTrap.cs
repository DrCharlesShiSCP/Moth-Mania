using UnityEngine;
using System.Collections;

public class HydraulicPressTrap : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The moving head of the press (has collider + kinematic rigidbody).")]
    public Transform pressHead;

    [Tooltip("Rest position (usually close to the wall).")]
    public Transform startPoint;

    [Tooltip("Fully extended position (where the player gets crushed).")]
    public Transform endPoint;

    [Header("Timing")]
    [Tooltip("Time between each press cycle while idle at start position.")]
    public float idleTime = 2f;

    [Tooltip("How long the small 'bounce' / wind-up lasts before the slam.")]
    public float telegraphTime = 0.3f;

    [Tooltip("How long the actual slam/extension takes.")]
    public float extendTime = 0.15f;

    [Tooltip("How long it stays fully extended before retracting.")]
    public float holdTime = 0.3f;

    [Tooltip("How long it takes to retract back to start.")]
    public float retractTime = 0.4f;

    [Header("Telegraph / Bounce")]
    [Tooltip("How far it pulls back during the wind-up (relative to startPoint).")]
    public float bounceDistance = 0.15f;

    [Header("Damage")]
    [Tooltip("Tag used to identify the player.")]
    public string playerTag = "Player";


    [Tooltip("Optional: should we only kill if it's in the fast slam phase?")]
    public bool onlyKillWhileExtending = true;
    public int livesToRemove = 3;

    private enum PressState { Idle, Telegraph, Extending, Holding, Retracting }
    private PressState _state = PressState.Idle;

    private Vector3 _telegraphPos;
    private Coroutine _loopRoutine;

    private void Start()
    {
        // Clamp head to start position initially
        if (pressHead && startPoint)
            pressHead.position = startPoint.position;

        // Compute telegraph position (a small pull back from the startPoint)
        if (startPoint && endPoint)
        {
            Vector3 slamDirection = (endPoint.position - startPoint.position).normalized;
            // Move backwards along the opposite of slam direction
            _telegraphPos = startPoint.position - slamDirection * bounceDistance;
        }

        _loopRoutine = StartCoroutine(PressLoop());
    }

    private IEnumerator PressLoop()
    {
        while (true)
        {
            // 1. Idle at rest
            _state = PressState.Idle;
            yield return new WaitForSeconds(idleTime);

            // 2. Telegraph / bounce back
            if (telegraphTime > 0f)
            {
                _state = PressState.Telegraph;
                yield return MoveOverTime(pressHead.position, _telegraphPos, telegraphTime);
            }

            // 3. Fast slam to end point
            _state = PressState.Extending;
            yield return MoveOverTime(pressHead.position, endPoint.position, extendTime);

            // 4. Hold extended
            _state = PressState.Holding;
            yield return new WaitForSeconds(holdTime);

            // 5. Retract back to start
            _state = PressState.Retracting;
            yield return MoveOverTime(pressHead.position, startPoint.position, retractTime);
        }
    }

    /// <summary>
    /// Moves pressHead from 'from' to 'to' in 'duration' seconds using a simple lerp.
    /// </summary>
    private IEnumerator MoveOverTime(Vector3 from, Vector3 to, float duration)
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
            float clamped = Mathf.Clamp01(t);
            pressHead.position = Vector3.Lerp(from, to, clamped);
            yield return null;
        }

        pressHead.position = to;
    }

    /// <summary>
    /// Put this on the SAME object that has the trigger collider.
    /// If the collider is on PressHead, add this script to the parent
    /// and also a small helper on the head that forwards collisions,
    /// OR just move this whole script onto PressHead and wire references.
    /// </summary>
   




}
