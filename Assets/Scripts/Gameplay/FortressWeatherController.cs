using System.Collections.Generic;
using KingdomBorder.TwinTowers;
using UnityEngine;
using UnityEngine.UI;

public enum FortressWeatherKind { Clear, Snow, Sandstorm }

// One scene-owned weather source drives both combat and presentation.
public sealed class FortressWeatherController : MonoBehaviour
{
    public static FortressWeatherController Instance { get; private set; }
    public FortressWeatherKind Weather { get; private set; }
    public WeatherBossAbility CurrentBoss { get; private set; }
    private readonly List<WeatherBossAbility> bosses = new List<WeatherBossAbility>();
    private WaveGameController game;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() { Instance = null; WorldCombatModifiers.RestoreWeather(); }
    private void Awake()
    {
        Instance = this;
        game = GetComponent<WaveGameController>();
        WorldCombatModifiers.RestoreWeather();
        var node = new GameObject("Weather Screen FX", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        node.transform.SetParent(transform, false);
        var canvas = node.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80; // Beneath the HUD and above the world.
        var scaler = node.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        var overlay = new GameObject("Snow and Sand", typeof(RectTransform), typeof(CanvasRenderer), typeof(FortressWeatherGraphic));
        overlay.transform.SetParent(node.transform, false);
        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        overlay.GetComponent<FortressWeatherGraphic>().controller = this;
    }
    private void Update() { RefreshWeather(); }
    public void Register(WeatherBossAbility boss)
    {
        if (boss && !bosses.Contains(boss)) bosses.Add(boss);
        RefreshWeather();
    }
    public void Unregister(WeatherBossAbility boss) { bosses.Remove(boss); RefreshWeather(); }
    private void RefreshWeather()
    {
        for (int i = bosses.Count - 1; i >= 0; i--) if (!bosses[i] || !bosses[i].IsActive) bosses.RemoveAt(i);
        CurrentBoss = game && game.IsGameOver ? null : bosses.Count > 0 ? bosses[0] : null;
        Weather = CurrentBoss ? CurrentBoss.RequestedWeather : FortressWeatherKind.Clear;
        WorldCombatModifiers.Set(
            Weather == FortressWeatherKind.Snow ? CurrentBoss.snowAttackSpeed : 1,
            Weather == FortressWeatherKind.Sandstorm ? CurrentBoss.sandMovementSpeed : 1);
    }
    private void OnDisable()
    {
        bosses.Clear(); CurrentBoss = null; Weather = FortressWeatherKind.Clear;
        if (Instance == this) { Instance = null; WorldCombatModifiers.RestoreWeather(); }
    }
}
