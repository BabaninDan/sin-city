using UnityEngine;

public enum EnemyType
{
    Shuler,
    Croupier,
    Bouncer
}

/// <summary>
/// Ссылки на ассеты, которые нужны системам во время игры.
/// Лежит в Resources/SinCity/GameConfig.asset, создаётся меню
/// "Sin City → Setup Prototype".
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Game Config")]
public class GameConfig : ScriptableObject
{
    private const string ResourcePath = "SinCity/GameConfig";

    [Header("Cards")]
    public CardData[] cards;

    [Header("Enemies")]
    public GameObject shulerPrefab;
    public GameObject croupierPrefab;
    public GameObject bouncerPrefab;

    [Header("Run")]
    [Tooltip("Шаблоны комнат забега — импортированные .map из Assets/Maps/Rooms.")]
    public GameObject[] roomTemplates;

    [Header("Materials")]
    [Tooltip("Unlit материал для процедурных объектов (фишки, снаряды, пикапы).")]
    public Material unlitMaterial;

    [Tooltip("Материал для частиц (кровь, искры).")]
    public Material particleMaterial;

    private static GameConfig instance;
    private static bool triedLoad;

    public static GameConfig Instance
    {
        get
        {
            if (instance == null && !triedLoad)
            {
                triedLoad = true;
                instance = Resources.Load<GameConfig>(ResourcePath);

                if (instance == null)
                {
                    Debug.LogWarning(
                        "GameConfig не найден в Resources/SinCity. " +
                        "Запусти меню Sin City → Setup Prototype."
                    );
                }
            }

            return instance;
        }
    }

    public GameObject GetEnemyPrefab(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Shuler: return shulerPrefab;
            case EnemyType.Croupier: return croupierPrefab;
            case EnemyType.Bouncer: return bouncerPrefab;
        }

        return null;
    }
}
