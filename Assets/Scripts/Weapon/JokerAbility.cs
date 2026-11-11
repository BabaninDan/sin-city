using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Джокер (F). Шкала копится за убийства и попадания в масть.
/// Цвет джокера = цвет карты в руке:
///   красный — бьёт всех ♥♦ врагов в поле зрения и лечит,
///   чёрный  — бьёт всех ♣♠ врагов в поле зрения и даёт броню.
/// </summary>
public class JokerAbility : MonoBehaviour
{
    [Header("Charge")]
    [SerializeField] private float maxCharge = 100f;
    [SerializeField] private float chargePerKill = 12f;
    [SerializeField] private float chargePerSuitHit = 4f;
    [SerializeField] private float chargePerHit = 0.5f;

    [Header("Effect")]
    [SerializeField] private float damage = 500f;
    [SerializeField] private float range = 60f;
    [SerializeField] private float redHeal = 40f;
    [SerializeField] private float blackShield = 50f;

    [Header("Slow-mo")]
    [SerializeField] private float slowMoScale = 0.25f;
    [SerializeField] private float slowMoDuration = 0.35f;

    private float charge;

    private PlayerStats stats;
    private PlayerArsenal arsenal;
    private PlayerCamera cameraFx;
    private Camera cam;

    public float Charge01 => charge / maxCharge;
    public bool IsReady => charge >= maxCharge;

    /// <summary>Какой джокер выйдет, если нажать F сейчас.</summary>
    public bool NextIsRed
    {
        get
        {
            CardData card = arsenal != null && arsenal.Cards != null ? arsenal.Cards.ColorCard : null;
            return card == null || card.IsRed;
        }
    }

    /// <summary>Вызывается, когда джокер сработал: (красный?, сколько целей).</summary>
    public event System.Action<bool, int> Used;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        arsenal = GetComponent<PlayerArsenal>();
        cameraFx = GetComponentInChildren<PlayerCamera>();
        cam = GetComponentInChildren<Camera>();
    }

    private void OnEnable()
    {
        GameState.EnemyKilled += OnEnemyKilled;
        GameState.EnemyHit += OnEnemyHit;
    }

    private void OnDisable()
    {
        GameState.EnemyKilled -= OnEnemyKilled;
        GameState.EnemyHit -= OnEnemyHit;
    }

    private void OnEnemyKilled(EnemyBase enemy)
    {
        AddCharge(chargePerKill);
    }

    private void OnEnemyHit(EnemyBase enemy, float dmg, bool suitMatch)
    {
        AddCharge(suitMatch ? chargePerSuitHit : chargePerHit);
    }

    public void AddCharge(float amount)
    {
        bool wasReady = IsReady;

        charge = Mathf.Min(maxCharge, charge + amount * MetaProgress.JokerChargeMultiplier);

        if (!wasReady && IsReady)
            GameState.Say("ДЖОКЕР ГОТОВ  [F]", SuitUtil.Gold);
    }

    private void Update()
    {
        if (arsenal == null)
            arsenal = GetComponent<PlayerArsenal>();

        if (GameState.InputBlocked || GameState.PlayerDead)
            return;

        if (Input.GetKeyDown(KeyCode.F))
            TryUse();
    }

    private void TryUse()
    {
        if (!IsReady)
        {
            GameState.Say("Джокер не заряжен", new Color(0.7f, 0.7f, 0.7f));
            return;
        }

        bool red = NextIsRed;
        List<EnemyBase> targets = FindTargets(red);

        if (targets.Count == 0)
        {
            GameState.Say(red ? "Нет целей ♥♦ в поле зрения" : "Нет целей ♣♠ в поле зрения",
                red ? SuitUtil.Red : new Color(0.75f, 0.75f, 0.85f));
            return;
        }

        charge = 0f;
        StartCoroutine(Execute(red, targets));
    }

    private List<EnemyBase> FindTargets(bool red)
    {
        List<EnemyBase> result = new List<EnemyBase>();

        if (cam == null)
            cam = Camera.main;

        Vector3 eye = cam.transform.position;

        foreach (EnemyBase enemy in EnemyBase.All)
        {
            if (enemy.IsDead || enemy.Suit.IsRed() != red)
                continue;

            Vector3 aim = enemy.AimPoint;

            if (Vector3.Distance(eye, aim) > range)
                continue;

            Vector3 vp = cam.WorldToViewportPoint(aim);

            if (vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f)
                continue;

            if (!enemy.IsVisibleFrom(eye))
                continue;

            result.Add(enemy);
        }

        return result;
    }

    private IEnumerator Execute(bool red, List<EnemyBase> targets)
    {
        Color color = red ? SuitUtil.Red : new Color(0.15f, 0.15f, 0.2f);
        Color beamColor = red ? new Color(1f, 0.2f, 0.25f) : new Color(0.6f, 0.65f, 1f);

        Used?.Invoke(red, targets.Count);

        GameState.Say(red ? "КРАСНЫЙ ДЖОКЕР" : "ЧЁРНЫЙ ДЖОКЕР", red ? SuitUtil.Red : new Color(0.8f, 0.85f, 1f));

        // Азартное замедление
        Time.timeScale = slowMoScale;

        if (cameraFx != null)
            cameraFx.Shake(0.35f, 0.4f);

        Vector3 from = cam.transform.position + cam.transform.forward * 0.5f - cam.transform.up * 0.2f;

        foreach (EnemyBase enemy in targets)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            Vector3 aim = enemy.AimPoint;

            FX.Beam(from, aim, beamColor, 0.25f, 0.5f);
            FX.Burst(aim, SuitUtil.Gold, 25, 8f, 0.2f, 0.6f, 0.2f);
            FX.Burst(aim, color, 20, 6f, 0.25f, 0.6f, 0.5f);

            enemy.TakeHit(damage, (aim - from).normalized, true, aim, true);

            yield return new WaitForSecondsRealtime(0.05f);
        }

        if (stats != null)
        {
            if (red)
                stats.Heal(redHeal);
            else
                stats.AddShield(blackShield);
        }

        yield return new WaitForSecondsRealtime(slowMoDuration);

        if (!GameState.PlayerDead)
            Time.timeScale = 1f;
    }
}
