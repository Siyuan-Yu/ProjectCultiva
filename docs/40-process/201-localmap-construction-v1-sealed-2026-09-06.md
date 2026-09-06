# LocalMap 建造系统 V1 封板记录

> 状态：**已实现／已人工验收／已封板**
> 日期：2026-09-06
> 系统真源：[2L](../20-systems/2L-local-map-construction-v1.md)
> 架构决策：[ADR-0029](43-decisions/ADR-0029-construction-content-runtime-and-snapshot-boundary.md)

## 1. 背景

LocalMap 建造系统为玩家提供独立于背包的 RPG 建筑入口。V1 复用已封板的 FactionFlag、Territory Resolver、EstablishedOrder、LocalMap placement geometry、FactionFlag Snapshot 与 PartyInventory，不创建第二套阵营旗或领土合法性规则。

## 2. 最终产品规则

- 建造与 Inventory 是两个不同系统；建筑不是物品，也不存在可堆叠的建筑道具。
- Inventory 只提供建造材料。V1 唯一材料 authority 是 Player Party Inventory。
- `BuildingDefinition` 是独立的 Construction Content；Core 运行时通过 `ConstructionCatalog` 读取静态规格。
- 当前第一种建筑为 `base:building_faction_control_post`，显示名为“势力控制建筑”。
- 建造成本为 `base:resource_rough_wood × 10`，即粗木 ×10。
- 建造和主动拆除均瞬时完成，不包含施工进度、人员或中间建筑状态。

## 3. 数据权威

Data 层 `BuildingDefinition` 保存名称、说明、默认解锁、`placementKind`、材料成本和主动拆除返还率。启动新游戏及读档恢复静态内容壳时，`ContentRuntimeBootstrap`／`RuntimeContentShellBootstrap` 将其映射到 Core `ConstructionCatalog`。

`ConstructionCatalog` 是静态运行时内容壳，不是存档 authority。V1 只显示 `UnlockedByDefault == true` 的建筑，不创建解锁进度 Board。

## 4. 正式 UI 结构

顶部 HUD 的 `[地图] [建筑] [背包]` 是三个同级全局入口：

- `[地图]` 打开 `HostWorldMapPanel`。
- `[建筑]` 打开独立 `HostConstructionPanel`。
- `[背包]` 或 B 打开纯背包 `HostInventoryPanel`。

三个主面板互斥。建筑面板只读取 Inventory 数量用于展示材料是否足够，不属于背包子页。旧 LocalMap 免费“立阵营旗／选择立旗位置”入口已退出正式 Gameplay。

## 5. 建造事务

`HostConstructionPanel` 只负责展示和调用 `HostConstructionController.BeginPlacement`；Host 不直接组合扣料与 FactionFlag mutation。最终提交由 `ConstructionService.TryConstructFactionFlag` 负责：

1. 解析建筑规格并检查默认解锁与材料。
2. 调用 `FactionFlagService.ValidatePlacement` 完成 Domain preflight。
3. 扣除完整材料。
4. 调用 `FactionFlagService.TryPlace` 创建真实 FactionFlag。
5. 若意外创建失败，完整回滚已扣材料。

因此成功结果为 FactionFlag 存在且粗木精确减少 10；失败或取消时不创建 FactionFlag，材料保持不变。

## 6. Placement 与 FactionFlag 接入

建筑页面可以在任何已加载 LocalMap 打开，地点合法性不提前影响建筑卡片。进入 placement 后：

- WorldSite／Interior 保持 placement 状态并显示红色非法提示：“势力控制建筑只能建造在野外区域。”
- Enemy Effective Territory、重复 Anchor、WorldSite 范围及无法产生 Neutral expansion 等情况继续由 FactionFlag Domain 返回原因。
- 合法 Wilderness 同时满足 geometry 与 Domain gate 时显示绿色预览并允许提交。

FactionFlag placement rule 始终由 FactionFlag Domain authority 决定。Construction 不复制 Territory Resolver、EstablishedOrder earlier-first-claim、Nominal overlap 或 Effective Territory 规则。

## 7. 主动拆除事务

主动拆除只允许玩家势力自己的控制建筑。右键后需要确认，`ConstructionService.TryDismantleFactionFlag` 按每项 `floor(cost × refundRate)` 计算返还；当前粗木 10、返还率 0.5，因此返还粗木 5。

服务在删除建筑前无副作用检查 PartyInventory 能否完整容纳全部返还材料。容量不足时拒绝拆除，建筑、领土与 Inventory 均不变化。成功时移除 FactionFlag、重建 Territory 并完整返料。现有 authored 玩家 FactionFlag 与玩家运行时建造的 FactionFlag 使用同一规则，不按剩余 HP 折损返还。

Combat Destroy 与主动拆除严格分离。战斗摧毁继续调用 `FactionFlagService.TryDestroy`，返还为 0；敌方建筑不显示“拆除”。

## 8. 存读档

V1 不增加 `ConstructionSnapshotDto`。建造为瞬时事务，成功后的持久状态由既有两套权威共同表达：

- PartyInventory Snapshot 保存扣料或返料结果。
- FactionFlag Snapshot 保存建筑存在、移除及其位置状态。

读档后静态 `ConstructionCatalog` 从 Content 重新 hydrate，因此建筑页面无需保存 Definition 副本也能正常显示。

## 9. 人工验收结论

制作人已确认以下行为通过人工验收：

- 顶部 `[地图] [建筑] [背包]` 同级且互斥；背包内没有建筑页签，B 只控制背包。
- 建筑面板显示势力控制建筑、粗木数量、成本 10、返还率 50% 与正确按钮状态。
- WorldSite／Interior 和敌方有效领土能进入 placement 并显示明确红色非法原因，不扣材料。
- 合法 Wilderness 建造后建筑出现、粗木减少 10、Territory 立即更新；取消不扣材料。
- 己方建筑主动拆除后返还粗木 5；敌方或 Combat Destroy 不返还。
- Save → Load 后 FactionFlag、Inventory 与建筑目录保持连续。
- 多攻击者摧毁同一地图目标时的 session 越界回归已修复并通过人工验收。

## 10. 自动验证

- `Shared.Tests`：59/59 通过。
- `WorldGraphEditor` Release：0 warning／0 error。
- Unity Core／Data／Host／EditMode Tests：使用项目现有 Bee response files 离线编译，0 error；保留既有 obsolete／unreachable／unused-field warning。
- Construction 定向测试：本阶段 10/10 已通过；封板时 Unity Editor 正在打开并持有项目锁，因此未启动第二个 batchmode Unity 重复运行。
- `git diff --check` 与 staged diff 检查：提交前执行。

## 11. 后续方向（不属于 V1）

- 建筑解锁：建筑配方／蓝图、剧情解锁、技能或身份解锁。
- 施工 V2：建造耗时、拆除耗时、施工人员、`ConstructionSite`、中断／恢复与施工进度存读档。
- 资源系统：WorldSite 仓库、共享材料、物流与自动取材。V1 仍只使用 Player Party Inventory。
- 建筑种类：自动防护、阵法／临阵设施、储物设施、工作设施及其它 LocalMap 建筑。

上述内容只作后续方向记录，本轮不实现。

## 12. 封板规则

除明确 Bug／Regression 外，后续开发必须建立在本页 V1 baseline 上，不得把建筑改为 Inventory Item、恢复免费立旗入口、让 Host UI 自行拼装建造事务、为 Construction 维护第二套 Territory legality、让 Combat Destroy 返料，或擅自扩张 Snapshot 契约。
