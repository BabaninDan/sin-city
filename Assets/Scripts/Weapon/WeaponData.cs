using UnityEngine;

[CreateAssetMenu(
    fileName = "NewWeapon",
    menuName = "Game/Weapon"
)]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    public string weaponName;

    public Sprite weaponSprite;

    [Header("Ammo")]
    public int maxAmmo = 15;

    [Header("Projectile")]
    public float projectileSpeed = 30f;

    [Header("Fire")]
    public float fireRate = 0.2f;

    [Header("Reload")]
    public float reloadTime = 1f;

    [Header("Animation")]
    public string drawAnimation = "Draw";
    public string idleAnimation = "Idle";
    public string shootAnimation = "Shoot";
    public string reloadAnimation = "Reload";
}