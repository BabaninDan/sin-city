using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 30f;
    [SerializeField] private float lifetime = 5f;

    [Header("Behaviour")]
    [SerializeField] private bool destroyOnHit = true;

    private Vector3 direction;
    private float lifeTimer;

    private GameObject owner;

    private CardData card;

    public void Initialize(
        Vector3 direction,
        CardData card,
        GameObject owner,
        float speed = -1f)
    {
        this.direction = direction.normalized;
        this.card = card;
        this.owner = owner;

        if (speed > 0f)
            this.speed = speed;

        lifeTimer = lifetime;

        transform.forward = this.direction;

        IgnoreOwnerCollisions();
    }

    private void Update()
    {
        transform.position +=
            direction * speed * Time.deltaTime;

        lifeTimer -= Time.deltaTime;

        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == owner)
            return;

        IDamageable damageable =
            other.GetComponentInParent<IDamageable>();

        if (damageable == null)
        {
            if (destroyOnHit)
                Destroy(gameObject);

            return;
        }

        float damage = GetDamage(other);

        damageable.TakeDamage(damage);

        if (destroyOnHit)
            Destroy(gameObject);
    }

    private float GetDamage(Collider target)
    {
        if (card == null)
            return 0f;

        EnemySuit enemySuit =
            target.GetComponentInParent<EnemySuit>();

        if (enemySuit == null)
        {
            return card.GetBaseDamage();
        }

        return card.GetDamage(enemySuit.Suit);
    }

    private void IgnoreOwnerCollisions()
    {
        if (owner == null)
            return;

        Collider projectileCollider =
            GetComponent<Collider>();

        Collider[] ownerColliders =
            owner.GetComponentsInChildren<Collider>();

        foreach (Collider ownerCollider in ownerColliders)
        {
            Physics.IgnoreCollision(
                projectileCollider,
                ownerCollider
            );
        }
    }
}