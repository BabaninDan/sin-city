using UnityEngine;

[CreateAssetMenu(
    fileName = "NewWeapon",
    menuName = "Game/Weapon"
)]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    public string weaponName;

    [Tooltip("Спрайт оружия в руке (если не задан — берётся из Animator).")]
    public Sprite weaponSprite;

    [Tooltip("Иконка оружия для HUD.")]
    public Sprite idleSprite;

    [Header("Damage")]
    [Tooltip("Базовый урон для оружия без карт.")]
    public float damage = 10f;

    [Header("Ammo")]
    [Tooltip("Для колоды — сколько карт можно бросить до перезарядки.")]
    public int maxAmmo = 15;

    [Header("Projectile")]
    public Projectile projectilePrefab;
    public float projectileSpeed = 30f;

    [Header("Fire")]
    [Tooltip("Задержка между выстрелами в секундах.")]
    public float fireRate = 0.2f;

    [Header("Reload")]
    public float reloadTime = 1f;

    [Header("Animation")]
    public string drawAnimation = "Draw";
    public string idleAnimation = "Idle";
    public string shootAnimation = "Shoot";
    public string reloadAnimation = "Reload";
    public string interactAnimation = "Interact";

    // Совместимость со старым кодом колоды
    public int maxCards => maxAmmo;
}
