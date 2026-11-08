using UnityEngine;
using UnityEngine.UI;

public sealed class BattleItemIcon : MaskableGraphic
{
    public BattleItemKind kind;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (kind == BattleItemKind.Fireball)
        {
            var points = new[] { new Vector2(.1f,.35f), new Vector2(.2f,.7f), new Vector2(.35f,.5f), new Vector2(.55f,1),
                new Vector2(.7f,.67f), new Vector2(.9f,.5f), new Vector2(.95f,.25f), new Vector2(.75f,.05f), new Vector2(.35f,0) };
            for (int i = 0; i < points.Length; i++) Triangle(vh, new Vector2(.52f,.35f), points[i], points[(i+1)%points.Length], color);
            Triangle(vh, new Vector2(.3f,.15f), new Vector2(.53f,.6f), new Vector2(.75f,.15f), new Color(1, .91f, .45f));
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                float y = .06f + i * .3f;
                Quad(vh, new Vector2(.1f,y), new Vector2(.5f,y+.2f), new Vector2(.5f,y+.32f), new Vector2(.1f,y+.12f));
                Quad(vh, new Vector2(.5f,y+.2f), new Vector2(.9f,y), new Vector2(.9f,y+.12f), new Vector2(.5f,y+.32f));
            }
        }
    }
    private Vector3 Point(Vector2 value) => new Vector3(rectTransform.rect.xMin + value.x * rectTransform.rect.width, rectTransform.rect.yMin + value.y * rectTransform.rect.height, 0);
    private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
    { int i = vh.currentVertCount; vh.AddVert(Point(a), tint, Vector2.zero); vh.AddVert(Point(b), tint, Vector2.zero); vh.AddVert(Point(c), tint, Vector2.zero); vh.AddTriangle(i, i+1, i+2); }
    private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    { Triangle(vh, a, b, c, color); Triangle(vh, a, c, d, color); }
}
