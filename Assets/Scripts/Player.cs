using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    [Header("Andar / Correr")]
    [FormerlySerializedAs("speed")] public float walkSpeed = 6f;
    public float runSpeed = 10f;

    [Header("Pular / Pulo Duplo")]
    public float jumpForce = 12f;
    public float doubleJumpForce = 10f;
    public int extraJumps = 1;

    [Header("Segura Parede")]
    public Vector2 wallJumpForce = new Vector2(8f, 12f);
    public float wallJumpControlLock = .15f;

    [Header("Planar")]
    public float glideFallSpeed = 1.5f;

    [Header("Jogar Nozes")]
    public Rigidbody2D nutPrefab;
    public Transform throwPoint;
    public Vector2 throwVelocity = new Vector2(12f, 2f);
    public float throwCooldown = .3f;

    public NutControls Controls { get; private set; }
    public Vector2 MoveInput { get; private set; }
    public int FacingDirection { get; private set; } = 1;
    public bool IsRunning { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsGrabbingWall { get; private set; }
    public bool IsGliding { get; private set; }

    private Rigidbody2D rig;
    private Collider2D col;
    private SpriteRenderer sprite;
    private float defaultGravity;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    private int wallDirection;
    private int jumpsLeft;
    private float controlLockedUntil;
    private float nextThrowTime;

    private bool jumpRequested;
    private bool wallGrabHeld;
    private bool glideHeld;

    private void Awake()
    {
        rig = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        defaultGravity = rig.gravityScale;

        Controls = new NutControls();
    }

    private void OnEnable()
    {
        Controls.Enable();
    }

    private void OnDisable()
    {
        Controls.Disable();
    }

    private void OnDestroy()
    {
        Controls.Dispose();
    }

    // lê o input a cada frame; a física é aplicada no FixedUpdate
    private void Update()
    {
        var input = Controls.Player;

        MoveInput = input.Move.ReadValue<Vector2>();
        IsRunning = IsHeld(input.Run);
        wallGrabHeld = IsHeld(input.WallGrab);
        glideHeld = IsHeld(input.Glide);

        // "triggered" só vale no frame em que o botão foi apertado,
        // então guardamos o pedido até o próximo FixedUpdate
        if (input.Jump.triggered)
            jumpRequested = true;

        if (input.ThrowNut.triggered)
            ThrowNut();
    }

    private void FixedUpdate()
    {
        CheckContacts();

        if (IsGrounded)
            jumpsLeft = extraJumps;

        IsGrabbingWall = wallGrabHeld && wallDirection != 0 && !IsGrounded;
        IsGliding = false;

        var velocity = rig.velocity;

        if (IsGrabbingWall)
        {
            rig.gravityScale = 0f;
            velocity = Vector2.zero;
            jumpsLeft = extraJumps;

            if (jumpRequested)
            {
                // pulo de parede: empurra para longe da parede
                velocity = new Vector2(-wallDirection * wallJumpForce.x, wallJumpForce.y);
                controlLockedUntil = Time.time + wallJumpControlLock;
            }
        }
        else
        {
            rig.gravityScale = defaultGravity;

            if (Time.time >= controlLockedUntil)
                velocity.x = MoveInput.x * (IsRunning ? runSpeed : walkSpeed);

            if (jumpRequested && IsGrounded)
            {
                velocity.y = jumpForce;
            }
            else if (jumpRequested && jumpsLeft > 0)
            {
                velocity.y = doubleJumpForce;
                jumpsLeft--;
            }
            else if (!IsGrounded && glideHeld && velocity.y < -glideFallSpeed)
            {
                velocity.y = -glideFallSpeed;
                IsGliding = true;
            }
        }

        jumpRequested = false;
        rig.velocity = velocity;
        HandleFlip(velocity.x);
    }

    private static bool IsHeld(InputAction action)
    {
        return action.ReadValue<float>() > .5f;
    }

    private void CheckContacts()
    {
        IsGrounded = false;
        wallDirection = 0;

        var count = rig.GetContacts(contacts);
        for (var i = 0; i < count; i++)
        {
            var normal = contacts[i].normal;

            if (normal.y > .7f)
                IsGrounded = true;
            else if (normal.x > .7f)
                wallDirection = -1;
            else if (normal.x < -.7f)
                wallDirection = 1;
        }
    }

    private void HandleFlip(float xVelocity)
    {
        if (xVelocity > .01f)
            FacingDirection = 1;
        else if (xVelocity < -.01f)
            FacingDirection = -1;

        if (sprite != null)
            sprite.flipX = FacingDirection < 0;
    }

    private void ThrowNut()
    {
        if (Time.time < nextThrowTime)
            return;

        if (nutPrefab == null)
        {
            Debug.LogWarning("Player: arraste um prefab de noz para o campo 'Nut Prefab'.", this);
            return;
        }

        nextThrowTime = Time.time + throwCooldown;

        var origin = throwPoint != null
            ? throwPoint.position
            : transform.position + new Vector3(FacingDirection * .3f, 0f);

        var nut = Instantiate(nutPrefab, origin, Quaternion.identity);
        nut.velocity = new Vector2(throwVelocity.x * FacingDirection, throwVelocity.y);

        var nutCollider = nut.GetComponent<Collider2D>();
        if (nutCollider != null && col != null)
            Physics2D.IgnoreCollision(nutCollider, col);
    }
}
