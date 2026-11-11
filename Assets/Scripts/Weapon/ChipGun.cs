using UnityEngine;

/// <summary>
/// Пулемёт фишек — второе оружие с ограниченным боезапасом.
/// Быстрая стрельба, фишки рикошетят от стен.
/// Патроны: ящики и дроп с крупье/вышибал.
/// </summary>
public class ChipGun : MonoBehaviour
{
    [Header("Fire")]
    [SerializeField] private float fireRate = 0.075f;
    [SerializeField] private float damage = 6f;
    [SerializeField] private float projectileSpeed = 45f;
    [SerializeField] private int bounces = 2;
    [SerializeField] private float spread = 1.6f;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 200;
    [SerializeField] private int startAmmo = 60;

    private int ammo;
    private float nextFireTime;

    private Camera playerCamera;
    private Transform spawnPoint;
    private GameObject owner;
    private PlayerCamera cameraFx;
    private PlayerAudio playerAudio;
    private ChipGunView view;

    public int Ammo => ammo;
    public int MaxAmmo => maxAmmo;

    public void Setup(Camera camera, Transform spawnPoint)
    {
        playerCamera = camera;
        this.spawnPoint = spawnPoint;

        if (view == null && camera != null)
            view = ChipGunView.Create(camera.transform);

        if (view != null)
            view.gameObject.SetActive(enabled);
    }

    private void Awake()
    {
        ammo = startAmmo;

        PlayerStats stats = GetComponentInParent<PlayerStats>();
        owner = stats != null ? stats.gameObject : gameObject;

        cameraFx = owner.GetComponentInChildren<PlayerCamera>();
        playerAudio = owner.GetComponentInChildren<PlayerAudio>();
    }

    private void OnEnable()
    {
        if (view != null)
            view.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (view != null)
            view.gameObject.SetActive(false);
    }

    public void AddAmmo(int amount)
    {
        ammo = Mathf.Clamp(ammo + amount, 0, maxAmmo);
    }

    private void Update()
    {
        if (GameState.InputBlocked || GameState.PlayerDead)
            return;

        if (!Input.GetMouseButton(0))
            return;

        if (Time.time < nextFireTime)
            return;

        if (ammo <= 0)
        {
            if (Input.GetMouseButtonDown(0))
                GameState.Say("Нет фишек!", new Color(1f, 0.6f, 0.2f));

            return;
        }

        nextFireTime = Time.time + fireRate;
        ammo--;

        Fire();
    }

    private void Fire()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        Vector3 from = spawnPoint != null
            ? spawnPoint.position
            : playerCamera.transform.position + playerCamera.transform.forward * 0.6f;

        if (view != null)
            from = view.Muzzle.position;

        Vector3 dir = WeaponController.GetAimDirection(from, playerCamera, owner);

        // Разброс
        dir = Quaternion.Euler(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            0f
        ) * dir;

        ChipProjectile.Spawn(
            from,
            dir,
            projectileSpeed,
            damage * MetaProgress.DamageMultiplier,
            bounces,
            owner
        );

        if (view != null)
            view.Kick();

        if (cameraFx != null)
            cameraFx.Shake(0.025f, 0.05f);

        if (playerAudio != null)
            playerAudio.PlayShoot();
    }
}
