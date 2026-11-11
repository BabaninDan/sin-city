using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Управляет аренами: вход → двери закрываются → волны врагов →
/// зачистка → двери открываются.
///
/// Арена собирается по ID: все arena_trigger, arena_door, enemy_spawn
/// и враги с одинаковым полем "arena" относятся к одной арене.
/// </summary>
public class ArenaManager : MonoBehaviour
{
    public enum ArenaStatus
    {
        Idle,
        Active,
        Cleared
    }

    private class Arena
    {
        public string id;
        public ArenaStatus status = ArenaStatus.Idle;
        public readonly List<ArenaDoor> doors = new List<ArenaDoor>();
        public readonly List<EnemySpawn> spawns = new List<EnemySpawn>();
        public readonly List<EnemyBase> placedEnemies = new List<EnemyBase>();
        public readonly List<EnemyBase> alive = new List<EnemyBase>();
        public int currentWave;
        public int maxWave;
        public int number;

        // Сгенерированная встреча (забег)
        public List<SpawnOrder> generated;
        public System.Action onCleared;
        public string label;
    }

    public static ArenaManager Instance { get; private set; }

    private readonly Dictionary<string, Arena> arenas = new Dictionary<string, Arena>();

    public string CurrentArenaId { get; private set; }
    public int AliveInCurrentArena => current != null ? current.alive.Count : 0;
    public int CurrentWave => current != null ? current.currentWave : 0;
    public int MaxWave => current != null ? current.maxWave : 0;
    public int ArenaCount => arenas.Count;
    public int ClearedCount { get; private set; }

    private Arena current;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Собрать все арены со сцены.</summary>
    public void Collect()
    {
        arenas.Clear();

        foreach (ArenaDoor door in FindObjectsByType<ArenaDoor>(FindObjectsSortMode.None))
            Get(door.ArenaId)?.doors.Add(door);

        foreach (EnemySpawn spawn in FindObjectsByType<EnemySpawn>(FindObjectsSortMode.None))
        {
            Arena arena = Get(spawn.ArenaId);

            if (arena == null)
                continue;

            arena.spawns.Add(spawn);
            arena.maxWave = Mathf.Max(arena.maxWave, Mathf.Max(1, spawn.Wave));
        }

        foreach (EnemyBase enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            Arena arena = Get(enemy.Arena);

            if (arena == null)
                continue;

            arena.placedEnemies.Add(enemy);
            arena.maxWave = Mathf.Max(arena.maxWave, 1);
        }

        foreach (ArenaTrigger trigger in FindObjectsByType<ArenaTrigger>(FindObjectsSortMode.None))
            Get(trigger.ArenaId);

        // Номера арен по порядку ID для сообщений
        List<string> ids = new List<string>(arenas.Keys);
        ids.Sort(System.StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < ids.Count; i++)
            arenas[ids[i]].number = i + 1;

        foreach (Arena arena in arenas.Values)
        {
            foreach (ArenaDoor door in arena.doors)
                door.SetOpen(door.Mode == ArenaDoor.DoorMode.Entrance, true);
        }

        Debug.Log($"ArenaManager: найдено арен — {arenas.Count}");
    }

    private Arena Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        id = id.Trim();

        if (!arenas.TryGetValue(id, out Arena arena))
        {
            arena = new Arena { id = id };
            arenas.Add(id, arena);
        }

        return arena;
    }

    // =========================================================
    // FLOW
    // =========================================================

    public void StartArena(string id)
    {
        Arena arena = Get(id);

        if (arena == null || arena.status != ArenaStatus.Idle)
            return;

        if (current != null && current.status == ArenaStatus.Active)
            return;

        current = arena;
        CurrentArenaId = arena.id;
        arena.status = ArenaStatus.Active;

        foreach (ArenaDoor door in arena.doors)
            door.SetOpen(false);

        GameState.Say(string.IsNullOrEmpty(arena.label) ? $"АРЕНА {arena.number}" : arena.label, new Color(1f, 0.3f, 0.3f));

        StartCoroutine(RunArena(arena));
    }

    private IEnumerator RunArena(Arena arena)
    {
        yield return new WaitForSeconds(0.6f);

        int lastWave = Mathf.Max(1, arena.maxWave);

        for (int wave = 1; wave <= lastWave; wave++)
        {
            arena.currentWave = wave;

            if (wave > 1)
            {
                GameState.Say($"ВОЛНА {wave}", SuitUtil.Gold);
                yield return new WaitForSeconds(0.8f);
            }

            SpawnWave(arena, wave);

            // Ждём, пока все враги волны умрут
            while (arena.alive.Count > 0)
            {
                arena.alive.RemoveAll(e => e == null || e.IsDead);

                if (GameState.PlayerDead)
                    yield break;

                yield return null;
            }

            yield return new WaitForSeconds(0.7f);
        }

        ClearArena(arena);
    }

    /// <summary>
    /// Запустить встречу, собранную генератором забега.
    /// onCleared вызывается после зачистки последней волны.
    /// </summary>
    public void StartGenerated(string id, string label, List<SpawnOrder> orders, System.Action onCleared)
    {
        Arena arena = new Arena
        {
            id = id,
            label = label,
            generated = orders,
            onCleared = onCleared,
            number = arenas.Count + 1
        };

        foreach (SpawnOrder order in orders)
            arena.maxWave = Mathf.Max(arena.maxWave, order.wave);

        arenas[id] = arena;

        // Прошлая арена могла остаться «активной», если комнату выгрузили
        if (current != null && current != arena)
        {
            current = null;
            CurrentArenaId = null;
        }

        StartArena(id);
    }

    private void SpawnWave(Arena arena, int wave)
    {
        if (arena.generated != null)
        {
            foreach (SpawnOrder order in arena.generated)
            {
                if (order.wave != wave)
                    continue;

                EnemyBase enemy = EnemySpawn.SpawnEnemy(
                    order.type,
                    order.position,
                    order.rotation,
                    arena.id,
                    EnemySpawn.ToChoice(order.suit));

                Track(arena, enemy);
            }

            return;
        }

        // Заранее расставленные враги — это первая волна
        if (wave == 1)
        {
            foreach (EnemyBase enemy in arena.placedEnemies)
            {
                if (enemy == null || enemy.IsDead)
                    continue;

                enemy.Activate();
                Track(arena, enemy);
            }
        }

        foreach (EnemySpawn spawn in arena.spawns)
        {
            if (Mathf.Max(1, spawn.Wave) != wave)
                continue;

            foreach (EnemyBase enemy in spawn.Spawn())
                Track(arena, enemy);
        }
    }

    private void Track(Arena arena, EnemyBase enemy)
    {
        if (enemy == null)
            return;

        arena.alive.Add(enemy);
        enemy.Activate();
    }

    private void ClearArena(Arena arena)
    {
        arena.status = ArenaStatus.Cleared;
        ClearedCount++;

        foreach (ArenaDoor door in arena.doors)
            door.SetOpen(true);

        GameState.Say(arena.generated != null ? "КОМНАТА ЗАЧИЩЕНА" : "АРЕНА ЗАЧИЩЕНА", new Color(0.4f, 1f, 0.5f));

        // Награда — щепотка душ
        MetaProgress.AddSouls(10);

        if (current == arena)
        {
            current = null;
            CurrentArenaId = null;
        }

        arena.onCleared?.Invoke();
    }

    public ArenaStatus GetStatus(string id)
    {
        Arena arena = Get(id);
        return arena != null ? arena.status : ArenaStatus.Cleared;
    }
}
