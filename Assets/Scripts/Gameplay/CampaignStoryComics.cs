using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RoyalFortress.ForestSanctuary;
using RoyalFortress.GateOfDead;

public sealed class CampaignStoryComics : MonoBehaviour
{
    public WaveGameController game;
    public Sprite entry, victory;
    public string chapterTitle, entryCaption, victoryCaption;
    public int chapterNumber;
    public bool IsOpen { get; private set; }
    public bool IsVictoryPage { get; private set; }
    public int VictoryShowCount { get; private set; }
    public Canvas OverlayCanvas { get; private set; }
    public Image ComicImage { get; private set; }
    public Button ContinueButton => action;
    public Button SkipButton { get; private set; }
    private bool victoryShown,previousPause,ready;
    private GameObject overlay;
    private RectTransform page;
    private Font font;
    private Text title,caption,actionLabel,pageLabel;
    private Button action;
    private FortressHud hud;
    private GraphicRaycaster hudRaycaster;
    private bool previousRaycaster;
    private GameObject previousSelection;
    private ForestSanctuaryGameplay forest;
    private GateOfDeadEncounter gate;
    private readonly Color ink=new Color32(46,36,31,255);

    private void Awake(){if(game)game.StoryOverlayActive=true;}
    private IEnumerator Start()
    {
        hud=FindFirstObjectByType<FortressHud>();forest=FindFirstObjectByType<ForestSanctuaryGameplay>();gate=FindFirstObjectByType<GateOfDeadEncounter>();
        while(hud&&!hud.IsReady)yield return null;
        if(!game||!entry||!victory){Debug.LogError("Story comic configuration is incomplete",this);if(game)game.StoryOverlayActive=false;yield break;}
        CreateUI();ready=true;game.DebugWaveJumped+=ResetForDebug;Show(false);
    }
    private void Update()
    {
        if(!ready)return;
        if(IsOpen){FitPage();return;}
        if(!victoryShown&&game.AllWavesFinished&&!game.IsGameOver&&game.Lives>0&&(!forest||forest.VictoryFinished)&&(!gate||gate.VictoryFinished))
        {victoryShown=true;VictoryShowCount++;Show(true);}
    }
    public void Show(bool won)
    {
        if(!ready||IsOpen)return;
        IsVictoryPage=won;previousPause=game.IsPaused;game.StoryOverlayActive=true;IsOpen=true;
        var build=FindFirstObjectByType<TowerBuildController>();if(build)build.Cancel();
        previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        hudRaycaster=hud?hud.GetComponent<GraphicRaycaster>():null;
        if(hudRaycaster){previousRaycaster=hudRaycaster.enabled;hudRaycaster.enabled=false;}
        game.SetPaused(true);
        ComicImage.sprite=won?victory:entry;caption.text=won?victoryCaption:entryCaption;
        title.text=chapterTitle+"  /  "+(won?"胜利篇":"启程篇");
        pageLabel.text=chapterNumber.ToString("D2")+"  —  "+(won?"02":"01");
        actionLabel.text=won?"继续  ›":"进入战场  ›";
        overlay.SetActive(true);FitPage();Canvas.ForceUpdateCanvases();
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(action.gameObject);
    }
    public void Close()
    {
        if(!IsOpen)return;
        IsOpen=false;overlay.SetActive(false);
        if(hudRaycaster)hudRaycaster.enabled=previousRaycaster;
        game.StoryOverlayActive=false;game.SetPaused(previousPause);
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(previousSelection&&previousSelection.activeInHierarchy?previousSelection:null);
    }
    private void ResetForDebug(){Close();victoryShown=false;}
    private void OnDisable(){if(IsOpen)Close();if(game)game.StoryOverlayActive=false;}
    private void OnDestroy(){if(game)game.DebugWaveJumped-=ResetForDebug;if(overlay)Destroy(overlay);if(font)Destroy(font);}
    private void FitPage()
    {
        var r=OverlayCanvas.GetComponent<RectTransform>().rect;
        float scale=Mathf.Min((r.width-32)/1320f,(r.height-32)/846f);
        page.localScale=Vector3.one*Mathf.Max(.1f,Mathf.Min(1,scale));
    }
    private Image Box(string name,Transform parent,Color color,float x,float y,float w,float h)
    {
        var o=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));o.transform.SetParent(parent,false);
        var image=o.GetComponent<Image>();image.color=color;image.raycastTarget=false;Place(image.rectTransform,x,y,w,h);return image;
    }
    static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    private Text Label(Transform parent,string value,float x,float y,float w,float h,int size,Color color)
    {
        var o=new GameObject("Comic text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));o.transform.SetParent(parent,false);
        var t=o.GetComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.raycastTarget=false;
        t.alignment=TextAnchor.MiddleLeft;Place(t.rectTransform,x,y,w,h);return t;
    }
    private Button Button(Transform parent,string value,float x,float y,float w,float h,bool filled,out Text label)
    {
        var edge=Box(value,parent,ink,x,y,w,h);
        var face=Box("Button face",edge.transform,filled?ink:new Color32(234,219,188,255),2,2,w-4,h-4);face.raycastTarget=true;
        var b=face.gameObject.AddComponent<Button>();b.targetGraphic=face;b.onClick.AddListener(Close);
        label=Label(face.transform,value,6,0,w-16,h-4,19,filled?new Color32(248,238,214,255):ink);label.alignment=TextAnchor.MiddleCenter;
        return b;
    }
    private void CreateUI()
    {
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
        overlay=new GameObject("Story Comic Overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        OverlayCanvas=overlay.GetComponent<Canvas>();OverlayCanvas.renderMode=RenderMode.ScreenSpaceOverlay;OverlayCanvas.sortingOrder=1000;
        var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        var shade=Box("Reading shade",overlay.transform,new Color32(12,15,20,240),0,0,0,0);shade.rectTransform.anchorMax=Vector2.one;shade.rectTransform.anchorMin=Vector2.zero;shade.rectTransform.sizeDelta=Vector2.zero;shade.raycastTarget=true;
        var holder=new GameObject("Comic page",typeof(RectTransform));holder.transform.SetParent(overlay.transform,false);page=holder.GetComponent<RectTransform>();page.anchorMin=page.anchorMax=page.pivot=Vector2.one*.5f;page.sizeDelta=new Vector2(1320,846);
        Box("Paper shadow",page,new Color32(0,0,0,110),10,12,1320,846);
        Box("Underlying sheet",page,new Color32(157,137,107,255),-5,5,1320,838);
        var paper=new GameObject("Textured paper",typeof(RectTransform),typeof(CanvasRenderer),typeof(ComicPaperGraphic));paper.transform.SetParent(page,false);Place(paper.GetComponent<RectTransform>(),0,0,1320,846);paper.GetComponent<ComicPaperGraphic>().color=new Color32(242,229,204,255);paper.GetComponent<ComicPaperGraphic>().raycastTarget=false;
        title=Label(page,"",40,8,950,38,23,ink);title.fontStyle=FontStyle.Bold;
        pageLabel=Label(page,"",1030,8,120,38,16,ink);
        SkipButton=Button(page,"跳过",1170,12,110,32,false,out _);
        Box("Outer ink",page,ink,36,51,1248,706);
        Box("Inner paper",page,new Color32(251,242,222,255),38,53,1244,702);
        ComicImage=Box("Comic illustration",page,Color.white,40,55,1240,698);ComicImage.preserveAspect=true;
        caption=Label(page,"",42,768,970,62,22,ink);
        action=Button(page,"",1040,778,240,45,true,out actionLabel);
        var nav=new Navigation{mode=Navigation.Mode.None};action.navigation=nav;
        overlay.SetActive(false);
    }
}
