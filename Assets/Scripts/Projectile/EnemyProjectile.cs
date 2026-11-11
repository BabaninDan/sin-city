using UnityEngine;

/// <summary>
/// Снаряд врага (крупье бросает раскалённые фишки).
/// Пролетает сквозь врагов, бьёт только игрока.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float damage;
    private float lifeTimer = 5f;
    private const float Radius = 0.25f;

    public static EnemyProjectile Spawn(Vector3 position, Vector3 direction, float speed, float damage, Color color)
    {
        GameObject go = FX.Primitive(
            PrimitiveType.Sphere,
            position,
            Vector3.one * 0.35f,
            color
        );

        go.name = "EnemyProjectile";

        // Светящееся ядро
        FX.Primitive(
            PrimitiveType.Sphere,
            position,
            Vector3.one * 0.6f,
            Color.white,
            false,
            go.transform
        ).transform.localScale = Vector3.one * 0.55f;

        EnemyProjectile p = go.AddComponent<EnemyProjectile>();
        p.direction = direction.normalized;
        p.speed = speed;
        p.damage = damage;

        return p;
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
            // Сквозь врагов, ящики и пикапы
            if (hit.collider.GetComponentInParent<EnemyBase>() != null)
                continue;

            if (hit.collider.GetComponentInParent<AmmoCrate>() != null)
                continue;

            Vector3 point = hit.point == Vector3.zero ? origin : hit.point;

            PlayerStats player = hit.collider.GetComponentInParent<PlayerStats>();

            if (player != null)
                player.TakeDamage(damage, direction);

            FX.Sparks(point, new Color(1f, 0.5f, 0.2f));
            Destroy(gameObject);
            return;
        }

        transform.position = origin + direction * distance;
    }
}
