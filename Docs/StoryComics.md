# 关卡剧情漫画

四个可玩关卡已配置入场和胜利漫画。原图位于 `Assets/Art/StoryComics/`，以 Sprite 引用接入各关卡的 `CampaignStoryComics` 组件；圣林胜利使用去除残留敌人的 `03-sanctuary-clear-v2.png`。

场景与素材对应：

| 场景 | 入场 | 胜利 |
| --- | --- | --- |
| RoyalFortressGameplay | 01-border-entry | 01-border-clear |
| EmeraldSwitchback | 02-emerald-entry | 02-emerald-clear |
| ForestSanctuaryGameplay | 03-sanctuary-entry | 03-sanctuary-clear-v2 |
| GateOfTheDeadGameplay | 04-gate-entry | 04-gate-clear |

每次进入或重新开始关卡时，先显示入场漫画。阅读期间暂停游戏并锁定出兵、建造和底层 HUD 输入；点击“进入战场”或“跳过”恢复此前暂停状态，随后由玩家手动开始第一波。漫画没有自动计时关闭。

边境与回廊在最后一波完全处理且基地生命大于零时显示胜利漫画。圣林额外等待 `ForestSanctuaryGameplay.VictoryFinished`，亡者之门额外等待 `GateOfDeadEncounter.VictoryFinished`。因此加护和封印不会被漫画提前暂停。失败不显示胜利图，每次胜利只显示一次；调试跳波会重置胜利漫画标记。

漫画采用保留宽高比的 Image，外加米色纸张、程序绘制的轻微毛边和颗粒、叠页阴影、双层墨线。原漫画不烘焙 UI，对白使用可编辑文本。窗口根据屏幕尺寸缩放；已经导出检查 16:9 和 4:3。新增 UI 没有重排原有选关菜单或更改解锁规则。

验证日期：2026-11-03。Unity 6000.3.27f1 CLI 在四个正式场景中分别验证了入场锁定、“进入战场”“继续”“跳过”按钮回调、最终波及演出后的胜利触发、关闭后不重复显示、真实漏怪触发失败时不显示胜利。测试临时减少敌人数并调用击败回调以加速波次，不是难度或完整战斗测试。没有把测试配置保存到场景。

截图、报告和替换前场景备份：`D:/游戏/output/story-comics-integration-2026-11-03/`。可重复检查入口：`CampaignComicsVerification.Run`（仅 Editor 批处理测试，测试完成会退出编辑器）。
