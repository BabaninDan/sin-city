using System;
using System.Collections.Generic;
using TinyGoose.Tremble;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Выбор масти врага в TrenchBroom / инспекторе.</summary>
public enum EnemySuitChoice
{
    Random,
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

/// <summary>
/// Базовый враг: спрайт-билборд с направлениями (Front/Back/Left/Right),
/// NavMesh-движение, масть, урон, оглушение, дроп.
/// Конкретное поведение — в наследниках (EnemyShuler, EnemyCroupier, EnemyBouncer).
/// </summary>
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

    /// <summary>Все живые враги на сцене.</summary>
    public static readonly List<EnemyBase> All = new List<EnemyBase>();

    [Header("Arena")]
    [Tooltip("ID арены (как у arena_trigger). Пусто — враг сам по себе.")]
    [SerializeField] protected string arena = "";

    [Header("Suit")]
    [SerializeField] protected EnemySuitChoice suit = EnemySuitChoice.Random;

    [Header("Movement")]
    [SerializeField] protected float moveSpeed = 4f;
    [SerializeField] protected float stopDistance = 1.5f;

    [Header("Health")]
    [SerializeField] protected float maxHealth = 100f;

    [Header("Detection")]
    [SerializeField] private float viewDistance = 25f;

    [Header("Drops")]
    [SerializeField] protected int souls = 5;
    [SerializeField, Range(0f, 1f)] protected float ammoDropChance = 0f;
    [SerializeField] protected int ammoDropAmount = 20;
    [SerializeField, Range(0f, 1f)] protected float healthDropChance = 0.1f;

    [Header("Hit reaction")]
    [SerializeField] protected float hitStunTime = 0.12f;
    [SerializeField] protected float knockbackForce = 4f;

    [Header("Target")]
    [SerializeField, NoTremble] protected Transform target;

    [Header("Visual")]
    [SerializeField, NoTremble] private Transform visual;
    [SerializeField, NoTremble] private Animator animator;
    [SerializeField, NoTremble] private SpriteRenderer spriteRenderer;
    [SerializeField, NoTremble] private Color tint = Color.white;

    [Header("Direction")]
    [Tooltip("Угол, в пределах которого игрок считается находящимся спереди.")]
    [SerializeField] private float frontAngle = 45f;

    [Tooltip("Угол, в пределах которого игрок считается находящимся сзади.")]
    [SerializeField] private float backAngle = 45f;

    protected NavMeshAgent agent;
    protected PlayerStats player;
    protected PlayerMovement playerMovement;

    protected float currentHealth;
    protected EnemyDirection currentDirection;
    protected EnemyState currentState;

    protected float distanceToTarget;
    protected bool hasLineOfSight;

    private CardSuit resolvedSuit;
    private bool hasSeenTarget;
    private bool isDead;
    private string currentAnimation;
    private HashSet<string> clipNames;

    private float stunTimer;
    private float flashTimer;
    private float telegraph;
    private float repathTimer;
    private float losTimer;
    private float spawnTimer;
    private Vector3 visualBaseScale = Vector3.one;
    private Vector3 knockbackVelocity;

    private SuitMarker marker;

    public float Health => currentHealth;
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public bool HasTarget => target != null;
    public bool IsDead => isDead;
    public bool IsActive => hasSeenTarget;
    public CardSuit Suit => resolvedSuit;
    public string Arena => arena;
    public EnemyDirection CurrentDirection => currentDirection;
    public EnemyState CurrentState => currentState;

    /// <summary>Точка прицеливания (центр тела).</summary>
    public Vector3 AimPoint => transform.position + Vector3.up * AimHeight;

    protected virtual float AimHeight => 1.1f * transform.localScale.y;

    public event Action<EnemyBase> Died;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        currentHealth = maxHealth;

        agent.speed = moveSpeed;
        agent.stoppingDistance = stopDistance;
        agent.acceleration = Mathf.Max(agent.acceleration, moveSpeed * 6f);
        agent.angularSpeed = 720f;

        // Навигация не вращает моба — поворачиваем сами
        agent.updateRotation = false;

        resolvedSuit = suit == EnemySuitChoice.Random
            ? SuitUtil.RandomSuit()
            : (CardSuit)((int)suit - 1);

        if (visual == null)
        {
            Transform foundVisual = transform.Find("Visual");

            if (foundVisual != null)
                visual = foundVisual;
        }

        if (visual != null)
        {
            if (animator == null)
                animator = visual.GetComponent<Animator>();

            if (spriteRenderer == null)
                spriteRenderer = visual.GetComponent<SpriteRenderer>();

            visualBaseScale = visual.localScale;
        }

        CacheClips();

        currentDirection = EnemyDirection.Front;
        currentState = EnemyState.Idle;

        PlayAnimation();

        marker = SuitMarker.Create(this);

        spawnTimer = 0.35f;
    }

    protected virtual void OnEnable()
    {
        All.Add(this);
    }

    protected virtual void OnDisable()
    {
        All.Remove(this);
    }

    protected virtual void Update()
    {
        UpdateVisualFx();

        if (isDead)
            return;

        if (!EnsureAgent())
            return;

        if (target == null || player == null)
        {
            FindTarget();

            if (target == null)
            {
                SetState(EnemyState.Idle);
                return;
            }
        }

        distanceToTarget = Vector3.Distance(transform.position, target.position);

        losTimer -= Time.deltaTime;
        if (losTimer <= 0f)
        {
            losTimer = 0.15f;
            hasLineOfSight = IsVisibleFrom(target.position + Vector3.up * 0.5f);
        }

        // Пока моб ещё не увидел игрока
        if (!hasSeenTarget)
        {
            if (CanSeeTarget())
                Activate();
            else
            {
                SetState(EnemyState.Idle);
                return;
            }
        }

        if (player != null && player.IsDead)
        {
            StopMoving();
            SetState(EnemyState.Idle);
            return;
        }

        ApplyKnockback();

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            StopMoving();
        }
        else
        {
            UpdateCombat();
        }

        UpdateFacing();
        UpdateDirection();
        UpdateMovementAnimation();
    }

    protected virtual void LateUpdate()
    {
        UpdateBillboard();
    }

    // =========================================================
    // NAVMESH
    // =========================================================

    /// <summary>Агент включается только когда NavMesh построен.</summary>
    private bool EnsureAgent()
    {
        if (agent.enabled && agent.isOnNavMesh)
            return true;

        if (!GameManager.NavMeshReady && GameManager.Instance != null)
            return false;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            agent.enabled = true;
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        return false;
    }

    protected void MoveTo(Vector3 destination, float speedMultiplier = 1f)
    {
        if (!agent.isOnNavMesh)
            return;

        agent.speed = moveSpeed * speedMultiplier;
        agent.isStopped = false;

        repathTimer -= Time.deltaTime;

        if (repathTimer <= 0f || agent.destination == Vector3.zero)
        {
            repathTimer = 0.2f;
            agent.SetDestination(destination);
        }
    }

    protected void StopMoving()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    /// <summary>Сдвиг без поиска пути (рывок, отталкивание).</summary>
    protected void Shove(Vector3 offset)
    {
        if (agent.isOnNavMesh)
            agent.Move(offset);
    }

    private void ApplyKnockback()
    {
        if (knockbackVelocity.sqrMagnitude < 0.01f)
            return;

        Shove(knockbackVelocity * Time.deltaTime);
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 30f * Time.deltaTime);
    }

    // =========================================================
    // TARGET
    // =========================================================

    private void FindTarget()
    {
        PlayerStats stats = GameManager.Instance != null
            ? GameManager.Instance.Player
            : FindAnyObjectByType<PlayerStats>();

        if (stats == null)
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");

            if (tagged != null)
                stats = tagged.GetComponent<PlayerStats>();
        }

        if (stats == null)
            return;

        player = stats;
        target = stats.transform;
        playerMovement = stats.GetComponent<PlayerMovement>();
    }

    private bool CanSeeTarget()
    {
        if (target == null)
            return false;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;

        if (distance > viewDistance)
            return false;

        if (!hasLineOfSight)
            return false;

        // Вплотную — замечает в любом случае
        if (distance < 8f)
            return true;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        return Vector3.Angle(forward, toTarget) <= frontAngle;
    }

    /// <summary>Нет ли стены между точкой и врагом.</summary>
    public bool IsVisibleFrom(Vector3 from)
    {
        Vector3 to = AimPoint;
        Vector3 dir = to - from;
        float dist = dir.magnitude;

        if (dist < 0.01f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(from, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.collider.transform;

            if (t.IsChildOf(transform))
                continue;

            // Сквозь других врагов, игрока и пикапы видно
            if (hit.collider.GetComponentInParent<EnemyBase>() != null)
                continue;

            if (hit.collider.GetComponentInParent<PlayerStats>() != null)
                continue;

            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic)
                continue;

            return false;
        }

        return true;
    }

    /// <summary>Разбудить (старт арены, попадание, увидел игрока).</summary>
    public void Activate()
    {
        if (hasSeenTarget)
            return;

        hasSeenTarget = true;
        OnActivated();
    }

    protected virtual void OnActivated()
    {
    }

    // =========================================================
    // COMBAT (переопределяется наследниками)
    // =========================================================

    protected virtual void UpdateCombat()
    {
        if (distanceToTarget > stopDistance)
            MoveTo(target.position);
        else
            StopMoving();
    }

    /// <summary>Ударить игрока, если он в радиусе.</summary>
    protected bool TryDamagePlayer(float range, float damage, float knockback, float upForce = 2f)
    {
        if (player == null || player.IsDead)
            return false;

        Vector3 flat = target.position - transform.position;
        flat.y = 0f;

        if (flat.magnitude > range)
            return false;

        Vector3 dir = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;

        player.TakeDamage(damage, dir);

        if (playerMovement != null)
            playerMovement.AddImpulse(dir * knockback + Vector3.up * upForce);

        return true;
    }

    /// <summary>Подсветка перед атакой (0..1).</summary>
    protected void SetTelegraph(float amount)
    {
        telegraph = Mathf.Clamp01(amount);
    }

    protected Vector3 FlatDirectionToTarget()
    {
        Vector3 d = target.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.001f ? d.normalized : transform.forward;
    }

    // =========================================================
    // FACING / DIRECTION / ANIMATION
    // =========================================================

    /// <summary>Куда смотрит враг: при движении — по ходу, иначе — на игрока.</summary>
    protected virtual void UpdateFacing()
    {
        Vector3 look = agent.isOnNavMesh ? agent.velocity : Vector3.zero;
        look.y = 0f;

        if (look.sqrMagnitude < 0.5f && target != null)
            look = FlatDirectionToTarget();

        if (look.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 540f * Time.deltaTime);
    }

    protected virtual void UpdateDirection()
    {
        Camera cam = Camera.main;

        if (cam == null)
            return;

        // Направление спрайта считаем относительно камеры игрока
        Vector3 toPlayer = cam.transform.position - transform.position;
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

        float forwardAngle = Vector3.Angle(forward, toPlayer);
        float backAngleFromForward = Vector3.Angle(-forward, toPlayer);

        if (forwardAngle <= frontAngle)
        {
            SetDirection(EnemyDirection.Front);
            return;
        }

        if (backAngleFromForward <= backAngle)
        {
            SetDirection(EnemyDirection.Back);
            return;
        }

        float rightDot = Vector3.Dot(right, toPlayer);

        SetDirection(rightDot > 0f ? EnemyDirection.Right : EnemyDirection.Left);
    }

    private void UpdateMovementAnimation()
    {
        if (currentState == EnemyState.Attack)
            return;

        bool moving = agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.2f;
        SetState(moving ? EnemyState.Run : EnemyState.Idle);
    }

    private void UpdateBillboard()
    {
        if (visual == null)
            return;

        Camera cam = Camera.main;

        if (cam == null)
            return;

        Vector3 direction = cam.transform.position - visual.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        visual.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    protected void SetDirection(EnemyDirection direction)
    {
        if (currentDirection == direction)
            return;

        currentDirection = direction;

        PlayAnimation();
    }

    protected void SetState(EnemyState state)
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

    private void CacheClips()
    {
        clipNames = new HashSet<string>();

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            clipNames.Add(clip.name);
    }

    /// <summary>
    /// Ищет анимацию "Направление_Состояние".
    /// Если её нет — пробует Idle этого направления, затем Front_Idle.
    /// (Так враги работают, даже если нарисованы не все анимации.)
    /// </summary>
    private void PlayAnimation()
    {
        if (animator == null || clipNames == null || clipNames.Count == 0)
            return;

        string dir = currentDirection.ToString();

        // Спины пока нет — показываем лицо
        if (currentDirection == EnemyDirection.Back && !clipNames.Contains("Back_Idle"))
            dir = EnemyDirection.Front.ToString();

        string animationName = dir + "_" + currentState;

        if (!clipNames.Contains(animationName))
            animationName = dir + "_Idle";

        if (!clipNames.Contains(animationName))
            animationName = "Front_Idle";

        if (!clipNames.Contains(animationName) || currentAnimation == animationName)
            return;

        animator.Play(animationName);
        currentAnimation = animationName;
    }

    // =========================================================
    // VISUAL FX (вспышка при попадании, замах, появление)
    // =========================================================

    private void UpdateVisualFx()
    {
        if (visual == null)
            return;

        float scale = 1f;

        if (spawnTimer > 0f)
        {
            spawnTimer -= Time.deltaTime;
            scale = 1f - Mathf.Clamp01(spawnTimer / 0.35f);
            scale = 1f - (1f - scale) * (1f - scale);
        }

        // Пульсация при замахе
        scale *= 1f + telegraph * 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.time * 40f));

        // Сплющивание от удара
        float squash = flashTimer > 0f ? flashTimer / 0.1f : 0f;

        visual.localScale = new Vector3(
            visualBaseScale.x * scale * (1f + squash * 0.12f),
            visualBaseScale.y * scale * (1f - squash * 0.1f),
            visualBaseScale.z
        );

        if (spriteRenderer != null)
        {
            Color color = tint;

            if (telegraph > 0f)
                color = Color.Lerp(color, new Color(1f, 0.85f, 0.3f), telegraph * 0.6f);

            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                color = Color.Lerp(color, new Color(1f, 0.2f, 0.2f), 0.8f);
            }

            if (isDead)
                color.a = 0f;

            spriteRenderer.color = color;
        }
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    public virtual void TakeDamage(float damage)
    {
        TakeHit(damage, Vector3.zero, false, AimPoint);
    }

    /// <summary>
    /// Попадание: урон, отбрасывание, оглушение, эффекты.
    /// </summary>
    public virtual void TakeHit(float damage, Vector3 direction, bool suitMatch, Vector3 point, bool fromJoker = false)
    {
        if (isDead || damage <= 0f)
            return;

        Activate();

        currentHealth -= damage;

        flashTimer = 0.1f;
        stunTimer = Mathf.Max(stunTimer, hitStunTime * (suitMatch ? 1.5f : 1f));

        Vector3 flat = direction;
        flat.y = 0f;

        if (flat.sqrMagnitude > 0.001f)
            knockbackVelocity += flat.normalized * knockbackForce * (suitMatch ? 1.5f : 1f);

        FX.Blood(point, suitMatch ? 1.4f : 0.8f);

        if (suitMatch)
            FX.Sparks(point, resolvedSuit.WorldColor());

        if (marker != null)
            marker.Pulse(suitMatch);

        HitFeedback.Report(point, damage, suitMatch, currentHealth <= 0f);
        GameState.RaiseEnemyHit(this, damage, suitMatch);

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

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        SetState(EnemyState.DieProcess);

        // Жестокость
        FX.Blood(AimPoint, 3f);
        FX.Blood(transform.position + Vector3.up * 0.3f, 2f);
        FX.Burst(AimPoint, resolvedSuit.WorldColor(), 12, 5f, 0.2f, 0.7f, 1f);

        DropLoot();

        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (marker != null)
            Destroy(marker.gameObject);

        GameState.RaiseEnemyKilled(this);
        Died?.Invoke(this);

        // Анимаций смерти пока нет — исчезаем сразу
        Destroy(gameObject, 0.05f);
    }

    protected virtual void DropLoot()
    {
        Vector3 pos = transform.position + Vector3.up * 0.8f;

        int soulCount = Mathf.Max(0, souls);

        // Несколько фишек душ разлетаются в стороны
        int pieces = Mathf.Clamp(soulCount / 3, 1, 6);
        int perPiece = Mathf.Max(1, soulCount / pieces);

        for (int i = 0; i < pieces && soulCount > 0; i++)
        {
            int amount = i == pieces - 1 ? soulCount : perPiece;
            soulCount -= amount;
            Pickup.Spawn(Pickup.Kind.Soul, pos, amount);
        }

        if (UnityEngine.Random.value < ammoDropChance)
            Pickup.Spawn(Pickup.Kind.ChipAmmo, pos, ammoDropAmount);

        if (UnityEngine.Random.value < healthDropChance)
            Pickup.Spawn(Pickup.Kind.Health, pos, 20);
    }

    // =========================================================
    // ANIMATION EVENTS (для будущих анимаций смерти/атаки)
    // =========================================================

    public void AnimationDeath()
    {
        if (!isDead)
            return;

        SetState(EnemyState.Die);
    }

    public void DestroyEnemy()
    {
        Destroy(gameObject);
    }

    public void SetAttackAnimation()
    {
        if (isDead)
            return;

        SetState(EnemyState.Attack);
    }

    public void SetFallAnimation()
    {
        if (isDead)
            return;

        SetState(EnemyState.Fall);
    }

    public void SetIdleAnimation()
    {
        if (isDead)
            return;

        SetState(EnemyState.Idle);
    }

    public void SetRunAnimation()
    {
        if (isDead)
            return;

        SetState(EnemyState.Run);
    }

    /// <summary>Настройка из EnemySpawn.</summary>
    public void Configure(string arenaId, EnemySuitChoice suitChoice)
    {
        arena = arenaId;

        if (suitChoice != EnemySuitChoice.Random)
            resolvedSuit = (CardSuit)((int)suitChoice - 1);

        if (marker != null)
            marker.Refresh();
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

        // Границы Front
        Vector3 leftBoundary = Quaternion.AngleAxis(-frontAngle, Vector3.up) * forward;
        Vector3 rightBoundary = Quaternion.AngleAxis(frontAngle, Vector3.up) * forward;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(origin, leftBoundary * 3f);
        Gizmos.DrawRay(origin, rightBoundary * 3f);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, viewDistance);
    }
}
