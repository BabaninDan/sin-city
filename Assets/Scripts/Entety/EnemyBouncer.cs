using TinyGoose.Tremble;
using UnityEngine;

/// <summary>
/// Вышибала — медленный и толстый.
/// Вблизи бьёт по земле (удар по площади), издалека делает таран.
/// После тарана ненадолго открыт для атаки.
/// </summary>
[PrefabEntity(category: "enemy")]
public class EnemyBouncer : EnemyBase
{
    private enum Mode
    {
        Chase,
        SlamWindup,
        ChargeWindup,
        Charging,
        Recover
    }

    [Header("Slam")]
    [SerializeField] private float slamRange = 3.2f;
    [SerializeField] private float slamRadius = 4f;
    [SerializeField] private float slamDamage = 25f;
    [SerializeField] private float slamWindup = 0.65f;
    [SerializeField] private float slamCooldown = 1.6f;

    [Header("Charge")]
    [SerializeField] private float chargeMinRange = 6f;
    [SerializeField] private float chargeMaxRange = 18f;
    [SerializeField] private float chargeWindup = 0.6f;
    [SerializeField] private float chargeSpeed = 16f;
    [SerializeField] private float chargeDuration = 0.9f;
    [SerializeField] private float chargeDamage = 20f;
    [SerializeField] private float chargeCooldown = 5f;

    [Header("Recover")]
    [SerializeField] private float recoverTime = 0.8f;

    private Mode mode = Mode.Chase;
    private float timer;
    private float slamCd;
    private float chargeCd = 2f;
    private Vector3 chargeDirection;

    protected override float AimHeight => 1.4f * transform.localScale.y;

    protected override void UpdateCombat()
    {
        float dt = Time.deltaTime;

        slamCd -= dt;
        chargeCd -= dt;
        timer -= dt;

        switch (mode)
        {
            case Mode.Chase:
                SetTelegraph(0f);

                if (distanceToTarget <= slamRange && slamCd <= 0f)
                {
                    mode = Mode.SlamWindup;
                    timer = slamWindup;
                    StopMoving();
                }
                else if (hasLineOfSight &&
                         distanceToTarget >= chargeMinRange &&
                         distanceToTarget <= chargeMaxRange &&
                         chargeCd <= 0f)
                {
                    mode = Mode.ChargeWindup;
                    timer = chargeWindup;
                    StopMoving();
                }
                else
                {
                    MoveTo(target.position);
                }

                break;

            case Mode.SlamWindup:
                StopMoving();
                SetState(EnemyState.Attack);
                SetTelegraph(1f - timer / slamWindup);

                if (timer <= 0f)
                {
                    Slam();
                    slamCd = slamCooldown;
                    mode = Mode.Recover;
                    timer = recoverTime * 0.6f;
                }

                break;

            case Mode.ChargeWindup:
                StopMoving();
                SetState(EnemyState.Attack);
                SetTelegraph(1f - timer / chargeWindup);
                chargeDirection = FlatDirectionToTarget();

                if (timer <= 0f)
                {
                    mode = Mode.Charging;
                    timer = chargeDuration;
                }

                break;

            case Mode.Charging:
                SetTelegraph(1f);
                SetState(EnemyState.Run);

                Shove(chargeDirection * chargeSpeed * dt);

                if (TryDamagePlayer(1.9f, chargeDamage, 18f, 5f) || timer <= 0f)
                {
                    chargeCd = chargeCooldown;
                    mode = Mode.Recover;
                    timer = recoverTime;
                }

                break;

            case Mode.Recover:
                StopMoving();
                SetTelegraph(0f);
                SetState(EnemyState.Idle);

                if (timer <= 0f)
                    mode = Mode.Chase;

                break;
        }
    }

    protected override void UpdateFacing()
    {
        if (mode == Mode.Charging)
        {
            transform.rotation = Quaternion.LookRotation(chargeDirection, Vector3.up);
            return;
        }

        base.UpdateFacing();
    }

    private void Slam()
    {
        SetTelegraph(0f);

        FX.Ring(transform.position, slamRadius, new Color(1f, 0.6f, 0.2f), 0.4f);
        FX.Burst(transform.position + Vector3.up * 0.2f, new Color(0.6f, 0.5f, 0.4f), 30, 8f, 0.25f, 0.6f, 2f);

        if (player != null)
        {
            PlayerCamera cam = player.GetComponentInChildren<PlayerCamera>();

            if (cam != null)
            {
                float distance = Vector3.Distance(transform.position, target.position);
                cam.Shake(Mathf.Lerp(0.35f, 0.08f, distance / 15f), 0.3f);
            }
        }

        TryDamagePlayer(slamRadius, slamDamage, 12f, 7f);
    }
}
