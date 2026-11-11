using TinyGoose.Tremble;
using UnityEngine;

/// <summary>
/// Шулер — быстрый враг ближнего боя (спрайты Dragger).
/// Бежит зигзагом, с короткого замаха бьёт и делает выпад.
/// </summary>
[PrefabEntity(category: "enemy")]
public class EnemyShuler : EnemyBase
{
    [Header("Shuler Attack")]
    [SerializeField] private float attackRange = 1.9f;
    [SerializeField] private float attackDamage = 12f;
    [SerializeField] private float windupTime = 0.35f;
    [SerializeField] private float attackCooldown = 0.9f;
    [SerializeField] private float zigzag = 2.5f;

    private float windupTimer;
    private float cooldownTimer;
    private bool attacking;
    private float zigzagSeed;

    protected override void Awake()
    {
        base.Awake();
        zigzagSeed = Random.value * 10f;
    }

    protected override void UpdateCombat()
    {
        float dt = Time.deltaTime;

        if (cooldownTimer > 0f)
            cooldownTimer -= dt;

        if (attacking)
        {
            StopMoving();

            windupTimer -= dt;
            SetTelegraph(1f - windupTimer / windupTime);
            SetState(EnemyState.Attack);

            if (windupTimer <= 0f)
            {
                attacking = false;
                SetTelegraph(0f);
                cooldownTimer = attackCooldown;

                // Выпад вперёд + удар
                Shove(FlatDirectionToTarget() * 0.8f);
                TryDamagePlayer(attackRange + 0.8f, attackDamage, 7f);

                SetState(EnemyState.Idle);
            }

            return;
        }

        if (distanceToTarget <= attackRange && cooldownTimer <= 0f && hasLineOfSight)
        {
            attacking = true;
            windupTimer = windupTime;
            return;
        }

        // Бег зигзагом, пока далеко
        Vector3 destination = target.position;

        if (distanceToTarget > 5f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, FlatDirectionToTarget());
            destination += side * Mathf.Sin(Time.time * 2.5f + zigzagSeed) * zigzag;
        }

        MoveTo(destination);
    }
}
