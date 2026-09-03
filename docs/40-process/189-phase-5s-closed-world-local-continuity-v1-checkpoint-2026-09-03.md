# 189 · Phase 5S CLOSED：真实世界战略战斗与世界／近景连续性 V1 Checkpoint（2026-09-03）

> 状态：**Checkpoint 已提交** ｜ 日期：2026-09-03
> 上级：[186 Phase 5S 真实世界战略战斗与世界／近景连续性 V1 封板](186-phase-5s-final-architecture-closure-2026-09-01.md)（2026-09-03 更新为权威封板）／[188 PlayerParty WorldSite ingress robustness](188-playerparty-worldsite-ingress-robustness-and-location-authority-consistency-2026-09-02.md)
> 本文 = 2026-09-03 未提交批（代码 + Content + 文档）的**归档 checkpoint**：把 devlog 逐条记录与 186 封板更新固化为一次提交，串联 Surface Exit / Casualty / NPC authoring 三条工作线与最终状态。

---

## 1. 本 checkpoint 是什么

自上一个已推送点（`1e8743f` 文档 187 / `e7404cc` 188 批次之后）以来，工作区积累了 29 个修改文件 + 6 个新增文件（含 3 个 `.meta`），横跨：

1. **非 Encounter FormalArmy 伤亡 → 独立残留**（`FormalArmyCasualtyService` 新增）
2. **友方 FormalArmy 参战快照与倒地残留生命周期**（移除 `EntityTag.Npc` 误排除 + `BattleParticipantSpatialGuard` 新增）
3. **WorldSite 跨面准入与可见出口契约**（access 拆分 + `SurfaceExitTraversalService` 新增）
4. **出口边身份 / 地点间事务 / 手动使用出口**（`HostSurfaceExitZonePresenter` 新增等）
5. **LocalVisible Surface Exit 的完成语义与可达性**（`SurfaceExitWalkGridReachability` 新增 + `HostMoveController` 路径完成策略）
6. **NPC 初始 LocalMap 坐标归属 Spawn Instance**（`OpeningSpawnEntry.localPosition`，删除五个伪 LocalPlace）
7. **186 Phase 5S 权威封板更新**（2026-09-03，新增 V1 运行时权威总览 + backlog）

devlog 已按主题逐条记录（见 §4）。本文档是批次的提交性归档，不重写历史细节。

---

## 2. 主题清单（devlog 条目 × 主要文件）

| # | devlog 条目（42-devlog.md） | 主要新增/修改 |
|---|---|---|
| 1 | Phase 5S CLOSED：真实世界战略战斗与世界／近景连续性 V1（待 checkpoint） | 186 封板更新 + 本文 |
| 2 | P0 修复：非 Encounter FormalArmy 伤亡转独立残留 | `FormalArmyCasualtyService.cs`(新)、`ArmyService.cs`、`StrategicEncounterResolveService.cs`、`StrategicEncounterSpawner.cs` |
| 3 | P0 修复：友方 FormalArmy 参战快照与倒地残留生命周期 | `BattleParticipantGatheringService.cs`、`BattleParticipantSpatialGuard.cs`(新)、`StrategicEncounterSpawner.cs`、`HostCombatSkillBar.cs` |
| 4 | P0 回归修复：WorldSite 跨面准入与可见出口契约 | `StrategicWorldSiteAccessService.cs`、`SurfaceExitTraversalService.cs`(新)、`WorldTravelService.cs`、`PlayableHostBootstrap.cs` |
| 5 | P0 修复：出口边身份、地点间事务与手动使用出口 | `WorldSiteFootprintExitConnectionResolver`（既有）、`HostSurfaceExitZonePresenter.cs`(新)、`PlayerPartyHexTravelService.cs`、`BackgroundCharacterSiteDepartureResolver.cs` |
| 6 | FIX：LocalVisible Surface Exit 的完成语义与可达性 | `HostMoveController.cs`、`SurfaceExitWalkGridReachability.cs`(新)、`WalkGrid.cs`、`PlayerPartyLocalVisibleAutoTravelService.cs` |
| 7 | REFACTOR：NPC 初始 LocalMap 坐标归属 Spawn Instance | `OpeningScenarioDefinition.cs`、`WorldRegionBootstrap.cs`、`ContentPackageLoader.cs`、`ch01_site_chengzhen_places.json`、`level_tester_roster.json`、`scenarios.json`、`SCHEMA.md` |

---

## 3. 关键 authority 收口（本批新增的事实）

- **FormalArmy 伤亡 handoff**：普通 Local Combat（非 strategic Encounter）中 FormalArmy non-living member 由 `FormalArmyCasualtyService` → `ArmyService.DetachNonLivingMemberAtCurrentArmyLocation` 交接为 independent `StrategicResidual`；`FormalArmyMemberPresenceSync` 写 exact CurrentHex；不清 LocalMap occupant / `PresentationOverride`。战略 manual combat 仍延迟到 `ResolveAndEnd` detach。
- **参战快照**：`FormalArmyContentBootstrap` 正式军团士兵带 `EntityTag.Npc` 但必须以 `MandatoryFriendly` + 正确 `FormalArmyId` 入 snapshot；PlayerParty 的 NPC 排除不变。
- **Surface Exit transition contract**：所有 Site/Wilderness transition = `PREPARE → COMMIT`；准入 = `CanTransitionPlayerPartyIntoWorldSite`（transition admission），不得误用 already-present access gate（`CanOpenWorldSiteLocalMapFromPresence`，旧名兼容保留）。canonical edge identity = source boundary hex + destination hex / exact shared edge；`RepresentativeSource` 仅为表现聚合。
- **NPC authoring**：`OpeningSpawnEntry.localPosition { x, z }` 是 authored instance 的精确初始近景坐标；`worldSiteId` 是宏观落点；删除"一名 NPC 一个 fake LocalLocation"（青石五个伪地点已删）。

---

## 4. 文档状态

- `docs/40-process/186-…md`：更新为 **2026-09-03 权威封板**（标题/状态行 + §3 V1 运行时权威总览 + §8 backlog + §7 静态审计补充）。
- `docs/40-process/42-devlog.md`：追加 2026-09-03 七条 + 2026-09-02 REFACTOR 一条（均随本 checkpoint 提交；条目中的"（未提交）"为记录时状态，保留历史原貌）。
- 本文 = 189 批次归档。

---

## 5. 验证（本 checkpoint 提交前复核）

- Host 全链非 Unity 编译（真实 Unity 2022.3.6f1 dll + Core + Data + 全部 Unity 脚本）：**0 错误**；2 个既有无关 warning（`HostWorldMapPanel.cs:725` CS0162、`HostFormalHud.cs:123` CS0169）。
- `git diff --check`：通过（exit 0）。
- 未运行 Unity Test Runner / PlayMode / EditMode 新用例；运行时行为留 LevelTester 人工验收（见 devlog 各条目）。

---

## 6. 非阻塞 backlog（沿用 186 §8）

1. PendingEngagement JSON save/load 完整持久化
2. Authored Character instance identity（`SpawnInstanceId` / `AuthoredCharacterInstance`，解决 `GameStartLookup` DefinitionId→EntityId 覆盖限制）
3. Surface Exit 性能：WalkGrid connectivity cache + distance-field route selection
4. 更大规模 population authoring（`AuthoredPopulationDefinition` / `PopulationSet`）

---

## 7. 提交记录

- 本 checkpoint：一次提交固化全部未提交代码 + Content + 186 封板 + devlog 七条 + 本文（189），推送到 `origin/dev`。
- 前置已推送：`1e8743f`（文档 187）、`e7404cc`（Local hostile + 188）、`062f691`（Localmap 战斗支援范围稳定）、`d209c02`（试炼弱匪避让）。
