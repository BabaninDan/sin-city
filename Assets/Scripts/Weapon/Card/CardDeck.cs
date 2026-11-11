using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Колода из набора карт. Карты тянутся из перемешанной стопки,
/// когда стопка кончается — сброс перемешивается обратно.
/// Это обычный класс (не MonoBehaviour), им владеет WeaponController.
/// </summary>
public class CardDeck
{
    private readonly List<CardData> source = new List<CardData>();
    private readonly List<CardData> drawPile = new List<CardData>();

    public int Count => source.Count;

    public CardDeck(IEnumerable<CardData> cards)
    {
        if (cards != null)
        {
            foreach (CardData card in cards)
            {
                if (card != null)
                    source.Add(card);
            }
        }

        Shuffle();
    }

    public void Shuffle()
    {
        drawPile.Clear();
        drawPile.AddRange(source);

        // Фишер — Йетс
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
        }
    }

    public CardData Draw()
    {
        if (source.Count == 0)
            return null;

        if (drawPile.Count == 0)
            Shuffle();

        int last = drawPile.Count - 1;
        CardData card = drawPile[last];
        drawPile.RemoveAt(last);

        return card;
    }

    /// <summary>Подсмотреть следующую карту, не вытягивая её.</summary>
    public CardData Peek()
    {
        if (source.Count == 0)
            return null;

        if (drawPile.Count == 0)
            Shuffle();

        return drawPile[drawPile.Count - 1];
    }
}
