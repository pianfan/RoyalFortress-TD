using UnityEngine;
using UnityEngine.UI;

// A paper surface authored as UI geometry so it stays sharp at every screen size.
public sealed class ComicPaperGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        const int segments=96;
        for(int i=0;i<segments;i++){
            float a=i/(float)segments,b=(i+1)/(float)segments;
            float x0=Mathf.Lerp(r.xMin,r.xMax,a),x1=Mathf.Lerp(r.xMin,r.xMax,b);
            float top0=r.yMax-Noise(i)*2.4f,top1=r.yMax-Noise(i+1)*2.4f;
            float bot0=r.yMin+Noise(i+97)*2.4f,bot1=r.yMin+Noise(i+98)*2.4f;
            Quad(vh,new Vector2(x0,bot0),new Vector2(x0,top0),new Vector2(x1,top1),new Vector2(x1,bot1),color);
        }
        for(int i=0;i<1600;i++){
            float x=Mathf.Lerp(r.xMin+3,r.xMax-3,Noise(i+400)),y=Mathf.Lerp(r.yMin+3,r.yMax-3,Noise(i+3000));
            float s=.35f+Noise(i+7000)*.75f;
            Quad(vh,new Vector2(x,y),new Vector2(x,y+s),new Vector2(x+s,y+s),new Vector2(x+s,y),new Color(.38f,.27f,.15f,.055f));
        }
    }
    static float Noise(int x){uint n=(uint)x*747796405u+2891336453u;n=((n>>((int)(n>>28)+4))^n)*277803737u;return ((n>>22)^n)/(float)uint.MaxValue;}
    static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint){int i=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddVert(d,tint,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
}
