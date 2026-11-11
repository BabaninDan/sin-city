using UnityEngine;

public class WeaponViewMotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement movement;

    [Header("General")]
    [SerializeField] private float positionSmooth = 12f;
    [SerializeField] private float rotationSmooth = 10f;

    [Header("Ground Movement")]
    [SerializeField] private float movementAmplitudeX = 0.035f;
    [SerializeField] private float movementAmplitudeY = 0.025f;
    [SerializeField] private float movementFrequency = 7f;

    [Header("Jump")]
    [SerializeField] private float jumpOffset = 0.08f;
    [SerializeField] private float jumpSmooth = 8f;

    [Header("Falling")]
    [SerializeField] private float fallingOffset = 0.06f;
    [SerializeField] private float fallingSmooth = 5f;

    [Header("Air Strafe")]
    [SerializeField] private float airStrafeTilt = 8f;
    [SerializeField] private float airStrafeTiltSmooth = 8f;

    [Header("Landing")]
    [SerializeField] private float landingOffset = 0.12f;
    [SerializeField] private float landingDuration = 0.12f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private float movementTime;

    private float currentJumpOffset;
    private float targetJumpOffset;

    private float currentFallingOffset;

    private float currentAirTilt;

    private float landingTimer;

    private bool wasGrounded;

    private void Awake()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;

        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();

        if (movement != null)
            wasGrounded = movement.IsGrounded;
    }

    private void Update()
    {
        if (movement == null)
            return;

        bool grounded = movement.IsGrounded;

        HandleGroundMovement(grounded);
        HandleJump(grounded);
        HandleFalling(grounded);
        HandleAirStrafe(grounded);
        HandleLanding(grounded);

        ApplyPosition();
        ApplyRotation();

        wasGrounded = grounded;
    }

    // =========================================================
    // GROUND MOVEMENT
    // =========================================================

    private void HandleGroundMovement(bool grounded)
    {
        // В воздухе полностью отключаем ground bob
        if (!grounded)
            return;

        float speed = movement.HorizontalVelocity.magnitude;

        if (speed > 0.1f)
        {
            float speedMultiplier =
                Mathf.Clamp01(speed / 8f);

            movementTime +=
                Time.deltaTime *
                movementFrequency *
                Mathf.Lerp(0.5f, 1.5f, speedMultiplier);
        }
    }

    private Vector3 GetInfinityOffset()
    {
        float speed = movement.HorizontalVelocity.magnitude;

        if (!movement.IsGrounded || speed < 0.1f)
            return Vector3.zero;

        // Форма 
        float x = Mathf.Sin(movementTime);
        float y = Mathf.Sin(movementTime * 2f);

        return new Vector3(
            x * movementAmplitudeX,
            y * movementAmplitudeY,
            0f
        );
    }

    // =========================================================
    // JUMP
    // =========================================================

    private void HandleJump(bool grounded)
    {
        // Только что прыгнули
        if (!grounded && wasGrounded)
        {
            targetJumpOffset = jumpOffset;
        }

        // На земле возвращаемся
        if (grounded)
        {
            targetJumpOffset = 0f;
        }

        currentJumpOffset = Mathf.Lerp(
            currentJumpOffset,
            targetJumpOffset,
            jumpSmooth * Time.deltaTime
        );
    }

    // =========================================================
    // FALLING
    // =========================================================

    private void HandleFalling(bool grounded)
    {
        if (grounded)
        {
            currentFallingOffset = Mathf.Lerp(
                currentFallingOffset,
                0f,
                fallingSmooth * Time.deltaTime
            );

            return;
        }

        // Получаем вертикальную скорость
        float verticalVelocity = movement.Velocity.y;

        if (verticalVelocity < 0f)
        {
            // Игрок падает
            currentFallingOffset = Mathf.Lerp(
                currentFallingOffset,
                fallingOffset,
                fallingSmooth * Time.deltaTime
            );
        }
        else
        {
            // Игрок ещё летит вверх
            currentFallingOffset = Mathf.Lerp(
                currentFallingOffset,
                0f,
                fallingSmooth * Time.deltaTime
            );
        }
    }

    // =========================================================
    // AIR STRAFE
    // =========================================================

    private void HandleAirStrafe(bool grounded)
    {
        if (grounded)
        {
            currentAirTilt = Mathf.Lerp(
                currentAirTilt,
                0f,
                airStrafeTiltSmooth * Time.deltaTime
            );

            return;
        }

        // Переводим мировую скорость в локальные координаты игрока
        Vector3 localVelocity =
            transform.parent.parent.InverseTransformDirection(
                movement.HorizontalVelocity
            );

        float horizontalSpeed = localVelocity.x;

        // Движение вправо
        if (horizontalSpeed > 0.1f)
        {
            currentAirTilt = Mathf.Lerp(
                currentAirTilt,
                -airStrafeTilt,
                airStrafeTiltSmooth * Time.deltaTime
            );
        }
        // Движение влево
        else if (horizontalSpeed < -0.1f)
        {
            currentAirTilt = Mathf.Lerp(
                currentAirTilt,
                airStrafeTilt,
                airStrafeTiltSmooth * Time.deltaTime
            );
        }
        else
        {
            currentAirTilt = Mathf.Lerp(
                currentAirTilt,
                0f,
                airStrafeTiltSmooth * Time.deltaTime
            );
        }
    }

    // =========================================================
    // LANDING
    // =========================================================

    private void HandleLanding(bool grounded)
    {
        // Воздух земля
        if (grounded && !wasGrounded)
        {
            landingTimer = landingDuration;
        }

        if (landingTimer > 0f)
        {
            landingTimer -= Time.deltaTime;
        }
    }

    private float GetLandingOffset()
    {
        if (landingTimer <= 0f)
            return 0f;

        float progress =
            1f - landingTimer / landingDuration;

        float curve =
            Mathf.Sin(progress * Mathf.PI);

        return -curve * landingOffset;
    }

    // =========================================================
    // POSITION
    // =========================================================

    private void ApplyPosition()
    {
        Vector3 targetPosition = initialPosition;

        // Только на земле
        targetPosition += GetInfinityOffset();

        // Прыжок
        targetPosition.y += currentJumpOffset;

        // При падении тянемся вверх
        targetPosition.y += currentFallingOffset;

        // Удар при приземлении
        targetPosition.y += GetLandingOffset();

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPosition,
            positionSmooth * Time.deltaTime
        );
    }

    // =========================================================
    // ROTATION
    // =========================================================

    private void ApplyRotation()
    {
        Quaternion targetRotation =
            initialRotation *
            Quaternion.Euler(
                0f,
                0f,
                currentAirTilt
            );

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRotation,
            rotationSmooth * Time.deltaTime
        );
    }
}