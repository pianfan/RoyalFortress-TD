using UnityEngine;

public enum BattleItemKind { Fireball, Haste }

[CreateAssetMenu(menuName = "Royal Fortress/Battle Item")]
public sealed class BattleItemDefinition : ScriptableObject
{
    public string stableId;
    public BattleItemKind kind;
    public string displayName;
    [Min(0)] public int price;
    [Min(.1f)] public float radius = 3;
    [Min(0)] public float damage = 180;
    [Range(0, 1)] public float armorPenetration = 1;
    [Min(.1f)] public float duration = 12;
    [Min(1)] public float attackSpeed = 1.8f;
    public Color color = new Color(1, .5f, .15f);
}
