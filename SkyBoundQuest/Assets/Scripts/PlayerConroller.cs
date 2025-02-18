using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    public float jumpVelocity;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float climbSpeed = 3f;
    private float _moveInput;
    private float _climbInput;
    private Rigidbody2D _rb;
    public float currentSpeed;

    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private Vector2 wallCheckSize;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isTouchingWall;

    [Header("Dash Settings")] [SerializeField]
    private float dashVelocity = 15f;

    [SerializeField] private float dashTime = 0.1f;
    private Vector2 _dashDirection;
    private bool _isDashing;
    private bool _canDash = true;

    [SerializeField] private bool isClimbing;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        _moveInput = Input.GetAxisRaw("Horizontal");
        _climbInput = Input.GetAxisRaw("Vertical");
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        isTouchingWall = Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0, groundLayer);

        if (Input.GetKeyDown(KeyCode.Period) && isGrounded)
        {
            Jump();
        }

        if (Input.GetButtonDown("Dash") && _canDash)
        {
            StartDash();
        }

        if (_climbInput is > 0 or < 0 && isTouchingWall)
        {
            isClimbing = true;
        }
        else if (!isTouchingWall || Input.GetAxisRaw("Vertical") == 0)
        {
            isClimbing = false;
        }

        if (isGrounded && !_isDashing)
        {
            _canDash = true;
        }

        Flip();
    }

    private void FixedUpdate()
    {
        if (_isDashing) return;

        if (isClimbing)
        {
            Climb();
        }

        Run();
        SetJumpFallSpeed();
    }

    private void Climb()
    {
        _rb.velocity = new Vector2(_rb.velocity.x, _climbInput * climbSpeed);
    }

    private void StartDash()
    {
        _isDashing = true;
        _canDash = false;

        _rb.velocity = new Vector2(_rb.velocity.x, 0); 

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

        _rb.velocity = new Vector2(_rb.velocity.x, 0); 
    }


    private void Jump()
    {
        _rb.velocity = new Vector2(_rb.velocity.x, jumpVelocity);
    }

    private void SetJumpFallSpeed()
    {
        switch (_rb.velocity.y)
        {
            case < 0:
                _rb.velocity += Vector2.up * ((Physics2D.gravity.y * (fallMultiplier - 1)) * Time.fixedDeltaTime);
                break;
            case > 0 when !(Input.GetKey(KeyCode.Period)):
                _rb.velocity += Vector2.up * ((Physics2D.gravity.y * (lowJumpMultiplier - 1)) * Time.fixedDeltaTime);
                break;
        }
    }

    private void Run()
    {
        _rb.velocity = _moveInput != 0
            ? new Vector2(_moveInput * moveSpeed, _rb.velocity.y)
            : new Vector2(0, _rb.velocity.y);
    }

    private void OnDrawGizmos()
    {
        if (wallCheck == null) return;
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(wallCheck.position, wallCheckSize);
        }
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    private void Flip()
    {
        transform.localScale = _moveInput switch
        {
            > 0 => new Vector3((float)1.5, (float)1.5, 1),
            < 0 => new Vector3((float)-1.5, (float)1.5, 1),
            _ => transform.localScale
        };
    }
}