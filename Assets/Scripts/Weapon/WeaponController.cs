using UnityEngine;

/// <summary>
/// Основное оружие — колода карт.
/// Бесконечный запас, но после N бросков нужна быстрая перезарядка.
/// Карта в руке видна в HUD; J/Q/K/A имеют особые эффекты (см. Projectile).
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponView weaponView;

    [Header("Weapon")]
    [SerializeField] private WeaponData weapon;

    [Header("Card Deck")]
    [Tooltip("Если пусто — берутся все карты из GameConfig.")]
    [SerializeField] private CardData[] cards;

    [Header("Projectile")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;

    private CardDeck deck;

    private int currentAmmo;
    private bool isReloading;
    private float reloadTimer;
    private float nextFireTime;

    private CardData currentCard;
    private CardData lastThrownCard;

    private PlayerAudio playerAudio;
    private PlayerCamera playerCameraFx;
    private GameObject owner;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => (weapon != null ? weapon.maxAmmo : 15) + MetaProgress.BonusCards;
    public bool IsReloading => isReloading;
    public float ReloadProgress => isReloading && weapon != null && weapon.reloadTime > 0f
        ? 1f - reloadTimer / weapon.reloadTime
        : 1f;
    public WeaponData CurrentWeapon => weapon;
    public CardData CurrentCard => currentCard;
    public CardData NextCard => deck != null ? deck.Peek() : null;

    /// <summary>Карта для определения цвета джокера: в руке, иначе последняя брошенная.</summary>
    public CardData ColorCard => currentCard != null ? currentCard : lastThrownCard;

    public WeaponView View => weaponView;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        PlayerStats stats = GetComponentInParent<PlayerStats>();
        owner = stats != null ? stats.gameObject : gameObject;

        playerAudio = owner.GetComponentInChildren<PlayerAudio>();
        playerCameraFx = owner.GetComponentInChildren<PlayerCamera>();

        if (weaponView == null)
            weaponView = owner.GetComponentInChildren<WeaponView>(true);

        // Колоду создаём первой — без неё стрелять нечем
        CardData[] deckCards = cards;

        if ((deckCards == null || deckCards.Length == 0) && GameConfig.Instance != null)
            deckCards = GameConfig.Instance.cards;

        if (deckCards == null || deckCards.Length == 0)
        {
            Debug.LogWarning(
                "WeaponController: карты не назначены — собираю стандартную колоду из 52 карт в рантайме. " +
                "Запусти Sin City → Setup Prototype, чтобы создать ассеты карт.",
                gameObject
            );

            deckCards = CreateRuntimeDeck();
        }

        deck = new CardDeck(deckCards);

        currentAmmo = MaxAmmo;
        DrawNextCard();
    }

    /// <summary>Заменить колоду (забег: стартовая колода, награды-карты).</summary>
    public void SetDeck(System.Collections.Generic.IEnumerable<CardData> newCards)
    {
        deck = new CardDeck(newCards);
        isReloading = false;
        currentAmmo = MaxAmmo;
        DrawNextCard();
    }

    public int DeckSize => deck != null ? deck.Count : 0;

    private void Start()
    {
        // Вид оружия настраиваем в Start: к этому моменту Awake у WeaponView уже отработал
        if (weaponView != null && weapon != null && isActiveAndEnabled)
            weaponView.SetWeapon(weapon);
    }

    /// <summary>Запасная колода, если ассеты карт ещё не созданы.</summary>
    public static CardData[] CreateRuntimeDeck()
    {
        System.Collections.Generic.List<CardData> list = new System.Collections.Generic.List<CardData>();

        foreach (CardSuit suit in System.Enum.GetValues(typeof(CardSuit)))
        {
            foreach (CardRank rank in System.Enum.GetValues(typeof(CardRank)))
            {
                CardData card = ScriptableObject.CreateInstance<CardData>();
                card.name = $"Runtime_{rank}_{suit}";
                card.suit = suit;
                card.rank = rank;
                card.damageMultiplier = 1.5f;
                card.matchingSuitMultiplier = 2f;
                list.Add(card);
            }
        }

        return list.ToArray();
    }

    private void OnEnable()
    {
        if (weaponView != null)
        {
            weaponView.gameObject.SetActive(true);

            if (weapon != null)
            {
                weaponView.SetWeapon(weapon);

                if (isReloading)
                    weaponView.PlayReload();
            }
        }
    }

    private void OnDisable()
    {
        if (weaponView != null)
            weaponView.gameObject.SetActive(false);
    }

    private void Update()
    {
        UpdateReload();

        if (GameState.InputBlocked || GameState.PlayerDead)
            return;

        if (Input.GetKeyDown(KeyCode.R))
            StartReload();

        if (isReloading)
            return;

        if (Input.GetMouseButton(0))
            TryShoot();

        if (currentAmmo <= 0)
            StartReload();
    }

    // =========================================================
    // CARD
    // =========================================================

    private void DrawNextCard()
    {
        currentCard = deck != null ? deck.Draw() : null;
    }

    // =========================================================
    // RELOAD
    // =========================================================

    public void StartReload()
    {
        if (weapon == null || isReloading)
            return;

        if (currentAmmo >= MaxAmmo)
            return;

        isReloading = true;
        reloadTimer = weapon.reloadTime;

        if (weaponView != null && weaponView.isActiveAndEnabled)
            weaponView.PlayReload();
    }

    private void UpdateReload()
    {
        if (!isReloading)
            return;

        reloadTimer -= Time.deltaTime;

        if (reloadTimer > 0f)
            return;

        isReloading = false;
        currentAmmo = MaxAmmo;

        // Новая рука — новая карта сверху
        if (currentCard == null)
            DrawNextCard();

        if (weaponView != null && weaponView.isActiveAndEnabled)
            weaponView.PlayIdle();
    }

    // =========================================================
    // SHOOT
    // =========================================================

    private void TryShoot()
    {
        if (weapon == null || currentCard == null)
            return;

        if (Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + weapon.fireRate;
        currentAmmo--;

        CardData cardToThrow = currentCard;
        lastThrownCard = cardToThrow;

        Shoot(cardToThrow);

        DrawNextCard();
    }

    private void Shoot(CardData card)
    {
        if (weaponView != null)
            weaponView.PlayShoot();

        if (playerAudio != null)
            playerAudio.PlayShoot();

        Projectile prefab = projectilePrefab != null
            ? projectilePrefab
            : weapon.projectilePrefab;

        if (prefab == null)
        {
            Debug.LogError("Projectile Prefab не назначен!", gameObject);
            return;
        }

        if (projectileSpawnPoint == null || playerCamera == null)
        {
            Debug.LogError("Projectile Spawn Point / Camera не назначены!", gameObject);
            return;
        }

        Vector3 direction = GetAimDirection(projectileSpawnPoint.position, playerCamera, owner);

        Projectile projectile = Instantiate(
            prefab,
            projectileSpawnPoint.position,
            Quaternion.LookRotation(direction)
        );

        projectile.Initialize(
            direction,
            card,
            owner,
            weapon.projectileSpeed,
            MetaProgress.DamageMultiplier
        );

        if (card.IsSpecialCard && playerCameraFx != null)
            playerCameraFx.Shake(0.05f, 0.08f);
    }

    /// <summary>
    /// Луч из центра экрана → направление от точки вылета к точке прицела.
    /// </summary>
    public static Vector3 GetAimDirection(Vector3 from, Camera camera, GameObject owner)
    {
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 targetPoint = ray.origin + ray.direction * 1000f;

        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            if (owner != null && hit.collider.transform.IsChildOf(owner.transform))
                continue;

            if (hit.distance < best)
            {
                best = hit.distance;
                targetPoint = hit.point;
            }
        }

        Vector3 direction = targetPoint - from;

        // Цель слишком близко — летим просто вперёд
        if (direction.sqrMagnitude < 0.25f)
            return ray.direction;

        return direction.normalized;
    }
}
