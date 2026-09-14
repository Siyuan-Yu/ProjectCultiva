# CW-04 Flag Placement + Player Camp Retirement

> 状态：CW-04 Producer Accepted / Sealed
> 日期：2026-09-14  
> 范围：Continuous FactionFlag placement closure；退役 TEST Player Camp；不进入 CW-05

## Surface authority separation

此前 `FactionFlagService.ValidateSiteCorePlacement` 使用 `SurfaceGroundNavigation.Contains/IsWalkable`，但 Main Surface checked-in geography 只覆盖局部区域；完整 Surface 中真实可走的当前 CompositeWalkGrid 落点因此会被 Core 错拒。`WorldSpatialRules` 和行政 Surface membership 也存在同类混用。

现分为三类 authority：

- `OutdoorSurfaceSpatialAuthority`：Core session 元数据；保存完整 SurfaceId、origin、cell/chunk metric 与 authored chunk coverage，只回答存在、尺度和 membership。
- `SurfaceGroundNavigation`：精细地理路径、河桥、solid blocker 和其真实 coverage 内的 routefinding；不代表完整 Surface。
- Host CompositeWalkGrid：当前 loaded chunks 合成后的真实 placement walkability，包括地面、W2A geography、Site、destructible、现有旗和动态 blocker。

`WorldSpatialRules.ResolveLevel/ResolveWildernessEncounter` 与 `WorldSiteAdministrativeControlResolver.TryResolveOnRegisteredSurface` 已改用完整 Surface authority。路径规划、河桥和动态 composite 构建仍使用 SurfaceGround，不改变 W2A route behavior。

## Player flag placement

Preview 与点击提交都调用同一 Host preflight；提交时重新读取当前 CompositeWalkGrid。Core 不再检查 Strategic Hex passability，不再检查局部 SurfaceGround walkability，也删除固定 `dx²+dy²<16`（4 domain world units）排斥；只使用 Surface cellSize 的0.01-cell epsilon 防止数值同点 Core identity。

政治规则为：Core center 若由其它势力实际管理则拒绝；同势力 manager 允许。150×150 cells 理论范围可与任意 Site 重叠，既有重叠仍由 Claim AcquiredOrder 决定。Core-center invariant 要求存在同势力 manager，不要求 winner 必须是新 Site 自身，也不要求新 Site 立即获得最小面积。

正常成功链保持：

```text
Host exact CompositeWalkGrid preflight
→ Domain Surface/political validation
→ materials transaction
→ WorldSite + FactionFlag Core
→ Initial Claim + actual rebuild
→ WalkGrid blocker refresh + ground visual
→ WorldMap flag marker + Actual Overlay
```

## Player Camp retirement and replacement

正式 Content 已删除：

- `test:site_player_camp` Hex World Site；
- `test:region_player_camp`；
- Main Surface SiteRegion envelope；
- `player_camp_center_mark`／`player_camp_edge_e`；
- `base:loc_player_camp_start` Surface place。

原 canonical arrival 直接迁移为：

- FlagId：`base:flag_player_origin`
- Faction：`base:faction_player`
- Surface：`base:surface_main_wilderness_v1`
- WorldPosition：`(6.9282, 6.0)`
- Level：1
- Site：`site:runtime:base:flag_player_origin`，显示名“主角据点”

它完全使用 authored FactionFlag bootstrap，不是玩家势力特判。人物 opening anchors 与 snapshot WorldPresence 没有修改，不把人物传送到旗旁。

`StrategicContentBootstrap` 已停止调用 `EnsureLevelTesterFixtures`；旧 `EnsureLevelTesterPlayerCampSite` 只保留给显式 Core fixture。`base:map_player_camp` 与 `base:places_player_camp` 文件仍保留，唯一消费者是彼此及显式旧 fixture，不再被正常 HexWorld／Surface／scenario 引用。

## Snapshot boundary

明确含 `test:site_player_camp` owner identity 的旧 Snapshot，在缺少 `base:flag_player_origin` 且没有其它玩家 SiteCore flag 时，按当前 Content 一次补入起始旗、稳定 Site 和 baseline Claim。已有玩家自建 SiteCore 时不再造起始旗。旧 Camp 建筑、Region、marker 和 local map 不迁移；人物位置继续由原 snapshot authority 恢复。新格式损坏不会 fallback 重建 TEST Camp。

## Validation boundary

- Core／Data／Unity Host 现成 offline compile；
- BaseGame JSON、schema/reference、正式启动及少量内存调用链检查；
- `git diff --check`；
- 未运行 Unity、EditMode、PlayMode、TestRunner、batchmode、Bake 或新增测试；
- 实现与本页已纳入 checkpoint `1e48464`，等待制作人人工验收。
