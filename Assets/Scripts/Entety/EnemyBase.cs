using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBase : MonoBehaviour, IDamageable
{
    public enum EnemyDirection
    {
        Front,
        Back,
        Left,
        Right
    }

    public enum EnemyState
    {
        Idle,
        Run,
        Fall,
        DieProcess,
        Die,
        Attack
    }

    [Header("Movement")]
    [SerializeField] protected float moveSpeed = 4f;
    [SerializeField] protected float stopDistance = 1.5f;

    [Header("Health")]
    [SerializeField] protected float maxHealth = 100f;

    [Header("Target")]
    [SerializeField] protected Transform target;
    [Header("Detection")]
    [SerializeField] private float viewDistance = 15f;

    private bool hasSeenTarget;

    [Header("Visual")]
    [SerializeField] private Transform visual;
    [SerializeField] private Animator animator;

    [Header("Direction")]
    [Tooltip("Угол, в пределах которого игрок считается находящимся спереди.")]
    [SerializeField] private float frontAngle = 45f;

    [Tooltip("Угол, в пределах которого игрок считается находящимся сзади.")]
    [SerializeField] private float backAngle = 45f;

    protected NavMeshAgent agent;

    protected float currentHealth;

    protected EnemyDirection currentDirection;
    protected EnemyState currentState;

    private bool isDead;

    private string currentAnimation;

    public float Health => currentHealth;
    public float MaxHealth => maxHealth;

    public float MoveSpeed => moveSpeed;

    public bool HasTarget => target != null;

    public bool IsDead => isDead;

    public EnemyDirection CurrentDirection => currentDirection;

    public EnemyState CurrentState => currentState;


    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        currentHealth = maxHealth;

        agent.speed = moveSpeed;
        agent.stoppingDistance = stopDistance;

        // Навигация не вращает моба.
        agent.updateRotation = false;

        FindTarget();

        if (visual == null)
        {
            Transform foundVisual =
                transform.Find("Visual");

            if (foundVisual != null)
                visual = foundVisual;
        }

        if (animator == null && visual != null)
        {
            animator =
                visual.GetComponent<Animator>();
        }

        currentDirection = EnemyDirection.Front;
        currentState = EnemyState.Idle;

        PlayAnimation();
    }


    protected virtual void Update()
    {
        if (isDead)
            return;

        if (target == null)
        {
            FindTarget();

            if (target == null)
            {
                SetState(EnemyState.Idle);
                return;
            }
        }

        // Пока моб ещё не увидел игрока
        if (!hasSeenTarget)
        {
            if (CanSeeTarget())
            {
                hasSeenTarget = true;
            }
            else
            {
                SetState(EnemyState.Idle);
                return;
            }
        }

        UpdateMovement();
        UpdateDirection();
        UpdateAnimation();
    }


    protected virtual void LateUpdate()
    {
        UpdateBillboard();
    }
    private bool CanSeeTarget()
    {
        if (target == null)
            return false;

        Vector3 toTarget =
            target.position - transform.position;

        // Не учитываем высоту
        toTarget.y = 0f;

        float distance = toTarget.magnitude;

        // Игрок слишком далеко
        if (distance > viewDistance)
            return false;

        if (distance < 0.001f)
            return true;

        toTarget.Normalize();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        float angle =
            Vector3.Angle(forward, toTarget);

        // Игрок находится внутри Front-сектора
        return angle <= frontAngle;
    }

    private void FindTarget()
    {
        if (target != null)
            return;

        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            target = player.transform;
    }


    protected virtual void UpdateMovement()
    {
        if (!agent.isOnNavMesh)
            return;

        float distance =
            Vector3.Distance(
                transform.position,
                target.position
            );

        if (distance > stopDistance)
        {
            agent.isStopped = false;

            agent.SetDestination(
                target.position
            );

            SetState(EnemyState.Run);
        }
        else
        {
            agent.isStopped = true;

            SetState(EnemyState.Idle);
        }
    }


    protected virtual void UpdateDirection()
    {
        if (target == null)
            return;

        Vector3 toPlayer =
            target.position - transform.position;

        // Не учитываем высоту.
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 0.001f)
            return;

        toPlayer.Normalize();

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        float forwardAngle =
            Vector3.Angle(forward, toPlayer);

        float backAngleFromForward =
            Vector3.Angle(-forward, toPlayer);

        /*
         * FRONT
         *
         * Игрок находится внутри конуса
         * перед мобом.
         */
        if (forwardAngle <= frontAngle)
        {
            SetDirection(
                EnemyDirection.Front
            );

            return;
        }

        /*
         * BACK
         *
         * Игрок находится внутри конуса
         * позади моба.
         */
        if (backAngleFromForward <= backAngle)
        {
            SetDirection(
                EnemyDirection.Back
            );

            return;
        }

        /*
         * LEFT / RIGHT
         *
         * Если игрок не спереди и не сзади,
         * смотрим на какую сторону он смещён.
         */

        float rightDot =
            Vector3.Dot(right, toPlayer);

        if (rightDot > 0f)
        {
            SetDirection(
                EnemyDirection.Right
            );
        }
        else
        {
            SetDirection(
                EnemyDirection.Left
            );
        }
    }


    private void UpdateBillboard()
    {
        if (visual == null)
            return;

        Camera cam = Camera.main;

        if (cam == null)
            return;

        Vector3 direction =
            cam.transform.position -
            visual.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        visual.rotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );
    }


    protected void SetDirection(
        EnemyDirection direction)
    {
        if (currentDirection == direction)
            return;

        currentDirection = direction;

        PlayAnimation();
    }


    protected void SetState(
        EnemyState state)
    {
        if (isDead &&
            state != EnemyState.DieProcess &&
            state != EnemyState.Die)
        {
            return;
        }

        if (currentState == state)
            return;

        currentState = state;

        PlayAnimation();
    }


    private void UpdateAnimation()
    {
        PlayAnimation();
    }


    private void PlayAnimation()
    {
        if (animator == null)
            return;

        string animationName =
            currentDirection.ToString() +
            "_" +
            currentState.ToString();

        if (currentAnimation == animationName)
            return;

        if (!HasAnimation(animationName))
        {
            Debug.LogWarning(
                $"{gameObject.name}: " +
                $"Анимация '{animationName}' " +
                $"не найдена в Animator.",
                gameObject
            );

            return;
        }

        animator.Play(animationName);

        currentAnimation = animationName;
    }


    private bool HasAnimation(
        string animationName)
    {
        if (animator == null)
            return false;

        RuntimeAnimatorController controller =
            animator.runtimeAnimatorController;

        if (controller == null)
            return false;

        foreach (AnimationClip clip
            in controller.animationClips)
        {
            if (clip.name == animationName)
                return true;
        }

        return false;
    }


    public virtual void TakeDamage(float damage)
    {
        if (isDead)
            return;

        if (damage <= 0f)
            return;

        currentHealth -= damage;

        Debug.Log(
            $"{gameObject.name} получил " +
            $"{damage} урона. " +
            $"HP: {currentHealth}/{maxHealth}"
        );

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;

            Die();
        }
    }


    protected virtual void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (agent != null &&
            agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        SetState(
            EnemyState.DieProcess
        );
    }


    public void AnimationDeath()
    {
        if (!isDead)
            return;

        SetState(
            EnemyState.Die
        );
    }


    public void DestroyEnemy()
    {
        Destroy(gameObject);
    }


    public void SetAttackAnimation()
    {
        if (isDead)
            return;

        SetState(
            EnemyState.Attack
        );
    }


    public void SetFallAnimation()
    {
        if (isDead)
            return;

        SetState(
            EnemyState.Fall
        );
    }


    public void SetIdleAnimation()
    {
        if (isDead)
            return;

        SetState(
            EnemyState.Idle
        );
    }


    public void SetRunAnimation()
    {
        if (isDead)
            return;

        SetState(
            EnemyState.Run
        );
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        // Центральное направление
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(origin, forward * 3f);

        // Левая граница Front
        Vector3 leftBoundary =
            Quaternion.AngleAxis(-frontAngle, Vector3.up)
            * forward;

        // Правая граница Front
        Vector3 rightBoundary =
            Quaternion.AngleAxis(frontAngle, Vector3.up)
            * forward;

        Gizmos.color = Color.green;

        Gizmos.DrawRay(
            origin,
            leftBoundary * 3f
        );

        Gizmos.DrawRay(
            origin,
            rightBoundary * 3f
        );

        // Дуга сектора
        int segments = 20;

        Vector3 previousPoint =
            origin + leftBoundary * 3f;

        for (int i = 1; i <= segments; i++)
        {
            float angle =
                Mathf.Lerp(
                    -frontAngle,
                    frontAngle,
                    i / (float)segments
                );

            Vector3 direction =
                Quaternion.AngleAxis(
                    angle,
                    Vector3.up
                ) * forward;

            Vector3 point =
                origin + direction * 3f;

            Gizmos.DrawLine(
                previousPoint,
                point
            );

            previousPoint = point;
        }
    }
}