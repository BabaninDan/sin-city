using System.Collections.Generic;
using TinyGoose.Tremble;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Точка появления врагов (TrenchBroom: enemy_spawn).
/// Ручные уровни: arena, enemy_type, suit, wave, count — спавнится ровно это.
/// Шаблоны комнат забега: role = Any / Melee / Ranged / Heavy — кого и в какой
/// волне поставить, решает генератор (остальные поля игнорируются).
/// </summary>
[PointEntity("spawn", category: "enemy", colour: "1.0 0.2 0.2", size: 0.5f)]
public class EnemySpawn : MonoBehaviour
{
    [SerializeField] private string arena = "1";
    [SerializeField] private EnemyType enemyType = EnemyType.Shuler;
    [SerializeField] private EnemySuitChoice suit = EnemySuitChoice.Random;
    [SerializeField] private int wave = 1;
    [SerializeField] private int count = 1;
    [SerializeField] private SpawnRole role = SpawnRole.Fixed;

    public string ArenaId => arena;
    public int Wave => wave;
    public SpawnRole Role => role;
    public EnemyType FixedType => enemyType;

    /// <summary>Для точек, созданных кодом (запасная комната).</summary>
    public void SetRole(SpawnRole value)
    {
        role = value;
    }

    /// <summary>Подходит ли точка для врага этого типа (для генератора).</summary>
    public bool Accepts(EnemyType type)
    {
        switch (role)
        {
            case SpawnRole.Fixed: return type == enemyType;
            case SpawnRole.Any: return true;
            case SpawnRole.Melee: return type == EnemyType.Shuler;
            case SpawnRole.Ranged: return type == EnemyType.Croupier;
            case SpawnRole.Heavy: return type == EnemyType.Bouncer;
        }

        return true;
    }

    public List<EnemyBase> Spawn()
    {
        List<EnemyBase> result = new List<EnemyBase>();

        int amount = Mathf.Max(1, count);

        for (int i = 0; i < amount; i++)
        {
            Vector3 offset = amount > 1
                ? Quaternion.Euler(0f, 360f * i / amount, 0f) * Vector3.forward * 1.5f
                : Vector3.zero;

            EnemyBase enemy = SpawnEnemy(enemyType, transform.position + offset, transform.rotation, arena, suit);

            if (enemy != null)
                result.Add(enemy);
        }

        return result;
    }

    /// <summary>Создать врага с эффектом появления.</summary>
    public static EnemyBase SpawnEnemy(EnemyType type, Vector3 position, Quaternion rotation, string arenaId, EnemySuitChoice suitChoice)
    {
        GameConfig config = GameConfig.Instance;
        GameObject prefab = config != null ? config.GetEnemyPrefab(type) : null;

        if (prefab == null)
        {
            Debug.LogError($"EnemySpawn: нет префаба для {type}. Запусти Sin City → Setup Prototype.");
            return null;
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            position = hit.position;

        GameObject go = Instantiate(prefab, position, rotation);
        EnemyBase enemy = go.GetComponent<EnemyBase>();

        if (enemy == null)
            return null;

        enemy.Configure(arenaId, suitChoice);

        // Появление из карточного вихря
        FX.Burst(position + Vector3.up, new Color(0.15f, 0.05f, 0.1f), 25, 5f, 0.3f, 0.6f, -0.5f);
        FX.Burst(position + Vector3.up, SuitUtil.Gold, 10, 4f, 0.12f, 0.5f, -0.5f);
        FX.Ring(position, 2f, enemy.Suit.WorldColor(), 0.4f);

        return enemy;
    }

    public static EnemySuitChoice ToChoice(CardSuit suit)
    {
        return (EnemySuitChoice)((int)suit + 1);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = enemyType == EnemyType.Bouncer
            ? new Color(1f, 0.4f, 0f)
            : enemyType == EnemyType.Croupier ? Color.yellow : Color.red;

        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
    }
}
