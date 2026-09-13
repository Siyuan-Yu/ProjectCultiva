# LocalMap 建造系统 V1

> 状态：旧 Wilderness-only V1 已验收；CW-03 旗创建 Site 已实现／制作人验收待完成 | 优先级：P0 | 最后更新：2026-09-13
> 依赖：2K、2A、FactionFlag Domain、PartyInventory
> 封板记录：[201](../40-process/201-localmap-construction-v1-sealed-2026-09-06.md)

> **2026-09-13 当前状态：** 最新建设范围与 SiteCore 生命周期见 [ADR-0032](../40-process/43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md)。CW-03 已接通“正常建筑入口→Continuous 鼠标真实落点→材料与新 Site／唯一旗核心一次提交→表现／地图→存读档→拆旗失效”；复杂重叠扩张与资产管理接续仍后置。制作人尚未验收 CW-03。

## 1. 边界

Construction 是独立于 Inventory 的 RPG 建筑入口。建筑不是物品；Inventory 仅提供材料。V1 只开放默认解锁的“势力控制建筑”，但目录与服务按建筑列表设计。

- 静态内容：`BuildingDefinition` 经 Content Pipeline 映射为 Core `ConstructionCatalog`。
- 运行时提交：创建 Site 的核心建筑会在同一事务内生成 runtime `WorldSite` 与唯一关联 `FactionFlagState`；拆除令核心与该 Site 的有效控制失效。
- 持久化：复用 Strategic Snapshot，同时保存 runtime Site、关联核心与 PartyInventory；不新增独立 Construction Snapshot。
- 不包含：解锁进度、施工时间、工人、维修、AI 建造、地面返料。

## 2. 势力控制建筑

- Definition：`base:building_faction_control_post`
- PlacementKind：`factionFlag`
- 成本：`base:resource_rough_wood × 10`
- 主动拆除返还率：50%，逐项 `floor(cost × rate)`。
- 运行时新建且可拆的关联核心按此建筑处理。预设 Site 的议政厅／ControlCore 不进入通用拆旗返料流程；旧孤立旗按兼容边界处理。

建造入口为全局 HUD 的独立 `[建筑]` 面板。它与 `[地图]`、`[背包]` 是同级入口，不属于背包页签。旧的无条件免费立旗入口退出正式 Gameplay。Host 只负责 UI、预览、输入与几何；Core `ConstructionService` 负责材料和 FactionFlag 领域提交。

## 3. 当前 Site 建设目标（2026-09-12）

### 3.0 最新范围规则

放置许可按**候选建筑占地／预览位置**解析唯一有效的 Site 行政／建设范围，不能偷用主控当前站立点替代远处候选。河流、水面和岸边不从管辖区统一裁掉；能否建造由具体 `BuildingDefinition` 的地形、占地、碰撞和许可共同决定。核心理论覆盖允许重叠，但一个建筑不能同时向两个 Site 生产或计数。

- 普通建筑：候选占地必须已经位于合法行政／建设范围内。
- 创建新 Site 的势力旗：是核心建立例外，不循环要求候选处先存在新 Site；仍须满足成本、占地／碰撞、地形、不得在敌方有效控制内、不得覆盖其他核心实体，并能产生合法新增 claim 等前置。
- 议政厅：预设 Site 的不可拆 SiteCore，不通过普通拆除流程移除。

### 3.1 CW-03 已实现边界

`BuildingDefinition` 的 `createsWorldSite`、`initialSiteLevel` 与 `siteRangeWidth/siteRangeHeight` 进入 Content loader、`ConstructionCatalog` 和正式提交。BaseGame 当前一级范围为 `4.2 × 2.8` 世界单位，是调参值。Host 预览先把鼠标 presentation 点经当前 Continuous mapper 转成 Surface 世界坐标和战略摘要 Hex；提交重新验证同一请求，不使用玩家脚下 Hex 代替落点。

成功提交同时产生稳定 runtime SiteId 和唯一关联 FlagId；Site Owner 是政治权威，关联旗的 Faction 字段由统一 Owner 写入口同步。旗不再作为与 Site 并列的第二个 Territory source。动态 Site 按 Surface／真实矩形范围提供基础管理查询，Hex 只保存地图和兼容摘要。同 Hex 多 Site 不互相覆盖；CW-03 在无争议空地暂时拒绝范围重叠，完整历史优先和接续属于 CW-04/05。

拆旗或合法摧毁会令该 runtime Site 核心失效并撤销控制，保留 Site 历史身份及其他资产；主动拆除仍按旧配置返料一次。动态 Site 与关联核心进入正式 Snapshot，旧档缺字段可读；只有同时具备可靠 Surface＋世界位置的旧孤立旗才可幂等迁移，缺失空间信息时保留兼容旗并输出诊断。

## 4. 历史 Wilderness-only Placement V1（已验收基线）

> 本节只描述 2026-09-06 版本。其“WorldSite／Interior 只能红色提示、只能在 Wilderness 建造”不再是当前永久产品规则；费用十木材、返料与材料事务仍保留为该版本事实，不锁定未来建筑成本。

玩家在任意已加载 LocalMap 都能进入 placement。几何预览与领域合法性分层：

- WorldSite／Interior：模式保持、可取得布局时继续显示红色预览，并明确提示只能在野外 LocalMap 建造。
- Wilderness：复用 `FactionFlagService.ValidatePlacement`；不复制 Anchor、WorldSite、重复 Flag、敌方有效领土、Neutral gain、EstablishedOrder 或 Territory Resolver 规则。
- 只有 geometry 与 domain 同时合法时左键才提交；Esc／右键取消，不扣材料。

## 5. 事务

建造先完成目录、解锁、材料、placement kind 与 SiteCore placement 全部 preflight，再扣完整材料并以单一事务注册 runtime Site 与关联 Flag；意外失败必须回滚材料且不得留下半个 Site 或孤立核心。

主动拆除只允许玩家势力自己的 Flag。服务先按当前 BuildingDefinition 计算返还，并无副作用验证背包能完整容纳，再调用既有 destroy 和完整返料。容量不足时 Flag、领土、Inventory 均不变化。战斗摧毁继续只走 `FactionFlagService.TryDestroy`，永不返料。

## 6. 表现入口

`HostFormalHud` 提供同级 `[地图] [建筑] [背包]` 按钮；三者互斥。`HostInventoryPanel` 只负责背包，B 只切换背包；`HostConstructionPanel` 独立显示已解锁建筑卡片。点击建造会同步关闭建筑面板并释放其暂停与输入所有权，再由 `HostConstructionController` dispatch 到 FactionFlag placement presenter。新游戏与读档重建时，Bootstrap 都会清理并重新绑定这两个独立面板。

## 7. V1 封板边界（历史）

- 建造与拆除均瞬时完成，不存在施工进度或中间态。
- 建造合法性只由既有 FactionFlag Domain authority 决定；Construction 不维护第二套 Territory legality。
- 成功后的持久状态由 PartyInventory 与 Strategic Snapshot 中的 runtime Site／关联核心共同表达，不增加 Construction 专用 snapshot。
- 旧的免费立旗入口已退出正式 Gameplay；正常入口唯一为 `[建筑]` → 势力控制建筑 → `[建造]`。

## 8. 后续方向（不属于旧 V1）

- 建筑解锁：配方／蓝图、剧情、技能或身份解锁。
- 施工 V2：建造与拆除耗时、施工人员、`ConstructionSite`、中断／恢复及进度存档。
- 资源系统：WorldSite 仓库、共享材料、物流与自动取材；V1 只使用 Player Party Inventory。
- 建筑种类：自动防护、阵法／临阵设施、储物设施、工作设施及其它 LocalMap 建筑。
