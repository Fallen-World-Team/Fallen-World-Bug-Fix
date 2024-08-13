using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//////////SETUP INSTRUCTIONS//////////
//Attach this script a RigidBody2D to the player GameObject
//Set Body type to Dynamic, Collision detection to continuous and Freeze Z rotation
//Add a 2D Collider (Any will do, but 2D box collider)
//Define the ground and wall mask layers (In the script and in the GameObjects)
//Adjust and play around with the other variables (Some require you to activate gizmos in order to visualize)

public class Movement2D : MonoBehaviour
{
    [Header("Components")]
    private Rigidbody2D _rb;
    private Transform playerTransform;

    [Header("Layer Masks")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _wallLayer;
    [SerializeField] private LayerMask _cornerCorrectLayer;

    [Header("Movement Variables")]
    [SerializeField] private float _movementAcceleration = 70f;
    [SerializeField] private float _maxMoveSpeed = 12f;
    [SerializeField] private float _groundLinearDrag = 7f;
    private float recentInputX;
    [SerializeField] private float _horizontalDirection;
    [SerializeField] private float _verticalDirection;
    private bool _changingDirection => (_rb.velocity.x > 0f && _horizontalDirection < 0f) || (_rb.velocity.x < 0f && _horizontalDirection > 0f);
    private bool _facingRight = true;
    private bool _canMove => !_wallGrab;

    [Header("Jump Variables")]
    [SerializeField] private float _jumpForce = 12f;
    [SerializeField] private float _jumpGravity;
    [SerializeField] private float _airLinearDrag = 2.5f;
    [SerializeField] private float _fallMultiplier = 8f;
    [SerializeField] private float _lowJumpFallMultiplier = 5f;
    [SerializeField] private float _downMultiplier = 12f;
    [SerializeField] private int _extraJumps = 1;
    [SerializeField] private float _hangTime = .1f;
    [SerializeField] private float _jumpBufferLength = .1f;
    private int _extraJumpsValue;
    private float _hangTimeCounter;
    private float _jumpBufferCounter;
    private bool _canJump => _jumpBufferCounter > 0f && (_hangTimeCounter > 0f || _extraJumpsValue > 0 || _onWall);
    private bool _isJumping = false;
    
    [Header("Dash Variables")]
    [SerializeField] private float _dashSpeed = 15f;
    [SerializeField] private float _dashLength = .3f;
    [SerializeField] private float _dashBufferLength = .1f;
    private float _dashBufferCounter;
    private bool _isDashing;
    private bool hasDashed;
    private bool _canDash => _dashBufferCounter > 0f && !hasDashed;

    [Header("Wall Movement Variables")]
    [SerializeField] private float _wallSlideModifier = 0.5f;
    [SerializeField] private float _wallJumpXVelocityHaltDelay = 0.2f;
    private bool _wallGrab => _onWall && !isGrounded() && Input.GetButton("WallGrab");
    private bool _wallSlide => _onWall && !isGrounded() && !Input.GetButton("WallGrab") && _rb.velocity.y < 0f;

    [Header("Ground Collision Variables")]
    [SerializeField] private Vector2 groundBoxSize;
    [SerializeField] private float groundCastDistance;

    [Header("Wall Collision Variables")]
    [SerializeField] private Vector2 wallBoxSize;
    [SerializeField] private float wallCastDistance;
    private bool _onWall;
    private bool isTouchingWallLeft;
    private bool isTouchingWallRight;

    [Header("Corner Correction Variables")]
    [SerializeField] private float _topRaycastLength;
    [SerializeField] private Vector3 _edgeRaycastOffset;
    [SerializeField] private Vector3 _innerRaycastOffset;
    private bool _canCornerCorrect;
    
    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        playerTransform = GetComponent<Transform>();
    }

    private void Update()
    {

        if(_canDash)
        {
            if(!isGrounded() && _horizontalDirection == 0f && _verticalDirection != 0f)
            {
                StartCoroutine(Dash(0, _verticalDirection));
            }
            else
            {
                StartCoroutine(Dash(recentInputX, _verticalDirection));
            }
        }

        isTouchingWallRight = Physics2D.BoxCast(transform.position, wallBoxSize, 0, Vector2.right, wallCastDistance, _wallLayer);
        isTouchingWallLeft = Physics2D.BoxCast(transform.position, wallBoxSize, 0, Vector2.right, wallCastDistance * -1, _wallLayer);
        _horizontalDirection = GetInput().x;
        _verticalDirection = GetInput().y;
        if (Input.GetButtonDown("Jump")) _jumpBufferCounter = _jumpBufferLength;
        else _jumpBufferCounter -= Time.deltaTime;
        if (Input.GetButtonDown("Dash")) _dashBufferCounter = _dashBufferLength;
        else _dashBufferCounter -= Time.deltaTime;
        _onWall = isTouchingWallRight || isTouchingWallLeft;

        if (_horizontalDirection == 1)
        {
            Vector3 rotator = new Vector3(playerTransform.rotation.x, 0f, playerTransform.rotation.z);
            transform.rotation = Quaternion.Euler(rotator);
            recentInputX = 1f;
        }
        else if (_horizontalDirection == -1)
        {
            Vector3 rotator = new Vector3(playerTransform.rotation.x, 180f, playerTransform.rotation.z);
            transform.rotation = Quaternion.Euler(rotator);
            recentInputX = -1f;
        }
    }

    private void FixedUpdate()
    {
        CheckCollisions();
        if(!_isDashing)
        {
        if (_canMove) MoveCharacter();
        else _rb.velocity = Vector2.Lerp(_rb.velocity, (new Vector2(_horizontalDirection * _maxMoveSpeed, _rb.velocity.y)), .5f * Time.deltaTime);
        if (isGrounded())
        {
            hasDashed = false;
            ApplyGroundLinearDrag();
            _extraJumpsValue = _extraJumps;
            _hangTimeCounter = _hangTime;
        }
        else
        {
            ApplyAirLinearDrag();
            FallMultiplier();
            _hangTimeCounter -= Time.fixedDeltaTime;
            if (!_onWall || _rb.velocity.y < 0f) _isJumping = false;
        }
        if (_canJump)
        {
            if (_onWall && !isGrounded())
            {
                if (isTouchingWallRight && _horizontalDirection > 0f || !isTouchingWallRight && _horizontalDirection < 0f)
                {
                    StartCoroutine(NeutralWallJump());
                }
                else
                {
                    WallJump();
                }
            }
            else
            {
                Jump(Vector2.up);
            }
        }
        if (!_isJumping)
        {
            if (_wallSlide && _horizontalDirection != 0f) WallSlide();
            if (_onWall) StickToWall();
        }
        }
    }
    private Vector2 GetInput()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    private void MoveCharacter()
    {
        _rb.AddForce(new Vector2(_horizontalDirection, 0f) * _movementAcceleration);

        if (Mathf.Abs(_rb.velocity.x) > _maxMoveSpeed)
            _rb.velocity = new Vector2(Mathf.Sign(_rb.velocity.x) * _maxMoveSpeed, _rb.velocity.y);
    }

    private void ApplyGroundLinearDrag()
    {
        if (Mathf.Abs(_horizontalDirection) < 0.4f || _changingDirection)
        {
            _rb.drag = _groundLinearDrag;
        }
        else
        {
            _rb.drag = 0f;
        }
    }

    private void ApplyAirLinearDrag()
    {
        _rb.drag = _airLinearDrag;
    }

    private void Jump(Vector2 direction)
    {
        if (!isGrounded() && !_onWall)
            _extraJumpsValue--;

        ApplyAirLinearDrag();
        _rb.velocity = new Vector2(_rb.velocity.x, 0f);
        _rb.AddForce(direction * _jumpForce, ForceMode2D.Impulse);
        _hangTimeCounter = 0f;
        _jumpBufferCounter = 0f;
        _isJumping = true;
        _airLinearDrag = 2;
    }

    private void WallJump()
    {
        Vector2 jumpDirection = isTouchingWallRight ? Vector2.left : Vector2.right;
        Jump(Vector2.up + jumpDirection);
    }

    IEnumerator NeutralWallJump()
    {
        Vector2 jumpDirection = isTouchingWallRight ? Vector2.left : Vector2.right;
        Jump(Vector2.up + jumpDirection);
        yield return new WaitForSeconds(_wallJumpXVelocityHaltDelay);
        _rb.velocity = new Vector2(0f, _rb.velocity.y);
    }
    
    private void FallMultiplier()
    {
        if (_verticalDirection < 0f)
        {
            _rb.gravityScale = _downMultiplier;
        }
        else
        {
            if (_rb.velocity.y < 0)
            {
                _rb.gravityScale = _fallMultiplier;
            }
            else if (_rb.velocity.y > 0 && !Input.GetButton("Jump"))
            {
                _rb.gravityScale = _lowJumpFallMultiplier;
            }
            else
            {
                _rb.gravityScale = _jumpGravity;
            }
        }
    }

    void WallSlide()
    {
        _rb.velocity = new Vector2(_rb.velocity.x, -_maxMoveSpeed * _wallSlideModifier);
    }

    void StickToWall()
    {
        //Push player torwards wall
        if (isTouchingWallRight && _horizontalDirection >= 0f)
        {
            _rb.velocity = new Vector2(1f, _rb.velocity.y);
        }
        else if (!isTouchingWallRight && _horizontalDirection <= 0f)
        {
            _rb.velocity = new Vector2(-1f, _rb.velocity.y);
        }
    }

    private void CheckCollisions()
    {

        //Corner Collisions
        _canCornerCorrect = Physics2D.Raycast(transform.position + _edgeRaycastOffset, Vector2.up, _topRaycastLength, _cornerCorrectLayer) &&
                            !Physics2D.Raycast(transform.position + _innerRaycastOffset, Vector2.up, _topRaycastLength, _cornerCorrectLayer) ||
                            Physics2D.Raycast(transform.position - _edgeRaycastOffset, Vector2.up, _topRaycastLength, _cornerCorrectLayer) &&
                            !Physics2D.Raycast(transform.position - _innerRaycastOffset, Vector2.up, _topRaycastLength, _cornerCorrectLayer);

    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        //Ground Check
        Gizmos.DrawWireCube(transform.position - transform.up * groundCastDistance, groundBoxSize);

        //Corner Check
        Gizmos.DrawLine(transform.position + _edgeRaycastOffset, transform.position + _edgeRaycastOffset + Vector3.up * _topRaycastLength);
        Gizmos.DrawLine(transform.position - _edgeRaycastOffset, transform.position - _edgeRaycastOffset + Vector3.up * _topRaycastLength);
        Gizmos.DrawLine(transform.position + _innerRaycastOffset, transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength);
        Gizmos.DrawLine(transform.position - _innerRaycastOffset, transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength);

        //Corner Distance Check
        Gizmos.DrawLine(transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength,
                        transform.position - _innerRaycastOffset + Vector3.up * _topRaycastLength + Vector3.left * _topRaycastLength);
        Gizmos.DrawLine(transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength,
                        transform.position + _innerRaycastOffset + Vector3.up * _topRaycastLength + Vector3.right * _topRaycastLength);

        //Wall Check
        Gizmos.DrawWireCube(transform.position - transform.right * wallCastDistance, wallBoxSize);
        Gizmos.DrawWireCube(transform.position - transform.right * wallCastDistance * -1, wallBoxSize);
    }
    //Ground Collisions
    public bool isGrounded()
    {
        if((Physics2D.BoxCast(transform.position, groundBoxSize, 0, -transform.up, groundCastDistance, _groundLayer)))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    IEnumerator Dash(float x, float y)
    {
        float dashStartTime = Time.time;
        _isDashing = true;
        _isJumping = false;
        hasDashed = true;

        _rb.velocity = Vector2.zero;
        _rb.gravityScale = 0f;
        _rb.drag = 0f;

        Vector2 dir;
        if (x != 0f || y != 0f) dir = new Vector2(x,y);
        else
        {
            if (_facingRight) dir = new Vector2(1f, 0f);
            else dir = new Vector2(-1f, 0f);
        }

        while (Time.time < dashStartTime + _dashLength)
        {
            _rb.velocity = dir.normalized * _dashSpeed;
            yield return null;
        }

        _isDashing = false;
    }
}