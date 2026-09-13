# ADR-0032：SiteCore、实际行政控制与建设范围

> 状态：已采纳（设计已确认；实现待迁移／核查；制作人验收待完成）
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

本 ADR 只确认设计。现有 Control Asset／FactionFlag／Construction 的历史验收仍适用于当时版本；实际控制历史、动态资产存档、重叠稳定解析和新 Site 生命周期均须迁移或核查，制作人尚未验收本 ADR 的目标行为。

