using System.Collections.Generic;
using UnityEngine;

namespace KingdomBorder.TwinTowers
{
    public sealed class MagicProjectile : MonoBehaviour
    {
        public SpriteRenderer body;
        public Sprite haloSprite;
        public Material trailMaterial;
        private EnemyAgent target;
        private float damage, penetration, slow, duration, speed, age, splashRadius;
        private int source;
        private bool launched, fancyMagic;
        private TowerKind kind;
        private SpriteRenderer halo;
        private readonly SpriteRenderer[] orbiters = new SpriteRenderer[2];
        private static readonly List<EnemyAgent> splashScratch = new List<EnemyAgent>(32);

        public void Launch(EnemyAgent enemy, TowerDefinition settings, int sourceId)
        {
            target = enemy;
            damage = settings.damage;
            penetration = settings.armorPenetration;
            slow = settings.slowFraction;
            duration = settings.slowDuration;
            speed = Mathf.Max(0.1f, settings.projectileSpeed);
            splashRadius = Mathf.Max(0f, settings.splashRadius);
            kind = settings.kind;
            fancyMagic = kind == TowerKind.Mage;
            source = sourceId;
            age = 0;
            launched = true;
            if (body) body.color = fancyMagic ? settings.magicColor : Color.white;
            if (fancyMagic && body && haloSprite)
            {
                halo = ChildSprite("OrbitingRune", haloSprite, new Color(settings.magicColor.r, settings.magicColor.g, settings.magicColor.b, 0.55f));
                halo.transform.localScale = Vector3.one * 0.8f;
                for (int i = 0; i < orbiters.Length; i++)
                {
                    orbiters[i] = ChildSprite("OrbitLight", body.sprite, i == 0 ? Color.white : new Color(0.7f, 0.8f, 1));
                    orbiters[i].transform.localScale = Vector3.one * 0.19f;
                }
                ArcaneBurst.Spawn(transform.position, settings.magicColor, haloSprite, body.sprite, 0.42f);
            }
            if (trailMaterial && (fancyMagic || kind == TowerKind.Arrow))
            {
                var trail = gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMaterial;
                trail.time = fancyMagic ? 0.24f : 0.12f;
                trail.startWidth = fancyMagic ? 0.22f : 0.08f;
                trail.endWidth = 0.01f;
                trail.minVertexDistance = 0.035f;
                trail.sortingOrder = 14;
                trail.numCapVertices = 2;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var gradient = new Gradient();
                Color mid = fancyMagic ? settings.magicColor : new Color(0.85f, 0.75f, 0.45f);
                Color end = fancyMagic ? new Color(0.35f, 0.2f, 0.9f) : new Color(0.45f, 0.35f, 0.2f);
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(mid, 0.25f), new GradientColorKey(end, 1) },
                    new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.55f, 0.4f), new GradientAlphaKey(0, 1) });
                trail.colorGradient = gradient;
            }
        }

        private SpriteRenderer ChildSprite(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 16;
            return sr;
        }

        private void Update()
        {
            if (!launched || !target || !target.IsAlive) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            if (halo)
            {
                halo.transform.localRotation = Quaternion.Euler(0, 0, age * 360);
                for (int i = 0; i < orbiters.Length; i++)
                {
                    float a = age * 12 + i * Mathf.PI;
                    orbiters[i].transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * 0.25f;
                }
            }
            if (age > 8) { Destroy(gameObject); return; }
            Vector3 aim = target.transform.position + Vector3.up * 0.25f;
            Vector3 previous = transform.position;
            transform.position = Vector3.MoveTowards(previous, aim, speed * Time.deltaTime);
            if (!fancyMagic)
            {
                Vector3 delta = aim - previous;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    if (kind == TowerKind.Arrow) transform.rotation = Quaternion.Euler(0, 0, angle);
                    else if (kind == TowerKind.Artillery) transform.rotation = Quaternion.Euler(0, 0, age * 420f);
                }
            }
            if ((transform.position - aim).sqrMagnitude > 0.04f) return;
            ApplyImpact(aim);
            Destroy(gameObject);
        }

        private void ApplyImpact(Vector3 aim)
        {
            FortressAudio.Play(kind == TowerKind.Arrow ? FortressSound.ArrowHit
                : kind == TowerKind.Artillery ? FortressSound.StoneHit : FortressSound.MagicHit, aim);
            if (splashRadius > 0.01f)
            {
                float radiusSquared = splashRadius * splashRadius;
                splashScratch.Clear();
                splashScratch.AddRange(EnemyAgent.Active);
                for (int i = 0; i < splashScratch.Count; i++)
                {
                    EnemyAgent enemy = splashScratch[i];
                    if (!enemy || !enemy.IsAlive) continue;
                    if (((Vector2)(enemy.transform.position - aim)).sqrMagnitude > radiusSquared) continue;
                    enemy.TakeDamage(damage, penetration);
                }
            }
            else
            {
                target.TakeDamage(damage, penetration);
                if (target && target.IsAlive) target.ApplySlow(slow, duration, source);
            }

            if (fancyMagic && body && haloSprite) ArcaneBurst.Spawn(aim, body.color, haloSprite, body.sprite);
            else if (body) ArcaneBurst.Spawn(aim, body.color, body.sprite, body.sprite, kind == TowerKind.Artillery ? 1.15f : 0.55f);
        }
    }
}
