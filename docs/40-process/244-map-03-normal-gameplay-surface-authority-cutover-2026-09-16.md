# MAP-03 — Normal Gameplay Surface Authority Cutover

> 日期：2026-09-16
> 状态：**Accepted / Sealed — 2026-09-17**
> 前置：[MAP-01](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[MAP-02](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)

## 目标

主 Continuous Surface 的正常 gameplay 以 exact `WorldPosition`、`SurfaceGroundNavigation`、continuous WorldSite context 与 Continuous Outdoor assets 为 authority。HexWorld 与 Outdoor LocalMap 仅保留给 legacy test content、旧存档迁移和 derived debug/compatibility metadata。

## 本轮实施

- **PlayerParty / Opening / WorldSite**：主 Surface 旅行由 `PlayerPartySurfaceTravelService` 从精确当前位置与目的地查询 `SurfaceGroundNavigation`，不以 HexPath 作为启动条件。最终到达按物理距离结算，成员 Presence 保持 `SurfaceId + WorldPosition`；opening 取 baked continuous anchor。正常 WorldSite 上下文由 continuous 行政/物理查询判定，不回退到 Hex footprint。WorldMap 关闭及 Snapshot active focus 不再装载普通 Outdoor LocalMap。
- **旅行存档**：PlayerParty active Surface 旅行保存精确目的地、到达半径与 Site 意图；Content shell 注册导航后按保存的起点重新求路。NPC 后台 Surface 旅行保存精确当前位置、SurfaceId 与 Site 目标，旧 HexPath 字段继续可读；恢复后重建地形路线。缺失新增字段的旧档按原路径读取。
- **FactionFlag**：正常建旗先以 `SurfaceId + WorldPosition` 验证地表和实际控制；`StrategicAnchor` 只从精确位置派生用于旧拓扑。新 runtime FlagId 使用阵营与持久 ID 序列，不嵌入 Q/R。FlagId、Site CoreAssetId 和 SiteId 是身份链；Hex index 只供兼容查询。读档时连续 Site-Core 不要求保存的 AnchorQ/R 在 Hex grid 内，改从存档精确坐标重建派生索引。破坏/重建按 FlagId 与 Site 关联处理。
- **NPC / Squad / FormalArmy**：NPC schedule 的实时精确位置路径保持现有产品行为；后台 NPC 到连续 Site 使用地表寻路与 authored Site arrival，逐段推进时写入个人精确 Presence。FormalArmy/Squad Site 命令同样以精确起点和 authored arrival 求 Surface route；成员从组织位置派生时附 SurfaceId，Site 初始化不再取 AnchorHex center。非 Outdoor Site 继续原逻辑。
- **Encounter / Battle**：正常 FormalArmy 与 PlayerParty 接触以同一 Surface 内的世界距离判断，不要求同格/邻格。BattleParticipantSnapshot 增加 `HasBattleAnchorWorldPosition`、`BattleAnchorWorldX/Y`、`BattleAnchorSurfaceId`，在 offer 冻结时从实际接触点记录，并以 additive JSON 字段读写。参战者战前精确 Presence 在复制与存档中保留；普通连续战后位置与残留以 world anchor 写回，Hex anchor 留给旧模式。独立战场、世界冻结、BattleOffer 与战报流程不重做。
- **Residual / Lingering**：弥留和尸体的个人 `SurfaceId + WorldPosition` 可独立成为稳定空间 authority；旧 residual Hex 仍兼容。Continuous WorldMap 的残留标记直接投影个人精确位置，不靠 Hex 栅格分组。角色生死状态和组织归属规则未改。

## 依赖审计与兼容边界

本次搜索覆盖 `HexWorld.HasGrid`、`WorldToHex`、`CurrentHex`、`DestinationHex`、`FinalDestinationHex`、`AnchorHex`、`PresenceHex`、`OccupiedHexes`、`BattleAnchorHex`、`StrategicAnchor`、`LocalMapId`、`mapLayout`，并按入口分类：

| 类别 | 当前保留含义 |
| --- | --- |
| Normal Authority | Surface navigation、exact WorldPosition、Site continuous arrival/control、个人 SurfaceId、Battle world anchor。 |
| Derived Compatibility | WorldToHex/CurrentHex、Site/Flag AnchorHex、BattleAnchorHex、WorldMap Hex 展示索引；这些不决定普通 Surface 路线、放旗合法性或战后精确位置。 |
| Legacy-only | `PlayerPartyHexTravelService` 的 HexPath/Footprint 旅行、`BackgroundCharacterTravelService` 的 Hex route、FormalArmy Hex route、`WildernessLocalMapFallback`、旧 Outdoor LocalMap、仅 Hex 的旧存档；由 Surface/Outdoor 分支隔离。 |
| 独立区域 | Interior、Cave、Dungeon/Separate Surface 继续允许独立 map transition 与 save/load；本次只退出主 Outdoor 的 LocalMap takeover。 |

仍可能在代码搜索中看到 Hex 字段和旧 LocalMapId；它们服务于派生索引与 legacy/独立区域，不是要求 grep=0。Runtime Chunk streaming、Surface seam 与真实地形寻路规则未修改。

## 验证与制作人验收

### Producer Acceptance Hotfix（已验收）

- Continuous Outdoor 的玩家交互拾取只命中实际物理对象；抽象 WorkArea/Housing anchor 不再以 6.5 world units 半径从空地触发详情。NPC 的 WorkTarget 解析仍可使用逻辑 WorkArea；旧 LocalMap 点击行为保留。
- Surface WorldMap 全图缩放按视口宽高比与当前投影公式计算 fit half；轴向可见范围达到世界半宽时归中，全图状态固定中心。世界绘制进入裁剪组，固定标题、工具栏、情报栏和底栏保持屏幕坐标。
- Surface Site 标记使用同 Surface 的 active Core 精确坐标，不凭 Site arrival 或 Hex 锚点伪造房屋。固定 Core 用房屋、FactionFlag Core 与独立旗帜用旗帜标记；标记命中与右键目标共用该位置。Surface 左键先选标记/单位/角色，再检查空地；右键 Site 传精确位置和 TargetSiteId。
- Surface 势力范围只读 `WorldSiteActualControlOverlayBuilder.BuildFactionUnion(world)` 的当前 Claim union；图层开关在 Surface 和 Legacy 模式均生效。装饰性正交坐标网格按缩放选择 1/2/5×10^N Surface Cell 间隔，不进入存档、寻路或拾取。Surface 情报默认文案及地形图例改用连续世界语义。
- 上述热修已纳入 2026-09-17 制作人人工验收。
- 热修仅运行 Core/Data/Unity Host 离线编译与 `git diff --check`；没有运行 Unity、PlayMode、Test Runner、batchmode 或大型自动化测试。

### Producer Acceptance Hotfix：WorldMap 布局与固定 SiteCore

- WorldMap 分成固定顶部操作区、占满可用宽度的地图视口、按需出现的右侧情报浮层；底部支援控制条仍固定。地图渲染统一使用 `BeginGroup(mapRect)` 内的局部 `Rect(0,0,width,height)` 投影；组结束后只将 marker 命中区域转成屏幕坐标一次。移除永久右栏和 `GUI.matrix` 平移抵消，详情开关不改变视口尺寸或镜头。浮层可点 X、按 Esc 关闭；空白 Surface 的坐标与地形也在浮层显示。坐标网格仅留稀疏线，删除 X/Y 数字。
- Surface 战略房屋严格代表同 Surface、active、不可拆卸的真实 Continuous Council Hall Core；可拆卸 Site 只有在解析到对应 FactionFlag 时画旗。Site arrival 只承担旅行抵达，不再补画假房屋；无 Core 的 Legacy Site 不在正常 Surface Map 伪造 marker。房屋和旗采用独立于实际建筑占地的稳定世界空间展示尺寸，随地图缩放。
- 青石镇、青石关、灵地、林间、庄院原先只有 Prototype 树。五棵树各自原有连续世界中心被用作固定议政厅 Core 中心：青石镇 `(20.50315,12.545)`、青石关 `(21.05308,16.92)`、灵地 `(38.72433,8.63)`、林间 `(12.30622,7.72)`、庄院 `(29.0335,5.315)`。每处建立独立 `controlCore` 放置、`base:loc_site_<站点>_core` SitePlace 和 `base:workarea_core_<站点>` ControlCore WorkArea；原 siteRegion arrival 保留且均在建筑阻挡范围之外。
- WorldComposer 兼容候选导出以当前运行时主世界为模板，只重建黄村放置。实际执行一次候选导出后，核对五个新 Core 及 SitePlace 全部保留；WorkArea 数据文件不由兼容发布替换。现有 `RebindPresetWorldSiteCoreMetadata` 负责给固定 Site 绑定 Core 和默认一级范围，继续使用既有 baseline Claim 初始化；Surface Actual Control 仍消费当前 Claim union。
- 本次运行 Core/Data/Unity Host 离线编译、BaseGame Content 加载校验、五个 Core/WorkArea/SitePlace 唯一性与边界静态校验、兼容候选 round-trip 核对及 `git diff --check`。开发侧没有打开 Unity 或运行 Unity Test；最终已由制作人人工验收。
- 战略 marker 的房屋与旗帜以精确 Core／独立旗帜世界坐标为中心，使用固定 28 Surface Cells 的世界空间展示宽高，经 Surface 投影得到随 zoom 缩放的屏幕矩形；这不是实体建筑占地。名称从图标右侧 8 Surface Cells 起排，字号按投影后的标签高度缩放并按字号缓存样式。命中区域直接使用投影后的图标矩形，在退出地图裁剪组后一次转换成屏幕坐标。Actual Control、道路、河流、地形及路线继续按世界空间投影；Header、底部控制条和情报浮层保持固定屏幕尺寸。缩放、平移时的视觉锚定与点击一致性已纳入制作人人工验收。

离线最小编译：`XianXia.Core`、`XianXia.Data`、`XianXia.Unity` 均通过，0 error；5 条既有 warning。`git diff --check` 通过。开发侧未运行 Unity、PlayMode、Test Runner、batchmode 或大型自动测试。

### Producer Acceptance — 2026-09-17

制作人已在 Unity 中完成人工验收并正式通过 MAP-03。确认范围：New Game continuous opening、exact Surface travel、旅行中 Save→Load 后恢复目标与旅行、Outdoor Site 进出不再切旧 Outdoor LocalMap；农田、储藏室、恢复处等 Continuous asset 可正常使用。FactionFlag 的精确世界位置放置、Save/Load 与控制正常；NPC／Squad continuous movement 与 encounter 未发现阻断。BattleOffer → 独立战场 → 战后精确世界位置回归、residual／downed 的世界空间表现、Interior／Cave 独立空间切换均正常。MAP-02 Surface WorldMap 没有回退 Hex。

后续 WorldMap 验收热修也已通过：抽象 WorkArea 不再被空地误点、全图 Fit 镜头稳定、视口裁剪正常、固定 Header 与按需情报浮层正常、Actual Control 与 Site／FactionFlag marker 正常、永久 WorldSite controlCore 正常。此处只记录制作人确认的人工结果，不将其扩展为自动化测试结论。

## 最终 authority 边界

主 Continuous Outdoor 的正常 Gameplay authority 是 exact `WorldPosition`、`SurfaceId`、`SurfaceGroundNavigation`、Continuous SiteCore、Actual Administrative Control 与 Continuous asset placement。

`CurrentHex`、`DestinationHex`、`FinalDestinationHex`、`HexPath`、`AnchorHex`、`PresenceHex`、`OccupiedHexes`、`StrategicAnchor`、`BattleAnchorHex`、Outdoor `LocalMapId` 和旧 `mapLayout` 已降级为 **Legacy / Derived Compatibility**。它们仍可能存在于代码、Save 与 Content 中，但不再是 Main Continuous Surface 正常 Gameplay 的必要 authority。MAP-04 可以据此开始物理清理前的 consumer audit；本次封板不删除兼容实现或旧 Content。

## 版本控制

MAP-03 已于 2026-09-17 完成制作人人工验收并封板。已验收的正式代码与 Content 存在于当前分支的先前提交；本轮封板提交更新正式文档，不混入本地生成物。

## MAP-04 Cleanup Candidates

以下仅为后续 consumer audit 的输入候选，不代表已获删除授权。

### Hex Runtime / Data

- `HexWorld`、旧 HexWorld JSON。
- `HexCoord` 正常路径兼容字段与 Hex travel fallback。
- Hex battle anchor fallback、Hex Site footprint compatibility。

### Outdoor LocalMap

- Outdoor LocalMap materialization、wilderness fallback `mapLayout`。
- Site 旧 `mapLayout`、LocalPlace outdoor compatibility、`sourceLocalMapId`。

### Authoring Tools

- `WorldGraphEditor`、`MapEditor` 的 Outdoor 职责、`RegionEditor`。
- `LocalPlaceEditor` 中已无消费者的 Outdoor 职责。

### Content

- `ch01_reference_map` 等仅供 migration／fallback 的旧 Outdoor Source。
- fallback maps、W2A temporary authoring artifacts、obsolete Hex summaries。

MAP-04 必须先逐项审计消费者与存档兼容要求，之后才能决定保留、迁移或删除；本轮没有开始 MAP-04 实施。
