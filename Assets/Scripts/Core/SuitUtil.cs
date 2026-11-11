using UnityEngine;

public static class SuitUtil
{
    public static readonly Color Red = new Color(0.95f, 0.12f, 0.18f);
    public static readonly Color Black = new Color(0.08f, 0.08f, 0.1f);
    public static readonly Color Gold = new Color(1f, 0.8f, 0.25f);

    public static bool IsRed(this CardSuit suit)
    {
        return suit == CardSuit.Hearts || suit == CardSuit.Diamonds;
    }

    public static string Symbol(this CardSuit suit)
    {
        switch (suit)
        {
            case CardSuit.Hearts: return "♥";   // ♥
            case CardSuit.Diamonds: return "♦"; // ♦
            case CardSuit.Clubs: return "♣";    // ♣
            case CardSuit.Spades: return "♠";   // ♠
        }

        return "?";
    }

    public static string RussianName(this CardSuit suit)
    {
        switch (suit)
        {
            case CardSuit.Hearts: return "Черви";
            case CardSuit.Diamonds: return "Бубны";
            case CardSuit.Clubs: return "Трефы";
            case CardSuit.Spades: return "Пики";
        }

        return "?";
    }

    /// <summary>Цвет масти для карт (на белом фоне).</summary>
    public static Color CardColor(this CardSuit suit)
    {
        return suit.IsRed() ? Red : Black;
    }

    /// <summary>Цвет масти, хорошо видимый в тёмном 3D мире.</summary>
    public static Color WorldColor(this CardSuit suit)
    {
        return suit.IsRed() ? Red : new Color(0.55f, 0.6f, 0.75f);
    }

    public static CardSuit RandomSuit()
    {
        return (CardSuit)Random.Range(0, 4);
    }
}
