using UnityEngine;

/// <summary>
/// Фишка из пулемёта фишек. Рикошетит от стен, по врагам бьёт без учёта масти.
/// Создаётся процедурно (см. Spawn).
/// </summary>
public class ChipProjectile : MonoBehaviour
{
    private static readonly Color[] ChipColors =
    {
        new Color(0.9f, 0.1f, 0.15f),
        new Color(0.1f, 0.35f, 0.9f),
        new Color(0.1f, 0.75f, 0.3f),
        new Color(0.95f, 0.95f, 0.95f),
        new Color(0.1f, 0.1f, 0.1f),
        new Color(1f, 0.8f, 0.2f)
    };

    private Vector3 direction;
    private float speed;
    private float damage;
    private int bouncesLeft;
    private float lifeTimer;
    private GameObject owner;
    private const float Radius = 0.09f;

    public static ChipProjectile Spawn(
        Vector3 position,
        Vector3 direction,
        float speed,
        float damage,
        int bounces,
        GameObject owner)
    {
        Color color = ChipColors[Random.Range(0, ChipColors.Length)];

        GameObject go = FX.Primitive(
            PrimitiveType.Cylinder,
            position,
            new Vector3(0.16f, 0.015f, 0.16f),
            color
        );

        go.name = "Chip";

        ChipProjectile chip = go.AddComponent<ChipProjectile>();
        chip.direction = direction.normalized;
        chip.speed = speed;
        chip.damage = damage;
        chip.bouncesLeft = bounces;
        chip.owner = owner;
        chip.lifeTimer = 3f;

        go.transform.up = chip.direction;

        return chip;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        lifeTimer -= dt;

        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float distance = speed * dt;
        Vector3 origin = transform.position;

        RaycastHit[] hits = Physics.SphereCastAll(origin, Radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (owner != null && hit.collider.transform.IsChildOf(owner.transform))
                continue;

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            Vector3 point = hit.point == Vector3.zero ? origin : hit.point;

            if (damageable != null)
            {
                if (damageable is EnemyBase enemy)
                    enemy.TakeHit(damage, direction, false, point);
                else
                    damageable.TakeDamage(damage);

                Destroy(gameObject);
                return;
            }

            if (bouncesLeft > 0 && hit.normal != Vector3.zero)
            {
                bouncesLeft--;
                direction = Vector3.Reflect(direction, hit.normal).normalized;
                transform.position = point + hit.normal * (Radius + 0.02f);
                FX.Sparks(point, SuitUtil.Gold);
                return;
            }

            FX.Sparks(point, Color.white);
            Destroy(gameObject);
            return;
        }

        transform.position = origin + direction * distance;
        transform.Rotate(Vector3.forward, 1500f * dt, Space.Self);
    }
}
