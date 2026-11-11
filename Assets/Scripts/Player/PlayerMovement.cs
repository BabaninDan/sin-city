using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float maxSpeed = 30f;
    [SerializeField] private float groundAcceleration = 45f;
    [SerializeField] private float groundDeceleration = 20f;

    [Header("Air Movement")]
    [SerializeField] private float airAcceleration = 30f;
    [SerializeField] private float airDeceleration = 5f;
    [SerializeField] private float maxAirSpeed = 8f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float doubleJumpForce = 6f;
    [SerializeField] private int maxJumps = 2;

    [Header("Gravity")]
    [SerializeField] private float gravity = -24f;
    [SerializeField] private float terminalVelocity = -50f;
    [SerializeField] private float groundedForce = -2f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 18f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashStaminaCost = 25f;
    [SerializeField] private bool allowAirDash = true;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Slide")]
    [SerializeField] private float slideBoost = 3f;
    [SerializeField] private float slideDeceleration = 8f;
    [SerializeField] private float slideDuration = 0.8f;
    [SerializeField] private float slideStaminaCost = 15f;

    [Header("Slide Collider")]
    [SerializeField] private float slideHeight = 1f;
    [SerializeField] private float slideCenterY = 0.5f;

    [Header("Fast Fall")]
    [SerializeField] private float fastFallSpeed = 25f;
    [SerializeField] private float fastFallAcceleration = 60f;

    [Header("Player Stats")]
    [SerializeField] private PlayerStats stats;

    //sound
    [Header("Footsteps")]
    [SerializeField] private float footstepInterval = 0.4f;
    [SerializeField] private float sprintFootstepInterval = 0.25f;

    private float footstepTimer;

    public PlayerAudio playerAudio;

    private CharacterController controller;

    // Horizontal velocity
    private Vector3 horizontalVelocity;

    // Vertical velocity
    private float verticalVelocity;

    // Jump counter
    private int jumpsUsed;

    // Dash
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

    // Input
    private Vector2 moveInput;


    private float normalHeight;
    private Vector3 normalCenter;


    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;

    private bool fastFalling;

    public bool IsSliding => isSliding;
    public bool IsFastFalling => fastFalling;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        

        normalHeight = controller.height;
        normalCenter = controller.center;



        if (controller == null)
        {
            Debug.LogError(
                "PlayerMovement: CharacterController не найден на Player!",
                gameObject
            );
        }

        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();

            if (cam != null)
            {
                cameraTransform = cam.transform;
            }
        }
       
    }

    private void Update()
    {
        // Меню / смерть — персонаж не слушается
        bool inputBlocked = GameState.InputBlocked || GameState.PlayerDead;

        if (inputBlocked)
            moveInput = Vector2.zero;
        else
            ReadInput();

        CheckGround();

        if (!inputBlocked)
            HandleJump();

        if (!inputBlocked && Input.GetKeyDown(KeyCode.LeftShift))
        {
            TryDash();
        }
        HandleDash();


        HandleMovement();

        HandleGravity();

        MovePlayer();

        HandleSlide();
        HandleFastFall();
        HandleFootsteps();
    }


    private void HandleFootsteps()
    {
        if (!IsGrounded)
            return;

        if (isSliding)
            return;

        float speed = horizontalVelocity.magnitude;

        if (speed < 1f)
        {
            footstepTimer = 0f;
            return;
        }

        float interval = footstepInterval;

        if (speed > moveSpeed)
            interval = sprintFootstepInterval;

        footstepTimer -= Time.deltaTime;

        if (footstepTimer <= 0f)
        {
            playerAudio?.PlayFootstep();

            footstepTimer = interval;
        }
    }
    private void HandleSlide()
    {
        if (!IsGrounded)
        {
            StopSlide();
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            StartSlide();
        }

        if (!isSliding)
            return;

        slideTimer -= Time.deltaTime;

        // Постепенно теряем скорость
        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            Vector3.zero,
            slideDeceleration * Time.deltaTime
        );

        if (slideTimer <= 0f ||
            horizontalVelocity.magnitude < 1f)
        {
            StopSlide();
        }
    }

    private void StartSlide()
    {
        if (isSliding)
            return;

        if (!IsGrounded)
            return;

        // Нужно хотя бы немного двигаться
        if (horizontalVelocity.magnitude < moveSpeed * 0.5f)
            return;

        // Проверяем стамину
        if (stats != null)
        {
            if (!stats.TryUseStamina(slideStaminaCost))
                return;
        }

        isSliding = true;
        slideTimer = slideDuration;

        // Направление текущего движения
        Vector3 slideDirection =
            horizontalVelocity.normalized;

        // Небольшой буст
        horizontalVelocity +=
            slideDirection * slideBoost;

        LimitHorizontalSpeed();

        // Уменьшаем хитбокс
        controller.height = slideHeight;

        controller.center = new Vector3(
            normalCenter.x,
            slideCenterY,
            normalCenter.z
        );
    }



    private void StopSlide()
    {
        if (!isSliding)
            return;

        isSliding = false;
        slideTimer = 0f;

        controller.height = normalHeight;
        controller.center = normalCenter;
    }

    private void HandleFastFall()
    {
        fastFalling = false;

        if (IsGrounded)
            return;

        if (Input.GetKey(KeyCode.LeftControl))
        {
            fastFalling = true;

            verticalVelocity = Mathf.MoveTowards(
                verticalVelocity,
                -fastFallSpeed,
                fastFallAcceleration * Time.deltaTime
            );
        }
    }

    private void ReadInput()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    private void CheckGround()
    {
        if (controller.isGrounded)
        {
            jumpsUsed = 0;

            if (verticalVelocity < 0f)
                verticalVelocity = groundedForce;
        }
    }

    private void HandleMovement()
    {
        if (isDashing)
            return;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // Не учитываем наклон камеры вверх/вниз
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 desiredDirection =
            forward * moveInput.y +
            right * moveInput.x;

        desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

        Vector3 targetVelocity = desiredDirection * moveSpeed;

        if (controller.isGrounded)
        {
            MoveOnGround(targetVelocity);
        }
        else
        {
            MoveInAir(targetVelocity);
        }
    }

    private void MoveOnGround(Vector3 targetVelocity)
    {
        if (isSliding)
            return;

        if (targetVelocity.sqrMagnitude > 0.01f)
        {
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetVelocity.normalized *
                horizontalVelocity.magnitude,
                groundAcceleration * Time.deltaTime
            );

            if (horizontalVelocity.magnitude < moveSpeed)
            {
                horizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    targetVelocity.normalized * moveSpeed,
                    groundAcceleration * Time.deltaTime
                );
            }
        }
        else
        {
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                groundDeceleration * Time.deltaTime
            );
        }

        LimitHorizontalSpeed();
    }

    private void MoveInAir(Vector3 targetVelocity)
    {
        if (targetVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 targetDirection = targetVelocity.normalized;

            float currentSpeed = horizontalVelocity.magnitude;

            // В воздухе сохраняем набранную скорость,
            // но позволяем менять направление
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetDirection * Mathf.Max(currentSpeed, moveSpeed),
                airAcceleration * Time.deltaTime
            );
        }

        LimitHorizontalSpeed();
    }

    private void LimitHorizontalSpeed()
    {
        Vector3 horizontal = new Vector3(
            horizontalVelocity.x,
            0f,
            horizontalVelocity.z
        );

        if (horizontal.magnitude > maxSpeed)
        {
            horizontal = horizontal.normalized * maxSpeed;

            horizontalVelocity.x = horizontal.x;
            horizontalVelocity.z = horizontal.z;
        }
    }

    private void HandleJump()
    {
        if (!Input.GetKeyDown(KeyCode.Space))
            return;

        // Первый прыжок
        if (controller.isGrounded)
        {
            verticalVelocity = jumpForce;
            jumpsUsed = 1;
            return;
        }

        // Второй прыжок
        if (jumpsUsed < maxJumps)
        {
            verticalVelocity = doubleJumpForce;
            jumpsUsed++;

            if (playerAudio != null)
                playerAudio.PlayDoubleJump();
        }
    }




    private void TryDash()
    {
        if (isDashing)
            return;

        if (!allowAirDash && !IsGrounded)
            return;

       

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            forward * moveInput.y +
            right * moveInput.x;

        if (direction.sqrMagnitude < 0.01f)
            direction = forward;

        direction.Normalize();

        // Тратим стамину
        if (stats != null)
        {

            if (!stats.TryUseStamina(dashStaminaCost))
                return;
            if (playerAudio != null)
                playerAudio.PlayDash();
        }

        dashDirection = direction;

        isDashing = true;
        dashTimer = dashDuration;

        // Задаём скорость рывка
        horizontalVelocity = dashDirection * dashSpeed;
    }

    private void HandleDash()
    {
        if (!isDashing)
            return;

        dashTimer -= Time.deltaTime;

        horizontalVelocity = dashDirection * dashSpeed;

        if (dashTimer <= 0f)
        {
            isDashing = false;
        }
    }
    private void HandleGravity()
    {
        if (isDashing)
            return;

        verticalVelocity += gravity * Time.deltaTime;

        if (verticalVelocity < terminalVelocity)
        {
            verticalVelocity = terminalVelocity;
        }
    }

    private void MovePlayer()
    {
        Vector3 velocity = horizontalVelocity;

        if (isDashing)
        {
            velocity = dashDirection * dashSpeed;
        }

        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    // =========================
    // Public properties
    // =========================

    public Vector3 Velocity
    {
        get
        {
            Vector3 velocity = horizontalVelocity;
            velocity.y = verticalVelocity;
            return velocity;
        }
    }

    public Vector3 HorizontalVelocity => horizontalVelocity;

    /// <summary>Мгновенно перенести игрока (переход между комнатами).</summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        bool wasEnabled = controller.enabled;
        controller.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        isDashing = false;
        StopSlide();

        controller.enabled = wasEnabled;
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Толчок от удара врага / взрыва.
    /// </summary>
    public void AddImpulse(Vector3 impulse)
    {
        horizontalVelocity += new Vector3(impulse.x, 0f, impulse.z);
        LimitHorizontalSpeed();

        if (impulse.y > 0f)
            verticalVelocity = Mathf.Max(verticalVelocity, impulse.y);
    }

    public bool IsDashing => isDashing;

    public bool IsGrounded =>
    controller != null && controller.isGrounded;

    public int JumpsUsed => jumpsUsed;

    public float DashCooldownRemaining => Mathf.Max(0f, dashCooldownTimer);
}