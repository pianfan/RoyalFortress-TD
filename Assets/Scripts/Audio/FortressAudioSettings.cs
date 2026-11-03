using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shared small panel, used on the title page and inside the pause modal.
public sealed class FortressAudioSettings : MonoBehaviour, IPointerUpHandler
{
    private Font font;
    private Slider music, effects;
    private Text musicValue, effectsValue;
    public static void Create(Transform parent, Font font, Vector2 topLeft, float width)
    {
        var node = new GameObject("Audio Settings", typeof(RectTransform), typeof(FortressAudioSettings));
        node.transform.SetParent(parent, false);
        Place(node.GetComponent<RectTransform>(), topLeft.x, topLeft.y, width, 140);
        var panel = node.GetComponent<FortressAudioSettings>(); panel.font = font;
        panel.Label("声音设置", 0, 0, width, 28, 15);
        panel.music = panel.Row("音乐", "Music Volume", 34, width, out panel.musicValue);
        panel.effects = panel.Row("音效", "Effects Volume", 84, width, out panel.effectsValue);
        panel.music.onValueChanged.AddListener(value => {
            if (FortressAudio.Instance) FortressAudio.Instance.SetMusicVolume(value);
            panel.musicValue.text = Mathf.RoundToInt(value*100) + "%";
        });
        panel.effects.onValueChanged.AddListener(value => {
            if (FortressAudio.Instance) FortressAudio.Instance.SetEffectsVolume(value);
            panel.effectsValue.text = Mathf.RoundToInt(value*100) + "%";
        });
        panel.Refresh();
    }
    private Slider Row(string caption, string name, float y, float width, out Text value)
    {
        Label(caption, 0, y, 46, 32, 14);
        value = Label("", width-48, y, 48, 32, 13);
        value.alignment = TextAnchor.MiddleRight;
        var track = Image(name, transform, new Color32(37, 52, 62, 255));
        Place(track.rectTransform, 55, y, width-114, 32);
        var slider = track.gameObject.AddComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 1;
        var line = Image("Track", track.transform, new Color32(72, 87, 93, 255));
        Stretch(line.rectTransform, new Vector2(8, 13), new Vector2(-8,-13));
        var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(track.transform, false); Stretch(fillArea, new Vector2(8,13), new Vector2(-8,-13));
        var fill = Image("Fill", fillArea, new Color32(223, 185, 111, 255));
        Stretch(fill.rectTransform, Vector2.zero, Vector2.zero); fill.raycastTarget = false;
        var handleArea = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(track.transform, false); Stretch(handleArea, new Vector2(8,0), new Vector2(-8,0));
        var handle = Image("Handle", handleArea, new Color32(244, 235, 214, 255));
        handle.rectTransform.sizeDelta = new Vector2(12, -8);
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }
    private void OnEnable() { Refresh(); }
    private void Refresh()
    {
        if (!music || !effects || !FortressAudio.Instance) return;
        music.SetValueWithoutNotify(FortressAudio.Instance.MusicVolume);
        effects.SetValueWithoutNotify(FortressAudio.Instance.EffectsVolume);
        musicValue.text = Mathf.RoundToInt(music.value * 100) + "%";
        effectsValue.text = Mathf.RoundToInt(effects.value * 100) + "%";
    }
    public void OnPointerUp(PointerEventData data) { FortressAudio.Play(FortressSound.Click); }
    private Text Label(string text, float x, float y, float width, float height, int size)
    {
        var node = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        node.transform.SetParent(transform, false);
        var label = node.GetComponent<Text>(); label.font = font; label.fontSize = size;
        label.color = new Color32(232,226,211,255); label.text = text;
        label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
        Place(label.rectTransform, x, y, width, height); return label;
    }
    private static Image Image(string name, Transform parent, Color color)
    {
        var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        node.transform.SetParent(parent, false); var image = node.GetComponent<Image>(); image.color = color; return image;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width,height);
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = min; rect.offsetMax = max;
    }
}
