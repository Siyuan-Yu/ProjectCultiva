# 192 · Phase 2J — Hex Territory / TerritoryRegion V1 基础层硬化封板（2026-09-03）

> 状态：**代码完成 + Content 修复完成，待 Unity 人工验收** ｜ 日期：2026-09-03
> 上级：[191 Phase 5S Persistence 收口 + Hex Territory V1 基础层封板](191-phase5s-persistence-closure-and-hex-territory-v1-2026-09-03.md)／[2J Hex Territory 规则](docs/20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)
> 本文 = 2J「TerritoryRegion / 固定 WorldSite 拥有明确 Territory」V1 指令（0~44 节）的落地归档：
> 在 **191 已封板的基础层**之上做四类增量——① Domain 硬化缺口；② Site+Territory 一次易主事务；
> ③ **ch01 Content 形状修复（上轮固化 Region hexes ≠ footprint+1-ring，本轮权威重生成）**；
> ④ Host/Exporter/Snapshot 收尾。不重写 191；191 缺失项 = 本文 Part A~D。

---

## Part A — Core Domain 硬化（191 未实现缺口）

### A1. TerritoryRegionBoard：hex→region O(1) 索引 + Register overlap 硬校验
- 新增 `_regionIdByHex`（`Dictionary<HexCoord,string>`）+ `TryGetAtHex`（O(1)，不再扫全表）。
- `Register` 现在对**跨 Region overlap 直接 throw**（错误信息含 `hex=(q,r)` + existingRegion + newRegion）；
  同 RegionId 重复注册 = 覆盖（幂等重载，先清旧 hex 索引）。生产语义：
  `overlap` 永不自动裁决（不做 nearest / SiteId tie-break / first-come）。

### A2. TerritoryControlService：查询入口补全
- 新增 `TryGetRegionAtHex(world, hex, out region)`（Board O(1)）。
- 新增 `TryGetRegionForSite(world, siteId, out region)`（规格命名对齐；优先 `site.TerritoryRegionId`）。
- `GetController` / `SetRegionController` 保持唯一写入口语义（Region + 全部 Hex 一起写，不自动改 Site Owner）。

### A3. TerritoryInvariantValidator：13.6 + Bandit 防护
- 新增检查 ⑤：Region 每个 Hex 的 `cell.ControlFactionId` 必须 == `Region.ControlFactionId`。
- 新增检查 ⑥：`ControlFactionId == base:faction_bandits` 的 Region = content error
  （2J §9.2 Bandit 不拥有正式 Territory）。ch01 现无 bandit-controlled region，安全。

### A4. HexWorldContentLoader：Register 异常 → Result
- `TerritoryRegions.Register`（现会 throw）包 try/catch → `Result.Failure(ContentLoadFailed)`，
  保证 `Apply` 的 Result 契约不被异常击穿（ContentPackageLoader 链正常报 ValidationReport）。

---

## Part B — Site + Territory 一次易主事务（Capture 一致性）

### B1. 新增 `WorldSiteTerritoryTransferService.Transfer(world, siteId, newFactionId)`
唯一 Fixed Site 政治易主入口，顺序：
1. 找 WorldSite；
2. 经 `site.TerritoryRegionId` 找 TerritoryRegion；缺 region（legacy/dynamic，`TerritoryRegionId==""`）→
   fallback `WorldSiteOwnershipService.SetOwner`（未来 Dynamic Site 无 Region 的合法路径）；
3. 验证 `Region.PrimaryWorldSiteId == site.SiteId`（双向绑定不一致 = 数据错误，返回 failure 不静默修）；
4. `site.OwnerFactionId = newFactionId`；
5. `TerritoryControlService.SetRegionController`（Region.Controller + 全部 Hex.ControlFactionId 一次同步）；
6. Development-only 后置 assert（Owner/Region/每 Hex == newFaction）。

### B2. CaptureObjectiveService 接入 Transfer
`TryCompleteWorldSiteCapture` 把裸 `WorldSiteOwnershipService.SetOwner` 替换为 `WorldSiteTerritoryTransferService.Transfer`，
失败即返回——**不再存在「Site Owner 已改、Region/Hex 未改」的运行时中间态**。
`WorldSiteOwnershipService.SetOwner` 保留低层用途（dynamic / legacy setup / 底层 restore），未改。

---

## Part C — Content：ch01 Region 形状修复（权威重生成）

### C1. 上轮固化 Region hexes 与规格不符（决定性证据）
用一次性验证工具（镜像 HexMath Odd-R 邻居）对两个 Hex World 全量校验，发现：
- `ch01_hex_world.json`：30 Region 中 **17 个形状错**（缺 ring 格或含多余格）；
- `travel_mvp_hex_world_30x15.json`：8 Region 中 **7 个形状错**。
上一轮（191/13352b0）的 Region 生成算法有 bug —— 不是规格要求的「整个 footprint + 1 跳 ring」。

### C2. ch01：按权威 HexMath 重新生成（footprint ∪ ring ∩ bounds，排序 R,Q）
- 工具文本级替换每个 Region 的 `"hexes"` 数组（只动 territoryRegions 段；site/owner/control/双向绑定全保留）。
- **修复前 190 hexes → 修复后 285 hexes**：Regions=30、ControlledRegions=15、NeutralRegions=15、
  TerritoryHexes=285、ControlledHexes=119、NeutralRegionHexes=166、**Overlap=0、InvariantErrors=0**（verify ALL PASS）。
- 修复后 diff：`+212/−117` 行，仅 territoryRegions 段；`git diff --check` 通过；UTF-8 无 BOM 保持。

### C3. travel_mvp：**overlap STOP（未写盘，等设计裁决）**
- 若把 mvp 也按 footprint+ring 权威重生成，出现 **overlap**：
  - Region A：`base:region_huangcun`（荒村）
  - Region B：`test:region_player_camp`（玩家营地）
  - 重叠 Hex（5 个）：`(3,6) (4,6) (4,7) (3,7) (5,6)`
- 按制作人决定（2J §6.12 SUPERSEDED / 指令 §0.5 §40）：**不自动裁决、不改 Site 坐标** → mvp 文件**保持原状未写盘**。
  需用户调整 player_camp / huangcun 的 footprint 或位置后再生成。mvp 当前仍为旧不规则 hexes（无 overlap，validator 不会 fail 启动）。

---

## Part D — Host / 工具 / Snapshot 收尾

- **WorldMap Territory tint 强度** 0.26 → 0.22（2J §9.4 区间 0.15~0.22；terrain 仍可读）。实现为每帧实时
  `ResolveTerritoryTint`（terrain fill 混合 `ControlFactionId`→`StrategicFactionCatalog.MapTint`），0 GameObject per Hex；
  `SetRegionController` 后下一帧自动刷新，无需 invalidate（Core 不依赖 Host）。
- **HostWorldMapPanel Hex inspect**：Region 归属改 Board `TryGetAtHex` O(1)，去掉全表 foreach（输出字段不变）。
- **HexWorldContentExporter**：Region hex 列表稳定排序（R then Q），保证导出 diff 好看。
- **ContentPackageLoader**：territoryRegions 对象级 `RejectUnknownFields(HexWorldTerritoryRegionFields)`。
- **StrategicSnapshotHelper.Restore**：Owner/Region 恢复完成后 Development-only 校验
  `Site.OwnerFactionId == Region.ControlFactionId == 每 Hex.ControlFactionId`，不一致 `Debug.Fail`（不静默修）。
- **2J 文档**：顶部新增 Implementation Status（2026-09-03 V1）；§6.12 加 SUPERSEDED banner
  （Initial authored Territory may not overlap = content validation failure）；变更记录追加。

---

## Final Invariant（2J V1 后维持）

- `WorldSite.Footprint` = 地点本体空间；`TerritoryRegion.Hexes[]` = 该地点辖区 membership（内容固化，Runtime 不重算）。
- `HexCell.ControlFactionId` = 每 Hex 最终政治控制唯一真源；Region 只是组织结构，非第二套真源。
- Fixed Site 永远 `OwnerFactionId == TerritoryRegion.ControlFactionId`，且 region 每 hex `ControlFactionId == Controller`。
- Initial authored Region 不可重叠（overlap = content error，register throw / validator fail / load fail）。
- Dynamic Site 无 Territory；未来 player-built Site 走 first-claim，不抢已有 Region hex（只记录不实现）。
- Capture 只经 `WorldSiteTerritoryTransferService`（一次易主）；WorldSiteOwnershipService 仅低层/dynamic/restore。
- 本轮不做 Capture 后的 Territorial AI / 非法入境 / 外交效果（2J 后续）。

---

## 验收指引（人工，Unity LevelTester / WorldMap）

1. **Case A 视觉**：有势力 WorldSite（如朔风堡/东林）周围 footprint + 外围一圈出现淡 faction tint（0.22），不是 anchor+1 圈。
2. **Case B Multi-Hex**：青石镇（4 hex footprint）Region = 整个 footprint 外扩一圈。
3. **Case C 荒野有势力**：WorldSite 外围 Wilderness hex `WorldSiteId=""` 但 `ControlFactionId=势力`，有 tint。
4. **Case D 无主 Site**：有 TerritoryRegion identity、ControlFactionId="" → 无 tint；inspect 显示 Region != None、Controller=None。
5. **Case E Region 外荒野**：TerritoryRegion=None、ControlFactionId="" → 纯 terrain。
6. **Case F 同 faction 多 Site**：RegionId 不同、ControlFactionId 相同 → 同色、Domain 不 merge。
7. **Case G Capture smoke**：Capture 固定 Site 前记录 Site Owner/Region Controller/外围 hex；Capture 后三者一次易主、tint 立即变、LocalMap 人物不消失瞬移。
8. **Case H Save/Load**：Capture 后 Save→Load，Owner/Region/Hex/tint 保持；Development 无 Debug.Fail。
9. **Case I Regression**：PlayerParty/FormalArmy travel、footprint ingress、SupportArea、Manual/Auto Battle、residual 均不受影响。

### travel_mvp 未决（需用户裁决）
`base:region_huangcun` ↔ `test:region_player_camp` 半径 1 Region 重叠 5 hex（见 C3）。请调整 mvp 设计后重跑修复工具。

---

## 验证状态
- Host 全链编译（Unity 2022.3.6f1 dll + Core + Data + Unity 脚本）：**0 error**（2 个既有无关 warning）。
- ch01 Territory Content 校验：**ALL PASS**（Regions=30 Overlap=0 InvariantErrors=0）；`git diff --check` 通过。
- travel_mvp：overlap STOP，未写盘（内容设计待裁决）。
- 未跑 Unity tests（按要求）；运行时留人工验收。
