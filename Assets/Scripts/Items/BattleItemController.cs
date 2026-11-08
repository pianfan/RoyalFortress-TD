using System;
using System.Collections.Generic;
using KingdomBorder.TwinTowers;
using UnityEngine;

public sealed class BattleItemController : MonoBehaviour
{
    public const int ChargesPerRun = 3;
    public const float SharedCooldown = 3;
    public WaveGameController game;
    public MainBaseCaster caster;
    public BattleItemDefinition fireball, haste;
    public TowerBuildController building;
    public bool UnlockedForRun { get; private set; }
    public int UsedCount { get; private set; }
    public int RemainingCount => Mathf.Max(0, ChargesPerRun - UsedCount);
    public float CooldownRemaining => Mathf.Max(0, cooldownUntil - Time.time);
    public float HasteRemaining => Mathf.Max(0, hasteUntil - Time.time);
    public bool HasteActive => HasteRemaining > 0;
    public event Action Changed;
    private float cooldownUntil, hasteUntil;
    private bool committing;
    private BattleItemTargeting targeting;
    private BattleItemVfx effects;

    private void Awake()
    {
        if (!game) game = GetComponent<WaveGameController>();
        UnlockedForRun = CampaignProgress.ItemsUnlocked;
        WorldCombatModifiers.RestoreItems();
        targeting = GetComponent<BattleItemTargeting>();
        effects = GetComponent<BattleItemVfx>();
        game.StateChanged += GameChanged;
        game.DebugWaveJumped += Jumped;
    }
    private void Start() { if (!building) building = FindFirstObjectByType<TowerBuildController>(); }
    private void Update()
    {
        if (hasteUntil > 0 && Time.time >= hasteUntil) ClearHaste();
    }
    public BattleItemDefinition Definition(BattleItemKind kind) => kind == BattleItemKind.Fireball ? fireball : kind == BattleItemKind.Haste ? haste : null;
    public string UnavailableReason(BattleItemKind kind)
    {
        var item = Definition(kind);
        if (!item) return "道具未配置";
        if (committing) return "正在施放";
        if (!UnlockedForRun) return CampaignProgress.ItemsUnlocked ? "下次进入关卡可用" : "通关圣林加护后解锁";
        if (!game || game.IsGameOver || game.Lives <= 0) return "基地已失守";
        if (game.AllWavesFinished) return "本关已结束";
        if (game.IsPaused || game.StoryOverlayActive) return "暂停期间不可用";
        if (!game.IsWaveActive) return "开始波次后可用";
        if (RemainingCount == 0) return "本关次数已用完";
        if (kind == BattleItemKind.Haste && HasteActive) return "强化中 · " + Mathf.CeilToInt(HasteRemaining) + " 秒";
        if (CooldownRemaining > 0) return "冷却 · " + Mathf.CeilToInt(CooldownRemaining) + " 秒";
        if (game.Gold < item.price) return "金币不足 · 需要 " + item.price;
        if (kind == BattleItemKind.Haste && AttackTowerCount() == 0) return "请先建造攻击塔";
        return null;
    }
    private int AttackTowerCount()
    {
        int count = 0;
        if (building && building.builtTowersRoot)
            foreach (var tower in building.builtTowersRoot.GetComponentsInChildren<TowerBehaviour>())
                if (tower.attackEnabled && tower.definition && tower.definition.kind != TowerKind.Frost) count++;
        return count;
    }
    public bool IsMapPoint(Vector3 point)
    {
        var camera = building && building.worldCamera ? building.worldCamera.GetComponent<MapCameraController>() : null;
        if (!camera || !camera.mapRenderer || !float.IsFinite(point.x) || !float.IsFinite(point.y)) return false;
        var bounds = camera.mapRenderer.bounds;
        point.z = bounds.center.z;
        return bounds.Contains(point);
    }
    private List<WaypointEnemy> Targets(Vector3 point, float radius)
    {
        var targets = new List<WaypointEnemy>();
        if (game && game.enemiesRoot)
            foreach (var enemy in game.enemiesRoot.GetComponentsInChildren<WaypointEnemy>())
                if (!enemy.Resolved && enemy.Combat && enemy.Combat.IsAlive
                    && ((Vector2)(enemy.transform.position - point)).sqrMagnitude <= radius * radius) targets.Add(enemy);
        return targets;
    }
    public bool HasFireballTarget(Vector3 point) => fireball && IsMapPoint(point) && Targets(point, fireball.radius).Count > 0;

    // The only spending entry point: reserve before synchronous gold/kill notifications can reenter.
    public bool TryUse(BattleItemKind kind, Vector3 point, out string reason)
    {
        reason = UnavailableReason(kind);
        if (reason != null) return false;
        var item = Definition(kind);
        List<WaypointEnemy> targets = null;
        if (kind == BattleItemKind.Fireball)
        {
            if (!IsMapPoint(point)) { reason = "请选择地图内的位置"; return false; }
            targets = Targets(point, item.radius);
            if (targets.Count == 0) { reason = "范围内没有敌人，未消耗道具"; return false; }
        }
        committing = true;
        float previousCooldown = cooldownUntil;
        UsedCount++; cooldownUntil = Time.time + SharedCooldown;
        bool spent = false, applied = false;
        try
        {
            spent = game.TrySpendGold(item.price);
            if (!spent || !game.CanAct || !game.IsWaveActive) { reason = "当前无法使用，未消耗道具"; return false; }
            applied = true;
            if (kind == BattleItemKind.Fireball)
            {
                foreach (var enemy in targets)
                    if (enemy && !enemy.Resolved && enemy.Combat && enemy.Combat.IsAlive)
                        enemy.Combat.TakeDamage(item.damage, item.armorPenetration);
                FortressAudio.Play(FortressSound.StoneHit, point);
            }
            else
            {
                hasteUntil = Time.time + item.duration;
                WorldCombatModifiers.SetItems(item.attackSpeed);
                FortressAudio.Play(FortressSound.Pulse, caster ? caster.Position : transform.position);
            }
            if (targeting) targeting.Cancel();
            // Visual failures never refund an already applied gameplay effect.
            if (effects)
            {
                try { effects.Show(item, caster ? caster.Position : transform.position, point); }
                catch (Exception error) { Debug.LogWarning("Item effect could not be displayed: " + error.Message, this); }
            }
            return true;
        }
        finally
        {
            if (!applied)
            {
                UsedCount--; cooldownUntil = previousCooldown;
                if (spent && game) game.AddGold(item.price);
            }
            committing = false;
            Changed?.Invoke();
        }
    }
    private void GameChanged()
    {
        if (!game.CanAct || !game.IsWaveActive) { if (targeting) targeting.Cancel(); }
        if (game.IsGameOver || game.AllWavesFinished) ClearHaste();
    }
    private void ClearHaste()
    {
        hasteUntil = 0; WorldCombatModifiers.RestoreItems();
    }
    private void Jumped()
    {
        if (targeting) targeting.Cancel();
        ClearHaste();
        if (effects) effects.Clear();
        Changed?.Invoke(); // Usage and cooldown deliberately survive a jump.
    }
    private void OnDisable() { if (targeting) targeting.Cancel(); ClearHaste(); if (effects) effects.Clear(); }
    private void OnDestroy()
    {
        if (game) { game.StateChanged -= GameChanged; game.DebugWaveJumped -= Jumped; }
    }
}
