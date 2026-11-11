using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class WeaponView : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private WeaponData currentWeapon;

    private void Awake()
    {
        CacheComponents();
    }

    /// <summary>
    /// Компоненты берём лениво: другие скрипты могут вызвать SetWeapon
    /// раньше, чем отработает Awake этого объекта.
    /// </summary>
    private void CacheComponents()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void SetWeapon(WeaponData weapon)
    {
        if (weapon == null)
        {
            Debug.LogError(
                "WeaponView: WeaponData не назначен!",
                gameObject
            );

            return;
        }

        CacheComponents();

        currentWeapon = weapon;

        if (weapon.weaponSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite =
                weapon.weaponSprite;
        }

        PlayIdle();
    }

    public void PlayDraw()
    {
        Play(currentWeapon != null ? currentWeapon.drawAnimation : null);
    }

    public void PlayIdle()
    {
        Play(currentWeapon != null ? currentWeapon.idleAnimation : null);
    }

    public void PlayShoot()
    {
        Play(currentWeapon != null ? currentWeapon.shootAnimation : null);
    }

    public void PlayReload()
    {
        Play(currentWeapon != null ? currentWeapon.reloadAnimation : null);
    }

    private void Play(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
            return;

        CacheComponents();

        // Animator на выключенном объекте не проигрывает анимации
        if (animator == null || !animator.isActiveAndEnabled)
            return;

        animator.Play(animationName, 0, 0f);
    }
}
