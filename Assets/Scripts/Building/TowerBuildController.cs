using System.Collections.Generic;
using KingdomBorder.TwinTowers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class TowerBuildController : MonoBehaviour
{
    public Camera worldCamera;
    public WaveGameController game;
    public BuildSite[] sites;
    public GameObject mageTowerPrefab, frostTowerPrefab, arrowTowerPrefab, artilleryTowerPrefab;
    public Transform builtTowersRoot;
    public Canvas hudCanvas;
    [Tooltip("Optional map-specific settings; tower visuals and prices remain shared.")]
    public TowerDefinition[] mapTowerSettings;
    [Header("建造价格")]
    [Min(1)] public int arrowCost = 90;
    [Min(1)] public int frostCost = 110;
    [Min(1)] public int mageCost = 140;
    [Min(1)] public int artilleryCost = 160;
    [Range(0, 1)] public float saleRefundRate = .7f;
    public bool IsDragging { get; private set; }
    public BuildSite SelectedTowerSite { get; private set; }
    public BuildSite HoveredSite => hoveredSite;
    public TowerKind DraggedKind { get; private set; }
    public int BuiltCount
    {
        get { int count = 0; if (sites != null) foreach (var s in sites) if (s && s.IsOccupied) count++; return count; }
    }
    public event System.Action<string> PlacementMessage;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private BuildSite hoveredSite;
    private GameObject preview;
    private SpriteRenderer[] previewSprites;
    private LineRenderer rangeRing;
    private Vector2 dragPosition;
    private bool purchasing;
    private bool towerPointerDown, towerPointerMoved;
    private Vector2 towerPointerStart;
    private BuildSite pressedTowerSite;
    public int Cost(TowerKind kind)
    {
        switch (kind)
        {
            case TowerKind.Arrow: return Mathf.Max(1, arrowCost);
            case TowerKind.Frost: return Mathf.Max(1, frostCost);
            case TowerKind.Artillery: return Mathf.Max(1, artilleryCost);
            default: return Mathf.Max(1, mageCost);
        }
    }
    public GameObject Prefab(TowerKind kind)
    {
        switch (kind)
        {
            case TowerKind.Arrow: return arrowTowerPrefab;
            case TowerKind.Frost: return frostTowerPrefab;
            case TowerKind.Artillery: return artilleryTowerPrefab;
            default: return mageTowerPrefab;
        }
    }
    public bool CanAfford(TowerKind kind) => game && game.CanBuild && Prefab(kind) && game.Gold >= Cost(kind);
    public TowerDefinition Settings(TowerKind kind)
    {
        if (mapTowerSettings != null)
            foreach (var settings in mapTowerSettings) if (settings && settings.kind == kind) return settings;
        var prefab = Prefab(kind);
        return prefab ? prefab.GetComponent<TowerBehaviour>().definition : null;
    }
    private void Awake()
    {
        if (hudCanvas)
        {
            Transform oldMenu = hudCanvas.transform.Find("Tower Build Menu");
            if (oldMenu) oldMenu.gameObject.SetActive(false);
        }
    }
    private void Update()
    {
        if (!IsDragging) { UpdateTowerSelection(); return; }
        if (!game || !game.CanBuild || !CanAfford(DraggedKind)) { Cancel(); return; }
        bool cancel = false;
#if ENABLE_INPUT_SYSTEM
        cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
        cancel = Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);
#endif
        if (cancel) Cancel();
    }
    private void LateUpdate() { if (IsDragging) UpdateDrag(dragPosition); }
    private void OnDisable() { Cancel(); }
    private void OnApplicationFocus(bool focus) { if (!focus) Cancel(); }
    public bool BeginDrag(TowerKind kind, Vector2 screenPosition)
    {
        Cancel();
        if (game && game.TryGetComponent<BattleItemTargeting>(out var targeting)) targeting.Cancel();
        if (!CanAfford(kind)) { FortressAudio.Play(FortressSound.Invalid); PlacementMessage?.Invoke("金币不足，击败敌人可获得金币"); return false; }
        FortressAudio.Play(FortressSound.CardPick);
        IsDragging = true;
        DraggedKind = kind;
        CreatePreview(kind);
        if (sites != null) foreach (var s in sites) if (s) s.ShowPlacementHint(true);
        PlacementMessage?.Invoke("拖到发光的空坑位 · 松手建造 · Esc / 右键取消");
        UpdateDrag(screenPosition);
        return true;
    }
    public void UpdateDrag(Vector2 screenPosition)
    {
        if (!IsDragging || !worldCamera) return;
        dragPosition = screenPosition;
        bool insideMap = worldCamera.pixelRect.Contains(screenPosition) && !IsOverUI(screenPosition);
        BuildSite next = insideMap ? SiteAt(screenPosition) : null;
        if (hoveredSite != next)
        {
            if (hoveredSite) hoveredSite.Hover(false);
            hoveredSite = next;
            if (hoveredSite) hoveredSite.Hover(true);
        }
        if (!preview) return;
        preview.SetActive(insideMap);
        Vector3 point = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
        preview.transform.position = hoveredSite
            ? (hoveredSite.buildAnchor ? hoveredSite.buildAnchor.position : hoveredSite.transform.position)
            : new Vector3(point.x, point.y, 0);
        Color tint = hoveredSite && CanAfford(DraggedKind) ? new Color(0.55f, 1f, 0.72f, 0.76f) : new Color(1f, 0.42f, 0.36f, 0.6f);
        foreach (var sprite in previewSprites) sprite.color = tint;
        rangeRing.startColor = rangeRing.endColor = tint;
    }
    public bool EndDrag(Vector2 screenPosition)
    {
        if (!IsDragging) return false;
        UpdateDrag(screenPosition);
        bool built = TryPurchase(DraggedKind, hoveredSite);
        Cancel();
        PlacementMessage?.Invoke(built ? "建造完成 · 继续拖出卡片部署防线" : "已取消 · 只在空坑位建造，未扣金币");
        return built;
    }
    public bool TryPurchase(TowerKind kind, BuildSite site)
    {
        if (purchasing || !site || sites == null || System.Array.IndexOf(sites, site) < 0
            || site.IsOccupied || !CanAfford(kind)) return false;
        purchasing = true;
        bool spent = false, built = false;
        int price = Cost(kind);
        try
        {
            spent = game.TrySpendGold(price);
            if (spent) built = site.TryBuild(Prefab(kind), builtTowersRoot, Settings(kind), price);
            if (built) FortressAudio.Play(FortressSound.Build, site.transform.position);
            return built;
        }
        finally
        {
            if (spent && !built) game.AddGold(price);
            purchasing = false;
        }
    }
    public void Cancel()
    {
        ClearTowerSelection();
        bool wasDragging = IsDragging;
        IsDragging = false;
        if (hoveredSite) hoveredSite.Hover(false);
        hoveredSite = null;
        if (sites != null) foreach (var s in sites) if (s) s.ShowPlacementHint(false);
        if (preview) { preview.SetActive(false); Destroy(preview); }
        preview = null;
        if (wasDragging) PlacementMessage?.Invoke("拖动下方塔卡到空坑位，购买并建造");
    }
    public int SaleRefund(BuildSite site) => OwnsSite(site) ? Mathf.FloorToInt(site.PaidCost * Mathf.Clamp01(saleRefundRate)) : 0;
    private bool OwnsSite(BuildSite site) => site && sites != null && System.Array.IndexOf(sites, site) >= 0;
    public bool CanSell(BuildSite site)
        => isActiveAndEnabled && game && game.CanBuild && !IsDragging && OwnsSite(site) && site.BuiltTower && !IsItemTargeting;
    private bool IsItemTargeting => game && game.TryGetComponent<BattleItemTargeting>(out var targeting) && targeting.IsTargeting;
    public bool SelectTower(BuildSite site)
    {
        if (!CanSell(site)) return false;
        ClearTowerSelection();
        SelectedTowerSite = site;
        site.Select(true);
        return true;
    }
    public void ClearTowerSelection()
    {
        if (SelectedTowerSite) SelectedTowerSite.Select(false);
        SelectedTowerSite = null;
        towerPointerDown = false;
        pressedTowerSite = null;
    }
    public bool TrySell(BuildSite site)
    {
        if (purchasing || !CanSell(site)) return false;
        purchasing = true;
        try
        {
            int refund = SaleRefund(site);
            if (!site.TryRemoveTower()) return false;
            ClearTowerSelection();
            game.AddGold(refund);
            FortressAudio.Play(FortressSound.Click);
            PlacementMessage?.Invoke("出售完成 · 返还 " + refund + " 金币 · 空坑位可重新建塔");
            return true;
        }
        finally { purchasing = false; }
    }
    private void UpdateTowerSelection()
    {
        if (!game || !game.CanBuild || IsItemTargeting) { ClearTowerSelection(); return; }
        if (SelectedTowerSite && !CanSell(SelectedTowerSite)) ClearTowerSelection();
        Vector2 pointer = Vector2.zero;
        bool pressed = false, released = false, cancelled = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return;
        pointer = Mouse.current.position.ReadValue();
        pressed = Mouse.current.leftButton.wasPressedThisFrame;
        released = Mouse.current.leftButton.wasReleasedThisFrame;
        cancelled = Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        pointer = Input.mousePosition;
        pressed = Input.GetMouseButtonDown(0); released = Input.GetMouseButtonUp(0); cancelled = Input.GetMouseButtonDown(1);
#endif
        HandleTowerPointer(pointer, pressed, released, cancelled);
    }
    private void HandleTowerPointer(Vector2 pointer, bool pressed, bool released, bool cancelled)
    {
        if (cancelled) { ClearTowerSelection(); return; }
        bool onMap = worldCamera && worldCamera.pixelRect.Contains(pointer) && !IsOverUI(pointer);
        if (pressed)
        {
            towerPointerDown = onMap;
            towerPointerStart = pointer;
            towerPointerMoved = false;
            pressedTowerSite = onMap ? BuiltSiteAt(pointer) : null;
        }
        if (!towerPointerDown) return;
        float threshold = Mathf.Max(8, EventSystem.current ? EventSystem.current.pixelDragThreshold : 8);
        if ((pointer - towerPointerStart).sqrMagnitude > threshold * threshold)
        {
            towerPointerMoved = true;
            if (SelectedTowerSite) SelectedTowerSite.Select(false);
            SelectedTowerSite = null;
        }
        if (!released) return;
        towerPointerDown = false;
        if (towerPointerMoved || !onMap) return;
        var site = BuiltSiteAt(pointer);
        if (site && site == pressedTowerSite) SelectTower(site);
        else ClearTowerSelection();
    }
    private BuildSite BuiltSiteAt(Vector2 screenPosition)
    {
        if (!worldCamera || sites == null) return null;
        Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
        foreach (var site in sites)
        {
            if (!site || !site.BuiltTower) continue;
            var tower = site.BuiltTower.GetComponent<TowerBehaviour>();
            foreach (var sprite in site.BuiltTower.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sprite.enabled || !sprite.sprite || (tower && sprite == tower.auraVisual)) continue;
                var bounds = sprite.bounds;
                if (world.x >= bounds.min.x && world.x <= bounds.max.x && world.y >= bounds.min.y && world.y <= bounds.max.y) return site;
            }
            var anchor = site.buildAnchor ? site.buildAnchor.position : site.transform.position;
            if (((Vector2)(world - anchor)).sqrMagnitude < .6f * .6f) return site;
        }
        return null;
    }
    private bool IsOverUI(Vector2 position)
    {
        if (!EventSystem.current) return false;
        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
        foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return true;
        return false;
    }
    private BuildSite SiteAt(Vector2 screenPosition)
    {
        if (!worldCamera || sites == null) return null;
        Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
        foreach (var site in sites)
            if (site && !site.IsOccupied && site.clickArea && site.clickArea.enabled && site.clickArea.OverlapPoint(world)) return site;
        return null;
    }
    private void CreatePreview(TowerKind kind)
    {
        GameObject prefab = Prefab(kind);
        var tower = prefab.GetComponent<TowerBehaviour>();
        preview = new GameObject("Placement Preview (visuals only)");
        var sprites = new List<SpriteRenderer>();
        foreach (var source in prefab.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!source.sprite || (tower && source == tower.auraVisual)) continue;
            var node = new GameObject(source.name);
            node.transform.SetParent(preview.transform, false);
            node.transform.localPosition = prefab.transform.InverseTransformPoint(source.transform.position);
            node.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * source.transform.rotation;
            Vector3 scale = source.transform.lossyScale;
            Vector3 rootScale = prefab.transform.lossyScale;
            node.transform.localScale = new Vector3(scale.x / rootScale.x, scale.y / rootScale.y, scale.z / rootScale.z);
            var sprite = node.AddComponent<SpriteRenderer>();
            sprite.sprite = source.sprite;
            sprite.sharedMaterial = source.sharedMaterial;
            sprite.sortingLayerID = source.sortingLayerID;
            sprite.sortingOrder = 100;
            sprite.flipX = source.flipX;
            sprite.flipY = source.flipY;
            sprites.Add(sprite);
        }
        preview.transform.localScale = prefab.transform.localScale;
        previewSprites = sprites.ToArray();
        var ringNode = new GameObject("Attack Range");
        ringNode.transform.SetParent(preview.transform, false);
        rangeRing = ringNode.AddComponent<LineRenderer>();
        rangeRing.useWorldSpace = false;
        rangeRing.loop = true;
        rangeRing.positionCount = 96;
        rangeRing.widthMultiplier = 0.035f;
        rangeRing.sortingOrder = 99;
        if (sites != null) foreach (var site in sites)
            if (site && site.selectionRing) { rangeRing.sharedMaterial = site.selectionRing.sharedMaterial; break; }
        var settings = Settings(kind);
        float radius = settings ? settings.range : 3f;
        for (int i = 0; i < rangeRing.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / rangeRing.positionCount;
            rangeRing.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
    }
}
