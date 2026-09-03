# 191 · Phase 5S Persistence 收口 + Hex Territory V1 基础层 封板（2026-09-03）

> 状态：**已封板（代码完成，待 Unity 验收）** ｜ 日期：2026-09-03
> 上级：[190 Local Combat 弥留者 ownership 封板](190-local-combat-casualty-ownership-residual-handoff-and-precise-position-2026-09-03.md)／[189 Phase 5S CLOSED Checkpoint](189-phase-5s-closed-world-local-continuity-v1-checkpoint-2026-09-03.md)／[2J Hex Territory 规则](docs/20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)
> 本文 = 2026-09-03 两批未提交工作（① Phase 5S persistence 收口：PendingEngagement/BattleOffer JSON Save-Load；② Hex Territory + TerritoryRegion V1 基础层）的**封板归档**。devlog 已按两轮主题逐条记录，本文固化最终 authority 与 invariant，不重写历史细节。

---

## Part 1 — Phase 5S Persistence 收口：PendingEngagement / BattleOffer JSON Save-Load

### 1.1 背景与缺口
- `PendingEngagementSnapshotRestore.Capture/Restore` 与 `StrategicSnapshotHelper` 已接线；缺口在 `JsonSnapshotSerializer` 未序列化 `pendingEngagement`，且既有 DTO 漏了若干 **gameplay authority** 字段（尤其 `BattleParticipantSnapshot.LocalMapResolutionKind`，读档缺失会默认 `ExplicitEncounterMap`，对 WORLD_COMBAT Auto Battle 危险——Manual 入场时会重置所以看不出，Auto 的 `BindEncounterAfterAutoResolve/StrategicEncounterResolveService/LingeringBattlefieldRegistry` 会读 frozen 值）。

### 1.2 DTO 扩展（WorldSnapshot.cs）
- engagement 级：`PrimaryEnemyFactionId / PlayerInclusionReason / RequiresPlayerDecision / PendingBattleTriggerReason / InitiatorCommittedHexQ·R / DefenderCommittedHexQ·R`（committed hex 默认 `int.MinValue` = ArmyHexBattleAnchorService.InvalidHexComponent）+ `RetreatHasValue`（防把 null retreat 恢复成默认对象）。
- participant 级：`ParticipantEncounterLocalMapId / ParticipantLocalMapResolutionKind / HasParticipantLocalMapResolutionKind`（旧存档缺失与合法 `0=WorldSite` 无法区分 → 必须 flag）。
- record 级：`IncludedReason` + `HasPreBattle/PreBattleMode/SiteId/HexQ·R/FollowStackId/CombatPursuitStackId`（Frozen BattleParticipantSnapshot 不存半份）。

### 1.3 Capture / Restore（PendingEngagementSnapshotRestore.cs）
- Capture 补全部字段（含 `HasParticipantLocalMapResolutionKind=true`、retreat 仅非空写值）。
- Restore：
  - `RequiresPlayerDecision = src.RequiresPlayerDecision`（不再由 InvolvesPlayerSide 推导）。
  - `PlayerInclusionReason`：持久化非空直接用；仅旧 snapshot 才 `PlayerParty initiator + PlayerPartyIncluded → DirectInitiator` fallback。
  - frozen participant resolution：有 flag 直接还原；无 flag 用 `BattleLocalMapResolver.ResolvePendingEngagement(world)` fallback（不盲信 0）。
  - record 还原 IncludedReason + PreBattle（PreBattleHexQ/R 缺省 int.MinValue）。
  - 尾部 `offer.SetPlayerParty(participants.CollectSelectedFriendly())` —— **Participants.Selected 是 frozen selection authority**，不另存重复 roster；用户勾选 Optional 后存档回来完全一致。
  - `PrimaryPlayerFactionId` 继续从 `world.Strategic.PlayerFactionId` 恢复，不单独存。
- Development dump：Capture/Restore 后 #if 输出 `[PendingEngagementSnapshot]` 全关键字段（Save 前 Load 后直接比较）。

### 1.4 JsonSnapshotSerializer（独立 helper，不塞巨型 inline）
- `SerializePendingEngagement / ReadPendingEngagement` + hex pair `[{q,r}]` helpers（DTO 内部 Q/R 双 list 在 serializer 层转换）。
- `SerializeStrategic`：仅 `PendingEngagement.EngagementId` 非空才写 `pendingEngagement`（无 active BattleOffer 不写）。
- `ReadStrategic`：读到才设 dto（缺省 null = 无 pending battle，正常继续加载）。
- EntityId 继续走 `U()/ReadUValue()`（不 JSON double 强转 ulong）；retreat 用 `hasValue` flag。
- **不升 SchemaVersion**（v6 optional 字段；旧 v6 档无 pendingEngagement = 按无 pending battle 加载）。

### 1.5 验证与验收
- `PendingEngagementRoundTripCheck` 52 项 PASS（Capture / JSON round-trip / Restore 语义 / 旧格式 fallback / no-pending 五组）。
- 人工验收 Case A–D + No-pending regression（见 devlog）。

---

## Part 2 — Hex Territory + TerritoryRegion V1 基础层

### 2.1 规则核心（真源：2J）
```
WorldSite.TerritoryRegionId → TerritoryRegion{RegionId, PrimaryWorldSiteId, ControlFactionId, Hexes[]} → HexCell.ControlFactionId
```
永久 invariant：`WorldSite.OwnerFactionId == Region.ControlFactionId`，且 Region 内每个 `HexCell.ControlFactionId == Region.ControlFactionId`。Footprint ≠ Territory（4 Hex 城镇 footprint=4，辖区可几十 Hex）。

### 2.2 Domain（Core）
- `TerritoryRegion.cs`：固化 Hexes[]，无 Runtime radius 逻辑；Region identity 与 Controller 分离（**无主 Site 也有 Region**，以后 Capture `None→Player` 只改 controller）。
- `TerritoryRegionBoard.cs`：Register / TryGet / TryGetByPrimaryWorldSite / Clear（**禁止扫描全 Hex 猜 Region 归属**）。
- `WorldSite.TerritoryRegionId`；`StrategicBoard.TerritoryRegions` 挂 Board。
- `TerritoryControlService.cs`：`GetController(world, hex)` / `GetRegionForSite(world, siteId)` / `SetRegionController(world, regionId, factionId)`——后者改 `region.ControlFactionId` + 全部 hex；**暂不自动改 Site Owner**（避免与 Capture 循环依赖；下一轮 `TransferWorldSiteAndTerritory` 高层事务）。
- `TerritoryInvariantValidator.cs`：Site↔Region 双向绑定（TerritoryRegionId 存在且 PrimaryWorldSiteId==siteId）、Owner==Controller、Region hex 在界内/无重复/无跨 Region overlap、footprint ⊆ 自身 Region；不一致 = Content ERROR，不静默猜谁覆盖谁。

### 2.3 Content（Data + JSON）
- `HexWorldContentDefinition` + `TerritoryRegionContentDefinition{RegionId, PrimaryWorldSiteId, ControlFactionId, Hexes}`；root `TerritoryRegions`；Site 加 `TerritoryRegionId`。
- `HexWorldContentLoader.Apply` 顺序：cells → sites → territoryRegions（territory **最后**写 cell.ControlFactionId = region.controller）；加载后跑 invariant，error → Result.Failure。
- `HexWorldContentExporter` 补 regions 导出；`ContentPackageLoader`/`DefinitionSchema` 支持字段；`SCHEMA.md` 补 hexWorld/territoryRegions。
- 一次性生成器 `TestResults/territory_generate.py`：footprint 距离最近竞争 + SiteId ordinal tie-break（deterministic，禁 Random）、footprint 保护（任何 Site footprint hex 只能属于自己绑定的 Region）、有主 Site radius：footprint≥4 → 2 否则 1、无主 Site = footprint-only。
- 结果：**travel_mvp**（验收图）8 regions（huangcun 27 hex/压迫宗门、zhuangyuan 30/南堰、lingdi 7、player_camp 3；chengzhen 无主 = region 存在但空 controller=footprint-only）；**ch01 大图** 30 regions（15 有主）。涂色 ≈ 15% 地图，85% 无主荒野。

### 2.4 Persistence
- `TerritoryRegionControllerSnapshotDto{RegionId, ControlFactionId}` —— Content identity（RegionId/Hexes/PrimaryWorldSiteId）重新从 Content 建，**只存会变化的 Controller**，不把 Hex list 复制进 Save。
- StrategicSnapshotHelper Capture 全 region controller、Restore 走 `TerritoryControlService.SetRegionController`；JsonSnapshotSerializer 读写 `territoryRegionControllers`。Restore 顺序：Site Owner（既有 worldSiteOwners）先 → region controller 后；一致性由下一轮 transfer transaction 统一（本轮可分别 restore 后跑 invariant）。

### 2.5 WorldMap 表现
- `HostHexWorldRenderer`：`ControlFactionId` 非空 → `StrategicFactionCatalog.MapTint`（正式 MapColor 非 hash）淡色半透明 overlay（Lerp 0.26）；`""` → 不加 tint。不覆盖 terrain；WorldSite footprint 继续现有 Site 表现。无 border/political mode/outline。
- `HostWorldMapPanel`：Hex / WorldSite inspector 显示 `ControlFactionId / TerritoryRegion / PrimaryWorldSite / Controller`。

### 2.6 验证与验收
- `TerritoryContentCheck` 16 项 PASS；回归（WeakHex/Bootstrap/LocalCombatHandoff/PreciseResidual/PendingEngagement/HostSim3）全 PASS；三程序集 0 error。
- Unity 验收：有 Owner 村庄周边淡色 Territory / 大量无主荒野 / Multi-Hex Territory 从整个 footprint 延伸（非 anchor 圆）/ inspector 显示 territory 字段 / Save→Load 颜色与 controller 不变 / travel·战斗·LocalMap 无回归。

### 2.7 本轮绝对没做（下一阶段）
Capture Site / Attack WorldSite / Siege / Garrison / Supply / Trespassing punishment / AI 扩张 / Bandit Camp / Army 自动 Claim Hex / Territory Economy / Region merge / Contested Territory。下一轮真正事务链：`Capture WorldSite → OwnerFactionId 易主 → TerritoryRegion.ControlFactionId 易主 → Region 全部 Hex.ControlFactionId 易主 → WorldMap Territory Color 当场刷新`。

---

## 3. 主要文件
- **Part 1**：`WorldSnapshot.cs`、`PendingEngagementSnapshotRestore.cs`、`JsonSnapshotSerializer.cs`、`42-devlog.md`
- **Part 2（新增）**：`TerritoryRegion.cs`、`TerritoryRegionBoard.cs`、`TerritoryControlService.cs`、`TerritoryInvariantValidator.cs`
- **Part 2（修改）**：`WorldSite.cs`、`StrategicBoard.cs`、`HexWorldContentDefinition.cs`、`HexWorldContentLoader.cs`、`HexWorldContentExporter.cs`、`ContentPackageLoader.cs`、`DefinitionSchema.cs`、`JsonSnapshotSerializer.cs`、`HostHexWorldRenderer.cs`、`HostWorldMapPanel.cs`、`StrategicSnapshotHelper.cs`、`WorldSnapshot.cs`
- **Content**：`travel_mvp_hex_world_30x15.json`、`ch01_hex_world.json`、`SCHEMA.md`；工具 `TestResults/territory_generate.py`
- **验证 harness（TestResults/，gitignore）**：`PendingEngagementRoundTripCheck`、`TerritoryContentCheck`
