using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Брошенная карта.
/// 2–10: урон = номинал (x2 по врагу своей масти).
/// J — пробивает насквозь, Q — самонаводится,
/// K — взрывается, A — рикошетит между врагами.
///
/// Движение через SphereCast, чтобы быстрые карты не пролетали сквозь врагов.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 30f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float radius = 0.15f;

    [Header("Behaviour")]
    [SerializeField] private bool destroyOnHit = true;

    [Header("Visual")]
    [SerializeField] private float spinSpeed = 900f;

    [Header("Special cards")]
    [SerializeField] private int jackPierceCount = 4;
    [SerializeField] private float queenTurnSpeed = 10f;
    [SerializeField] private float queenSeekRange = 35f;
    [SerializeField] private float kingExplosionRadius = 3.5f;
    [SerializeField] private int aceChainCount = 3;
    [SerializeField] private float aceChainRange = 14f;

    private Vector3 direction;
    private float lifeTimer;
    private GameObject owner;
    private CardData card;
    private float damageMultiplier = 1f;

    private int pierceLeft;
    private int chainsLeft;
    private EnemyBase homingTarget;
    private bool redirected;

    private readonly HashSet<Object> alreadyHit = new HashSet<Object>();

    private Transform visual;

    public void Initialize(
        Vector3 direction,
        CardData card,
        GameObject owner,
        float speed = -1f,
        float damageMultiplier = 1f)
    {
        this.direction = direction.normalized;
        this.card = card;
        this.owner = owner;
        this.damageMultiplier = damageMultiplier;

        if (speed > 0f)
            this.speed = speed;

        lifeTimer = lifetime;

        transform.forward = this.direction;

        // Коллайдер карты нужен только как форма — попадания считаем сами
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        visual = transform;

        if (card != null)
        {
            switch (card.rank)
            {
                case CardRank.Jack:
                    pierceLeft = jackPierceCount;
                    break;

                case CardRank.Ace:
                    chainsLeft = aceChainCount;
                    break;
            }

            Tint();
        }
    }

    private void Tint()
    {
        Color color = card.suit.IsRed()
            ? new Color(1f, 0.75f, 0.75f)
            : new Color(0.7f, 0.72f, 0.8f);

        if (card.IsSpecialCard)
            color = Color.Lerp(color, SuitUtil.Gold, 0.6f);

        foreach (Renderer r in GetComponentsInChildren<MeshRenderer>())
            FX.SetColor(r, color);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        lifeTimer -= dt;

        if (lifeTimer <= 0f)
        {
            Finish(transform.position, false);
            return;
        }

        if (card != null && card.rank == CardRank.Queen)
            UpdateHoming(dt);

        Move(speed * dt);

        // Вращение карты в полёте
        if (visual != null && this != null)
            visual.Rotate(Vector3.up, spinSpeed * dt, Space.Self);
    }

    // =========================================================
    // MOVE / HIT
    // =========================================================

    private void Move(float distance)
    {
        Vector3 origin = transform.position;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            radius,
            direction,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (IsOwner(hit.collider))
                continue;

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                if (alreadyHit.Contains(damageable as Object))
                    continue;

                Vector3 point = hit.point == Vector3.zero ? hit.collider.bounds.center : hit.point;

                bool keepFlying = HitTarget(damageable, hit.collider, point);

                if (!keepFlying)
                    return;

                // Туз сменил направление — продолжим со следующего кадра
                if (redirected)
                {
                    redirected = false;
                    return;
                }

                continue;
            }

            // Стена / пол
            Vector3 wallPoint = hit.point == Vector3.zero ? origin : hit.point;
            FX.Sparks(wallPoint, Color.white);
            Finish(wallPoint, true);
            return;
        }

        transform.position = origin + direction * distance;
        transform.forward = direction;
    }

    /// <summary>Нанести урон. Возвращает true, если карта летит дальше.</summary>
    private bool HitTarget(IDamageable damageable, Collider collider, Vector3 point)
    {
        alreadyHit.Add(damageable as Object);

        ApplyDamage(damageable, collider, point, 1f);

        // Король — взрыв при попадании
        if (card != null && card.rank == CardRank.King)
        {
            Explode(point);
            Destroy(gameObject);
            return false;
        }

        // Валет — летит насквозь
        if (pierceLeft > 0)
        {
            pierceLeft--;
            return true;
        }

        // Туз — рикошет к следующему врагу
        if (chainsLeft > 0)
        {
            EnemyBase next = FindClosestEnemy(point, aceChainRange, true);

            if (next != null)
            {
                chainsLeft--;

                Vector3 to = next.AimPoint - point;
                FX.Beam(point, next.AimPoint, SuitUtil.Gold, 0.06f, 0.15f);

                transform.position = point;
                direction = to.normalized;
                lifeTimer = lifetime;
                redirected = true;
                return true;
            }
        }

        if (destroyOnHit)
        {
            Destroy(gameObject);
            return false;
        }

        return true;
    }

    private void ApplyDamage(IDamageable damageable, Collider collider, Vector3 point, float scale)
    {
        float damage = card != null ? card.GetBaseDamage() : 5f;

        if (damageable is EnemyBase enemy)
        {
            if (card != null)
                damage = card.GetDamage(enemy.Suit);

            damage *= damageMultiplier * scale;

            bool match = card != null && card.suit == enemy.Suit;
            enemy.TakeHit(damage, direction, match, point);
        }
        else
        {
            damageable.TakeDamage(damage * damageMultiplier * scale);
        }
    }

    private void Explode(Vector3 point)
    {
        FX.Burst(point, SuitUtil.Gold, 30, 9f, 0.2f, 0.5f, 0.3f);
        FX.Burst(point, new Color(1f, 0.35f, 0.1f), 20, 6f, 0.25f, 0.4f, 0f);
        FX.Ring(point, kingExplosionRadius, SuitUtil.Gold);

        PlayerCamera cam = owner != null ? owner.GetComponentInChildren<PlayerCamera>() : null;
        if (cam != null)
            cam.Shake(0.2f, 0.25f);

        Collider[] cols = Physics.OverlapSphere(point, kingExplosionRadius, ~0, QueryTriggerInteraction.Ignore);
        HashSet<IDamageable> done = new HashSet<IDamageable>();

        foreach (Collider col in cols)
        {
            if (IsOwner(col))
                continue;

            IDamageable d = col.GetComponentInParent<IDamageable>();

            if (d == null || done.Contains(d) || alreadyHit.Contains(d as Object))
                continue;

            done.Add(d);
            ApplyDamage(d, col, col.bounds.center, 0.75f);
        }
    }

    private void Finish(Vector3 point, bool hitWall)
    {
        if (hitWall && card != null && card.rank == CardRank.King)
            Explode(point);

        Destroy(gameObject);
    }

    private bool IsOwner(Collider col)
    {
        return owner != null && col.transform.IsChildOf(owner.transform);
    }

    // =========================================================
    // HOMING (Дама)
    // =========================================================

    private void UpdateHoming(float dt)
    {
        if (homingTarget == null || homingTarget.IsDead)
            homingTarget = FindTargetInCone();

        if (homingTarget == null)
            return;

        Vector3 to = (homingTarget.AimPoint - transform.position).normalized;
        direction = Vector3.RotateTowards(direction, to, queenTurnSpeed * dt, 0f).normalized;
    }

    private EnemyBase FindTargetInCone()
    {
        EnemyBase best = null;
        float bestScore = float.MaxValue;

        foreach (EnemyBase enemy in EnemyBase.All)
        {
            if (enemy.IsDead || alreadyHit.Contains(enemy))
                continue;

            Vector3 to = enemy.AimPoint - transform.position;
            float dist = to.magnitude;

            if (dist > queenSeekRange)
                continue;

            float angle = Vector3.Angle(direction, to);

            if (angle > 70f)
                continue;

            float score = dist + angle * 0.3f;

            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }

    private EnemyBase FindClosestEnemy(Vector3 point, float range, bool requireSight)
    {
        EnemyBase best = null;
        float bestDist = range;

        foreach (EnemyBase enemy in EnemyBase.All)
        {
            if (enemy.IsDead || alreadyHit.Contains(enemy))
                continue;

            float dist = Vector3.Distance(point, enemy.AimPoint);

            if (dist >= bestDist)
                continue;

            if (requireSight && !enemy.IsVisibleFrom(point))
                continue;

            bestDist = dist;
            best = enemy;
        }

        return best;
    }
}
