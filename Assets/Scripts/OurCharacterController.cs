using UnityEngine;


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
    [Tooltip("Allow jump shortly after leaving a platform")]
    public float coyoteTime = 0.12f;
    [Tooltip("Buffer jump input slightly before hitting ground")]
    public float jumpBufferTime = 0.12f;

    [Header("Wall Interactions")]
    public float wallCheckDistance = 0.55f;
    public float wallSlideSpeed = -2.5f;
    [Tooltip("Z push away from the wall on wall jump")]
    public float wallJumpZPush = 7.5f;
    [Tooltip("Y force on wall jump")]
    public float wallJumpUpForce = 12f;
    [Tooltip("How long to inhibit normal control after wall jump")]
    public float wallJumpControlLock = 0.15f;

    [Header("Slide (Ground)")]
    public KeyCode slideKey = KeyCode.LeftControl;
    public float slideDuration = 0.5f;
    public float slideInitialBoost = 2.0f; // multiplier on current speed
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
    public string moveAxis = "Horizontal"; // we'll use Horizontal to map A/D or left/right to Z
    public KeyCode jumpKey = KeyCode.Space;

    // internals
    Rigidbody rb;
    CapsuleCollider col;

    float coyoteTimer;
    float jumpBufferTimer;
    float wallJumpLockTimer;
    bool isGrounded;
    bool touchingWallFront; // in direction of input/facing
    bool touchingWallBack;  // behind
    int wallDir = 0; // -1 back (-Z), +1 front (+Z)
    bool isSliding;
    float slideTimer;

    // cached
    float targetZ;
    float currentZVel;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        // Use whatever is set in the Inspector as the standing pose
        standingHeight = col.height;
        standingCenter = col.center;
        // Optional: ensure Y direction
        col.direction = 1;
    }

    void Update()
    {
        // --- Input ---
        float input = Input.GetAxisRaw(moveAxis); // -1..1 (we'll map to Z)
        bool jumpPressed = Input.GetKeyDown(jumpKey);
        bool jumpHeld = Input.GetKey(jumpKey);
        bool slidePressed = Input.GetKeyDown(slideKey);

        // --- Ground & Wall Checks ---
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        // Determine where we're looking/moving for wall checks
        int desiredDir = input > 0.05f ? 1 : (input < -0.05f ? -1 : 0);

        // Raycast for walls on +Z and -Z
        touchingWallFront = Physics.Raycast(col.bounds.center, Vector3.forward, out _, wallCheckDistance, wallMask, QueryTriggerInteraction.Ignore);
        touchingWallBack = Physics.Raycast(col.bounds.center, Vector3.back, out _, wallCheckDistance, wallMask, QueryTriggerInteraction.Ignore);

        wallDir = 0;
        if (desiredDir != 0)
        {
            if (desiredDir > 0 && touchingWallFront) wallDir = +1;
            if (desiredDir < 0 && touchingWallBack) wallDir = -1;
        }

        // --- Timers ---
        if (isGrounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= Time.deltaTime;

        if (jumpPressed) jumpBufferTimer = jumpBufferTime;
        else jumpBufferTimer -= Time.deltaTime;

        if (wallJumpLockTimer > 0f) wallJumpLockTimer -= Time.deltaTime;

        // --- Jump Logic (ground / buffered) ---
        if (jumpBufferTimer > 0 && coyoteTimer > 0 && !isSliding)
        {
            Jump();
            jumpBufferTimer = 0;
        }

        // --- Wall Slide + Wall Jump ---
        bool canWallSlide = !isGrounded && wallDir != 0 && input != 0 && IsMovingTowardsWall(input, rb.linearVelocity.z);
        if (canWallSlide)
        {
            // clamp vertical fall while on wall
            Vector3 v = rb.linearVelocity;
            if (v.y < wallSlideSpeed) v.y = wallSlideSpeed;
            rb.linearVelocity = v;

            if (jumpPressed)
            {
                WallJump(wallDir);
            }
        }

        // --- Slide (ground, tap key while moving) ---
        if (slidePressed && isGrounded && Mathf.Abs(rb.linearVelocity.z) > 0.5f && !isSliding)
        {
            StartSlide();
        }

        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            HandleSlideFriction();
            if (slideTimer <= 0f || Mathf.Abs(rb.linearVelocity.z) < slideMinSpeed || !CanStandUpCheckBlocked() && Input.GetKeyUp(slideKey))
            {
                StopSlide();
            }
        }

        // --- Horizontal target (Z) ---
        float runAccel = isGrounded ? acceleration : acceleration * airControlTimes;
        float runDecel = isGrounded ? deceleration : deceleration * 0.7f;

        float desiredZVel = input * moveSpeed;

        if (wallJumpLockTimer > 0f) // brief lock after wall jump so player can't instantly cancel launch
        {
            desiredZVel = rb.linearVelocity.z;
        }
        else
        {
            // accelerate towards desiredZVel
            float zVel = rb.linearVelocity.z;
            float velDiff = desiredZVel - zVel;
            float accel = Mathf.Abs(desiredZVel) > 0.01f ? runAccel : runDecel;
            float movement = Mathf.Clamp(velDiff, -accel * Time.deltaTime, accel * Time.deltaTime);
            targetZ = zVel + movement;
        }
    }

    [Header("Better Jump")]
    public float fallGravityMultiplier = 2.5f;   // stronger pull when falling
    public float lowJumpMultiplier = 2.0f;       // tap jump = shorter hop

    void FixedUpdate()
    {
        Vector3 v = rb.linearVelocity;

        // Horizontal as before
        if (!isSliding) v.z = targetZ;

        // Extra gravity for better feel
        bool falling = v.y < 0f;
        bool risingButJumpReleased = v.y > 0f && !Input.GetKey(jumpKey);
        float extraMult = 1f;

        if (falling) extraMult = fallGravityMultiplier;
        else if (risingButJumpReleased) extraMult = lowJumpMultiplier;

        if (extraMult > 1f)
        {
            // add extra downward accel on top of Physics.gravity
            v.y += Physics.gravity.y * (extraMult - 1f) * Time.fixedDeltaTime;
        }

        // Optional: keep a terminal velocity, but let it be high enough
        if (v.y < maxFallSpeed) v.y = maxFallSpeed; // e.g., set to -60f

        rb.linearVelocity = v;
    }

    void Jump()
    {
        // preserve Z, set Y impulse
        Vector3 v = rb.linearVelocity;
        v.y = jumpForce;
        rb.linearVelocity = v;
        coyoteTimer = 0f;
    }

    void WallJump(int wallDirection) // wallDirection: +1 means wall at +Z, -1 means wall at -Z
    {
        // push away from wall (opposite Z) + up
        float push = -wallDirection * wallJumpZPush;
        Vector3 v = rb.linearVelocity;
        v.y = wallJumpUpForce;
        v.z = push;
        rb.linearVelocity = v;

        wallJumpLockTimer = wallJumpControlLock;
        jumpBufferTimer = 0f;
    }

    bool IsMovingTowardsWall(float input, float zVel)
    {
        // If input and velocity (or desired) point into a wall direction, treat as towards wall
        if (input > 0.05f && touchingWallFront) return true;
        if (input < -0.05f && touchingWallBack) return true;
        return false;
    }

    // --- Slide helpers ---
    void StartSlide()
    {
        isSliding = true;
        slideTimer = slideDuration;

        // collider shrink
        ApplySlidingCollider();

        // give initial boost (keep direction)
        Vector3 v = rb.linearVelocity;
        v.z *= slideInitialBoost;
        rb.linearVelocity = v;
    }

    void HandleSlideFriction()
    {
        Vector3 v = rb.linearVelocity;
        float sign = Mathf.Sign(v.z);
        float mag = Mathf.Max(0f, Mathf.Abs(v.z) - slideFriction * Time.deltaTime);
        v.z = mag * sign;
        rb.linearVelocity = v;
    }

    void StopSlide()
    {
        // only stop slide if we have room to stand up OR we¡¯re not under a low ceiling
        if (CanStandUpCheckBlocked()) return;

        isSliding = false;
        ApplyStandingCollider();
    }

    bool CanStandUpCheckBlocked()
    {
        // do a capsule check above current sliding collider to see if there's room to stand
        float radius = col.radius * 0.95f;
        Vector3 center = transform.TransformPoint(standingCenter);
        float half = standingHeight * 0.5f - radius;
        Vector3 p1 = center + Vector3.up * half;
        Vector3 p2 = center - Vector3.up * half;
        bool blocked = Physics.CheckCapsule(p1, p2, radius, groundMask, QueryTriggerInteraction.Ignore);
        return blocked;
    }

    void ApplyStandingCollider()
    {
        col.height = standingHeight;
        col.center = standingCenter;
    }

    void ApplySlidingCollider()
    {
        col.height = slidingHeight;
        col.center = slidingCenter;
    }

    // --- Gizmos for easier setup ---
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (GetComponent<Collider>() != null)
        {
            Gizmos.color = Color.cyan;
            Bounds b = GetComponent<Collider>().bounds;
            Vector3 p = b.center;
            Gizmos.DrawLine(p, p + Vector3.forward * wallCheckDistance);
            Gizmos.DrawLine(p, p + Vector3.back * wallCheckDistance);
        }
    }
}
