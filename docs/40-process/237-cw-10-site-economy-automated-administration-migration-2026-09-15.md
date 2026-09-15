# CW-10：Site Economy / Automated Administration Migration

> 状态：Implementation Completed / Producer Acceptance Pending｜优先级：P0｜最后更新：2026-09-15

## 目标与权威

CW-10 将旧 VS0.8 `Settlement` 原型迁移到当前 Continuous World / WorldSite 行政架构。每个 WorldSite 的公共资源以 `SiteId` 为稳定身份，`WorldSitePublicStockBoard` 保存数量，政治归属始终从 `WorldSite.OwnerFactionId` 读取。固定据点易主不会替换公库；可拆旗摧毁后原 Site inactive，但公库和历史 Site 仍保留；玩家之后新建的旗产生新 Site 和空公库。

青石荒村 authored 初始公库为粗木 10、灵草 2、粮食 0、敛息草 3。该默认值来自 `worldSiteEconomy` Content，只在 NewGame 或缺少公库 authority 的旧档迁移时应用一次。新格式损坏会失败，不回退默认值掩盖错误。

## 自动行政与农作

农田 cell 的行政锚点保留稳定 cell identity、精确 Surface 世界坐标，并增加只作物理分组的 `BoundLocationId` 索引。NPC 日程解析到 Labor + farm WorkArea 时，必须拥有真实 `FactionMembership`，且该地点至少有一个由其势力当前获准管理的真实农田 cell。

Host 只表现真实 NPC 的移动和逐格劳动。选格、移动、开工及收获提交前都重新调用 `WorldAdministrativeAssetAuthorizationService.ResolveForFaction`；权限失效立即释放预约并停止或换到仍合法的格。自然生长仍由 Core WorldTick 推进所有 farm state，不因此凭空产出资源。

成熟作物由真实 NPC 收获时，`WorldSiteFarmHarvestService` 在提交点再次读取人物势力和当前 Managing Site，产物进入该 Site 公库。同势力 Site A→B 接续后进入 B。玩家手动收获继续进入 Party Inventory。Capture 不改 NPC FactionMembership，因此原敌方农民在据点易主后失去新 Owner 的组织劳动授权，除非后续人物政治系统明确改变其身份。

## UI、事件与持久化

议政厅与势力旗 Inspect 直接按绑定 Site 显示实时公库，资源名称来自现有 InventoryCatalog。农田 Inspect 继续只展示当前行政管理方。公库数量变化发布独立 `WorldSitePublicStockChanged` 事件，不借用旧 Settlement 生产语义。

Snapshot v6 增加 additive `worldSitePublicStocks` authority。新保存按 SiteId、ResourceId 序列化排序，并为每个 Site 写一条记录，包括空公库；恢复严格检查 Site 存在、Site/Resource 不重复和数量非负。旧档缺字段在 Content shell 与政治状态恢复后幂等应用 authored defaults；存在新 authority 的存档不会再套默认值。

## 旧原型退役

已删除 `SettlementState/Board/Service/ProductionHandler`、`FacilityRuntime`、`SettlementBootstrap`、`WorkAssignmentComponent/WorkRoleKind`、对应 Data definition/loader/schema/reference validation、BaseGame facility/settlement JSON、opening `openingSettlementId/workRole` 和 Host AssignWork／HUD／debug formatter。`SimulationWorld.Settlements` 与默认日终 SettlementProduction 也已移除。

保留 `SettlementAuthorityBoard/Sync`，因为它仍是现有住房与课表权限 bridge；保留 `ManualBattleSettlementState` 和 Demo Runtime 的 `PartyCommandController.AssignWorkToSpot`，二者不属于退役经济原型。BattleOffer、FormalArmy、SiteCore Warfare 与 CW-05 农田物理 identity 均未扩改。

## 正常验收 Content

- 固定据点：**青石荒村**（`base:site_huangcun`）及议政厅。
- 真实农民：**阿土**、**阿禾**，使用现有 NPC Schedule、FactionMembership 和真实 Entity。
- 农作地点：**农田杂役区**（`base:loc_ref_labor_yard`），包含现有 grainField；青石荒村同时保留现有 herbField。
- 初始公库：粗木 10、灵草 2、粮食 0、敛息草 3。

没有新增作弊按钮、运行时验收生成器或离屏生产器。

## 验证

- `tools/offline-compile.ps1`：Core、Data、Unity Host、Unity Editor、Tests、PlayModeTests、Assembly-CSharp、Assembly-CSharp-Editor 编译级验证。
- `SiteEconomyMigrationTests` 与 `ConstructibleFarmLifecycleTests` 的 Mono headless 定向验证，覆盖 authored defaults、幂等旧档 fallback、独立事件、固定易主公库 identity、inactive Site 保留、新旗空公库、确定性 Snapshot、损坏新格式拒绝、真实 NPC 授权、同势力 Site 接续、他方接管拒绝、玩家背包隔离和自然生长无 phantom production。
- BaseGame strict Content reference validation，确认青石荒村 economy、农田 WorkArea、阿土／阿禾与真实 farm anchors。
- 定向静态搜索确认正常代码与 Content 不再引用退役 Settlement/Facility/WorkAssignment/AssignWork 原型；`git diff --check`。

本轮不运行 Unity、EditMode／PlayMode TestRunner、batchmode 或 Bake。Unity 正常玩法体验等待制作人验收。

## 制作人人工验收

1. 启动 `base:scenario_ch01_reference`，左键青石荒村议政厅，确认据点公库显示粗木 10、灵草 2、粮食 0、敛息草 3。
2. 观察阿土／阿禾在农田杂役区按日程进入真实田格劳动；成熟粮食收获后，议政厅公库粮食增加，队伍背包不增加。
3. 按现有正常玩法攻破并占领青石荒村；原农田、公库数量和 NPC 身份均保留，NPC 因仍属原势力而停止对玩家据点的组织农作。
4. Save / Load，确认新 Site Owner、公库数量、原农田状态和 NPC 身份保持。
5. 对可拆前哨验证旗被摧毁后原 Site 公库不消失；正常新建己方旗后，新 Site 公库为空。

No commit created.
Changes remain uncommitted for producer review.
