# 游戏音频

游戏包含三首背景音乐和 22 个程序合成的短音效、天气环境声。进入首页即可播放音乐；准备阶段、普通波次和灾厄巫师战斗会自动切换配乐，并使用淡入淡出。

## 玩家设置

首页以及 Esc 暂停菜单均提供「音乐」「音效」两条音量滑块。拉到 0 可分别静音，设置会保存到本机并在下次运行时恢复。默认音乐 45%、音效 70%。

暂停时，战斗短音效停止、天气循环声暂停，背景音乐降低至原音量的 35%；菜单音效仍可播放。继续游戏后天气声恢复。窗口失去焦点时静音，重新获得焦点后恢复。

## 声音覆盖

| 类型 | 触发 |
| --- | --- |
| 操作 | 按钮点击、拿起塔卡、资金不足、建造完成 |
| 塔攻击 | 箭矢发射与命中、魔法发射与命中、投石发射与落地 |
| 敌人及波次 | 怪物死亡、漏怪警示、开始波次、清波、胜利、失败、Boss 出场 |
| 天气及仪式 | 暴雪施放与风声、沙尘暴风声、圣林净化脉冲、亡者之门封印 |

冰霜塔是持续光环，不循环播放每帧攻击声。战斗声音最多同时占用 12 个声道，同类攻击/命中最短间隔 0.09 秒、死亡声最短间隔 0.16 秒。镜头外的事件降低音量，画面内的左右位置影响声像。仪式关卡的胜利提示等待实际仪式或封印完成。

## 背景音乐来源

以下三首均在下载时核对来源页标注为 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。保留作者署名以致谢；源文件未剪辑或重写，仅交由 Unity 导入压缩。音乐不依赖运行时联网。

| 用途 | 曲目与作者 | 来源 |
| --- | --- | --- |
| 首页、选关、准备阶段 | Medieval: The Old Tower Inn — RandomMind | [OpenGameArt](https://opengameart.org/content/medieval-the-old-tower-inn) |
| 普通战斗 | Heartfelt Battle — request（使用单独提供的 loop 版本） | [OpenGameArt](https://opengameart.org/content/heartfelt-battle-loopable-fantasy-stringspianohorn) |
| 灾厄巫师战斗 | Fairy Battles — MintoDog | [OpenGameArt](https://opengameart.org/content/fairy-battles) |

具体文件名、下载地址和 SHA-256 保存在 `Assets/Resources/FortressAudio/Music/Sources.json`，随资源附带 `License.txt`。短音效由本项目的 Python 脚本合成，未使用第三方录音采样或歌曲片段。

## 文件与维护

- 播放服务：`Assets/Scripts/Audio/FortressAudio.cs`，跨场景保留一个实例；自动补充首页的音频监听器，进入有相机监听器的场景时关闭补充监听器。
- 音量面板与按钮声音：同目录的 `FortressAudioSettings.cs`、`FortressButtonSound.cs`。
- 资源：`Assets/Resources/FortressAudio/Music/` 与 `Sfx/`；名称相同的音乐和音效由不同字典管理。
- 导入设置：`Assets/Editor/FortressAudioImporter.cs`。音乐采用流式 Vorbis，短音效采用单声道 PCM 解压加载。
- 重新生成短音效：在仓库根目录运行 `python Tools/generate_audio.py`，仅需 Python 标准库。
- 重新下载音乐：`python Tools/fetch_music.py --proxy http://127.0.0.1:7897`。代理参数可省略，下载前会核对来源页的 CC0 标注。

## 运行检查

2026-11-03，Unity 6000.3.27f1：编译通过；25 个音频资源成功导入；22 个 WAV 均为 24 kHz 单声道、非静音、无削波；三首音乐的 SHA-256 与记录相符。

Play Mode 中检查了首页/普通战斗/Boss 音乐切换、按钮和建塔声音、实际箭塔射击、独立静音、暂停与恢复、暴雪/沙尘暴环境声、Boss 死亡后的天气声音清理、漏怪提示、切图后的单一监听器、声道上限与音量设置记忆。查看了首页和暂停面板的实际截图。最终响度与听感可在游戏中试听调整；本次未重新构建独立程序。
