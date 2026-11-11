using UnityEngine;
using System.Collections;

public class WeaponController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponView weaponView;

    [Header("Weapon")]
    [SerializeField] private WeaponData weapon;

    [Header("Card Deck")]
    [SerializeField] private CardData[] cards;

    [Header("Projectile")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;

    private int currentAmmo;
    private bool isReloading;

    private CardData currentCard;

    private float nextFireTime;

    public int CurrentAmmo => currentAmmo;
    public bool IsReloading => isReloading;
    public WeaponData CurrentWeapon => weapon;

    public CardData CurrentCard => currentCard;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (weaponView != null && weapon != null)
            weaponView.SetWeapon(weapon);

        if (weapon != null)
            currentAmmo = weapon.maxAmmo;

        DrawNextCard();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartReload();
        }

        if (isReloading)
            return;

        if (Input.GetButton("Fire1"))
        {
            TryShoot();
        }

        if (currentAmmo <= 0)
        {
            StartReload();
        }
    }

    // =========================================================
    // CARD
    // =========================================================

    private void DrawNextCard()
    {
        if (cards == null || cards.Length == 0)
        {
            Debug.LogError(
                "WeaponController: Массив cards пуст!",
                gameObject
            );

            currentCard = null;
            return;
        }

        currentCard =
            cards[Random.Range(0, cards.Length)];

        Debug.Log(
            $"Выпала карта: {currentCard.GetShortName()}"
        );
    }

    // =========================================================
    // RELOAD
    // =========================================================

    private void StartReload()
    {
        if (weapon == null)
            return;

        if (isReloading)
            return;

        if (currentAmmo >= weapon.maxAmmo)
            return;

        StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        isReloading = true;

        if (weaponView != null)
            weaponView.PlayReload();

        yield return new WaitForSeconds(
            weapon.reloadTime
        );

        currentAmmo = weapon.maxAmmo;

        DrawNextCard();

        isReloading = false;

        if (weaponView != null)
            weaponView.PlayIdle();
    }

    // =========================================================
    // SHOOT
    // =========================================================

    private void TryShoot()
    {
        if (weapon == null)
            return;

        if (currentCard == null)
            return;

        if (Time.time < nextFireTime)
            return;

        nextFireTime =
            Time.time + weapon.fireRate;

        currentAmmo--;

        CardData cardToThrow = currentCard;

        Shoot(cardToThrow);

        // После броска достаём новую карту
        DrawNextCard();
    }

    private void Shoot(CardData card)
    {
        if (weaponView != null)
            weaponView.PlayShoot();

        if (projectilePrefab == null)
        {
            Debug.LogError(
                "Projectile Prefab не назначен!",
                gameObject
            );

            return;
        }

        if (projectileSpawnPoint == null)
        {
            Debug.LogError(
                "Projectile Spawn Point не назначен!",
                gameObject
            );

            return;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "Player Camera не назначена!",
                gameObject
            );

            return;
        }

        // =====================================================
        // 1. Луч из центра экрана
        // =====================================================

        Ray cameraRay =
            playerCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

        Vector3 targetPoint;

        if (Physics.Raycast(
            cameraRay,
            out RaycastHit hit,
            1000f))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint =
                cameraRay.origin +
                cameraRay.direction * 1000f;
        }

        // =====================================================
        // 2. Направление от руки к цели
        // =====================================================

        Vector3 direction =
            (
                targetPoint -
                projectileSpawnPoint.position
            ).normalized;

        // =====================================================
        // 3. Создаём карту
        // =====================================================

        Projectile projectile =
            Instantiate(
                projectilePrefab,
                projectileSpawnPoint.position,
                Quaternion.LookRotation(direction)
            );

        // =====================================================
        // 4. Передаём Projectile саму карту
        // =====================================================

        projectile.Initialize(
            direction,
            card,
            gameObject,
            weapon.projectileSpeed
        );
    }
}