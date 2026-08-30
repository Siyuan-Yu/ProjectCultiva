# 160 — Hex WorldSite 准入/人口、Ch01 全 Site LocalMap 与 Manual Battle 可见性（2026-08-24）

> **⚠️ 2026-08-30 · 进入 WorldSite 的空间模型部分被 [ADR-0027](../../40-process/43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md) SUPERSEDED（ingress 按来向 footprint 格 + Spatial Mapping；不再无条件 Anchor）。本页准入/人口链路结论保持。**

> **飞书同步：** 本轮 **未** 同步（制作人要求先本地 commit + 文档）。

## 背景

在 159 轮（Encounter Lingering / 参战名单 / 路线预览）入库后，Hex 战略层仍有三类阻塞验收的问题：

1. **Hex 敌军误判**：`HexActiveEnemyArmyQuery` 在 Pure Hex 下仍用 `stack.NodeId → Legacy Node` fallback，导致山匪在 (82,56) 被误判为青石荒村 (80,52) 的敌军。
2. **WorldSite 无法进入 / 进入后人口缺失**：右键菜单与 `WorldTravelService` 仍偏 Legacy Node；Presentation 层把 `FocusFormalArmyId` 当作人口白名单，且 `PartyWorld.NodeId == ""` 时 `LocalMapVisibility` 误杀所有 WorldPresence 角色。
3. **Manual Battle 我方 Members 不出现**：`PlanManualEncounter` 主路径未调用 `MarkPartyInEncounter`；遭遇图可见性在 Hex/WorldSite 下同样误杀 `AtNode` 参战者。

本轮一次性修复上述链路，并为 Ch01 全部 WorldSite 补全独立 LocalMap 内容。

## 交付摘要

### 1. Hex 敌军查询（Pure Hex 真源）

- **HexActiveEnemyArmyQuery**：Pure Hex 下禁止 Node fallback；`TryResolveStackOccupyingHex` 只认 `FormalArmy.CurrentHex` 与 Hex 足迹。
- **HexRightClickResolver**：WorldSite 右键「进入地点」走 `StrategicWorldSiteAccessService`。
- EditMode：**HexActiveEnemyArmyQueryTests**。

### 2. WorldSite LocalMap 准入

- **StrategicWorldSiteAccessService**：真源 = `WorldSite.LocalMapId` + 我方 FormalArmy 在 Site 足迹内；移动中 / 遭遇中锁定。
- **WorldTravelService.EnterWorldSiteScene**：进入前 `ClearSiteFocus()`，写入 `PartyWorld.SiteId`。
- **HostWorldMapPanel**：Hex 右键菜单绑定准入校验与菜单文案。
- EditMode：**WorldSiteEntryTests**（SITE-ENTER-01~07）。

### 3. WorldSite LocalMap 人口

- **StrategicWorldSitePopulationService**：按 Site 物理在场解析 Character（Resident + 足迹内 Army 成员）；与 EnteringArmy / Focus 分离。
- **PlayableHostBootstrap.ApplyPartyWorldNodePresentation**：WorldSite 场景用 Population 服务刷 Actor，不再用 `FocusFormalArmyId` 作人口白名单。
- **LocalMapVisibility**：`PartyWorld.NodeId == ""` 且处于 WorldSite 时，按 Site 人口规则可见；`FocusFormalArmyId` 仅作控制焦点。
- **PartyWorldPresence.ClearSiteFocus**：Manual Battle / 离开 Site 时清焦点，避免污染遭遇图。
- EditMode：**WorldSiteLocalMapPopulationTests**。

### 4. Ch01 全 Site LocalMap 内容

- 为 **28 个** WorldSite 新建独立 `ch01_site_*_map.json` + `ch01_site_*_places.json`。
- **ch01_hex_world.json**：全部 Site 写入 `localMapId`（青石荒村仍用 `base:map_ch01_reference`）。
- 每张 Prototype 图含 **1 棵 treeM/treeL** 作确定性视觉标记，便于手操区分地点。
- EditMode：**Ch01WorldSiteLocalMapMappingTests**（localMapId 缺失数 = 0；Loader 遇重复 MapId 拒绝注册）。

### 5. Manual Battle 我方 Members 可见性

- **StrategicEncounterSpawner**：`PlanManualEncounter` 主路径补 `MarkPartyInEncounter`。
- **LocalMapVisibility**：遭遇图 `IsEngaged` + `PresentationOverride` 可见；修复 Hex/WorldSite 下 AtNode 参战者被误杀。
- **HostStrategicInterruptPresenter**：Manual Battle 前 `ClearSiteFocus()`。
- EditMode：**EncounterAssemblyTests** 新增 **ENCOUNTER_ASSEMBLY_03**。

## 已知限制 / 未做

- 飞书 docId 映射：**本轮未更新**
- Unity EditMode 全套在 Editor 已打开时可能 batch 失败：**需关 Editor 后跑**
- Snapshot 多 Lingering save/load：**延期**（同 159）

## 手操 Smoke Test（建议）

1. NEW GAME → 青石荒村：进入 + Resident/Army 正常显示
2. 青云路 / 林间等 Site：可进入，树标记可区分不同地点
3. WorldMap 选中我方军团 → 右键 Site → 进入 / 移动中拒绝进入
4. Manual Battle：我方与敌方 Members 均出现
5. Auto Battle / WorldSite Enter / Lingering 再进：无回归

## 相关文件（核心）

| 区域 | 代表路径 |
|------|----------|
| 敌军 Query | `HexActiveEnemyArmyQuery.cs`, `HexRightClickResolver.cs` |
| Site 准入 | `StrategicWorldSiteAccessService.cs`, `WorldTravelService.cs`, `HostWorldMapPanel.cs` |
| Site 人口 | `StrategicWorldSitePopulationService.cs`, `PlayableHostBootstrap.cs`, `LocalMapVisibility.cs` |
| Manual Battle | `StrategicEncounterSpawner.cs`, `HostStrategicInterruptPresenter.cs` |
| Content | `ch01_hex_world.json`, `Content/BaseGame/Data/Maps/ch01_site_*`, `LocalPlaces/ch01_site_*` |
| Tests | `HexActiveEnemyArmyQueryTests.cs`, `WorldSiteEntryTests.cs`, `WorldSiteLocalMapPopulationTests.cs`, `Ch01WorldSiteLocalMapMappingTests.cs`, `EncounterAssemblyTests.cs` |
