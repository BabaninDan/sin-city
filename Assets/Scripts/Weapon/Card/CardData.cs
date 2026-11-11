using UnityEngine;

public enum CardSuit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

public enum CardRank
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,

    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

[CreateAssetMenu(
    fileName = "NewCard",
    menuName = "Game/Cards/Card"
)]
public class CardData : ScriptableObject
{
    [Header("Card")]
    public CardSuit suit;
    public CardRank rank;

    [Header("Visual")]
    public Sprite cardSprite;

    [Header("Normal Card Damage")]
    [Min(0f)]
    public float damageMultiplier = 1f;

    [Min(0f)]
    public float matchingSuitMultiplier = 2f;

    public bool IsSpecialCard
    {
        get
        {
            return rank >= CardRank.Jack;
        }
    }

    public float GetBaseDamage()
    {
        return (int)rank * damageMultiplier;
    }

    public float GetDamage(CardSuit targetSuit)
    {
        float damage = GetBaseDamage();

        if (targetSuit == suit)
            damage *= matchingSuitMultiplier;

        return damage;
    }

    public string GetShortName()
    {
        string rankName = GetRankName();

        return $"{rankName}{GetSuitSymbol()}";
    }

    public string GetRankName()
    {
        switch (rank)
        {
            case CardRank.Two:
                return "2";

            case CardRank.Three:
                return "3";

            case CardRank.Four:
                return "4";

            case CardRank.Five:
                return "5";

            case CardRank.Six:
                return "6";

            case CardRank.Seven:
                return "7";

            case CardRank.Eight:
                return "8";

            case CardRank.Nine:
                return "9";

            case CardRank.Ten:
                return "10";

            case CardRank.Jack:
                return "J";

            case CardRank.Queen:
                return "Q";

            case CardRank.King:
                return "K";

            case CardRank.Ace:
                return "A";
        }

        return "?";
    }

    public string GetSuitSymbol()
    {
        switch (suit)
        {
            case CardSuit.Hearts:
                return "Cherv";

            case CardSuit.Diamonds:
                return "Bubni";

            case CardSuit.Clubs:
                return "Kresti";

            case CardSuit.Spades:
                return "Piki";
        }

        return "?";
    }
}