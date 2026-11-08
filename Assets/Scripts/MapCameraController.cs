using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Camera))]
public sealed class MapCameraController : MonoBehaviour
{
    [Tooltip("地图边界来源。换关卡时指定该关的背景 SpriteRenderer。")]
    public SpriteRenderer mapRenderer;
    [Min(0.1f)] public float minimumSize = 3f;
    [Min(0.1f)] public float maximumSize = 8f;
    [Min(0.1f)] public float startSize = 8f;
    [Min(0.01f)] public float zoomSpeed = 1.4f;
    [Tooltip("加减按钮每次缩放的倍率，独立于滚轮速度。")]
    [Min(1.01f)] public float buttonZoomFactor = 1.2f;
    [Min(1f)] public float zoomResponse = 24f;
    [Min(0f)] public float keyboardSpeed = 8f;
    [Header("全图视角（按关卡启用）")]
    public bool enableFullMapView;
    [Min(0)] public float overviewPadding = .6f;
    public Material overviewBackdropMaterial;

    public float ZoomPercent => viewCamera ? MaximumViewSize / viewCamera.orthographicSize * 100f : 100f;
    public float MaximumViewSize
    {
        get
        {
            float limit = Mathf.Max(0.1f, maximumSize);
            if (!mapRenderer || !viewCamera) return limit;
            if (enableFullMapView)
            {
                var area = SafeViewport;
                var fullBounds = OverviewBounds;
                return Mathf.Max(fullBounds.extents.y / area.height,
                    fullBounds.extents.x / (Mathf.Max(.01f, viewCamera.aspect) * area.width));
            }
            Bounds bounds = mapRenderer.bounds;
            // Cover the whole viewport: crop the surplus axis instead of exposing empty borders.
            return Mathf.Max(0.01f, Mathf.Min(limit, bounds.extents.y, bounds.extents.x / Mathf.Max(0.01f, viewCamera.aspect)));
        }
    }
    public float MinimumViewSize => Mathf.Min(Mathf.Max(0.1f, minimumSize), MaximumViewSize);
    private Camera viewCamera;
    private TowerBuildController build;
    private BattleItemTargeting itemTargeting;
    private FortressHud hud;
    private SpriteRenderer overviewBackdrop, overviewShadow;
    private Bounds overviewBounds;
    private bool cachedOverviewBounds, overviewFramed;
    private Rect lastSafeViewport;
    private float lastAspect;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private Vector2 lastMousePosition, zoomAnchor;
    private bool wasDragging, pointerPanActive;
    private float targetSize;
    private bool wasFocused = true;

    private void Awake()
    {
        viewCamera = GetComponent<Camera>();
        build = FindFirstObjectByType<TowerBuildController>();
        itemTargeting = FindFirstObjectByType<BattleItemTargeting>();
        hud = FindFirstObjectByType<FortressHud>();
        ShowInitialView();
    }
    private Rect SafeViewport => hud ? hud.MapSafeViewport(viewCamera) : new Rect(.012f, .17f, .94f, .74f);
    private Bounds OverviewBounds
    {
        get
        {
            if (cachedOverviewBounds) return overviewBounds;
            overviewBounds = mapRenderer ? mapRenderer.bounds : new Bounds(Vector3.zero, new Vector3(32, 24, 0));
            if (build && build.game)
                foreach (var route in new[] { build.game.routeA, build.game.routeB, build.game.routeC })
                    if (route) foreach (var point in route.GetPositions())
                    {
                        overviewBounds.Encapsulate(point);
                        overviewBounds.Encapsulate(point + Vector3.up * 1.3f);
                    }
            overviewBounds.Expand(new Vector3(overviewPadding * 2, overviewPadding * 2, 0));
            cachedOverviewBounds = true;
            return overviewBounds;
        }
    }
    private void Update()
    {
        if (enableFullMapView && (lastSafeViewport != SafeViewport || !Mathf.Approximately(lastAspect, viewCamera.aspect)))
            RefreshOverviewLayout();
        ClampToMap();
        if (itemTargeting && itemTargeting.IsTargeting) { StopNavigation(); return; }
        if (build && build.game && build.game.IsPaused) { StopNavigation(); return; }
        if (build && build.IsDragging) { StopNavigation(); return; }
        if (!wasFocused) { AnimateZoom(); return; }
        Vector2 mousePosition = Vector2.zero, direction = Vector2.zero;
        float wheel = 0;
        bool dragging = false, pointerPressed = false, reset = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            mousePosition = Mouse.current.position.ReadValue();
            wheel = Mouse.current.scroll.ReadValue().y / 120f;
            dragging = Mouse.current.leftButton.isPressed;
            pointerPressed = Mouse.current.leftButton.wasPressedThisFrame;
        }
        if (Keyboard.current != null)
        {
            var keys = Keyboard.current;
            direction.x = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1f : 0f)
                - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1f : 0f);
            direction.y = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1f : 0f)
                - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1f : 0f);
            reset = keys.homeKey.wasPressedThisFrame;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        mousePosition = Input.mousePosition;
        wheel = Input.mouseScrollDelta.y;
        dragging = Input.GetMouseButton(0);
        pointerPressed = Input.GetMouseButtonDown(0);
        direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        reset = Input.GetKeyDown(KeyCode.Home);
#endif
        bool overMap = CanUsePointer(mousePosition);
        if (pointerPressed) pointerPanActive = overMap;
        if (!dragging) pointerPanActive = false;
        dragging &= pointerPanActive;
        if (!overMap) { wheel = 0; dragging = false; }
        if (reset) ResetView();
        if (Mathf.Abs(wheel) > 0.001f) ZoomAt(mousePosition, wheel);
        if (dragging && wasDragging) PanByPixels(mousePosition - lastMousePosition);
        if (direction.sqrMagnitude > 0)
            PanByWorld(direction.normalized * keyboardSpeed * Time.unscaledDeltaTime);
        AnimateZoom();
        lastMousePosition = mousePosition;
        wasDragging = dragging;
    }
    private void OnApplicationFocus(bool focus) { wasFocused = focus; StopNavigation(); }
    private void OnDisable()
    {
        StopNavigation();
        if (overviewBackdrop) overviewBackdrop.gameObject.SetActive(false);
        if (overviewShadow) overviewShadow.gameObject.SetActive(false);
    }
    private void StopNavigation()
    {
        wasDragging = false;
        pointerPanActive = false;
        if (viewCamera) targetSize = viewCamera.orthographicSize;
    }
    public bool CanUsePointer(Vector2 screenPosition)
    {
        if (!viewCamera || !viewCamera.pixelRect.Contains(screenPosition)) return false;
        if (!EventSystem.current) return true;
        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPosition }, uiHits);
        foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return false;
        return true;
    }
    public void ZoomAt(Vector2 screenPosition, float steps)
    {
        ZoomByFactor(screenPosition, Mathf.Exp(-steps * zoomSpeed));
    }
    private void ZoomByFactor(Vector2 screenPosition, float factor)
    {
        if (itemTargeting && itemTargeting.IsTargeting) return;
        if (!viewCamera || (build && (build.IsDragging || (build.game && build.game.IsPaused)))) return;
        targetSize = Mathf.Clamp(targetSize * factor, MinimumViewSize, MaximumViewSize);
        if (enableFullMapView) overviewFramed = Mathf.Abs(targetSize - MaximumViewSize) < .01f;
        zoomAnchor = screenPosition;
    }
    public void ZoomIn() { if (viewCamera) ZoomByFactor(viewCamera.pixelRect.center, 1f / Mathf.Max(1.01f, buttonZoomFactor)); }
    public void ZoomOut() { if (viewCamera) ZoomByFactor(viewCamera.pixelRect.center, Mathf.Max(1.01f, buttonZoomFactor)); }
    private void AnimateZoom()
    {
        if (Mathf.Abs(targetSize - viewCamera.orthographicSize) < 0.0001f) return;
        Vector3 before = ScreenPoint(zoomAnchor);
        float size = Mathf.Lerp(viewCamera.orthographicSize, targetSize, 1f - Mathf.Exp(-zoomResponse * Time.unscaledDeltaTime));
        if (Mathf.Abs(targetSize - size) < 0.001f) size = targetSize;
        viewCamera.orthographicSize = size;
        transform.position += before - ScreenPoint(zoomAnchor);
        ClampToMap();
    }
    private Vector3 ScreenPoint(Vector2 position)
    { return viewCamera.ScreenToWorldPoint(new Vector3(position.x, position.y, -transform.position.z)); }
    public void PanByPixels(Vector2 delta)
    {
        if (!viewCamera || viewCamera.pixelHeight <= 0) return;
        PanByWorld(-delta * (viewCamera.orthographicSize * 2f / viewCamera.pixelHeight));
    }
    public void PanByWorld(Vector2 delta)
    {
        if (itemTargeting && itemTargeting.IsTargeting) return;
        if (!viewCamera || (build && (build.IsDragging || (build.game && build.game.IsPaused)))) return;
        targetSize = viewCamera.orthographicSize;
        if (enableFullMapView) overviewFramed = Mathf.Abs(targetSize - MaximumViewSize) < .01f;
        transform.position += new Vector3(delta.x, delta.y, 0);
        ClampToMap();
    }
    public void ShowInitialView() { SetView(false); }
    public void ResetView() { SetView(enableFullMapView); }
    private void SetView(bool showWholeMap)
    {
        if (itemTargeting && itemTargeting.IsTargeting) return;
        if (!viewCamera) viewCamera = GetComponent<Camera>();
        if (build && build.IsDragging) return;
        viewCamera.rect = new Rect(0, 0, 1, 1);
        targetSize = viewCamera.orthographicSize = showWholeMap ? MaximumViewSize : Mathf.Clamp(startSize, MinimumViewSize, MaximumViewSize);
        overviewFramed = showWholeMap;
        Vector3 center = showWholeMap ? OverviewBounds.center - SafeCenterOffset : mapRenderer ? mapRenderer.bounds.center : Vector3.zero;
        transform.position = new Vector3(center.x, center.y, -10f);
        wasDragging = false;
        ClampToMap();
        lastSafeViewport = SafeViewport; lastAspect = viewCamera.aspect;
    }
    private Vector3 SafeCenterOffset
    {
        get
        {
            var center = SafeViewport.center;
            return new Vector3((center.x - .5f) * viewCamera.orthographicSize * 2 * viewCamera.aspect,
                (center.y - .5f) * viewCamera.orthographicSize * 2, 0);
        }
    }
    public void RefreshOverviewLayout()
    {
        if (!enableFullMapView || !viewCamera) return;
        if (overviewFramed) ResetView(); else ClampToMap();
        lastSafeViewport = SafeViewport; lastAspect = viewCamera.aspect;
    }
    public void ClampToMap()
    {
        if (!viewCamera) return;
        viewCamera.orthographicSize = Mathf.Clamp(viewCamera.orthographicSize, MinimumViewSize, MaximumViewSize);
        targetSize = Mathf.Clamp(targetSize, MinimumViewSize, MaximumViewSize);
        if (!mapRenderer) return;
        if (enableFullMapView)
        {
            var area = SafeViewport; var fullBounds = OverviewBounds; var offset = SafeCenterOffset;
            float halfSafeHeight = viewCamera.orthographicSize * area.height;
            float halfSafeWidth = viewCamera.orthographicSize * viewCamera.aspect * area.width;
            var position = transform.position;
            position.x = fullBounds.size.x <= halfSafeWidth * 2 + .001f ? fullBounds.center.x - offset.x
                : Mathf.Clamp(position.x, fullBounds.min.x + halfSafeWidth - offset.x, fullBounds.max.x - halfSafeWidth - offset.x);
            position.y = fullBounds.size.y <= halfSafeHeight * 2 + .001f ? fullBounds.center.y - offset.y
                : Mathf.Clamp(position.y, fullBounds.min.y + halfSafeHeight - offset.y, fullBounds.max.y - halfSafeHeight - offset.y);
            transform.position = position;
            return;
        }
        Bounds bounds = mapRenderer.bounds;
        float halfHeight = viewCamera.orthographicSize;
        float halfWidth = halfHeight * viewCamera.aspect;
        Vector3 p = transform.position;
        p.x = bounds.size.x <= halfWidth * 2f ? bounds.center.x
            : Mathf.Clamp(p.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        p.y = bounds.size.y <= halfHeight * 2f ? bounds.center.y
            : Mathf.Clamp(p.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);
        transform.position = p;
    }
    private void LateUpdate()
    {
        bool visible = enableFullMapView && mapRenderer && overviewBackdropMaterial;
        if (!visible)
        {
            if (overviewBackdrop) overviewBackdrop.gameObject.SetActive(false);
            if (overviewShadow) overviewShadow.gameObject.SetActive(false);
            return;
        }
        if (!overviewBackdrop)
        {
            overviewBackdrop = BackgroundSprite("Forest Overview Backdrop", -20, new Color(.25f, .34f, .29f, 1));
            overviewShadow = BackgroundSprite("Forest Map Shadow", -1, new Color(0, 0, 0, .48f));
        }
        overviewBackdrop.gameObject.SetActive(true); overviewShadow.gameObject.SetActive(true);
        overviewBackdrop.transform.position = new Vector3(transform.position.x, transform.position.y, mapRenderer.transform.position.z + 2);
        var size = overviewBackdrop.sprite.bounds.size;
        float cover = Mathf.Max(viewCamera.orthographicSize * 2 / Mathf.Max(.01f, size.y),
            viewCamera.orthographicSize * viewCamera.aspect * 2 / Mathf.Max(.01f, size.x));
        overviewBackdrop.transform.localScale = Vector3.one * cover * 1.02f;
        overviewShadow.transform.position = mapRenderer.transform.position + new Vector3(.2f, -.2f, 1);
        overviewShadow.transform.rotation = mapRenderer.transform.rotation;
        overviewShadow.transform.localScale = mapRenderer.transform.lossyScale * 1.015f;
    }
    private SpriteRenderer BackgroundSprite(string label, int orderOffset, Color tint)
    {
        var node = new GameObject(label); node.transform.SetParent(transform, false);
        var sprite = node.AddComponent<SpriteRenderer>(); sprite.sprite = mapRenderer.sprite;
        sprite.sharedMaterial = overviewBackdropMaterial; sprite.color = tint;
        sprite.sortingLayerID = mapRenderer.sortingLayerID; sprite.sortingOrder = mapRenderer.sortingOrder + orderOffset;
        sprite.flipX = mapRenderer.flipX; sprite.flipY = mapRenderer.flipY;
        return sprite;
    }
}
