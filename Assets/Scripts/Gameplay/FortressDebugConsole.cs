using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// An explicit tool inside pause/settings. It shares the pause shade and cannot click through it.
public sealed class FortressDebugConsole : MonoBehaviour
{
    public bool IsOpen => gameObject.activeSelf;
    private WaveGameController game;
    private FortressHud hud;
    private GameObject settings;
    private InputField waveInput;
    private Text preview, feedback;
    private Button jump;
    private Font font;
    private readonly Color cream = new Color32(242,234,213,255);
    private readonly Color gold = new Color32(222,183,107,255);

    public static FortressDebugConsole Create(Transform parent, GameObject settings, Font font, WaveGameController game, FortressHud hud)
    {
        var node = new GameObject("Debug Console", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(FortressDebugConsole));
        node.transform.SetParent(parent, false);
        var rect = node.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        rect.sizeDelta = new Vector2(520, 432);
        node.GetComponent<Image>().color = new Color32(20,29,38,255);
        var console = node.GetComponent<FortressDebugConsole>();
        console.game = game; console.hud = hud; console.settings = settings; console.font = font;
        console.Build(); node.SetActive(false); return console;
    }
    private void Build()
    {
        Label("调试台 · 跳转波次", 28, 22, 464, 42, 24, cream);
        Label(game.levelName + "    共 " + game.TotalWaves + " 波", 28, 68, 464, 28, 14, gold);
        Label("目标波次", 28, 122, 110, 42, 17, cream);
        Button("Previous Wave", "−", 152, 122, 46, 42, () => Step(-1));
        var field = Box("Target Wave", 206, 122, 104, 42, new Color32(39,54,65,255));
        waveInput = field.gameObject.AddComponent<InputField>();
        var text = Label("1", 0, 0, 104, 42, 22, cream, field.transform);
        text.alignment = TextAnchor.MiddleCenter;
        waveInput.textComponent = text;
        waveInput.targetGraphic = field;
        waveInput.contentType = InputField.ContentType.IntegerNumber;
        waveInput.characterLimit = Mathf.Max(1, game.TotalWaves.ToString().Length);
        waveInput.onValueChanged.AddListener(_ => Refresh());
        Button("Next Wave", "+", 318, 122, 46, 42, () => Step(1));
        Label("1 — " + game.TotalWaves, 380, 122, 112, 42, 15, cream);
        preview = Label("", 28, 181, 464, 32, 18, gold);
        Label("保留防御塔与当前生命，清除场上的敌人与投射物。\n补齐此前波次的击杀金币，已发放的奖励不重复领取。\n向前或向后跳转都从目标波次开头重新出怪。", 28, 222, 464, 80, 14, cream);
        feedback = Label("", 28, 308, 464, 28, 13, new Color32(245,160,140,255));
        Button("Back to Settings", "返回设置", 28, 354, 180, 48, Close);
        jump = Button("Jump to Wave", "跳转并开始", 224, 354, 268, 48, Jump, true);
        var items = GetComponentsInChildren<Selectable>();
        for (int i = 0; i < items.Length; i++) items[i].navigation = new Navigation {
            mode = Navigation.Mode.Explicit,
            selectOnUp = items[(i+items.Length-1)%items.Length], selectOnDown = items[(i+1)%items.Length]
        };
    }
    public void Open()
    {
        if (!game.IsPaused) return;
        settings.SetActive(false); gameObject.SetActive(true);
        waveInput.text = Mathf.Clamp(game.CurrentWave > 0 ? game.CurrentWave : 1, 1, Mathf.Max(1,game.TotalWaves)).ToString();
        feedback.text = ""; Refresh();
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(waveInput.gameObject);
    }
    public void Close()
    {
        gameObject.SetActive(false); settings.SetActive(true);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(settings.GetComponentInChildren<Button>().gameObject);
    }
    private void Step(int delta)
    {
        int.TryParse(waveInput.text, out var number);
        waveInput.text = Mathf.Clamp(number + delta, 1, Mathf.Max(1,game.TotalWaves)).ToString();
    }
    private void Refresh()
    {
        if (!jump) return;
        bool valid = int.TryParse(waveInput.text, out var target) && target >= 1 && target <= game.TotalWaves;
        jump.interactable = valid && game.Lives > 0;
        preview.text = valid ? "补发金币：+" + game.GetDebugJumpGold(target) + "    当前金币：" + game.Gold : "请输入 1 — " + game.TotalWaves + " 之间的波次";
        feedback.text = game.Lives <= 0 ? "基地生命已耗尽，请重新开始后调试。" : "";
    }
    private void Jump()
    {
        if (!int.TryParse(waveInput.text, out var target)) return;
        if (!game.TryDebugJumpToWave(target, out _)) { feedback.text = "无法跳转，请检查该波路线及敌人配置。"; return; }
        hud.ShowPauseMenu(false);
    }
    private Text Label(string value, float x, float y, float width, float height, int size, Color color, Transform parent = null)
    {
        var node = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        node.transform.SetParent(parent ? parent : transform, false);
        var text = node.GetComponent<Text>(); text.font = font; text.text = value;
        text.fontSize = size; text.color = color; text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleLeft;
        Place(text.rectTransform, x,y,width,height); return text;
    }
    private Image Box(string name, float x, float y, float width, float height, Color color)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        node.transform.SetParent(transform, false);
        var image = node.GetComponent<Image>(); image.color = color;
        Place(image.rectTransform,x,y,width,height); return image;
    }
    private Button Button(string name, string title, float x, float y, float width, float height, UnityEngine.Events.UnityAction action, bool primary = false)
    {
        var face = Box(name,x,y,width,height,primary ? gold : new Color32(43,57,68,255));
        var button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face;
        button.onClick.AddListener(action);
        Label(title,0,0,width,height,17,primary ? new Color32(15,23,29,255) : cream,face.transform).alignment = TextAnchor.MiddleCenter;
        return button;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height);
    }
}
