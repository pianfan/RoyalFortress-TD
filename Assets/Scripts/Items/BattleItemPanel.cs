using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattleItemPanel : MonoBehaviour
{
    private BattleItemController items;
    private BattleItemTargeting targeting;
    private RectTransform panel;
    private Font font;
    private FortressHud hud;
    private readonly Text[] names = new Text[2], details = new Text[2];
    private readonly Text[] prices = new Text[2], states = new Text[2];
    private readonly Button[] buttons = new Button[2];
    private int hovering = -1;
    private string feedback;
    private float feedbackUntil, refreshAt;
    private readonly Color cream = new Color32(242, 234, 213, 255);
    private readonly Color gold = new Color32(222, 183, 107, 255);

    private IEnumerator Start()
    {
        items = GetComponent<BattleItemController>(); targeting = GetComponent<BattleItemTargeting>();
        hud = FindFirstObjectByType<FortressHud>();
        if (!items || !hud) yield break;
        while (!hud.IsReady) yield return null;
        font = hud.lifeText.font;
        panel = hud.SkillCardsRoot as RectTransform;
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            var kind = (BattleItemKind)i;
            var item = items.Definition(kind);
            var buttonFace = Box("Use " + kind, panel, new Color32(20, 30, 38, 235), 4 + i * 326, 30, 308, 94);
            var accent = Box("Item Accent", buttonFace.transform, item ? item.color : gold, 0, 0, 308, 3);
            accent.raycastTarget = false;
            buttons[i] = buttonFace.gameObject.AddComponent<Button>(); buttons[i].targetGraphic = buttonFace;
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.None };
            buttons[i].onClick.AddListener(() => Use(index));
            var icon = new GameObject("Item Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleItemIcon));
            icon.transform.SetParent(buttonFace.transform, false);
            Place(icon.GetComponent<RectTransform>(), 12, 16, 38, 60);
            var graphic = icon.GetComponent<BattleItemIcon>(); graphic.kind = kind;
            graphic.color = item ? item.color : gold; graphic.raycastTarget = false;
            names[i] = Label(buttonFace.transform, "", 65, 7, 230, 28, 20, cream);
            details[i] = Label(buttonFace.transform, "", 65, 37, 230, 20, 12, item ? item.color : gold);
            prices[i] = Label(buttonFace.transform, "", 65, 64, 100, 24, 17, gold);
            states[i] = Label(buttonFace.transform, "", 170, 64, 126, 24, 11, cream);
            var hover = buttonFace.gameObject.AddComponent<BattleItemHover>(); hover.panel = this; hover.index = index;
        }
        Refresh();
    }
    private void Update()
    {
        if (panel && Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .1f; Refresh(); }
    }
    private void Use(int index)
    {
        if (index == 0)
        {
            if (targeting.IsTargeting) targeting.Cancel(); else targeting.Begin();
        }
        else
        {
            targeting.Cancel(); if (items.building) items.building.Cancel();
            bool used = items.TryUse(BattleItemKind.Haste, Vector3.zero, out var reason);
            feedback = used ? "攻速强化已生效" : reason; feedbackUntil = Time.unscaledTime + 2.5f;
            if (!used) FortressAudio.Play(FortressSound.Invalid);
        }
        Refresh();
    }
    public void Hover(int index) { hovering = index; Refresh(); }
    public void RefreshView() { Refresh(); }
    private void Refresh()
    {
        if (!panel) return;
        bool unlocked = items.UnlockedForRun;
        if (!hud.SkillsVisible || hud.DockCollapsed) hovering = -1;
        for (int i = 0; i < 2; i++)
        {
            var kind = (BattleItemKind)i; var item = items.Definition(kind);
            if (!item) continue;
            var reason = items.UnavailableReason(kind);
            buttons[i].interactable = reason == null;
            names[i].text = item.displayName;
            details[i].text = i == 0 ? "伤害 " + item.damage + " · 半径 " + item.radius : "攻速 +" + Mathf.RoundToInt((item.attackSpeed - 1) * 100) + "% · 持续 " + item.duration + " 秒";
            prices[i].text = item.price + " 金币";
            states[i].text = reason != null ? reason.StartsWith("金币不足") ? "金币不足" : reason
                : i == 0 ? targeting.IsTargeting ? "选点中 · 再点取消" : "点击选点" : "立即施放";
        }
        string message;
        if (!unlocked) message = CampaignProgress.ItemsUnlocked ? "已获得技能，下次进入关卡可用" : "通关圣林加护解锁火球与攻速强化";
        else if (targeting.IsTargeting) message = targeting.Feedback;
        else if (feedbackUntil > Time.unscaledTime) message = feedback;
        else if (hovering == 0) message = "火球范围伤害 · 空放不消耗金币和次数";
        else if (hovering == 1) message = "强化期间新建攻击塔也生效 · 冰霜光环不受影响";
        else if (items.HasteActive) message = "攻速强化剩余 " + Mathf.CeilToInt(items.HasteRemaining) + " 秒";
        else message = "两种技能每关共用 3 次 · 施放另需金币";
        hud.SetSkillDockState(items.RemainingCount, unlocked, message);
    }
    private Image Box(string name, Transform parent, Color color, float x, float y, float w, float h)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); node.transform.SetParent(parent, false);
        var image = node.GetComponent<Image>(); image.color = color; Place(image.rectTransform, x, y, w, h); return image;
    }
    private Text Label(Transform parent, string value, float x, float y, float w, float h, int size, Color color)
    {
        var node = new GameObject("Item Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); node.transform.SetParent(parent, false);
        var text = node.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.color = color;
        text.raycastTarget = false; text.alignment = TextAnchor.MiddleLeft; Place(text.rectTransform, x, y, w, h); return text;
    }
    private static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up; rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
    }
}

public sealed class BattleItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public BattleItemPanel panel;
    public int index;
    public void OnPointerEnter(PointerEventData data) { panel.Hover(index); }
    public void OnPointerExit(PointerEventData data) { panel.Hover(-1); }
}
