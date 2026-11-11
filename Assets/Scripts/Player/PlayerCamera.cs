using UnityEngine;
using UnityEngine.Events;

public class PlayerCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerMovement movement;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2.5f;
    [SerializeField] private float maxLookAngle = 89f;

    [Header("Strafe Tilt")]
    [SerializeField] private float rollAngle = 4f;
    [SerializeField] private float rollSpeed = 12f;

    private float currentRoll;

    [Header("Jump Camera")]
    [SerializeField] private float jumpTilt = -5f;
    [SerializeField] private float fallTilt = 3f;
    [SerializeField] private float jumpTiltSpeed = 8f;

    [Header("Landing")]
    [SerializeField] private float landingOffset = -0.12f;
    [SerializeField] private float landingTilt = 4f;
    [SerializeField] private float landingDuration = 0.12f;
    [SerializeField] private float landingReturnSpeed = 10f;

    [Header("Head Bob")]
    [SerializeField] private bool enableHeadBob = true;
    [SerializeField] private float bobFrequency = 10f;
    [SerializeField] private float bobAmplitude = 0.035f;
    [SerializeField] private float bobSideAmplitude = 0.02f;

    [Header("Dash FOV")]
    [SerializeField] private float normalFOV = 90f;
    [SerializeField] private float dashFOV = 100f;
    [SerializeField] private float fovSpeed = 10f;
    [Header("Camera Shake")]
    [SerializeField] private float defaultShakeIntensity = 0.15f;
    [SerializeField] private float defaultShakeDuration = 0.2f;
    [SerializeField] private float shakeFrequency = 18f;

   

    private Vector3 shakeOffset;

    [Header("Events")]
    public UnityEvent OnShake;

    private Camera cam;

    private float pitch;
    private float currentTilt;

    private Vector3 initialLocalPosition;

    private float bobTimer;

    private float shakeTimer;
    private float shakeIntensity;
    private float shakeDuration;

    private bool wasGrounded;

    private float landingTimer;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>();
        }

        if (player == null)
            player = transform.parent;

        if (movement == null && player != null)
            movement = player.GetComponent<PlayerMovement>();

        initialLocalPosition = transform.localPosition;

        wasGrounded = movement != null && movement.IsGrounded;

        cam.fieldOfView = normalFOV;
    }

    private void Start()
    {
        LockCursor();
    }

    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovementCamera();
        HandleLanding();
        HandleHeadBob();
        HandleShake();
        HandleFOV();
    }

    // ============================================
    // LOOK
    // ============================================

    private void HandleLook()
    {
        if (player == null)
            return;

        // Открыто меню / игрок мёртв — мышь не крутит камеру
        if (GameState.InputBlocked || GameState.PlayerDead)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Горизонтальный поворот игрока — мгновенный
        player.Rotate(Vector3.up * mouseX);

        // Вертикальный поворот камеры — мгновенный
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

        // Наклон камеры при стрейфе
        float targetRoll = 0f;

        if (movement != null)
        {
            Vector3 localVelocity =
                player.InverseTransformDirection(
                    movement.HorizontalVelocity
                );

            if (localVelocity.x < -0.1f)
            {
                // Движение влево
                targetRoll = rollAngle;
            }
            else if (localVelocity.x > 0.1f)
            {
                // Движение вправо
                targetRoll = -rollAngle;
            }
        }

        // Плавность оставляем только для наклона
        currentRoll = Mathf.Lerp(
            currentRoll,
            targetRoll,
            rollSpeed * Time.deltaTime
        );

        // Итоговый поворот камеры
        transform.localRotation = Quaternion.Euler(
            pitch + currentTilt,
            0f,
            currentRoll
        );
    }

    // ============================================
    // JUMP / FALL CAMERA
    // ============================================

    private void HandleMovementCamera()
    {
        if (movement == null)
            return;

        float targetTilt = 0f;

        if (!movement.IsGrounded)
        {
            Vector3 velocity = movement.Velocity;

            if (velocity.y > 0.1f)
            {
                // Игрок летит вверх
                targetTilt = jumpTilt;
            }
            else if (velocity.y < -0.1f)
            {
                // Игрок падает
                targetTilt = fallTilt;
            }
        }

        currentTilt = Mathf.Lerp(
            currentTilt,
            targetTilt,
            jumpTiltSpeed * Time.deltaTime
        );
    }

    // ============================================
    // LANDING
    // ============================================

    private void HandleLanding()
    {
        if (movement == null)
            return;

        bool grounded = movement.IsGrounded;

        // Был в воздухе  стал на земле
        if (!wasGrounded && grounded)
        {
            StartLanding();
        }

        wasGrounded = grounded;

        // Анимация приземления
        if (landingTimer > 0f)
        {
            landingTimer -= Time.deltaTime;

            float normalized =
                1f - Mathf.Clamp01(landingTimer / landingDuration);

            float strength =
                1f - Mathf.SmoothStep(0f, 1f, normalized);

            Vector3 landingPosition =
                initialLocalPosition +
                Vector3.up * (landingOffset * strength);

            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                landingPosition,
                20f * Time.deltaTime
            );
        }
        else
        {
            // Возврат камеры в центр
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                initialLocalPosition,
                landingReturnSpeed * Time.deltaTime
            );
        }
    }

    private void StartLanding()
    {
        landingTimer = landingDuration;

        // Небольшой удар камеры
        Shake(0.08f, 0.1f);
    }

    // ============================================
    // HEAD BOB
    // ============================================

    private void HandleHeadBob()
    {
        if (!enableHeadBob || movement == null)
            return;

        if (!movement.IsGrounded)
        {
            bobTimer = 0f;
            return;
        }

        Vector3 horizontalVelocity =
            movement.HorizontalVelocity;

        float speed = horizontalVelocity.magnitude;

        if (speed < 0.1f)
        {
            bobTimer = Mathf.Lerp(
                bobTimer,
                0f,
                10f * Time.deltaTime
            );

            return;
        }

        bobTimer += Time.deltaTime * bobFrequency;

        float verticalBob =
            Mathf.Sin(bobTimer) * bobAmplitude;

        float sideBob =
            Mathf.Cos(bobTimer * 0.5f) * bobSideAmplitude;
        Vector3 targetPosition =
            initialLocalPosition +
            new Vector3(
                sideBob,
                verticalBob,
                0f
            );

        targetPosition += shakeOffset;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPosition,
            10f * Time.deltaTime
                );
    }

    // ============================================
    // CAMERA SHAKE
    // ============================================

    private void HandleShake()
    {
        if (shakeTimer <= 0f)
        {
            shakeOffset = Vector3.Lerp(
                shakeOffset,
                Vector3.zero,
                15f * Time.deltaTime
            );

            return;
        }

        shakeTimer -= Time.deltaTime;

        float progress = 1f - (shakeTimer / shakeDuration);

        // Плавное затухание
        float fade = 1f - Mathf.SmoothStep(0f, 1f, progress);

        float time = Time.time * shakeFrequency;

        // Плавное псевдослучайное движение
        float x = Mathf.Sin(time * 1.1f);
        float y = Mathf.Sin(time * 1.7f + 2f);
        float z = Mathf.Sin(time * 1.3f + 4f);

        Vector3 targetShake = new Vector3(x, y, z);

        targetShake *= shakeIntensity * fade;

        // Плавно двигаемся к новой позиции
        shakeOffset = Vector3.Lerp(
            shakeOffset,
            targetShake,
            12f * Time.deltaTime
        );

        // Поворотная тряска — видна, даже когда игрок стоит.
        // HandleLook каждый кадр выставляет поворот заново, поэтому не накапливается.
        if (!GameState.InputBlocked && !GameState.PlayerDead)
        {
            transform.localRotation *= Quaternion.Euler(
                shakeOffset.y * 20f,
                shakeOffset.x * 20f,
                shakeOffset.z * 12f
            );
        }
    }
    

    /// <summary>
    /// Shake камеры с параметрами по умолчанию.
    /// </summary>
    public void Shake()
    {
        Shake(
            defaultShakeIntensity,
            defaultShakeDuration
        );
    }

    /// <summary>
    /// Shake камеры.
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        // Не перебиваем более сильную тряску слабой
        if (shakeTimer > 0f && intensity < shakeIntensity * (shakeTimer / Mathf.Max(0.0001f, shakeDuration)))
            return;

        shakeIntensity = intensity;
        shakeTimer = duration;
        shakeDuration = Mathf.Max(0.0001f, duration);

        OnShake?.Invoke();
    }

    // ============================================
    // FOV
    // ============================================

    private void HandleFOV()
    {
        if (cam == null || movement == null)
            return;

        float targetFOV =
            movement.IsDashing
                ? dashFOV
                : normalFOV;

        cam.fieldOfView = Mathf.Lerp(
            cam.fieldOfView,
            targetFOV,
            fovSpeed * Time.deltaTime
        );
    }
}