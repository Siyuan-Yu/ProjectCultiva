# 162 — Pure Hex 终局审计 + Snapshot v6 JSON 修复（2026-08-24）

> **HEAD：** `ff112cd` — `fix(snapshot): complete pure-hex strategic v6 json roundtrip`  
> **前置：** [161 Purge 收束](161-pure-hex-legacy-purge-and-post-fix-rollup-2026-08-24.md)、[156 Pure Hex 替换](156-pure-hex-world-replacement-2026-08-23.md)

## 背景

在 `8a41534`（Legacy Purge + 编译/运行/编码修复）落地且手操回归正常后，执行两轮只读/修复验收：

1. **终局 Forensic Audit** — 确认 Production 战略 Legacy 依赖 = 0。
2. **Snapshot v6 JSON** — 修复 `JsonSnapshotSerializer` 与 `StrategicSnapshotHelper` 不同步，使 PlayableHost JSON Save/Load 不再丢失战略状态。

**明确未进入：** TerritoryRegion、Dynamic Bandit、命名/doc Minor Cleanup。

---

## 一、终局审计结论

### Final Verdict

**PURE HEX LEGACY PURGE: PASS WITH MINOR CLEANUP**

Pure Hex 已是唯一正式战略空间 Runtime；无 WorldNode/Route/Graph 生产依赖。

### Production Legacy References（`Assets/Scripts`）

| 符号 | 命中 |
|------|------|
| WorldNode / WorldRoute / WorldGraph | **0** |
| LegacyNodeId / AtNode / OnRoute | **0** |
| strategic NodeId / RouteId（字段） | **0** |

残留仅为 **命名**（`ResolveNodeLabel`、`WatchNodeOwnerChanges`、`IsFriendlyNodeForFormation`）与 **历史文档**，不构成 Runtime 回退。

### 真源链（摘要）

| 需求 | 真源 |
|------|------|
| Army 位置 | `FormalArmy.CurrentHex` |
| Resident 位置 | `WorldAgentPresence.SiteId`（AtSite） |
| Residual | `WorldAgentPresence.AtHex` + HexQ/R |
| Site Owner | `WorldSite.OwnerFactionId` → Snapshot `WorldSiteOwners` |
| Capture | `CaptureObjectiveService` → `WorldSiteOwnershipService.SetOwner` |
| Battle 位置 | `BattleParticipantSnapshot.BattleAnchorHexQ/R` |
| 移动路径 | `HexPathfinder` + `FormalArmy.HexPath` |

### 审计发现的生产阻塞项（已在本轮修复）

| 项 | 严重度 | 状态 |
|----|--------|------|
| `JsonSnapshotSerializer` 仅写 armies+wars，丢 v6 战略字段 | **HIGH** | **已修复 `ff112cd`** |
| `StrategicSnapshotHelper.Restore` 在 `SetHexPath` 后清零 `StepProgress` | MEDIUM | **已修复 `ff112cd`** |

### 已知限制（未在本轮处理）

- EditMode 全量仍有 Route-era 遗留失败（历史 ~185；本轮相关 35 项中 3 FAIL 与 Snapshot 无关）。
- 误导性现行文档（`SCHEMA.md` worldGraph 段、`2A`、`AGENTS.md` L45 等）— Minor Cleanup backlog。
- Load 后 Site Owner 生效需 Content 地图 bootstrap（与 `RebuildPresentationAfterLoad` 一致）；JSON 现已完整持久化 Owner 数据。

---

## 二、Snapshot v6 JSON 修复

### 根因

`StrategicSnapshotHelper.Capture` 已捕获完整 v6 DTO，但 `JsonSnapshotSerializer.SerializeStrategic/ReadStrategic` 仍停留在早期 stub（仅 `formalArmies` + `wars`，且无 `hexPath`、无 attackers/defenders）。`PlayableHostSession.CaptureJson/RestoreJson` 因此丢失大部分战略状态。

### 补齐字段

| 字段 | Serialize | Deserialize |
|------|-----------|-------------|
| formalArmies + hexPath + hex 移动状态 | ✓ | ✓ |
| armyMemberships | ✓ | ✓ |
| residualCharacterPresences | ✓ | ✓ |
| worldSiteOwners | ✓ | ✓ |
| wars（attackers/defenders） | ✓ | ✓ |
| alliances / vassalages | ✓ | ✓ |
| retreatingArmies | ✓ | ✓ |
| captureObjectives | ✓ | ✓ |

**禁止恢复：** NodeOwners、NodeId、RouteId、OnRoute、RouteProgress 等 Legacy 字段。

### Restore 附加修复

`SetHexPath()` 会重置 `StepProgress`；Restore 现于路径重建后重新应用 `StepProgress/StepRemainingTicks/StepTotalTicks/CurrentPathIndex`。

### 回归测试

新增 `StrategicSnapshotJsonV6RoundtripTests`（6 用例）：

- DTO JSON deserialize 与 Capture 一致
- WorldSite Owner / Army Membership / Hex Path / Residual / Capture+Diplomacy roundtrip

更新 `ArmyPhaseKTests` 走完整 `CaptureJson → RestoreJson` 路径。

---

## 三、Unity Test Runner 修复

`tools/run-editmode-tests.ps1`：

- 默认 `$Project` 改为脚本上级目录（`F:\ProjectCultiva\ProjectCultiva`），不再硬编码 `D:\UnityProjects\XianXia`。
- 新增 `-Filter` 参数（Unity `-testFilter`）。

---

## 四、验证状态（2026-08-24 晚）

| 项目 | 结果 |
|------|------|
| Shared.Tests | **PASS** 12/12 |
| Unity batchmode 编译 | **PASS** 0 CS |
| StrategicSnapshotJsonV6RoundtripTests | **PASS** 6/6 |
| ArmyPhaseKTests | **PASS** |
| EditMode 相关子集（35 项） | **31 PASS / 3 FAIL / 1 SKIP** |
| 手操回归（制作人） | 此前反馈正常 |

**仍 FAIL（非 Snapshot、非 Legacy Route）：**

- `PHOM_01_AtSite_SetAtSite_CollectAtSite`
- `PHOM_03_TryCompleteWorldSiteCapture_SetsSiteOwner`
- `SITE_RCLICK_05_SiteWithoutLocalMap_NoEnterMenu`

---

## 五、Git 时间线

| 提交 | 说明 |
|------|------|
| `1e89a7b` | Pure Hex ownership 落地 |
| `8a41534` | Legacy Purge + 编译/运行/编码修复 + doc 161 |
| `ff112cd` | Snapshot v6 JSON 完整 roundtrip + 测试 + runner 修复 |

---

## 六、Minor Cleanup Backlog（未执行）

- `ResolveNodeLabel` / `WatchNodeOwnerChanges` / `IsFriendlyNodeForFormation` 重命名
- `HexStrategicLegacyGuard` orphan 清理
- `HostWorldMapPanel.TryOpenResidualHexEnter` Obsolete 删除
- 现行误导文档更新（`SCHEMA.md`、`2A`、`glossary`、`AGENTS.md`）
- `Content/_backups/` legacyNodeId 备份清理
- EditMode Route-era 遗留用例批量更新

---

## 七、手操 Smoke Test（建议）

1. Level Tester 荒村：角色可见，文案正常。
2. F5 存档 → F9 读档：改过的 Site Owner、行军中军队位置、Downed Residual 仍正确。
3. 大地图：山匪名、Site 名无 `??`。

---

## 相关文件

| 区域 | 路径 |
|------|------|
| JSON Serializer | `Assets/Scripts/Data/Serialization/JsonSnapshotSerializer.cs` |
| Snapshot Restore | `Assets/Scripts/Core/Persistence/StrategicSnapshotHelper.cs` |
| 回归测试 | `Assets/Tests/EditMode/StrategicSnapshotJsonV6RoundtripTests.cs` |
| Test Runner | `tools/run-editmode-tests.ps1` |
| Host Save/Load | `Assets/Scripts/Unity/Host/PlayableHostSession.cs` |
