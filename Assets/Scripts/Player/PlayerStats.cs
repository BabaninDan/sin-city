using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Shield")]
    [SerializeField] private float maxShield = 3f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegen = 20f;
    [SerializeField] private float staminaRegenDelay = 0.5f;

    private float health;
    private float shield;
    private float stamina;

    private float staminaRegenTimer;

    public float Health => health;
    public float MaxHealth => maxHealth;

    public float Shield => shield;
    public float MaxShield => maxShield;

    public float Stamina => stamina;
    public float MaxStamina => maxStamina;

    private void Awake()
    {
        health = maxHealth;
        shield = maxShield;
        stamina = maxStamina;
    }

    private void Update()
    {
        RegenerateStamina();
    }

    // =========================
    // DAMAGE
    // =========================

    public void TakeDamage(float damage)
    {
        if (damage <= 0f)
            return;

        // Сначала урон принимает щит
        if (shield > 0f)
        {
            float shieldDamage = Mathf.Min(shield, damage);

            shield -= shieldDamage;
            damage -= shieldDamage;
        }

        // Остаток урона идёт в HP
        if (damage > 0f)
        {
            health -= damage;
        }

        if (health <= 0f)
        {
            health = 0f;
            Die();
        }
    }

    // =========================
    // STAMINA
    // =========================

    public bool TryUseStamina(float amount)
    {
        if (amount <= 0f)
            return true;

        if (stamina < amount)
            return false;

        stamina -= amount;

        staminaRegenTimer = staminaRegenDelay;

        return true;
    }

    private void RegenerateStamina()
    {
        if (stamina >= maxStamina)
            return;

        if (staminaRegenTimer > 0f)
        {
            staminaRegenTimer -= Time.deltaTime;
            return;
        }

        stamina += staminaRegen * Time.deltaTime;
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
    }

    // =========================
    // HEAL / SHIELD
    // =========================

    public void Heal(float amount)
    {
        if (amount <= 0f)
            return;

        health += amount;
        health = Mathf.Clamp(health, 0f, maxHealth);
    }

    public void RestoreShield(float amount)
    {
        if (amount <= 0f)
            return;

        shield += amount;
        shield = Mathf.Clamp(shield, 0f, maxShield);
    }

    private void Die()
    {
        Debug.Log("Игрок погиб");

        // Здесь позже сделаем смерть / рестарт
    }
}