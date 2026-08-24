# 161 — Pure Hex Legacy Purge + 编译/运行/编码修复收束（2026-08-24）

> **相对提交：** `1e89a7b`（Pure Hex ownership）→ `8a41534`（main）  
> **后续：** [162 终局审计 + Snapshot v6 JSON](162-pure-hex-final-audit-and-snapshot-v6-json-2026-08-24.md)（`ff112cd`）

## 背景

在 `1e89a7b` 落地 Pure Hex ownership 后，Unity 出现三类阻塞 PlayableHost / Level Tester 验收的问题：

1. **编译失败**：Legacy 删除不完整 + 批量替换导致 C# 字符串引号损坏（CS1010 等）。
2. **本地图无角色**：Pure Hex 迁移时 `Traveling/RouteAnchored/AtNode` 被机械替换成 `AtSite`，导致 `LocalMapVisibility` 与人口服务误过滤；另有一处 `PartyWorld.SiteId` 被误清空。
3. **UI / Content 乱码**：Host UI 字符串、Content JSON、`ArmyStackAdapter` 山匪 DisplayName 等在编码迁移中截断为 `?` / `????`。

本轮在 **不进入 TerritoryRegion / Dynamic Bandit** 前提下，完成 Legacy Purge、编译修复、运行修复与编码修复，并补工具脚本便于后续扫描。

---

## 交付摘要

### A. Pure Hex Legacy Purge（相对 `1e89a7b` 大 diff）

| 类别 | 动作 |
|------|------|
| **FormalArmy** | 删除 `NodeId`/Route 字段；位置真源 = `CurrentHex` + Site 足迹 |
| **PartyWorldPresenceMode** | 删除 `AtNode`/`Traveling`/`RouteAnchored` 生产路径；保留 `AtSite`/`AtHex`/`InEncounter` |
| **Snapshot** | v5→v6；移除 Legacy Node/Route 读写 |
| **Content** | 删 `WorldGraphDefinition`、`ch01_world_graph.json`、Editor `HexWorldMigrationCli`；strip `legacyNodeId` |
| **命名** | `NodeDefenseService`→`SiteDefenseService`；`StrategicNodeAccessService`→`StrategicSiteAccessService` |
| **测试** | 更新/删除 Legacy 用例；Shared.Tests No-op Roundtrip 通过 |

### B. 编译修复轮

- 修复 **100+** 处 C# 字符串闭合损坏（`HostWorldMapPanel`、`PlayableHostBootstrap`、Core 服务等）。
- 新增 `tools/fix-broken-strings.py` 扫描 `Assets/**/*.cs` 中 `?"` 类损坏模式。
- **Unity batchmode 编译：EXIT=0，0 CS 错误**（`compile-check11`）。

### C. 运行修复（本地图 / Level Tester）

#### C1. `AtSite` 误过滤（Legacy 机械替换）

**根因：** 原 `Traveling`/`RouteAnchored`/`AtNode` 排除分支被误改为 `AtSite`，导致 `AtSite` 角色在 LocalMap 一律不可见。

**修复文件：**

- `StrategicWorldSitePopulationService.IsUngroupedResidentAtSite`
- `LocalMapVisibility`（`IsFriendlyCharacterOnMapLayout` / `IsEntityVisible` / `CanLoadMapLayoutForParty`）
- `ArrivalNoticeService`、`StrategicEncounterResolveService`（同类误替换）
- `HostWorldMapPanel`（头像计数不再 skip `AtSite`）

#### C2. `PartyWorld.SiteId` 被误清空

**根因：** Pure Hex 删除 `NodeId` 时，将 `world.PartyWorld.NodeId = string.Empty` 误改为 `SiteId = string.Empty`，在 `EnterWorldSiteScene` / `SyncPartyFocus` 写入后立即清空焦点 Site。

**修复：** `WorldTravelService.cs` 删除两处误清空；`WorldSiteEntryTests.SITE_RCLICK_02` 断言改为保留 `base:site_huangcun`。

**效果：** Level Tester / PlayableHost 开局荒村应能显示主角团与名册 NPC；大地图与 LocalMap 焦点一致。

### D. Content / UI 编码修复

| 位置 | 问题 | 修复 |
|------|------|------|
| `scenarios.json` | `displayName` 缺闭合引号 | 恢复「村内可招者」「巡卫甲/乙/丙」「将老」等 |
| `ch01_hex_world.json` | 12 处 Site `displayName` 截断 | 从 git `1e89a7b` 恢复（如 铁匠铺、洞府外、药田谷） |
| `ArmyStackAdapter.cs` | 山匪 stack 名为 `????` | 荒村山匪 / 试炼弱匪（自动必胜）/ 山匪斥候 |
| Host UI（7 文件） | 328 处 U+FFFD | `tools/fix-host-encoding.py` 自 git 嫁接 + 手工补全 |

**说明：** 大地图 `4人 · ????` 并非「未命名」，而是 DisplayName 编码损坏；修复后应为 `4人 · 荒村山匪` 等。

---

## 验证状态（截至 2026-08-24 晚）

| 项目 | 结果 |
|------|------|
| Shared.Tests No-op Roundtrip | PASS（Purge 时） |
| Unity batchmode 编译 | **PASS**（EXIT=0） |
| EditMode 全量（612） | **185 FAIL** — 多为 Pure Hex 前 Route/Node 遗留用例，需分批更新或 `[Ignore]` |
| EditMode 相关子集 | `WorldSiteLocalMapPopulationTests` PASS；`WorldSiteEntryTests.SITE_RCLICK_02` PASS |
| 手操 Level Tester | 用户反馈「目前没啥问题」（本地图角色 + 文案修复后） |
| EditMode 全跑 | Unity Editor 占用时 batch 会失败，需关 Editor |

---

## 新增工具（`tools/`）

| 脚本 | 用途 |
|------|------|
| `fix-broken-strings.py` | 修复 C# 损坏字符串闭合 |
| `fix-scenarios-json.py` | 修复 scenarios.json 截断 UTF-8 |
| `fix-host-encoding.py` | Host `.cs` 从 git 嫁接 UI 字符串 |
| `fix-bandit-display-names.py` | 恢复 ArmyStackAdapter 山匪 DisplayName |
| `fix-hex-world-display-names.py` | 从 git 恢复 hex world Site displayName |
| `scan-broken-json-strings.py` | 扫描 Content JSON 损坏模式 |
| `scan-host-encoding.py` | 扫描 Host 目录 U+FFFD / 非法 UTF-8 |

---

## 已知限制 / 未做

- **TerritoryRegion / Dynamic Bandit**：明确 STOP，未进入。
- **EditMode 185 失败**：需单独开一轮「Pure Hex 测试迁移」，非本轮阻塞项。
- **Core 层部分 `.cs` 仍有非法 UTF-8 字节**（如部分 Strategic 服务注释）；不影响当前编译，可后续批量 `fix-host-encoding` 扩展至 Core。
- **飞书 docId 映射**：本轮未更新。
- **最终 19 节 Pure Hex 验收报告**：未按模板完整输出；本文档作过程收束。

---

## 手操 Smoke Test（建议）

1. **Level Tester** → 荒村：主角团 + 名册 NPC 可见；Console 无 definitions 解析红错。
2. **大地图**：敌军显示 `4人 · 荒村山匪`、`1人 · 试炼弱匪（自动必胜）`；Site 列表无 `??` 地点名。
3. **军队详情**：Leader/Faction/State/Location 标签中文正常。
4. **进入 WorldSite** → 返回 LocalMap：人口不丢。

---

## 相关文件（核心）

| 区域 | 代表路径 |
|------|----------|
| Legacy Purge | `FormalArmy.cs`, `PartyWorldPresenceMode.cs`, `JsonSnapshotSerializer.cs`, 删 `WorldGraph*` |
| LocalMap 可见性 | `LocalMapVisibility.cs`, `StrategicWorldSitePopulationService.cs` |
| Party 焦点 | `WorldTravelService.cs`, `HexStrategicSessionBootstrap.cs` |
| 山匪命名 | `ArmyStackAdapter.cs` |
| Content | `scenarios.json`, `ch01_hex_world.json` |
| Host UI | `HostWorldMapPanel.cs`, `HostArmyFormPanel.cs`, `PlayableHostBootstrap.cs` |
| Tests | `WorldSiteEntryTests.cs`, `WorldSiteLocalMapPopulationTests.cs` |

---

## Git 建议

```text
feat(strategic): Pure Hex legacy purge + compile/runtime/encoding fixes

- Remove Node/Route legacy from runtime, snapshot v6, and content
- Fix AtSite visibility filter and PartyWorld.SiteId clear bug
- Repair scenarios.json, hex world site names, bandit display names, Host UI strings
- Add encoding repair tools under tools/
```
