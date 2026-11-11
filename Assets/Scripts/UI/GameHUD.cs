using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD прототипа, собранный кодом (не требует настройки в префабе):
/// карта в руке, колода, джокер, фишки, души, арены, сообщения,
/// хитмаркер, урон, экран смерти/победы, пауза и меню улучшений.
///
/// Существующий PlayerHUD (HP / броня / стамина) продолжает работать.
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    private static readonly Color PanelColor = new Color(0.05f, 0.02f, 0.03f, 0.85f);
    private static readonly Color CardWhite = new Color(0.97f, 0.95f, 0.9f);

    private Canvas canvas;
    private RectTransform root;

    // Карта
    private RectTransform cardPanel;
    private Image cardImage;
    private Image cardBorder;
    private TMP_Text cardRank;
    private TMP_Text cardSuit;
    private TMP_Text cardEffect;
    private Image nextCardImage;
    private TMP_Text nextCardText;
    private TMP_Text deckText;
    private Image reloadFill;
    private GameObject reloadBar;

    // Фишки
    private GameObject chipPanel;
    private TMP_Text chipText;
    private TMP_Text slotsText;

    // Джокер
    private Image jokerFill;
    private TMP_Text jokerText;

    // Души / арена
    private TMP_Text soulsText;
    private TMP_Text arenaText;
    private TMP_Text hpText;

    // Сообщения
    private TMP_Text announceText;
    private float announceTimer;

    // Хитмаркер
    private TMP_Text hitMarker;
    private float hitTimer;

    // Урон / вспышки
    private Image damageVignette;
    private float damageAlpha;
    private Image flash;
    private float flashAlpha;
    private Color flashColor;

    // Взаимодействие
    private TMP_Text promptText;

    // Экраны
    private GameObject deathScreen;
    private TMP_Text deathStats;
    private GameObject victoryScreen;
    private TMP_Text victoryStats;
    private GameObject pauseScreen;
    private GameObject upgradeScreen;
    private TMP_Text upgradeSouls;
    private readonly UpgradeRow[] upgradeRows = new UpgradeRow[4];

    private PlayerStats player;
    private PlayerArsenal arsenal;
    private JokerAbility joker;
    private PlayerInteractor interactor;

    public bool UpgradeMenuOpen => upgradeScreen != null && upgradeScreen.activeSelf;

    // Забег: окно выбора, затемнение, строка забега
    private GameObject choiceScreen;
    private RectTransform choiceList;
    private TMP_Text choiceTitle;
    private TMP_Text choiceSubtitle;
    private Image fadeImage;
    private float fadeAlpha;
    private TMP_Text runText;

    public bool ChoiceOpen => choiceScreen != null && choiceScreen.activeSelf;

    /// <summary>Вариант в окне выбора.</summary>
    public class ChoiceOption
    {
        public string label;
        public string description;
        public bool enabled = true;
        public System.Action onPick;
        /// <summary>Не закрывать окно после выбора (магазин: купить ещё).</summary>
        public bool keepOpen;
    }

    private class UpgradeRow
    {
        public MetaProgress.Upgrade upgrade;
        public TMP_Text title;
        public TMP_Text level;
        public TMP_Text costText;
        public Button button;
    }

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        Instance = this;

        BuildCanvas();
        BuildCardPanel();
        BuildJoker();
        BuildTopBar();
        BuildCenter();
        BuildDeathScreen();
        BuildVictoryScreen();
        BuildPauseScreen();
        BuildUpgradeScreen();
        BuildRunUI();

        GameState.Announce += OnAnnounce;
        HitFeedback.Hit += OnHit;
    }

    private void Start()
    {
        BindPlayer();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        HitFeedback.Hit -= OnHit;
    }

    private void BindPlayer()
    {
        if (GameManager.Instance == null || GameManager.Instance.Player == null)
            return;

        player = GameManager.Instance.Player;
        arsenal = player.GetComponent<PlayerArsenal>();
        joker = player.GetComponent<JokerAbility>();
        interactor = player.GetComponent<PlayerInteractor>();

        player.Damaged += OnPlayerDamaged;

        if (joker != null)
            joker.Used += OnJokerUsed;
    }

    private void Update()
    {
        if (player == null)
            BindPlayer();

        float dt = Time.unscaledDeltaTime;

        UpdateWeapon();
        UpdateJoker();
        UpdateTopBar();
        UpdateEffects(dt);
        UpdatePrompt();
    }

    // =========================================================
    // BUILD HELPERS
    // =========================================================

    private void BuildCanvas()
    {
        GameObject go = new GameObject("GameHUD");
        go.transform.SetParent(transform, false);

        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        root = go.GetComponent<RectTransform>();
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        return rt;
    }

    private static RectTransform Stretch(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return rt;
    }

    private static Image AddImage(RectTransform rt, Color color, bool raycast = false)
    {
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static TMP_Text Text(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions align = TextAlignmentOptions.Center,
        Color? color = null)
    {
        RectTransform rt = Rect(name, parent, anchor, position, size);

        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color ?? Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;

        return text;
    }

    private static Button MakeButton(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rt = Rect("Button_" + label, parent, anchor, position, size);
        Image image = AddImage(rt, new Color(0.55f, 0.08f, 0.1f), true);

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.85f, 0.5f);
        colors.pressedColor = new Color(0.8f, 0.6f, 0.3f);
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f);
        button.colors = colors;

        button.onClick.AddListener(onClick);

        TMP_Text text = Text("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size, 26f);
        text.text = label;
        text.fontStyle = FontStyles.Bold;

        return button;
    }

    // =========================================================
    // CARD / WEAPON
    // =========================================================

    private void BuildCardPanel()
    {
        Vector2 anchor = new Vector2(1f, 0f);

        cardPanel = Rect("CardPanel", root, anchor, new Vector2(-150f, 330f), new Vector2(240f, 330f));

        // Следующая карта (выглядывает сзади)
        RectTransform next = Rect("NextCard", cardPanel, new Vector2(0.5f, 0.5f), new Vector2(38f, 22f), new Vector2(120f, 170f));
        next.localRotation = Quaternion.Euler(0f, 0f, -12f);
        nextCardImage = AddImage(next, new Color(0.85f, 0.83f, 0.78f));
        nextCardText = Text("NextText", next, new Vector2(0.5f, 0.5f), new Vector2(18f, 50f), new Vector2(80f, 50f), 28f, TextAlignmentOptions.TopRight);

        // Текущая карта
        RectTransform border = Rect("CardBorder", cardPanel, new Vector2(0.5f, 0.5f), new Vector2(-10f, 10f), new Vector2(134f, 184f));
        border.localRotation = Quaternion.Euler(0f, 0f, 4f);
        cardBorder = AddImage(border, SuitUtil.Gold);

        RectTransform card = Rect("Card", border, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(126f, 176f));
        cardImage = AddImage(card, CardWhite);

        cardRank = Text("Rank", card, new Vector2(0.5f, 0.5f), new Vector2(-28f, 58f), new Vector2(70f, 60f), 46f);
        cardRank.fontStyle = FontStyles.Bold;

        cardSuit = Text("Suit", card, new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(120f, 110f), 92f);

        cardEffect = Text("Effect", cardPanel, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(300f, 30f), 20f, TextAlignmentOptions.Center, SuitUtil.Gold);

        deckText = Text("Deck", cardPanel, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(260f, 34f), 26f);
        deckText.fontStyle = FontStyles.Bold;

        // Полоса перезарядки
        RectTransform bar = Rect("ReloadBar", cardPanel, new Vector2(0.5f, 0f), new Vector2(0f, -6f), new Vector2(180f, 8f));
        AddImage(bar, new Color(0f, 0f, 0f, 0.7f));
        reloadBar = bar.gameObject;

        RectTransform fill = Rect("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(180f, 8f));
        fill.pivot = new Vector2(0f, 0.5f);
        reloadFill = AddImage(fill, SuitUtil.Gold);
        reloadFill.type = Image.Type.Filled;
        reloadFill.fillMethod = Image.FillMethod.Horizontal;

        // Пулемёт фишек
        RectTransform chips = Rect("ChipPanel", root, anchor, new Vector2(-150f, 330f), new Vector2(260f, 120f));
        AddImage(chips, PanelColor);
        chipPanel = chips.gameObject;
        chipText = Text("Chips", chips, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 110f), 34f);
        chipText.fontStyle = FontStyles.Bold;
        chipPanel.SetActive(false);

        slotsText = Text("Slots", root, anchor, new Vector2(-150f, 140f), new Vector2(280f, 30f), 20f);
    }

    private void UpdateWeapon()
    {
        if (arsenal == null)
        {
            cardPanel.gameObject.SetActive(false);
            return;
        }

        bool chipsActive = arsenal.Current == PlayerArsenal.Slot.Chips;

        cardPanel.gameObject.SetActive(!chipsActive);
        chipPanel.SetActive(chipsActive);

        string cardsSlot = chipsActive ? "<color=#888>1 КАРТЫ</color>" : "<color=#FFD24A>1 КАРТЫ</color>";
        string chipsSlot = arsenal.ChipGunUnlocked
            ? (chipsActive ? "<color=#FFD24A>2 ФИШКИ</color>" : "<color=#888>2 ФИШКИ</color>")
            : "<color=#444>2 ???</color>";

        slotsText.text = cardsSlot + "    " + chipsSlot;

        if (chipsActive && arsenal.Chips != null)
        {
            int ammo = arsenal.Chips.Ammo;
            string color = ammo == 0 ? "#FF4040" : ammo < 20 ? "#FFA040" : "#FFFFFF";
            chipText.text = $"<size=20>ПУЛЕМЁТ ФИШЕК</size>\n<color={color}>{ammo}</color> <size=22>/ {arsenal.Chips.MaxAmmo}</size>";
            return;
        }

        WeaponController cards = arsenal.Cards;

        if (cards == null)
            return;

        CardData card = cards.CurrentCard;

        if (card != null && !cards.IsReloading)
        {
            Color c = card.suit.CardColor();
            cardRank.text = card.GetRankName();
            cardRank.color = c;
            cardSuit.text = card.suit.Symbol();
            cardSuit.color = c;
            cardImage.color = CardWhite;
            cardBorder.color = card.IsSpecialCard ? SuitUtil.Gold : new Color(0.15f, 0.1f, 0.1f);
            cardEffect.text = card.GetEffectName();
        }
        else
        {
            cardRank.text = "";
            cardSuit.text = cards.IsReloading ? "..." : "";
            cardSuit.color = new Color(0.4f, 0.1f, 0.1f);
            cardImage.color = new Color(0.45f, 0.08f, 0.1f);
            cardBorder.color = new Color(0.15f, 0.1f, 0.1f);
            cardEffect.text = cards.IsReloading ? "Тасую колоду..." : "";
        }

        CardData next = cards.NextCard;
        if (next != null && !cards.IsReloading)
        {
            nextCardImage.gameObject.SetActive(true);
            nextCardText.text = next.GetDisplayName();
            nextCardText.color = next.suit.CardColor();
        }
        else
        {
            nextCardImage.gameObject.SetActive(false);
        }

        int ammoLeft = cards.CurrentAmmo;
        string ammoColor = ammoLeft <= 3 ? "#FF5050" : "#FFFFFF";
        deckText.text = $"<color={ammoColor}>{ammoLeft}</color> / {cards.MaxAmmo}  <size=18><color=#AAA>[R]</color></size>";

        reloadBar.SetActive(cards.IsReloading);
        reloadFill.fillAmount = cards.ReloadProgress;
    }

    // =========================================================
    // JOKER
    // =========================================================

    private void BuildJoker()
    {
        Vector2 anchor = new Vector2(0.5f, 0f);

        RectTransform back = Rect("JokerBar", root, anchor, new Vector2(0f, 60f), new Vector2(460f, 22f));
        AddImage(back, new Color(0f, 0f, 0f, 0.75f));

        RectTransform fill = Rect("Fill", back, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(454f, 16f));
        fill.pivot = new Vector2(0f, 0.5f);
        jokerFill = AddImage(fill, SuitUtil.Gold);
        jokerFill.type = Image.Type.Filled;
        jokerFill.fillMethod = Image.FillMethod.Horizontal;

        jokerText = Text("JokerText", root, anchor, new Vector2(0f, 92f), new Vector2(600f, 36f), 24f);
        jokerText.fontStyle = FontStyles.Bold;
    }

    private void UpdateJoker()
    {
        if (joker == null)
        {
            jokerText.text = "";
            jokerFill.fillAmount = 0f;
            return;
        }

        bool red = joker.NextIsRed;
        string suits = red ? "<color=#FF3040>♥♦</color>" : "<color=#9AA0C0>♣♠</color>";

        jokerFill.fillAmount = joker.Charge01;

        if (joker.IsReady)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            jokerFill.color = Color.Lerp(red ? SuitUtil.Red : new Color(0.4f, 0.45f, 0.7f), SuitUtil.Gold, pulse);
            jokerText.text = $"{(red ? "КРАСНЫЙ" : "ЧЁРНЫЙ")} ДЖОКЕР ГОТОВ {suits}  <color=#FFD24A>[F]</color>";
        }
        else
        {
            jokerFill.color = red ? new Color(0.7f, 0.12f, 0.15f) : new Color(0.3f, 0.32f, 0.45f);
            jokerText.text = $"<size=20>ДЖОКЕР {suits}  {Mathf.FloorToInt(joker.Charge01 * 100f)}%</size>";
        }
    }

    // =========================================================
    // TOP BAR
    // =========================================================

    private void BuildTopBar()
    {
        soulsText = Text("Souls", root, new Vector2(1f, 1f), new Vector2(-170f, -40f), new Vector2(320f, 50f), 32f, TextAlignmentOptions.Right, SuitUtil.Gold);
        soulsText.fontStyle = FontStyles.Bold;

        arenaText = Text("Arena", root, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 40f), 26f);

        // Подсказка по здоровью рядом с джокером (на случай, если PlayerHUD не настроен)
        hpText = Text("HP", root, new Vector2(0f, 1f), new Vector2(170f, -40f), new Vector2(320f, 40f), 26f, TextAlignmentOptions.Left);
    }

    private void UpdateTopBar()
    {
        if (RunState.Active)
        {
            soulsText.text = $"● ДУШИ: {MetaProgress.Souls} <color=#C8A040>(+{RunState.PocketSouls})</color>\n<size=24><color=#7FD0FF>◆ ЖЕТОНЫ: {RunState.Tokens}</color></size>";

            string depth = RunState.Depth >= RunState.DepthsPerChapter
                ? "<color=#FF4040>БОСС</color>"
                : $"комната {RunState.Depth}/{RunState.DepthsPerChapter - 1}";

            runText.text = $"ГЛАВА {RunState.Chapter}  ·  {depth}  ·  масть зала {RunState.RoomSuit.Symbol()}  <size=16><color=#777>сид {RunState.Seed}</color></size>";
        }
        else
        {
            soulsText.text = $"● ДУШИ: {MetaProgress.Souls}";
            runText.text = "";
        }

        ArenaManager arenas = ArenaManager.Instance;

        if (arenas != null && !string.IsNullOrEmpty(arenas.CurrentArenaId))
        {
            arenaText.text = $"<color=#FF5050>БОЙ</color>   волна {arenas.CurrentWave}/{Mathf.Max(1, arenas.MaxWave)}   врагов: {arenas.AliveInCurrentArena}";
        }
        else if (arenas != null && arenas.ArenaCount > 0)
        {
            arenaText.text = $"<color=#AAA>Арен зачищено: {arenas.ClearedCount}/{arenas.ArenaCount}</color>";
        }
        else
        {
            arenaText.text = "";
        }

        if (player != null)
        {
            string shield = player.Shield > 0.5f ? $"   <color=#8CB4FF>БРОНЯ {Mathf.CeilToInt(player.Shield)}</color>" : "";
            hpText.text = $"<color=#FF4050>♥</color> {Mathf.CeilToInt(player.Health)}/{Mathf.CeilToInt(player.MaxHealth)}{shield}";
        }
    }

    // =========================================================
    // CENTER: announce, hitmarker, prompt, overlays
    // =========================================================

    private void BuildCenter()
    {
        Vector2 center = new Vector2(0.5f, 0.5f);

        damageVignette = AddImage(Stretch("DamageVignette", root), new Color(0.8f, 0f, 0f, 0f));
        flash = AddImage(Stretch("Flash", root), new Color(1f, 1f, 1f, 0f));

        announceText = Text("Announce", root, center, new Vector2(0f, 220f), new Vector2(1400f, 90f), 56f);
        announceText.fontStyle = FontStyles.Bold;
        announceText.outlineWidth = 0.2f;
        announceText.outlineColor = new Color32(0, 0, 0, 255);

        hitMarker = Text("HitMarker", root, center, Vector2.zero, new Vector2(80f, 80f), 44f);
        hitMarker.text = "✖";
        hitMarker.alpha = 0f;

        promptText = Text("Prompt", root, center, new Vector2(0f, -90f), new Vector2(800f, 40f), 28f);
    }

    private void OnAnnounce(string text, Color color)
    {
        announceText.text = text;
        announceText.color = color;
        announceTimer = 1.8f;
    }

    private void OnHit(float damage, bool suitMatch, bool kill)
    {
        hitTimer = kill ? 0.3f : 0.15f;
        hitMarker.color = kill ? new Color(1f, 0.2f, 0.2f) : suitMatch ? SuitUtil.Gold : Color.white;
        hitMarker.fontSize = kill ? 60f : suitMatch ? 50f : 40f;
    }

    private void OnPlayerDamaged(float damage, Vector3 direction)
    {
        damageAlpha = Mathf.Clamp(damageAlpha + damage / 40f, 0.2f, 0.6f);
    }

    private void OnJokerUsed(bool red, int targets)
    {
        Flash(red ? new Color(1f, 0.1f, 0.15f) : new Color(0.05f, 0.05f, 0.1f), 0.6f);
    }

    public void Flash(Color color, float alpha)
    {
        flashColor = color;
        flashAlpha = alpha;
    }

    private void UpdateEffects(float dt)
    {
        if (announceTimer > 0f)
        {
            announceTimer -= dt;
            float a = Mathf.Clamp01(announceTimer / 0.4f);
            announceText.alpha = a;

            float pop = Mathf.Clamp01((1.8f - announceTimer) / 0.12f);
            announceText.transform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, pop);
        }
        else
        {
            announceText.alpha = 0f;
        }

        if (hitTimer > 0f)
        {
            hitTimer -= dt;
            hitMarker.alpha = Mathf.Clamp01(hitTimer / 0.1f);
        }
        else
        {
            hitMarker.alpha = 0f;
        }

        damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, dt * 1.2f);
        damageVignette.color = new Color(0.7f, 0f, 0f, damageAlpha * 0.5f);

        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.5f);
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);

        if (fadeImage != null)
            fadeImage.color = new Color(0f, 0f, 0f, fadeAlpha);
    }

    private void UpdatePrompt()
    {
        if (interactor != null && interactor.Current != null && !GameState.InputBlocked)
            promptText.text = interactor.Current.Prompt;
        else
            promptText.text = "";
    }

    // =========================================================
    // SCREENS
    // =========================================================

    private RectTransform MakeScreen(string name, Color background)
    {
        RectTransform rt = Stretch(name, root);
        AddImage(rt, background, true);
        rt.gameObject.SetActive(false);
        return rt;
    }

    private void BuildDeathScreen()
    {
        RectTransform screen = MakeScreen("DeathScreen", new Color(0.1f, 0f, 0f, 0.88f));
        deathScreen = screen.gameObject;

        Vector2 c = new Vector2(0.5f, 0.5f);

        TMP_Text title = Text("Title", screen, c, new Vector2(0f, 200f), new Vector2(1400f, 140f), 110f, TextAlignmentOptions.Center, new Color(0.9f, 0.1f, 0.1f));
        title.text = "ТЫ МЁРТВ";
        title.fontStyle = FontStyles.Bold;

        TMP_Text sub = Text("Sub", screen, c, new Vector2(0f, 100f), new Vector2(1400f, 50f), 30f, TextAlignmentOptions.Center, new Color(0.8f, 0.7f, 0.7f));
        sub.text = "Казино не отпускает. Душа возвращается в твою комнату.";

        deathStats = Text("Stats", screen, c, new Vector2(0f, -40f), new Vector2(900f, 220f), 30f);

        TMP_Text hint = Text("Hint", screen, c, new Vector2(0f, -220f), new Vector2(1400f, 50f), 30f, TextAlignmentOptions.Center, SuitUtil.Gold);
        hint.text = "[R] / [ЛКМ] — снова за стол";
    }

    public void ShowDeath()
    {
        float time = Time.time - GameState.RunStartTime;

        deathStats.text =
            $"Убито: <color=#FFD24A>{GameState.RunKills}</color>\n" +
            $"Души за забег: <color=#FFD24A>{GameState.RunSouls}</color>    всего: <color=#FFD24A>{MetaProgress.Souls}</color>\n" +
            $"Время: {Mathf.FloorToInt(time / 60f)}:{Mathf.FloorToInt(time % 60f):00}    Смертей: {MetaProgress.Deaths}\n" +
            "<size=24><color=#AAA>Потрать души на автомате в комнате</color></size>";

        CloseUpgradeMenu();
        ShowPause(false);
        deathScreen.SetActive(true);
    }

    private void BuildVictoryScreen()
    {
        RectTransform screen = MakeScreen("VictoryScreen", new Color(0.05f, 0.03f, 0f, 0.9f));
        victoryScreen = screen.gameObject;

        Vector2 c = new Vector2(0.5f, 0.5f);

        TMP_Text title = Text("Title", screen, c, new Vector2(0f, 200f), new Vector2(1400f, 140f), 90f, TextAlignmentOptions.Center, SuitUtil.Gold);
        title.text = "ГЛАВА ПРОЙДЕНА";
        title.fontStyle = FontStyles.Bold;

        TMP_Text sub = Text("Sub", screen, c, new Vector2(0f, 100f), new Vector2(1400f, 50f), 30f, TextAlignmentOptions.Center, new Color(0.9f, 0.85f, 0.7f));
        sub.text = "Дьявол ждёт дальше. Конец прототипа — спасибо за игру!";

        victoryStats = Text("Stats", screen, c, new Vector2(0f, -40f), new Vector2(900f, 200f), 30f);

        TMP_Text hint = Text("Hint", screen, c, new Vector2(0f, -220f), new Vector2(1400f, 50f), 30f, TextAlignmentOptions.Center, SuitUtil.Gold);
        hint.text = "[R] — сыграть ещё раз";
    }

    public void ShowVictory()
    {
        float time = Time.time - GameState.RunStartTime;

        victoryStats.text =
            $"Убито: <color=#FFD24A>{GameState.RunKills}</color>\n" +
            $"Души за забег: <color=#FFD24A>{GameState.RunSouls}</color> (+50 за главу)\n" +
            $"Время: {Mathf.FloorToInt(time / 60f)}:{Mathf.FloorToInt(time % 60f):00}    Смертей всего: {MetaProgress.Deaths}";

        victoryScreen.SetActive(true);
    }

    private void BuildPauseScreen()
    {
        RectTransform screen = MakeScreen("PauseScreen", new Color(0f, 0f, 0f, 0.8f));
        pauseScreen = screen.gameObject;

        Vector2 c = new Vector2(0.5f, 0.5f);

        TMP_Text title = Text("Title", screen, c, new Vector2(0f, 300f), new Vector2(1000f, 100f), 72f, TextAlignmentOptions.Center, SuitUtil.Gold);
        title.text = "ГОРОД ГРЕХА";
        title.fontStyle = FontStyles.Bold;

        TMP_Text controls = Text("Controls", screen, c, new Vector2(0f, 40f), new Vector2(900f, 380f), 26f, TextAlignmentOptions.Left);
        controls.text =
            "<color=#FFD24A>WASD</color> — движение      <color=#FFD24A>Space</color> — прыжок (двойной)\n" +
            "<color=#FFD24A>Shift</color> — рывок            <color=#FFD24A>Ctrl</color> — подкат / быстрое падение\n" +
            "<color=#FFD24A>ЛКМ</color> — бросок карты      <color=#FFD24A>R</color> — перетасовать колоду\n" +
            "<color=#FFD24A>1 / 2 / колесо / Q</color> — карты / пулемёт фишек\n" +
            "<color=#FFD24A>F</color> — джокер (цвет = цвет карты в руке)\n" +
            "<color=#FFD24A>E</color> — взаимодействие\n\n" +
            "<size=22><color=#CCC>Карта своей масти бьёт x2.  J — насквозь, Q — наводится, K — взрыв, A — рикошет.\n" +
            "Красный джокер уничтожает ♥♦ врагов и лечит, чёрный — ♣♠ и даёт броню.</color></size>";

        MakeButton(screen, c, new Vector2(-170f, -230f), new Vector2(300f, 60f), "Продолжить", () => GameManager.Instance?.SetPaused(false));
        MakeButton(screen, c, new Vector2(170f, -230f), new Vector2(300f, 60f), "Сбросить прогресс", () => GameManager.Instance?.ResetProgressAndRestart());
    }

    public void ShowPause(bool show)
    {
        if (pauseScreen != null)
            pauseScreen.SetActive(show);
    }

    // =========================================================
    // UPGRADES
    // =========================================================

    private void BuildUpgradeScreen()
    {
        RectTransform screen = MakeScreen("UpgradeScreen", new Color(0.04f, 0f, 0.02f, 0.9f));
        upgradeScreen = screen.gameObject;

        Vector2 c = new Vector2(0.5f, 0.5f);

        TMP_Text title = Text("Title", screen, c, new Vector2(0f, 330f), new Vector2(1000f, 90f), 64f, TextAlignmentOptions.Center, SuitUtil.Gold);
        title.text = "АВТОМАТ ДУШ";
        title.fontStyle = FontStyles.Bold;

        upgradeSouls = Text("Souls", screen, c, new Vector2(0f, 260f), new Vector2(1000f, 50f), 32f);

        MetaProgress.Upgrade[] list =
        {
            MetaProgress.Upgrade.Health,
            MetaProgress.Upgrade.Damage,
            MetaProgress.Upgrade.Deck,
            MetaProgress.Upgrade.Joker
        };

        for (int i = 0; i < list.Length; i++)
        {
            MetaProgress.Upgrade up = list[i];
            float y = 150f - i * 110f;

            RectTransform row = Rect("Row_" + up, screen, c, new Vector2(0f, y), new Vector2(980f, 95f));
            AddImage(row, new Color(0.15f, 0.03f, 0.05f, 0.95f));

            UpgradeRow r = new UpgradeRow { upgrade = up };

            r.title = Text("Title", row, new Vector2(0f, 0.5f), new Vector2(240f, 0f), new Vector2(440f, 85f), 28f, TextAlignmentOptions.Left);
            r.level = Text("Level", row, new Vector2(0.5f, 0.5f), new Vector2(110f, 0f), new Vector2(220f, 60f), 34f, TextAlignmentOptions.Center, SuitUtil.Gold);
            r.button = MakeButton(row, new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(200f, 64f), "Купить", () => Buy(up));
            r.costText = r.button.GetComponentInChildren<TMP_Text>();

            upgradeRows[i] = r;
        }

        MakeButton(screen, c, new Vector2(0f, -330f), new Vector2(300f, 60f), "Закрыть [Esc]", CloseUpgradeMenu);
    }

    private void Buy(MetaProgress.Upgrade upgrade)
    {
        if (!MetaProgress.TryBuy(upgrade))
            return;

        // Применяем сразу, без перезапуска
        if (upgrade == MetaProgress.Upgrade.Health && player != null)
            player.ApplyUpgrades(MetaProgress.BonusHealth);

        FX.Sparks(player != null ? player.transform.position + Vector3.up : Vector3.zero, SuitUtil.Gold);
        RefreshUpgrades();
    }

    private void RefreshUpgrades()
    {
        upgradeSouls.text = $"● Души: <color=#FFD24A>{MetaProgress.Souls}</color>";

        foreach (UpgradeRow row in upgradeRows)
        {
            if (row == null)
                continue;

            int level = MetaProgress.GetLevel(row.upgrade);
            bool maxed = level >= MetaProgress.MaxLevel;

            row.title.text = $"<b>{MetaProgress.GetTitle(row.upgrade)}</b>\n<size=22><color=#CCC>{MetaProgress.GetDescription(row.upgrade)}</color></size>";
            row.level.text = new string('◆', level) + "<color=#555>" + new string('◇', MetaProgress.MaxLevel - level) + "</color>";
            row.costText.text = maxed ? "МАКС" : $"{MetaProgress.GetCost(row.upgrade)} ●";
            row.button.interactable = !maxed && MetaProgress.CanBuy(row.upgrade);
        }
    }

    public void OpenUpgradeMenu()
    {
        if (GameState.PlayerDead)
            return;

        RefreshUpgrades();
        upgradeScreen.SetActive(true);
        GameState.InputBlocked = true;
        PlayerCamera.SetCursorLocked(false);
    }

    public void CloseUpgradeMenu()
    {
        if (upgradeScreen == null || !upgradeScreen.activeSelf)
            return;

        upgradeScreen.SetActive(false);
        GameState.InputBlocked = false;

        if (!GameState.PlayerDead)
            PlayerCamera.SetCursorLocked(true);
    }
    // =========================================================
    // RUN UI
    // =========================================================

    private void BuildRunUI()
    {
        Vector2 c = new Vector2(0.5f, 0.5f);

        runText = Text("RunInfo", root, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(1100f, 34f), 22f, TextAlignmentOptions.Center, new Color(0.9f, 0.85f, 0.75f));

        // Окно выбора
        RectTransform screen = MakeScreen("ChoiceScreen", new Color(0.03f, 0f, 0.02f, 0.88f));
        choiceScreen = screen.gameObject;

        choiceTitle = Text("Title", screen, c, new Vector2(0f, 300f), new Vector2(1200f, 80f), 56f, TextAlignmentOptions.Center, SuitUtil.Gold);
        choiceTitle.fontStyle = FontStyles.Bold;

        choiceSubtitle = Text("Subtitle", screen, c, new Vector2(0f, 240f), new Vector2(1200f, 40f), 26f, TextAlignmentOptions.Center, new Color(0.85f, 0.8f, 0.75f));

        choiceList = Rect("List", screen, c, new Vector2(0f, -20f), new Vector2(1200f, 420f));

        // Затемнение для переходов — поверх всего
        RectTransform fade = Stretch("Fade", root);
        fadeImage = AddImage(fade, new Color(0f, 0f, 0f, 0f));
        fade.SetAsLastSibling();
    }

    /// <summary>Мгновенно задать затемнение экрана (0 — нет, 1 — чёрный).</summary>
    public void SetFade(float alpha)
    {
        fadeAlpha = Mathf.Clamp01(alpha);

        if (fadeImage != null)
            fadeImage.color = new Color(0f, 0f, 0f, fadeAlpha);
    }

    /// <summary>
    /// Окно выбора: награда-карта, магазин, рулетка.
    /// Варианты — карточки в ряд; клик вызывает onPick.
    /// </summary>
    public void ShowChoice(string title, string subtitle, System.Collections.Generic.List<ChoiceOption> options)
    {
        if (choiceScreen == null)
            return;

        for (int i = choiceList.childCount - 1; i >= 0; i--)
            Destroy(choiceList.GetChild(i).gameObject);

        choiceTitle.text = title;
        choiceSubtitle.text = subtitle;

        int count = options.Count;
        float width = Mathf.Min(320f, 1180f / Mathf.Max(1, count) - 20f);

        for (int i = 0; i < count; i++)
        {
            ChoiceOption option = options[i];
            float x = (i - (count - 1) * 0.5f) * (width + 20f);

            RectTransform card = Rect("Option_" + i, choiceList, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(width, 380f));
            Image bg = AddImage(card, option.enabled ? new Color(0.18f, 0.04f, 0.06f, 0.97f) : new Color(0.12f, 0.1f, 0.1f, 0.9f), true);

            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.interactable = option.enabled;

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.5f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f);
            button.colors = colors;

            TMP_Text label = Text("Label", card, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(width - 24f, 140f), 34f);
            label.text = option.label;
            label.fontStyle = FontStyles.Bold;

            TMP_Text desc = Text("Desc", card, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(width - 28f, 180f), 22f, TextAlignmentOptions.Top, new Color(0.85f, 0.8f, 0.75f));
            desc.text = option.description;

            button.onClick.AddListener(() =>
            {
                if (!option.keepOpen)
                    CloseChoice();

                option.onPick?.Invoke();
            });
        }

        choiceScreen.SetActive(true);
        GameState.InputBlocked = true;
        PlayerCamera.SetCursorLocked(false);
    }

    public void CloseChoice()
    {
        if (choiceScreen == null || !choiceScreen.activeSelf)
            return;

        choiceScreen.SetActive(false);
        GameState.InputBlocked = false;

        if (!GameState.PlayerDead)
            PlayerCamera.SetCursorLocked(true);
    }
}
