# MAP-04 — Physical Legacy Cleanup

> **2026-09-22 实现现名索引（不改历史清单／正文）：** 历史 diff 中的 `WorldSiteOutdoorBakeTransform`、`WorldSiteSpatialMapping`、`WorldSiteFootprintLocationAuthority` 当前分别对应 `WorldSiteHexFootprintBakeTransform`、`WorldSiteHexFootprintSpatialMapping`、`WorldSitePhysicalRegionQuery`／`LegacyWorldSiteHexLocationCompatibility` 的收窄职责。测试名与下方历史路径按当时事实保留。

> 日期：2026-09-17
> 状态：**Producer Accepted / Sealed（2026-09-20）**
> **2026-09-20 封板：** 制作人已完成人工验收；最终 Cave Loot persistence 补丁亦通过。MAP-04 正式封板，后续 Legacy Finalization 另立 LEGACY-FINAL-A／B／C，不回写本阶段验收范围。
> **历史 checkpoint（2026-09-19）：** 第一批大清理 = `596d9c9`；第二批 + FormalArmy／Snapshot 回归修复 = `54141d1`。当时尚无 seal 提交；[247](247-project-handoff-current-state-2026-09-18.md) §13 的 consumer 数量与 gate 是封板前审计快照，不再是当前待办。
> **封板后的兼容边界：** 源码中的 Legacy／Hex 命名只有在旧 Content／Snapshot、显式 compatibility、工具或测试契约中才可保留；判断标准见 [ADR-0038](43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)，不得按旧文件数量重新开启 MAP-04。
> 制作人约束：不暂存、不提交（除非授权 checkpoint）；不打开 Unity，不运行 PlayMode、Test Runner 或 batchmode。

## 2026-09-20 Final Acceptance Patch — Cave Loot Persistence

制作人人工验收确认 MAP-04 其余最终项目正常，唯一剩余阻断是洞府地上物拾取后 Save／Load 会再次出现。根因是拾取所得物品已由既有 `PartyInventorySlots` 保存，但决定物体是否再次物化的 `loot:*` runtime flag 未进入 Snapshot。

本补丁为 `WorldSnapshot` 增加 additive、optional 的 taken-loot authority，不升级 schema，也不持久化通用 StoryFlag。新存档按 ordinal 排序保存所有已取走的稳定 loot spot identity；恢复直接重建 `loot:*` runtime state，不发布拾取、背包或 StoryFlag gameplay event。LocalMap identity 统一为 `MapLayoutId + PlacementId`，Outdoor 继续使用已有全局 `StableId`；地图隐藏检查和拾取命令共用同一 identity。背包满时仍先由既有 inventory transaction 拒绝，taken state 不会写入。

制作人已确认洞府拾取→保存→加载→重新进入不再复生，本补丁随 MAP-04 一并 **Producer Accepted / Sealed**。

## 2026-09-20 Final Physical Legacy Cleanup

本轮以当前调用链重新审计，不按旧 grep 数量删除。Normal Outdoor 的正式入口为 `SurfaceId + exact WorldPosition + SurfaceGroundNavigation`。`HostWorldMapPanel` 的 marker、列表定位与点选旅行读取 Surface 坐标；SiteCore / FactionFlag 攻击读取 `CoreSurfaceId/CoreWorldPosition` 与 `ContinuousCharacterSpatialAuthorityResolver`；opening population 由 `openingEntityAnchors` 建立 personal presence；FormalArmy authored deployment 由 `initialSurfaceDeployment` 相对 SiteCore 精确落点建立 `WorldMotion`。

制作人验收发现 follower 出现短移动与停顿交替。删除 `TryCommitNormalWalk` 时遗留的 Active Character `if` 把共享 waypoint arrival gate 错误变成了仅 Active 执行；非 Active follower 与普通 NPC 因此会逐帧提前消费整条 path。现已删除该遗留条件，`TickMoves` 对所有 moving Entity 统一在真正到达当前 waypoint 后才推进 path 或完成移动；跟随参数与 W1B runtime 均未恢复。其余 MAP-04 删除点未发现同类 dangling control flow，仍待 Unity 人工复验。

物理删除了无剩余激活 caller 的 `ContinuousWildernessLoadedSet` / `ContinuousWildernessPairSelector` / `ContinuousWildernessSurfaceCoordinates`，同步删除只验证该退役 W1B runtime 的 `ContinuousWildernessW1BTests`，并清除 Host 中的 pair seam、复合 bounds、pair walk-grid、pair overlay 分支。正常 Surface position sync 的合法性现在只由 `SurfaceGroundNavigation` 判定；`CurrentHex` 只在成功提交 exact `WorldPosition` 后作为序列化兼容投影更新。正常 Outdoor 的 `PartyWorld` summary 与 retired Outdoor LocalMap snapshot migration 写 `AtWorldPosition`，不再复活 `AtHex` authority。

剩余 `AtHex` caller 分为：`StrategicSnapshotHelper` / `SnapshotActiveControlledLocalMapResolver` 的旧档迁移；`WorldTravelService.EnterWildernessLocalMap`、旧 LocalVisible / Wilderness transition 的 retired Outdoor LocalMap compatibility；`ManualBattleWorldCommitService`、battle anchor、encounter resolve / residual 的 Independent Battle 或战后 residual；无正常 Surface WorldMap、movement、materialization 或 SiteCore/Flag attack caller。`WorldRegion` 在 Core / Unity 已无实际 board 或 gameplay lookup，只剩 Data schema、旧包 parser / validator / authored import。

删除了 LevelTester 的 `Debug Force Leave Separate Space` 和 `[W1C] Activated / Hex change / Neighborhood / Deactivated` 日志；保留 `OpeningPopulationDiagnostic`、`ContinuousStartupPostconditionDiagnostic`、精简后的 `OutdoorAuthorityDiagnostic` 和性能计数。Separate Space 的 `LocalMap`、occupants、精确 `EntityLocation` placement 与 Independent Battle arena 保持合法独立 authority。Outdoor materialization cleanup 只作用于其自己的 `_continuousSitePopulation` / materialization board，不遍历全世界清理 inactive Separate Space placement。

本轮未修改 opening anchors、试炼匪军成员或部署 Content。荒村山匪／试炼弱匪／强匪仍使用 `initialSurfaceDeployment`，相对荒村 SiteCore 约 800 Surface Cells；每组成员定义保持原样。WorldMap marker world-space scale、Actual Control union overlay、Site/Flag exact attack、snapshot distant-NPC loaded-chunk gate 均沿用已建立的 Continuous authority。

## Final Seal Preparation — Legacy Residual Matrix（2026-09-20）

本矩阵按真实 producer / consumer 与入口分类。保留项是兼容边界，不授权新功能继续写入 Legacy authority。

| Residual | 分类 | 当前合法用途 | 新功能禁止事项 |
| --- | --- | --- | --- |
| `PartyWorldPresenceMode.AtHex` / `WorldAgentPresence.SetAtHex` | B：旧档／兼容 | 旧 Snapshot 缺少 modern world authority；旧 Outdoor LocalMap；非 Continuous Hex battle；只有 Hex 的 residual/corpse | Normal Continuous PlayerParty、普通 Character、Surface battle 不得生产新的 AtHex spatial truth |
| `CurrentHex` / Destination / HexPath | B：派生 metadata／旧路线 | `WorldPosition → derived CurrentHex` 序列化兼容；旧 Hex route、旧存档和诊断 | 不得用 CurrentHex 重建、吸附或决定 normal Continuous 真实位置 |
| `BattleAnchorHex` | B：非 Continuous battle compatibility | 无 `HasBattleAnchorWorldPosition` 的旧 Hex world combat／旧 Snapshot | Normal Continuous battle 使用冻结的 `BattleAnchorSurfaceId + BattleAnchorWorldX/Y` |
| `WorldRegion` | B：authored/import compatibility | Data schema、旧包 parser/validator、DefinitionRegistry、migration/import source | 不得决定 normal runtime visibility、movement、combat、Site、WorldMap 或 opening materialization |
| Outdoor `LocalMap` / `EnterWildernessLocalMap` / LocalVisible Hex stack | B：旧地图 compatibility | 旧包、旧档、legacy Hex travel 与历史 fixture | Continuous Surface 跨 Chunk／旧 Hex boundary 不切 LocalMap；WorldMap 点选走 `PlayerPartySurfaceTravelService` |
| Separate Space `LocalMapSession` | C：正式独立空间 | Cave／Interior／Dungeon 的 map、occupants、local placement、snapshot restore | 不得与旧 Outdoor LocalMap 语义合并；离开恢复 exact Surface return |
| Independent / non-continuous battle LocalMap | C：正式独立战斗或 B：旧 Hex battle | Explicit encounter arena 与没有 world anchor 的 Hex compatibility | Continuous ground combat 不得退回 BattleHex center 或 Outdoor LocalMap |
| Continuous runtime 对 residual `AtHex` 的读取 | B：old-save residual presentation | restore 已附加明确 `PersonalSurfaceId` 后，在已加载 chunk 呈现旧 downed/corpse | 不得作为 modern producer；没有明确 Surface provenance 不得物化 |
| `ClearPresentationOverride` | A/C 内部受限 cleanup | Continuous runtime 自己 materialize 的 population、prepared combat rollback、明确 entity teardown | 不得遍历全世界或删除 inactive Separate Space persistent placement |

Final audit 修复了三处仍会产生 modern AtHex 的真实 A 类残留：Continuous movement 的 traveling members 现在写 `AtWorldPosition + SurfaceId`；Snapshot active resolver 对已确认 Continuous position 返回 `AtWorldPosition`；`ManualBattleWorldCommitService` 在 snapshot 带 Continuous world anchor 时以 exact anchor 提交 AutoResolve／兼容 caller，只有无 world anchor 的 non-continuous battle 才进入 BattleHex branch。现代 Snapshot 的 `AtWorldPosition`／`AtSite`／`InSeparateSpace` 原样恢复；旧 `AtHex` 若能落入已注册 Surface，会在 restore 时迁移成 `AtWorldPosition`。

严格 dead-code gate 只删除了已无 Runtime／migration／Separate Space／battle／import caller 的 W1B primary-context API：`ApplyWildernessPrimaryContextWithoutUnload`、两个 `TryCommitSeamlessWildernessCrossing*` 方法及其 W1B 专用测试。`EnterWildernessLocalMap`、LocalVisible／Hex travel stack、AtHex restore、residual consumer 与 WorldRegion schema 均特意保留，因为仍有明确 compatibility consumer。

`HostLevelTesterCheatPanel` 的普通「诊断」Tab 保留到 MAP-04 制作人验收，继续展示 authority、opening census、startup postcondition、performance 与错误信息；未恢复 Force Leave，未新增诊断页。制作人验收通过后的 housekeeping 可退休普通诊断 UI，底层 performance/error counters 可继续保留。

## FormalArmy authored deployment authority 稳定性修复（制作人复验待定）

制作人再次发现三支军队成员堆在荒村附近。根因不只是坐标：Bootstrap 先给新成员写 assembly Site personal presence，`CreateAuthoredArmy` 再把 Army 初始化到该 Site；随后虽然改写 Army.WorldMotion，idle `FormalArmyMemberPresenceSync` 保留旧个人位置，LocalVisible idle presentation 也优先读旧个人位置。另有 `FormalArmy.SyncLegacyFromWorldMotion()` 每次把 `UsesHexStrategicPosition` 写成 `HasPosition`，覆盖 Surface 语义。本热修使 authored Surface 部署直接建立首次 Army anchor，明确 GroupRelocation 同步受控成员；普通 idle tick 仅保留 Army 锚点附近的 near-field personal position。idle／moving View 共用 Army.WorldMotion + transient formation，队形不写回个人 canonical position，间距由 1.5 提到 3 Surface Cells，并对超出合理编队半径的物化位置作一次性诊断。

正式 Content 新增 `initialSurfaceDeployment = {surfaceId, anchorSiteId, offsetCellsX, offsetCellsY}`。Bootstrap 从同一份 authored controlCore placement 解析精确中心（当前军队初始化早于运行时 SiteCore metadata bind），加导航 `CellSize × offsetCells` 得到一次性的 exact deployment；旧 `initialSurfacePosition` 继续只作绝对坐标兼容。荒村议政厅实际 placement 左下角是 `(6.01888,11.075)`，Core 中心为 `(6.105485,11.215)`。荒村山匪／弱匪／强匪分别用 `(722,334)`、`(797,-5)`、`(722,-343)` cells，约 795.5／797.0／799.3 cells，落点约 `(26.321485,20.567)`、`(28.421485,11.075)`、`(26.321485,1.611)`。原绝对坐标相对实际 Core 中心仅约 79.3／27.4／45.5 cells。三点在正式 Surface bounds 内，geography cell 为 Ground、无 blocking SitePlacement；成员名册与战力不变。

Snapshot 继续以 Army.WorldMotion 为 authority；restore/finalize 对仍由 Army 控制的 Surface 成员执行 GroupRelocation，而不是重新按 assemblySiteId 或 authored deployment 回生。Surface 初始化和 legacy 同步按 SurfaceId 设置 `UsesHexStrategicPosition=false`。当前仅离线 Core／Data／Unity Host 编译、Content load/validation、一次 Ch01 NewGame authored army invariant sanity，以及把三支军队临时移回 assembly Site 后分别应用已捕获的 FormalArmy motion DTO，三支 Army 与 living member 均恢复原始远程锚点。此为 isolated motion restore 检查，不等同完整 Host Save/Load 人工验收；未打开 Unity。状态仍为 **Implementation In Progress / Producer Acceptance Pending**，暂停其余 MAP-04 Legacy 删除。

## 前次荒村山匪野外部署尝试（已由上述修复取代，记录保留）

三支山匪／试炼 FormalArmy 保留 `assemblySiteId=base:site_huangcun` 作为组织所属地点，仅增加不同的 `initialSurfacePosition`：荒村山匪 `(3.990, 10.542)`、试炼弱匪 `(5.446, 11.606)`、试炼强匪 `(6.594, 10.038)`，均属 `base:surface_main_wilderness_v1`。不改成员、首领、战力或普通 NPC anchor。NewGame 沿用 `FormalArmyContentBootstrap → FormalArmyContinuousTravelService` 写入 exact `WorldMotion`。WorldMap marker 改为优先读取当前 Continuous `WorldMotion`，Snapshot motion 恢复保留 SurfaceId，不再因 derived Hex／assembly Site 偏移。

离线 BaseGame Content validator 0 error；三点均在 active Surface 内，Navigation `IsWalkable=true`、cell=`Ground`，且不与荒村 `blocksMovement=true` 的 SitePlacement 相交。NewGame 三支 Army 与 marker 均与 authored 坐标一致；将一支 Army 移到另一可步行点后 Save→Restore→Content shell→FormalArmy motion restore，三支 Army 与 marker 均保持 saved exact 位置。Host 最小离线编译 0 error、15 条既有 warning；未启动 Unity，仍待制作人人工验收。

## Restore / Presentation 稳定性回归修复（制作人复验待定）

制作人报告 WorldMap 透出 Continuous Surface，以及 Snapshot Load 后远处角色出现在玩家附近并抽搐。暂停进一步 Legacy 删除。对照 MAP-03 已验收 checkpoint `b04920b`，Surface WorldMap 重新使用不透明全屏底色及地图视口底色，恢复顶层输入阻断、打开时关闭背包／建造／任务 UI 并冻结 LocalVisible 旅行、关闭后恢复旅行；保留 Surface 地图、固定头部／底栏、战略面板与 inspect，不恢复 Hex WorldMap。

Snapshot DTO 的 `Mode`、`SiteId`、`PersonalSurfaceId`、exact `WorldX/Y` 在 presentation rebuild 前逐实体校验，不一致为 `SnapshotInvalid`。开局 anchor 只供 NewGame，以及旧档真正缺少对应 presence DTO 时按该实体 SpawnKey／DefinitionId 唯一匹配迁移。换 World 后清除普通 outdoor 的旧 presentation override；首次 Surface activation／materialization 期间只允许 Domain→View，不从 View 捕获 personal position。Current snapshot 的 AtSite 角色若无 exact position，不走 SiteArrival fallback；有 exact position 时 loaded chunk 是硬门。

FormalArmy 驻站成员由 continuous `WorldMotion.AtWorldSite + SiteId` 判定，只有确实有 Hex grid 时才走旧兼容分支。个人位置捕获跳过 Party、FormalArmy、背景旅行、手动战斗、独立战斗与遭遇 owner；军队队形 View 偏移不再写入个人 WorldPresence。View realign 只处理新物化实体，并避开正在移动或由上述 owner 控制的人。首次物化输出 restored／eligible／materialized／rejected chunk／rejected site／legacy migrated／missing authority 计数，远处 exact-position 实体若仍有 View 会报 `[SnapshotMaterializationLeak]`。

本轮轻量离线检查：Core／Data／Unity Host 编译 0 error、15 条既有 warning；BaseGame Content validator 0 error；Ch01 24 anchors／24 spawned／24 presence；Snapshot round-trip 保留异地 NPC 的 exact 坐标，故意篡改 DTO 可触发 mismatch，缺失 DTO 可检测；`git diff --check` 通过。未启动 Unity，因此全屏视觉、输入遮挡、旅行冻结／恢复和实际 Load 后的抖动仍须制作人人工验收。状态保持 **Implementation In Progress / Producer Acceptance Pending**。

## Opening NPC 回归热修（制作人复验待定）

制作人 Unity 人工测试发现开局 NPC 消失。删除 `WorldRegionBootstrap.ApplyOpening` 后，15 名既无 `worldSiteId` 又无 `localLocationId` 的 Ch01 NPC 不再获得旧 `EntityLocation`；其中主管经 FormalArmy bootstrap 另获 Army presence，其余 14 名因旧 place→Site 推断失效而没有 `WorldPresence`。现在 Normal Surface 开局直接以 `openingEntityAnchors` 的稳定 SpawnKey、DefinitionId、SiteId、WorldX/Y 建立 presence；`SourceLocationId` 仅恢复逻辑地点。独立 anchor census 在 NewGame 成功前检查当前 scenario 的全部 opening spawn。离线 Ch01：24 anchors、24 spawned、24 presence、missing/wrongSite/missingPosition 均 0；BaseGame validator 0 error。未修改 `ContinuousOutdoorSurfaceRuntime`，未继续删除 Legacy，状态仍为 Implementation In Progress / Producer Acceptance Pending。

## 本轮已落地

- New Game 使用 `openingSurfaceId`、已发布 Surface Site／FactionFlag 和 `openingEntityAnchors`；不调用 Hex strategic bootstrap，不加载 HexWorld JSON，不激活 Outdoor WorldRegion。离线启动：`HEX=False`、17 Site、0 旧 LocalPlace、玩家精确坐标 `(5.253, 10.239)`。
- 三个当前 Scenario 只写 `openingSurfaceId`。Runtime opening anchor 校验唯一性、定义、Surface 覆盖、可行走性；不再从 Outdoor MapLayout／LocalPlaceSet 反推或复算位置。`WorldSitePhysicalRegionDefinition` 不再使用 SourceLocalMapId；旧字段仅可被 schema 识别并忽略。
- `HostWorldMapPanel` 收敛为 Surface 地图，保留地图查看、站点／部队定位、玩家路线和战略面板；缺 Surface 显示“当前世界没有可用的连续世界地图。”。已删除 Hex renderer、grid drawing、picker、projection、Hex map editor 和专属旧 UI helper。
- `WorldVec2` 从 `Core.World.Hex` 移入中性 `Core.World`。玩家 Surface travel 使用 `BeginSurfaceAutoTravel` 和中性取消／抵达 helper；NPC／FormalArmy 的正常 Surface route 失败不回退 Hex。军队增加精确 Surface 位置旅行，Surface 追击可走当前导航。
- 从 BaseGame 删除两份 HexWorld JSON、35 个 Outdoor MapLayout JSON、34 个 Outdoor LocalPlaceSet JSON，以及 `world_regions.json`。洞府拆出独立 LocalPlaceSet；遭遇战术图从 `world_node_stub` 更名为独立 arena。旧 HexWorld 与参考 Outdoor 地图作为 EditMode 历史测试 fixture 单独保留在 `Assets/Tests/Fixtures/LegacyWorld`，不会被当前 ContentPackage 加载。
- `WorldRegionBootstrap` 已退役；独立地图使用 `InteriorLocalPlaceBootstrap`。旧运行时 board 改为 `LocalPlaceBoard`；WorldRegion definition parser 只用于旧包输入。旧 Outdoor LocalMap snapshot ID 在 Host restore 时转换为 Surface，旧遭遇图 ID 映射到新 arena；旧 Hex 人物／军队位置在加载时尝试转换成 Surface 世界坐标。
- WorldGraphEditor、RegionEditor、Shared/HexWorld 及其 launcher、manifest／solution 项已删除；HexWorldContentLoader、HexStrategicMapBootstrap、Ch01HexPrototypeMapBuilder、HexTestWorldBootstrap、HexWorldStressMapBuilder、StrategicBootstrap 等 8 个 fixture 类已移到 EditMode 测试目录，不再编入 Runtime。MapEditor、LocalPlaceEditor 收窄为 Interior／Cave／独立地图；WorldComposer 当前发布 UI 已去掉“兼容模式”，Migration Import 可以缺少已删除的旧参考图。

## 未通过的 Completion Gate 与风险

1. **Build All Apps 切换失败。** 本轮只运行一次 `publish.ps1`：manifest 确认为 10 个 Editor，10 个项目全部编译并发布到临时 staging；将候选 `Apps` 移入正式目录时，Windows 返回 `Access to the path ...\.build\publish-staging-...\Apps is denied`。脚本回滚成功，现有 `Apps` 仍为 10 个旧 exe。未按制作人“最多一次”约束重复 Build All。`publish.ps1` 已加入候选目录访问拒绝的有限重试与部分切换保全；此修正只经过 PowerShell 语法解析，尚未再次执行发布。需要查明访问拒绝来源，之后由制作人允许再次执行交付构建。
2. **Legacy Hex gameplay 源码仍较广。** 8 个纯 fixture 类已移出 Runtime。 `StrategicTravelDriver` 对无 Hex grid 的当前 BaseGame 直接返回；`PlayerPartyHexTravelService`、`ArmyHexTravelService`、Hex pathfinder／pursuit、HexWorld loader 与 fixture bootstrap 仍供历史测试／旧包兼容源代码使用。当前产品不能声称“完整 Hex Gameplay stack 已物理删除”。特别是旧快照中的特殊路线／战斗情形尚未经 Unity 人工验收。
3. **WorldMap 功能回归风险。** Surface-only 组件经离线 Host 编译，但未进行 Unity 视觉与交互验收。旧 Hex UI 附带的复杂检查／战斗交互没有逐一映射到新的 Surface UI；制作人需手验。
4. **旧战略可视化编辑入口。** WorldGraphEditor 的 FactionManagerWindow、OpeningStrategicEditorWindow 已删除；当前 WorldComposer 可发布 Surface faction flag 内容，但尚无等价的独立可视化外交／开局战略编辑窗口。可直接编辑 current JSON，正式工具体验待制作人确认。
5. **历史 EditMode 测试。** 旧 fixture 路径已搬迁，但按本轮禁止运行 Unity Test Runner 的要求，未执行这些测试；其语义仍覆盖旧 Hex，而非当前产品验收。

因此本文件保持 **Implementation In Progress / Producer Acceptance Pending**，不得写成 Accepted／Sealed 或宣称 Completion Gate 全部通过。

## Intentional Legacy Compatibility Remaining

| 类别 | 文件／调用 | 处理边界 |
| --- | --- | --- |
| 旧包解析 | `DefinitionSchema`、`ContentPackageLoader`；`Assets/Tests/EditMode/LegacyHexFixtures` | Runtime parser 识别旧字段；旧 HexWorld apply／bootstrap 只编入 EditMode 测试。 |
| 旧快照 DTO 与迁移 | `WorldSnapshot`、`StrategicSnapshotHelper`、`FormalArmySnapshotRestore`、`HostSnapshotSessionRehydration` | 保留 Hex 坐标、Presence、旧 Outdoor LocalMapId 读取；加载时尽量转换到 Surface。 |
| 独立地图 | `MapLayoutDefinition`、`LocalPlaceSetDefinition`、`LocalPlaceBoard`、`InteriorLocalPlaceBootstrap` | Cave、独立 Encounter 的地点与战术布局；不再担当 Outdoor 开局世界。 |
| 历史测试 | `Assets/Tests/Fixtures/LegacyWorld`、旧 EditMode Hex 测试 | 不属于 current BaseGame Content。 |

## 轻量验证

- Core／Data 离线 Content load + `ContentReferenceValidator`：`VALID=True ERRORS=0`。
- `PlayableDayBootstrap.Start`：`START=True`，`HEX=False`，17 Sites，旧 LocalPlace 0。
- Core／Data／Unity Host 离线 C# 编译：0 error（现有 warning）。未打开 Unity。
- BaseGame/Data JSON 解析：37 文件，0 error。
- `git diff --check`：exit 0。
- ExternalTools Build All：10 项 publish 成功，`Apps` switch 因 Windows access denied 失败并成功 rollback；最终 exit 1。

## 制作人人工验收 checklist（待执行）

- 在 Unity 中新开 Ch01：验证玩家／同伴／NPC 精确位置、Surface 地面、6 个站点及 11 面旗帜，不加载 HexWorld 或 Outdoor LocalMap。
- 打开 WorldMap：验证 Surface terrain、站点／部队标记、右键路线、抵达、定位和战略面板；缺 Surface 时核对 Content Error。
- 进入洞府与独立遭遇战术图：验证进入／退出、地点与交互。
- 加载旧快照：至少覆盖旧 Outdoor LocalMap、纯 Hex 人物位置、军队旅行和遭遇战术图 ID 迁移。
- 关闭可能占用 `Apps` 或 staging 的外部进程后，由制作人允许再次 Build All；核对 10 个新 exe 与 WorldComposer 当前发布产物。
- 审核 Surface-only WorldMap 与战略作者工具是否满足原有操作需求；确认是否需要补回可视化外交／开局战略编辑能力。

## 完整未提交文件列表

以下为 `git status --short`，未跟踪目录已展开到实际文件。所有项目均未暂存。

```text
 M Assets/DynamicData/GameData/Levels/README.txt
 M Assets/Scenes/LevelTester.unity
 M Assets/Scripts/Core/Exploration/ExplorationService.cs
?? Assets/Scripts/Core/Exploration/LocalPlaceBoard.cs
?? Assets/Scripts/Core/Exploration/LocalPlaceBoard.cs.meta
 D Assets/Scripts/Core/Exploration/WorldRegionBoard.cs
 D Assets/Scripts/Core/Exploration/WorldRegionBoard.cs.meta
 M Assets/Scripts/Core/Persistence/FormalArmySnapshotRestore.cs
 M Assets/Scripts/Core/Persistence/StrategicSnapshotHelper.cs
 M Assets/Scripts/Core/Simulation/SimulationWorld.cs
 D Assets/Scripts/Core/World/Hex/HexMapEditorService.cs
 D Assets/Scripts/Core/World/Hex/HexMapEditorService.cs.meta
 D Assets/Scripts/Core/World/Hex/WorldVec2.cs
 D Assets/Scripts/Core/World/Hex/WorldVec2.cs.meta
 M Assets/Scripts/Core/World/PlayerPartyRuntime.cs
 M Assets/Scripts/Core/World/Strategic/ArmyHexTravelService.cs
 M Assets/Scripts/Core/World/Strategic/ArmyPursuitTargetService.cs
 M Assets/Scripts/Core/World/Strategic/BackgroundCharacterSiteDepartureResolver.cs
 M Assets/Scripts/Core/World/Strategic/BackgroundCharacterTravelMotion.cs
 M Assets/Scripts/Core/World/Strategic/BackgroundCharacterTravelService.cs
 M Assets/Scripts/Core/World/Strategic/BackgroundSimulationScheduler.cs
 M Assets/Scripts/Core/World/Strategic/BackgroundSiteDepartureTravelTrace.cs
 M Assets/Scripts/Core/World/Strategic/BattleEngagementTriggerService.cs
 D Assets/Scripts/Core/World/Strategic/Ch01HexPrototypeMapBuilder.cs
 D Assets/Scripts/Core/World/Strategic/Ch01HexPrototypeMapBuilder.cs.meta
 D Assets/Scripts/Core/World/Strategic/Ch01ScenarioStrategicSetup.cs
 D Assets/Scripts/Core/World/Strategic/Ch01ScenarioStrategicSetup.cs.meta
 M Assets/Scripts/Core/World/Strategic/CharacterEncounter.cs
 M Assets/Scripts/Core/World/Strategic/CharacterPersonalSpaceQuery.cs
 M Assets/Scripts/Core/World/Strategic/FactionFlagService.cs
 M Assets/Scripts/Core/World/Strategic/FormalArmyContinuousTravelService.cs
 M Assets/Scripts/Core/World/Strategic/FormalArmyLocationKinds.cs
 M Assets/Scripts/Core/World/Strategic/FormalArmyOrderReplaceTrace.cs
 M Assets/Scripts/Core/World/Strategic/FormalArmyTestSupport.cs
 M Assets/Scripts/Core/World/Strategic/FormalArmyWorldLocationQuery.cs
 M Assets/Scripts/Core/World/Strategic/FormalArmyWorldMotion.cs
 M Assets/Scripts/Core/World/Strategic/HexFootprintSpatialGeometry.cs
 M Assets/Scripts/Core/World/Strategic/HexFootprintSpatialMapping.cs
 D Assets/Scripts/Core/World/Strategic/HexStrategicMapBootstrap.cs
 D Assets/Scripts/Core/World/Strategic/HexStrategicMapBootstrap.cs.meta
 M Assets/Scripts/Core/World/Strategic/HexStrategicRuntime.cs
 D Assets/Scripts/Core/World/Strategic/HexTestWorldBootstrap.cs
 D Assets/Scripts/Core/World/Strategic/HexTestWorldBootstrap.cs.meta
 D Assets/Scripts/Core/World/Strategic/HexWorldStressMapBuilder.cs
 D Assets/Scripts/Core/World/Strategic/HexWorldStressMapBuilder.cs.meta
 M Assets/Scripts/Core/World/Strategic/LoadedDestinationArrivalMaterializer.cs
 M Assets/Scripts/Core/World/Strategic/PlayerPartyLocalMapMaterializationService.cs
 M Assets/Scripts/Core/World/Strategic/PlayerPartyLocalVisibleAutoTravelService.cs
 M Assets/Scripts/Core/World/Strategic/PlayerPartySiteIngressTrace.cs
 M Assets/Scripts/Core/World/Strategic/PlayerPartyStrategicCombatCommandService.cs
 M Assets/Scripts/Core/World/Strategic/PlayerPartySurfaceTravelService.cs
 M Assets/Scripts/Core/World/Strategic/PlayerPartyWorldMotion.cs
 M Assets/Scripts/Core/World/Strategic/PreEngagementLegalLocation.cs
 M Assets/Scripts/Core/World/Strategic/ResidualSpatialAuthorityService.cs
 M Assets/Scripts/Core/World/Strategic/SquadState.cs
 D Assets/Scripts/Core/World/Strategic/StrategicBootstrap.cs
 D Assets/Scripts/Core/World/Strategic/StrategicBootstrap.cs.meta
 M Assets/Scripts/Core/World/Strategic/StrategicEncounterCatalog.cs
 M Assets/Scripts/Core/World/Strategic/StrategicEncounterSpawner.cs
 M Assets/Scripts/Core/World/Strategic/SurfaceExitZoneCalculator.cs
 M Assets/Scripts/Core/World/Strategic/WildernessLocalWorldProjection.cs
 M Assets/Scripts/Core/World/Strategic/WorldMapPartyTravelCommand.cs
 M Assets/Scripts/Core/World/Strategic/WorldSiteFootprintLocationAuthority.cs
 M Assets/Scripts/Core/World/Strategic/WorldSiteOutdoorBakeTransform.cs
 M Assets/Scripts/Core/World/Strategic/WorldSiteSpatialMapping.cs
 M Assets/Scripts/Core/World/Surface/ContinuousSurfaceHexCommitResolver.cs
 M Assets/Scripts/Core/World/Surface/ContinuousSurfacePrototypeGroundLegality.cs
 M Assets/Scripts/Core/World/WorldPresenceBoard.cs
?? Assets/Scripts/Core/World/WorldVec2.cs
?? Assets/Scripts/Core/World/WorldVec2.cs.meta
 M Assets/Scripts/Data/Bootstrap/ContentRuntimeBootstrap.cs
 M Assets/Scripts/Data/Bootstrap/ContinuousOutdoorOpeningPlacementResolver.cs
 M Assets/Scripts/Data/Bootstrap/ContinuousOutdoorOpeningPopulationBootstrap.cs
 M Assets/Scripts/Data/Bootstrap/ContinuousOutdoorSpawnPresenceResolver.cs
?? Assets/Scripts/Data/Bootstrap/ContinuousSurfaceSessionBootstrap.cs
?? Assets/Scripts/Data/Bootstrap/ContinuousSurfaceSessionBootstrap.cs.meta
 M Assets/Scripts/Data/Bootstrap/FormalArmyContentBootstrap.cs
 D Assets/Scripts/Data/Bootstrap/HexStrategicSessionBootstrap.cs
 D Assets/Scripts/Data/Bootstrap/HexStrategicSessionBootstrap.cs.meta
?? Assets/Scripts/Data/Bootstrap/InteriorLocalPlaceBootstrap.cs
?? Assets/Scripts/Data/Bootstrap/InteriorLocalPlaceBootstrap.cs.meta
 M Assets/Scripts/Data/Bootstrap/OpeningSpawnWorldPresenceApplier.cs
 M Assets/Scripts/Data/Bootstrap/PlayableDayBootstrap.cs
 M Assets/Scripts/Data/Bootstrap/StrategicContentBootstrap.cs
 D Assets/Scripts/Data/Bootstrap/WorldRegionBootstrap.cs
 D Assets/Scripts/Data/Bootstrap/WorldRegionBootstrap.cs.meta
 M Assets/Scripts/Data/Content/ContentPackageLoader.cs
 M Assets/Scripts/Data/Content/ContentReferenceValidator.cs
 M Assets/Scripts/Data/Content/ContinuousOutdoorOpeningAnchorResolver.cs
 M Assets/Scripts/Data/Content/ContinuousOutdoorStartupPlanner.cs
 M Assets/Scripts/Data/Content/DefinitionSchema.cs
 M Assets/Scripts/Data/Content/FormalArmyDefinition.cs
 D Assets/Scripts/Data/Content/HexStrategicMapContentBootstrap.cs
 D Assets/Scripts/Data/Content/HexStrategicMapContentBootstrap.cs.meta
 D Assets/Scripts/Data/Content/HexWorldContentLoader.cs
 D Assets/Scripts/Data/Content/HexWorldContentLoader.cs.meta
 M Assets/Scripts/Data/Content/LocalPlaceSetDefinition.cs
 M Assets/Scripts/Data/Content/OpeningScenarioDefinition.cs
 M Assets/Scripts/Data/Content/OutdoorWorldSurfaceDefinition.cs
 M Assets/Scripts/Data/Content/WorldSiteOutdoorOpeningAnchorBake.cs
 M Assets/Scripts/Unity/Editor/LevelTesterSceneTool.cs
 D Assets/Scripts/Unity/Host/BattleEngagementWorldMapDebug.cs
 D Assets/Scripts/Unity/Host/BattleEngagementWorldMapDebug.cs.meta
 M Assets/Scripts/Unity/Host/ContinuousOutdoorEncounterField.cs
 M Assets/Scripts/Unity/Host/ContinuousOutdoorSurfaceRuntime.cs
 M Assets/Scripts/Unity/Host/EntityViewSpawner.cs
 D Assets/Scripts/Unity/Host/FactionFlagWorldMapPresentation.cs
 D Assets/Scripts/Unity/Host/FactionFlagWorldMapPresentation.cs.meta
 D Assets/Scripts/Unity/Host/HexMapMousePick.cs
 D Assets/Scripts/Unity/Host/HexMapMousePick.cs.meta
 D Assets/Scripts/Unity/Host/HexMapViewportProjection.cs
 D Assets/Scripts/Unity/Host/HexMapViewportProjection.cs.meta
 M Assets/Scripts/Unity/Host/HostCaveEntranceQuery.cs
 M Assets/Scripts/Unity/Host/HostCaveSurveyPresenter.cs
 M Assets/Scripts/Unity/Host/HostCharacterPresentation.cs
 M Assets/Scripts/Unity/Host/HostCommandBridge.cs
 M Assets/Scripts/Unity/Host/HostControlCoreQuery.cs
 M Assets/Scripts/Unity/Host/HostCrowdPresenter.cs
 M Assets/Scripts/Unity/Host/HostCultivateConfirmPrompt.cs
 M Assets/Scripts/Unity/Host/HostDemoTileMap.cs
 M Assets/Scripts/Unity/Host/HostFactionFlagPresenter.cs
 D Assets/Scripts/Unity/Host/HostHexGridDrawing.cs
 D Assets/Scripts/Unity/Host/HostHexGridDrawing.cs.meta
 D Assets/Scripts/Unity/Host/HostHexWorldRenderer.cs
 D Assets/Scripts/Unity/Host/HostHexWorldRenderer.cs.meta
 M Assets/Scripts/Unity/Host/HostHousingAreaSelection.cs
 M Assets/Scripts/Unity/Host/HostHudSnapshot.cs
 M Assets/Scripts/Unity/Host/HostInteractSpots.cs
 M Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs
 M Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs
 M Assets/Scripts/Unity/Host/HostMapGraybox.cs
 M Assets/Scripts/Unity/Host/HostMoveController.cs
 M Assets/Scripts/Unity/Host/HostNpcContextMenu.cs
 M Assets/Scripts/Unity/Host/HostPlayerPartyController.cs
 M Assets/Scripts/Unity/Host/HostSnapshotSessionRehydration.cs
 M Assets/Scripts/Unity/Host/HostWorldMapPanel.cs
 M Assets/Scripts/Unity/Host/HostWorldObjectPicker.cs
 M Assets/Scripts/Unity/Host/HostZoneQuery.cs
 M Assets/Scripts/Unity/Host/LocalMapVisibility.cs
 M Assets/Scripts/Unity/Host/MapLayoutPick.cs
 M Assets/Scripts/Unity/Host/MapLayoutPresentationSync.cs
 M Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs
 D Assets/Scripts/Unity/Host/WorldSitePresentationLayer.cs
 D Assets/Scripts/Unity/Host/WorldSitePresentationLayer.cs.meta
 M Assets/Tests/EditMode/ArmyPhaseHTests.cs
 M Assets/Tests/EditMode/CaveShadePlacementTests.cs
 M Assets/Tests/EditMode/Chapter01ReferenceLevelAcceptanceTests.cs
 M Assets/Tests/EditMode/ContinuousMaterializePlacementSyncTests.cs
 D Assets/Tests/EditMode/ContinuousOutdoorOpeningPlacementBakeTests.cs
 D Assets/Tests/EditMode/ContinuousOutdoorOpeningPlacementBakeTests.cs.meta
 M Assets/Tests/EditMode/ContinuousOutdoorOpeningSpatialTests.cs
 M Assets/Tests/EditMode/ContinuousSurfaceSpatialRepairTests.cs
 M Assets/Tests/EditMode/DemoParityLevelAcceptanceTests.cs
 M Assets/Tests/EditMode/DemoVerticalSlice10AcceptanceTests.cs
 M Assets/Tests/EditMode/EncounterAssemblyTests.cs
 D Assets/Tests/EditMode/HexMapMousePickTests.cs
 D Assets/Tests/EditMode/HexMapMousePickTests.cs.meta
 D Assets/Tests/EditMode/HexMapViewportProjectionTests.cs
 D Assets/Tests/EditMode/HexMapViewportProjectionTests.cs.meta
 D Assets/Tests/EditMode/HexTerrainVisualInsetTests.cs
 D Assets/Tests/EditMode/HexTerrainVisualInsetTests.cs.meta
 M Assets/Tests/EditMode/HostZoneQueryTests.cs
?? Assets/Tests/EditMode/LegacyHexFixtures.meta
?? Assets/Tests/EditMode/LegacyHexFixtures/Ch01HexPrototypeMapBuilder.cs
?? Assets/Tests/EditMode/LegacyHexFixtures/Ch01HexPrototypeMapBuilder.cs.meta
?? Assets/Tests/EditMode/LegacyHexFixtures/Ch01ScenarioStrategicSetup.cs
?? Assets/Tests/EditMode/LegacyHexFixtures/Ch01ScenarioStrategicSetup.cs.meta
?? Assets/Tests/EditMode/LegacyHexFixtures/HexStrategicMapBootstrap.cs
?? Assets/Tests/EditMode/LegacyHexFixtures/HexStrategicMapBootstrap.cs.meta
?? Assets/Tests/EditMode/LegacyHexFixtures/HexTestWorldBootstrap.cs
?? Assets/Tests/EditMode/LegacyHexFixtures/HexTestWorldBootstrap.cs.meta
?? Assets/Tests/EditMode/LegacyHexFixtures/HexWorldContentLoader.cs
?? Assets/Tests/EditMode/LegacyHexFixtures/HexWorldContentLoader.cs.meta
?? Assets/Tests/EditMode/LegacyHexFixtures/HexWorldStressMapBuilder.cs
?? Assets/Tests/EditMode/LegacyHexFixtures/HexWorldStressMapBuilder.cs.meta
?? Assets/Tests/EditMode/LegacyHexFixtures/LegacyHexStrategicMapContentAdapter.cs
?? Assets/Tests/EditMode/LegacyHexFixtures/LegacyHexStrategicMapContentAdapter.cs.meta
?? Assets/Tests/EditMode/LegacyHexFixtures/StrategicBootstrap.cs
?? Assets/Tests/EditMode/LegacyHexFixtures/StrategicBootstrap.cs.meta
 M Assets/Tests/EditMode/LocalMapEnterLeaveTests.cs
 M Assets/Tests/EditMode/MapLayoutWalkGridTests.cs
 M Assets/Tests/EditMode/NpcSimulationFoundationTests.cs
 M Assets/Tests/EditMode/PlayerPartyContinuousWorldPhase2CTests.cs
 M Assets/Tests/EditMode/PlayerPartyLocalCoPresenceTests.cs
 M Assets/Tests/EditMode/PlayerPartyRuntimeTests.cs
 M Assets/Tests/EditMode/SiteCoreWarfareTests.cs
 M Assets/Tests/EditMode/SiteEconomyMigrationTests.cs
 M Assets/Tests/EditMode/StrategicPhaseTests.cs
 M Assets/Tests/EditMode/WildernessContextAuthorityTests.cs
 M Assets/Tests/EditMode/WildernessLocalWorldProjectionTests.cs
 M Assets/Tests/EditMode/WorldExplorationPhaseETests.cs
 M Assets/Tests/EditMode/WorldSiteCanonicalQueryTests.cs
 M Assets/Tests/EditMode/WorldSiteDepartureCrossingTests.cs
 M Assets/Tests/EditMode/WorldSiteDeparturePresentationTests.cs
 M Assets/Tests/EditMode/WorldSiteDepartureRouteConsistencyTests.cs
 M Assets/Tests/EditMode/WorldSiteDepartureTests.cs
 M Assets/Tests/EditMode/WorldSiteEgressContinuationTests.cs
 M Assets/Tests/EditMode/WorldSiteMultiHexGoalAuthorityTests.cs
 M Assets/Tests/EditMode/WorldSiteSpatialMappingTests.cs
 M Assets/Tests/EditMode/WorldSiteSpatialMappingV2Tests.cs
 M Assets/Tests/EditMode/WorldSiteSurfaceExitReliabilityTests.cs
?? Assets/Tests/Fixtures.meta
?? Assets/Tests/Fixtures/LegacyWorld.meta
?? Assets/Tests/Fixtures/LegacyWorld/ch01_hex_world.json
?? Assets/Tests/Fixtures/LegacyWorld/ch01_hex_world.json.meta
?? Assets/Tests/Fixtures/LegacyWorld/ch01_reference_map.json
?? Assets/Tests/Fixtures/LegacyWorld/ch01_reference_map.json.meta
 M Content/BaseGame/Data/Armies/qingshi_hostility_acceptance_armies.json
 M Content/BaseGame/Data/Events/chapter1_harness_events.json
 M Content/BaseGame/Data/Events/content_events.json
?? Content/BaseGame/Data/LocalPlaces/ch01_cave_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_reference_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_a_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_b_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_bei_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_chengzhen_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_daoguan_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_dong_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_dukou_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_er_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_feixu_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_fengkou_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_guanai_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_gudao_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_haijiao_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_kuangshan_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_lingdi_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_linjian_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_lu_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_miao_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_nan_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_shankou_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_shuizhai_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_tiejiangpu_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_wai_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_xi_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_yaotian_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_yingdi_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_yucun_places.json
 D Content/BaseGame/Data/LocalPlaces/ch01_site_zhuangyuan_places.json
 D Content/BaseGame/Data/LocalPlaces/player_camp_places.json
?? Content/BaseGame/Data/LocalPlaces/strategic_encounter_arena_places.json
 D Content/BaseGame/Data/LocalPlaces/wilderness_forest_fallback_places.json
 D Content/BaseGame/Data/LocalPlaces/wilderness_mountain_fallback_places.json
 D Content/BaseGame/Data/LocalPlaces/wilderness_plain_fallback_places.json
 D Content/BaseGame/Data/LocalPlaces/wilderness_road_fallback_places.json
 D Content/BaseGame/Data/LocalPlaces/world_node_stub_places.json
 M Content/BaseGame/Data/Maps/ch01_cave_map.json
 D Content/BaseGame/Data/Maps/ch01_reference_map.json
 D Content/BaseGame/Data/Maps/ch01_site_a_map.json
 D Content/BaseGame/Data/Maps/ch01_site_b_map.json
 D Content/BaseGame/Data/Maps/ch01_site_bei_map.json
 D Content/BaseGame/Data/Maps/ch01_site_chengzhen_map.json
 D Content/BaseGame/Data/Maps/ch01_site_daoguan_map.json
 D Content/BaseGame/Data/Maps/ch01_site_dong_map.json
 D Content/BaseGame/Data/Maps/ch01_site_dukou_map.json
 D Content/BaseGame/Data/Maps/ch01_site_er_map.json
 D Content/BaseGame/Data/Maps/ch01_site_feixu_map.json
 D Content/BaseGame/Data/Maps/ch01_site_fengkou_map.json
 D Content/BaseGame/Data/Maps/ch01_site_guanai_map.json
 D Content/BaseGame/Data/Maps/ch01_site_gudao_map.json
 D Content/BaseGame/Data/Maps/ch01_site_haijiao_map.json
 D Content/BaseGame/Data/Maps/ch01_site_kuangshan_map.json
 D Content/BaseGame/Data/Maps/ch01_site_lingdi_map.json
 D Content/BaseGame/Data/Maps/ch01_site_linjian_map.json
 D Content/BaseGame/Data/Maps/ch01_site_lu_map.json
 D Content/BaseGame/Data/Maps/ch01_site_miao_map.json
 D Content/BaseGame/Data/Maps/ch01_site_nan_map.json
 D Content/BaseGame/Data/Maps/ch01_site_shankou_map.json
 D Content/BaseGame/Data/Maps/ch01_site_shuizhai_map.json
 D Content/BaseGame/Data/Maps/ch01_site_tiejiangpu_map.json
 D Content/BaseGame/Data/Maps/ch01_site_wai_map.json
 D Content/BaseGame/Data/Maps/ch01_site_xi_map.json
 D Content/BaseGame/Data/Maps/ch01_site_yaotian_map.json
 D Content/BaseGame/Data/Maps/ch01_site_yingdi_map.json
 D Content/BaseGame/Data/Maps/ch01_site_yucun_map.json
 D Content/BaseGame/Data/Maps/ch01_site_zhuangyuan_map.json
 D Content/BaseGame/Data/Maps/huangcun_01.json
 D Content/BaseGame/Data/Maps/player_camp_map.json
?? Content/BaseGame/Data/Maps/strategic_encounter_arena.json
 D Content/BaseGame/Data/Maps/wilderness_forest_fallback_map.json
 D Content/BaseGame/Data/Maps/wilderness_mountain_fallback_map.json
 D Content/BaseGame/Data/Maps/wilderness_plain_fallback_map.json
 D Content/BaseGame/Data/Maps/wilderness_road_fallback_map.json
 D Content/BaseGame/Data/Maps/world_node_stub_map.json
 D Content/BaseGame/Data/Regions/world_regions.json
 M Content/BaseGame/Data/Scenarios/scenarios.json
 D Content/BaseGame/Data/Worlds/ch01_hex_world.json
 M Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json
 D Content/BaseGame/Data/Worlds/travel_mvp_hex_world_30x15.json
 D Content/BaseGame/Data/Worlds/w1c_wilderness_acceptance_surface.json
 M ExternalTools/ContentAuthoring/ContentAuthoring.sln
 M ExternalTools/ContentAuthoring/EditorManifest.json
 M ExternalTools/ContentAuthoring/LocalPlaceEditor/MainWindow.xaml
 M ExternalTools/ContentAuthoring/MapEditor/MainWindow.xaml
 M ExternalTools/ContentAuthoring/MapEditor/MainWindow.xaml.cs
 M ExternalTools/ContentAuthoring/README.md
 D ExternalTools/ContentAuthoring/RegionEditor/App.xaml
 D ExternalTools/ContentAuthoring/RegionEditor/App.xaml.cs
 D ExternalTools/ContentAuthoring/RegionEditor/AssemblyInfo.cs
 D ExternalTools/ContentAuthoring/RegionEditor/MainWindow.xaml
 D ExternalTools/ContentAuthoring/RegionEditor/MainWindow.xaml.cs
 D ExternalTools/ContentAuthoring/RegionEditor/RegionEditor.csproj
 D ExternalTools/ContentAuthoring/Shared.Tests/Ch01HexWorldRoundtripTests.cs
 D ExternalTools/ContentAuthoring/Shared.Tests/FactionFlagAuthoringTests.cs
 D ExternalTools/ContentAuthoring/Shared.Tests/HexEditorRenderCachePerfTests.cs
 D ExternalTools/ContentAuthoring/Shared.Tests/HexWorldEditorFootprintTests.cs
 D ExternalTools/ContentAuthoring/Shared.Tests/HexWorldTerritoryEditorTests.cs
 D ExternalTools/ContentAuthoring/Shared.Tests/MultiHexFootprintValidationTests.cs
 D ExternalTools/ContentAuthoring/Shared.Tests/TerritoryBrushDocumentTests.cs
 D ExternalTools/ContentAuthoring/Shared.Tests/TerritoryValidatorTests.cs
 M ExternalTools/ContentAuthoring/Shared/ContentPathRules.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/FactionFlagAuthoring.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexEditorRenderCache.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexMapViewport.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexTerrainPalette.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldContentGenerator.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldContentJson.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldContentModels.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldContentValidator.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldEditorDocument.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldEditorFootprintService.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldFootprintRules.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldLayoutShared.cs
 D ExternalTools/ContentAuthoring/Shared/HexWorld/HexWorldPresenceRules.cs
 M ExternalTools/ContentAuthoring/SurfaceAuthoring.Core/LegacyWorldMigration.cs
 M ExternalTools/ContentAuthoring/WorldComposer/MainWindow.xaml.cs
 D ExternalTools/ContentAuthoring/WorldGraphEditor/App.xaml
 D ExternalTools/ContentAuthoring/WorldGraphEditor/App.xaml.cs
 D ExternalTools/ContentAuthoring/WorldGraphEditor/AssemblyInfo.cs
 D ExternalTools/ContentAuthoring/WorldGraphEditor/FactionManagerWindow.cs
 D ExternalTools/ContentAuthoring/WorldGraphEditor/HexMapCanvasRenderer.cs
 D ExternalTools/ContentAuthoring/WorldGraphEditor/HexMapViewHost.cs
 D ExternalTools/ContentAuthoring/WorldGraphEditor/MainWindow.xaml
 D ExternalTools/ContentAuthoring/WorldGraphEditor/MainWindow.xaml.cs
 D ExternalTools/ContentAuthoring/WorldGraphEditor/OpeningStrategicEditorWindow.cs
 D ExternalTools/ContentAuthoring/WorldGraphEditor/WorldGraphEditor.csproj
 M ExternalTools/ContentAuthoring/publish.ps1
 D ExternalTools/ContentAuthoring/启动-RegionEditor.cmd
 D ExternalTools/ContentAuthoring/启动-WorldGraphEditor.cmd
 M docs/00-project/00-overview.md
 M docs/00-project/04-reading-guide.md
 M docs/20-systems/2N-continuous-surface-world-authoring-and-composition.md
 M docs/20-systems/README.md
?? docs/40-process/245-map-04-physical-legacy-cleanup-2026-09-17.md
 M docs/40-process/41-roadmap.md
 M docs/40-process/42-devlog.md
 M docs/40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md
```
