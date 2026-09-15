# ADR-0032：SiteCore、实际行政控制与建设范围

> 状态：已采纳（CW-04／CW-04.5 已封板；CW-05A/B/Closing Producer Accepted / Sealed）
> 日期：2026-09-12
> 关联：[24](../../20-systems/24-world-and-settlements.md)、[26](../../20-systems/26-territory-management.md)、[2J](../../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)、[2L](../../20-systems/2L-local-map-construction-v1.md)、[ADR-0029](ADR-0029-construction-content-runtime-and-snapshot-boundary.md)、[ADR-0031](ADR-0031-continuous-outdoor-world-surface-architecture.md)

## 背景

Continuous Outdoor World Surface 已确定为普通户外的物理世界，但旧文仍把 `WorldSite.FootprintHexes`、LocalMap、领土色和真实建设范围混为一体，也存在“范围禁止重叠”“拆旗等于删除其上资产”等冲突描述。

## 决策

1. V1 一个 `WorldSite` 只有一个 `SiteCore`。预设 Site 的议政厅固定存在、不可拆除或删除，可以升级，并可通过正式战斗与交互接管；核心防御被攻破不等于核心实体被摧毁。
2. 玩家建造的势力旗可成为一个新 Site 的核心；它可以升级、拆除或摧毁。另立旗产生新 Site，不是给原 Site 增加第二核心。
3. 核心等级决定理论覆盖。配置值是内容参数，不把 200×100 等示例写成全局常量。
4. 行政管辖与允许建设使用同一范围；建筑自身再判断地形、占地、碰撞和许可。河流不从管辖范围统一扣除。
5. 理论覆盖允许重叠。每个位置和建筑只能有一个实际行政管理者；同势力面积按并集统计，不重复生产或计数。
6. 不同势力保留已经取得的有效控制；核心扩张不得追溯抢占他方既有控制。同势力多个 Site 也采用稳定的有效先占／交接结果。具体历史数据表达和平局算法在实现前定向核对。
7. 接管议政厅整体移交该 Site 的既有实际行政控制，保留 Site ID、名字、等级、核心位置和实体建筑；不生成替代城市，也不借易主吞并其他 Site 的控制。
8. 拆旗只移除该核心的控制声明并重算覆盖。建筑、库存、生命、损伤和占地继续存在；有其他有效 Site 时接续管理，无接续时只暂停依赖行政管理的功能。
9. Site Owner／范围变化不移动人物，不改其 HomeSite、Faction、忠诚、关系、日程、出生点、移动命令或玩家控制权。当前 Site 是由世界位置与有效控制解析的上下文，不是第二份位置真源。
10. Hex 保留战略叠加和摘要职责，但 Hex terrain、footprint 或格心不得规定真实河桥通行、人物精确位置、Site 精确边界或战场裁切。

## 替代范围

- 部分替代 [ADR-0025](ADR-0025-strategic-spatial-model-hexgrid.md) 及 [2J](../../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md) 中把 Hex footprint 当作真实物理／行政精确范围、禁止重叠或按核心出生时间重排全部新版范围的条款；Hex 战略叠加仍保留。
- 补充并部分替代 [ADR-0029](ADR-0029-construction-content-runtime-and-snapshot-boundary.md) 的旧阶段范围限制；其 BuildingDefinition、材料事务和 Snapshot 边界保持。
- 补充 [ADR-0031](ADR-0031-continuous-outdoor-world-surface-architecture.md)：Continuous Surface 是物理空间，SiteCore 范围是其上的行政覆盖，两者不可互相代替。

## 状态边界

本 ADR 的原始采纳只确认设计。CW-04 已完成实际控制历史、重叠稳定解析、扩张接续和持久化并由制作人封板；CW-04.5 的同势力产品视觉 union 也已封板。CW-05A 已进入资产层：自然状态与当前行政执行上下文分离。

> **2026-09-14 CW-05A／CW-05B 实现注记：** `TerritoryClaim` 与 CW-04.5 已封板。Stateful World Object 不等于 Administrative Asset：当前只有 Farm Plot 显式登记 Content-derived administrative anchor，并从精确位置动态派生 managing WorldSite；tree/wall 虽保留持久 HP/damage state，但没有 Administrative Manager。Physical Asset Identity、Property Ownership、Administrative Manager 相互独立，本轮不实现产权。“无人管理 != 时间停止”，crop state 与自然生长不依赖 manager 或 Host materialization。CW-05B 只让玩家发起的 Organized Farm Labor 按每格 StableCellId 消费当前 manager faction；NPC schedule、Settlement production 与 generic construction 延后 Economy / Automated Settlement Production migration。CW-05A/B 的验收并入 CW-05 Closing 正常建田闭环。

> **2026-09-14 final scale：** 核心等级范围的 Content 单位为 Surface cells，运行时按目标 Surface 的实际 `cellSize` 解析；Claim 保存解析后的 world-space 历史。Main Surface Level 1 最终为 150×150 cells = 4.2×4.2 world = 3×3 chunks。Wilderness Encounter 使用独立的 500×500 cells 配置。

> **2026-09-14 FactionFlag Content migration：** 正式预设旗不再只是 Hex marker。一次性把既有兼容显示中心写成明确 Surface 世界坐标；运行时与玩家新旗共用唯一 SiteCore→TerritoryClaim 路径，SiteId 由 FlagId 稳定生成，baseline 使用旧 `EstablishedOrder`。无法对应唯一 Continuous Surface 的旗保持 legacy-only，禁止运行时从 Hex、最近 Surface 或 Site arrival 猜测。



## 2026-09-14 CW-05 Closing：可建造农田

制作人已授权并实现 `farmField`：正常建筑入口创建5×4 Surface cells的农田，成本粗木5；reference scenario 新游戏通过 `startingInventory` 发粗木20。Core 逐格查询 Actual Administrative Control，只要各格当前 managing Site 同属玩家势力即可，允许跨同势力 Site 边界。Host 校验加载范围、几何、距离并提供预览；Core 在扣料前重验并事务注册。

`OutdoorConstructedAssetBoard` 保存稳定 root ID、建筑/kind、SurfaceId、精确左下角矩形、格数和独立 BoundLocationId；每格复用 `OutdoorStatefulObjectId.ForCell`。Snapshot v6 optional additive `outdoorConstructedAssets`＋`nextOutdoorConstructedAssetSequence` 保存物理资产和序列，crop state 继续走原 FarmPlots；旧档缺字段为空，新格式损坏报错。当前 manager 不进资产或 Snapshot，由 authored＋runtime anchors 动态派生；拆旗不删除田和作物，新 Site 接管立即恢复组织权限。读档内容壳绝不执行 OpeningInventoryBootstrap。

Streaming 复用现有农田 stamping；建造立即局部补齐，卸载只销毁表现，重载恢复相同 cell IDs/crop。CW-05A Probe UI 已移除。CW-05A/B 与 Closing 已于 2026-09-15 Producer Accepted / Sealed；NPC schedule economy、SettlementProduction、generic house/workshop、产权和农田拆除继续延期；玩家 SiteCore 战争转入 CW-08 / CW-09。


## 2026-09-15 制作人封板

CW-05A、CW-05B 与 CW-05 Closing Slice 正式 **Producer Accepted / Sealed**。制作人已通过正常可建农田、管理接续、拆旗保留资产、重新取得管理及 Save/Load 验收。Subsequently Producer Accepted after normal gameplay validation. 历史记录中当时未运行 Unity 验证的事实保持不变。

## 2026-09-15 CW-08 / CW-09：玩家 SiteCore 战争

制作人授权规则见 [235](../235-sitecore-warfare-worldsite-takeover-2026-09-15.md)。固定核心由 `CoreIsRemovable=false` 判定，攻破后建筑仍存在，在原建筑交互范围持续占领；己方存活人物在场且没有存活敌方参战者争夺才计时，离开或争夺归零。占领仅经 `CaptureObjectiveService` → `WorldSiteTerritoryTransferService` 改同一 Site 的 Owner 并恢复核心满耐久。ClaimId、AcquiredOrder、农田 identity/crop、人物 faction/home/squad 均不改。

`CoreIsRemovable=true` 的势力旗被击毁即移除物理旗、令原 Site inactive；保留原 Owner 与 Claim 历史，不占领、不自动变成己方旗。攻方通过正常建造建立新旗、新 Site 和新 Claim。资产继续存在，行政管理者由原 CW-05 查询动态接续。

正常玩家入口统一为 SiteCore Warfare → real Character/Squad → CharacterEncounter，退出两条旧 Siege/BattleOffer 编排。按精确 Surface/WorldPosition、目标 Site 等级范围和 War side 选守军，最近者优先、EntityId 升序打破平局。战中目标仍限定同一 frozen range，最多一个未完成 Site 目标；新守军追加原 roster，不回血、不重置冷却或候选。

目标捕获或摧毁完成可 ReadyToEnd；击倒敌人也可 ReadyToEnd，但不自动占地。ReadyToEnd 仍可攻击与占领当前目标。仅玩家发起 SiteCore 战争属于本轮；NPC 自动攻城、普通建筑战争、产权及居民政治后果延期。
