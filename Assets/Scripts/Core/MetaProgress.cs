using UnityEngine;

/// <summary>
/// Прогресс, который переживает смерть (рогалик):
/// фишки душ, купленные улучшения и открытое оружие.
/// Хранится в PlayerPrefs.
/// </summary>
public static class MetaProgress
{
    public enum Upgrade
    {
        Health,
        Damage,
        Deck,
        Joker
    }

    public const int MaxLevel = 5;

    private const string SoulsKey = "sc_souls";
    private const string ChipGunKey = "sc_chipgun";
    private const string DeathsKey = "sc_deaths";

    public static int Souls
    {
        get => PlayerPrefs.GetInt(SoulsKey, 0);
        // На диск пишем в Save() (смерть, победа, покупка, выход), а не на каждую душу
        private set => PlayerPrefs.SetInt(SoulsKey, Mathf.Max(0, value));
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    public static bool ChipGunUnlocked
    {
        get => PlayerPrefs.GetInt(ChipGunKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ChipGunKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static int Deaths
    {
        get => PlayerPrefs.GetInt(DeathsKey, 0);
        set
        {
            PlayerPrefs.SetInt(DeathsKey, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Души за убийства/награды. В забеге они идут в карман (сдаются в кассу после босса),
    /// вне забега — сразу в кассу.
    /// </summary>
    public static void AddSouls(int amount)
    {
        if (amount <= 0)
            return;

        GameState.RunSouls += amount;

        if (RunState.Active)
            RunState.PocketSouls += amount;
        else
            Souls += amount;
    }

    /// <summary>Сдать души в кассу (после босса / конца забега).</summary>
    public static void BankSouls(int amount)
    {
        if (amount <= 0)
            return;

        Souls += amount;
        PlayerPrefs.Save();
    }

    // =========================
    // Потерянные души (остаются на месте смерти)
    // =========================

    public static int LostSouls => PlayerPrefs.GetInt("sc_lost_souls", 0);
    public static int LostChapter => PlayerPrefs.GetInt("sc_lost_ch", 0);
    public static int LostDepth => PlayerPrefs.GetInt("sc_lost_depth", 0);

    public static bool HasLostSoulsAt(int chapter, int depth)
    {
        return LostSouls > 0 && LostChapter == chapter && LostDepth == depth;
    }

    /// <summary>Новая смерть: старые потерянные души сгорают, на их место ложатся новые.</summary>
    public static void SetLostSouls(int amount, int chapter, int depth)
    {
        PlayerPrefs.SetInt("sc_lost_souls", Mathf.Max(0, amount));
        PlayerPrefs.SetInt("sc_lost_ch", chapter);
        PlayerPrefs.SetInt("sc_lost_depth", depth);
        PlayerPrefs.Save();
    }

    public static int TakeLostSouls()
    {
        int amount = LostSouls;
        PlayerPrefs.DeleteKey("sc_lost_souls");
        PlayerPrefs.DeleteKey("sc_lost_ch");
        PlayerPrefs.DeleteKey("sc_lost_depth");
        PlayerPrefs.Save();
        return amount;
    }

    public static int GetLevel(Upgrade upgrade)
    {
        return PlayerPrefs.GetInt("sc_up_" + upgrade, 0);
    }

    public static int GetCost(Upgrade upgrade)
    {
        int level = GetLevel(upgrade);
        return 40 + level * 40;
    }

    public static bool CanBuy(Upgrade upgrade)
    {
        return GetLevel(upgrade) < MaxLevel && Souls >= GetCost(upgrade);
    }

    public static bool TryBuy(Upgrade upgrade)
    {
        if (!CanBuy(upgrade))
            return false;

        Souls -= GetCost(upgrade);
        PlayerPrefs.SetInt("sc_up_" + upgrade, GetLevel(upgrade) + 1);
        PlayerPrefs.Save();
        return true;
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(SoulsKey);
        PlayerPrefs.DeleteKey(ChipGunKey);
        PlayerPrefs.DeleteKey(DeathsKey);
        PlayerPrefs.DeleteKey("sc_lost_souls");
        PlayerPrefs.DeleteKey("sc_lost_ch");
        PlayerPrefs.DeleteKey("sc_lost_depth");

        foreach (Upgrade up in System.Enum.GetValues(typeof(Upgrade)))
            PlayerPrefs.DeleteKey("sc_up_" + up);

        PlayerPrefs.Save();
    }

    // =========================
    // Эффекты улучшений
    // =========================

    public static float BonusHealth => GetLevel(Upgrade.Health) * 20f;

    public static float DamageMultiplier => 1f + GetLevel(Upgrade.Damage) * 0.15f;

    public static int BonusCards => GetLevel(Upgrade.Deck) * 3;

    public static float JokerChargeMultiplier => 1f + GetLevel(Upgrade.Joker) * 0.25f;

    public static string GetTitle(Upgrade upgrade)
    {
        switch (upgrade)
        {
            case Upgrade.Health: return "Крепкая душа";
            case Upgrade.Damage: return "Острые края";
            case Upgrade.Deck: return "Толстая колода";
            case Upgrade.Joker: return "Шулерская рука";
        }

        return upgrade.ToString();
    }

    public static string GetDescription(Upgrade upgrade)
    {
        switch (upgrade)
        {
            case Upgrade.Health: return "+20 к здоровью";
            case Upgrade.Damage: return "+15% урона";
            case Upgrade.Deck: return "+3 карты в колоде";
            case Upgrade.Joker: return "Джокер заряжается на 25% быстрее";
        }

        return "";
    }
}
