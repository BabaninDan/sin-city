using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Ведёт забег: грузит комнаты из шаблонов, запускает встречи,
/// выдаёт награды, ставит двери-выбор и переводит между главами.
///
/// Комнаты создаются в той же сцене далеко от комнаты героя (RoomOrigin),
/// предыдущая комната уничтожается. Игрок, его HP и колода переживают переход.
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    /// <summary>Где строятся комнаты забега (далеко от хаба).</summary>
    private static readonly Vector3 RoomOrigin = new Vector3(0f, 0f, 3000f);

    private class RoomLayout
    {
        public Vector3 entry;
        public Vector3 center;
        public readonly List<Vector3> exits = new List<Vector3>();
        public readonly List<EnemySpawn> slots = new List<EnemySpawn>();
    }

    private GameObject currentRoom;
    private RoomLayout layout;
    private readonly List<RunDoor> doors = new List<RunDoor>();
    private bool transitioning;

    // Магазин: товар бросается один раз на визит
    private readonly List<CardData> shopCards = new List<CardData>();
    private readonly List<int> shopPrices = new List<int>();
    private readonly List<bool> shopSold = new List<bool>();
    private int burnPrice;

    private PlayerStats Player => GameManager.Instance != null ? GameManager.Instance.Player : null;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        GameState.EnemyKilled += OnEnemyKilled;
    }

    private void OnDisable()
    {
        GameState.EnemyKilled -= OnEnemyKilled;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnemyKilled(EnemyBase enemy)
    {
        if (RunState.Active)
            RunState.Tokens += Random.Range(5, 16);
    }

    // =========================================================
    // START / END
    // =========================================================

    public void StartRun()
    {
        if (RunState.Active || transitioning || Player == null)
            return;

        int seed = System.Environment.TickCount & 0x7fffffff;

        RunState.Begin(seed, StarterDeck());
        RunGenerator.ResetHistory();
        ApplyDeck();

        Debug.Log($"RunManager: забег начат, сид {seed}");

        StartCoroutine(EnterRoom(new DoorOffer(RoomKind.Combat, RewardKind.Card, 1)));
    }

    /// <summary>Стартовая колода «Новичок»: 2–6 всех мастей, 20 карт.</summary>
    private static List<CardData> StarterDeck()
    {
        CardData[] all = AllCards();
        List<CardData> deck = new List<CardData>();

        foreach (CardData card in all)
        {
            if (card != null && card.rank <= CardRank.Six)
                deck.Add(card);
        }

        return deck;
    }

    private static CardData[] AllCards()
    {
        GameConfig config = GameConfig.Instance;

        if (config != null && config.cards != null && config.cards.Length > 0)
            return config.cards;

        return WeaponController.CreateRuntimeDeck();
    }

    private void ApplyDeck()
    {
        WeaponController weapon = Player != null ? Player.GetComponentInChildren<WeaponController>(true) : null;

        if (weapon != null)
            weapon.SetDeck(RunState.Deck);
    }

    /// <summary>Игрок погиб: незданные души остаются на месте смерти.</summary>
    public void HandleDeath()
    {
        if (!RunState.Active)
            return;

        if (RunState.PocketSouls > 0)
            MetaProgress.SetLostSouls(RunState.PocketSouls, RunState.Chapter, Mathf.Max(1, RunState.Depth));

        RunState.End();
    }

    // =========================================================
    // ROOMS
    // =========================================================

    private IEnumerator EnterRoom(DoorOffer offer)
    {
        transitioning = true;
        GameState.InputBlocked = true;

        yield return Fade(0f, 1f, 0.25f);

        ClearDoors();

        if (currentRoom != null)
        {
            Destroy(currentRoom);
            currentRoom = null;
            yield return null;
        }

        RunState.Depth++;
        RunState.CurrentRoom = offer;

        if (offer.kind == RoomKind.Shop)
            RunState.ShopVisitedThisChapter = true;

        System.Random rng = RunRng.Get(RunState.Seed, RunState.Chapter, RunState.Depth, 2);
        RunState.RoomSuit = RunGenerator.RollRoomSuit(rng);

        GameObject template = RunGenerator.PickTemplate(offer.kind, RunState.Chapter, rng);

        if (template != null)
        {
            currentRoom = Instantiate(template, RoomOrigin, Quaternion.identity);
            currentRoom.name = "Room_" + template.name;
        }
        else
        {
            Debug.LogWarning("RunManager: нет шаблонов комнат в GameConfig — строю запасную коробку. Запусти Sin City → Setup Prototype.");
            currentRoom = BuildFallbackRoom();
        }

        Physics.SyncTransforms();

        layout = ReadLayout(currentRoom);

        BuildNavMesh(currentRoom);
        AddLights(currentRoom);
        PlacePlayer(layout);

        yield return Fade(1f, 0f, 0.3f);

        GameState.InputBlocked = false;
        transitioning = false;

        BeginRoom(offer, rng);
    }

    private void BeginRoom(DoorOffer offer, System.Random rng)
    {
        switch (offer.kind)
        {
            case RoomKind.Combat:
            case RoomKind.Elite:
            case RoomKind.Boss:
            case RoomKind.SoulRetrieval:
            {
                List<SpawnOrder> orders = RunGenerator.BuildEncounter(
                    RunState.Chapter, Mathf.Min(RunState.Depth, 5), offer.kind, layout.slots, RunState.RoomSuit, rng);

                if (orders.Count == 0 || ArenaManager.Instance == null)
                {
                    StartCoroutine(AfterClear(offer));
                    return;
                }

                string label = offer.kind == RoomKind.Boss
                    ? $"БОСС ГЛАВЫ {RunState.Chapter}"
                    : offer.kind == RoomKind.Elite ? "ЭЛИТА"
                    : offer.kind == RoomKind.SoulRetrieval ? "ТВОЯ ДУША ЗДЕСЬ"
                    : $"ГЛАВА {RunState.Chapter} · КОМНАТА {RunState.Depth}";

                ArenaManager.Instance.StartGenerated(
                    $"run_{RunState.Chapter}_{RunState.Depth}",
                    label,
                    orders,
                    () => StartCoroutine(AfterClear(offer)));
                break;
            }

            case RoomKind.Bar:
            {
                if (Player != null)
                    Player.Heal(Player.MaxHealth * 0.4f);

                GameState.Say("БАР: +40% HP", new Color(0.5f, 0.75f, 1f));
                StartCoroutine(AfterClear(offer));
                break;
            }

            case RoomKind.Shop:
                RollShopStock(rng);
                StartCoroutine(OpenAfterDelay(OpenShop));
                break;

            case RoomKind.Roulette:
                StartCoroutine(OpenAfterDelay(OpenRoulette));
                break;
        }
    }

    private IEnumerator OpenAfterDelay(System.Action open)
    {
        yield return new WaitForSeconds(0.4f);
        open();
    }

    private IEnumerator AfterClear(DoorOffer offer)
    {
        if (GameState.PlayerDead || !RunState.Active)
            yield break;

        if (offer.IsFight)
            RunState.Tokens += 30;

        yield return new WaitForSeconds(0.8f);

        if (GameState.PlayerDead || !RunState.Active)
            yield break;

        GrantReward(offer, () => FinishRoom(offer));
    }

    private void FinishRoom(DoorOffer offer)
    {
        if (offer.kind == RoomKind.Boss)
        {
            int banked = RunState.PocketSouls;
            MetaProgress.BankSouls(banked);
            RunState.PocketSouls = 0;

            if (RunState.Chapter >= RunState.Chapters)
            {
                RunState.End();
                GameManager.Instance?.Win();
                return;
            }

            GameState.Say($"ГЛАВА {RunState.Chapter} ПРОЙДЕНА  ·  {banked} душ в кассе", SuitUtil.Gold);
            RunState.NextChapter();

            SpawnDoors(RunGenerator.GenerateOffers(RunState.Chapter, 1), $"ГЛАВА {RunState.Chapter}");
            return;
        }

        SpawnDoors(RunGenerator.GenerateOffers(RunState.Chapter, RunState.Depth + 1), null);
    }

    // =========================================================
    // REWARDS
    // =========================================================

    private void GrantReward(DoorOffer offer, System.Action done)
    {
        switch (offer.reward)
        {
            case RewardKind.Card:
                OfferCards(Mathf.Max(1, offer.amount), done);
                return;

            case RewardKind.Tokens:
                RunState.Tokens += offer.amount;
                GameState.Say($"+{offer.amount} жетонов", new Color(0.5f, 0.85f, 1f));
                break;

            case RewardKind.Souls:
                if (offer.kind == RoomKind.SoulRetrieval)
                {
                    int lost = MetaProgress.TakeLostSouls();
                    RunState.PocketSouls += lost;
                    GameState.Say($"ДУША ВЕРНУЛАСЬ  ·  +{lost}", new Color(0.7f, 0.5f, 1f));
                }
                else
                {
                    MetaProgress.AddSouls(offer.amount);
                    GameState.Say($"+{offer.amount} душ", SuitUtil.Gold);
                }

                break;
        }

        done?.Invoke();
    }

    /// <summary>Выбор 1 из 3 карт в колоду (повторяется times раз).</summary>
    private void OfferCards(int times, System.Action done)
    {
        System.Random rng = RunRng.Get(RunState.Seed, RunState.Chapter, RunState.Depth, 10 + times);

        List<GameHUD.ChoiceOption> options = new List<GameHUD.ChoiceOption>();
        List<CardData> picked = new List<CardData>();

        for (int i = 0; i < 3; i++)
        {
            CardData card = RollCard(rng, picked);
            picked.Add(card);

            options.Add(new GameHUD.ChoiceOption
            {
                label = CardLabel(card),
                description = CardDescription(card),
                onPick = () =>
                {
                    AddCard(card);
                    Next();
                }
            });
        }

        options.Add(new GameHUD.ChoiceOption
        {
            label = "ПРОПУСТИТЬ",
            description = "+15 жетонов\n\nТонкая колода — старшие карты приходят чаще",
            onPick = () =>
            {
                RunState.Tokens += 15;
                Next();
            }
        });

        void Next()
        {
            if (times > 1)
                OfferCards(times - 1, done);
            else
                done?.Invoke();
        }

        if (GameHUD.Instance != null)
            GameHUD.Instance.ShowChoice("НАГРАДА: КАРТА", $"Колода: {RunState.Deck.Count} карт", options);
        else
            Next();
    }

    private static CardData RollCard(System.Random rng, List<CardData> exclude)
    {
        CardData[] all = AllCards();

        for (int attempt = 0; attempt < 20; attempt++)
        {
            bool high = rng.Value() < 0.25f;
            CardData card = all[rng.Next(0, all.Length)];

            if (card == null || exclude.Contains(card))
                continue;

            if (high != card.IsSpecialCard && attempt < 15)
                continue;

            if (!high && card.rank < CardRank.Five && attempt < 10)
                continue;

            return card;
        }

        return all[rng.Next(0, all.Length)];
    }

    private void AddCard(CardData card)
    {
        RunState.Deck.Add(card);
        ApplyDeck();
        GameState.Say($"+ {card.GetDisplayName()} в колоду", card.IsRed ? SuitUtil.Red : new Color(0.8f, 0.85f, 1f));
    }

    private static string CardLabel(CardData card)
    {
        string color = card.IsRed ? "#FF3040" : "#E8E8F0";
        return $"<color={color}><size=150%>{card.GetDisplayName()}</size></color>";
    }

    private static string CardDescription(CardData card)
    {
        string effect = card.GetEffectName();
        string text = $"{card.suit.RussianName()}, урон {Mathf.RoundToInt(card.GetBaseDamage())}";

        if (!string.IsNullOrEmpty(effect))
            text += $"\n\n<color=#FFD24A>{effect}</color>";

        return text;
    }

    // =========================================================
    // SHOP
    // =========================================================

    private void RollShopStock(System.Random rng)
    {
        shopCards.Clear();
        shopPrices.Clear();
        shopSold.Clear();

        List<CardData> picked = new List<CardData>();

        for (int i = 0; i < 2; i++)
        {
            CardData card = RollCard(rng, picked);
            picked.Add(card);
            shopCards.Add(card);
            shopPrices.Add(card.IsSpecialCard ? 90 : 35);
            shopSold.Add(false);
        }

        burnPrice = 50;
    }

    private void OpenShop()
    {
        List<GameHUD.ChoiceOption> options = new List<GameHUD.ChoiceOption>();

        options.Add(ShopItem("ЛЕЧЕНИЕ", "+30 HP", 40, () => Player != null && Player.Health < Player.MaxHealth, () => Player.Heal(30f)));

        for (int i = 0; i < shopCards.Count; i++)
        {
            int index = i;
            CardData card = shopCards[i];

            options.Add(ShopItem(
                shopSold[i] ? "ПРОДАНО" : CardLabel(card),
                shopSold[i] ? "" : CardDescription(card),
                shopPrices[i],
                () => !shopSold[index],
                () =>
                {
                    shopSold[index] = true;
                    AddCard(card);
                }));
        }

        PlayerArsenal arsenal = Player != null ? Player.GetComponent<PlayerArsenal>() : null;

        if (arsenal != null && arsenal.ChipGunUnlocked)
            options.Add(ShopItem("50 ФИШЕК", "Патроны для пулемёта", 30, () => true, () => arsenal.AddChipAmmo(50)));

        options.Add(ShopItem("СЖЕЧЬ КАРТУ", "Убирает самую слабую карту из колоды", burnPrice,
            () => RunState.Deck.Count > 8,
            () =>
            {
                CardData weakest = null;

                foreach (CardData c in RunState.Deck)
                {
                    if (weakest == null || c.rank < weakest.rank)
                        weakest = c;
                }

                if (weakest != null)
                {
                    RunState.Deck.Remove(weakest);
                    ApplyDeck();
                    GameState.Say($"{weakest.GetDisplayName()} сгорела", new Color(1f, 0.5f, 0.2f));
                }

                burnPrice += 25;
            }));

        options.Add(new GameHUD.ChoiceOption
        {
            label = "УЙТИ",
            description = "Дальше — к дверям",
            onPick = () => StartCoroutine(AfterClear(RunState.CurrentRoom))
        });

        GameHUD.Instance?.ShowChoice("МАГАЗИН КРУПЬЕ", $"◆ Жетоны: {RunState.Tokens}    Колода: {RunState.Deck.Count}", options);
    }

    private GameHUD.ChoiceOption ShopItem(string label, string description, int price, System.Func<bool> available, System.Action buy)
    {
        bool canBuy = available() && RunState.Tokens >= price;

        return new GameHUD.ChoiceOption
        {
            label = label,
            description = $"{description}\n\n<color=#7FD0FF>◆ {price}</color>",
            enabled = canBuy,
            keepOpen = true,
            onPick = () =>
            {
                if (RunState.Tokens < price)
                    return;

                RunState.Tokens -= price;
                buy();
                OpenShop(); // перерисовать с новыми ценами и остатком
            }
        };
    }

    // =========================================================
    // ROULETTE
    // =========================================================

    private void OpenRoulette()
    {
        List<GameHUD.ChoiceOption> options = new List<GameHUD.ChoiceOption>
        {
            new GameHUD.ChoiceOption
            {
                label = "КРУТИТЬ",
                description = "Ставка: 15 HP\n\nЖетоны, карта, старшая карта, души... или проклятие",
                enabled = Player != null && Player.Health > 16f,
                onPick = SpinRoulette
            },
            new GameHUD.ChoiceOption
            {
                label = "ПРОЙТИ МИМО",
                description = "Дом не всегда в плюсе",
                onPick = () => StartCoroutine(AfterClear(RunState.CurrentRoom))
            }
        };

        GameHUD.Instance?.ShowChoice("РУЛЕТКА", "Ставишь кровь — получаешь шанс", options);
    }

    private void SpinRoulette()
    {
        System.Random rng = RunRng.Get(RunState.Seed, RunState.Chapter, RunState.Depth, 77);
        float roll = rng.Value();

        // Ставка и проклятие — одним ударом (после удара есть короткая неуязвимость)
        if (Player != null)
            Player.TakeDamage(roll >= 0.9f ? 35f : 15f);

        if (GameState.PlayerDead)
            return;

        if (roll < 0.35f)
        {
            RunState.Tokens += 60;
            GameState.Say("РУЛЕТКА: +60 жетонов", new Color(0.5f, 0.85f, 1f));
        }
        else if (roll < 0.6f)
        {
            AddCard(RollCard(rng, new List<CardData>()));
        }
        else if (roll < 0.75f)
        {
            CardData[] all = AllCards();
            CardData high = null;

            for (int i = 0; i < 30 && (high == null || !high.IsSpecialCard); i++)
                high = all[rng.Next(0, all.Length)];

            AddCard(high);
        }
        else if (roll < 0.9f)
        {
            MetaProgress.AddSouls(25);
            GameState.Say("РУЛЕТКА: +25 душ", SuitUtil.Gold);
        }
        else
        {
            GameState.Say("РУЛЕТКА: ПРОКЛЯТИЕ  ·  −20 HP", SuitUtil.Red);
        }

        StartCoroutine(AfterClear(RunState.CurrentRoom));
    }

    // =========================================================
    // DOORS
    // =========================================================

    private void SpawnDoors(List<DoorOffer> offers, string extraLine)
    {
        ClearDoors();

        List<Vector3> spots = new List<Vector3>(layout.exits);

        // В шаблоне нет выходов — ставим двери полукругом за центром
        if (spots.Count == 0)
        {
            Vector3 away = layout.center - layout.entry;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;

            Vector3 side = Vector3.Cross(Vector3.up, away);

            for (int i = 0; i < offers.Count; i++)
                spots.Add(layout.center + away * 6f + side * (i - (offers.Count - 1) * 0.5f) * 4f);
        }

        int count = Mathf.Min(offers.Count, spots.Count);

        // Если выходов больше, чем дверей, — берём центральные
        int start = (spots.Count - count) / 2;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = spots[start + i];
            Vector3 facing = layout.center - pos;

            DoorOffer offer = offers[i];
            string extra = extraLine;

            if (offer.kind == RoomKind.SoulRetrieval)
                extra = "здесь ты погиб";

            RunDoor door = RunDoor.Create(pos, facing, offer, OnDoorEntered, extra);
            door.transform.SetParent(currentRoom != null ? currentRoom.transform : null, true);
            doors.Add(door);
        }

        GameState.Say(count > 1 ? "ВЫБЕРИ ДВЕРЬ" : "ДАЛЬШЕ", new Color(0.9f, 0.85f, 0.75f));
    }

    private void OnDoorEntered(DoorOffer offer)
    {
        if (transitioning)
            return;

        StartCoroutine(EnterRoom(offer));
    }

    private void ClearDoors()
    {
        foreach (RunDoor door in doors)
        {
            if (door != null)
                Destroy(door.gameObject);
        }

        doors.Clear();
    }

    // =========================================================
    // ROOM SETUP
    // =========================================================

    private static RoomLayout ReadLayout(GameObject room)
    {
        RoomLayout result = new RoomLayout();

        Bounds bounds = new Bounds(room.transform.position, Vector3.one);
        bool hasBounds = false;

        foreach (Renderer r in room.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        RoomEntry entry = room.GetComponentInChildren<RoomEntry>(true);
        result.entry = entry != null ? entry.transform.position : bounds.center;

        result.center = new Vector3(bounds.center.x, result.entry.y, bounds.center.z);

        List<RoomExit> exits = new List<RoomExit>(room.GetComponentsInChildren<RoomExit>(true));
        exits.Sort((a, b) => a.Slot.CompareTo(b.Slot));

        foreach (RoomExit exit in exits)
            result.exits.Add(exit.transform.position);

        result.slots.AddRange(room.GetComponentsInChildren<EnemySpawn>(true));

        return result;
    }

    private void PlacePlayer(RoomLayout room)
    {
        PlayerStats player = Player;

        if (player == null)
            return;

        Vector3 pos = room.entry;

        // Ставим на пол под точкой входа
        if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            pos = hit.point;

        Vector3 look = room.center - room.entry;
        look.y = 0f;

        Quaternion rot = look.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(look.normalized, Vector3.up)
            : Quaternion.identity;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();

        CharacterController cc = player.GetComponent<CharacterController>();
        float lift = cc != null ? cc.height * 0.5f - cc.center.y + 0.05f : 1.05f;

        if (movement != null)
            movement.Teleport(pos + Vector3.up * lift, rot);
        else
            player.transform.SetPositionAndRotation(pos + Vector3.up * lift, rot);
    }

    private static void BuildNavMesh(GameObject room)
    {
        NavMeshSurface surface = room.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.BuildNavMesh();
    }

    private static void AddLights(GameObject room)
    {
        Bounds bounds = new Bounds(room.transform.position, Vector3.one);
        bool has = false;

        foreach (Renderer r in room.GetComponentsInChildren<Renderer>())
        {
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!has)
            return;

        int nx = Mathf.Clamp(Mathf.RoundToInt(bounds.size.x / 14f), 1, 3);
        int nz = Mathf.Clamp(Mathf.RoundToInt(bounds.size.z / 14f), 1, 3);

        for (int x = 0; x < nx; x++)
        {
            for (int z = 0; z < nz; z++)
            {
                Vector3 pos = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, (x + 0.5f) / nx),
                    bounds.max.y - 1.5f,
                    Mathf.Lerp(bounds.min.z, bounds.max.z, (z + 0.5f) / nz));

                GameObject go = new GameObject("RoomLight");
                go.transform.SetParent(room.transform, false);
                go.transform.position = pos;

                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 18f;
                light.intensity = 2.5f;
                light.color = new Color(1f, 0.6f, 0.45f);
                light.shadows = LightShadows.None;
            }
        }
    }

    /// <summary>Запасная комната-коробка, если шаблонов ещё нет.</summary>
    private static GameObject BuildFallbackRoom()
    {
        GameObject root = new GameObject("FallbackRoom");
        root.transform.position = RoomOrigin;

        Color floor = new Color(0.25f, 0.08f, 0.1f);
        Color wall = new Color(0.15f, 0.12f, 0.12f);

        Box(root.transform, new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), floor);
        Box(root.transform, new Vector3(0f, 3f, 20.5f), new Vector3(42f, 6f, 1f), wall);
        Box(root.transform, new Vector3(0f, 3f, -20.5f), new Vector3(42f, 6f, 1f), wall);
        Box(root.transform, new Vector3(20.5f, 3f, 0f), new Vector3(1f, 6f, 42f), wall);
        Box(root.transform, new Vector3(-20.5f, 3f, 0f), new Vector3(1f, 6f, 42f), wall);
        Box(root.transform, new Vector3(-7f, 2f, 3f), new Vector3(2f, 4f, 2f), wall);
        Box(root.transform, new Vector3(7f, 2f, 3f), new Vector3(2f, 4f, 2f), wall);

        GameObject entry = new GameObject("Entry");
        entry.transform.SetParent(root.transform, false);
        entry.transform.localPosition = new Vector3(0f, 0.2f, -16f);
        entry.AddComponent<RoomEntry>();

        Vector3[] spawnPoints =
        {
            new Vector3(-12f, 0.2f, 12f), new Vector3(0f, 0.2f, 14f), new Vector3(12f, 0.2f, 12f),
            new Vector3(-14f, 0.2f, 0f), new Vector3(14f, 0.2f, 0f), new Vector3(0f, 0.2f, 6f)
        };

        foreach (Vector3 p in spawnPoints)
        {
            GameObject s = new GameObject("Spawn");
            s.transform.SetParent(root.transform, false);
            s.transform.localPosition = p;
            s.AddComponent<EnemySpawn>().SetRole(SpawnRole.Any);
        }

        return root;
    }

    private static void Box(Transform parent, Vector3 localPos, Vector3 size, Color color)
    {
        GameObject go = FX.Primitive(PrimitiveType.Cube, Vector3.zero, Vector3.one, color, true, parent);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
    }

    // =========================================================
    // FADE
    // =========================================================

    private static IEnumerator Fade(float from, float to, float duration)
    {
        GameHUD hud = GameHUD.Instance;

        if (hud == null)
            yield break;

        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            hud.SetFade(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }

        hud.SetFade(to);
    }
}
