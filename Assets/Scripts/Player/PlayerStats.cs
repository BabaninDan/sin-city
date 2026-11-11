using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Shield")]
    [SerializeField] private float maxShield = 25f;

    [Tooltip("Максимум брони, который можно набрать сверх maxShield (чёрный джокер).")]
    [SerializeField] private float shieldCap = 100f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegen = 20f;
    [SerializeField] private float staminaRegenDelay = 0.5f;

    [Header("Damage")]
    [Tooltip("Неуязвимость после удара, чтобы толпа не убивала за кадр.")]
    [SerializeField] private float invulnerabilityTime = 0.15f;

    private float health;
    private float shield;
    private float stamina;

    private float staminaRegenTimer;
    private float invulnerableTimer;
    private float baseMaxHealth;
    private bool isDead;

    public float Health => health;
    public float MaxHealth => maxHealth;

    public float Shield => shield;
    public float MaxShield => Mathf.Max(maxShield, shield);

    public float Stamina => stamina;
    public float MaxStamina => maxStamina;

    public bool IsDead => isDead;

    /// <summary>Урон получен: (урон, направление удара).</summary>
    public event Action<float, Vector3> Damaged;

    public event Action Died;

    private void Awake()
    {
        baseMaxHealth = maxHealth;

        health = maxHealth;
        shield = maxShield;
        stamina = maxStamina;
    }

    private void Update()
    {
        RegenerateStamina();

        if (invulnerableTimer > 0f)
            invulnerableTimer -= Time.deltaTime;
    }

    /// <summary>Применить постоянные улучшения (вызывается при старте забега).</summary>
    public void ApplyUpgrades(float bonusHealth)
    {
        maxHealth = baseMaxHealth + bonusHealth;
        health = maxHealth;
    }

    // =========================
    // DAMAGE
    // =========================

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, Vector3.zero);
    }

    public void TakeDamage(float damage, Vector3 direction)
    {
        if (isDead || damage <= 0f)
            return;

        if (invulnerableTimer > 0f)
            return;

        invulnerableTimer = invulnerabilityTime;

        float total = damage;

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

        Damaged?.Invoke(total, direction);

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
        if (isDead || amount <= 0f)
            return;

        health += amount;
        health = Mathf.Clamp(health, 0f, maxHealth);
    }

    public void RestoreShield(float amount)
    {
        if (amount <= 0f)
            return;

        // Обычное восстановление — до maxShield (лишнюю броню от джокера не срезаем)
        shield = Mathf.Max(shield, Mathf.Min(shield + amount, maxShield));
    }

    /// <summary>Броня сверх обычного максимума (до shieldCap).</summary>
    public void AddShield(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        shield = Mathf.Min(shield + amount, shieldCap);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        Debug.Log("Игрок погиб");

        Died?.Invoke();
    }
}
