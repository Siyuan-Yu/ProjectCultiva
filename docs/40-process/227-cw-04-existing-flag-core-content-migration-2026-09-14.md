# CW-04 Existing Flag Core Content Migration

> 状态：CW-04 Producer Accepted / Sealed
> 日期：2026-09-14  
> 范围：已有正式势力旗 → 精确 WorldSite SiteCore → baseline Claim → Actual Overlay；不进入 CW-05

## 原因与统一链路

荒村议政厅已有 Main Surface `controlCore` placement，因此能建立正式 Site Core、TerritoryClaim 和 Actual Overlay。旧 `factionFlags` 只有 `FlagId/FactionId/AnchorHex/EstablishedOrder`，只能画兼容旗图标，不能凭 Owner 或 Hex 产生实际行政控制。

现统一为：

```text
authored FactionFlag precise Surface position
→ FactionFlagSiteCoreBootstrap
→ SiteIdForCoreFlag(flagId)
→ Level1 WorldSite Core
→ EstablishBaselineFromLegacy（保留 EstablishedOrder）
→ TerritoryClaim
→ WorldSiteActualControlOverlayBuilder
→ direct world-space WorldMap projection
```

玩家新建旗继续使用 `TryConstructFactionFlagSite → TryPlaceSiteCore → CreateInitialClaim`，没有第二套寻址或领土算法。后续前置 Fix 将 Core-center invariant 收口为“必须存在同势力 actual manager”：旧同势力 Site 可按 Claim 历史继续管理重叠中心，不要求新 Site 插队成为 winner。

## 正式开局旗的精确 Content migration

本次位置只在 Content 迁移时由旧 `AnchorHex` 和当前 HexSize=1 计算并写死；Runtime 以后直接读取 authored `SurfaceId/WorldX/WorldY`，不再从 Hex 猜位置。

| FlagId | Faction | Old AnchorHex | Authored SurfaceId | Authored WorldX/Y | Runtime SiteId |
|---|---|---:|---|---:|---|
| `base:flag_q1_r10` | `base:sect_huangcun_labor` | (1,10) | `base:surface_main_wilderness_v1` | (1.732051,15.0) | `site:runtime:base:flag_q1_r10` |
| `base:flag_q3_r11` | `base:sect_huangcun_labor` | (3,11) | 同上 | (6.062178,16.5) | `site:runtime:base:flag_q3_r11` |
| `base:flag_q7_r8` | `base:sect_huangcun_labor` | (7,8) | 同上 | (12.124356,12.0) | `site:runtime:base:flag_q7_r8` |
| `base:flag_q5_r10` | `base:sect_huangcun_labor` | (5,10) | 同上 | (8.660254,15.0) | `site:runtime:base:flag_q5_r10` |
| `base:flag_q27_r10` | `base:faction_fisher_village` | (27,10) | 同上 | (46.765373,15.0) | `site:runtime:base:flag_q27_r10` |
| `base:flag_q26_r8` | `base:faction_fisher_village` | (26,8) | 同上 | (45.033321,12.0) | `site:runtime:base:flag_q26_r8` |
| `base:flag_q29_r10` | `base:faction_fisher_village` | (29,10) | 同上 | (50.229473,15.0) | `site:runtime:base:flag_q29_r10` |
| `base:flag_q15_r12` | `base:faction_xijin` | (15,12) | 同上 | (25.980762,18.0) | `site:runtime:base:flag_q15_r12` |
| `base:flag_q10_r13` | `base:faction_xijin` | (10,13) | 同上 | (18.186533,19.5) | `site:runtime:base:flag_q10_r13` |
| `base:flag_q12_r13` | `base:faction_xijin` | (12,13) | 同上 | (21.650635,19.5) | `site:runtime:base:flag_q12_r13` |

## 保持 legacy-only 的旗

- `test:flag_fisher_east`：正式开局 World 中的显式测试命名空间内容，不提升为产品 Core。
- 非开局 `base:hex_world_ch01` 的三面旧旗：`base:flag_xijin_westroad`、`base:flag_nan_yan_centralroad`、`base:flag_fisher_eastroad`。其旧 Hex 显示中心分别约为 (95.263,45)、(225.167,52.5)、(277.128,45)，不属于任何正式 Continuous Surface，因此按规则拒绝迁移；没有放到最近 Surface、玩家位置或 Site arrival。

## Baseline、Snapshot 与表现

- 议政厅和 authored flag Sites 进入同一 baseline candidate 列表；排序使用既有 `ControlEstablishedOrder/EstablishedOrder`，冲突再用稳定 Site identity。
- 新 flag snapshot 写 `siteCoreFormat=1` 以及完整 world/Site 字段。旧条目 format=0 且 FlagId 匹配当前 authored flag 时，从当前明确 Content 补齐，不从 AnchorHex 重算；缺失 Runtime Site/Claim 只补一次，重复恢复不产生重复 ID。
- 新格式损坏或 authoritative runtime Site 不匹配时返回 `SnapshotInvalid`。
- WorldMap legacy flag presentation 继续跳过 `IsSiteCore`；Actual Overlay 仍由 Claim 的 direct world geometry 绘制，不恢复 Hex tint fallback。
- Level1 未改变：150×150 Surface cells＝Main Surface 4.2×4.2 world＝3×3 chunks。

## 当前正式启动链统计

| FactionId | CouncilHall Core Sites | Flag Core Sites | Actual Control Sites | Actual Overlay count | Pieces |
|---|---:|---:|---:|---:|---:|
| `base:sect_huangcun_labor` | 1 | 4 | 5 | 5 | 8 |
| `base:faction_fisher_village` | 0 | 3 | 3 | 3 | 4 |
| `base:faction_xijin` | 0 | 3 | 3 | 3 | 3 |

Main Surface authored `controlCore` placements=1；正式开局 runtime flags=11；migrated flag cores=10；legacy-only=1；baseline Claims=11；Actual overlays=11／pieces=15。

其它 Faction 没有范围的原因仍是没有明确议政厅 Core，也没有成功迁移的正式势力旗；OwnerFactionId 本身不会凭空创建 Core。

## 验证边界

- 使用现有非 Unity Core/Data/Host 离线编译。
- 使用刚编译的 Core/Data 程序集执行只读 BaseGame Content load、reference validation、正式开局 HexWorld apply 与 runtime bootstrap 统计。
- 使用内存 DTO 演练旧旗缺失 Site-Core 字段的恢复：10 flags、10 runtime Sites、11 unique Claims、11 overlays；未读写制作人存档文件。
- 未运行 Unity、EditMode、PlayMode、TestRunner、batchmode、Bake 或新增自动测试。
- 修改保持工作区未提交，等待制作人人工验收。
