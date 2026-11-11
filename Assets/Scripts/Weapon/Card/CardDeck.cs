using UnityEngine;

public class CardDeck : MonoBehaviour
{
    [SerializeField] private WeaponData weaponData;

    private CardData currentCard;

    private int cardsThrown;

    private CardData[] deck;

    public CardData CurrentCard => currentCard;

    public int CardsThrown => cardsThrown;

    public int MaxCards
    {
        get
        {
            if (weaponData == null)
                return 0;

            return weaponData.maxCards;
        }
    }

    public bool IsEmpty
    {
        get
        {
            return currentCard == null;
        }
    }

    private void Awake()
    {
        CreateDeck();
        DrawNextCard();
    }

    private void CreateDeck()
    {
        if (weaponData == null)
        {
            Debug.LogError(
                "CardDeck: WeaponData не назначен!",
                gameObject
            );

            return;
        }

        deck = weaponData.normalCards;
    }

    public CardData DrawNextCard()
    {
        if (deck == null || deck.Length == 0)
        {
            Debug.LogError(
                "CardDeck: В WeaponData нет normalCards!",
                gameObject
            );

            return null;
        }

        CardData newCard =
            deck[Random.Range(0, deck.Length)];

        currentCard = newCard;

        return currentCard;
    }

    public CardData ThrowCurrentCard()
    {
        if (currentCard == null)
            return null;

        CardData thrownCard = currentCard;

        cardsThrown++;

        currentCard = null;

        return thrownCard;
    }

    public bool CanThrow()
    {
        return currentCard != null;
    }

    public bool NeedsReload()
    {
        return cardsThrown >= MaxCards;
    }

    public void ResetDeck()
    {
        cardsThrown = 0;

        DrawNextCard();
    }
}