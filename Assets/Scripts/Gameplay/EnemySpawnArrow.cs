using UnityEngine;

[RequireComponent(typeof(WaypointPath))]
public sealed class EnemySpawnArrow : MonoBehaviour
{
    public WaveGameController game;
    public Material arrowMaterial;
    public Color color = new Color(1, .65f, .12f, 1);
    private LineRenderer outline, arrow;
    private Vector3 origin, direction;
    private Material ownedMaterial;
    private WaypointPath route;
    public bool IsVisible => arrow && arrow.enabled;
    private void Start()
    {
        route = GetComponent<WaypointPath>();
        var points = route.GetPositions();
        if (points.Length < 2) { enabled = false; return; }
        origin = points[0];
        for (int i = 1; i < points.Length; i++)
            if ((points[i] - origin).sqrMagnitude > .01f) { direction = (points[i] - origin).normalized; break; }
        if (direction.sqrMagnitude < .1f) { enabled = false; return; }
        if (!arrowMaterial) { ownedMaterial = new Material(Shader.Find("Sprites/Default")); arrowMaterial = ownedMaterial; }
        outline = Stroke("Entrance Arrow Outline", .22f, 7);
        arrow = Stroke("Entrance Arrow", .11f, 8);
        RefreshArrow();
    }
    private LineRenderer Stroke(string name, float width, int order)
    {
        var child = new GameObject(name); child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>(); line.sharedMaterial = arrowMaterial;
        line.useWorldSpace = true; line.positionCount = 5; line.widthMultiplier = width; line.sortingOrder = order;
        line.numCapVertices = 3; line.numCornerVertices = 3;
        return line;
    }
    private void LateUpdate() { RefreshArrow(); }
    private void OnDisable()
    {
        if (outline) outline.enabled = false;
        if (arrow) arrow.enabled = false;
    }
    private void RefreshArrow()
    {
        if (!arrow) return;
        bool visible = game && game.IsSpawnEntrance(route);
        outline.enabled = arrow.enabled = visible;
        if (visible) Draw();
    }
    private void Draw()
    {
        Vector3 tail = origin + direction * (1.0f + .1f * Mathf.Sin(Time.time * 3));
        Vector3 tip = tail + direction * 1.3f;
        Vector3 side = new Vector3(-direction.y, direction.x, 0) * .37f;
        var positions = new[] { tail, tip, tip - direction * .48f + side, tip, tip - direction * .48f - side };
        outline.SetPositions(positions); arrow.SetPositions(positions);
        outline.startColor = outline.endColor = new Color(.05f, .06f, .05f, .9f);
        var tint = color; tint.a = .85f + .15f * Mathf.Sin(Time.time * 3);
        arrow.startColor = arrow.endColor = tint;
    }
    private void OnDestroy() { if (ownedMaterial) Destroy(ownedMaterial); }
}
