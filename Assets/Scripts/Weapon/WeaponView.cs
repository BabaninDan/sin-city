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
        spriteRenderer =
            GetComponent<SpriteRenderer>();

        animator =
            GetComponent<Animator>();
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

        currentWeapon = weapon;

        if (weapon.weaponSprite != null)
        {
            spriteRenderer.sprite =
                weapon.weaponSprite;
        }

        PlayIdle();
    }

    public void PlayDraw()
    {
        if (currentWeapon == null)
            return;

        animator.Play(
            currentWeapon.drawAnimation
        );
    }

    public void PlayIdle()
    {
        if (currentWeapon == null)
            return;

        animator.Play(
            currentWeapon.idleAnimation
        );
    }

    public void PlayShoot()
    {
        if (currentWeapon == null)
            return;

        animator.Play(
            currentWeapon.shootAnimation
        );
    }

    public void PlayReload()
    {
        if (currentWeapon == null)
            return;

        animator.Play(
            currentWeapon.reloadAnimation
        );
    }
}