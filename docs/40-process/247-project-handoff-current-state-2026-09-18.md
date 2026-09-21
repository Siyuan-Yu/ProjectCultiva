# Project Handoff — Continuous World Current State
## Resume Snapshot — 2026-09-18
> **2026-09-21 current handoff：** LEGACY-FINAL-A 已由制作人完整人工验收并正式 **Accepted / Sealed**；已验收实现 checkpoint 为 `e97f53f`。当前主线为 **LEGACY-FINAL-B — PlayerParty Continuous Surface Travel Authority Cutover**，目标是把正常玩家旅行收口为 `SurfaceId + exact WorldPosition + SurfaceVisible`，并把 Hex／Outdoor LocalMap executor 限定为兼容路径。LEGACY-FINAL-C 为下一阶段；B/C 完成前不得宣称 Legacy Finalization Complete。

> **2026-09-20 Seal / Legacy Finalization superseding state：** MAP-04、CW-10、CW-10.5 与 Cave Loot persistence 已由制作人人工验收并正式 **Accepted / Sealed**。当前主线切换为 **LEGACY-FINAL-A — Unified NPC Squad World Motion / FormalArmy Runtime Retirement**；随后依次为 LEGACY-FINAL-B、LEGACY-FINAL-C。A/B/C 全部完成前不得宣称 Legacy migration complete。本文后续关于 MAP-04／CW-10／CW-10.5 pending 的段落均为历史恢复快照。

> **2026-09-20 LEGACY-FINAL-A implementation state：** A 已达到 **Implementation Complete / Producer Acceptance Pending**，正式记录见 [248 LEGACY-FINAL-A](248-legacy-final-a-unified-npc-squad-world-motion-2026-09-20.md)。正常 New Game 的 NPC group authority 已收口为 `Squad + SquadWorldMotion + exact Surface position`；Current BaseGame 不再创建 FormalArmy/ArmyStack，旧 content/save 仅作单向 migration input。待制作人验收 A 后才进入 B，TerritoryRegion 等 C 边界仍未完成。

> **2026-09-21 LEGACY-FINAL-A2 runtime zero-state（已封板）：** FormalArmyBoard、ArmyStackBoard、ArmyMembershipComponent 与旧 Army runtime/services 已物理退出 Simulation；旧 Content/Snapshot DTO 只在兼容边界直接单向迁移到 Squad + SquadWorldMotion，旧 active battle identity 迁到 SquadId / CharacterEncounter。制作人已完成人工验收，A 状态为 **Accepted / Sealed**。

> **2026-09-20 Current State supersedes the older snapshot below:** SPACE-01 已由制作人验收并以 `49f8650`（`Seal SPACE-01 separate-space ownership and persistence`）提交、推送，状态为 **Accepted / Sealed**。MAP-04 已恢复并完成 final physical cleanup 实施：删除 `ContinuousWildernessPair` runtime、清理 W1C acceptance logs / force-leave cheat、Normal Outdoor Surface sync 不再回退 Hex terrain legality、正常 `PartyWorld` summary 不再写 `AtHex`。MAP-04 当前为 **Implementation Complete / Producer Acceptance Pending**；本轮 MAP-04 文件全部保持未暂存、未提交、未推送。后文 2026-09-18 的 WIP、暂停与旧 consumer 数量是历史恢复快照，不再代表当前状态。

> **2026-09-20 Final Seal Preparation：** 最终 caller audit 发现并修复三处 modern AtHex producer：Continuous movement member presence、Snapshot active Continuous focus、Continuous AutoResolve battle commit。W1B primary-context dead APIs 已删除。现存 AtHex 只服务 old-save restore、旧 Outdoor LocalMap／Hex travel、non-continuous battle 与 legacy residual；WorldRegion 只保留 Data schema、旧包验证和 migration/import。Separate Space LocalMap 是正式独立空间 authority，不属于 Legacy。完整 Residual Matrix 见 [245 MAP-04](245-map-04-physical-legacy-cleanup-2026-09-17.md#final-seal-preparation--legacy-residual-matrix2026-09-20)。状态仍为 **Implementation Complete / Producer Acceptance Pending**，不得提前 Sealed。

> **2026-09-20 Cave Loot acceptance patch：** MAP-04 其余人工验收项已由制作人确认正常；最后阻断是洞府 loot 的 `loot:*` runtime flag 未进入 Snapshot，导致 Load 后重新物化。现增加窄范围 taken-loot Snapshot authority，并将 LocalMap loot identity 统一为 `MapLayoutId + PlacementId`；PartyInventory 沿用既有 authority，未扩展通用 StoryFlag persistence。纯 C# round-trip 覆盖已取、空 authority、跨 MapLayout 同名 placement 和背包满失败。MAP-04 仍待制作人复验后封板。CW-10／CW-10.5 current-code readiness audit 未发现 blocker，二者仍为 **Implementation Completed / Producer Acceptance Pending**。

> **本文是未来新会话（Codex / 新 ChatGPT / 新 Cursor 会话）接手本项目的正式入口。**
>
> - 轮次性质：**Documentation / Recovery Snapshot**（本轮禁止任何实现修改）
> - 请求日期：2026-09-18
> - 实际生成时间：**2026-09-19 00:33～00:45（本地）**，即制作人 09-18 夜场跨过午夜之后
> - 生成时 HEAD：`c05a3d26651693d0346db09e1b86220c3965d757`（branch `dev_openworld`）
> - 生成时工作树：**clean**（SPACE-01 的全部改动已由制作人在 2026-09-19 00:32:36 提交为 `c05a3d2`）
> - 关联：[246 SPACE-01](246-space-01-separate-space-interior-transition-v1-2026-09-18.md)｜[245 MAP-04](245-map-04-physical-legacy-cleanup-2026-09-17.md)｜[244 MAP-03](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)｜[243 MAP-02](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)｜[242 MAP-01](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)｜[240 Editor 工具链](240-editor-toolchain-cleanup-2026-09-15.md)｜[2N 系统真源](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)｜[ADR-0036](43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)｜[ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)
>
> 本文件**不是**新的架构真源；它是对当前仓库真实状态的登记与索引。系统正文仍在 `docs/20-systems/**`，决策仍在 `docs/40-process/43-decisions/**`。

---

# 1. Executive Summary

本项目是 **Unity 2D top-down 仙侠 RPG**（修仙世界模拟 + 角色扮演 + 战略层）。

当前阶段的真实结论：

- **Continuous Surface（连续地表世界）已经成为正常的 Outdoor 世界。** 一个大陆一张 Final Continuous Surface，正常户外 gameplay 使用 exact `WorldPosition` + `SurfaceId` + `SurfaceGroundNavigation`，**没有** Outdoor LocalMap 切换。
- **Hex / Outdoor LocalMap 正在退役。** Hex 字段与旧 LocalMap 代码仍大量存在于仓库中，但已被降级为 **Legacy / Derived Compatibility**，不再是正常产品的 authority。
- **WorldComposer / FineEditor 已经建立**（MAP-01，Producer Accepted / Sealed）。
- **Surface WorldMap 已经建立**（MAP-02，Producer Accepted / Sealed）。
- **Normal Gameplay Surface authority 已完成**（MAP-03，2026-09-17 Producer Accepted / Sealed；封板提交 `b04920b`）。
- **MAP-04（物理删除 Legacy）尚未结束**：已完成第一批大清理（外层 Editor、旧 Content 地图集、旧 WorldRegion 管线），剩余 consumer 仍多。
- **Separate Space / Cave 系统正在 SPACE-01 WIP**：主体实现已完成并已提交（`c05a3d2`），但存在明确的 deferred hardening，**尚未 Producer Accepted / Sealed**。

**必须明确：当前不是一个“坏掉的中间版本”。**

- 绝大部分 Continuous Outdoor 正常玩法（New Game 开局、精确地表旅行、旅行存读档、WorldSite 连续归属、FactionFlag 世界坐标放置与存读、NPC/FormalArmy 连续移动、Outdoor CharacterEncounter → 独立战场 → 战后精确回归、农田/储藏室/恢复处等 Continuous asset）已经可以正常运行，并已由制作人在 MAP-03 人工验收。
- 本轮离线验证（2026-09-19）：`XianXia.Core`（470 sources）、`XianXia.Data`（80）、`XianXia.Unity`（147）离线 C# 编译 **ALL_OK，0 error**（4 条既有 warning）。

**当前未完成主要集中在三处：**

1. **MAP-04 最终 Legacy cleanup**（Hex / WorldRegion / Outdoor LocalMap / 历史测试 fixture）；
2. **SPACE-01 的边缘 ownership / persistence hardening**（transition membership、逃离所有权、失能/尸体/落单、`PartyWorld` 语义、洞内 NPC 落点持久化）；
3. **Cave / Separate Space 的最终封板**（制作人人工验收 + 正式 seal）。

---

# 2. 本轮 Snapshot Audit 依据

本轮**不是**根据旧聊天或旧文档推断，而是同时对照：当前 git 状态 + 当前未提交/已提交代码 + 当前正式 Content + 当前 process docs。

实际读取/核对：

| 类别 | 实际核对对象 |
|---|---|
| Git | `git status --short`、`git diff --stat`、`git diff`、`git log -10 --oneline`、`git branch --show-current`、`git remote -v`、`git reflog`、`git show --stat`（`c05a3d2` / `54141d1` / `596d9c9`） |
| 正式文档 | [2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)、[41-roadmap](41-roadmap.md)、[42-devlog](42-devlog.md)、[240](240-editor-toolchain-cleanup-2026-09-15.md)、[242](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[243](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)、[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)、[245](245-map-04-physical-legacy-cleanup-2026-09-17.md)、[246](246-space-01-separate-space-interior-transition-v1-2026-09-18.md) |
| ADR | ADR-0036（地图方向）、ADR-0037（Editor 工具链／旧 Content 迁移）、ADR-0031／0032／0033／0035／0026／0023（Continuous World / SiteCore / Encounter / Squad / PlayerParty / Battle 时间） |
| 代码 | `SeparateSpaceTransitionService`、`SeparateSpaceResolver`、`SeparateSpaceExitEdgeTrigger`、`SeparateSpaceSessionSnapshotRestore`、`SeparateSpaceCombatPolicy`、`LocalMapSession`、`LoadedLocalMapPlacementSnapshotRestore`、`HostSeparateSpaceExitTrigger`、`HostCommandBridge`、`HostNpcContextMenu`、`PlayableHostBootstrap`、`HostSnapshotSessionRehydration`、`LocalMapVisibility`、`SnapshotActiveControlledLocalMapResolver`、`WorldSnapshot` |
| Content | `Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json`（解析：6 SiteRegion / 11 FactionFlag / 76 SitePlacement（含 6 controlCore）/ 21 SitePlace / 646 chunk / 24 openingEntityAnchor；cellSize 0.028、origin (-1.4,-1.4)、1900×850 Surface Cells）、`Sites/sites.json`、`Maps/ch01_cave_map.json`、`Maps/strategic_encounter_arena.json`、`LocalPlaces/ch01_cave_places.json`、`LocalPlaces/strategic_encounter_arena_places.json` |
| 工具链 | `ExternalTools/ContentAuthoring/EditorManifest.json`（当前 10 项）、`Apps/*.exe` 时间戳、`build-all.log` 末次记录 |
| 检索证据 | `findstr` 全仓库统计（见 §16）：Hex 字段名消费者 113 文件；`WorldRegion` 15 文件；LocalMap compat token 35 文件；Hex 命名文件 36 个 |
| 离线验证 | `tools/offline-compile.ps1`（Core/Data/Unity，2026-09-19，ALL_OK） |

### ⚠ 本轮审计期间发生的并发提交（重要）

审计开始时工作树有 46 项未提交改动（SPACE-01）。**审计进行中，制作人在 2026-09-19 00:32:36 从外部（非本会话）提交了 `c05a3d2`「先传一版洞府也好了」**，把当时全部 SPACE-01 代码 + 文档 + `tools/space-01-sanity.ps1` 提交并推送到 `origin/dev_openworld`。因此：

- 审计开始时的工作树快照仍完整保留在本文件 §22；
- 审计结束时工作树为 clean，HEAD = `c05a3d2`；
- **本会话没有执行任何 `git add` / `commit` / `push`**，也没有修改任何 SPACE-01 代码。

---

# 3. Milestone Status Table

| # | Milestone | Status | Producer Acceptance | Git checkpoint | Current meaning | Remaining work |
|---|---|---|---|---|---|---|
| 0 | **Editor Toolchain Cleanup** | **Accepted / Sealed** | 已验收（[240](240-editor-toolchain-cleanup-2026-09-15.md)） | 已提交 | 唯一 manifest（`EditorManifest.json`）为 Editor metadata 真源；Build All 为 staging all-or-nothing；正式输出平铺在 `Apps/<Editor>.exe` | 最后一次成功 Build All 产物**陈旧**（见 §17 问题 6）：`Apps/` 仍含已删除的 `RegionEditor.exe`／`WorldGraphEditor.exe` |
| 1 | **MAP-01 — WorldComposer / FineEditor Production V1** | **Accepted / Sealed（2026-09-16）** | 已验收（[242](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)） | 已提交 | 大陆级 macro authoring + 逐 Surface Cell 精修 + CompositionEngine/Bake + Legacy Migration Bridge + 荒村（青石荒村）迁移 | 自动水文、道路 A*、detail scatter、minor POI、terrain compatibility matrix、Runtime Chunk profiling 仍属后续范围 |
| 2 | **MAP-02 — Continuous Surface WorldMap** | **Accepted / Sealed（2026-09-16）** | 已验收（[243](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)） | 已提交 | WorldMap 由 exact `WorldPosition` 投影/反投影；Surface terrain/forest cache；固定 Header + 地图视口 + 按需 Flyout；Site/Flag marker 世界空间缩放 | 无（该 milestone 范围已封板）；不表示“完全去 Hex” |
| 3 | **MAP-03 — Normal Gameplay Surface Authority Cutover** | **Accepted / Sealed（2026-09-17）** | 已验收（[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)） | `b04920b`（`docs: seal MAP-03 continuous surface cutover`） | 正常玩法 authority = exact WorldPosition / SurfaceGroundNavigation / Continuous SiteCore / Actual Control；Hex 与 Outdoor LocalMap 降级为 Legacy / Derived Compatibility | MAP-04 负责物理清理；MAP-03 本身不删除兼容实现 |
| 4 | **MAP-04 — Physical Legacy Cleanup** | **In Progress / Paused / Not Accepted** | **未验收**（[245](245-map-04-physical-legacy-cleanup-2026-09-17.md)） | 部分已提交：`596d9c9`（第一批大清理）+ `54141d1`（第二批 + 回归修复）；**无 seal 提交** | 逐 consumer 删除 Hex / WorldRegion / Outdoor LocalMap / 旧 Editor / 旧 Content | 见 §15「Still remaining」；另有未通过的 Completion Gate（Build All 切换失败、Legacy Hex gameplay 源码面仍广、WorldMap 视觉未 Unity 验收） |
| 5 | **SPACE-01 — Separate Space / Interior Transition V1** | **Implementation Complete / Producer Acceptance Pending**（**不是 Accepted**） | **未验收 / 未封板** | `c05a3d2`（2026-09-19 00:32:36，已 push） | 统一 Separate Space 模型（Cave／Interior／Dungeon／SeparateMap）；Cave 为第一份样板；Enter/Leave Domain 收口到 `SeparateSpaceTransitionService`；in-place combat；physical exit；additive snapshot | **Deferred Final Hardening A–F**（§14）+ 制作人人工验收 checklist；封板前必须先做 hardening |

**说明：** `c05a3d2` 的提交信息「先传一版洞府也好了」表明制作人自测认为 Cave 基本好用；但**本仓库没有任何 Producer Acceptance / Seal 记录**，245 与 246 都明确写着未验收。**不要把它写成 Accepted。**

编辑工具链当前状态（`EditorManifest.json`）：Active 8 项 = `PackageBrowser`、`CharacterNpcEditor`、`ManualArtEditor`、`QuestEditor`、`EventEditor`、`WorkAreaEditor`、`WorldComposer`、`FineEditor`；Legacy Compatibility 2 项 = `LocalPlaceEditor`（仅室内/洞穴/独立地图）、`MapEditor`（仅室内/洞穴/独立战斗地图）。`WorldGraphEditor` 与 `RegionEditor` **已不存在**（项目、启动器、manifest 项均删除）。

---

# 4. 当前 Authoring 架构（MAP-01 已建立，固定术语）

## 4.1 比例与术语（**冻结，不得重新争论**）

| 术语 | 比例 / 尺寸 | 产品职责 | 非职责 |
|---|---|---|---|
| **Surface Cell** | 1×1 | **最小 gameplay terrain unit**：terrain / walkability / footprint / water / road / farm / fine edit | 不是运行块 |
| **World Editor Cell** | 10×10 Surface Cells | WorldComposer macro terrain authoring | runtime 不读取 |
| **Runtime Chunk** | 50×50 Surface Cells | **只用于 streaming / materialization / navigation cache** | **不是 authoring authority**、不是 Site 尺寸、不是行政范围、不是编辑格 |
| **WorldSite Blueprint** | 任意整数 Surface Cell 尺寸 | 固定 Site 的精修布局与 bake 输入 | 不是 runtime scene / map piece |
| **Detail Patch** | 任意 Surface Cell 尺寸 | 局部覆盖与手工修饰 | 不是 chunk |
| **WorldComposer** | — | **whole-continent authoring**（大陆级 macro） | 不做逐格精修 |
| **FineEditor** | — | **1×1 Surface Cell local authoring**（Blueprint / DetailPatch） | 不绘制整片大陆 |
| **Final Continuous Surface** | 一大陆一张 | runtime 户外地理唯一真源 | 不保留 source authoring pieces |

当前主 Surface 实测：`base:surface_main_wilderness_v1`，cellSize `0.028`，origin `(-1.4, -1.4)`，**1900×850 Surface Cells**，**646 个 50×50 chunk**（38×17）。Composer 视角为 190×85 World Editor Cells。

## 4.2 Content 分层

**Base Terrain（基础地形，仅三种）：** `Plain`、`Mountain`、`Water`
**Overlay（覆盖层，仅一种）：** `Forest`
**Path（曲线 overlay）：** `River`、`Road`
**World Object（显式世界物件）：** 树、桥、岩石等（`WorldObjectPlacement`）

- **Forest 是 overlay，不是 base terrain；Forest 可通行。**
- Road、River 是带控制点、宽度/等级的 Surface 坐标曲线 overlay，**不是单独地图**。
- Authoring schema v3：基础地形只有 `Plain / Mountain / Water`，覆盖层只有 `None / Forest`；旧 v1/v2 制作源加载时迁移；未设置的 Fine Cell = `Inherit`。

**MAP-01 V1 固定合成顺序（不得随意重排）：**
`Macro Base Terrain → Forest Coverage → River（最终基础地形为 Water）→ Road → Detail Patch terrain override → WorldSite Blueprint terrain override → Composition World Object / Blueprint object overlay → Preview / Final Continuous Surface Bake`

Preview 与 Bake 必须消费**同一个** `CompositionEngine`；确定性只依赖 world seed、全局 Surface 坐标与 source intent，**不依赖随机调用顺序**。

## 4.3 Authoring Source 与 Runtime Publish 分离（硬规则）

| 类别 | 谁编辑 | 谁消费 | 内容 |
|---|---|---|---|
| **Authoring Source** | WorldComposer / FineEditor | 只有 baker | `WorldCompositionDocument`、`WorldSiteBlueprintDocument`、`DetailPatchDocument` |
| **Runtime Generated Content** | Bake 产生 | runtime loader（只读） | Final Continuous Surface、Runtime Chunk、final terrain/geography、object placements、WorldSite 位置与 content、navigation input、WorldMap LOD/cache input |

- Authoring Source **不得**继续作为正常 runtime Data 直接塞进 `Content/BaseGame/Data` 被 runtime loader 当正式 gameplay definition 加载。
- 运行时**不需要知道**某个位置来自哪个 Blueprint / Detail Patch / 旧 LocalMap。
- 当前 `base:surface_main_wilderness_v1` 仍是混合容器（chunk source + old Site bake + LocalMap bridge + fallback）；其未来等价物是**纯 bake runtime output**。

## 4.4 编辑器 UI 语言

**WorldComposer / FineEditor 的 user-facing UI 目前要求中文。**

---

# 5. WorldComposer UX 决策（**以后不得重新争论**）

- WorldComposer coarse authoring 使用 **Area Brush**（1/2/4/8/16 编辑格、连续 stroke、Fill、Eraser、框选、pan/zoom、grid）。
- **River / Road 使用 path / spline**（Catmull-Rom 控制点；河流保存宽度；道路保存 `Trail / SmallRoad / Road / MajorRoad` 等级；交叉口可 `Unresolved` 或标记 `Bridge / Ford`）。
- **Forest 是 overlay，不是 base terrain；Forest 可通行。**
- **Road / Bridge / tree 可作为 authored world assets**（Blueprint / Composition 显式对象 placement）。
- **Runtime Chunk 只作为 Debug overlay** 显示（Runtime Chunk ≠ authoring unit，不进任何 gameplay authority）。
- **Terrain Expansion deterministic**（边缘展开由 world seed + 全局 Surface 坐标 + 周边 source intent 决定）。
- **Authoring Source 与 Runtime Publish 分离**（见 §4.3）。
- Blueprint 支持 quarter-turn rotation 与通用对象 placement（稳定 id、kind、Content/Asset 引用、局部 Surface 坐标、footprint、rotation、metadata）；缺失/损坏链接、越界、重复 id、重叠、未解决道路河流交叉口进入 Problems。
- 地图缩放：**Editor 用 `Alt + mouse wheel` zoom**。
- **WorldMap strategic marker（Council Hall / FactionFlag / Site label）属于 world-space presentation：**
  - 地图放大 → 图标和文字**一起变大**；
  - 地图缩小 → 图标和文字**一起变小**；
  - **不是** fixed-pixel HUD marker。
- **Header / Flyout 等 UI chrome 才是 fixed screen-space。**

---

# 6. Current Continuous World Runtime 架构

## 6.1 Normal Outdoor authority（MAP-03 之后）

- **SurfaceId** + **exact WorldPosition**（Canonical WorldPosition）
- **SurfaceGroundNavigation**（换路、寻路、walkability、river 不可通行 / bridge 可通行）
- **WorldSite CoreWorldPosition**（fixed Council Hall 精确世界坐标）
- **Actual Administrative Control**（与实际领土范围分离）
- **Continuous placements**（农田、储藏室、恢复处、工作区等 Continuous asset）
- **Runtime Chunk 3×3 streaming 仍保留**（只影响表现、缓存与近场 runtime，不是行政/建筑身份/编辑边界）

## 6.2 Hex 字段的当前地位（**已降级**）

`CurrentHex`、`DestinationHex`、`FinalDestinationHex`、`HexPath`、`AnchorHex`、`PresenceHex`、`OccupiedHexes`、`StrategicAnchor`、`BattleAnchorHex`、旧 Outdoor `LocalMapId`、旧 `mapLayout`

→ MAP-03 之后全部为 **Legacy / Derived Compatibility**。它们仍存在于代码、Save 与 Content 中，但**不再是 Main Continuous Surface 正常 Gameplay 的 authority**。

> “代码里还能 grep 到 Hex”**不是**判定未完成的依据；判定依据是它是否仍决定正常 gameplay 的位置/路线/合法性。MAP-04 的剩余项见 §15。

## 6.3 独立空间（Separate Space）架构（SPACE-01）

| | Continuous Outdoor Surface | Separate Space |
|---|---|---|
| 例 | `base:surface_main_wilderness_v1` | `base:map_ch01_cave` |
| 位置权威 | Continuous `WorldPosition` + SurfaceId | `ActiveMapLayoutId` + Interior local position |
| Streaming | Runtime Chunk | 独立 MapLayout / WalkGrid / LocalPlaceSet |
| 进入后 | — | 暂时离开 Outdoor playable presentation（Domain 保留） |
| 离开后 | — | 回到进入前 **exact Surface return** |

- Domain 真源：`SeparateSpaceTransitionService`（`ExplorationService.EnterLocalMap / LeaveLocalMap` 是薄封装）。
- Session 真源：`LocalMapSession` **已收窄为 Separate Space Session**（类型名保留兼容，语义不再是“所有局部世界”）。
- 正式可见性入口：`LocalMapVisibility.IsEntityVisibleInCurrentPlayableSpace`（SeparateSpace-first）。
- `SpaceKind`：`Cave / Interior / Dungeon / SeparateMap`，由 `MapLayoutDefinition.spaceKind` + `SeparateSpaceResolver` 解析（**业务代码禁止写死 map id**）。

---

# 7. WorldSite / Territory 当前最终规则

## 7.1 Site Core

| | Permanent Site | Runtime FactionFlag Site |
|---|---|---|
| Core | fixed Council Hall（议政厅） | 可拆卸 FactionFlag |
| `CoreIsRemovable` | `false` | `true` |
| WorldMap marker | house / council-hall symbol | flag symbol |
| anchor | exact `CoreWorldPosition` | exact flag world position |

## 7.2 控制与范围

- **Actual Control 与 theoretical range 分离**：实际行政资产 / 建造 / 公库存取消费 **Actual Administrative Control**。
- **同势力范围：WorldMap union presentation**（`WorldSiteActualControlOverlayBuilder.BuildFactionUnion`）。
- **FactionFlag 被敌人打爆 → 直接摧毁**；**议政厅不可直接永久删除**，只能走既有 Site warfare / occupation 规则。
- 150×150 Surface Cells 只是**一级 SiteCore 的理论行政控制范围**，不是 Chunk / Blueprint / World Editor Cell / authoring 最小尺寸。

## 7.3 当前永久 Site（Permanent Sites）实测

来自 `Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json` → `siteRegions`（**6 个**）：

| SiteId | DisplayName | siteType | arrival (WorldX, WorldY) | ownerFactionId |
|---|---|---|---|---|
| `base:site_chengzhen` | 青石镇 | Town | (18.61955, 11.25) | — |
| `base:site_guanai` | 青石关 | Pass | (21.65064, 16.5) | `base:faction_xijin` |
| `base:site_huangcun` | 青石荒村 | Village | (6.49519, 11.25) | `base:sect_huangcun_labor` |
| `base:site_lingdi` | 灵地 | SpiritLand | (38.10512, 9.0) | `base:faction_shuofeng` |
| `base:site_linjian` | 林间 | Forest | (12.99038, 7.5) | `base:sect_huangcun_labor` |
| `base:site_zhuangyuan` | 庄园 | Village | (29.01185, 6.75) | `base:faction_nan_yan` |

- 六处均已拥有 **fixed Continuous `controlCore`**（`sitePlacements` 中 `kind=controlCore` 共 6 个）；青石镇、青石关、灵地、林间、庄院的五棵树原有连续世界中心被用作固定议政厅 Core 中心（见 [244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)）。
- 另有 `Content/BaseGame/Data/Sites/sites.json` 中的 `base:site_abandoned_cave`（废弃洞府）——它是 **Separate Space 入口 Site（洞穴）**，不是永久 controlCore Site。
- `factionFlags` 实测 **11 面**（`base:flag_player_origin`（主角据点）＋ 荒村劳工 4 ＋ 渔村 3 ＋ 西晋 3），全部 `createsWorldSite: true`、`coreLevel: 1`。
- `openingEntityAnchors` 实测 **24 条**（New Game 断言 24 anchors / 24 spawned / 24 presence）。

---

# 8. MAP-02 WorldMap 正式产品规则（已封板）

- **Continuous Surface only** 是正常产品路径；**不显示 gameplay Hex grid**。
- Surface bounds、**exact WorldPosition click**、Surface travel 目标、Actual Control、Site Core marker、FactionFlag marker、NPC Squad、角色/势力面板、坐标网格。
- **UI 结构：** `Fixed Header` + `Full Map Viewport` + `Optional Inspect Flyout`（底部支援控制条仍固定）。
- **地图 content 不允许覆盖 Header**；地图渲染统一在 `BeginGroup(mapRect)` 内使用局部 `Rect(0,0,w,h)` 投影，组结束后一次性把 marker 命中区域转屏幕坐标（不再有永久右栏与 `GUI.matrix` 平移抵消）。
- **坐标网格：** 稀疏正交线，**无 X/Y 数字标签**（按缩放取 1/2/5×10^N Surface Cell 间隔，不进存档/寻路/拾取）。
- **Site marker：** Permanent Site → house / council-hall symbol；Runtime Flag Site → flag symbol；anchor = exact `CoreWorldPosition`。
- **图标/名字：** world-space scaling with map zoom；名称从图标右侧 8 Surface Cells 起排，字号按投影后标签高度缩放。
- **WorldMap 打开时是独立 planning overlay：** 打开时关闭背包/建造/任务 UI、冻结 LocalVisible 旅行、顶层输入阻断；**WorldMap open 不移动 Player**；**closing WorldMap 才继续 AutoTravel**。
- 无 Surface 时显示「当前世界没有可用的连续世界地图。」并走 Content error，不伪造 Hex 地图。

---

# 9. MAP-03 已验收的 Normal Gameplay Authority

制作人于 2026-09-17 人工验收通过（[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)）：

- **PlayerParty Surface travel**：以精确当前位置与目的地查询 `SurfaceGroundNavigation`，不以 HexPath 作为启动条件；最终按物理距离结算；成员 Presence = `SurfaceId + WorldPosition`。
- **exact destination**：WorldMap 点选与路线目标共用 exact WorldPosition。
- **travel Save/Load**：保存精确目的地、到达半径与 Site 意图；Content shell 注册导航后按保存起点重新求路。
- **New Game opening exact anchor**：`openingSurfaceId` + 已发布 Surface Site/FactionFlag + `openingEntityAnchors`；不调用 Hex strategic bootstrap，不加载 HexWorld JSON，不激活 Outdoor WorldRegion。
- **WorldSite continuous membership**：正常 WorldSite 上下文由 continuous 行政/物理查询判定，不回落 Hex footprint。
- **FactionFlag world-space authority**：先以 `SurfaceId + WorldPosition` 验证地表与 Actual Control；`StrategicAnchor` 只做派生兼容；读档不要求保存的 AnchorQ/R 落在 Hex grid 内。
- **NPC / FormalArmy continuous movement**：后台 NPC 到连续 Site 走地表寻路 + authored Site arrival；FormalArmy/Squad Site 命令同样以精确起点与 authored arrival 求路。
- **Battle world-space anchor**：`BattleParticipantSnapshot` 增加 `HasBattleAnchorWorldPosition` / `BattleAnchorWorldX/Y` / `BattleAnchorSurfaceId`，offer 冻结时从实际接触点记录。
- **Residual world-space authority**：弥留/尸体的个人 `SurfaceId + WorldPosition` 可独立成为稳定空间 authority。
- **Outdoor Site 不切旧 Outdoor LocalMap**：正常 WorldSite 上下文不再装载 Outdoor LocalMap。
- **Interior / Cave 仍允许 separate space**（SPACE-01 承接）。
- **CharacterEncounter 正式战斗规则：**
  `Continuous Outdoor → BattleOffer → independent battle field`（这是当前 Outdoor 正式战斗规则）。

---

# 10. Battle 当前封板规则（**不要重新设计**）

**Outdoor CharacterEncounter（ADR-0023 / ADR-0033 / Phase 4 已封板）：**

- unified `BattleOffer`
- participant roster（参战名单）
- manual / auto battle
- **independent battlefield**（同源独立遭遇空间）
- **main world freeze**（StrategicClockFreeze；见 ADR-0023）
- battle participant only
- **post-battle exact return**（返回战前精确世界位置）
- report（战报）
- active-character takeover（战斗中控制权接管）
- lingering / downed / residual persistence（弥留、倒地、残留）

**FormalArmy / PlayerParty spatial owner 已支持 group authority**（战后按组写回，不散落成个人坐标）。

---

# 11. SPACE-01 完整状态

## 11.1 目的

SPACE-01 建立 **Continuous Outdoor ↔ Separate Space** 的统一切换机制，而不是继续给 Cave 打零散补丁。

**Separate Space 将来用于：** Cave、Interior（室内）、Dungeon、Secret Realm（独立秘境）、其他 isolated playable spaces。
**当前 Cave 是第一份样板**（`base:map_ch01_cave`，`spaceKind: cave`）。

## 11.2 已实现的架构（`c05a3d2`）

- **Domain 真源收口：** `SeparateSpaceTransitionService.Enter / Leave`；Host 只负责 presentation rebuild / input / registry place activation。
- **Enter 流程：** resolve entrance/map/LocalPlaceSet/spawn → validate → **capture Outdoor exact return（SurfaceId + WorldX/Y）→ 建立 SeparateSpaceSession → 移动 PlayerParty membership + deterministic formation → Host 切换 presentation**。任意中途失败：Host 回滚 PlaceSet，**不得半进洞**。
- **Leave 流程：** validate session → clear Interior places/session → **restore exact Surface return** → Host 重新激活 Surface streaming/neighborhood/camera。**禁止**返回 Hex / Site center / fake entrance center。
- **Return authority：** 只有 `ReturnSurfaceId` + `ReturnWorldX/Y` + `ReturnLocationId`（不依赖 Hex / AnchorHex / PresenceHex / Overworld MapLayout）。
- **Occupant / visibility：** Separate Space 内只认 PlayerParty occupants + Active MapLayout/LocalPlaceSet 居民；Outdoor WorldSite / Surface chunk / Background NPC 不参与当前 playable presentation（Domain 保留）。
- **Combat policy：** `SeparateSpaceCombatPolicy`。Separate Space 内为 **direct in-place combat**（当前 MapLayout）；`CharacterEncounterService.RequiresEntry` 在双方均属 active Separate Space 时返回 `false`；`HostNpcMeleeAssault` / `HostNpcContextMenu` / `HostCharacterEncounter.Request` guard 均走 in-place；`HostPlayerPartyController.TickCombatFollow` 对 occupants 自动协战。
- **Entry Party Rule：** 进入 Separate Space = 当前真正随队成员**全部自动**transition（authority：`PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty`）；**已删除 `HostLocalMapEnterPrompt` 队员选择弹窗**；Host API = `IssueEnterSeparateSpace(leader, entranceId)`，旧 `IssueEnterLocalMapWithParty` 仅 legacy wrapper（忽略 party 数组）。
- **Physical Exit Trigger：** `HostSeparateSpaceExitTrigger` + Core `SeparateSpaceExitEdgeTrigger`。只有 **Active Controlled Character** 的 View 进入 authored Exit geometry 才触发；edge-trigger 防 spawn/Load 立刻离开；无确认窗；战斗中/失能/modal 不触发。**已删除**：右键「离开」、`TryPickInteriorExitAtMouse` pick 路径、Action Menu「离开洞窟」。
- **Snapshot（additive）：** `StrategicSnapshotDto.SeparateSpace`（`SeparateSpaceSessionSnapshotDto`：IsInSeparateSpace / SpaceKind / ActiveMapLayoutId / ActiveLocalPlaceSetId / Entry / Return / ReturnSurface + WorldX/Y / OccupantIds / ActiveCharacterId / EntryReason）。
- **Presentation restore 优先级（严格）：** 1) **Active Separate Space** → `RebuildSeparateSpacePresentationAfterLoad`（禁止 Outdoor Surface 抢先） 2) Independent CharacterEncounter → Encounter restore 3) Continuous Outdoor → `ContinuousOutdoorSurfaceRuntime.RebuildAfterWorldRestore`。Separate Space restore **不重新调用 `Enter()`**。
- **旧档迁移：** 无新 DTO 但 placements/推断 mapId 指向 Cave/Dungeon/Interior/Arena 时，用 PlayerPartyTravel exact position 重建 return authority；信息不足 → 明确 `SnapshotInvalid`，**不默默送回荒村**。
- **Content 边界：** `base:loc_ref_cave`（Outdoor entrance only）／`base:loc_cave_chamber`（Interior only）／`base:map_ch01_cave`（`spaceKind: cave`）。

## 11.3 Cave 当前已经基本工作的流程（依据代码 + 制作人自测记录）

| 阶段 | 当前真实行为 |
|---|---|
| **Discovery** | Hidden Cave entrance → 附近 strange-presence hint → Survey（神识）→ reveal entrance。Reveal authority = **PlayerParty knowledge**（`KnownSites`），**不是**全球 NPC knowledge。Survey 只做 `KnownSites.Discover` + reveal visual + 轻量 toast；**不**激活 Interior places、不改 ActiveMapLayoutId、不建 Session、不 deactivate Surface、不移动 Party。 |
| **Approach** | Revealed + Far → 右键 = 只走向 entrance；Revealed + Near → 出现 Enter action。 |
| **Entry** | 当前 PlayerParty traveling members **自动一起进入**（不再有 participant checkbox 选择）。 |
| **Cave Combat** | Separate Space 内 **direct in-place combat**：不进 BattleOffer、不建 CharacterEncounter、不开第二个独立战场；current Party followers 自动 assist。 |
| **Save/Load** | Cave 内 Save/Load 已基本验证可用（Load 后留在洞内、不默默回 Outdoor）；`[SeparateSpaceRestore]` / `[SeparateSpaceRestoreInvariantFailure]` 诊断日志已就位。 |
| **Exit** | **Physical Exit Trigger**：Active Controlled Character 实际进入 Exit geometry 才触发；不是右键菜单；返回 exact Outdoor position。 |

---

# 12. SPACE-01 Final Hardening（本轮已实现 · 待人工验收）

> 主体实现 checkpoint：`c05a3d2`。Final Hardening 代码在工作树**未提交**。
> 状态仍为 **Implementation Complete / Producer Acceptance Pending**。详见 [246](246-space-01-separate-space-interior-transition-v1-2026-09-18.md)。

| 项 | 状态 |
|---|---|
| A. Enter membership fallback 删除 | **Done** — 空成员明确失败 |
| B. Leave 严格 `ShouldMemberTransitionWithParty` | **Done** |
| C. stranded／downed／corpse 不错误 teleport | **Done**（代码）；待 Unity 人工验 |
| D. `PartyWorldPresenceMode.InSeparateSpace` | **Done**（additive=5；0–4 不变） |
| E. active-map 全部 Character local placement | **Done**；restore 不污染 membership |
| F. Producer Accepted／Sealed | **Pending** |

MAP-04 继续 **Paused / Producer Acceptance Pending**。

---

# 13. MAP-04 当前真实做到哪里

> **MAP-04 仍然 In Progress（Paused）。不要写完成，不要写 Accepted。**

## 13.1 Already migrated / removed（已提交，证据为 `596d9c9` / `54141d1` / `c05a3d2` 与当前仓库实测）

**A. Runtime / Bootstrap**

- `openingSurfaceId` 成为 Scenario 唯一 opening authority；`WorldRegionBootstrap` **已删除**（独立地图改用 `InteriorLocalPlaceBootstrap`）。
- `HexStrategicSessionBootstrap`、`HexStrategicMapBootstrap`、`HexTestWorldBootstrap`、`HexWorldStressMapBuilder`、`Ch01HexPrototypeMapBuilder`、`Ch01ScenarioStrategicSetup`、`StrategicBootstrap`、`HexWorldContentLoader`（+legacy adapter）**已移出 Runtime**，只在 `Assets/Tests/EditMode/LegacyHexFixtures/` 保留（8 个 fixture 类）。
- `HostFormalArmyContinuousPresenter`、`WorldSitePresentationLayer`、`FactionFlagWorldMapPresentation`、`BattleEngagementWorldMapDebug`、`HexMapMousePick`、`HexMapViewportProjection`、`HostHexGridDrawing`、`HostHexWorldRenderer`、`HexMapEditorService`、`WorldRegionBoard`（→ `LocalPlaceBoard`）**已删除**。
- `WorldVec2` 从 `Core.World.Hex` 移入中性 `Core.World`。
- `PlayerPartyContinuousFormationResolver`、`FormalArmyContinuousFormationResolver`、`CharacterEncounterSpatialAuthorityResolver`、`ContinuousCharacterSpatialAuthorityResolver`、`ContinuousSurfaceSessionBootstrap`、`ContinuousOpeningSpawnPresenceResolver` 等 Surface 侧 authority 已就位。

**B. Content（`Content/BaseGame/Data`）**

- **已删除**：`Worlds/ch01_hex_world.json`、`Worlds/travel_mvp_hex_world_30x15.json`、`Worlds/w1c_wilderness_acceptance_surface.json`、`Regions/world_regions.json`、**35 个 Outdoor MapLayout JSON**（含 `huangcun_01`、`player_camp_map`、各 `ch01_site_*_map`、wilderness fallback maps）、**34 个 Outdoor LocalPlaceSet JSON**、`world_node_stub_*`。
- **现存 `Maps/` 只有 2 个**：`ch01_cave_map.json`（`spaceKind: cave`）、`strategic_encounter_arena.json`。
- **现存 `LocalPlaces/` 只有 2 个**：`ch01_cave_places.json`、`strategic_encounter_arena_places.json`。
- `Regions/` 目录**已不存在**。
- 遭遇战术图由 `world_node_stub` 更名为独立 arena。
- 旧内容作为 EditMode 历史 fixture 保留在 `Assets/Tests/Fixtures/LegacyWorld/`（`ch01_hex_world.json`、`ch01_reference_map.json`），**不被当前 ContentPackage 加载**。

**C. 编辑器 / 工具链（ExternalTools）**

- `WorldGraphEditor`、`RegionEditor` **已删除**（项目、solution 项、manifest 项、启动器 `.cmd`）；`Shared/HexWorld/*`（14 个文件）与 `Shared.Tests` 的 8 个 Hex/Territory 测试**已删除**。
- `MapEditor` / `LocalPlaceEditor` 收窄为 Interior / Cave / 独立地图；`WorldComposer` 当前发布 UI 去掉“兼容模式”。
- `EditorManifest.json` 成为唯一 metadata 真源；`publish.ps1` 为 staging all-or-nothing。

**D. Host / WorldMap**

- `HostWorldMapPanel` 收敛为 Surface 地图（大幅删除 Hex renderer / grid drawing / picker / projection）；Surface-only WorldMap product 已建立。
- Outdoor opening authority、Site/FactionFlag 连续 anchor、Actual Control overlay 均已迁到 Surface。
- **部分** Hex / WorldRegion / Outdoor LocalMap fallback 已移除（Surface route 失败不再回退 Hex）。

**E. SPACE-01 附带的 legacy 删除**

- 删除 `HostLocalMapEnterPrompt`（478 行）；右键离开、Action Menu「离开洞窟」、`TryPickInteriorExitAtMouse` pick 路径均删除。

## 13.2 Still remaining（**2026-09-19 重新 grep 当前仓库的结果，不是旧聊天假设**）

| 类别 | 实测残留 | 数量 | 说明 |
|---|---|---|---|
| Hex 拓扑/表现源码 | `Assets/Scripts/Core/World/Hex/*`（HexWorld、HexCoord、HexMath、HexMetrics、HexPathfinder、HexTerrainCatalog/Presentation/Type/VisualInset、HexTravelMode、HexWorldLayout/MapRenderBounds/Scale、HexCell） | 14 文件 | 大量仍被 derived compat 消费者引用；不能一次性删 |
| Hex strategic 服务 | `ArmyHexBattleAnchorService`、`ArmyHexCommandService`、`ArmyHexLingeringArrivalService`、`ArmyHexMigrationHelper`、`ArmyHexPosition`、`ArmyHexPursuitService`、`ArmyHexTravelService`、`BattleEngagementHexDistance`、`FormalArmyHexWorldPositionResolver`、`HexActiveEnemyArmyQuery`、`HexFootprintSpatialGeometry`、`HexFootprintSpatialMapping`、`HexResidualContextQuery`、`HexRightClickResolver`、`HexStrategicLegacyGuard`、`HexStrategicRuntime`、`PlayerPartyHexPursuitService`、`PlayerPartyHexTravelService` | 19 文件（Hex 命名文件共 36 个） | `PlayerPartyHexTravelService` 的 HexPath/Footprint 旅行、Site `PresenceHex`、Flag strategic anchor 仍在 |
| Hex 字段消费者 | 命中 `CurrentHex/DestinationHex/FinalDestinationHex/AnchorHex/PresenceHex/OccupiedHexes/BattleAnchorHex/StrategicAnchor` 的文件 | **113 文件** | 多数为 derived/compat + Snapshot DTO；需逐 consumer 判定 |
| WorldRegion | 含 `WorldRegion` 的文件（`WorldRegionDefinition`、`DefinitionSchema`、`ContentPackageLoader`、`ContentReferenceValidator`、`MapLayoutDefinition/JsonLoader`、`OpeningScenarioDefinition`、`SpawnZoneApplier`、`LocalPlaceBoard`、`ContinuousMaterializePlacementSync`、`ContinuousOutdoorSurfaceRuntime`、`HostSnapshotLocalPlacementCaptureSync`、`PlayableHostBootstrap` 等） | 15 文件 | definition parser 只用于旧包输入；Content 侧 `world_regions.json` 已删 |
| Outdoor LocalMap compat | `WildernessLocalMapFallback`、`PlayerPartyLocalMapMaterializationService`、`BackgroundCharacterWildernessLocalMapMaterialization`、`BattleLocalMapResolver`、`LoadedLocalMapBelongingQuery/Explain`、`LocalMapHexDirectionProjection`、`BackgroundLoadedLocalMapArrivalDebug`、`PlayerPartyWildernessTransitionService`、`LoadedLocalMapPlacementSnapshotRestore` | 35 文件命中 compat token | 旧 Outdoor LocalMap snapshot ID 在 Host restore 时转 Surface；旧遭遇图 ID 映射到 arena |
| 旧 Content exporter | `Assets/Scripts/Data/Content/HexWorldContentExporter.cs`、`HexWorldContentDefinition.cs` | 2 文件 | 仍编入 Runtime |
| 历史测试 fixture | `Assets/Tests/EditMode/LegacyHexFixtures/`（8 类）、`Assets/Tests/Fixtures/LegacyWorld/`（2 JSON） | — | 按“不跑 Unity Test”约束未执行 |
| 旧档兼容 | `WorldSnapshot`、`StrategicSnapshotHelper`、`FormalArmySnapshotRestore`、`HostSnapshotSessionRehydration` 的 Hex 坐标/Presence/旧 LocalMapId 读取 | — | Intentional Legacy Compatibility，需先审计再删 |

**MAP-04 未通过的 Completion Gate（[245](245-map-04-physical-legacy-cleanup-2026-09-17.md)）：**

1. **Build All Apps 切换失败**：`publish.ps1` 候选 `Apps` 移入正式目录时 Windows `Access to the path ... denied`，脚本回滚成功 → 现有 `Apps/` 仍为旧 exe（见 §17 问题 6）。
2. **Legacy Hex gameplay 源码面仍广**：当前产品**不能**声称“完整 Hex Gameplay stack 已物理删除”。
3. **WorldMap 功能回归风险**：Surface-only 组件只经离线编译，未做 Unity 视觉/交互验收。
4. **旧战略可视化编辑入口**：`WorldGraphEditor` 的 `FactionManagerWindow` / `OpeningStrategicEditorWindow` 已删；当前 WorldComposer 可发布 Surface faction flag，但**尚无等价的独立可视化外交/开局战略编辑窗口**。
5. **历史 EditMode 测试**：旧 fixture 已搬迁，但按约束未运行；其语义覆盖旧 Hex，不是当前产品验收。

---

# 14. MAP-04 过程中已经发生的主要 Regression 与修复

> 记录这些是为了让未来开发者理解“为什么代码里存在某些 guard”。简短根因，不贴长代码。真源见 [245](245-map-04-physical-legacy-cleanup-2026-09-17.md) 与 [42-devlog](42-devlog.md)。

| # | 现象 | 根因 | 修复方向 |
|---|---|---|---|
| 1 | **New Game NPC 全消失** | 删除 `WorldRegionBootstrap.ApplyOpening` 后，15 名既无 `worldSiteId` 又无 `localLocationId` 的 Ch01 NPC 失去旧 `EntityLocation`；旧 place→Site 推断失效 | 改为 **anchor-first presence**：Normal Surface 开局直接以 `openingEntityAnchors`（稳定 SpawnKey/DefinitionId/SiteId/WorldX/Y）建立 presence + 独立 anchor census 断言 24/24/24 |
| 2 | **Snapshot load 后远处 NPC 泄漏/抽搐** | presentation 与 Domain spatial authority 冲突：先有 View 再回写 Domain，且 SiteArrival fallback 覆盖精确位置 | **snapshot restore 必须优先于 opening anchor**；首帧只允许 Domain→View；loaded chunk 是硬门；不一致 → `SnapshotInvalid`；加 `[SnapshotMaterializationLeak]` 诊断 |
| 3 | **FormalArmy authored deployment 三支部队堆在一起** | `assemblySite` personal presence 与 authored deployment 竞争 authority；idle `FormalArmyMemberPresenceSync` 保留旧个人位置；`SyncLegacyFromWorldMotion()` 每次把 `UsesHexStrategicPosition` 写回 `HasPosition` 覆盖 Surface 语义 | 新增 `initialSurfaceDeployment = {surfaceId, anchorSiteId, offsetCellsX/Y}`；authored Surface 部署直接建立首次 Army anchor；普通 idle tick 只保留锚点附近 near-field 个人位置；队形不写回 canonical position；间距 3 Surface Cells |
| 4 | **CharacterEncounter `LegacyUnqualifiedPosition`** | Personal space 被当成通用要求；但 FormalArmy / PlayerParty 是 **group authority** | 新增 `CharacterEncounterSpatialAuthorityResolver` / `ContinuousCharacterSpatialAuthorityResolver`；个人空间不再是通用前置 |
| 5 | **Runtime FactionFlag Site 启动 invariant 失败** | 动态 runtime Site ≠ authored Opening SiteRegion（authored 列表不含运行时新建 Site） | 启动 invariant 区分 authored 与 runtime Site，不用 authored 列表校验动态 Site |
| 6 | **SiteCore warfare 枚举异常** | `TickOccupation` 遍历 `PlayerPartyRuntime.Members` 时，位置解析再次读取 `Members`，getter 清空并重填同一投影列表 → `Collection was modified` | getter 每次返回独立快照（**PlayerParty group spatial authority** 相关） |
| 7 | **WorldMap UI 回归** | 曾出现 Continuous Surface 透出、被规划 overlay 覆盖、Header 被地图内容覆盖、flyout 改变视口、点击优先级错乱 | 恢复不透明全屏底色与视口底色；恢复顶层输入阻断；打开时关闭背包/建造/任务 UI 并冻结 LocalVisible 旅行；固定 Header + Full viewport + 按需 flyout；战略面板点击优先级；真实 checkbox UI |
| 8 | **Cave transition 回归** | Survey／reveal 生命周期混乱（成功后 `RefreshMapStampsOnly` 触发旧 MapLayout 全图刷新导致伪切场景）；presentation 未分离；Save/Load 后回 Outdoor；右键离开语义不明 | Survey 只 reveal + 轻量 toast；`LocalMapVisibility` SeparateSpace-first；presentation restore 优先级；`SeparateSpaceSessionSnapshotRestore`；**physical exit trigger** 取代右键/菜单 |

---

# 15. Current Known Issues / Deferred Work

> 只列有证据的问题。每项含 Severity / Current behavior / Desired behavior / Likely files-services / Recommended fix direction。

## Issue 1 — SPACE-01 Final Hardening deferred

- **Severity:** High（SPACE-01 封板前置）
- **Current:** §12 的 A～F 全部仍存在（membership fallback、leave 过宽、失能/尸体未验证、`AtHex` 语义复用、洞内 NPC 落点不持久化）。
- **Desired:** transition membership 严格化；逃离只迁移 `ShouldMemberTransitionWithParty`；失能/尸体/落单不被 teleport；`InSeparateSpace` 语义；洞内 resident/enemy 落点持久化。
- **Files:** `Assets/Scripts/Core/Exploration/SeparateSpaceTransitionService.cs`、`SeparateSpaceSessionSnapshotRestore.cs`、`LoadedLocalMapPlacementSnapshotRestore.cs`、`Assets/Scripts/Core/World/PartyWorldPresenceMode.cs`、`Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs`、`HostSnapshotSessionRehydration.cs`
- **Direction:** 先写清楚四态目标行为（对照 ADR-0019 / CW-02），再改 Core，不改 Host 行为契约。

## Issue 2 — MAP-04 final physical cleanup pending

- **Severity:** Medium-High（技术债，非当前玩法阻断）
- **Current:** §13.2 的残留仍全部存在（Hex 113 文件命中、19 个 Hex strategic 服务、15 文件 `WorldRegion`、35 文件 LocalMap compat、2 个 HexWorld 导出器）。
- **Desired:** 逐 consumer 审计后删除，或明确标注为 intentional legacy。
- **Files:** 见 §13.2 表格。
- **Direction:** 先做 consumer audit 清单（每个文件 → 谁在 normal gameplay 调用 → 是否可删/需迁移），再分批删除；**不要**在 SPACE-01 未封板时激进删除地图 runtime。

## Issue 3 — Old-save migration coverage 尚需最终验证

- **Severity:** Medium
- **Current:** 旧档迁移路径已实现（旧 Outdoor LocalMap ID → Surface；旧 Hex 人物/军队位置 → Surface 世界坐标；旧遭遇图 ID → arena；SPACE-01 legacy → Separate Space），但**没有 Unity 人工验收记录**。
- **Desired:** 至少覆盖：旧 Outdoor LocalMap、纯 Hex 人物位置、军队旅行、遭遇战术图 ID、Cave 内旧档。
- **Files:** `HostSnapshotSessionRehydration.cs`、`StrategicSnapshotHelper.cs`、`FormalArmySnapshotRestore.cs`、`SeparateSpaceSessionSnapshotRestore.TryMigrateLegacySeparateSpace`
- **Direction:** 制作人用真实旧档人工验收；信息不足时继续保持 `SnapshotInvalid` 明确失败，不做 silent teleport。

## Issue 4 — Remaining Legacy Hex consumer

- **Severity:** Medium
- **Current:** 113 个文件仍命中 Hex 字段名；其中 `PlayerPartyHexTravelService`、`PlayerPartyHexPursuitService`、`ArmyHex*`（7 个）、`HexStrategicRuntime`、`BattleEngagementHexDistance`、`HexRightClickResolver`、`HexResidualContextQuery`、`HexStrategicLegacyGuard`、`HexFootprintSpatial*`、`LocalMapHexDirectionProjection`、`FormalArmyHexWorldPositionResolver`、`HexActiveEnemyArmyQuery` 仍在 Runtime。
- **Desired:** 正常 gameplay 不依赖任何 Hex authority；Hex 只留在明确的 legacy/derived 边界。
- **Files:** 见 §13.2。
- **Direction:** 以 `HexStrategicLegacyGuard` / `HexWorld.HasGrid` 为入口做 consumer 分类，不做全局正则删除。

## Issue 5 — Remaining Outdoor LocalMap consumer

- **Severity:** Medium
- **Current:** `WildernessLocalMapFallback`、`PlayerPartyLocalMapMaterializationService`、`BackgroundCharacterWildernessLocalMapMaterialization`、`BattleLocalMapResolver`、`LoadedLocalMapBelonging*`、`PlayerPartyWildernessTransitionService` 等仍在（35 文件命中 compat token）。
- **Desired:** Outdoor 不再有 LocalMap authority；LocalMap 只剩 Separate Space（Cave/Interior/Dungeon/Arena）语义。
- **Files:** 同上 + `Assets/Scripts/Unity/Host/LocalMapVisibility.cs`
- **Direction:** 区分 “Outdoor LocalMap legacy” 与 “Separate Space（正当使用者）”；后者保留，前者清。

## Issue 6 — `Apps/` 生成产物陈旧（Build All 未在删除 Editor 后成功重跑）

- **Severity:** Medium（工具链/交付，非 gameplay）
- **Current（实测）:** `ExternalTools/ContentAuthoring/Apps/` 仍含 **12 个 exe**，包括已从仓库删除的 `RegionEditor.exe`（2026-09-16 02:13:04）与 `WorldGraphEditor.exe`（2026-09-16 02:13:10）；`build-all.log` 末次成功记录为 **2026-09-16 02:13:10 exit 0**。245 记录了 09-17 的一次 `publish.ps1` 尝试：10 项 publish 成功，但把候选 `Apps` 移入正式目录时 Windows `Access to the path ... denied`，脚本回滚，最终 exit 1。
- **Desired:** 关闭占用 `Apps`/staging 的外部进程后重跑 Build All，得到与当前 manifest（10 项，无 RegionEditor/WorldGraphEditor）一致的扁平产物。
- **Files:** `ExternalTools/ContentAuthoring/publish.ps1`、`EditorManifest.json`、`Apps/`（gitignored 生成产物）
- **Direction:** 制作人允许后重跑一次 Build All；`Apps/` 不是版本真源，不因陈旧而 commit。

## Issue 7 — Cave 内非 occupant NPC 落点不持久

- **Severity:** Medium（见 §12 E）
- **Current:** `LoadedLocalMapPlacementSnapshotRestore.Capture()` 只保存 active map 的 **occupants**；洞内居民/敌人移动后不落点。
- **Desired:** active Separate Space 内全部真实 entity 的 local position 随 Save/Load 保留。
- **Files:** `Assets/Scripts/Core/Persistence/LoadedLocalMapPlacementSnapshotRestore.cs`
- **Direction:** 扩展 capture 范围为 active map 的全部可见 entity；注意与 `[SeparateSpaceRestoreInvariantFailure]` 的 saved placement 校验语义保持一致。

## Issue 8 — `PartyWorldPresenceMode.AtHex` 语义复用

- **Severity:** Low-Medium
- **Current:** Separate Space 激活时把 `PartyWorld.Mode` 写成 `AtHex`（5 处），使独立空间与 Hex 兼容语义混淆。
- **Desired:** 新增 `InSeparateSpace`（或等价中性值）。
- **Files:** `Assets/Scripts/Core/World/PartyWorldPresenceMode.cs`、`SeparateSpaceTransitionService.cs`、`SeparateSpaceSessionSnapshotRestore.cs`、`PlayableHostBootstrap.cs`、`HostSnapshotSessionRehydration.cs`、`SnapshotActiveControlledLocalMapResolver.cs`
- **Direction:** 加枚举值 + 迁移所有写入点 + 确认 Snapshot 序列化兼容（`Mode` 是整型）。

## Issue 9 — 两处恒等/死分支（代码卫生，不改行为）

- **Severity:** Low
- **Current（实测）:** ① `SeparateSpaceTransitionService.Leave()` 末行 `return continuousReturn ? Result.Success() : Result.Success();`；② `HostNpcContextMenu` 的 LocalCombat 分支内 `if (IsActiveStrategicCombatTarget(...)) return false; return false;`。
- **Desired:** 语义清晰（若真需区分则返回不同结果；否则删除冗余分支）。
- **Files:** 上述两文件。
- **Direction:** 纯清理；**本轮不做**。

## Issue 10 — 未验证项（不得当作已验证）

- **Severity:** Informational
- **Current:** 本轮**没有**打开 Unity、没有跑 PlayMode / Unity Test Runner / batchmode。只有离线编译（Core/Data/Unity `ALL_OK`）。SPACE-01 的 Unity 端表现（视觉、输入遮挡、真实 Save/Load 落点、physical exit 手感、失能边界）**没有**本轮证据。
- **Desired:** 由制作人人工验收。
- **Direction:** 按 §16 的 checklist 执行。

---

# 16. Do Not Regress

> 未来任何重构**必须保持**下列行为。违反即视为回归。

1. **WorldMap 不显示 gameplay Hex grid。**
2. **WorldMap 打开时是独立 planning overlay**（地图内容不覆盖 Header）。
3. **WorldMap open 不移动 Player。**
4. **closing WorldMap 才继续 AutoTravel。**
5. **River unwalkable / Bridge walkable。**
6. **Outdoor Site remains same Continuous Surface**（不切旧 Outdoor LocalMap）。
7. **Cave / Interior alone can be separate space。**
8. **Outdoor CharacterEncounter uses independent battle**（BattleOffer → 独立战场 → 战后精确回归）。
9. **Separate Space combat is in-place**（不建 BattleOffer / 第二战场）。
10. **PlayerParty current members auto-enter Separate Space**（无队员选择弹窗）。
11. **Site Core exact world position。**
12. **FactionFlag exact world position。**
13. **Battle returns exact world position。**
14. **runtime chunk != authoring unit**（50×50 chunk 只是 streaming 分区；authoring 是 World Editor Cell / Surface Cell）。
15. **no Hex authority resurrection**（不得新增任何 Hex gameplay authority）。
16. **no Outdoor LocalMap authority resurrection。**
17. **WorldMap marker 是 world-space scaling**（图标与文字随 zoom 变大小）；Header/Flyout 才是 fixed screen-space。
18. **Forest 是 overlay、可通行；River/Road 是曲线 overlay，不是独立地图。**
19. **Authoring Source ≠ Runtime Publish**（authoring 不得直接当 runtime gameplay definition 加载）。
20. **Separate Space Load 不重新 Enter**（沿用 snapshot session/occupants/saved placements）。
21. **Separate Space presentation restore 优先于 Outdoor Surface restore。**
22. **Leave Separate Space 只能通过 physical exit trigger**（不是右键菜单/Action Menu）。
23. **Actual Control 与 theoretical range 分离**；行政资产/建造/公库消费 Actual Control。
24. **Permanent Site 的 fixed Council Hall 不可直接永久删除**；FactionFlag 被敌人打爆直接摧毁。
25. **FactionFlag 世界坐标 authority 与 Actual Control 判定不得回退 Hex anchor。**

---

# 17. Recommended Resume Order

> **不要**从 MAP-04 删除工作直接乱接。推荐顺序：

### 1. SPACE-01 Final Hardening（优先）

- transition membership strictness（去掉 `CollectTransitionMembers` 的 all-player fallback）；
- downed / corpse / stranded / detached behavior（明确四态，不被错误 teleport）；
- `PartyWorldPresenceMode.InSeparateSpace` semantic；
- 所有 active Cave Character（含 resident/enemy）local-position snapshot。

### 2. SPACE-01 Producer Acceptance / Seal

完整人工验收：**discover → reveal → enter → combat → save/load → physical exit → downed edge case**。

### 3. Return to MAP-04 Final Consumer Audit

重新从**当前代码**搜残留：Hex / WorldRegion / Outdoor LocalMap / Legacy Editor / HexWorld exporter；建立“文件 → 谁是 normal gameplay 消费者 → 可删/需迁移”的清单。

### 4. Finish MAP-04 physical deletion

按审计清单分批删除（先 Runtime 死代码，再 Content，再测试 fixture）。

### 5. Producer Acceptance（MAP-04）

包含 WorldMap Surface-only 视觉/交互人工验收 + 旧档迁移人工验收 + Build All 重跑成功。

### 6. MAP-04 seal / commit / push

**在 SPACE-01 未封板时，不要继续 aggressively 删除地图 runtime。**

---

# 18. Producer / Codex Workflow Rules

1. **每次需要实现：** 给制作人一份**唯一可复制**的 Codex 指令（一份完整指令，不要让制作人拼装）。
2. **每个实现轮：** 必须附**简短人工验收 checklist**。
3. **制作人没说验收通过前：** 不 commit / 不 push —— 除非制作人明确授权 checkpoint 提交。
4. **Codex：** 不打开 Unity；不跑大量 Unity Test；不大量自动化测试；只做 compile / static / Content validation。
5. **Unity 验收：** 由制作人人工执行。
6. **Codex 不因**「Unity Editor 不可用」**而停止实现**。
7. **没有真实 hard blocker：** 不停在 Foundation / partial wiring。
8. **如果只是讨论 / 设计：** 明确写「不需要给指令，也不需要验收」。

---

# 19. Git / Working Tree 当前状态

> 生成时间：2026-09-19 00:33～00:45（本地）

- **current branch:** `dev_openworld`
- **HEAD commit hash:** `c05a3d26651693d0346db09e1b86220c3965d757`（`先传一版洞府也好了`，author `Patoooo`，2026-09-19 00:32:36 +0800）
- **remote:** `origin` = `https://github.com/Siyuan-Yu/ProjectCultiva.git`
- **last pushed checkpoint:** 本会话**未执行 `git fetch`**；按本地 remote-tracking ref，`origin/dev_openworld` 与 HEAD **一致**（`git status -sb` 无 ahead/behind）→ 视为 `c05a3d2` 已推送。**下次会话建议先 `git fetch` 确认。**
- **git status summary（生成本文件前）：** **clean**（无 staged / unstaged / untracked）。
- **生成本文件后：** 预期仅新增一个未跟踪文件 `docs/40-process/247-project-handoff-current-state-2026-09-18.md`（+ 本轮可能的文档同步改动），**未 `git add` / `commit` / `push`**。
- **MAP-04 是否已 commit：** **部分**。第一批大清理 = `596d9c9`（`清理hex相关先传一个`，329 files changed, +3591/-22554），第二批 + FormalArmy / Snapshot 回归修复 = `54141d1`（`势力范围这一块基本好了……`）。**仍有 §13.2 的剩余项未做，且无 seal 提交。**
- **SPACE-01 是否已 commit：** **是**，= `c05a3d2`（46 files changed, +2093/-830），包含全部 SPACE-01 代码、新增 `docs/40-process/246-*.md` 与 `tools/space-01-sanity.ps1`，以及 `docs/00-project/03-glossary.md`／`245`／`42-devlog.md` 的更新。**由制作人在本会话审计期间提交，非本会话所为。**

## 19.1 审计开始时的工作树快照（已解析进 `c05a3d2`）

按 subsystem 分组：

- **SPACE-01 Core（新增）:** `Core/Exploration/SeparateSpaceExitEdgeTrigger.cs`、`SeparateSpaceKind.cs`、`SeparateSpaceResolver.cs`、`SeparateSpaceTransitionService.cs`、`Core/Persistence/SeparateSpaceSessionSnapshotRestore.cs`、`Core/World/Strategic/SeparateSpaceCombatPolicy.cs`
- **SPACE-01 Host（新增）:** `Unity/Host/HostSeparateSpaceExitTrigger.cs`
- **SPACE-01 Core（修改）:** `ExplorationService.cs`、`LocalMapSession.cs`、`SnapshotActiveControlledLocalMapResolver.cs`、`StrategicSnapshotHelper.cs`、`WorldSnapshot.cs`、`CharacterEncounter.cs`
- **SPACE-01 Host（修改）:** `ContinuousOutdoorSurfaceRuntime.cs`、`HostActionMenu.cs`、`HostCaveEntranceQuery.cs`、`HostCaveSurveyPresenter.cs`、`HostCharacterEncounter.cs`、`HostCommandBridge.cs`、`HostLevelTesterCheatPanel.cs`、`HostNpcContextMenu.cs`、`HostNpcMeleeAssault.cs`、`HostPlayerPartyController.cs`、`HostSnapshotSessionRehydration.cs`、`HostWorldMapPanel.cs`、`LocalMapVisibility.cs`、`PlayableHostBootstrap.cs`
- **SPACE-01 Host（删除）:** `HostLocalMapEnterPrompt.cs`（+ meta）
- **Content:** `Content/BaseGame/Data/Maps/ch01_cave_map.json`（+`spaceKind: cave`）
- **Docs:** `docs/00-project/03-glossary.md`、`docs/40-process/245-*.md`、`docs/40-process/246-*.md`（新增）、`docs/40-process/42-devlog.md`
- **Tools:** `tools/space-01-sanity.ps1`（新增）
- **其它:** `Assets/Scripts.zip`（M）、`Assets/Scripts.zip.meta`（D）

**审计开始时的完整 `git status --short`（原样保留）：**

```text
 M Assets/Scripts.zip
 D Assets/Scripts.zip.meta
 M Assets/Scripts/Core/Exploration/ExplorationService.cs
 M Assets/Scripts/Core/Exploration/LocalMapSession.cs
 M Assets/Scripts/Core/Persistence/SnapshotActiveControlledLocalMapResolver.cs
 M Assets/Scripts/Core/Persistence/StrategicSnapshotHelper.cs
 M Assets/Scripts/Core/Persistence/WorldSnapshot.cs
 M Assets/Scripts/Core/World/Strategic/CharacterEncounter.cs
 M Assets/Scripts/Data/Content/ContentPackageLoader.cs
 M Assets/Scripts/Data/Content/DefinitionSchema.cs
 M Assets/Scripts/Data/Content/MapLayoutDefinition.cs
 M Assets/Scripts/Unity/Host/ContinuousOutdoorSurfaceRuntime.cs
 M Assets/Scripts/Unity/Host/HostActionMenu.cs
 M Assets/Scripts/Unity/Host/HostCaveEntranceQuery.cs
 M Assets/Scripts/Unity/Host/HostCaveSurveyPresenter.cs
 M Assets/Scripts/Unity/Host/HostCharacterEncounter.cs
 M Assets/Scripts/Unity/Host/HostCommandBridge.cs
 M Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs
 D Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs
 D Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs.meta
 M Assets/Scripts/Unity/Host/HostNpcContextMenu.cs
 M Assets/Scripts/Unity/Host/HostNpcMeleeAssault.cs
 M Assets/Scripts/Unity/Host/HostPlayerPartyController.cs
 M Assets/Scripts/Unity/Host/HostSnapshotSessionRehydration.cs
 M Assets/Scripts/Unity/Host/HostWorldMapPanel.cs
 M Assets/Scripts/Unity/Host/LocalMapVisibility.cs
 M Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs
 M Content/BaseGame/Data/Maps/ch01_cave_map.json
 M docs/00-project/03-glossary.md
 M docs/40-process/245-map-04-physical-legacy-cleanup-2026-09-17.md
 M docs/40-process/42-devlog.md
?? Assets/Scripts/Core/Exploration/SeparateSpaceExitEdgeTrigger.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceExitEdgeTrigger.cs.meta
?? Assets/Scripts/Core/Exploration/SeparateSpaceKind.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceKind.cs.meta
?? Assets/Scripts/Core/Exploration/SeparateSpaceResolver.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceResolver.cs.meta
?? Assets/Scripts/Core/Exploration/SeparateSpaceTransitionService.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceTransitionService.cs.meta
?? Assets/Scripts/Core/Persistence/SeparateSpaceSessionSnapshotRestore.cs
?? Assets/Scripts/Core/Persistence/SeparateSpaceSessionSnapshotRestore.cs.meta
?? Assets/Scripts/Core/World/Strategic/SeparateSpaceCombatPolicy.cs
?? Assets/Scripts/Core/World/Strategic/SeparateSpaceCombatPolicy.cs.meta
?? Assets/Scripts/Unity/Host/HostSeparateSpaceExitTrigger.cs
?? Assets/Scripts/Unity/Host/HostSeparateSpaceExitTrigger.cs.meta
?? docs/40-process/246-space-01-separate-space-interior-transition-v1-2026-09-18.md
?? tools/space-01-sanity.ps1
```

**审计开始时的 `git diff --stat`：**

```text
 Assets/Scripts.zip                                 | Bin 1862803 -> 1874506 bytes
 Assets/Scripts.zip.meta                            |   7 -
 .../Scripts/Core/Exploration/ExplorationService.cs | 172 +-------
 Assets/Scripts/Core/Exploration/LocalMapSession.cs | 101 ++++-
 .../SnapshotActiveControlledLocalMapResolver.cs    |  30 ++
 .../Core/Persistence/StrategicSnapshotHelper.cs    |   4 +
 Assets/Scripts/Core/Persistence/WorldSnapshot.cs   |  23 +
 Assets/Scripts/Core/World/Strategic/CharacterEncounter.cs |   4 +
 .../Scripts/Data/Content/ContentPackageLoader.cs    |   3 +-
 Assets/Scripts/Data/Content/DefinitionSchema.cs    |   2 +-
 Assets/Scripts/Data/Content/MapLayoutDefinition.cs |   4 +
 .../Unity/Host/ContinuousOutdoorSurfaceRuntime.cs  |   3 +
 Assets/Scripts/Unity/Host/HostActionMenu.cs        |   6 -
 Assets/Scripts/Unity/Host/HostCaveEntranceQuery.cs |  30 +-
 .../Scripts/Unity/Host/HostCaveSurveyPresenter.cs  |  13 +-
 Assets/Scripts/Unity/Host/HostCharacterEncounter.cs |  21 +-
 Assets/Scripts/Unity/Host/HostCommandBridge.cs     | 109 ++--
 Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs |  17 +
 Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs | 478 ---------------------
 Assets/Scripts/Unity/Host/HostNpcContextMenu.cs    | 114 ++---
 Assets/Scripts/Unity/Host/HostNpcMeleeAssault.cs   |  20 +-
 .../Scripts/Unity/Host/HostPlayerPartyController.cs |   6 +
 Assets/Scripts/Unity/Host/HostSnapshotSessionRehydration.cs |  40 +-
 Assets/Scripts/Unity/Host/HostWorldMapPanel.cs     |   8 +
 Assets/Scripts/Unity/Host/LocalMapVisibility.cs    |  61 +++
 Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs | 215 ++++++++-
 Content/BaseGame/Data/Maps/ch01_cave_map.json      |   1 +
 docs/00-project/03-glossary.md                     |   6 +-
 ...45-map-04-physical-legacy-cleanup-2026-09-17.md |   5 +-
 docs/40-process/42-devlog.md                       |  22 +
 31 files changed, 696 insertions(+), 840 deletions(-)
```

## 19.2 最近提交历史（`git log -10 --oneline`）

```text
c05a3d2 先传一版洞府也好了                       ← SPACE-01（2026-09-19 00:32:36）
54141d1 势力范围这一块基本好了，现在有洞府还没有做，只做到一半没额度了，之后再来。  ← MAP-04 第二批 + 回归修复
596d9c9 清理hex相关先传一个                       ← MAP-04 第一批大清理（pull --ff）
b04920b docs: seal MAP-03 continuous surface cutover
37de675 删压缩包
e01af91 很好
e133d57 mp-03 暂时版
5bf6734 feat: 完成连续世界制作与世界地图收口
7c29454 重置方向基本正常
9a4a428 编辑器 中途先传一版
```

**提交信息风格说明：** 本项目提交信息常为制作人中文口语短句，不代表 milestone 状态。**milestone 状态以 process docs 的 Producer Acceptance 记录为准。**

---

# 20. 同步的 Current Docs

本轮（外加 SPACE-01 已提交的那一轮）对 current docs 的最小同步：

| 文档 | 同步内容 |
|---|---|
| **本文件 247**（新增） | Project Handoff / Resume Snapshot —— 新会话入口 |
| **[245 MAP-04](245-map-04-physical-legacy-cleanup-2026-09-17.md)** | 顶部状态改为 `Paused / Producer Acceptance Pending` + 暂停原因（SPACE-01 dependency）+ 「勿写成 MAP-04 Accepted」；本轮追加真实 checkpoint 与剩余项 |
| **[246 SPACE-01](246-space-01-separate-space-interior-transition-v1-2026-09-18.md)** | 追加当前基本通过的功能 + **Deferred Final Hardening A～F** + 提交 checkpoint（`c05a3d2`）/ 未验收状态 |
| **[41-roadmap](41-roadmap.md)** | 顶部状态改为 `SPACE-01 final hardening pending` / `MAP-04 paused / resume after SPACE-01` |
| **[2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)** | 只做简短 **Implementation Status** 更新（不大改系统正文），指向本文件 |
| **[42-devlog](42-devlog.md)** | 追加本轮 Documentation / Handoff 条目 |

**未修改历史 acceptance 文档：** [240](240-editor-toolchain-cleanup-2026-09-15.md)、[242](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[243](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)、[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md) —— 已 Accepted / Sealed，**不得重写**，只被本文件引用。

---

# 21. Copy-Paste Context for a New ChatGPT Session

> 以下整段可直接复制给一个**没有本仓库聊天记录**的新会话。（实测长度：3791 字符（含换行与英文技术词/文件名），其中汉字 822 个。）

```text
项目：Unity 2D top-down 仙侠 RPG（修仙世界模拟 + RPG + 战略层）。仓库 D:\UnityProjects\XianXia，分支 dev_openworld，最新提交 c05a3d2。请把它当成“已实现大量内容、正在收口”的项目，不要重新设计架构。

【架构（已冻结）】
- Continuous Surface 已是正常 Outdoor 世界：一大陆一张 Final Continuous Surface；正常户外以 SurfaceId + exact WorldPosition +
  SurfaceGroundNavigation + Continuous SiteCore + Actual Administrative Control 为 authority；普通户外没有 LocalMap 切换。
- Hex 与 Outdoor LocalMap 正在退役（CurrentHex/DestinationHex/AnchorHex/PresenceHex/OccupiedHexes/BattleAnchorHex/
  StrategicAnchor/旧 mapLayout 均为 Legacy or Derived Compatibility）。还能 grep 到 Hex 不等于没做完。
- 比例术语固定：Surface Cell = 1×1 gameplay 地形最小单位；World Editor Cell = 10×10 Surface Cells（Composer 大陆级 macro
  authoring）；Runtime Chunk = 50×50，只是 streaming 分区，不是 authoring 单位；Blueprint / Detail Patch 尺寸任意。
- 地形只有 Plain/Mountain/Water；Forest 是 overlay 且可通行；River/Road 是曲线 overlay，不是独立地图。
- Authoring Source（WorldComposer/FineEditor，UI 要求中文）与 Runtime Bake 产物必须分离。
- WorldMap 是同一 Surface 的战略 LOD：exact WorldPosition 投影与点选、不显示 gameplay Hex grid、固定 Header + 全屏视口 +
  按需 Flyout、地图不得盖住 Header、稀疏坐标网格无 X/Y 数字。Site marker：永久 Site 画房屋、FactionFlag 画旗，anchor 是
  exact CoreWorldPosition；图标与文字随 zoom 缩放（world-space），只有 Header/Flyout 是屏幕空间。打开 WorldMap 是独立
  planning overlay：不移动玩家，关闭后才继续 AutoTravel。
- 独立空间：Outdoor ↔ Separate Space（Cave/Interior/Dungeon/秘境）。Domain 真源 SeparateSpaceTransitionService；authority
  是 ActiveMapLayoutId + Interior local position；离开必须回到进入前 exact Surface return（禁止回 Hex 或 Site 中心）。独立空间内
  是原地直接战斗（不进 BattleOffer、不开第二战场）；Outdoor 的 CharacterEncounter 才是 BattleOffer → 独立战场 → 战后精确位置回归。
  进洞时当前随队成员全部自动进入（已无队员选择窗）；离开只能靠物理出口触发（主控走进出口几何），不是右键菜单。

【已验收封板】Editor Toolchain Cleanup；MAP-01 WorldComposer/FineEditor（09-16）；MAP-02 Surface WorldMap（09-16）；
MAP-03 正常玩法 Surface authority cutover（09-17）。

【当前 WIP，未验收】
- SPACE-01 Separate Space V1：已实现并提交（c05a3d2），Cave 是样板。已基本可用：隐蔽洞口 + 近处气息提示 → 神识 Survey 只揭示
  （不激活内景）→ 远处右键只走近、近处才出进入 → 随队成员自动进入 → 洞内原地战斗（随从自动协战）→ 洞内存读档留在洞内 →
  物理出口回到 exact 户外位置。状态：Implementation Complete / Producer Acceptance Pending，**不是 Accepted**。
- MAP-04 物理删除旧地图栈：In Progress 且 Paused（等 SPACE-01）。已删旧 WorldGraphEditor/RegionEditor、Shared/HexWorld、
  35 个 Outdoor MapLayout、34 个 Outdoor LocalPlaceSet、world_regions.json、HexWorld JSON、旧 Hex WorldMap 渲染/拾取/投影、
  WorldRegionBootstrap 等；仍剩约 36 个 Hex 命名文件、113 个文件仍引用 Hex 字段、15 个文件仍含 WorldRegion、
  35 个文件仍含 Outdoor LocalMap 兼容代码、2 个 HexWorld 导出器、历史 EditMode Hex fixtures。

【SPACE-01 尚未完成（封板前必须做；动手前先向制作人确认）】
A) CollectTransitionMembers 在收集为空时会 fallback 抓所有 Player characters，应删除该 fallback；
B) 撤离应严格只迁移 PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty 为真的成员；
C) 主控出洞时失能/尸体/脱队/落单队员行为未验证，必须确保不被错误传送；
D) Separate Space 仍复用 PartyWorldPresenceMode.AtHex，未来加 InSeparateSpace；
E) 洞内非 occupant 的 NPC（居民/敌人）移动后的 Local position 读档后不保留，需补；
F) 以上完成前不得封板。

【工作流纪律】Unity 由制作人自己打开并人工验收；开发侧不打开 Unity、不跑 Unity Test Runner/batchmode、不跑大量自动化测试，
只做离线编译（tools/offline-compile.ps1）、静态检查、Content 校验、git diff --check。每轮给制作人一份可复制的唯一指令 +
简短验收 checklist；制作人确认验收前不要 commit/push，除非明确授权 checkpoint。不要把 commit 里的口语短句当 milestone 状态，
状态以 docs/40-process 的 Producer Acceptance 为准。实质改动更新 docs/40-process/42-devlog.md，重大决定写 ADR，术语走
docs/00-project/03-glossary.md；不要修改已封板的历史文档（240/242/243/244）。Core/Data 禁止 UnityEngine；随机用 IRandomSource；
WorldTick 是唯一世界时间轴；RelationshipLedger 是唯一关系真源；Dead ≠ Removed。

【建议的下一步】1) 先做 SPACE-01 final hardening（A~E）→ 制作人完整验收 discover/reveal/enter/combat/save-load/physical exit/
downed → 封板；2) 再回 MAP-04 从当前代码重做 consumer audit，逐文件判定后分批删除 → 验收 → seal；3) SPACE-01 未封板前
不要激进删除地图 runtime。

【入口文档】交接 docs/40-process/247-project-handoff-current-state-2026-09-18.md；SPACE-01 = 246；MAP-04 = 245；
系统真源 = docs/20-systems/2N-continuous-surface-world-authoring-and-composition.md；决策 ADR-0036 / ADR-0037；
路线图 docs/40-process/41-roadmap.md；日志 docs/40-process/42-devlog.md
```

---

# 22. 本轮未做实现修改的确认

- 本轮**没有**修改任何 Gameplay 逻辑、Content 逻辑、ExternalTools 功能。
- 本轮**没有**打开 Unity、没有运行 PlayMode / Unity Test Runner / batchmode。
- 本轮**没有**继续实现任何 TODO，没有修 SPACE-01 deferred hardening、没有做 MAP-04 Hex cleanup、没有删除 Editor。
- 本轮**没有**执行 `git add` / `git commit` / `git push`。
- 本轮实际执行的验证仅为：只读 git 状态查询、文档阅读、Content JSON 解析核对、`findstr` 静态检索、`tools/offline-compile.ps1`（Core/Data/Unity 离线编译 → `ALL_OK`）。
- 工作树中出现的 `c05a3d2` 提交由**制作人从外部提交**，不是本会话所为（见 §2 并发提交说明）。
