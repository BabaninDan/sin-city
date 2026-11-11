using UnityEngine;

/// <summary>
/// Переключение оружия: 1 — колода, 2 — пулемёт фишек (если открыт),
/// колесо мыши / Q — следующее оружие.
/// </summary>
public class PlayerArsenal : MonoBehaviour
{
    public enum Slot
    {
        Cards = 0,
        Chips = 1
    }

    [SerializeField] private float switchDelay = 0.15f;

    private WeaponController cards;
    private ChipGun chips;

    private Slot current = Slot.Cards;
    private float switchTimer;

    public Slot Current => current;
    public WeaponController Cards => cards;
    public ChipGun Chips => chips;
    public bool ChipGunUnlocked => MetaProgress.ChipGunUnlocked && chips != null;
    public bool IsSwitching => switchTimer > 0f;

    public void Setup(WeaponController cards, ChipGun chips)
    {
        this.cards = cards;
        this.chips = chips;

        Select(Slot.Cards, true);
    }

    private void Update()
    {
        if (switchTimer > 0f)
        {
            switchTimer -= Time.deltaTime;

            if (switchTimer <= 0f)
                EnableCurrent();
        }

        if (GameState.InputBlocked || GameState.PlayerDead)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            Select(Slot.Cards);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            Select(Slot.Chips);

        float wheel = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(wheel) > 0.01f || Input.GetKeyDown(KeyCode.Q))
            Select(current == Slot.Cards ? Slot.Chips : Slot.Cards);
    }

    public void Select(Slot slot, bool instant = false)
    {
        if (slot == Slot.Chips && !ChipGunUnlocked)
        {
            if (!instant)
                GameState.Say("Пулемёт фишек ещё не найден", new Color(0.8f, 0.8f, 0.8f));

            return;
        }

        if (slot == current && !instant)
            return;

        current = slot;

        // Пока идёт смена — оба оружия убраны
        if (cards != null) cards.enabled = false;
        if (chips != null) chips.enabled = false;

        if (instant)
        {
            switchTimer = 0f;
            EnableCurrent();
        }
        else
        {
            switchTimer = switchDelay;
        }
    }

    private void EnableCurrent()
    {
        if (cards != null) cards.enabled = current == Slot.Cards;
        if (chips != null) chips.enabled = current == Slot.Chips;
    }

    /// <summary>Подобрали пулемёт — открыть навсегда и сразу взять в руки.</summary>
    public void UnlockChipGun(int bonusAmmo)
    {
        bool wasUnlocked = MetaProgress.ChipGunUnlocked;

        MetaProgress.ChipGunUnlocked = true;

        if (chips != null)
            chips.AddAmmo(bonusAmmo);

        if (!wasUnlocked)
        {
            GameState.Say("ПУЛЕМЁТ ФИШЕК!  [2] / колесо мыши", SuitUtil.Gold);
            Select(Slot.Chips);
        }
        else
        {
            GameState.Say($"+{bonusAmmo} фишек", SuitUtil.Gold);
        }
    }

    public void AddChipAmmo(int amount)
    {
        if (chips != null)
            chips.AddAmmo(amount);
    }
}
