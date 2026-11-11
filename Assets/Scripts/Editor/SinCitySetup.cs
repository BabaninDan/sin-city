using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Меню Sin City → Setup Prototype.
/// Создаёт всё, что нужно для играбельного прототипа:
/// карты колоды, материалы, префабы врагов и пропов, GameConfig,
/// синхронизирует энтити с TrenchBroom и собирает сцену Prototype.
///
/// Повторный запуск безопасен: существующие префабы и ассеты не перезаписываются
/// (кроме пункта "Rebuild Prefabs").
/// </summary>
public static class SinCitySetup
{
    private const string DataFolder = "Assets/Data";
    private const string CardsFolder = "Assets/Data/Cards";
    private const string MaterialsFolder = "Assets/Data/Materials";
    private const string EnemiesFolder = "Assets/Prefab/Enemies";
    private const string PropsFolder = "Assets/Prefab/Props";
    private const string ResourcesFolder = "Assets/Resources/SinCity";
    private const string ConfigPath = "Assets/Resources/SinCity/GameConfig.asset";

    private const string CardWeaponPath = "Assets/Weapon/Card.asset";
    private const string CardProjectilePath = "Assets/Prefab/CardProjectile.prefab";
    private const string EnemyControllerPath = "Assets/Sprite/Enemy/Dragger/Enemy.controller";
    private const string EnemySpritePath = "Assets/Sprite/Enemy/Dragger/DragerFront.png";
    private const string CrateMaterialPath = "Assets/TextMesh Pro/Examples & Extras/Materials/Crate - URP.mat";

    private const string MapPath = "Assets/Maps/MAP_Prototype.map";
    private const string ScenePath = "Assets/Scenes/Prototype.unity";
    private const string HubMapPath = "Assets/Maps/MAP_Hub.map";
    private const string HubScenePath = "Assets/Scenes/Hub.unity";
    private const string RoomsFolder = "Assets/Maps/Rooms";

    // =========================================================
    // MENU
    // =========================================================

    [MenuItem("Sin City/Setup Prototype", priority = 0)]
    public static void SetupAll()
    {
        Run(false);
    }

    [MenuItem("Sin City/Rebuild Prefabs (overwrite)", priority = 20)]
    public static void RebuildPrefabs()
    {
        if (!EditorUtility.DisplayDialog(
                "Sin City",
                "Пересоздать префабы врагов и пропов? Ручные правки в них будут потеряны.",
                "Пересоздать",
                "Отмена"))
            return;

        Run(true);
    }

    [MenuItem("Sin City/Build Prototype Scene", priority = 1)]
    public static void BuildSceneMenu()
    {
        ReimportMap();
        AssignRoomTemplates();
        BuildScene(MapPath, ScenePath, true, 1);
        BuildScene(HubMapPath, HubScenePath, true, 0);
    }

    [MenuItem("Sin City/Reset Saved Progress", priority = 40)]
    public static void ResetProgress()
    {
        MetaProgress.ResetAll();
        Debug.Log("Sin City: прогресс (души, улучшения, пулемёт) сброшен.");
    }

    [MenuItem("Sin City/Give 500 Souls (debug)", priority = 41)]
    public static void GiveSouls()
    {
        MetaProgress.AddSouls(500);
        Debug.Log($"Sin City: душ теперь {MetaProgress.Souls}.");
    }

    private static void Run(bool overwritePrefabs)
    {
        try
        {
            EditorUtility.DisplayProgressBar("Sin City", "Папки и материалы...", 0.1f);
            EnsureFolders();

            Material unlit = EnsureMaterial(MaterialsFolder + "/FX_Unlit.mat", "Universal Render Pipeline/Unlit", Color.white);
            Material particles = EnsureMaterial(MaterialsFolder + "/FX_Particle.mat", "Universal Render Pipeline/Particles/Unlit", Color.white);

            Material body = EnsureMaterial(MaterialsFolder + "/M_MachineBody.mat", "Universal Render Pipeline/Lit", new Color(0.35f, 0.04f, 0.06f));
            Material gold = EnsureMaterial(MaterialsFolder + "/M_Gold.mat", "Universal Render Pipeline/Lit", new Color(1f, 0.78f, 0.25f), 0.8f, 0.7f);
            Material screen = EnsureMaterial(MaterialsFolder + "/M_Screen.mat", "Universal Render Pipeline/Unlit", new Color(1f, 0.7f, 0.2f));
            Material dark = EnsureMaterial(MaterialsFolder + "/M_Dark.mat", "Universal Render Pipeline/Lit", new Color(0.08f, 0.07f, 0.08f));
            Material red = EnsureMaterial(MaterialsFolder + "/M_ChipRed.mat", "Universal Render Pipeline/Lit", new Color(0.85f, 0.08f, 0.1f));
            Material blue = EnsureMaterial(MaterialsFolder + "/M_ChipBlue.mat", "Universal Render Pipeline/Lit", new Color(0.1f, 0.3f, 0.9f));

            EditorUtility.DisplayProgressBar("Sin City", "Колода карт...", 0.25f);
            CardData[] cards = EnsureCards();

            EditorUtility.DisplayProgressBar("Sin City", "Оружие...", 0.35f);
            SetupCardWeapon();

            EditorUtility.DisplayProgressBar("Sin City", "Враги...", 0.5f);
            GameObject shuler = BuildEnemy<EnemyShuler>("Shuler", Color.white, 1.4f, 0.45f, 2.1f, overwritePrefabs,
                hp: 45f, speed: 7f, souls: 4, ammoChance: 0.15f);

            GameObject croupier = BuildEnemy<EnemyCroupier>("Croupier", new Color(0.65f, 1f, 0.7f), 1.35f, 0.45f, 2.0f, overwritePrefabs,
                hp: 35f, speed: 4.5f, souls: 6, ammoChance: 0.6f);

            GameObject bouncer = BuildEnemy<EnemyBouncer>("Bouncer", new Color(1f, 0.55f, 0.45f), 1.9f, 0.75f, 2.9f, overwritePrefabs,
                hp: 170f, speed: 3.2f, souls: 15, ammoChance: 1f);

            EditorUtility.DisplayProgressBar("Sin City", "Пропы...", 0.65f);
            BuildCrate(overwritePrefabs);
            BuildUpgradeMachine(body, gold, screen, overwritePrefabs);
            BuildChipGunPickup(dark, gold, red, blue, overwritePrefabs);

            EditorUtility.DisplayProgressBar("Sin City", "GameConfig...", 0.75f);
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.cards = cards;
            config.shulerPrefab = shuler;
            config.croupierPrefab = croupier;
            config.bouncerPrefab = bouncer;
            config.unlitMaterial = unlit;
            config.particleMaterial = particles;
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        SyncThenBuildScene();
    }

    /// <summary>
    /// Синхронизация Tremble асинхронная: ждём её, потом переимпортируем карту
    /// (чтобы новые префаб-энтити нашлись) и собираем сцену.
    /// </summary>
    private static async void SyncThenBuildScene()
    {
        Debug.Log("Sin City: синхронизация с TrenchBroom...");

        Task sync = null;

        try
        {
            System.Type syncType = typeof(TinyGoose.Tremble.Editor.TrembleEditorAPI).Assembly
                .GetType("TinyGoose.Tremble.Editor.TrembleSync");

            MethodInfo method = syncType?.GetMethod(
                "PerformSync",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            sync = method?.Invoke(null, null) as Task;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Sin City: не получилось вызвать синхронизацию Tremble напрямую: " + e.Message);
        }

        if (sync != null)
        {
            try
            {
                await sync;
            }
            catch (System.Exception e)
            {
                Debug.LogError("Sin City: ошибка синхронизации Tremble: " + e);
            }
        }
        else
        {
            EditorApplication.ExecuteMenuItem("Tools/Tremble/Sync Project to TrenchBroom");
            await Task.Delay(4000);
        }

        ReimportMap();
        int rooms = AssignRoomTemplates();

        bool prototypeBuilt = BuildScene(MapPath, ScenePath, false, 1);
        bool sceneBuilt = BuildScene(HubMapPath, HubScenePath, false, 0);

        string message =
            "Готово!\n\n" +
            "• 52 карты, оружие, 3 врага, ящик, автомат душ и пулемёт фишек созданы\n" +
            "• Энтити отправлены в TrenchBroom (категории arena / enemy / prop / level)\n" +
            $"• Шаблонов комнат забега: {rooms}\n" +
            (sceneBuilt
                ? "• Сцена Hub (комната героя + портал «За стол») собрана и открыта — жми Play\n"
                : $"• Карта {HubMapPath} не импортировалась — проверь Console, затем Sin City → Build Prototype Scene\n") +
            (prototypeBuilt ? "• Ручной уровень — сцена Prototype" : "");

        EditorUtility.DisplayDialog("Sin City", message, "OK");
    }

    /// <summary>Переимпортировать все .map (новые префаб-энтити должны найтись).</summary>
    private static void ReimportMap()
    {
        if (!Directory.Exists("Assets/Maps"))
            return;

        foreach (string file in Directory.GetFiles("Assets/Maps", "*.map", SearchOption.AllDirectories))
        {
            string path = file.Replace("\\", "/");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }

    /// <summary>Все карты из Assets/Maps/Rooms → GameConfig.roomTemplates.</summary>
    private static int AssignRoomTemplates()
    {
        GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

        if (config == null || !Directory.Exists(RoomsFolder))
            return 0;

        List<GameObject> templates = new List<GameObject>();

        foreach (string file in Directory.GetFiles(RoomsFolder, "*.map", SearchOption.TopDirectoryOnly))
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace("\\", "/"));

            if (template != null)
                templates.Add(template);
            else
                Debug.LogWarning($"Sin City: шаблон {file} ещё не импортирован Tremble.");
        }

        config.roomTemplates = templates.ToArray();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        Debug.Log($"Sin City: шаблонов комнат забега — {templates.Count}");
        return templates.Count;
    }

    // =========================================================
    // FOLDERS / MATERIALS
    // =========================================================

    private static void EnsureFolders()
    {
        EnsureFolder(DataFolder);
        EnsureFolder(CardsFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(EnemiesFolder);
        EnsureFolder(PropsFolder);
        EnsureFolder("Assets/Resources");
        EnsureFolder(ResourcesFolder);
        EnsureFolder("Assets/Maps");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }

    private static Material EnsureMaterial(string path, string shaderName, Color color, float metallic = 0f, float smoothness = 0.3f)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
            return material;

        Shader shader = Shader.Find(shaderName);

        if (shader == null)
        {
            Debug.LogWarning($"Sin City: шейдер '{shaderName}' не найден, использую стандартный.");
            shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        }

        material = new Material(shader);

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // =========================================================
    // CARDS
    // =========================================================

    private static CardData[] EnsureCards()
    {
        List<CardData> result = new List<CardData>();

        foreach (CardSuit suit in System.Enum.GetValues(typeof(CardSuit)))
        {
            foreach (CardRank rank in System.Enum.GetValues(typeof(CardRank)))
            {
                string path = $"{CardsFolder}/Card_{(int)rank:00}_{suit}.asset";
                CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);

                if (card == null)
                {
                    card = ScriptableObject.CreateInstance<CardData>();
                    card.suit = suit;
                    card.rank = rank;
                    card.damageMultiplier = 1.5f;
                    card.matchingSuitMultiplier = 2f;
                    AssetDatabase.CreateAsset(card, path);
                }

                result.Add(card);
            }
        }

        return result.ToArray();
    }

    private static void SetupCardWeapon()
    {
        WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(CardWeaponPath);

        if (weapon == null)
        {
            Debug.LogWarning($"Sin City: не найден {CardWeaponPath} — пропускаю настройку колоды.");
            return;
        }

        Projectile projectile = AssetDatabase.LoadAssetAtPath<Projectile>(CardProjectilePath);

        if (weapon.projectilePrefab == null && projectile != null)
            weapon.projectilePrefab = projectile;

        // Темп под "скорость": бросок чаще, карта летит быстрее
        if (weapon.fireRate > 0.35f)
            weapon.fireRate = 0.28f;

        if (weapon.projectileSpeed < 35f)
            weapon.projectileSpeed = 40f;

        if (weapon.reloadTime > 0.9f)
            weapon.reloadTime = 0.8f;

        EditorUtility.SetDirty(weapon);
    }

    // =========================================================
    // ENEMIES
    // =========================================================

    private static GameObject BuildEnemy<T>(
        string name,
        Color tint,
        float visualScale,
        float radius,
        float height,
        bool overwrite,
        float hp,
        float speed,
        int souls,
        float ammoChance) where T : EnemyBase
    {
        string path = $"{EnemiesFolder}/{name}.prefab";

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !overwrite)
            return existing;

        GameObject root = new GameObject(name);

        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.radius = radius;
        capsule.height = height;
        capsule.center = new Vector3(0f, height * 0.5f, 0f);

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = radius;
        agent.height = height;
        agent.baseOffset = 0f;
        agent.speed = speed;
        agent.acceleration = 40f;
        agent.angularSpeed = 720f;
        agent.enabled = false; // включается, когда NavMesh построен

        // Спрайт
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * visualScale;

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnemySpritePath);
        sr.color = tint;
        sr.shadowCastingMode = ShadowCastingMode.Off;

        Animator animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyControllerPath);

        T enemy = root.AddComponent<T>();

        SerializedObject so = new SerializedObject(enemy);
        SetFloat(so, "maxHealth", hp);
        SetFloat(so, "moveSpeed", speed);
        SetInt(so, "souls", souls);
        SetFloat(so, "ammoDropChance", ammoChance);
        SetObject(so, "visual", visual.transform);
        SetObject(so, "animator", animator);
        SetObject(so, "spriteRenderer", sr);

        SerializedProperty tintProp = so.FindProperty("tint");
        if (tintProp != null)
            tintProp.colorValue = tint;

        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return prefab;
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.intValue = value;
    }

    private static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
    }

    // =========================================================
    // PROPS
    // =========================================================

    private static GameObject Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;

        if (!keepCollider)
            Object.DestroyImmediate(go.GetComponent<Collider>());

        if (material != null)
            go.GetComponent<Renderer>().sharedMaterial = material;

        return go;
    }

    private static void BuildCrate(bool overwrite)
    {
        string path = $"{PropsFolder}/AmmoCrate.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null && !overwrite)
            return;

        GameObject root = new GameObject("AmmoCrate");

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.45f, 0f);
        box.size = Vector3.one * 0.9f;

        Material crate = AssetDatabase.LoadAssetAtPath<Material>(CrateMaterialPath);
        Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.45f, 0f), Vector3.one * 0.9f, crate);

        root.AddComponent<AmmoCrate>();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void BuildUpgradeMachine(Material body, Material gold, Material screenMat, bool overwrite)
    {
        string path = $"{PropsFolder}/UpgradeMachine.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null && !overwrite)
            return;

        GameObject root = new GameObject("UpgradeMachine");

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 1.1f, 0f);
        box.size = new Vector3(1.3f, 2.2f, 1.3f);

        Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.0f, 0f), new Vector3(1.2f, 2f, 1.2f), body);
        Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 2.05f, 0f), new Vector3(1.3f, 0.12f, 1.3f), gold);
        Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1.3f, 0.1f, 1.3f), gold);

        // Экраны со всех сторон — автомат видно откуда угодно
        GameObject screenRoot = new GameObject("Screens");
        screenRoot.transform.SetParent(root.transform, false);

        Renderer firstScreen = null;

        for (int i = 0; i < 4; i++)
        {
            Quaternion rot = Quaternion.Euler(0f, i * 90f, 0f);
            GameObject s = Part(PrimitiveType.Cube, screenRoot.transform, rot * new Vector3(0f, 0f, 0.61f) + Vector3.up * 1.35f, Vector3.one, screenMat);
            s.transform.localRotation = rot;
            s.transform.localScale = new Vector3(0.8f, 0.55f, 0.02f);

            if (firstScreen == null)
                firstScreen = s.GetComponent<Renderer>();

            // Надписи создаются в рантайме (UpgradeMachine.Awake):
            // TMP в префабе мешает экспорту модели в TrenchBroom.
        }

        // Рычаг
        Part(PrimitiveType.Cylinder, root.transform, new Vector3(0.7f, 1.3f, 0f), new Vector3(0.06f, 0.35f, 0.06f), gold);
        Part(PrimitiveType.Sphere, root.transform, new Vector3(0.7f, 1.68f, 0f), Vector3.one * 0.16f, gold);

        UpgradeMachine machine = root.AddComponent<UpgradeMachine>();

        SerializedObject so = new SerializedObject(machine);
        SetObject(so, "screen", firstScreen);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void BuildChipGunPickup(Material dark, Material gold, Material red, Material blue, bool overwrite)
    {
        string path = $"{PropsFolder}/ChipGunPickup.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null && !overwrite)
            return;

        GameObject root = new GameObject("ChipGunPickup");

        // Постамент
        Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.4f, 0.9f), dark, true);
        Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.82f, 0f), new Vector3(1f, 0.03f, 1f), gold);

        // Вращающийся пулемёт
        GameObject spinner = new GameObject("Spinner");
        spinner.transform.SetParent(root.transform, false);
        spinner.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        Part(PrimitiveType.Cube, spinner.transform, Vector3.zero, new Vector3(0.25f, 0.22f, 0.7f), red);
        Part(PrimitiveType.Cube, spinner.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.27f, 0.03f, 0.72f), gold);

        Material[] chipMats = { red, blue, gold, dark, red, blue };

        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            GameObject chip = Part(PrimitiveType.Cylinder, spinner.transform,
                new Vector3(Mathf.Cos(a) * 0.12f, Mathf.Sin(a) * 0.12f, 0.42f),
                new Vector3(0.08f, 0.14f, 0.08f), chipMats[i]);
            chip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        ChipGunPickup pickup = root.AddComponent<ChipGunPickup>();

        SerializedObject so = new SerializedObject(pickup);
        SetObject(so, "spinner", spinner.transform);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    // =========================================================
    // SCENE
    // =========================================================

    /// <summary>Собрать сцену Prototype из импортированной карты.</summary>
    private static bool BuildScene(string mapPath, string scenePath, bool interactive, int buildIndex)
    {
        GameObject map = AssetDatabase.LoadAssetAtPath<GameObject>(mapPath);

        if (map == null)
        {
            if (interactive)
            {
                EditorUtility.DisplayDialog("Sin City",
                    $"Карта {mapPath} не найдена или ещё не импортирована Tremble.\n" +
                    "Подожди окончания импорта (или выбери её и нажми Reimport) и повтори.",
                    "OK");
            }

            return false;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Карта
        GameObject mapInstance = (GameObject)PrefabUtility.InstantiatePrefab(map, scene);
        mapInstance.transform.position = Vector3.zero;

        // Тусклый тёплый "лунный" свет + тёмно-красная атмосфера казино
        GameObject sun = new GameObject("Directional Light");
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(1f, 0.85f, 0.75f);
        sunLight.intensity = 0.6f;
        sunLight.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.4f, 0.4f);
        RenderSettings.ambientEquatorColor = new Color(0.4f, 0.25f, 0.25f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.1f, 0.1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.12f, 0.03f, 0.04f);
        RenderSettings.fogDensity = 0.012f;

        // Лампы над аренами и в комнате
        GameObject lights = new GameObject("Arena Lights");

        foreach (ArenaTrigger trigger in mapInstance.GetComponentsInChildren<ArenaTrigger>(true))
        {
            Bounds b = GetBounds(trigger.gameObject);
            AddPointLight(lights.transform, b.center + Vector3.up * 3f, Mathf.Max(b.size.x, b.size.z) * 0.9f, new Color(1f, 0.55f, 0.4f), 3f);
        }

        foreach (UpgradeMachine machine in mapInstance.GetComponentsInChildren<UpgradeMachine>(true))
            AddPointLight(lights.transform, machine.transform.position + Vector3.up * 3f, 12f, new Color(1f, 0.75f, 0.4f), 2.5f);

        foreach (ChipGunPickup pickup in mapInstance.GetComponentsInChildren<ChipGunPickup>(true))
            AddPointLight(lights.transform, pickup.transform.position + Vector3.up * 2.5f, 8f, new Color(1f, 0.85f, 0.4f), 3f);

        foreach (RunStartPortal portal in mapInstance.GetComponentsInChildren<RunStartPortal>(true))
            AddPointLight(lights.transform, portal.transform.position + Vector3.up * 3f, 10f, new Color(1f, 0.3f, 0.3f), 3f);

        // Комната без арен (хаб) — общий тёплый свет по центру
        if (mapInstance.GetComponentsInChildren<ArenaTrigger>(true).Length == 0)
        {
            Bounds all = GetBounds(mapInstance);
            foreach (Renderer r in mapInstance.GetComponentsInChildren<Renderer>())
                all.Encapsulate(r.bounds);

            AddPointLight(lights.transform, all.center, Mathf.Max(all.size.x, all.size.z), new Color(1f, 0.7f, 0.5f), 2f);
        }

        foreach (LevelExit exit in mapInstance.GetComponentsInChildren<LevelExit>(true))
            AddPointLight(lights.transform, GetBounds(exit.gameObject).center + Vector3.up * 2f, 12f, new Color(1f, 0.9f, 0.6f), 4f);

        // Пост-обработка из SampleScene, если есть
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
        if (profile != null)
        {
            GameObject volumeGo = new GameObject("Global Volume");
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // UI-события для меню
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        EditorSceneManager.SaveScene(scene, scenePath);

        AddSceneToBuild(scenePath, buildIndex);

        if (mapInstance.GetComponentInChildren<PlayerStats>(true) == null)
        {
            Debug.LogWarning("Sin City: в карте нет игрока (энтити PlayerPrefab1). Поставь его в TrenchBroom в комнату.");
        }

        Debug.Log($"Sin City: сцена {scenePath} собрана.");
        return true;
    }

    private static Bounds GetBounds(GameObject go)
    {
        Collider col = go.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds;

        Renderer r = go.GetComponentInChildren<Renderer>();
        if (r != null)
            return r.bounds;

        return new Bounds(go.transform.position, Vector3.one);
    }

    private static void AddPointLight(Transform parent, Vector3 position, float range, Color color, float intensity)
    {
        GameObject go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.color = color;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    private static void AddSceneToBuild(string path, int index)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(Mathf.Clamp(index, 0, scenes.Count), new EditorBuildSettingsScene(path, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
