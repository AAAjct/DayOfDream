# 鹅企中学 · 夜间探索基础场景

## Anon 旧宅

在 Unity 打开 `Assets/Scenes/AnonHouse.unity` 后点 Play。构建从主菜单开始，选择开始游戏即可进入旧宅。

- 地图共四层：地下室、一楼、二楼、阁楼，另有可进入的外院。东侧双折楼梯连接一楼、二楼和阁楼，原坡道通向地下室；二楼和阁楼房间之间也有连接门。
- 从卧室出发，去厨房找小钥匙，再打开书房铁柜取得正门钥匙。
- 从东侧坡道下到地窖。在工作台取保险丝，装到墙上的电闸，再返回正门。
- 对准交互目标时，中心实心点变为较大的平滑空心圈，不显示 E 或目标文字。轻按 E 进出衣柜、床底或拾取瓶子。躲藏时可转动视角并使用已拾取的 DV。瓶子可以把 Anon 引到别处，走路和蹲行完全静音；跑步产生轻微声音（约 4 米范围）。没有听到卧室内声响时，Anon 不会进入初始卧室，也不会仅因看到房内玩家而追入。
- 屏幕下方四格依次为小钥匙、正门钥匙、保险丝和瓶子；按 1 / 2 / 3 / 4 切换左手物品，选中格会高亮。DV 独立使用右手。短按 Q 就近丢弃，长按 Q 蓄力，松手按抛物线投出；约 1.25 秒蓄满，蓄力时选中物品栏高亮增强。钥匙和保险丝落地后可按 E 拾回，瓶子落地破碎并发出引开 Anon 的响声。暂停、躲藏及处决会取消未完成的蓄力。物品栏使用独立于场景光照的 2D 图标。
- Anon 持棍巡逻。被抓住后，玩家先倒地，Anon 双手举棍从上往下砸，玩家失去意识；处决期间播放唐笑，结束时停止。随后在床上模糊睁眼、坐起、下床，开始下一天。第一天使用同一套起床动画。钥匙、保险丝和瓶子会掉在死亡地点，DV 作为独立装备保留，已打开的铁柜和通电状态会保留。第五夜被抓后失败，结局画面按 E 重开。
- 开局下床后，低头看床头柜，按 E 或手柄 Y 拾取 DV。右键举起或收下 DV，F 切换夜视，滚轮变焦。按住 Alt 回头看，松开回正。

所有房门都会根据交互者所在的一侧向远离交互者的方向打开。卧室床已加长、拆掉床头竖板，顶灯照亮床、墙壁和桌面。手持和桌面 DV 共享精细模型和 URP/Lit 材质，均受真实场景灯光影响；手持 DV 没有独立补光灯。

旧宅建筑源文件为 `SourceArt/AnonHouse.blend`。Unity 场景中的 `House Architecture • Blender` 使用导出的 FBX。编辑旧宅碰撞结构后，可用 `Tools > Day Of Dream > Export Anon House Layout` 更新 Blender 生成数据，并通过 `AnonHouseVisuals.RefreshShell()` 只更新建筑视觉模型。Blender MCP 的本地后台工作进程由 `Tools/run_blender_mcp_worker.py` 提供，监听 `127.0.0.1:9877`。

## 鹅企中学

在 Unity 打开 `Assets/Scenes/HaojieSchool_Night.unity`，点击 Play，再点击 Game 画面锁定鼠标。

| 操作 | 键位 |
| --- | --- |
| 移动 / 视角 | WASD / 鼠标 |
| 奔跑 | 左 Shift |
| 蹲下 / 起身 | C，按一下切换 |
| 回头看 | 按住 Alt；松开回正 |
| 举起 / 收起 DV | 鼠标右键；手柄 RB / R1 |
| DV 夜视 | F；手柄右摇杆按下 |
| DV 变焦 | 鼠标滚轮；手柄方向键左 / 右 |
| 开关教学楼正门 / 阅读公告 | 对准目标后 E，距离 2.5 米以内 |
| 暂停 / 继续 | Esc；点击画面也可继续 |

主角使用项目现有 Tomorin FBX，第一人称看不见自己的身体，仍保留角色阴影。基础手柄映射沿用项目输入资产，Y 交互、B 蹲下、左摇杆按下奔跑。

DV 的机身 LCD 与全屏取景显示同一镜头的实时画面。普通模式保留彩色，带有轻微暗角和传感器噪点；夜视使用沿镜头方向的补光，保留中心的文字和道具细节。DV 和夜视可持续使用。

当前内容是可探索的第一人称恐怖场景基础，包含夜间照明、双扇正门、公告交互与校园/教学楼碰撞。敌人、追逐、正式剧情、存档及完整通关流程尚未实现。

三层主楼、东/西侧楼的教室入口与走廊现已接通。两侧外楼梯采用连续斜面碰撞，依次可到二楼、三楼及屋顶；每层经外侧回廊绕回下一段楼梯。入口、平台和回廊保留了足够的角色净空，避免卡在上层楼梯底面或护栏侧面。楼层指示牌能帮助找路。旧资料室、二楼办公室、三楼广播室和屋顶各有一份可按 E 阅读的记录。

`Tools > Day Of Dream > Connect All School Floors` 可更新这些连接；上下楼与入口验证脚本为 `Tools/ValidateSchoolRoutes.cs`。它检查 96 处教室入口、各楼层走廊，并实际驱动 CharacterController 从两侧地面走至屋顶。结果位于 `Docs/Validation/upstairs_routes.json`。已有独立运行包需要重新 Build 才会包含这次场景更新。

美术源文件仍保留在 `D:/wenjian/BlenderAISceneMake/output/Haojie_Refined/Haojie_Horror_School_Refined.blend`。游戏导出位于 `Assets/Art/Environment/HaojieSchool`，手电源位于 `SourceArt/Flashlight.blend`。原学校源文件没有修改。Blender 材质转换为 URP Lit 与程序化生成的平铺贴图，效果不等同于 Cycles 离线渲染。

导入过程按区域拆分大型网格，并显式三角化，补后走廊通路、教学楼端口和中央入口；Unity 场景补充楼梯连接平台及门槛台阶。平整校园地面使用连续碰撞面消除砖缝接触引起的抖动，砖缝与地面细节保留为视觉表现。走路时镜头保持稳定，奔跑时镜头和手中的 DV 随步态晃动。

资源重导出：

```powershell
& 'D:/blender-5.2-LTS/blender.exe' -b 'D:/wenjian/BlenderAISceneMake/output/Haojie_Refined/Haojie_Horror_School_Refined.blend' --python Tools/export_horror_assets.py
& 'D:/blender-5.2-LTS/blender.exe' --factory-startup -b --python Tools/export_flashlight.py
```

现有场景不要重建覆盖。`Tools > Day Of Dream > Apply Night Exploration Fixes` 可应用当前交互、碰撞与夜间配置。资源重导入后重新进入 Play，避免 Play 期间静态批处理仍显示旧网格。

Pipeline 启动问题已通过 `ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json` 中的项目端口 17900 解决。Windows 的系统保留端口覆盖了默认 7800–7849；没有修改系统网络配置或 PackageCache。

验证脚本：Play 模式运行 `unity command script eval --file Tools/ValidateExploration.cs --format json`。它使用临时虚拟键盘验证移动、奔跑、蹲下、镜头稳定、回头、空格无跳跃、门交互与通行、墙面碰撞及暂停，结果写入 `Docs/Validation/exploration_checks.json`。


## 角色音效

走路、跑步和蹲行按实际移动距离向玩家播放脚步声，蹲行音量较低；敌人的听觉独立计算，走路和蹲行不会引起敌人注意，跑步只在近距离引起注意；拾取、投出和落地有各自音效。处决开始时播放提供的《爱音唐笑.mp3》，原文件复制到 `Assets/Resources/GameplayAudio/ExecutionLaugh.mp3`。新音效自动由运行脚本加载，无需 Inspector 手动绑定。

物品与音效回归脚本：Play 模式通过 `unity command script eval --file Tools/ValidateInventoryAudio.cs --format json` 执行，结果保存到 `output/inventory_audio/checks.json`。该脚本会临时移动玩家并禁用敌人，验证结束后退出 Play 模式恢复场景。
