using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class OurCharacterController : MonoBehaviour
{
    [Header("Movement Adjust")]
    public float moveSpeed = 7.5f;
    public float airControlTimes = 0.6f;
    public float acceleration = 60f;
    public float deceleration = 70f;
    public float maxFallSpeed = -25f;

    [Header("Jumping")]
    public float jumpForce = 12f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Wall Interactions")]
    public float wallCheckDistance = 0.55f;
    public float wallCheckRadius = 0.3f;
    public float wallSlideSpeed = -2.5f;
    public float wallJumpZPush = 7.5f;
    public float wallJumpUpForce = 12f;
    public float wallJumpControlLock = 0.15f;

    [Header("Slide (Ground)")]
    public KeyCode slideKey = KeyCode.LeftControl;
    public float slideDuration = 0.5f;
    public float slideInitialBoost = 2.0f;
    public float slideFriction = 8f;
    public float slideMinSpeed = 2.5f;

    [Header("Checks")]
    public LayerMask groundMask;
    public LayerMask wallMask;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;

    [Header("Collider (for slide)")]
    public float standingHeight = 2.0f;
    public float slidingHeight = 1.2f;
    public Vector3 standingCenter = new Vector3(0, 1.0f, 0);
    public Vector3 slidingCenter = new Vector3(0, 0.6f, 0);

    [Header("Input (Old Input Manager)")]
    public string moveAxis = "Horizontal";    // mapped to Z
    public string verticalAxis = "Vertical";  // ladder climb
    public KeyCode jumpKey = KeyCode.Space;

    [Header("Better Jump")]
    public float fallGravityMultiplier = 2.5f;
    public float lowJumpMultiplier = 2.0f;

    [Header("Rigidbody Constraints")]
    public bool freezeAllRotation = true;

    // -------------------------
    // Ladder (tile-safe, Celeste-ish)
    // -------------------------
    [Header("Ladder (Tile-safe)")]
    public KeyCode ladderInteractKey = KeyCode.E;

    [Tooltip("Pull to ladder centerline.")]
    public float ladderMagnet = 26f;

    [Tooltip("Allow some sideways drift while climbing.")]
    public float ladderMaxDrift = 0.25f;

    [Tooltip("Detach if pushing sideways beyond this.")]
    public float ladderDetachInputThreshold = 0.35f;

    [Tooltip("Smoothing for climb Y velocity.")]
    public float ladderClimbAccel = 60f;

    [Tooltip("Ignore tiny vertical input.")]
    public float ladderVerticalDeadzone = 0.15f;

    [Tooltip("Grace time to prevent dropping between ladder tiles.")]
    public float ladderLoseContactGrace = 0.12f;

    [Tooltip("Press E again to exit ladder.")]
    public bool ladderEToExit = true;

    [Tooltip("Press jump to exit ladder.")]
    public bool ladderJumpToExit = true;

    // internals
    Rigidbody rb;
    CapsuleCollider col;

    float coyoteTimer;
    float jumpBufferTimer;
    float wallJumpLockTimer;

    bool isGrounded;
    bool touchingWallFront;
    bool touchingWallBack;
    int wallDir = 0;

    bool pressingIntoWall;
    bool isSliding;
    float slideTimer;

    float targetZ;

    // -------------------------
    // Ladder state (NEW)
    // -------------------------
    private readonly HashSet<LadderClimb3D> laddersInRange = new HashSet<LadderClimb3D>();
    private LadderClimb3D activeLadder;
    private bool isClimbing;
    private float ladderVerticalInput;
    private float moveInputZ;
    private bool jumpPressedThisFrame;
    private bool interactPressedThisFrame;

    private float ladderClimbVelY;
    private float ladderContactTimer; // counts down when we lose ladder contact

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        standingHeight = col.height;
        standingCenter = col.center;
        col.direction = 1;

        if (freezeAllRotation)
            rb.freezeRotation = true;
    }

    void Update()
    {
        moveInputZ = Input.GetAxisRaw(moveAxis);
        ladderVerticalInput = Input.GetAxisRaw(verticalAxis);

        jumpPressedThisFrame = Input.GetKeyDown(jumpKey);
        bool slidePressed = Input.GetKeyDown(slideKey);
        interactPressedThisFrame = Input.GetKeyDown(ladderInteractKey);

        UpdateGroundedCheck();

        // Pick active ladder from set (closest by Z; good for 2.5D)
        activeLadder = ChooseBestLadder();

        // Maintain grace contact timer while climbing (prevents drop between tiles)
        if (isClimbing)
        {
            if (activeLadder != null) ladderContactTimer = ladderLoseContactGrace;
            else ladderContactTimer -= Time.deltaTime;

            // If we fully lost contact (beyond grace), exit ladder
            if (ladderContactTimer <= 0f)
                ExitLadder(jumpOff: false);

            // Exit inputs
            if (isClimbing)
            {
                if (Mathf.Abs(moveInputZ) > ladderDetachInputThreshold)
                    ExitLadder(jumpOff: false);
                else if ((ladderEToExit && interactPressedThisFrame) ||
                         (ladderJumpToExit && jumpPressedThisFrame))
                    ExitLadder(jumpOff: ladderJumpToExit && jumpPressedThisFrame);
            }

            // If still climbing, skip normal movement
            if (isClimbing) return;
        }
        else
        {
            // Not climbing: require ONE E press to latch in
            if (activeLadder != null && interactPressedThisFrame)
                EnterLadder();
        }

        // ---- Normal movement logic (your existing controller) ----
        // (I’m leaving your original wall/slide/jump logic out here for brevity in the ladder-focused rewrite.)
        // If you want, I can merge this ladder system into your *exact* current full controller version line-for-line.

        // Basic horizontal (Z) target (keep your original version if you prefer)
        float desiredZVel = moveInputZ * moveSpeed;
        float runAccel = isGrounded ? acceleration : acceleration * airControlTimes;
        float runDecel = isGrounded ? deceleration : deceleration * 0.7f;

        float zVel = rb.linearVelocity.z;
        float velDiff = desiredZVel - zVel;
        float accel = Mathf.Abs(desiredZVel) > 0.01f ? runAccel : runDecel;
        float movement = Mathf.Clamp(velDiff, -accel * Time.deltaTime, accel * Time.deltaTime);
        targetZ = zVel + movement;

        // Jump buffer / coyote (minimal)
        if (isGrounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= Time.deltaTime;

        if (jumpPressedThisFrame) jumpBufferTimer = jumpBufferTime;
        else jumpBufferTimer -= Time.deltaTime;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f && !isSliding)
        {
            Jump();
            jumpBufferTimer = 0f;
        }

        if (slidePressed && isGrounded && Mathf.Abs(rb.linearVelocity.z) > 0.5f && !isSliding)
            StartSlide();

        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            HandleSlideFriction();

            bool releasedAndCanStand = Input.GetKeyUp(slideKey) && !CanStandUpCheckBlocked();
            if (slideTimer <= 0f || Mathf.Abs(rb.linearVelocity.z) < slideMinSpeed || releasedAndCanStand)
                StopSlide();
        }
    }

    void FixedUpdate()
    {
        if (isClimbing)
        {
            ApplyTileSafeCelesteLadder();
            return;
        }

        Vector3 v = rb.linearVelocity;

        if (!isSliding) v.z = targetZ;

        bool falling = v.y < 0f;
        bool risingButJumpReleased = v.y > 0f && !Input.GetKey(jumpKey);

        float extraMult = 1f;
        if (falling) extraMult = fallGravityMultiplier;
        else if (risingButJumpReleased) extraMult = lowJumpMultiplier;

        if (extraMult > 1f)
            v.y += Physics.gravity.y * (extraMult - 1f) * Time.fixedDeltaTime;

        if (v.y < maxFallSpeed) v.y = maxFallSpeed;

        rb.linearVelocity = v;
    }

    // -------------------------
    // Ladder core
    // -------------------------
    private void ApplyTileSafeCelesteLadder()
    {
        if (activeLadder == null)
        {
            // rely on grace timer in Update()
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, ladderClimbVelY, 0f);
            return;
        }

        // Soft magnet to ladder centerline (Z axis for your 2.5D setup)
        Vector3 ladderPos = activeLadder.transform.position;
        Vector3 pos = rb.position;

        float newZ = Mathf.MoveTowards(pos.z, ladderPos.z, ladderMagnet * Time.fixedDeltaTime);
        newZ = Mathf.Clamp(newZ, ladderPos.z - ladderMaxDrift, ladderPos.z + ladderMaxDrift);

        rb.position = new Vector3(pos.x, pos.y, newZ);

        // Climb velocity
        float vIn = Mathf.Abs(ladderVerticalInput) < ladderVerticalDeadzone ? 0f : ladderVerticalInput;
        float targetY = vIn * activeLadder.climbSpeed;

        ladderClimbVelY = Mathf.MoveTowards(ladderClimbVelY, targetY, ladderClimbAccel * Time.fixedDeltaTime);

        Vector3 v = rb.linearVelocity;
        v.y = ladderClimbVelY;

        // lock horizontal motion while climbing
        v.z = 0f;

        rb.linearVelocity = v;
    }

    private LadderClimb3D ChooseBestLadder()
    {
        if (laddersInRange.Count == 0) return null;

        LadderClimb3D best = null;
        float bestDz = float.MaxValue;
        float playerZ = transform.position.z;

        // Clean up any destroyed ladder references
        // (HashSet can hold nulls if objects are destroyed)
        // We'll just skip nulls.
        foreach (var l in laddersInRange)
        {
            if (l == null) continue;

            float dz = Mathf.Abs(playerZ - l.transform.position.z);
            if (dz < bestDz)
            {
                bestDz = dz;
                best = l;
            }
        }
        return best;
    }

    private void EnterLadder()
    {
        isClimbing = true;
        rb.useGravity = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        ladderClimbVelY = 0f;
        ladderContactTimer = ladderLoseContactGrace;

        // prevent buffered jump from popping immediately
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
    }

    private void ExitLadder(bool jumpOff)
    {
        isClimbing = false;
        rb.useGravity = true;

        if (jumpOff)
        {
            Vector3 v = rb.linearVelocity;
            v.y = jumpForce;
            rb.linearVelocity = v;

            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
        }
        else
        {
            Vector3 v = rb.linearVelocity;
            if (v.y > 0f) v.y = 0f;
            rb.linearVelocity = v;
        }
    }

    // -------------------------
    // Tile ladder registration (called by LadderClimb3D)
    // -------------------------
    public void RegisterLadder(LadderClimb3D ladder)
    {
        if (ladder == null) return;
        laddersInRange.Add(ladder);

        // If you are already climbing, keep contact alive
        if (isClimbing) ladderContactTimer = ladderLoseContactGrace;
    }

    public void UnregisterLadder(LadderClimb3D ladder)
    {
        if (ladder == null) return;
        laddersInRange.Remove(ladder);

        // Do NOT immediately exit here — Update() handles graceful exit.
        // This is the whole fix for “tile ladders require tapping E”.
    }

    // -------------------------
    // Helpers (from your controller)
    // -------------------------
    private void UpdateGroundedCheck()
    {
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
        }
        else
        {
            isGrounded = Physics.CheckSphere(
                col.bounds.center + Vector3.down * (col.bounds.extents.y + 0.05f),
                groundCheckRadius,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
        }
    }

    private void Jump()
    {
        Vector3 v = rb.linearVelocity;
        v.y = jumpForce;
        rb.linearVelocity = v;
        coyoteTimer = 0f;
    }

    private void StartSlide()
    {
        isSliding = true;
        slideTimer = slideDuration;
        ApplySlidingCollider();

        Vector3 v = rb.linearVelocity;
        v.z *= slideInitialBoost;
        rb.linearVelocity = v;
    }

    private void HandleSlideFriction()
    {
        Vector3 v = rb.linearVelocity;
        float sign = Mathf.Sign(v.z);
        float mag = Mathf.Max(0f, Mathf.Abs(v.z) - slideFriction * Time.deltaTime);
        v.z = mag * sign;
        rb.linearVelocity = v;
    }

    private void StopSlide()
    {
        if (CanStandUpCheckBlocked()) return;
        isSliding = false;
        ApplyStandingCollider();
    }

    private bool CanStandUpCheckBlocked()
    {
        float radius = col.radius * 0.95f;
        Vector3 center = transform.TransformPoint(standingCenter);

        float half = standingHeight * 0.5f - radius;
        Vector3 p1 = center + Vector3.up * half;
        Vector3 p2 = center - Vector3.up * half;

        return Physics.CheckCapsule(p1, p2, radius, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void ApplyStandingCollider()
    {
        col.height = standingHeight;
        col.center = standingCenter;
    }

    private void ApplySlidingCollider()
    {
        col.height = slidingHeight;
        col.center = slidingCenter;
    }
}
