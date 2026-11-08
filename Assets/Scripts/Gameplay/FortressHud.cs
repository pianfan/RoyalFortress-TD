using KingdomBorder.TwinTowers;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// The gameplay HUD is built from the scene's existing canvas and tower assets.
// No extra art, font asset or package dependency is required.
public sealed class FortressHud : MonoBehaviour
{
    public WaveGameController game;
    public Text lifeText, waveText, statusText, branchText, buttonText;
    public Button waveButton;
    public Text GoldText { get; private set; }
    public TowerBuildCard[] Cards { get; private set; }
    public bool IsReady => ready;
    public Transform ExtraControlsRoot => gameplayGroup ? gameplayGroup.transform : transform;
    public Transform SkillCardsRoot => skillCardsRoot;
    public bool SkillsVisible { get; private set; }
    public bool DockCollapsed { get; private set; }
    private RectTransform dockRoot, towerCardsRoot, skillCardsRoot;
    private GameObject collapsedDock;
    private Button towerTab, skillTab;
    private Text towerTabLabel, skillTabLabel, collapsedLabel;
    private string towerHint = "拖动塔卡到空坑位，松手购买", skillHint = "火球选点施放 · 攻速强化立即生效";
    private int skillCharges = 3;
    private bool skillsUnlocked;
    private string encounterStatus, encounterAction;
    private bool encounterAllowsRestart;
    private TowerBuildController build;
    private Font runtimeFont;
    private Text hint, waveHint, zoomText;
    private MapCameraController navigation;
    private Canvas canvas;
    private bool ready;
    private Image bossPanel, bossHealth;
    private Text bossTitle, bossWeather;
    private GameObject pauseMenu;
    private CanvasGroup gameplayGroup;
    private Button continueButton;
    private FortressDebugConsole debugConsole;
    private RectTransform towerSalePanel;
    private Text towerSaleTitle, towerSaleHint, towerSaleCaption;
    private Button towerSaleButton;
    private Button speedButton;
    private Text speedCaption;
    private readonly Color ink = new Color32(15, 23, 29, 255);
    private readonly Color cream = new Color32(242, 234, 213, 255);
    private readonly Color muted = new Color32(151, 167, 170, 255);
    private readonly Color gold = new Color32(222, 183, 107, 255);

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        // Keep serialized scene objects available for older scenes, but replace their layout.
        foreach (Transform child in transform) child.gameObject.SetActive(false);
    }
    private void OnEnable() { if (game) game.StateChanged += Refresh; }
    private void Start()
    {
        build = FindFirstObjectByType<TowerBuildController>();
        if (!build || !canvas || !game) return;
        navigation = build.worldCamera ? build.worldCamera.GetComponent<MapCameraController>() : null;
        var scaler = GetComponent<CanvasScaler>();
        if (scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0;
        }
        CreateLayout();
        CreatePauseMenu();
        build.PlacementMessage += ShowMessage;
        ready = true;
        Canvas.ForceUpdateCanvases();
        if (build.worldCamera)
        {
            build.worldCamera.rect = new Rect(0, 0, 1, 1);
            navigation = build.worldCamera.GetComponent<MapCameraController>();
            if (navigation) navigation.ShowInitialView();
        }
        Refresh();
    }
    private void LateUpdate()
    {
        if (ready) RefreshBoss();
        if (ready && navigation && zoomText) zoomText.text = Mathf.RoundToInt(navigation.ZoomPercent) + "%";
        if (ready) RefreshTowerSale();
        if (ready) RefreshGameSpeed();
    }
    private void Update()
    {
        if (!ready || game.StoryOverlayActive) return;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) EscapeMenu();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) EscapeMenu();
#endif
    }
    public void TogglePause() { if (ready) ShowPauseMenu(!game.IsPaused); }
    private void EscapeMenu()
    {
        if (debugConsole && debugConsole.IsOpen) debugConsole.Close();
        else if (build && build.SelectedTowerSite) build.ClearTowerSelection();
        else TogglePause();
    }
    public void ShowPauseMenu(bool show)
    {
        if (!ready || game.StoryOverlayActive) return;
        if (game.TryGetComponent<BattleItemTargeting>(out var targeting)) targeting.Cancel();
        if (debugConsole && debugConsole.IsOpen) debugConsole.Close();
        build.Cancel();
        game.SetPaused(show);
        pauseMenu.SetActive(show);
        gameplayGroup.interactable = !show;
        gameplayGroup.blocksRaycasts = !show;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(show ? continueButton.gameObject : null);
    }
    private void OnDisable()
    {
        if (game) game.StateChanged -= Refresh;
        if (build) build.Cancel();
        if (game && game.IsPaused) game.SetPaused(false);
    }
    private void OnDestroy()
    {
        if (build) build.PlacementMessage -= ShowMessage;
        if (runtimeFont) Destroy(runtimeFont);
    }
    private void CreateLayout()
    {
        Label(transform, game.levelName, 16, gold, 18, 14, 220, 28);
        var menuFace = Panel("Pause Button", transform, new Color32(15, 23, 29, 232), true);
        Place(menuFace.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-18, -14), new Vector2(98, 36));
        var menuButton = menuFace.gameObject.AddComponent<Button>();
        menuButton.targetGraphic = menuFace;
        menuButton.onClick.AddListener(TogglePause);
        Label(menuFace.transform, "菜单 Esc", 14, cream, 0, 0, 98, 36).alignment = TextAnchor.MiddleCenter;
        var header = Panel("Command Bar", transform, new Color32(15, 23, 29, 232), true);
        Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -14), new Vector2(708, 48));
        var line = Panel("Gold Rule", header.transform, gold);
        Place(line.rectTransform, Vector2.zero, Vector2.right, Vector2.zero, Vector2.zero, new Vector2(0, 1));
        Stat(header.transform, "金币", out var goldValue, 14, gold);
        GoldText = goldValue;
        Stat(header.transform, "生命", out lifeText, 174, cream);
        Stat(header.transform, "波次", out waveText, 334, cream);
        statusText = Label(header.transform, "", 14, cream, 484, 4, 134, 22);
        branchText = Label(header.transform, "", 10, muted, 484, 26, 134, 18);
        var speed = Panel("Game Speed", header.transform, new Color32(43, 57, 68, 255), true);
        Place(speed.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(628, -8), new Vector2(68, 32));
        speedButton = speed.gameObject.AddComponent<Button>(); speedButton.targetGraphic = speed;
        speedButton.navigation = new Navigation { mode = Navigation.Mode.None };
        speedButton.onClick.AddListener(() => game.CycleSpeed());
        speedCaption = Label(speed.transform, "速度 1×", 14, cream, 0, 0, 68, 32);
        speedCaption.alignment = TextAnchor.MiddleCenter;

        // The transparent root does not eat map input in the spaces between cards.
        var dock = Panel("Tower Card Dock", transform, Color.clear);
        dockRoot = dock.rectTransform;
        Place(dock.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 14), new Vector2(842, 130));
        towerCardsRoot = CardLayer("Tower Cards", dock.transform);
        skillCardsRoot = CardLayer("Skill Cards", dock.transform);
        skillCardsRoot.gameObject.SetActive(false);
        towerTab = DockButton(dock.transform, "Tower Tab", "防御塔", 4, 88, () => SetDockMode(false), out towerTabLabel);
        skillTab = DockButton(dock.transform, "Skill Tab", "技能", 98, 108, () => SetDockMode(true), out skillTabLabel);
        var hintPlate = Panel("Placement Hint", dock.transform, new Color32(15, 23, 29, 195));
        Place(hintPlate.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(214, 0), new Vector2(424, 24));
        hint = Label(hintPlate.transform, towerHint, 11, cream, 8, 0, 408, 24);
        DockButton(dock.transform, "Collapse Dock", "收起 ▾", 752, 86, () => SetDockCollapsed(true), out _);
        Cards = new TowerBuildCard[4];
        Cards[0] = CreateCard(towerCardsRoot, TowerKind.Arrow, "箭塔", "快速 · 单体", new Color32(186, 151, 93, 255), 4);
        Cards[1] = CreateCard(towerCardsRoot, TowerKind.Frost, "冰霜塔", "光环 · 减速", new Color32(112, 199, 222, 255), 164);
        Cards[2] = CreateCard(towerCardsRoot, TowerKind.Mage, "法师塔", "飞弹 · 穿甲", new Color32(182, 149, 232, 255), 324);
        Cards[3] = CreateCard(towerCardsRoot, TowerKind.Artillery, "投石塔", "重击 · 溅射", new Color32(203, 146, 103, 255), 484);
        var action = Panel("Send Wave", dock.transform, gold, true);
        Place(action.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(652, -42), new Vector2(186, 48));
        waveButton = action.gameObject.AddComponent<Button>();
        waveButton.targetGraphic = action;
        var colors = waveButton.colors;
        colors.highlightedColor = new Color(1f, 0.94f, 0.8f);
        colors.pressedColor = new Color(0.78f, 0.69f, 0.5f);
        colors.disabledColor = new Color32(86, 94, 97, 255);
        waveButton.colors = colors;
        buttonText = Label(action.transform, "", 19, ink, 0, 0, 186, 48);
        buttonText.alignment = TextAnchor.MiddleCenter;
        waveButton.onClick.AddListener(OnWaveButton);
        var wavePlate = Panel("Wave Hint", dock.transform, new Color32(15, 23, 29, 205));
        Place(wavePlate.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(652, -96), new Vector2(186, 24));
        waveHint = Label(wavePlate.transform, "准备就绪后出击", 12, cream, 0, 0, 186, 24);
        waveHint.alignment = TextAnchor.MiddleCenter;
        var compact = Panel("Collapsed Action Dock", transform, new Color32(15, 23, 29, 238), true);
        Place(compact.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(200, 32));
        collapsedDock = compact.gameObject;
        var expand = compact.gameObject.AddComponent<Button>(); expand.targetGraphic = compact;
        expand.navigation = new Navigation { mode = Navigation.Mode.None };
        expand.onClick.AddListener(() => SetDockCollapsed(false));
        collapsedLabel = Label(compact.transform, "", 13, gold, 0, 0, 200, 32);
        collapsedLabel.alignment = TextAnchor.MiddleCenter;
        collapsedDock.SetActive(false);
        RefreshDockAppearance();
        bossPanel = Panel("Weather Boss", transform, new Color32(22, 17, 33, 232));
        Place(bossPanel.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -70), new Vector2(628, 74));
        bossTitle = Label(bossPanel.transform, "", 16, cream, 14, 2, 600, 26);
        var track = Panel("Boss Health Track", bossPanel.transform, new Color32(57, 41, 66, 255));
        Place(track.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(14, -31), new Vector2(600, 6));
        bossHealth = Panel("Boss Health", track.transform, new Color32(181, 104, 223, 255));
        Place(bossHealth.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
        bossWeather = Label(bossPanel.transform, "", 13, gold, 14, 41, 600, 26);
        bossPanel.gameObject.SetActive(false);
        CreateMapControls();
        CreateTowerSalePanel();
    }
    private void RefreshGameSpeed()
    {
        if (!speedButton || !game) return;
        speedButton.interactable = game.CanAct;
        speedCaption.text = "速度 " + game.GameSpeed + "×";
        speedButton.image.color = game.GameSpeed > 1 ? gold : new Color32(43, 57, 68, 255);
        speedCaption.color = game.GameSpeed > 1 ? ink : cream;
    }
    private void CreateTowerSalePanel()
    {
        var panel = Panel("Tower Sale", transform, new Color32(15, 23, 29, 247), true);
        towerSalePanel = panel.rectTransform;
        Place(towerSalePanel, Vector2.one * .5f, Vector2.one * .5f, new Vector2(.5f, 0), Vector2.zero, new Vector2(226, 114));
        towerSaleTitle = Label(panel.transform, "", 18, cream, 12, 6, 202, 28);
        towerSaleHint = Label(panel.transform, "", 12, muted, 12, 35, 202, 22);
        var sell = Panel("Sell Tower", panel.transform, gold, true);
        Place(sell.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(12, -66), new Vector2(146, 36));
        towerSaleButton = sell.gameObject.AddComponent<Button>(); towerSaleButton.targetGraphic = sell;
        towerSaleButton.navigation = new Navigation { mode = Navigation.Mode.None };
        towerSaleCaption = Label(sell.transform, "", 16, ink, 0, 0, 146, 36);
        towerSaleCaption.alignment = TextAnchor.MiddleCenter;
        towerSaleButton.onClick.AddListener(() => { if (build) build.TrySell(build.SelectedTowerSite); });
        var close = Panel("Cancel Sale", panel.transform, new Color32(43, 57, 68, 255), true);
        Place(close.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(164, -66), new Vector2(50, 36));
        var cancel = close.gameObject.AddComponent<Button>(); cancel.targetGraphic = close;
        cancel.navigation = new Navigation { mode = Navigation.Mode.None };
        cancel.onClick.AddListener(() => { if (build) build.ClearTowerSelection(); });
        Label(close.transform, "取消", 13, cream, 0, 0, 50, 36).alignment = TextAnchor.MiddleCenter;
        panel.gameObject.SetActive(false);
    }
    private void RefreshTowerSale()
    {
        if (!towerSalePanel) return;
        var site = build ? build.SelectedTowerSite : null;
        bool visible = site && build.CanSell(site) && build.worldCamera;
        if (!visible) { towerSalePanel.gameObject.SetActive(false); return; }
        var worldCamera = build.worldCamera;
        var viewport = worldCamera.WorldToViewportPoint(site.BuiltTower.transform.position);
        if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1)
        {
            build.ClearTowerSelection(); towerSalePanel.gameObject.SetActive(false); return;
        }
        var tower = site.BuiltTower.GetComponent<TowerBehaviour>();
        towerSaleTitle.text = tower && tower.definition ? tower.definition.displayName : "防御塔";
        towerSaleHint.text = "返还建造价的 " + Mathf.RoundToInt(build.saleRefundRate * 100) + "%";
        towerSaleCaption.text = "出售 +" + build.SaleRefund(site) + " 金币";
        towerSaleButton.interactable = true;
        var canvasRect = (RectTransform)canvas.transform;
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var screen = worldCamera.WorldToScreenPoint(site.BuiltTower.transform.position + Vector3.up * 1.6f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out var point);
        var safe = MapSafeViewport(worldCamera);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, worldCamera.ViewportToScreenPoint(new Vector3(safe.xMin, safe.yMin)), uiCamera, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, worldCamera.ViewportToScreenPoint(new Vector3(safe.xMax, safe.yMax)), uiCamera, out var max);
        float halfWidth = towerSalePanel.sizeDelta.x * .5f;
        point.x = Mathf.Clamp(point.x, min.x + halfWidth, Mathf.Max(min.x + halfWidth, max.x - halfWidth));
        point.y = Mathf.Clamp(point.y + 12, min.y + 8, Mathf.Max(min.y + 8, max.y - towerSalePanel.sizeDelta.y - 8));
        towerSalePanel.anchoredPosition = point;
        towerSalePanel.gameObject.SetActive(true);
    }
    private RectTransform CardLayer(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(parent, false);
        var rect = node.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private Button DockButton(Transform parent, string name, string caption, float x, float width, UnityEngine.Events.UnityAction action, out Text label)
    {
        var face = Panel(name, parent, new Color32(29, 43, 52, 238), true);
        Place(face.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(x, 0), new Vector2(width, 24));
        var button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face;
        button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(action);
        label = Label(face.transform, caption, 12, cream, 0, 0, width, 24); label.alignment = TextAnchor.MiddleCenter;
        return button;
    }
    public void SetDockMode(bool skills)
    {
        if (!ready) return;
        CancelDockInteraction(); SkillsVisible = skills;
        towerCardsRoot.gameObject.SetActive(!skills); skillCardsRoot.gameObject.SetActive(skills);
        RefreshDockAppearance();
        if (game.TryGetComponent<BattleItemPanel>(out var itemPanel)) itemPanel.RefreshView();
    }
    public void SetDockCollapsed(bool collapsed)
    {
        if (!ready) return;
        CancelDockInteraction(); DockCollapsed = collapsed;
        dockRoot.gameObject.SetActive(!collapsed); collapsedDock.SetActive(collapsed);
        RefreshDockAppearance();
        if (navigation) navigation.RefreshOverviewLayout();
    }
    public Rect MapSafeViewport(Camera worldCamera)
    {
        var viewport = worldCamera ? worldCamera.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
        float scale = viewport.width / 1600f;
        float bottom = Mathf.Min((DockCollapsed ? 54 : 152) * scale / Mathf.Max(1, viewport.height), .42f);
        float top = Mathf.Min(74 * scale / Mathf.Max(1, viewport.height), .2f);
        return Rect.MinMaxRect(18 * scale / Mathf.Max(1, viewport.width), bottom,
            1 - 74 * scale / Mathf.Max(1, viewport.width), 1 - top);
    }
    private void CancelDockInteraction()
    {
        if (build) build.Cancel();
        if (game.TryGetComponent<BattleItemTargeting>(out var targeting)) targeting.Cancel();
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }
    public void SetSkillDockState(int remaining, bool unlocked, string message)
    {
        skillCharges = remaining; skillsUnlocked = unlocked; skillHint = message;
        RefreshDockAppearance();
    }
    private void RefreshDockAppearance()
    {
        if (!towerTab || !skillTab) return;
        towerTab.image.color = !SkillsVisible ? gold : new Color32(29, 43, 52, 238);
        skillTab.image.color = SkillsVisible ? gold : new Color32(29, 43, 52, 238);
        towerTabLabel.color = !SkillsVisible ? ink : cream; skillTabLabel.color = SkillsVisible ? ink : cream;
        skillTabLabel.text = skillsUnlocked ? "技能  " + skillCharges + "/3" : "技能 · 未解锁";
        if (hint) hint.text = SkillsVisible ? skillHint : towerHint;
        if (collapsedLabel) collapsedLabel.text = "展开 · " + (SkillsVisible ? skillsUnlocked ? "技能 " + skillCharges + "/3" : "技能 · 未解锁" : "防御塔") + "  ▴";
    }
    private void CreatePauseMenu()
    {
        // Disable keyboard navigation and pointer interaction on the entire underlying HUD.
        var controls = new GameObject("Gameplay Controls", typeof(RectTransform), typeof(CanvasGroup));
        controls.transform.SetParent(transform, false);
        var rect = controls.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child != controls.transform) child.SetParent(controls.transform, false);
        }
        gameplayGroup = controls.GetComponent<CanvasGroup>();
        var shade = Panel("Pause Menu", transform, new Color32(5, 10, 17, 200), true);
        Place(shade.rectTransform, Vector2.zero, Vector2.one, Vector2.one * .5f, Vector2.zero, Vector2.zero);
        pauseMenu = shade.gameObject;
        var window = Panel("Pause Window", shade.transform, new Color32(20, 29, 38, 255), true);
        Place(window.rectTransform, Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(400, 508));
        Label(window.transform, "游戏暂停", 28, cream, 24, 24, 352, 44).alignment = TextAnchor.MiddleCenter;
        Label(window.transform, game.levelName + " · Esc 返回游戏", 14, muted, 24, 70, 352, 28).alignment = TextAnchor.MiddleCenter;
        continueButton = PauseAction(window.transform, "继续游戏", 120, () => ShowPauseMenu(false), true);
        PauseAction(window.transform, "重新开始", 184, () => { ShowPauseMenu(false); game.RestartLevel(); });
        PauseAction(window.transform, "返回地图选择", 248, () => { ShowPauseMenu(false); FortressMainMenu.ReturnToSelection(); });
        FortressAudioSettings.Create(window.transform, runtimeFont, new Vector2(32, 320), 336);
        debugConsole = FortressDebugConsole.Create(shade.transform, window.gameObject, runtimeFont, game, this);
        var debug = PauseAction(window.transform, "调试台  ›", 458, () => debugConsole.Open());
        debug.name = "Open Debug Console";
        debug.GetComponent<RectTransform>().sizeDelta = new Vector2(336, 34);
        debug.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(336, 34);
        // Explicit navigation keeps keyboard/controller focus within this modal.
        var buttons = window.GetComponentsInChildren<Selectable>();
        for (int i = 0; i < buttons.Length; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                selectOnDown = buttons[(i + 1) % buttons.Length] };
            buttons[i].navigation = nav;
        }
        pauseMenu.SetActive(false);
    }
    private Button PauseAction(Transform parent, string caption, float y, UnityEngine.Events.UnityAction action, bool primary = false)
    {
        var face = Panel(caption, parent, primary ? gold : new Color32(43, 57, 68, 255), true);
        Place(face.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(32, -y), new Vector2(336, 50));
        var button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face;
        button.onClick.AddListener(action);
        Label(face.transform, caption, 18, primary ? ink : cream, 0, 0, 336, 50).alignment = TextAnchor.MiddleCenter;
        return button;
    }
    private void RefreshBoss()
    {
        var weather = FortressWeatherController.Instance;
        var boss = weather ? weather.CurrentBoss : null;
        bool visible = boss && boss.IsActive && !game.IsGameOver;
        bossPanel.gameObject.SetActive(visible);
        if (!visible) return;
        var health = boss.Enemy.Combat;
        bossTitle.text = "BOSS · " + boss.Enemy.DisplayName + "     " + Mathf.CeilToInt(health.Health) + " / " + Mathf.CeilToInt(health.maxHealth);
        bossHealth.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(health.Health / health.maxHealth), 1);
        string effect = weather.Weather == FortressWeatherKind.Snow
            ? "暴雪 · 防御塔攻速 -" + Mathf.RoundToInt((1 - boss.snowAttackSpeed) * 100) + "%"
            : weather.Weather == FortressWeatherKind.Sandstorm
            ? "沙尘暴 · 全体怪物移速 +" + Mathf.RoundToInt((boss.sandMovementSpeed - 1) * 100) + "%"
            : "天气暂歇 · 即将施放" + (boss.NextWeather == FortressWeatherKind.Snow ? "暴雪" : "沙尘暴");
        bossWeather.text = effect + "    " + Mathf.CeilToInt(boss.SecondsRemaining) + " 秒";
    }
    private void Stat(Transform parent, string caption, out Text value, float x, Color color)
    {
        Label(parent, caption, 12, muted, x, 0, 36, 48);
        value = Label(parent, "", 22, color, x + 42, 0, 108, 48);
    }
    private TowerBuildCard CreateCard(Transform parent, TowerKind kind, string title, string role, Color accent, float x)
    {
        var outline = Panel(title + " Card", parent, new Color32(66, 74, 80, 255), true);
        Place(outline.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(x, -30), new Vector2(152, 94));
        var group = outline.gameObject.AddComponent<CanvasGroup>();
        var surface = Panel("Surface", outline.transform, new Color32(20, 30, 38, 190));
        Place(surface.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-2, -2));
        var stripe = Panel("Tower Accent", outline.transform, accent);
        Place(stripe.rectTransform, Vector2.up, Vector2.one, Vector2.up, new Vector2(1, -1), new Vector2(-2, 3));
        var portrait = Panel("Tower Portrait", outline.transform, Color.white);
        Place(portrait.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(7, -9), new Vector2(43, 75));
        var prefab = build.Prefab(kind);
        var behaviour = prefab ? prefab.GetComponent<TowerBehaviour>() : null;
        if (prefab) foreach (var sprite in prefab.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sprite.sprite && (!behaviour || sprite != behaviour.auraVisual)) { portrait.sprite = sprite.sprite; break; }
        }
        portrait.preserveAspect = true;
        if (!portrait.sprite) portrait.color = Color.clear;
        Label(outline.transform, title, 20, cream, 56, 9, 94, 28);
        Label(outline.transform, role, 11, accent, 56, 36, 94, 20);
        var price = Label(outline.transform, "", 17, gold, 56, 62, 94, 26);
        var card = outline.gameObject.AddComponent<TowerBuildCard>();
        card.Initialize(build, kind, group, outline, price, accent);
        return card;
    }
    private void CreateMapControls()
    {
        var help = Panel("Map Controls Hint", transform, new Color32(15, 23, 29, 205));
        Place(help.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(14, 14), new Vector2(246, 28));
        bool fullMap = navigation && navigation.enableFullMapView;
        Label(help.transform, fullMap ? "滚轮缩放 · 左键拖图 · Home 全图" : "滚轮缩放 · 左键拖图 · Home 复位", 12, cream, 9, 0, 230, 28);
        var controls = Panel("Map Navigation", transform, Color.clear);
        Place(controls.rectTransform, Vector2.right, Vector2.right, Vector2.right, new Vector2(-18, 18), new Vector2(42, 154));
        zoomText = Label(controls.transform, "100%", 11, cream, -5, 0, 52, 20);
        zoomText.alignment = TextAnchor.MiddleCenter;
        MapButton(controls.transform, "+", 24, () => { if (navigation) navigation.ZoomIn(); });
        MapButton(controls.transform, fullMap ? "全图" : "复位", 68, () => { if (navigation) navigation.ResetView(); });
        MapButton(controls.transform, "−", 112, () => { if (navigation) navigation.ZoomOut(); });
    }
    private void MapButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction action)
    {
        var face = Panel(label, parent, new Color32(15, 23, 29, 232), true);
        Place(face.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(0, -y), new Vector2(42, 40));
        var button = face.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.onClick.AddListener(action);
        var text = Label(face.transform, label, label == "复位" || label == "全图" ? 12 : 25, cream, 0, 0, 42, 40);
        text.alignment = TextAnchor.MiddleCenter;
    }
    private Image Panel(string name, Transform parent, Color color, bool blocksPointer = false)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        node.transform.SetParent(parent, false);
        var image = node.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = blocksPointer;
        return image;
    }
    private Text Label(Transform parent, string text, int size, Color color, float x, float y, float width, float height)
    {
        var node = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        node.transform.SetParent(parent, false);
        var label = node.GetComponent<Text>();
        label.font = runtimeFont;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        Place(label.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(x, -y), new Vector2(width, height));
        return label;
    }
    private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
    private void OnWaveButton()
    {
        if (game.AllWavesFinished && encounterStatus != null && !encounterAllowsRestart) return;
        build.Cancel();
        if (game.IsGameOver || game.AllWavesFinished) game.RestartLevel();
        else game.StartNextWave();
    }
    private void ShowMessage(string message) { towerHint = message; if (hint && !SkillsVisible) hint.text = message; }
    public void SetEncounterMessage(string status, string action, bool allowsRestart)
    {
        if (encounterStatus == status && encounterAction == action && encounterAllowsRestart == allowsRestart) return;
        encounterStatus = status; encounterAction = action; encounterAllowsRestart = allowsRestart; Refresh();
    }
    public void Refresh()
    {
        if (!ready || !game) return;
        GoldText.text = game.Gold.ToString();
        lifeText.text = game.Lives + " / " + game.startingLives;
        lifeText.color = game.Lives <= 5 ? new Color32(255, 125, 113, 255) : cream;
        waveText.text = game.CurrentWave + " / " + game.TotalWaves;
        branchText.text = "击败 " + game.DefeatedEnemies + (game.UsesBranch ? " · " + (game.BranchOpen ? game.branchDisplayName+"开放" : game.branchDisplayName+" " + game.branchUnlockWave + " 波") : " · 防守中");
        if (game.IsGameOver) statusText.text = game.homeDisplayName + "失守";
        else if (game.AllWavesFinished) statusText.text = "防守成功";
        else if (game.IsWaveActive) statusText.text = (game.IsSpawning ? "敌人来袭" : "清理敌人") + " · " + game.AliveCount;
        else statusText.text = game.CurrentWave == 0 ? game.preparationHint : "本波结束";
        bool ended = game.IsGameOver || game.AllWavesFinished;
        buttonText.text = ended ? "重新开始" : game.IsWaveActive ? "战斗进行中" : "开始第 " + (game.CurrentWave + 1) + " 波  ›";
        waveButton.interactable = ended || game.CanStartWave;
        waveHint.text = ended ? "重整防线，再战一局" : game.IsWaveActive ? "击败敌人赚取金币" : "准备就绪后出击";
        foreach (var card in Cards) card.Refresh();
        if (ended) { build.Cancel(); ShowMessage(game.IsGameOver ? "防线被突破了，重新部署再试一次" : "所有波次已完成，" + game.homeDisplayName + "安全"); }
        if (encounterStatus != null)
        {
            statusText.text = encounterStatus; buttonText.text = encounterAction;
            waveButton.interactable = encounterAllowsRestart && !game.IsPaused;
        }
    }
}
