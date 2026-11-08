using UnityEngine;

// Presentation only: spending, targets and damage remain owned by BattleItemController.
public sealed class FireballCastVfx : MonoBehaviour
{
    private const float FlightDuration = .38f;
    private const float BlastDuration = 1.05f;
    private Material material;
    private Mesh disc;
    private MaterialPropertyBlock tint;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private Vector3 origin, target;
    private float radius, elapsed;
    private MeshRenderer fireGlow, fireBody, fireCore, blastGlow, flash, scorch;
    private MeshRenderer[] flames, smoke;
    private LineRenderer[] trails, sparks;
    private LineRenderer shockwave, innerWave;

    public void Initialize(Material sharedMaterial, Vector3 from, Vector3 to, float blastRadius)
    {
        material = sharedMaterial;
        tint = new MaterialPropertyBlock();
        origin = from; origin.z = 0;
        target = to; target.z = 0;
        radius = Mathf.Max(.5f, blastRadius);
        disc = CreateDisc();
        fireGlow = Disc("Fireball Orange Glow", 133);
        fireBody = Disc("Fireball Yellow Body", 134);
        fireCore = Disc("Fireball White Core", 135);
        trails = new[] { Stroke("Flame Tail", .62f, 18, 130), Stroke("Hot Tail", .3f, 18, 131), Stroke("White Tail", .1f, 18, 132) };
        foreach (var line in trails) line.widthCurve = AnimationCurve.Linear(0, .05f, 1, 1);
        scorch = Disc("Impact Scorch", 124);
        blastGlow = Disc("Explosion Glow", 134);
        flash = Disc("Impact Flash", 139);
        shockwave = Stroke("Outer Shockwave", .16f, 80, 138, true);
        innerWave = Stroke("Inner Shockwave", .09f, 80, 137, true);
        flames = new MeshRenderer[10];
        for (int i = 0; i < flames.Length; i++) flames[i] = Disc("Explosion Flame " + i, 135 + i % 2);
        smoke = new MeshRenderer[5];
        for (int i = 0; i < smoke.Length; i++) smoke[i] = Disc("Rising Smoke " + i, 127);
        sparks = new LineRenderer[22];
        for (int i = 0; i < sparks.Length; i++) sparks[i] = Stroke("Flying Ember " + i, .055f + .015f * (i % 3), 2, 140);
        Draw(0);
    }

    private void Update()
    {
        if (!disc || Time.timeScale <= 0) return;
        // Keep a readable effect at 2x/3x; pause still freezes it.
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= FlightDuration + BlastDuration) { Destroy(gameObject); return; }
        Draw(elapsed);
    }

    private void Draw(float time)
    {
        bool flying = time < FlightDuration;
        fireGlow.enabled = fireBody.enabled = fireCore.enabled = flying;
        foreach (var line in trails) line.enabled = flying;
        blastGlow.enabled = flash.enabled = scorch.enabled = !flying;
        shockwave.enabled = innerWave.enabled = !flying;
        foreach (var flame in flames) flame.enabled = !flying;
        foreach (var puff in smoke) puff.enabled = !flying;
        foreach (var spark in sparks) spark.enabled = !flying;
        if (flying)
        {
            float t = Mathf.Clamp01(time / FlightDuration);
            Vector3 head = FlightPoint(t);
            float pulse = 1 + .07f * Mathf.Sin(time * 55);
            Paint(fireGlow, head, .72f * pulse, new Color(1, .19f, .015f, .85f));
            Paint(fireBody, head, .43f * pulse, new Color(1, .62f, .045f, 1));
            Paint(fireCore, head, .22f, new Color(1, .97f, .68f, 1));
            for (int k = 0; k < trails.Length; k++)
            {
                var line = trails[k];
                line.startColor = new Color(1, .12f, 0, 0);
                line.endColor = k == 2 ? new Color(1, .94f, .5f) : new Color(1, .32f + k * .3f, .02f, .9f);
                for (int j = 0; j < line.positionCount; j++)
                    line.SetPosition(j, FlightPoint(Mathf.Lerp(Mathf.Max(0, t - .32f), t, j / (float)(line.positionCount - 1))));
            }
            return;
        }
        float p = Mathf.Clamp01((time - FlightDuration) / BlastDuration);
        float fade = 1 - p;
        float spread = 1 - Mathf.Pow(1 - p, 3);
        Paint(scorch, target, radius * .75f, new Color(.16f, .055f, .015f, .38f * fade));
        Paint(blastGlow, target, radius * Mathf.Lerp(.5f, 1.05f, spread), new Color(1, .25f, .015f, .8f * fade * fade));
        Paint(flash, target, radius * (.28f + p * .65f), new Color(1, .98f, .65f, Mathf.Clamp01(1 - p * 5)));
        Circle(shockwave, radius * Mathf.Lerp(.18f, 1.08f, spread), new Color(1, .68f, .15f, fade * .95f));
        Circle(innerWave, radius * Mathf.Lerp(.08f, .83f, spread), new Color(1, .96f, .62f, fade * fade));
        shockwave.widthMultiplier = .05f + .16f * fade;
        for (int i = 0; i < flames.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / flames.Length;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            float variation = .75f + .25f * Mathf.Sin(i * 7.13f);
            Vector3 position = target + direction * radius * (.12f + .52f * spread) * variation + Vector3.up * p * .4f;
            Paint(flames[i], position, radius * (.28f + p * .14f) * variation,
                new Color(1, .34f + .3f * (i % 2), .025f, .9f * Mathf.Pow(fade, 1.4f)));
        }
        for (int i = 0; i < smoke.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / smoke.Length;
            Vector3 position = target + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius * .4f * spread + Vector3.up * p * 1.1f;
            Paint(smoke[i], position, radius * (.22f + p * .35f), new Color(.13f, .1f, .08f, .35f * Mathf.Sin(p * Mathf.PI)));
        }
        for (int i = 0; i < sparks.Length; i++)
        {
            float angle = i * 2.39996f;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            float distance = radius * (.2f + spread * (.6f + (i % 5) * .13f));
            var head = target + direction * distance + Vector3.down * p * p * .7f;
            sparks[i].SetPosition(0, head - direction * (.14f + .23f * fade));
            sparks[i].SetPosition(1, head);
            sparks[i].startColor = new Color(1, .22f, .01f, 0);
            sparks[i].endColor = new Color(1, .85f, .22f, fade);
        }
    }

    private Vector3 FlightPoint(float t) => Vector3.Lerp(origin, target, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.6f;
    private MeshRenderer Disc(string label, int order)
    {
        var node = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        node.transform.SetParent(transform, false);
        node.GetComponent<MeshFilter>().sharedMesh = disc;
        var renderer = node.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }
    private void Paint(MeshRenderer renderer, Vector3 position, float size, Color color)
    {
        renderer.transform.position = position;
        renderer.transform.localScale = Vector3.one * size;
        tint.SetColor(ColorId, color); renderer.SetPropertyBlock(tint);
    }
    private LineRenderer Stroke(string label, float width, int points, int order, bool loop = false)
    {
        var node = new GameObject(label); node.transform.SetParent(transform, false);
        var line = node.AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = true; line.loop = loop;
        line.positionCount = points; line.widthMultiplier = width; line.sortingOrder = order;
        line.numCapVertices = 3;
        return line;
    }
    private void Circle(LineRenderer line, float size, Color color)
    {
        line.startColor = line.endColor = color;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / line.positionCount;
            line.SetPosition(i, target + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * size);
        }
    }
    private static Mesh CreateDisc()
    {
        const int segments = 48;
        var vertices = new Vector3[segments + 1];
        var colors = new Color[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * 3];
        colors[0] = Color.white; uv[0] = Vector2.one * .5f;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            colors[i + 1] = new Color(1, 1, 1, 0);
            uv[i + 1] = (Vector2)vertices[i + 1] * .5f + Vector2.one * .5f;
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }
        var mesh = new Mesh { name = "Fireball Soft Disc" };
        mesh.vertices = vertices; mesh.colors = colors; mesh.uv = uv; mesh.triangles = triangles;
        mesh.RecalculateBounds(); return mesh;
    }
    private void OnDestroy() { if (disc) Destroy(disc); }
}
