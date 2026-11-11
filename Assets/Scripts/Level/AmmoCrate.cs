using TinyGoose.Tremble;
using UnityEngine;

/// <summary>
/// Ящик с фишками — разбивается выстрелом.
/// Внутри патроны для пулемёта фишек и иногда здоровье.
/// TrenchBroom: prop_AmmoCrate.
/// </summary>
[PrefabEntity(category: "prop")]
public class AmmoCrate : MonoBehaviour, IDamageable
{
    [SerializeField] private float health = 15f;
    [SerializeField] private int chipAmmo = 30;
    [SerializeField, Range(0f, 1f)] private float healthChance = 0.35f;
    [SerializeField] private int souls = 3;

    private bool broken;
    private float shake;
    private Vector3 basePosition;

    private void Start()
    {
        basePosition = transform.position;
    }

    public void TakeDamage(float damage)
    {
        if (broken)
            return;

        health -= damage;
        shake = 0.15f;

        FX.Burst(transform.position + Vector3.up * 0.5f, new Color(0.55f, 0.35f, 0.15f), 6, 3f, 0.12f, 0.4f, 2f);

        if (health <= 0f)
            Break();
    }

    private void Update()
    {
        if (shake <= 0f)
            return;

        shake -= Time.deltaTime;
        transform.position = basePosition + Random.insideUnitSphere * 0.05f;

        if (shake <= 0f)
            transform.position = basePosition;
    }

    private void Break()
    {
        broken = true;

        Vector3 center = transform.position + Vector3.up * 0.5f;

        FX.Burst(center, new Color(0.55f, 0.35f, 0.15f), 30, 7f, 0.25f, 0.8f, 2.5f);
        FX.Burst(center, SuitUtil.Gold, 10, 5f, 0.1f, 0.5f, 1f);

        Pickup.Spawn(Pickup.Kind.ChipAmmo, center, chipAmmo);

        if (Random.value < healthChance)
            Pickup.Spawn(Pickup.Kind.Health, center, 25);

        if (souls > 0)
            Pickup.Spawn(Pickup.Kind.Soul, center, souls);

        Destroy(gameObject);
    }
}
