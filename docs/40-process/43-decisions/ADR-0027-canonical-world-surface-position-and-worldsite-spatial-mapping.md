# ADR-0027：Canonical World Surface Position 与 WorldSite Spatial Mapping

- **状态：** 已采纳
- **日期：** 2026-08-30
- **决策者：** 项目负责人（底层空间模型大版本调整；Phase 5R）
- **关联：** [2K 系统真源](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[2J](../../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)、[03-glossary](../../00-project/03-glossary.md)、[ADR-0025](ADR-0025-strategic-spatial-model-hexgrid.md)、[ADR-0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)、[163](../../40-process/163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)

## 背景

在 Phase 2C（Continuous WorldPosition 真源）与 Phase 5D（PlayerParty 连续旅行、WorldSite Ingress、Dynamic Mandatory Transit）落地后，位置模型出现多个可独立漂移的字段（`WorldPosition` / `CurrentHex` / `PresenceHex` / `AnchorHex` / `SiteDepartureFootprintHex`）共同争夺"玩家到底在哪"的真源地位：

1. Wilderness 连续态已有"Continuous WorldPosition 真源 + CurrentHex 派生"契约（2K §5.8.3）；
2. 但 WorldSite 内仍是旧 "Aggregated" 模型：`LocalMap 只改 LocalPosition`、`WorldMap 投影恒 = 固定 PresenceHex`、禁止按 Local 坐标投影到 footprint 其他格（2K §5.8.5 / §6、2J §5.6.1、ADR-0026 #6/#12）；
3. `WorldSite.PresenceHex` 在 Runtime 被 invariant 强制钉死 `== AnchorHex`，且不随 LocalMap 内位置漂移 —— 多 Hex Site（如 4-Hex 青石镇）下角色在 LocalMap 左上/右下都投影到同一个锚格，WorldMap Avatar 会**跳 Anchor**；
4. 战斗/支援层对多 Hex Site 的邻接判定部分依赖 `Army.CurrentHex` 单格 ∈ footprint 才激活 footprint 分支（BattleEngagementSupportArea），否则退回单格模型 —— 与"Site 是一个逻辑地点"冲突。

结论：需要一个**唯一的物理位置真源**贯穿 Wilderness 与 WorldSite，并把"战略地点语义（AtSite）"与"物理位置"彻底分离。

## 选项

**A. 维持 Aggregated + 固定 PresenceHex**
- 优点：改动小。
- 缺点：多 Hex Site 下 WorldMap Avatar 跳 Anchor；Site 内位置丢失（ingress/egress 需重猜 FootprintHex）；战斗支援语义混乱；与"连续世界"产品方向矛盾。**否决。**

**B. Canonical World Surface Position 唯一真源 + WorldSite Spatial Mapping（采纳）**
- 优点：Wilderness 与 Site 统一；AtSite 是 Context、物理位置是连续真源；WorldMap Avatar 无跳变；ingress/egress 自然；战斗支援可用 Site 整体 Ring1。
- 缺点：需迁移旧 Presence/Anchor 写入、Battle support authority；需要 Local transform ↔ motion.WorldPosition 的同步接入（不新增第二份持久位置真源，见 5R-0.1 修正 #1）。

**C. 保留多真源、仅加同步**
- 优点：无。
- 缺点：同步逻辑继续累积漂移修补。**否决。**

## Decision

1. **CanonicalWorldSurfacePosition 为 PlayerParty 物理位置唯一真源。** PlayerParty 在整个连续世界表面（Wilderness 与 WorldSite 内）只有一个连续物理位置；WorldMap、Wilderness LocalMap、WorldSite LocalMap 看到的都是这同一个空间位置的不同表现。禁止多个可独立漂移的 `WorldPosition` / `CurrentHex` / `PresenceHex` / `AnchorHex` / `SiteDepartureHex` 并存争夺真源。

    **LocalPosition 不是第二份 persistent world-location authority（5R-0.1 修正 #1）。** LocalPosition = LocalMap execution/presentation coordinate，由既有 `EntityView.transform.position` / `EntityLocationComponent.PresentationOverrideX/Z` / `LoadedLocalMapPlacementSnapshotRestore` 承担，**不在 `PlayerPartyWorldMotion` 持久化**。LocalMap 可见时：`Local transform → WorldSiteSpatialMapping.LocalToWorld → 更新 motion.WorldPosition`；重新 Materialize：`motion.WorldPosition → WorldSiteSpatialMapping.WorldToLocal → Local presentation`。

2. **PresenceHex 改为 derived（staged deprecation，5R-0.1 修正 #4）。** 新定义 `DerivedPresenceHex = CanonicalWorldSurfacePosition → WorldToHex`；Site 内为 `LocalPosition → WorldSiteSpatialMapping → CanonicalWorldSurfacePosition → WorldToHex`。只作为查询结果/cache 存在，可重建，永不落盘为独立真源。**禁止长期出现 `WorldPosition=A、PresenceHex=B、LocalMap=C` 的分叉。** 弃用节奏：① 立即禁止新的 PlayerParty spatial 主链读取 `PresenceHex`；② 字段保留为 **Legacy Compatibility Representative Hex**（继续等价 AnchorHex）供 BackgroundCharacter / FormalArmy compatibility / Snapshot / CharacterWorldPresenceQuery / PreEngagement / Bootstrap 等既有读者过渡；③ PlayerParty mapping/query/materialization 完成后逐系统迁移其余 consumer；④ 最后零依赖后删除字段与 `EnsurePresenceHexValid`、`PresenceHex == AnchorHex` invariant。**不第一批物理删除。**

3. **AtSite(SiteId) 是战略 Context，不覆盖 Physical Position。** 角色在 WorldSite 内战略语义恒为 `AtSite(SiteId)`；同时 `CanonicalWorldSurfacePosition` 指向 footprint 内对应区域。不能用 `DerivedPresenceHex` 判断"是否离开 Site"——只要仍在 Site LocalMap / Site Context 内就是 AtSite。

4. **WorldSite LocalMap 与 footprint 建立连续空间映射（WorldSiteSpatialMapping，5R-0.1 修正 #2）。** 正式数学方案（禁止把 LocalMap absolute coordinate 直接当 HexWorld coordinate）：

    A. 用真实 `MapLayoutDefinition`（`OriginX/OriginY/CellSize/Width/Height`；经 `world.LocalMap.ActiveMapLayoutId` / `WorldSite.LocalMapId` 解析）得到 Local **playable bounds** —— 它们是 LocalMap grid 坐标，不是 HexWorld world-surface 坐标；
    B. `LocalPosition → normalized (u,v)`（相对 playable bounds）；
    C. 用 `WorldSite.OccupiedHexes` + `HexMath` 的真实 Pointy-Top / Odd-R polygon world geometry 计算 Site footprint 的 **WorldSurface domain / bounds**；
    D. `(u,v)` 等比例映射到该 domain（Local 左/右/上/下 ↔ footprint 左/右/上/下）；
    E. irregular footprint：若映射后 `WorldToHex` 结果 ∉ footprint，project/clamp 到最近合法 footprint domain；
    F. World→Local 用同一 mapping 的近似可逆 inverse。

    **禁止** `worldPos = OriginX + localCellX * CellSize` 之类把 LocalMap absolute coordinate 直接当 HexWorld coordinate 的映射；**AnchorHex 不参与 physical mapping**；`MapLayoutDefinition` 拿不到时明确失败，不伪造。

5. **WorldMap PlayerParty Avatar 永远按 CanonicalWorldSurfacePosition 绘制（5R-0.1 修正 #7）。** 进入/离开 WorldSite、WorldMap 开/关均无坐标跳变；不跳 Anchor、不重刷默认 Spawn、不 Snap 到 Hex center、不丢失 Site 内位置。`HostWorldMapPanel.DrawPlayerPartyMarker` 只消费 `PlayerPartyWorldLocationQuery.WorldPosition`（AtWorldSite 时也返回 `motion.WorldPosition`），不建立 Site Avatar UI 特例；禁止 `AtWorldSite → site.PresenceHex → Hex center`。Site Local movement 持续同步 CanonicalWorldPosition 后，WorldMap Avatar 自然连续。

6. **Multi-Hex WorldSite 在战斗/支援层视为一个逻辑地点（5R-0.1 修正 #9）。** `CombatLocation = Site(SiteId)`；Site 内所有合法参战实体 Same Combat Location；不按角色在 Site 内映射到 H1/H2/H3/H4 改变参战关系。现有 `BattleEngagementSupportArea`（BattleArea=Site footprint；SupportArea=footprint+direct-neighbor ring）**保留，不重写**；真正要改的是 **authority**：entity/FormalArmy `LocationKind == AtWorldSite && SiteId == X` → `CombatLocation = Site(X)`，**不再要求** representative CurrentHex/PresenceHex 恰好命中 footprint 才启用 Site battle area。`PresentationAnchorHex` 仍可用 `AnchorHex`（UI 展示锚，非 physical position）。

7. **SiteSupportRing1 = 所有 footprint Hex 的一阶 Hex Neighbor 并集 − footprint 自身。** 任何合法 Army 位于 SiteSupportRing1 即视为距离该 Site 1 格，进入现有一格支援/参战判定。不使用 AnchorHex 的 6 邻格，也不只使用 DerivedPresenceHex 的 6 邻格。

8. **MandatoryTransitSite 是路径中的动态关系，不是 WorldSite 固有属性（5R-0.1 修正 #11）。** 正常 A→B 路由：所有非目标 WorldSite footprint blocked（Shared Route Authority：PlayerParty 与 FormalArmy 共用 `WorldSiteTransitPolicy.BuildBlockedFootprintHexes`）。若 A→B NoRoute：对**每一个非目标 Site S** 做反事实 permeability probe（临时仅移除 S footprint，其余仍 blocked，同一 HexPathfinder 重算 A→B）；仅当 `ProbeRouteSuccess` 且 ProbePath 真实经过 S ≥1 footprint 格 → S 为本次路线的 MandatoryTransitSite；多候选按假设直通路径实际 cost 选最低。**不使用 TransitMode / DisplayName / SiteType / 预配置判断 Gateway。不重新引入 `TransitMode.Gateway`。**

9. **Context change must not imply Physical Position snap（5R-0.1 修正 #5）。** Wilderness→WorldSite 只改变 Context：`LocationKind: AtWorldPosition → AtWorldSite`、`SiteId: "" → targetSite`；`Canonical WorldPosition` 必须保持边界处连续位置，**禁止**因进入 Site 而 Snap Anchor／Snap PresenceHex／Snap ingress footprint center／Snap Site center。WorldSite→Wilderness 同理。改变的是 Context，不是 Physical Position。

10. **CurrentHex 不允许立即删除或全局替换（5R-0.1 修正 #3 / 补充修正 B）。** 当前 `CurrentHex` 混合三种职责：`PhysicalDerivedHex = WorldToHex(WorldPosition)`；`RouteCommittedHex = HexPath[SegmentIndex]`（Travel 正式提交格）；`CurrentWildernessHex = 当前 Wilderness LocalMap / Surface Context`（项目仍需它决定当前所属/加载的 Wilderness Hex）。Hex 边界附近派生与路由格可暂时不同（Phase 5C takeover boundary 分叉即证据）。迁移：先审计全部读取点分类，再逐步退役；最终拆为 `CurrentWildernessHex`（保留）／`DerivedSurfaceHex`（pure derived）／`RouteCommittedHex`（Travel progress）。**不物理删除该概念。**

11. **Spatial authority 职责分离（5R-0.1 修正 #8），不建 God-class。** `WorldSiteSpatialMapping`（仅 Local↔World physical mapping）／`WorldSiteFootprintLocationAuthority`（footprint membership / ingress-boundary topology；必要时增加 OuterRing1 topology helper）／`BattleEngagement*`（CombatLocation / Support / Participation 规则）各自独立。Site ingress/egress **复用现有 `SurfaceExitConnection`（`BoundaryContactWorldX/Y`）与 `WorldSiteFootprintExitConnectionResolver`（基于真实 footprint 生成外围 connection）**，不建立第二套 `SiteIngressWorldPoint` / magic offset / fake boundary（5R-0.1 修正 #6）。

## Supersedes

| 旧规则 | 位置 | 关系 |
|---|---|---|
| PresenceHex 是固定 Authoring 世界位置代理、WorldMap 投影真源 | 2K §6、2J §5.6.1、glossary"世界位置代理格"、ADR-0026 #6 | **SUPERSEDED** → DerivedPresenceHex（Decision #2） |
| Aggregated WorldSite：LocalMap 只改 LocalPosition；WorldMap 投影恒 = PresenceHex；禁止按 Local 坐标投影 | 2K §5.8.5、ADR-0026 #12、glossary"聚合地点定位" | **SUPERSEDED** → AtSite Context + Spatial Mapping（Decision #3/#4/#5） |
| 进入 Site 后 Avatar 固定到 Anchor / PresenceHex 单格 | 2K §5.8.10、ADR-0026 #12 | **SUPERSEDED** → Canonical 连续投影（Decision #5） |
| 多 Hex Site 进入"从 H1/H4 完全一样、无来向区分" | 2J §5.5 | **部分 SUPERSEDED**（5D-B2a 起按来向选 footprint 入口；仍不产生不同 LocalMap 实例） |
| Footprint 不阻挡普通移动（旅行路由可穿过） | 2J §5.10 | **World Travel 路由例外**：非目标 Site footprint 在旅行寻路中 blocked；该条对 Army 战略存在语义保留 |
| Multi-Hex Site 战斗支援按 Anchor/单格邻接 | BattleEngagementSupportArea（代码）、146"支援范围" | **SUPERSEDED** → CombatLocation=Site + SiteSupportRing1（Decision #6/#7） |
| 5D-A「TransitMode.Gateway 预配置决定必经点」 | 代码（WorldSiteTransitMode 已随 5D-B2 移除）；文档从未记录该设计 | **SUPERSEDED** → Dynamic MandatoryTransitSite（Decision #8）。本 ADR 固化此 supersede，防止旧设计被重新引入 |

不删除历史 ADR / 历史流程文档正文；以本 ADR 与更新的 2K/2J/glossary 为当前真源。

## Phase 5R-0.1 修正清单（2026-08-30 追加拍板；覆盖上文对应 Decision）

| # | 修正 | 对应 |
|---|---|---|
| 1 | 不新增 `PlayerPartyWorldMotion.LocalPosition` 持久真源；LocalPosition = execution/presentation coordinate（EntityView transform / PresentationOverride / SnapshotRestore） | Decision #1 |
| 2 | SpatialMapping 数学：LocalPosition → normalized (u,v) → Site footprint WorldSurface domain（Pointy-Top/Odd-R 真实几何）；禁止 `Origin+cell*CellSize` 直映射；irregular footprint project/clamp；Anchor 不参与 | Decision #4 |
| 3 | CurrentHex 不立即删除/全局替换：先分类（PhysicalDerivedHex / RouteCommittedHex / CurrentWildernessHex）再迁移 | Decision #10 |
| 4 | PresenceHex staged deprecation：立即禁新 PlayerParty 主链读取；保留 legacy 等价 AnchorHex；逐系统迁移；最后零依赖删除 | Decision #2 |
| 5 | AtSite transition invariant：Context change 不改变 Physical Position（无 Snap Anchor/PresenceHex/ingress center/Site center） | Decision #9 |
| 6 | Ingress/Egress 复用现有 `SurfaceExitConnection` + `WorldSiteFootprintExitConnectionResolver`，不建第二套 fake boundary | Decision #11 |
| 7 | WorldMap Avatar：`DrawPlayerPartyMarker` 只消费 `PlayerPartyWorldLocationQuery.WorldPosition`（AtWorldSite 也返回 motion.WorldPosition） | Decision #5 |
| 8 | 职责分离：SpatialMapping / FootprintLocationAuthority / BattleEngagement* 三 authority，不建 God-class | Decision #11 |
| 9 | Battle：保留 BattleArea=footprint + SupportArea=ring；改 authority（AtSite+SiteId → CombatLocation=Site(X)）；PresentationAnchorHex 仍用 AnchorHex（UI 锚） | Decision #6 |
| 10 | 迁移顺序：5R-B1..B5 → 5R-C → 5R-D → 5R-E（见下） | — |
| 11 | MandatoryTransit 保持动态 probe，不重新引入 TransitMode.Gateway | Decision #8 |
| 补充 A | LocalMap 尺寸（Width/Height/CellSize）不要求统一：Wilderness 用「当前 Wilderness Hex + 真实 playable bounds + LocalPosition → (u,v) → 该 Hex world domain」；WorldSite 用「SiteId + footprint + bounds → (u,v) → footprint domain」；(0.5,0.5) 恒=相对中心 | Decision #4 |
| 补充 B | 不删除「当前 Hex Context」概念：CurrentWildernessHex（保留）／DerivedSurfaceHex（pure derived）／RouteCommittedHex（HexPath[SegmentIndex]）三分，普通态前两者相等，边界过渡期允许暂时不同 | Decision #10 |

## Consequences

- **WorldSite ingress/egress 简化**：连续位置从 Wilderness 映射进入 Site LocalMap；离开时从当前 LocalPosition 自然确定外部 Hex，不再"进入后忘掉位置、出城重猜 FootprintHex"。
- **Site 内位置不再丢失**：WorldMap 开/关、进入/离开不丢 Site 内连续位置。
- **WorldMap open/close 同步状态减少**：只有一个 Canonical 真源，Avatar 与 LocalMap 角色位置天然一致。
- **Presence/Anchor 写入 staged 迁移**：先禁止新的 PlayerParty spatial 主链读取 PresenceHex，字段暂保留为 Legacy Compatibility Representative Hex（等价 AnchorHex）供既有读者过渡；PlayerParty mapping/query/materialization 完成后逐系统迁移，最后零依赖后删除字段与强制 invariant（5R-0.1 修正 #4）。
- **Battle support authority 更新**：保留现有 `BattleEngagementSupportArea`（BattleArea=footprint、SupportArea=ring）不重写；改 authority：AtSite + SiteId → `CombatLocation = Site(X)`，不再要求 representative hex 命中 footprint；Army 只接邻接判定，不进入 LocalMap 空间模型（5R-0.1 修正 #9）。
- **不新增 Site LocalMap 内 Core 持久位置真源**：LocalPosition 由既有 execution/presentation 载体（EntityView transform / PresentationOverride / SnapshotRestore）承担；Local transform ↔ motion.WorldPosition 经 WorldSiteSpatialMapping 双向同步（5R-0.1 修正 #1）。
- **SaveLoad 最终只需保存 canonical/context 等必要真源**，派生量不落盘。

## 技术附录：WorldSiteSpatialMapping 推荐语义（5R-0.1 修正；设计，本轮不实现）

Core authority（Strategic 层，纯静态，非 UI 专属）。**职责分离（修正 #8）**：本类只做 Local↔World physical mapping；footprint membership / ingress-boundary 拓扑归 `WorldSiteFootprintLocationAuthority`（必要时加 OuterRing1 helper）；CombatLocation / Support / Participation 归 `BattleEngagement*`。不建 God-class。

```text
WorldSiteSpatialMapping
  LocalToWorldSurface(Site, LocalMapBounds, LocalPosition) -> WorldSurfacePosition
  WorldSurfaceToLocal(Site, LocalMapBounds, WorldSurfacePosition) -> LocalPosition   // 近似可逆 inverse
  TryResolveDerivedFootprintHex(...)  // Canonical -> WorldToHex；Site Context 内 ∈ footprint
```

**映射数学（修正 #2）**：

A. 用真实 `MapLayoutDefinition`（`OriginX/Y、Width、Height、CellSize`；经 `world.LocalMap.ActiveMapLayoutId` / `WorldSite.LocalMapId` 解析）得到 Local **playable bounds** —— 它们只是 LocalMap grid 坐标；
B. `LocalPosition → normalized (u,v)`（相对 playable bounds）；
C. 用 `WorldSite.OccupiedHexes` + `HexMath` 真实 Pointy-Top / Odd-R polygon world geometry 计算 Site footprint 的 WorldSurface domain；
D. `(u,v)` 等比例映射到 domain（Local 左/右/上/下 ↔ footprint 左/右/上/下）；
E. irregular footprint：映射后 WorldToHex ∉ footprint → project/clamp 到最近合法 footprint domain；
F. World→Local 用同一 mapping 的近似可逆 inverse。

**禁止** `worldPos = OriginX + localCellX * CellSize` 直映射；**AnchorHex 不参与 physical mapping**；不用 fake bounds / magic offset。

**补充修正 A（LocalMap 尺寸不要求统一）**：mapping 是 Context-aware 的 —— Wilderness：`CurrentWildernessHex + 真实 playable bounds + LocalPosition → (u,v) → 该 Hex 的真实 world-surface domain → CanonicalWorldPosition`；WorldSite：`SiteId + footprint + bounds + LocalPosition → (u,v) → footprint domain`。不同 Hex/Site 的 LocalMap 允许不同 Width/Height/CellSize（如 50×50 的 (25,25) 与 100×80 的 (50,40) 都 = (0.5,0.5) → 各自相对中心）。尺寸差异影响 Local traversal time 属 Movement Speed / World Distance Budget 独立问题，不因此强制同尺寸。

**补充修正 B（保留当前 Hex Context 概念）**：不删除 CurrentHex；未来拆为 `CurrentWildernessHex`（当前 Wilderness LocalMap/Surface Context，保留）／`DerivedSurfaceHex`（pure WorldToHex）／`RouteCommittedHex`（HexPath[SegmentIndex]）。普通态前两者相等，边界过渡期允许暂时不同（SurfaceExit/Context transition commit 后才切换）。

## 保留待代码阶段决定的问题

1. **Local transform ↔ motion.WorldPosition 同步的写点与触发时机**（5R-B2：`UpdateWorldPositionWithinSite(...)` 保持 AtWorldSite Context）：在 EntityView transform / PresentationOverride 更新帧内调用，还是合并到 presence sync tick —— 需按实际 update 频率实测定。
2. `WorldSiteSpatialMapping` **归一化系数与 clamp 规则**：footprint 包络与 LocalMap playable rect 的精确对应方式（等比例 vs 保持方向的分段映射），irregular footprint 的 project/clamp 策略，需按具体 MapLayout 数据实测定参（5R-B1 shadow）。
3. `BattleEngagementSupportArea` authority 替换顺序：`LocationKind==AtWorldSite && SiteId==X → CombatLocation=Site(X)` 先 PlayerParty 后 Army，避免一次性大改（5R-E）。
4. SaveLoad schema：`CanonicalWorldSurfacePosition` + AtSite Context 的落盘格式（Session-only vs Snapshot）。
5. `CurrentHex` 调用点分类审计（5R-C）：DerivedSurfaceHex vs RouteCommittedHex vs CurrentWildernessHex 的逐点归属与退役顺序。

## 迁移顺序（5R-0.1 修正 #10 拍板）

| 阶段 | 内容 |
|---|---|
| **5R-B1** | `WorldSiteSpatialMapping` shadow implementation（读 MapLayoutDefinition + footprint 真实几何，**不接行为**） |
| **5R-B2** | Site Local movement → Canonical WorldPosition sync：新增 `UpdateWorldPositionWithinSite(...)`（必须保持 AtWorldSite Context；Local transform → LocalToWorld → motion.WorldPosition） |
| **5R-B3** | `PlayerPartyWorldLocationQuery` pure-read：AtSite marker 使用 canonical WorldPosition；DerivedSurfaceHex pure derived |
| **5R-B4** | WorldPosition → Site Local materialization：不再 default spawn / Anchor snap（motion.WorldPosition → WorldToLocal） |
| **5R-B5** | Ingress / Egress continuity：Context change 不改变 physical position（复用 SurfaceExitConnection / WorldSiteFootprintExitConnectionResolver） |
| **5R-C** | CurrentHex call-site classification：DerivedSurfaceHex vs RouteCommittedHex vs CurrentWildernessHex，然后逐步退役 CurrentHex |
| **5R-D** | PresenceHex staged removal（零依赖后删字段 + invariant） |
| **5R-E** | CombatLocation=Site + SiteSupportRing1（authority 替换，保留 BattleArea=footprint / SupportArea=ring） |

之后恢复 Phase 5D Travel 功能开发（动态 MandatoryTransit 已保留）。
