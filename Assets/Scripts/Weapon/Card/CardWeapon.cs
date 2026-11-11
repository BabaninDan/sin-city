using UnityEngine;

public class CardWeapon : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private WeaponData weaponData;

    [Header("References")]
    [SerializeField] private CardDeck cardDeck;
    [SerializeField] private WeaponView weaponView;

    [SerializeField] private Transform projectileSpawnPoint;

    [Header("Aim")]
    [SerializeField] private Camera playerCamera;

    [SerializeField] private float maxAimDistance = 100f;

    [Header("Reload")]
    [SerializeField] private bool isReloading;

    private float fireTimer;
    private float reloadTimer;

    public CardData CurrentCard
    {
        get
        {
            if (cardDeck == null)
                return null;

            return cardDeck.CurrentCard;
        }
    }

    public bool IsReloading => isReloading;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (cardDeck == null)
            cardDeck = GetComponent<CardDeck>();

        if (weaponView == null)
            weaponView = GetComponentInChildren<WeaponView>();
    }

    private void Start()
    {
        if (weaponView != null)
        {
            weaponView.SetWeapon(weaponData);
        }
    }

    private void Update()
    {
        UpdateFireTimer();
        UpdateReload();

        if (isReloading)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            TryShoot();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            TryReload();
        }
    }

    private void UpdateFireTimer()
    {
        if (fireTimer > 0f)
            fireTimer -= Time.deltaTime;
    }

    private void UpdateReload()
    {
        if (!isReloading)
            return;

        reloadTimer -= Time.deltaTime;

        if (reloadTimer <= 0f)
        {
            FinishReload();
        }
    }

    private void TryShoot()
    {
        if (fireTimer > 0f)
            return;

        if (cardDeck == null)
            return;

        if (!cardDeck.CanThrow())
        {
            TryReload();
            return;
        }

        CardData card =
            cardDeck.ThrowCurrentCard();

        if (card == null)
            return;

        Shoot(card);

        fireTimer = weaponData.fireRate;

        if (weaponView != null)
            weaponView.PlayShoot();

        if (cardDeck.NeedsReload())
        {
            TryReload();
            return;
        }

        cardDeck.DrawNextCard();
    }

    private void Shoot(CardData card)
    {
        if (weaponData.projectilePrefab == null)
        {
            Debug.LogError(
                "CardWeapon: Projectile Prefab не назначен!",
                gameObject
            );

            return;
        }

        if (projectileSpawnPoint == null)
        {
            Debug.LogError(
                "CardWeapon: Projectile Spawn Point не назначен!",
                gameObject
            );

            return;
        }

        Vector3 direction =
            GetAimDirection();

        Projectile projectile =
            Instantiate(
                weaponData.projectilePrefab,
                projectileSpawnPoint.position,
                Quaternion.identity
            );

        projectile.Initialize(
            direction,
            card,
            gameObject,
            weaponData.projectileSpeed
        );
    }

    private Vector3 GetAimDirection()
    {
        if (playerCamera == null)
            return transform.forward;

        Ray ray =
            playerCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

        Vector3 targetPoint;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxAimDistance))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint =
                ray.origin +
                ray.direction * maxAimDistance;
        }

        return (
            targetPoint -
            projectileSpawnPoint.position
        ).normalized;
    }

    private void TryReload()
    {
        if (isReloading)
            return;

        isReloading = true;

        reloadTimer =
            weaponData.reloadTime;

        if (weaponView != null)
            weaponView.PlayReload();
    }

    private void FinishReload()
    {
        isReloading = false;

        if (cardDeck != null)
            cardDeck.ResetDeck();

        if (weaponView != null)
            weaponView.PlayIdle();
    }
}