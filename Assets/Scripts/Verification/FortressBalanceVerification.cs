#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using KingdomBorder.TwinTowers;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit Play Mode verification, never attached to a shipped scene or included in a player build.
public sealed class FortressBalanceVerification : MonoBehaviour
{
    [Serializable] public sealed class WaveResult
    {
        public int wave, lives, gold, towers, kills, leaks;
        public float simulatedSeconds;
    }
    [Serializable] public sealed class RunResult
    {
        public string scene, mode, outcome;
        public bool victory, bossKilled, capacityPass;
        public float bossLowestHealth;
        public int finalLives, finalGold, towerCount;
        public List<WaveResult> waves = new List<WaveResult>();
    }
    [Serializable] public sealed class Report { public string label; public List<RunResult> runs = new List<RunResult>(); }
    public string reportPath;
    public string label = "balance";
    public float simulationSpeed = 6;
    public bool fullBuildOnly;
    private readonly Report report = new Report();
    private bool hadUnlock, oldBackground;
    private int oldUnlock;
    private float oldAudio;
    private bool restored;
    private readonly TowerKind[] forestKinds = { TowerKind.Arrow, TowerKind.Arrow, TowerKind.Arrow, TowerKind.Mage, TowerKind.Artillery,
        TowerKind.Mage, TowerKind.Frost, TowerKind.Mage, TowerKind.Mage, TowerKind.Frost };
    private readonly TowerKind[] emeraldKinds = { TowerKind.Arrow, TowerKind.Mage, TowerKind.Artillery, TowerKind.Frost, TowerKind.Arrow,
        TowerKind.Mage, TowerKind.Artillery, TowerKind.Frost, TowerKind.Arrow, TowerKind.Mage, TowerKind.Artillery, TowerKind.Arrow };

    [UnityEditor.MenuItem("Royal Fortress/Verification/Run two-map balance")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Enter Play Mode before running the balance check."); return; }
        if (FindFirstObjectByType<FortressBalanceVerification>()) return;
        var runner = new GameObject("Explicit Balance Verification").AddComponent<FortressBalanceVerification>();
        runner.reportPath = Path.GetFullPath("../ReviewArtifacts/balance-runtime.json");
    }
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        hadUnlock = PlayerPrefs.HasKey(CampaignProgress.ForestClearKey); oldUnlock = PlayerPrefs.GetInt(CampaignProgress.ForestClearKey, 0);
        oldBackground = Application.runInBackground; oldAudio = AudioListener.volume;
        Application.runInBackground = true; AudioListener.volume = 0;
        if (string.IsNullOrEmpty(reportPath)) reportPath = Path.GetFullPath("../ReviewArtifacts/balance-runtime.json");
        report.label = label;
        try
        {
            foreach (string sceneName in new[] { "ForestSanctuaryGameplay", "EmeraldSwitchback" })
                foreach (string mode in fullBuildOnly ? new[] { "full" } : new[] { "full", "economy" })
                    yield return VerifyRun(sceneName, mode);
        }
        finally { Restore(); Save(); }
        FortressMainMenu.ReturnToSelection();
        Destroy(gameObject);
    }
    private IEnumerator VerifyRun(string sceneName, string mode)
    {
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync(sceneName);
        var game = FindFirstObjectByType<WaveGameController>();
        var hud = FindFirstObjectByType<FortressHud>();
        var comics = FindFirstObjectByType<CampaignStoryComics>();
        float readyDeadline = Time.realtimeSinceStartup + 10;
        while ((!hud.IsReady || (game.StoryOverlayActive && !comics.IsOpen)) && Time.realtimeSinceStartup < readyDeadline) yield return null;
        if (comics.IsOpen) comics.Close();
        var result = new RunResult { scene = sceneName, mode = mode };
        report.runs.Add(result);
        var build = FindFirstObjectByType<TowerBuildController>();
        var kinds = sceneName == "ForestSanctuaryGameplay" ? forestKinds : emeraldKinds;
        var order = sceneName == "ForestSanctuaryGameplay" ? new[] { 7, 8, 0, 1, 2, 5, 4, 3, 9, 6 } : new[] { 0, 1, 4, 3, 2, 5, 6, 8, 9, 10, 11, 7 };
        if (mode == "full") game.AddGold(3000); // Only the explicit full-defence capacity check gets starting funds.
        Time.timeScale = Mathf.Clamp(simulationSpeed, 1, 8);
        var watchedBosses = new HashSet<WeatherBossAbility>();
        float deadline = Time.realtimeSinceStartup + 240;
        while (!game.AllWavesFinished && !game.IsGameOver && Time.realtimeSinceStartup < deadline)
        {
            Purchase(build, order, kinds);
            if (game.CanStartWave) game.TryStartNextWave();
            int wave = game.CurrentWave, kills = game.DefeatedEnemies, leaks = game.LeakedEnemies;
            float started = Time.time;
            while (game.IsWaveActive && !game.IsGameOver && Time.realtimeSinceStartup < deadline)
            {
                Purchase(build, order, kinds);
                foreach (var boss in FindObjectsByType<WeatherBossAbility>(FindObjectsSortMode.None))
                    if (boss.Enemy && boss.Enemy.Combat)
                    {
                        if (watchedBosses.Add(boss))
                        {
                            result.bossLowestHealth = boss.Enemy.Combat.Health;
                            boss.Enemy.Combat.Died += ignored => { result.bossKilled = true; result.bossLowestHealth = 0; };
                        }
                        if (boss.IsActive) result.bossLowestHealth = Mathf.Min(result.bossLowestHealth, boss.Enemy.Combat.Health);
                    }
                yield return null;
            }
            result.waves.Add(new WaveResult { wave = wave, lives = game.Lives, gold = game.Gold, towers = build.BuiltCount,
                kills = game.DefeatedEnemies - kills, leaks = game.LeakedEnemies - leaks, simulatedSeconds = Time.time - started });
            result.finalLives = game.Lives; result.finalGold = game.Gold; result.towerCount = build.BuiltCount;
            Save();
            yield return null;
        }
        result.victory = game.AllWavesFinished && game.Lives > 0;
        result.capacityPass = result.victory && (sceneName != "EmeraldSwitchback" || result.bossKilled);
        result.outcome = game.IsGameOver ? "defeat" : !game.AllWavesFinished ? "timeout" : "victory";
        Save();
    }
    private static void Purchase(TowerBuildController build, int[] order, TowerKind[] kinds)
    {
        foreach (int index in order)
        {
            if (index >= build.sites.Length || index >= kinds.Length || build.sites[index].IsOccupied) continue;
            if (!build.CanAfford(kinds[index])) break;
            if (!build.TryPurchase(kinds[index], build.sites[index])) break;
        }
    }
    private void Save()
    {
        if (string.IsNullOrEmpty(reportPath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
    }
    private void Restore()
    {
        if (restored) return; restored = true;
        Time.timeScale = 1; Application.runInBackground = oldBackground; AudioListener.volume = oldAudio;
        if (hadUnlock) PlayerPrefs.SetInt(CampaignProgress.ForestClearKey, oldUnlock); else PlayerPrefs.DeleteKey(CampaignProgress.ForestClearKey);
        PlayerPrefs.Save();
    }
    private void OnDestroy() { Restore(); }
}
#endif
