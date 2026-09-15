# 239｜CW-10.5 战略物资访问与储藏室（2026-09-15）

## 目标与 authority

本轮在既有据点经济上闭合玩家资源访问层，不新增第三份持久库存：

- `PartyInventory` 是小队真实随身物品，继续受 SlotCapacity 限制；采集、拾取和玩家手动收获仍进入背包。
- `WorldSitePublicStock` 是按稳定 `SiteId` 保存的行政公库；NPC 农业产出仍进入当前 Managing Site 公库。
- `StorageRoom` 是据点公库接入战略网络的物理设施，不拥有第二份 WarehouseInventory。
- `PlayerStrategicResourceService` 是实时派生的查询与事务消费层，不进入 Snapshot。

## 访问与共享规则

玩家只有当前处于 active、player-owned 的 Actual Managing Site 时才能接入网络。Continuous Outdoor 优先使用并重新验证 `PlayerPartyTravel.CurrentOutdoorWorldSiteId`；正式 `AtWorldSite` context 可使用其 `SiteId`。不以 Hex、理论 Core range、TerritoryRegion 或最近据点猜测权限。

接入后，所有同时满足 active、player-owned、has StorageRoom 的 Site Public Stock 全局共享；enemy、inactive、no-storage Site 的公库保留但 offline。离开全部己方 Actual Control 后，只能查询和消费 PartyInventory。占领只改变同一 WorldSite 的 Owner，因此同一 Public Stock 与 StorageRoom 会在下一次实时查询中自动加入；失去据点或核心 inactive 时自动退出，不复制、不删除库存。

## 消费事务

只聚合 `InventoryCatalog.HasTag(itemId, "resource")` 的物资。消费先确认总量并生成完整 withdrawal plan，再按以下稳定顺序扣除：

1. 当前 Actual Managing Site 公库（该 Site 有 StorageRoom 时）；
2. 其他 eligible Site，按 `SiteId` ordinal 排序；
3. PartyInventory。

任一扣除或随后世界对象提交失败，按 receipt 逆序回滚已扣来源。非 resource 物品始终只读写背包。

## 储藏室

- Building：`base:building_storage_room`，3×3、粗木 10、默认解锁、不创建 WorldSite、无拆除返还。
- MapKind：`storageRoom`，复用 `Warehouse.prefab`，SingleCentered，阻挡移动。
- 荒村 authored 实例：`base:site_huangcun:storage_room`（“荒村储藏室”），位于 source grid `(87,45)` 的议政厅右侧空地，world rect `(6.21373,11.075) + (0.06495,0.105)`，完整落入 canonical chunk `(5,8)`；其 `chunkX/Y` 必须等于 physical center 的 canonical Surface chunk，确保 `SingleCentered` Host materialization 有且仅有一个 owner；严格 Content 验证每 Site 最多一座 authored StorageRoom。
- runtime ID：`asset:runtime:storage:<sequence>`，继续共用 `OutdoorConstructedAssetBoard.NextSequence`；Location 为 `location:runtime:storage:<id>`。
- runtime StorageRoom 的整个 footprint 必须落在同一个 Actual Managing Site，且 Owner 与 acting faction 一致；`BoundWorldSiteId` 保存稳定 Site binding。一 Site 已有 authored/runtime StorageRoom 时拒绝重复建造。
- `WorldSiteStorageRoomBoard` 只保存物理 facility lookup，由专职 `WorldSiteStorageRoomBootstrap` 从 authored placement 与 runtime constructed asset 在 NewGame/Restore 时重建；自身不持久化资源。Farm administrative anchor bootstrap 不再承担 StorageRoom。
- 左键统一 Inspect Panel 显示所属据点、当前控制、网络/封存/他方状态与该 Site 的实时 Public Stock；右键当前只有“查看详情”。议政厅/势力旗改显示储藏设施与公库摘要，不再表现成核心建筑自身存货。

## HUD 与消费者

顶部始终显示真实背包已用槽/容量。处于己方 Actual Control 时，资源段标为“战略物资”，显示背包加全部 online Site 公库；范围外标为“随身物资”，只显示背包。Inventory Panel 保持真实背包内容，不注入 Site Stock。

本轮迁移的 resource consumers：

- `ConstructionService` 的材料查询与正式扣除；因此农田、恢复处、储藏室及势力旗自然服从当前位置 access gate，没有 FactionFlag 特例。
- `SkillMasteryService` 的突破材料查询与扣除。
- `UseConcealGrass`。
- 建筑面板与 Skill Mastery 对应 have/need 显示。

秘籍、斗技秘本、装备、任务物和其它非 resource item 仍为 bag-only；任务 `stockAtLeast`、has-item 与交付语义未迁移。

## 持久化与兼容

只给既有 `OutdoorConstructedAssetSnapshotDto` 增加 optional `BoundWorldSiteId`，JSON 缺字段时读取为空；旧 Farm/Recovery 资产不受影响，不升级 schema。StorageRoom Restore 必须能解析真实 Site。持久 authority 仍只有 PartyInventory、WorldSitePublicStock 与 runtime physical placement；聚合总量、network access 与 StorageRoomBoard 均不存档。

## 明确延期

V1 未实现仓库容量、玩家手动存取、物流/运输、仓库摧毁、仓库产权、库存掉落、普通建筑拆除，以及任务 stock 语义迁移。

## 验证记录

- 严格 Content load/reference validation：通过；荒村一座 StorageRoom，Building 为 3×3/木10。
- 定向离线测试 `StrategicResourceAndStorageRoomTests`：4/4 通过，覆盖共享查询、稳定消费顺序、rollback、enemy/inactive/no-storage/off-territory 排除、capture 实时接入、optional JSON 字段与 NewGame registry。
- 离线编译：Core、Data、Unity Host、Unity Editor、Tests、PlayModeTests、Assembly-CSharp、Assembly-CSharp-Editor 全部成功，0 error；18 条为工作区既有 warning。
- `git diff --check`：通过。
- 未运行 Unity Test Runner、PlayMode suite、batchmode、Bake 或 Unity 人工验收。
