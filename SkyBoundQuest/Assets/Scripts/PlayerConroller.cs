using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour, IPlayerController
{
    private Rigidbody2D _rb;
    private CapsuleCollider2D _col;
    private FrameInput _frameInput;
    private Vector2 _frameVelocity;
    private bool _cachedQueryStartInColliders;
    [SerializeField] private float dashTime = 0.1f;
    [SerializeField] private Vector2 _dashDirection;
    [SerializeField] private float dashVelocity = 15f;
    [SerializeField] private bool _isDashing;
    [SerializeField] private bool _canDash = true;
    
    public float climbInput;
    [SerializeField] private bool isClimbing;
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private bool isTouchingWall;
    [SerializeField] private Vector2 wallCheckSize;
    [SerializeField] private Transform wallCheck;

    [Header("LAYERS")] [Tooltip("Set this to the layer your player is on")]
    public LayerMask PlayerLayer;

    [Header("INPUT")]
    [Tooltip(
        "Makes all Input snap to an integer. Prevents gamepads from walking slowly. Recommended value is true to ensure gamepad/keybaord parity.")]
    public bool SnapInput = true;

    [Tooltip(
         "Minimum input required before you mount a ladder or climb a ledge. Avoids unwanted climbing using controllers"),
     Range(0.01f, 0.99f)]
    public float VerticalDeadZoneThreshold = 0.3f;

    [Tooltip("Minimum input required before a left or right is recognized. Avoids drifting with sticky controllers"),
     Range(0.01f, 0.99f)]
    public float HorizontalDeadZoneThreshold = 0.1f;

    [Header("MOVEMENT")] [Tooltip("The top horizontal movement speed")]
    public float MaxSpeed = 14;

    [Tooltip("The player's capacity to gain horizontal speed")]
    public float Acceleration = 120;

    [Tooltip("The pace at which the player comes to a stop")]
    public float GroundDeceleration = 60;

    [Tooltip("Deceleration in air only after stopping input mid-air")]
    public float AirDeceleration = 30;

    [Tooltip("A constant downward force applied while grounded. Helps on slopes"), Range(0f, -10f)]
    public float GroundingForce = -1.5f;

    [Tooltip("The detection distance for grounding and roof detection"), Range(0f, 0.25f)]
    public float GrounderDistance = 0.25f;

    [Header("JUMP")] [Tooltip("The immediate velocity applied when jumping")]
    public float JumpPower = 36;

    [Tooltip("The maximum vertical movement speed")]
    public float MaxFallSpeed = 40;

    [Tooltip("The player's capacity to gain fall speed. a.k.a. In Air Gravity")]
    public float FallAcceleration = 110;

    [Tooltip("The gravity multiplier added when jump is released early")]
    public float JumpEndEarlyGravityModifier = 3;

    [Tooltip(
        "The time before coyote jump becomes unusable. Coyote jump allows jump to execute even after leaving a ledge")]
    public float CoyoteTime = .15f;

    [Tooltip("The amount of time we buffer a jump. This allows jump input before actually hitting the ground")]
    public float JumpBuffer = .2f;


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
        climbInput = Input.GetAxisRaw("Vertical");

        // FIRST: Check if touching a wall
        isTouchingWall = Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0, PlayerLayer);

        // THEN: Check if we should start climbing
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
        if (Input.GetButtonDown("Dash") && _canDash)
        {
            StartDash();
        }
        if (grounded && !_isDashing)
        {
            _canDash = true;
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

        if (SnapInput)
        {
            _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < HorizontalDeadZoneThreshold
                ? 0
                : Mathf.Sign(_frameInput.Move.x);
            _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < VerticalDeadZoneThreshold
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
        
        if (_isDashing) return;
        CheckCollisions();

        HandleJump();
        HandleDirection();
        HandleGravity();

        ApplyMovement();
    }

    #region Collisions

    private float _frameLeftGrounded = float.MinValue;
    [FormerlySerializedAs("_grounded")] [SerializeField] private bool grounded;

    private void CheckCollisions()
    {
        Physics2D.queriesStartInColliders = false;

        // Ground and Ceiling
        bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down,
            GrounderDistance, PlayerLayer);
        bool ceilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up,
            GrounderDistance, ~PlayerLayer);

        // Hit a Ceiling
        if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

        // Landed on the Ground
        if (!grounded && groundHit)
        {
            grounded = true;
            _coyoteUsable = true;
            _bufferedJumpUsable = true;
            _endedJumpEarly = false;
            GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
        }
        // Left the Ground
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

    private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + JumpBuffer;
    private bool CanUseCoyote => _coyoteUsable && !grounded && _time < _frameLeftGrounded + CoyoteTime;

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
        _frameVelocity.y = JumpPower;
        Jumped?.Invoke();
    }

    #endregion

    #region Dashing
    private void StartDash()
    {
        _isDashing = true;
        _canDash = false;

        _dashDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (_dashDirection == Vector2.zero)
        {
            _dashDirection = new Vector2(transform.localScale.x, 0);
        }

        _dashDirection.Normalize();
        _rb.velocity = _dashDirection * dashVelocity;

        StartCoroutine(StopDash());
    }
    
    private IEnumerator StopDash()
    {
        yield return new WaitForSeconds(dashTime);
        _isDashing = false;

        _rb.velocity = new Vector2(0, _rb.velocity.y);
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
    #region Climbing

    #endregion

    #region Horizontal

    private void HandleDirection()
    {
        if (_frameInput.Move.x == 0)
        {
            var deceleration = grounded ? GroundDeceleration : AirDeceleration;
            _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
        }
        else
        {
            _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, _frameInput.Move.x * MaxSpeed,
                Acceleration * Time.fixedDeltaTime);
        }
    }

    #endregion
    

    #region Gravity

    private void HandleGravity()
    {
        if (isClimbing)
        {
            _frameVelocity.y = 0; // Stop gravity influence when climbing
            return;
        }
    
        if (grounded && _frameVelocity.y <= 0f)
        {
            _frameVelocity.y = GroundingForce;
        }
        else
        {
            var inAirGravity = FallAcceleration;
            if (_endedJumpEarly && _frameVelocity.y > 0) inAirGravity *= JumpEndEarlyGravityModifier;
            _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
        }
    }


    #endregion
    

    private void ApplyMovement()
    {
        if (isClimbing)
        {
            _rb.velocity = new Vector2(_rb.velocity.x, climbInput * climbSpeed);
        }
        else
        {
            _rb.velocity = _frameVelocity;
        }
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