using System.Collections;
using UnityEngine;
using KingdomBorder.TwinTowers;

namespace RoyalFortress.ForestSanctuary
{
    /// <summary>Playable-level ritual victory/failure handoff.</summary>
    public sealed class ForestSanctuaryGameplay : MonoBehaviour
    {
        public WaveGameController game;
        public ForestSanctuaryPresentation presentation;
        public TowerBuildController building;
        public FortressHud hud;
        public bool VictoryStarted { get; private set; }
        public bool VictoryFinished { get; private set; }
        public bool Failed { get; private set; }
        private bool stoppedBuilding;
        private IEnumerator Start()
        {
            while (!hud.IsReady) yield return null;
            presentation.showControls = false;
            presentation.simulationControlsEnabled = false;
            game.StateChanged += Refresh;
            game.DebugWaveJumped += ResetForDebug;
            presentation.StateChanged += Refresh;
            presentation.onBlessingCompleted.AddListener(FinishVictory);
            Refresh();
        }
        private void ResetForDebug()
        {
            VictoryStarted = VictoryFinished = Failed = stoppedBuilding = false;
            hud.SetEncounterMessage(null, null, false);
            Refresh();
        }
        private void StopBuildingAndTowers()
        {
            if (stoppedBuilding) return;
            stoppedBuilding = true; building.Cancel(); building.enabled = false;
            foreach (var site in building.sites)
            {
                if (site.clickArea != null) site.clickArea.enabled = false;
                site.Hover(false); site.Select(false);
            }
            foreach (var tower in building.builtTowersRoot.GetComponentsInChildren<TowerBehaviour>()) tower.attackEnabled = false;
        }
        private void Refresh()
        {
            if (game == null || presentation == null) return;
            Failed = game.IsGameOver || presentation.Stage == ForestSanctuaryPresentation.RitualStage.Interrupted;
            VictoryStarted = presentation.Stage == ForestSanctuaryPresentation.RitualStage.Completing || presentation.Stage == ForestSanctuaryPresentation.RitualStage.Blessed;
            if (VictoryStarted && !Failed) CampaignProgress.RecordForestVictory(game);
            if (Failed || VictoryStarted) StopBuildingAndTowers();
            if (Failed) hud.SetEncounterMessage("祭坛失守 · 仪式中断", "重新开始", true);
            else if (VictoryStarted && !VictoryFinished) hud.SetEncounterMessage("防守成功 · 正在完成加护", "仪式完成中…", false);
            else if (VictoryFinished) hud.SetEncounterMessage("加护完成 · 道具已解锁", "重新开始", true);
        }
        private void FinishVictory() { if (Failed || !game.AllWavesFinished || game.Lives<=0) return; VictoryFinished=true; Refresh(); }
        private void OnDestroy()
        {
            if (game!=null) game.StateChanged-=Refresh;
            if (game!=null) game.DebugWaveJumped-=ResetForDebug;
            if (presentation!=null) { presentation.StateChanged-=Refresh; presentation.onBlessingCompleted.RemoveListener(FinishVictory); }
        }
    }
}
