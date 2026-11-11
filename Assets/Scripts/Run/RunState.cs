using System.Collections.Generic;

/// <summary>
/// Состояние текущего забега. Живёт в статике между комнатами
/// (комнаты грузятся в той же сцене), сбрасывается при смерти или победе.
/// </summary>
public static class RunState
{
    public const int Chapters = 3;

    /// <summary>Глубина 1–5 — комнаты, 6 — босс.</summary>
    public const int DepthsPerChapter = 6;

    public static bool Active { get; private set; }
    public static int Seed { get; private set; }

    public static int Chapter;
    public static int Depth;

    /// <summary>Жетоны — валюта забега (магазин). Сгорают при смерти.</summary>
    public static int Tokens;

    /// <summary>Души, ещё не сданные в кассу.</summary>
    public static int PocketSouls;

    public static readonly List<CardData> Deck = new List<CardData>();

    public static DoorOffer CurrentRoom;
    public static CardSuit RoomSuit;

    /// <summary>Был ли магазин в текущей главе (правило «магазин гарантирован»).</summary>
    public static bool ShopVisitedThisChapter;

    public static void Begin(int seed, IEnumerable<CardData> deck)
    {
        Active = true;
        Seed = seed;
        Chapter = 1;
        Depth = 0;
        Tokens = 0;
        PocketSouls = 0;
        ShopVisitedThisChapter = false;

        Deck.Clear();
        Deck.AddRange(deck);
    }

    public static void End()
    {
        Active = false;
        Deck.Clear();
        Tokens = 0;
        PocketSouls = 0;
    }

    public static void NextChapter()
    {
        Chapter++;
        Depth = 0;
        ShopVisitedThisChapter = false;
    }
}
