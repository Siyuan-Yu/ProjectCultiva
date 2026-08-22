# 系统设计索引

> 最后更新：2026-08-22（[2A](2A-factions-armies-diplomacy-and-capture.md) 设计拍板）
> 上级：`docs/00-project/00-overview.md`（最高层大纲）
> 通读顺序见 [`../00-project/04-reading-guide.md`](../00-project/04-reading-guide.md)。
> 新增系统请复制 `docs/90-templates/system-design-template.md`。
> **当前阶段：Architecture Freeze v0.2；开发焦点：VS0.6 人工试玩验收（Social Host 已接入）。** 主契约见 `../30-tech/33-architecture-core-rules-freeze-v0.2.md`。

## 规则

1. **一个系统一份文档**，文件名 `2X-系统名.md`
2. 总览页只放最高层大纲，细节一律放在本目录
3. 文档没定稿不写代码；**架构冻结阶段默认不写代码**
4. 每份文档头部必须有：状态、优先级、最后更新日期、依赖、被引用
5. 所有字段命名以 `docs/00-project/03-glossary.md` 为准
6. 各系统自己的未决问题写在自己文档末尾；跨系统的写在总览第九节
7. 与 `33` 冲突时，以 `33` 冻结条文为准，并回头修订本目录文档

## 系统清单

| 编号 | 系统 | 优先级 | 状态 | 说明 |
|---|---|---|---|---|
| 20 | [开局体验](20-opening-experience.md) | P0 | 40分～1小时入炼气；隐藏修士 | |
| 21 | [核心循环与统一时间](21-core-loop-and-time.md) | P0 | 双层时间已冻结于 `33` | |
| 22 | [境界与机制能力](22-realms-and-abilities.md) | P0 | 炼气四能力方向已冻结 | |
| 23 | [战斗](23-combat.md) | P0 | RTS+暂停；正文待对齐 `33` §9 | |
| 24 | [世界与据点](24-world-and-settlements.md) | P0 | **World／Region／LocalMap（v0.2）** | |
| 25 | [修炼与突破](25-cultivation-and-breakthrough.md) | P0 | 突破=事件已冻结方向 | |
| 26 | [领地经营](26-territory-management.md) | P0 | 夺取控制权 + 时间表 | |
| 27 | [角色、修士与凡人人口](27-characters-and-population.md) | P0 | 四层+组合见 `34` | |
| 28 | [江湖关系](28-jianghu-relations.md) | P0 | Ledger 唯一真源 | |
| 29 | [世界观哲学](29-karma-and-consequence.md) | P1 | 设计方向已定 | |
| 2A | [势力、军队、外交与战略占领](2A-factions-armies-diplomacy-and-capture.md) | P0 | **设计已拍板／尚未实现** | ADR-0024；Army 真源 |
| 2B | [角色属性与修仙成长](2B-attributes-and-affinity.md) | P0 | 底层规则已定方向 | |
| 2C | [属性与 Modifier 管道](2C-attributes-and-modifier-pipeline.md) | P0 | **公式与字段已冻结** | |
| 2D | [功法、斗技与装备](2D-manuals-arts-and-equipment.md) | P0 | 设计方向已定 | |
| 2E | [事件与世界状态记账](2E-events-and-world-state.md) | P0 | **三层+分册；关系 Ledger 真源** | |
| 2F | [义务、配额与隐匿](2F-obligation-and-concealment.md) | P0 | 隐匿三层已冻结 | |
| 2G | [第一章流程](2G-first-chapter-flow.md) | P0 | 开局 Membership 已冻 | |
| 2H | [功法系统规则](2H-manual-system-rules.md) | P0 | 核心规则已定方向 | |
| 2I | [荒村杂役阶段叙事](2I-huangcun-labor-phase-narrative-v0.1.md) | P0 | **Draft v0.1／待审核**；非线性阶段框架 | |

## 架构文档（`30-tech`）

| 文档 | 说明 |
|---|---|
| [31 技术架构](../30-tech/31-architecture.md) | 程序集、数据驱动、工程约定 |
| [32 Demo→正式桥接](../30-tech/32-prototype-to-product-bridge.md) | Demo 映射表 |
| [33 架构核心规则冻结 v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md) | **主契约** |
| [34 实体与能力模块](../30-tech/34-entity-and-component-model.md) | IEntity／组件／四层 |
| [35 Order 与 Action](../30-tech/35-order-and-action-system.md) | 指令与行动 |
| [36 ContentPackage／Mod Ready](../30-tech/36-content-package-and-mod-architecture.md) | 统一内容包；阶段 A |
| [37 飞书同步](../30-tech/37-feishu-sync.md) | 本地→飞书 |

## 依赖关系

```
33 架构冻结
 ├── 34 实体 · 35 Order/Action · 2C Modifier · 2E 事件账本
 └── 21 时间
      ├── 2F → 20 → 2G
      ├── 22 → 23；24
      ├── 2B · 2H · 2D → 25 → 26
      └── 27 → 28 → 29
      └── 2A（战略势力／Army／外交／占点）
```
