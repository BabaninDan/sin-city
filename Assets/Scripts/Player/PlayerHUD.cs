using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Player HUD")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text armorText;
    [SerializeField] private Slider staminaSlider;

    [Header("Weapon")]
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private TMP_Text maxAmmoText;
    [SerializeField] private TMP_Text currentAmmoText;
    [SerializeField] private UnityEngine.UI.Image weaponImage;

    private void Update()
    {
        UpdatePlayerStats();
        UpdateWeapon();
    }

    private void UpdatePlayerStats()
    {
        if (playerStats == null)
            return;

        // HP
        if (hpText != null)
        {
            hpText.text =
                Mathf.CeilToInt(playerStats.Health).ToString();
        }

        // Броня / щит
        if (armorText != null)
        {
            armorText.text =
                Mathf.CeilToInt(playerStats.Shield).ToString();
        }

        // Стамина
        if (staminaSlider != null)
        {
            staminaSlider.maxValue =
                playerStats.MaxStamina;

            staminaSlider.value =
                playerStats.Stamina;
        }
    }

    private void UpdateWeapon()
    {
        if (weaponController == null)
            return;

        WeaponData weapon =
            weaponController.CurrentWeapon;

        if (weapon == null)
            return;

        // Максимальный боезапас
        if (maxAmmoText != null)
        {
            maxAmmoText.text =
                weaponController.MaxAmmo.ToString();
        }

        // Текущий боезапас
        if (currentAmmoText != null)
        {
            currentAmmoText.text =
                weaponController.CurrentAmmo.ToString();
        }

        // Картинка оружия
        if (weaponImage != null)
        {
            weaponImage.sprite =
                weapon.idleSprite;
        }
    }
}