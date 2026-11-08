using UnityEngine;

namespace KingdomBorder.TwinTowers
{
    // Shared environment multipliers, separate from enemy slow sources and tower assets.
    public static class WorldCombatModifiers
    {
        public static float WeatherAttackSpeed { get; private set; } = 1;
        public static float ItemAttackSpeed { get; private set; } = 1;
        public static float TowerAttackSpeed => WeatherAttackSpeed * ItemAttackSpeed;
        public static float EnemyMovementSpeed { get; private set; } = 1;
        public static void Set(float towerSpeed, float enemySpeed)
        {
            WeatherAttackSpeed = Mathf.Clamp(towerSpeed, 0.05f, 3f);
            EnemyMovementSpeed = Mathf.Clamp(enemySpeed, 0.1f, 3f);
        }
        public static void SetItems(float towerSpeed) { ItemAttackSpeed = Mathf.Clamp(towerSpeed, 1, 3); }
        public static void RestoreItems() { ItemAttackSpeed = 1; }
        public static void RestoreWeather() { Set(1, 1); }
        public static void Restore() { RestoreWeather(); RestoreItems(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Restore(); }
    }
}
