# LocalMap 建造系统 V1

> 状态：**已实现／已人工验收／已封板** | 优先级：P0 | 最后更新：2026-09-06
> 依赖：2K、2A、FactionFlag Domain、PartyInventory
> 封板记录：[201](../40-process/201-localmap-construction-v1-sealed-2026-09-06.md)

## 1. 边界

Construction 是独立于 Inventory 的 RPG 建筑入口。建筑不是物品；Inventory 仅提供材料。V1 只开放默认解锁的“势力控制建筑”，但目录与服务按建筑列表设计。

- 静态内容：`BuildingDefinition` 经 Content Pipeline 映射为 Core `ConstructionCatalog`。
- 运行时提交：建造立即生成真实 `FactionFlagState`；拆除立即移除该状态。
- 持久化：只复用既有 FactionFlag Snapshot 与 PartyInventory Snapshot；不新增 Construction Snapshot。
- 不包含：解锁进度、施工时间、工人、维修、AI 建造、地面返料。

## 2. 势力控制建筑

- Definition：`base:building_faction_control_post`
- PlacementKind：`factionFlag`
- 成本：`base:resource_rough_wood × 10`
- 主动拆除返还率：50%，逐项 `floor(cost × rate)`。
- 所有现存玩家所属 FactionFlag（包括 Content authored）均视为此建筑；不记录来源或历史成本。

建造入口为全局 HUD 的独立 `[建筑]` 面板。它与 `[地图]`、`[背包]` 是同级入口，不属于背包页签。旧的无条件免费立旗入口退出正式 Gameplay。Host 只负责 UI、预览、输入与几何；Core `ConstructionService` 负责材料和 FactionFlag 领域提交。

## 3. Placement

玩家在任意已加载 LocalMap 都能进入 placement。几何预览与领域合法性分层：

- WorldSite／Interior：模式保持、可取得布局时继续显示红色预览，并明确提示只能在野外 LocalMap 建造。
- Wilderness：复用 `FactionFlagService.ValidatePlacement`；不复制 Anchor、WorldSite、重复 Flag、敌方有效领土、Neutral gain、EstablishedOrder 或 Territory Resolver 规则。
- 只有 geometry 与 domain 同时合法时左键才提交；Esc／右键取消，不扣材料。

## 4. 事务

建造先完成目录、解锁、材料、placement kind 与 FactionFlag placement 全部 preflight，再扣完整材料并调用既有 `FactionFlagService.TryPlace`；意外失败必须回滚材料。

主动拆除只允许玩家势力自己的 Flag。服务先按当前 BuildingDefinition 计算返还，并无副作用验证背包能完整容纳，再调用既有 destroy 和完整返料。容量不足时 Flag、领土、Inventory 均不变化。战斗摧毁继续只走 `FactionFlagService.TryDestroy`，永不返料。

## 5. 表现入口

`HostFormalHud` 提供同级 `[地图] [建筑] [背包]` 按钮；三者互斥。`HostInventoryPanel` 只负责背包，B 只切换背包；`HostConstructionPanel` 独立显示已解锁建筑卡片。点击建造会同步关闭建筑面板并释放其暂停与输入所有权，再由 `HostConstructionController` dispatch 到 FactionFlag placement presenter。新游戏与读档重建时，Bootstrap 都会清理并重新绑定这两个独立面板。

## 6. V1 封板边界

- 建造与拆除均瞬时完成，不存在施工进度或中间态。
- 建造合法性只由既有 FactionFlag Domain authority 决定；Construction 不维护第二套 Territory legality。
- 成功后的持久状态由 PartyInventory Snapshot 与 FactionFlag Snapshot 共同表达，不增加 Construction runtime snapshot。
- 旧的免费立旗入口已退出正式 Gameplay；正常入口唯一为 `[建筑]` → 势力控制建筑 → `[建造]`。

## 7. 后续方向（不属于 V1）

- 建筑解锁：配方／蓝图、剧情、技能或身份解锁。
- 施工 V2：建造与拆除耗时、施工人员、`ConstructionSite`、中断／恢复及进度存档。
- 资源系统：WorldSite 仓库、共享材料、物流与自动取材；V1 只使用 Player Party Inventory。
- 建筑种类：自动防护、阵法／临阵设施、储物设施、工作设施及其它 LocalMap 建筑。
