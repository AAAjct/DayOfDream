# 梦之日 · Day of Dream

![《梦之日》封面，夜色中的鹅企中学](output/covers/day-of-dream-cover-v1.png)

《梦之日》是一款使用 Unity 开发的单人第一人称恐怖探索游戏。你在 Anon 的旧宅中醒来，需要在第五夜结束前找到逃出去的办法。翻找钥匙和保险丝，用 DV 夜视辨认暗处的线索，必要时躲进衣柜或床底，也可以扔出瓶子引开巡逻的 Anon。

## 快速开始

1. 在 Unity Hub 中使用 **Unity 6000.6.9f1** 打开项目根目录，等待包解析和资源导入完成。
2. 打开 [MainMenu.unity](Assets/Scenes/MainMenu.unity)，点击 **Play**，在 Game 窗口选择「开始游戏」。
3. 起床后可在床头柜上拾取 DV。已有本地检查点时，可以从主菜单选择「继续游戏」。开始新游戏会清除旧检查点。

想单独探索鹅企中学，可直接打开 [EQiSchool.unity](Assets/Scenes/EQiSchool.unity) 并点击 **Play**。

## 键鼠操作

| 操作 | 键位 |
| --- | --- |
| 移动 / 转动视角 | WASD / 鼠标 |
| 奔跑 | 左 Shift |
| 蹲下 / 起身 | C |
| 回头看 | 按住 Alt，松开回正 |
| 拾取、开门、阅读、进出藏身处 | 对准目标按 E |
| 切换左手物品栏（旧宅） | 1–4 |
| 丢弃或投掷所选物品（旧宅） | 短按 Q 丢弃，长按 Q 蓄力后松手投掷 |
| 举起 / 收起 DV | 鼠标右键 |
| 切换 DV 夜视 / 调整变焦 | F / 鼠标滚轮 |
| 暂停 / 继续 | Esc |

## 场景

| 场景 | 内容 |
| --- | --- |
| [MainMenu](Assets/Scenes/MainMenu.unity) | 游戏入口，可开始、继续或调整音量和鼠标灵敏度 |
| [AnonHouse](Assets/Scenes/AnonHouse.unity) | 旧宅逃脱关卡，包含地下室、一楼、二楼、阁楼和外院 |
| [EQiSchool](Assets/Scenes/EQiSchool.unity) | 鹅企中学夜间探索场景，可进入教学楼、前往屋顶并阅读散落的记录 |

## 开发与构建

项目使用 **URP 17.6.0** 和 **Input System 1.20.1**，依赖版本见 [Packages/manifest.json](Packages/manifest.json)。

| 目录 | 内容 |
| --- | --- |
| [Assets/Scripts/Horror](Assets/Scripts/Horror/) | 玩家控制、敌人、交互、DV、菜单和检查点 |
| [Assets/Editor/Horror](Assets/Editor/Horror/) | 场景生成、资源导入和场景调整工具 |
| [Assets/Art](Assets/Art/) · [Assets/Prefabs](Assets/Prefabs/) | 游戏美术资源和预制体 |
| [SourceArt](SourceArt/) · [Tools](Tools/) | Blender 源文件、资源导出和验证脚本 |

构建 Windows 试玩包时，在 Unity 中打开 **File → Build Profiles**，选择 Windows，并确认场景列表依次启用 `MainMenu`、`AnonHouse` 和 `EQiSchool`，然后点击 **Build**。

## 项目文档

- [详细试玩指南与资源导出说明](Docs/PLAY.md)（其中的 `HaojieSchool_Night` 为鹅企中学场景的旧文件名）
- [编辑器验证记录](Docs/Validation/README.md)
- [项目结构与开发背景](Docs/AI/UnityProjectContext.md)
