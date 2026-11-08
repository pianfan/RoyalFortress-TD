using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class BattleItemTargeting : MonoBehaviour
{
    public Material effectMaterial;
    public bool IsTargeting { get; private set; }
    public string Feedback { get; private set; }
    private BattleItemController items;
    private LineRenderer circle;
    private Material material;
    private bool ownsMaterial;
    private int armedFrame;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private void Awake() { items = GetComponent<BattleItemController>(); }
    public bool Begin()
    {
        Feedback = items.UnavailableReason(BattleItemKind.Fireball);
        if (Feedback != null) { FortressAudio.Play(FortressSound.Invalid); return false; }
        if (items.building) items.building.Cancel();
        IsTargeting = true; armedFrame = Time.frameCount + 1;
        Feedback = "点击敌群投放 · 右键取消";
        if (!circle)
        {
            ownsMaterial = !effectMaterial;
            material = effectMaterial ? effectMaterial : new Material(Shader.Find("Sprites/Default"));
            var node = new GameObject("Fireball Target Preview"); node.transform.SetParent(transform, false);
            circle = node.AddComponent<LineRenderer>(); circle.sharedMaterial = material;
            circle.useWorldSpace = true; circle.loop = true; circle.positionCount = 80;
            circle.widthMultiplier = .055f; circle.sortingOrder = 140;
        }
        circle.enabled = false;
        return true;
    }
    private void Update()
    {
        if (!IsTargeting) return;
        if (items.UnavailableReason(BattleItemKind.Fireball) != null) { Cancel(); return; }
        Vector2 pointer = Vector2.zero; bool pressed = false, cancel = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            pointer = Mouse.current.position.ReadValue(); pressed = Mouse.current.leftButton.wasPressedThisFrame;
            cancel = Mouse.current.rightButton.wasPressedThisFrame;
        }
        cancel |= Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        pointer = Input.mousePosition; pressed = Input.GetMouseButtonDown(0);
        cancel = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
#endif
        if (cancel) { Cancel(); return; }
        var camera = items.building ? items.building.worldCamera : null;
        bool overUI = PointerOverUI(pointer);
        if (!camera || overUI || !camera.pixelRect.Contains(pointer)) { circle.enabled = false; return; }
        Vector3 point = camera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, -camera.transform.position.z)); point.z = 0;
        bool inside = items.IsMapPoint(point);
        circle.enabled = inside;
        if (inside)
        {
            Color color = items.HasFireballTarget(point) ? items.fireball.color : new Color(1, .3f, .25f);
            circle.startColor = circle.endColor = color;
            for (int i = 0; i < circle.positionCount; i++)
            {
                float a = i * Mathf.PI * 2 / circle.positionCount;
                circle.SetPosition(i, point + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * items.fireball.radius);
            }
        }
        if (pressed && Time.frameCount > armedFrame)
        {
            if (!items.TryUse(BattleItemKind.Fireball, point, out var reason))
            { Feedback = reason; FortressAudio.Play(FortressSound.Invalid); }
        }
    }
    public bool PointerOverUI(Vector2 position)
    {
        if (!EventSystem.current) return false;
        hits.Clear(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        foreach (var hit in hits) if (hit.module is GraphicRaycaster) return true;
        return false;
    }
    public void Cancel() { IsTargeting = false; Feedback = null; if (circle) circle.enabled = false; }
    private void OnDisable() { Cancel(); }
    private void OnApplicationFocus(bool focus) { if (!focus) Cancel(); }
    private void OnDestroy() { if (ownsMaterial && material) Destroy(material); }
}
