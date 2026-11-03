using System.Collections.Generic;
using UnityEngine;

namespace KingdomBorder.TwinTowers
{
    public sealed class TowerBehaviour : MonoBehaviour
    {
        [Tooltip("塔参数资源；选择资源即可在 Inspector 中调整。")] public TowerDefinition definition;
        public MagicProjectile projectilePrefab;
        public Transform muzzle;
        public SpriteRenderer auraVisual;
        public bool attackEnabled = true;
        public int ShotsFired { get; private set; }
        public int AuraEntries { get; private set; }
        public int AffectedCount { get { return affected.Count; } }
        public EnemyAgent Target { get; private set; }
        private float attackCooldown;
        private readonly HashSet<EnemyAgent> affected = new HashSet<EnemyAgent>();
        private readonly HashSet<EnemyAgent> seen = new HashSet<EnemyAgent>();
        private readonly List<EnemyAgent> sweep = new List<EnemyAgent>();

        public void ResetCombat() { ClearAura(); attackCooldown = 0; ShotsFired = AuraEntries = 0; Target = null; }
        private void OnDisable() { ClearAura(); if (auraVisual) auraVisual.gameObject.SetActive(false); }
        private void Update()
        {
            if (!attackEnabled || !definition)
            { ClearAura(); Target = null; if (auraVisual) auraVisual.gameObject.SetActive(false); return; }
            if (definition.kind == TowerKind.Frost) { UpdateAura(); return; }
            ClearAura();
            if (auraVisual) auraVisual.gameObject.SetActive(false);
            if (!projectilePrefab || Time.timeScale <= 0) { Target = null; return; }
            attackCooldown = Mathf.Max(0, attackCooldown - Time.deltaTime * WorldCombatModifiers.TowerAttackSpeed);
            Target = FindTarget();
            if (!Target || attackCooldown > 0) return;
            Vector3 spawn = muzzle ? muzzle.position : transform.position;
            MagicProjectile shot = Instantiate(projectilePrefab, spawn, Quaternion.identity);
            shot.Launch(Target, definition, GetInstanceID());
            FortressAudio.Play(definition.kind == TowerKind.Arrow ? FortressSound.Arrow
                : definition.kind == TowerKind.Artillery ? FortressSound.Stone : FortressSound.Mage, transform.position);
            ShotsFired++;
            attackCooldown = Mathf.Max(0.05f, definition.attackInterval);
        }
        private void UpdateAura()
        {
            Target = null;
            seen.Clear();
            float radius = Mathf.Max(0.1f, definition.range);
            float radiusSquared = (radius + 0.0001f) * (radius + 0.0001f);
            foreach (EnemyAgent enemy in EnemyAgent.Active)
            {
                if (!enemy || !enemy.IsAlive || ((Vector2)(enemy.transform.position-transform.position)).sqrMagnitude > radiusSquared) continue;
                seen.Add(enemy);
                enemy.SetAuraSlow(definition.slowFraction, GetInstanceID());
                if (affected.Add(enemy)) AuraEntries++;
            }
            sweep.Clear(); sweep.AddRange(affected);
            foreach (EnemyAgent enemy in sweep)
                if (!enemy || !seen.Contains(enemy))
                { if (enemy) enemy.RemoveSlow(GetInstanceID()); affected.Remove(enemy); }
            if (auraVisual && auraVisual.sprite)
            {
                auraVisual.gameObject.SetActive(true);
                auraVisual.transform.localScale = Vector3.one * (radius*2/auraVisual.sprite.bounds.size.x);
                Color color=definition.magicColor; color.a=0.82f+0.08f*Mathf.Sin(Time.time*2);
                auraVisual.color=color;
            }
        }
        private void ClearAura()
        {
            foreach (EnemyAgent enemy in affected) if (enemy) enemy.RemoveSlow(GetInstanceID());
            affected.Clear();
        }
        private EnemyAgent FindTarget()
        {
            EnemyAgent best = null;
            float progress = -1, closest = float.MaxValue;
            float radius = Mathf.Max(0.1f, definition.range);
            foreach (EnemyAgent enemy in EnemyAgent.Active)
            {
                if (!enemy || !enemy.IsAlive) continue;
                float distance = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude;
                if (distance > radius * radius) continue;
                if (enemy.Progress > progress || (Mathf.Approximately(enemy.Progress, progress) && distance < closest))
                { best = enemy; progress = enemy.Progress; closest = distance; }
            }
            return best;
        }
        private void OnDrawGizmosSelected()
        {
            if (!definition) return;
            Gizmos.color = definition.magicColor;
            Gizmos.DrawWireSphere(transform.position, definition.range);
        }
    }
}
