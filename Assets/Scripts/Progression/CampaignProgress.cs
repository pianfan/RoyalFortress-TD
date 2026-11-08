using UnityEngine;

public static class CampaignProgress
{
    public const string ForestClearKey = "RoyalFortress.Progress.v1.forest_sanctuary";
    public static bool ItemsUnlocked => PlayerPrefs.GetInt(ForestClearKey, 0) == 1;

    // The debug console is an intended presentation tool, so skipped waves are accepted.
    public static bool RecordForestVictory(WaveGameController game)
    {
        if (!game || game.gameObject.scene.name != "ForestSanctuaryGameplay"
            || game.TotalWaves != 8 || !game.AllWavesFinished || game.IsGameOver
            || game.Lives <= 0 || game.IsSpawning || game.IsWaveActive || game.AliveCount != 0 || ItemsUnlocked) return false;
        PlayerPrefs.SetInt(ForestClearKey, 1);
        PlayerPrefs.Save();
        return true;
    }
}
