using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Создаётся автоматически в любой сцене, где есть игрок (PlayerStats).
///  - докидывает игроку недостающие компоненты (джокер, пулемёт, взаимодействие);
///  - применяет купленные улучшения;
///  - строит NavMesh по геометрии карты;
///  - собирает арены;
///  - смерть → экран смерти → перезапуск сцены (рогалик: уровень заново,
///    души/улучшения/оружие сохраняются);
///  - победа на level_exit_trigger.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>NavMesh построен — враги могут включать агентов.</summary>
    public static bool NavMeshReady { get; private set; }

    public PlayerStats Player { get; private set; }

    private NavMeshSurface surface;
    private bool paused;

    // =========================================================
    // BOOTSTRAP
    // =========================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        TryCreate();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate();
    }

    private static void TryCreate()
    {
        if (Instance != null)
            return;

        if (FindAnyObjectByType<PlayerStats>() == null)
            return;

        GameObject go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        NavMeshReady = false;
        Time.timeScale = 1f;

        GameState.ResetRun();
        HitFeedback.Clear();

        // Забег мог остаться «активным» после перезагрузки сцены
        if (RunState.Active)
            RunState.End();

        SetupPlayer();
        EnsureEventSystem();

        gameObject.AddComponent<ArenaManager>();
        gameObject.AddComponent<GameHUD>();
        gameObject.AddComponent<RunManager>();

        BuildNavMesh();

        ArenaManager.Instance.Collect();

        PlayerCamera.SetCursorLocked(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            NavMeshReady = false;
        }
    }

    // =========================================================
    // PLAYER
    // =========================================================

    private void SetupPlayer()
    {
        PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        if (players.Length == 0)
            return;

        // В сцене может оказаться два игрока (один в сцене, второй из карты).
        // Оставляем того, что пришёл из карты TrenchBroom.
        Player = players[0];

        if (players.Length > 1)
        {
            foreach (PlayerStats p in players)
            {
                if (p.GetComponentInParent<TinyGoose.Tremble.MapDocument>() != null)
                {
                    Player = p;
                    break;
                }
            }

            foreach (PlayerStats p in players)
            {
                if (p != Player)
                {
                    Debug.LogWarning($"GameManager: в сцене несколько игроков — отключаю лишнего '{p.name}'.", p);
                    p.gameObject.SetActive(false);
                }
            }
        }

        GameObject playerGo = Player.gameObject;

        if (playerGo.CompareTag("Untagged"))
            playerGo.tag = "Player";

        // Игрок не должен попадать в NavMesh
        AddNavIgnore(playerGo);

        Player.ApplyUpgrades(MetaProgress.BonusHealth);
        Player.Died += OnPlayerDied;

        WeaponController cards = playerGo.GetComponentInChildren<WeaponController>(true);
        Camera cam = playerGo.GetComponentInChildren<Camera>(true);

        ChipGun chips = playerGo.GetComponentInChildren<ChipGun>(true);

        if (chips == null)
        {
            GameObject holder = cards != null ? cards.gameObject : playerGo;
            chips = holder.AddComponent<ChipGun>();
        }

        chips.enabled = false;
        chips.Setup(cam, null);

        PlayerArsenal arsenal = playerGo.GetComponent<PlayerArsenal>();
        if (arsenal == null)
            arsenal = playerGo.AddComponent<PlayerArsenal>();

        arsenal.Setup(cards, chips);

        if (playerGo.GetComponent<JokerAbility>() == null)
            playerGo.AddComponent<JokerAbility>();

        if (playerGo.GetComponent<PlayerInteractor>() == null)
            playerGo.AddComponent<PlayerInteractor>();
    }

    private static void AddNavIgnore(GameObject go)
    {
        NavMeshModifier modifier = go.GetComponent<NavMeshModifier>();

        if (modifier == null)
            modifier = go.AddComponent<NavMeshModifier>();

        modifier.ignoreFromBuild = true;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // =========================================================
    // NAVMESH
    // =========================================================

    private void BuildNavMesh()
    {
        // Всё динамическое — мимо NavMesh
        foreach (EnemyBase enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            AddNavIgnore(enemy.gameObject);

        foreach (ArenaTrigger trigger in FindObjectsByType<ArenaTrigger>(FindObjectsSortMode.None))
            AddNavIgnore(trigger.gameObject);

        foreach (LevelExit exit in FindObjectsByType<LevelExit>(FindObjectsSortMode.None))
            AddNavIgnore(exit.gameObject);

        foreach (AmmoCrate crate in FindObjectsByType<AmmoCrate>(FindObjectsSortMode.None))
            AddNavIgnore(crate.gameObject);

        foreach (ChipGunPickup pickup in FindObjectsByType<ChipGunPickup>(FindObjectsSortMode.None))
            AddNavIgnore(pickup.gameObject);

        // Если в сцене уже есть запечённый NavMesh — не трогаем
        NavMeshSurface existing = FindAnyObjectByType<NavMeshSurface>();

        if (existing != null && existing.navMeshData != null)
        {
            NavMeshReady = true;
            return;
        }

        Physics.SyncTransforms();

        surface = gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;

        float started = Time.realtimeSinceStartup;
        surface.BuildNavMesh();

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        Debug.Log($"GameManager: NavMesh построен за {(Time.realtimeSinceStartup - started) * 1000f:0} мс, вершин: {tri.vertices.Length}");

        NavMeshReady = tri.vertices.Length > 0;

        if (!NavMeshReady)
            Debug.LogWarning("GameManager: NavMesh пустой — враги не смогут ходить. Проверь, что у карты есть коллайдеры.");
    }

    // =========================================================
    // DEATH / VICTORY
    // =========================================================

    private void OnPlayerDied()
    {
        if (GameState.PlayerDead)
            return;

        GameState.PlayerDead = true;
        MetaProgress.Deaths++;

        // Незданные души остаются на месте смерти
        if (RunManager.Instance != null)
            RunManager.Instance.HandleDeath();
        MetaProgress.Save();

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        PlayerCamera cam = Player != null ? Player.GetComponentInChildren<PlayerCamera>() : null;
        if (cam != null)
            cam.Shake(0.5f, 0.6f);

        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(0.8f);
        Time.timeScale = 0f;

        if (GameHUD.Instance != null)
            GameHUD.Instance.ShowDeath();

        PlayerCamera.SetCursorLocked(false);

        yield return new WaitForSecondsRealtime(0.6f);

        while (true)
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                break;

            yield return null;
        }

        Restart();
    }

    public void Win()
    {
        if (GameState.Victory || GameState.PlayerDead)
            return;

        GameState.Victory = true;
        GameState.InputBlocked = true;

        StartCoroutine(VictorySequence());
    }

    private void OnApplicationQuit()
    {
        MetaProgress.Save();
    }

    private IEnumerator VictorySequence()
    {
        MetaProgress.AddSouls(50);
        MetaProgress.Save();

        if (GameHUD.Instance != null)
            GameHUD.Instance.ShowVictory();

        Time.timeScale = 0f;
        PlayerCamera.SetCursorLocked(false);

        yield return new WaitForSecondsRealtime(1f);

        while (!(Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            yield return null;

        Restart();
    }

    public void Restart()
    {
        Time.timeScale = 1f;

        Scene scene = SceneManager.GetActiveScene();

        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }

#if UNITY_EDITOR
        // Сцены нет в Build Settings — в редакторе грузим по пути
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path,
            new LoadSceneParameters(LoadSceneMode.Single)
        );
#else
        SceneManager.LoadScene(0);
#endif
    }

    // =========================================================
    // PAUSE
    // =========================================================

    private void Update()
    {
        if (GameState.PlayerDead || GameState.Victory)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameHUD.Instance != null && GameHUD.Instance.ChoiceOpen)
            {
                // Окно награды/магазина закрывается только выбором
            }
            else if (GameHUD.Instance != null && GameHUD.Instance.UpgradeMenuOpen)
                GameHUD.Instance.CloseUpgradeMenu();
            else
                SetPaused(!paused);
        }

        // Если игрок кликнул в окно после паузы — вернуть захват мыши
        if (!paused && !GameState.InputBlocked && Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            PlayerCamera.SetCursorLocked(true);
    }

    public void SetPaused(bool value)
    {
        paused = value;
        GameState.InputBlocked = value;
        Time.timeScale = value ? 0f : 1f;
        PlayerCamera.SetCursorLocked(!value);

        if (GameHUD.Instance != null)
            GameHUD.Instance.ShowPause(value);
    }

    public void ResetProgressAndRestart()
    {
        MetaProgress.ResetAll();
        Restart();
    }
}
