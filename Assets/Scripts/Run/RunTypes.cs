using System;
using UnityEngine;

/// <summary>Тип комнаты за дверью.</summary>
public enum RoomKind
{
    Combat,
    Elite,
    Shop,
    Bar,
    Roulette,
    SoulRetrieval,
    Boss
}

/// <summary>Награда за зачистку комнаты (видна на двери).</summary>
public enum RewardKind
{
    None,
    Card,
    Tokens,
    Souls,
    Heal
}

/// <summary>Роль точки спавна в шаблоне комнаты.</summary>
public enum SpawnRole
{
    /// <summary>Спавнит ровно enemy_type (ручные уровни).</summary>
    Fixed,
    Any,
    Melee,
    Ranged,
    Heavy
}

/// <summary>Одно предложение двери: что за комната и какая награда.</summary>
[Serializable]
public struct DoorOffer
{
    public RoomKind kind;
    public RewardKind reward;
    public int amount;

    public DoorOffer(RoomKind kind, RewardKind reward = RewardKind.None, int amount = 0)
    {
        this.kind = kind;
        this.reward = reward;
        this.amount = amount;
    }

    public bool IsFight =>
        kind == RoomKind.Combat ||
        kind == RoomKind.Elite ||
        kind == RoomKind.Boss ||
        kind == RoomKind.SoulRetrieval;

    public string Title
    {
        get
        {
            switch (kind)
            {
                case RoomKind.Combat: return "БОЙ";
                case RoomKind.Elite: return "ЭЛИТА";
                case RoomKind.Shop: return "МАГАЗИН";
                case RoomKind.Bar: return "БАР";
                case RoomKind.Roulette: return "РУЛЕТКА";
                case RoomKind.SoulRetrieval: return "ТВОЯ ДУША";
                case RoomKind.Boss: return "БОСС";
            }

            return kind.ToString();
        }
    }

    public string RewardText
    {
        get
        {
            switch (reward)
            {
                case RewardKind.Card: return "карта";
                case RewardKind.Tokens: return $"+{amount} жетонов";
                case RewardKind.Souls: return $"+{amount} душ";
                case RewardKind.Heal: return $"+{amount} HP";
            }

            return "";
        }
    }

    public Color Color
    {
        get
        {
            switch (kind)
            {
                case RoomKind.Combat: return new Color(0.9f, 0.85f, 0.8f);
                case RoomKind.Elite: return new Color(1f, 0.35f, 0.2f);
                case RoomKind.Shop: return new Color(0.3f, 0.8f, 0.4f);
                case RoomKind.Bar: return new Color(0.4f, 0.6f, 1f);
                case RoomKind.Roulette: return new Color(0.9f, 0.2f, 0.6f);
                case RoomKind.SoulRetrieval: return new Color(0.6f, 0.4f, 1f);
                case RoomKind.Boss: return new Color(0.95f, 0.1f, 0.15f);
            }

            return Color.white;
        }
    }
}

/// <summary>Кого, где и в какой волне заспавнить.</summary>
public struct SpawnOrder
{
    public EnemyType type;
    public CardSuit suit;
    public Vector3 position;
    public Quaternion rotation;
    public int wave;
}

/// <summary>
/// Детерминированный случай: один сид забега + глава + глубина + соль
/// всегда дают одну и ту же последовательность.
/// </summary>
public static class RunRng
{
    public static System.Random Get(int seed, int chapter, int depth, int salt)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + seed;
            h = h * 31 + chapter;
            h = h * 31 + depth;
            h = h * 31 + salt;
            return new System.Random(h);
        }
    }

    public static float Value(this System.Random rng)
    {
        return (float)rng.NextDouble();
    }

    public static int Range(this System.Random rng, int min, int maxExclusive)
    {
        return rng.Next(min, maxExclusive);
    }

    public static T Pick<T>(this System.Random rng, System.Collections.Generic.IList<T> list)
    {
        return list[rng.Next(0, list.Count)];
    }
}
