# 美术表现二次代码审查（2026-10-16）

状态：**Implemented / Producer Acceptance Pending / Not Committed**。仅修改表现、资源接线与必要检查，未修改 Core 规则、Content 位置或身份，未提交 Git。

本轮按制作人明确授权补充并执行可用自动检查；该授权覆盖 AGENTS.md 的普通任务不运行测试默认要求。当前项目已在 Unity Editor 中打开，不接管现有 Editor，不启动同项目第二个 Editor；无法执行的 PlayMode 与视觉验收单独列明。

实施范围：角色身份与动作分离、实际移动与暂停时钟、生命染色恢复、远程出手；正式 Build 的小型 Resources Prefab 引用表；真实势力旗帜刷新；统一脚点排序与稳定逻辑几何；床用途、作物刷新检查；98 个荒村对象及 25 个名录角色逐项覆盖盘点。原图清理仅处理来源、引用均可证明安全的字节重复项。

## 修复的实质问题

- `EntityView` 使用独立的 `HostCharacterArtState` 保存动作和动画时钟，Profile 始终由原身份映射。优先级为不可行动/睡眠 → Idle；真实出手 → Attack；实际小幅位移 → Walk；正在执行的工作 → Work；其余 Idle。没有位移的移动指令不产生 Walk；大幅位置交接不视为步行。工作必须为真正 Running 的工作，不用动作帧可用性反推业务状态。
- 手动暂停与模态暂停停止动作计时、出手时长与受击闪光倒计时；统一近战/远程 VFX 也使用可暂停的表现时钟。实际出手方向不会被同帧移动方向覆盖。睡眠判定读取真实 sleep phase / Running BedRestAction，床 View 未加载时也不会继续工作、攻击或步行。
- 生命状态染色单独保存，不再覆盖阵营/槽位原色；Spawner 不再把弥留色写入原色。恢复健康时恢复本人外观或原色，同时清除本表现层写入的旧生命状态文字。
- 统一 `PlayRangedBetween` 在出手时触发攻击者，修复只有弹体的问题。攻击使用唯一的无交互视觉子层；即使本人缺攻击帧，也用本人静态图做微小出手位移，停止隐藏重复站姿。人物 Root、Collider、使用点与结算不动；睡眠、死亡、弥留中停止该子层。
- 缺失 Guard 工作/攻击帧时回退同一 Guard 的 Idle，避免保留上一段行走帧；主角/主管等缺动作时始终用本人静态图。重新配置或完全缺少资源时恢复最初旧 Sprite，不沿用另一身份的美术。
- `MapLayoutPrefabResolver` 的 Player 分支通过 `Resources/MapLayoutPrefabCatalog.asset` 获取 17 个正式 Prefab 引用，资源依赖随 Resources 纳入打包；Editor 原 AssetDatabase/PrefabUtility 路径保持。没有引入 Addressables 或新资产系统。以后扩充 MapKindCatalog 时必须同步该引用表；覆盖检查会检测漏项。
- 动态旗帜直接创建真实旗帜视觉，不再实例化 Warehouse。荒村杂役宗门使用现有红旗图，其余势力使用程序旗帜及 `StrategicFactionCatalog.MapTint`，无势力为中性色；每次同步读取真实旗帜 FactionId，接管后立即换外观。当前红旗 PNG 实际已经存在，本轮核对其 Resources 引用，未把上一轮已补图误报为本轮生成。
- 人物、树、墙、建筑和床使用共同脚点深度；睡眠子层在对应床面上方。树/墙会按前后位置遮挡人物，不再固定所有人物在环境之上。地表原有层级保留，战斗效果使用独立前景层。
- 地图工厂保存按传入逻辑宽高建立的 `HostMapVisualGeometry`，交互框和建造重叠优先使用它；动态机会物不再用新图 Sprite bounds 反推占地。视觉子层改变图片和比例不改变逻辑框、Root 或 Collider。没有改动 Content、人物尺寸、移动或玩法规则。

## 覆盖统计及保留问题

逐对象结果：[98 处荒村对象](277-huangcun-art-coverage.tsv)；逐角色结果：[25 人映射](277-roster-art-coverage.tsv)。统计以作者源、正式资源及代码路径为依据，**不是视觉验收通过数量**。

| 荒村类别 | 数量 | 实际情况 |
| --- | ---: | --- |
| 新美术已接入 | 60 | 床 27（按父房间用途分别记录实际图片）、中树 16、大树 5、矿 9、核心 1、储藏 1、洞口 1 |
| 旧表现保留 | 38 | 房间地板/墙体 22、农田 6、药田 4、蒲团 4、矿区覆盖 1、灵泉覆盖 1 |
| 当前运行资源缺失 | 0 | 98 对象相关现有资源、49 张正式 Resources PNG、17 个 Prefab 路径/GUID/root fileID 静态检查通过 |
| 待美术完善/视觉验收 | 见逐项表 | 38 处旧表现不冒充新美术；农田/药田新植物图层仍待合格。洞口按发现条件显示，不属于资源缺失 |

名录 **12 人已映射、13 人未映射**。13 人为 herb_b/c、woodcutter、wood_b/c、jiang_lao、青石验收无势力 a/b/hostile、朔风平民 a/b、商人和拍卖侍者；继续保留旧显示，不套用他人服装。已有主角三人 Walk/Idle 与 9 个身份卧姿保持；独有角色缺 Work/Attack 的问题是素材缺口，仍为本人静态回退加表现出手，不能声称完整动作集。

床用途切换已有真实 `HostBedVisualStyle.RefreshRoom` 接线；农田/药田 Growing、Mature、Empty/Ruined 已由真实状态刷新旧颜色表现。本轮增加对应检查，未接入未合格的新植物图层，未改变生长或采收结算。

ArtSource 的 479 条 JSON 源路径均存在。[同字节图片清单](277-artsource-identical-images.tsv) 共 33 组，包含来源/加工引用与有意 Idle/Walk 复用；继续保留，**本轮删除原图 0、释放空间 0**。没有为了去重破坏现有来源链，未把同字节即视为可删。此前记录的可重建 pyc 仍保留。

## 实际检查

- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/offline-compile.ps1`：8 个程序集均 `ALL_OK`；两条既有测试 warning（CS0105、CS1718），无编译错误。改动后的 Unity 程序集另外定向复编。
- 去掉所有 `UNITY_EDITOR*` define 的 Unity 程序集独立编译通过，覆盖 Player 分支；这不等于已完成 Unity Player 打包或运行。
- `tools/run-headless-tests.ps1 -Filter ArtPresentationStateTests`：**9/9 通过**，覆盖阻挡停步、工作/移动/出手优先级、暂停/模态暂停、睡眠/失能取消动作、未映射身份及主角身份保持。
- 新增 8 项 Unity EditMode 资源/表现检查与 1 项 PlayMode 暂停检查，已编译，**未执行**。覆盖 Resources Prefab 获取、缺失回退、所有 Profile 自身回退/卧姿、原色恢复、床用途切换、交互几何和 Collider 保持、排序、农田/药田成长与采收刷新。它们依赖 Unity 原生 API，不能拿独立 Mono 的结果冒充 Unity 执行结果。
- `python tools/audit-art-presentation.py`：49 张 PNG 格式、6 张图集尺寸、17 个 Prefab 引用、98 个对象覆盖、25 人映射与 479 条来源引用均通过；输出三份 TSV。
- `git diff --check` 无错误；Core、Content、场景、Packages、ProjectSettings 与原图均无修改。
- 当前同项目 Unity Editor 已打开，未接管或再开第二实例，**未执行 Unity EditMode Runner、PlayMode、实际 Build 打包、视觉/Profiler 验收**。上述九项 Unity 原生检查待在该 Editor 运行。

## 最短 LevelTester 人工验收

1. 仅打开 `Assets/Scenes/LevelTester.unity` 新开局：主角三人四方向移动/停步/撞阻挡，村民工作；近战及远程出手中按暂停，再打开模态面板，确认身体动作/弹体冻结且恢复后继续，服装不切换。
2. 正常住宅睡眠与就地休息后醒来；观察弥留/死亡不继续步行或攻击，正常救治后恢复外观/颜色。把床所在房间切换为住宅/医疗/牢房，核对三用途外观、选取及床身份。
3. 绕树冠、墙与床前后走；从既有交互种植/采收农田和药田，核对旧图颜色刷新与占地/可点击范围稳定。查看不同势力旗帜，并在正常接管后核对外观更新。
4. 在 Unity Test Runner 执行 `ArtPresentationResourceTests` 与 `ArtPresentationPausePlayModeTests`；实际 Build 仅包含同一 LevelTester 场景，检查进入世界后的树、矿、床、建筑和旗帜资源加载。无新场景或作弊按钮。

主要修改文件：`EntityView.cs`、`EntityViewSpawner.cs`、`HostCharacterArtState.cs`、`HostStepAArt.cs`、`HostMeleeStrikeVfx.cs`、`MapLayoutPrefabResolver.cs`、`MapLayoutPrefabCatalog.cs` 和 Resources 引用表、`HostFactionFlagPresenter.cs`、`HostMapVisualGeometry.cs`、`HostMapObjectVisualFactory.cs`、`HostWorldObjectPickGeometry.cs`、`HostFarmFieldConstructionPresenter.cs`、`HostDynamicOpportunityObjectPresenter.cs`，以及三份定向测试、审计脚本和本报告/覆盖 TSV。
