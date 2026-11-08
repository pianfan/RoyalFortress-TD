using System.Collections.Generic;
using KingdomBorder.TwinTowers;
using UnityEngine;

public sealed class BattleItemVfx : MonoBehaviour
{
    public Material effectMaterial;
    private sealed class Burst
    {
        public LineRenderer ring;
        public Vector3 target;
        public Color color;
        public float time, radius;
    }
    private BattleItemController items;
    private Material material;
    private bool ownsMaterial;
    private readonly List<Burst> bursts = new List<Burst>();
    private readonly List<FireballCastVfx> fireballs = new List<FireballCastVfx>();
    private readonly Dictionary<TowerBehaviour, LineRenderer> towerRings = new Dictionary<TowerBehaviour, LineRenderer>();
    private readonly List<TowerBehaviour> expired = new List<TowerBehaviour>();
    private void Awake()
    {
        items = GetComponent<BattleItemController>();
        ownsMaterial = !effectMaterial;
        material = effectMaterial ? effectMaterial : new Material(Shader.Find("Sprites/Default"));
    }
    private LineRenderer Line(string name, float width, int points, bool loop)
    {
        var node = new GameObject(name); node.transform.SetParent(transform, false);
        var line = node.AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = true; line.loop = loop;
        line.positionCount = points; line.widthMultiplier = width; line.sortingOrder = 120;
        return line;
    }
    private static void Ring(LineRenderer line, Vector3 center, float radius, Color color)
    {
        line.startColor = line.endColor = color;
        for (int i = 0; i < line.positionCount; i++)
        {
            float a = i * Mathf.PI * 2 / line.positionCount;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius);
        }
    }
    public void Show(BattleItemDefinition item, Vector3 origin, Vector3 target)
    {
        if (item.kind == BattleItemKind.Fireball)
        {
            var node = new GameObject("Fireball Cast Effect"); node.transform.SetParent(transform, false);
            var fireball = node.AddComponent<FireballCastVfx>();
            fireball.Initialize(material, origin, target, item.radius);
            fireballs.Add(fireball);
            return;
        }
        bursts.Add(new Burst {
            ring = Line("Haste Cast", .12f, 64, true),
            target = origin, color = item.color, radius = .9f
        });
    }
    private void LateUpdate()
    {
        fireballs.RemoveAll(fireball => !fireball);
        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            var fx = bursts[i]; fx.time += Time.deltaTime;
            if (fx.time >= .85f)
            {
                if (fx.ring) Destroy(fx.ring.gameObject);
                bursts.RemoveAt(i); continue;
            }
            var color = fx.color; color.a = Mathf.Clamp01(1 - fx.time / .85f);
            Ring(fx.ring, fx.target, fx.radius * Mathf.Clamp01(.2f + fx.time * 2), color);
        }
        if (items && items.HasteActive && items.building && items.building.builtTowersRoot)
        {
            foreach (var tower in items.building.builtTowersRoot.GetComponentsInChildren<TowerBehaviour>())
            {
                if (!tower.attackEnabled || !tower.definition || tower.definition.kind == TowerKind.Frost) continue;
                if (!towerRings.TryGetValue(tower, out var line))
                { line = Line("Haste Tower Aura", .045f, 32, true); towerRings.Add(tower, line); }
                var color = items.haste.color; color.a = .55f + .2f * Mathf.Sin(Time.time * 7);
                Ring(line, tower.transform.position, .55f + .04f * Mathf.Sin(Time.time * 7), color);
            }
        }
        expired.Clear();
        foreach (var pair in towerRings)
            if (!items || !items.HasteActive || !pair.Key || !pair.Key.isActiveAndEnabled || !pair.Key.attackEnabled)
            { if (pair.Value) Destroy(pair.Value.gameObject); expired.Add(pair.Key); }
        foreach (var tower in expired) towerRings.Remove(tower);
    }
    public void Clear()
    {
        foreach (var fireball in fireballs) if (fireball) { fireball.gameObject.SetActive(false); Destroy(fireball.gameObject); }
        fireballs.Clear();
        foreach (var fx in bursts) if (fx.ring) Destroy(fx.ring.gameObject);
        bursts.Clear();
        foreach (var line in towerRings.Values) if (line) Destroy(line.gameObject);
        towerRings.Clear();
    }
    private void OnDisable() { Clear(); }
    private void OnDestroy() { if (ownsMaterial && material) Destroy(material); }
}
