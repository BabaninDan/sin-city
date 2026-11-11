using TinyGoose.Tremble;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Крупье — стрелок. Держит дистанцию, обходит по кругу,
/// бросает раскалённые фишки с упреждением.
/// </summary>
[PrefabEntity(category: "enemy")]
public class EnemyCroupier : EnemyBase
{
    [Header("Croupier Ranged")]
    [SerializeField] private float minRange = 7f;
    [SerializeField] private float maxRange = 16f;
    [SerializeField] private float fireCooldown = 1.8f;
    [SerializeField] private float windupTime = 0.45f;
    [SerializeField] private float projectileSpeed = 17f;
    [SerializeField] private float projectileDamage = 10f;
    [SerializeField] private int burstCount = 2;

    private float cooldownTimer;
    private float windupTimer;
    private bool aiming;
    private int shotsLeft;

    private float strafeTimer;
    private Vector3 strafePoint;

    protected override void OnActivated()
    {
        cooldownTimer = Random.Range(0.6f, 1.4f);
    }

    protected override void UpdateCombat()
    {
        float dt = Time.deltaTime;

        if (cooldownTimer > 0f)
            cooldownTimer -= dt;

        if (aiming)
        {
            StopMoving();

            windupTimer -= dt;
            SetTelegraph(1f - windupTimer / windupTime);
            SetState(EnemyState.Attack);

            if (windupTimer <= 0f)
            {
                Fire();
                shotsLeft--;

                if (shotsLeft > 0)
                {
                    windupTimer = 0.18f;
                }
                else
                {
                    aiming = false;
                    SetTelegraph(0f);
                    cooldownTimer = fireCooldown * Random.Range(0.8f, 1.2f);
                    SetState(EnemyState.Idle);
                }
            }

            return;
        }

        bool inRange = distanceToTarget >= minRange * 0.7f && distanceToTarget <= maxRange;

        if (hasLineOfSight && inRange && cooldownTimer <= 0f)
        {
            aiming = true;
            windupTimer = windupTime;
            shotsLeft = Random.value < 0.4f ? burstCount : 1;
            return;
        }

        if (distanceToTarget < minRange)
        {
            // Отступаем от игрока
            Vector3 away = transform.position - FlatDirectionToTarget() * 5f;
            MoveTo(SampleOrSelf(away), 1.1f);
        }
        else if (distanceToTarget > maxRange || !hasLineOfSight)
        {
            MoveTo(target.position);
        }
        else
        {
            // Обходим по кругу
            strafeTimer -= dt;

            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(1f, 2f);

                Vector3 side = Vector3.Cross(Vector3.up, FlatDirectionToTarget());
                if (Random.value < 0.5f) side = -side;

                strafePoint = SampleOrSelf(transform.position + side * Random.Range(3f, 6f));
            }

            MoveTo(strafePoint, 0.8f);
        }
    }

    private Vector3 SampleOrSelf(Vector3 point)
    {
        return NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)
            ? hit.position
            : transform.position;
    }

    private void Fire()
    {
        if (player == null)
            return;

        Vector3 from = AimPoint + transform.forward * 0.6f + Vector3.up * 0.3f;

        // Упреждение по скорости игрока
        Vector3 aim = target.position + Vector3.up * 0.6f;
        float time = Vector3.Distance(from, aim) / projectileSpeed;

        if (playerMovement != null)
            aim += playerMovement.HorizontalVelocity * time * 0.6f;

        Color color = Suit.IsRed() ? new Color(1f, 0.25f, 0.2f) : new Color(0.5f, 0.4f, 1f);

        EnemyProjectile.Spawn(from, aim - from, projectileSpeed, projectileDamage, color);
        FX.Sparks(from, color);
    }
}
