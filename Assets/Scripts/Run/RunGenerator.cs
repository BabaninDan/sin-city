using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Генерация забега (см. диздок, раздел «Процедурная генерация»):
///  - какие двери предложить после зачистки;
///  - из какого шаблона собрать комнату;
///  - кого, где и в какой волне поставить.
/// Всё детерминировано от сида забега + глава + глубина.
/// </summary>
public static class RunGenerator
{
    // Цена врагов в бюджете встречи
    private const float ShulerCost = 1f;
    private const float CroupierCost = 1.5f;
    private const float BouncerCost = 3f;

    // =========================================================
    // DOORS
    // =========================================================

    /// <summary>Двери, ведущие на глубину nextDepth текущей главы.</summary>
    public static List<DoorOffer> GenerateOffers(int chapter, int nextDepth)
    {
        System.Random rng = RunRng.Get(RunState.Seed, chapter, nextDepth, 1);
        List<DoorOffer> offers = new List<DoorOffer>();

        // Последняя глубина — только босс
        if (nextDepth >= RunState.DepthsPerChapter)
        {
            offers.Add(new DoorOffer(RoomKind.Boss, RewardKind.Souls, 40 + 20 * chapter));
            return offers;
        }

        // Первая комната главы — всегда обычный бой
        if (nextDepth <= 1)
        {
            offers.Add(RollFight(RoomKind.Combat, chapter, rng));
            return offers;
        }

        int count = rng.Value() < 0.45f ? 3 : 2;

        // Обязательные двери
        if (MetaProgress.HasLostSoulsAt(chapter, nextDepth))
            offers.Add(new DoorOffer(RoomKind.SoulRetrieval, RewardKind.Souls, MetaProgress.LostSouls));

        bool shopDue =
            !RunState.ShopVisitedThisChapter &&
            (nextDepth == 4 || (nextDepth == 3 && rng.Value() < 0.6f));

        if (shopDue)
            offers.Add(new DoorOffer(RoomKind.Shop));

        if (nextDepth == RunState.DepthsPerChapter - 1)
            offers.Add(new DoorOffer(RoomKind.Bar, RewardKind.Heal, 40));

        // Остальные — случайно из пула
        int guard = 0;

        while (offers.Count < count && guard++ < 20)
        {
            float roll = rng.Value();
            DoorOffer offer;

            if (nextDepth >= 3 && roll < 0.3f)
                offer = RollFight(RoomKind.Elite, chapter, rng);
            else if (nextDepth >= 2 && roll < 0.42f)
                offer = new DoorOffer(RoomKind.Roulette);
            else
                offer = RollFight(RoomKind.Combat, chapter, rng);

            if (!Contains(offers, offer))
                offers.Add(offer);
        }

        // Хотя бы одна боевая дверь — иначе нечем зарабатывать
        bool hasFight = false;
        foreach (DoorOffer o in offers)
            hasFight |= o.kind == RoomKind.Combat || o.kind == RoomKind.Elite;

        if (!hasFight)
            offers[offers.Count - 1] = RollFight(RoomKind.Combat, chapter, rng);

        // Порядок дверей тоже случайный
        for (int i = offers.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (offers[i], offers[j]) = (offers[j], offers[i]);
        }

        return offers;
    }

    private static DoorOffer RollFight(RoomKind kind, int chapter, System.Random rng)
    {
        float roll = rng.Value();
        int mult = kind == RoomKind.Elite ? 2 : 1;

        if (roll < 0.5f)
            return new DoorOffer(kind, RewardKind.Card, mult);

        if (roll < 0.8f)
            return new DoorOffer(kind, RewardKind.Tokens, (30 + 10 * chapter) * mult);

        return new DoorOffer(kind, RewardKind.Souls, (10 + 5 * chapter) * mult);
    }

    private static bool Contains(List<DoorOffer> list, DoorOffer offer)
    {
        foreach (DoorOffer o in list)
        {
            if (o.kind == offer.kind && o.reward == offer.reward)
                return true;
        }

        return false;
    }

    // =========================================================
    // TEMPLATES
    // =========================================================

    private static readonly Queue<GameObject> recentTemplates = new Queue<GameObject>();

    public static GameObject PickTemplate(RoomKind kind, int chapter, System.Random rng)
    {
        GameConfig config = GameConfig.Instance;

        if (config == null || config.roomTemplates == null || config.roomTemplates.Length == 0)
            return null;

        string tag = kind == RoomKind.Boss
            ? "boss"
            : (kind == RoomKind.Shop || kind == RoomKind.Bar || kind == RoomKind.Roulette) ? "small" : "combat";

        List<GameObject> matching = new List<GameObject>();
        List<GameObject> fresh = new List<GameObject>();

        foreach (GameObject template in config.roomTemplates)
        {
            if (template == null)
                continue;

            RoomInfo info = template.GetComponentInChildren<RoomInfo>(true);

            bool ok = info == null
                ? tag == "combat"
                : info.HasKind(tag) && info.InChapter(chapter);

            if (!ok)
                continue;

            matching.Add(template);

            if (!recentTemplates.Contains(template))
                fresh.Add(template);
        }

        // Нет подходящего — любой боевой
        if (matching.Count == 0)
        {
            foreach (GameObject template in config.roomTemplates)
            {
                if (template != null)
                    matching.Add(template);
            }

            fresh.AddRange(matching);
        }

        if (matching.Count == 0)
            return null;

        GameObject picked = rng.Pick(fresh.Count > 0 ? fresh : matching);

        // Не повторяем последние 2 шаблона
        recentTemplates.Enqueue(picked);
        while (recentTemplates.Count > 2)
            recentTemplates.Dequeue();

        return picked;
    }

    public static void ResetHistory()
    {
        recentTemplates.Clear();
    }

    // =========================================================
    // ENCOUNTER
    // =========================================================

    /// <summary>B = 4 + 2·d + 4·(c − 1); элита ×1.5, босс ×2.2.</summary>
    public static float Budget(int chapter, int depth, RoomKind kind)
    {
        float budget = 4f + 2f * depth + 4f * (chapter - 1);

        if (kind == RoomKind.Elite) budget *= 1.5f;
        if (kind == RoomKind.Boss) budget *= 2.2f;

        return budget;
    }

    public static CardSuit RollRoomSuit(System.Random rng)
    {
        return (CardSuit)rng.Next(0, 4);
    }

    /// <summary>
    /// Собрать встречу: делим бюджет на волны и раскладываем врагов по точкам спавна,
    /// подходящим по роли. Половина врагов — «масти зала».
    /// </summary>
    public static List<SpawnOrder> BuildEncounter(
        int chapter,
        int depth,
        RoomKind kind,
        List<EnemySpawn> slots,
        CardSuit roomSuit,
        System.Random rng)
    {
        List<SpawnOrder> orders = new List<SpawnOrder>();

        if (slots == null || slots.Count == 0)
            return orders;

        float budget = Budget(chapter, depth, kind);
        int waves = budget < 8f ? 1 : budget < 14f ? 2 : 3;

        // Вес типов растёт с главой: больше крупье и вышибал
        float wShuler = 0.6f - 0.1f * (chapter - 1);
        float wCroupier = 0.28f + 0.05f * (chapter - 1);

        List<EnemyType> picks = new List<EnemyType>();

        // Босс-заглушка и элита: гарантированный вышибала
        if (kind == RoomKind.Boss)
        {
            picks.Add(EnemyType.Bouncer);
            picks.Add(EnemyType.Bouncer);
            budget -= BouncerCost * 2f;
        }
        else if (kind == RoomKind.Elite)
        {
            picks.Add(EnemyType.Bouncer);
            budget -= BouncerCost;
        }

        int guard = 0;

        while (budget >= ShulerCost && guard++ < 64)
        {
            float roll = rng.Value();
            EnemyType type;

            if (roll < wShuler)
                type = EnemyType.Shuler;
            else if (roll < wShuler + wCroupier)
                type = EnemyType.Croupier;
            else
                type = EnemyType.Bouncer;

            float cost = Cost(type);

            if (cost > budget)
            {
                type = EnemyType.Shuler;
                cost = ShulerCost;
            }

            picks.Add(type);
            budget -= cost;
        }

        // Тяжёлых — в последние волны
        picks.Sort((a, b) => Cost(a).CompareTo(Cost(b)));

        Dictionary<EnemySpawn, int> slotUse = new Dictionary<EnemySpawn, int>();

        for (int i = 0; i < picks.Count; i++)
        {
            EnemyType type = picks[i];
            int wave = Mathf.Min(waves, 1 + i * waves / Mathf.Max(1, picks.Count));

            EnemySpawn slot = PickSlot(slots, type, slotUse, rng);
            slotUse[slot] = slotUse.TryGetValue(slot, out int used) ? used + 1 : 1;

            // Несколько врагов на одной точке — разносим по кругу
            Vector3 offset = Quaternion.Euler(0f, rng.Range(0, 360), 0f) * Vector3.forward * (slotUse[slot] > 1 ? 1.6f : 0f);

            CardSuit suit = rng.Value() < 0.5f ? roomSuit : RunGenerator.RollRoomSuit(rng);

            orders.Add(new SpawnOrder
            {
                type = type,
                suit = suit,
                position = slot.transform.position + offset,
                rotation = slot.transform.rotation,
                wave = wave
            });
        }

        return orders;
    }

    private static float Cost(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Croupier: return CroupierCost;
            case EnemyType.Bouncer: return BouncerCost;
        }

        return ShulerCost;
    }

    private static EnemySpawn PickSlot(List<EnemySpawn> slots, EnemyType type, Dictionary<EnemySpawn, int> use, System.Random rng)
    {
        // Сначала точки с точной ролью, потом Any, потом любые; меньше занятые — лучше
        EnemySpawn best = null;
        float bestScore = float.MaxValue;

        foreach (EnemySpawn slot in slots)
        {
            float score = use.TryGetValue(slot, out int used) ? used * 10f : 0f;

            if (slot.Role == SpawnRole.Any)
                score += 1f;
            else if (!slot.Accepts(type))
                score += 50f;

            score += rng.Value();

            if (score < bestScore)
            {
                bestScore = score;
                best = slot;
            }
        }

        return best;
    }
}
