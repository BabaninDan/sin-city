using UnityEngine;

public class EnemySuit : MonoBehaviour
{
    [SerializeField] private CardSuit suit;

    public CardSuit Suit => suit;
}