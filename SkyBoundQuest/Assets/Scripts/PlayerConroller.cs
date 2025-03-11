using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using DialogueSystem.Runtime.Narration;

public class PlayerController : MonoBehaviour, IPlayerController
{
    private Rigidbody2D _rb;
    private CapsuleCollider2D _col;
    private FrameInput _frameInput;
    private Vector2 _frameVelocity;
    private bool _cachedQueryStartInColliders;
    [SerializeField] private float dashTime = 0.1f;
    [SerializeField] private Vector2 dashDirection;
    [SerializeField] private float dashVelocity = 28f;
    [SerializeField] private bool isDashing;
    [SerializeField] private bool canDash = true;

    public float climbInput;
    [SerializeField] private bool isClimbing;
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private bool isTouchingWall;
    [SerializeField] private Vector2 wallCheckSize;
    [SerializeField] private Transform wallCheck;

    [Header("LAYERS")] [Tooltip("Set this to the layer your player is on")]
    public LayerMask playerLayer;

    [Header("INPUT")]
    [Tooltip(
        "Makes all Input snap to an integer. Prevents gamepads from walking slowly. Recommended value is true to ensure gamepad/keybaord parity.")]
    public bool snapInput = true;

    [Tooltip(
         "Minimum input required before you mount a ladder or climb a ledge. Avoids unwanted climbing using controllers"),
     Range(0.01f, 0.99f)]
    public float verticalDeadZoneThreshold = 0.3f;

    [Tooltip("Minimum input required before a left or right is recognized. Avoids drifting with sticky controllers"),
     Range(0.01f, 0.99f)]
    public float horizontalDeadZoneThreshold = 0.1f;

    [Header("MOVEMENT")] [Tooltip("The top horizontal movement speed")]
    public float maxSpeed = 8;

    [Tooltip("The player's capacity to gain horizontal speed")]
    public float acceleration = 120;

    [FormerlySerializedAs("GroundDeceleration")] [Tooltip("The pace at which the player comes to a stop")]
    public float groundDeceleration = 140;

    [FormerlySerializedAs("AirDeceleration")] [Tooltip("Deceleration in air only after stopping input mid-air")]
    public float airDeceleration = 30;

    [FormerlySerializedAs("GroundingForce")]
    [Tooltip("A constant downward force applied while grounded. Helps on slopes"), Range(0f, -10f)]
    public float groundingForce = -1.5f;

    [FormerlySerializedAs("GrounderDistance")]
    [Tooltip("The detection distance for grounding and roof detection"), Range(0f, 0.25f)]
    public float grounderDistance = 0.25f;

    [FormerlySerializedAs("JumpPower")] [Header("JUMP")] [Tooltip("The immediate velocity applied when jumping")]
    public float jumpPower = 25;

    [FormerlySerializedAs("MaxFallSpeed")] [Tooltip("The maximum vertical movement speed")]
    public float maxFallSpeed = 20;

    [FormerlySerializedAs("FallAcceleration")]
    [Tooltip("The player's capacity to gain fall speed. a.k.a. In Air Gravity")]
    public float fallAcceleration = 110;

    [FormerlySerializedAs("JumpEndEarlyGravityModifier")]
    [Tooltip("The gravity multiplier added when jump is released early")]
    public float jumpEndEarlyGravityModifier = 5;

    [FormerlySerializedAs("CoyoteTime")]
    [Tooltip(
        "The time before coyote jump becomes unusable. Coyote jump allows jump to execute even after leaving a ledge")]
    public float coyoteTime = .15f;

    [FormerlySerializedAs("JumpBuffer")]
    [Tooltip("The amount of time we buffer a jump. This allows jump input before actually hitting the ground")]
    public float jumpBuffer = .2f;

    [SerializeField] private NarrativeController narrativeController;
    public Vector2 InputDirection { get; private set; }
    #region Interface

    public Vector2 FrameInput => _frameInput.Move;
    public event Action<bool, float> GroundedChanged;
    public event Action Jumped;

    #endregion

    private float _time;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<CapsuleCollider2D>();

        _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
    }

    private void Update()
    {
        Flip();
        climbInput = Input.GetAxisRaw("Vertical");

        isTouchingWall = Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0, playerLayer);

        if (climbInput != 0 && isTouchingWall)
        {
            isClimbing = true;
        }
        else if (!isTouchingWall || climbInput == 0)
        {
            isClimbing = false;
        }

        _time += Time.deltaTime;
        GatherInput();
        if (Input.GetButtonDown("Dash") && canDash)
        {
            StartDash();
        }

        if (grounded && !isDashing)
        {
            canDash = true;
        }
    }


    private void GatherInput()
    {
        _frameInput = new FrameInput
        {
            JumpDown = Input.GetKeyDown(KeyCode.Period),
            JumpHeld = Input.GetKey(KeyCode.Period),
            Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
        };

        if (snapInput)
        {
            _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < horizontalDeadZoneThreshold
                ? 0
                : Mathf.Sign(_frameInput.Move.x);
            _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < verticalDeadZoneThreshold
                ? 0
                : Mathf.Sign(_frameInput.Move.y);
        }

        if (_frameInput.JumpDown)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _time;
        }
    }

    private void FixedUpdate()
    {
        if (narrativeController.IsNarrating)
        {
            return;
        }
        if (isDashing) return;
        InputDirection = new Vector2(Input.GetAxisRaw("Horizontal"), 0);

        CheckCollisions();

        HandleJump();
        HandleDirection();
        HandleGravity();

        ApplyMovement();
    }

    #region Collisions

    private float _frameLeftGrounded = float.MinValue;

    [FormerlySerializedAs("_grounded")] [SerializeField]
    private bool grounded;

    private void CheckCollisions()
    {
        Physics2D.queriesStartInColliders = false;

        bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down,
            grounderDistance, playerLayer);
        bool ceilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up,
            grounderDistance, ~playerLayer);

        if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

        if (!grounded && groundHit)
        {
            grounded = true;
            _coyoteUsable = true;
            _bufferedJumpUsable = true;
            _endedJumpEarly = false;
            GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
        }
        else if (grounded && !groundHit)
        {
            grounded = false;
            _frameLeftGrounded = _time;
            GroundedChanged?.Invoke(false, 0);
        }

        Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
    }

    #endregion


    #region Jumping

    private bool _jumpToConsume;
    private bool _bufferedJumpUsable;
    private bool _endedJumpEarly;
    private bool _coyoteUsable;
    private float _timeJumpWasPressed;

    private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + jumpBuffer;
    private bool CanUseCoyote => _coyoteUsable && !grounded && _time < _frameLeftGrounded + coyoteTime;

    private void HandleJump()
    {
        if (!_endedJumpEarly && !grounded && !_frameInput.JumpHeld && _rb.velocity.y > 0) _endedJumpEarly = true;

        if (!_jumpToConsume && !HasBufferedJump) return;

        if (grounded || CanUseCoyote) ExecuteJump();

        _jumpToConsume = false;
    }

    private void ExecuteJump()
    {
        _endedJumpEarly = false;
        _timeJumpWasPressed = 0;
        _bufferedJumpUsable = false;
        _coyoteUsable = false;
        _frameVelocity.y = jumpPower;
        Jumped?.Invoke();
    }

    #endregion

    #region Dashing

    private void StartDash()
    {
        isDashing = true;
        canDash = false;

        dashDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (dashDirection == Vector2.zero)
        {
            dashDirection = new Vector2(transform.localScale.x, 0);
        }

        dashDirection.Normalize();
        _rb.velocity = dashDirection * dashVelocity;

        StartCoroutine(StopDash());
    }

    private IEnumerator StopDash()
    {
        yield return new WaitForSeconds(dashTime);
        isDashing = false;

    }

    #endregion

    #region DrawGizmos

    private void OnDrawGizmos()
    {
        if (wallCheck == null) return;
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(wallCheck.position, wallCheckSize);
        }
    }

    #endregion

    #region Horizontal

    private void HandleDirection()
    {
        if (_frameInput.Move.x == 0)
        {
            var deceleration = grounded ? groundDeceleration : airDeceleration;
            _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
        }
        else
        {
            _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * maxSpeed,
                acceleration * Time.fixedDeltaTime);
        }
    }

    #endregion


    #region Gravity

    private void HandleGravity()
    {
        if (isClimbing)
        {
            _frameVelocity.y = 0;
            return;
        }

        if (grounded && _frameVelocity.y <= 0f)
        {
            _frameVelocity.y = groundingForce;
        }
        else
        {
            var inAirGravity = fallAcceleration;
            if (_endedJumpEarly && _frameVelocity.y > 0) inAirGravity *= jumpEndEarlyGravityModifier;
            _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -maxFallSpeed, inAirGravity * Time.fixedDeltaTime);
        }
    }

    #endregion

    #region FlipFace

    private void Flip()
    {
        transform.localScale = _frameInput.Move.x switch
        {
            > 0 => new Vector3(2, 2, 1),
            < 0 => new Vector3(-2, 2, 1),
            _ => transform.localScale
        };
    }

    #endregion


    private void ApplyMovement()
    {
        _rb.velocity = isClimbing ? new Vector2(_rb.velocity.x, climbInput * climbSpeed) : _frameVelocity;
    }
}

public struct FrameInput
{
    public bool JumpDown;
    public bool JumpHeld;
    public Vector2 Move;
}

public interface IPlayerController
{
    public event Action<bool, float> GroundedChanged;

    public event Action Jumped;
    public Vector2 FrameInput { get; }
}