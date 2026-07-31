# 系统设计索引

> 最后更新：2026-07-31
> 上级：`docs/00-project/00-overview.md`（最高层大纲）
> 新增系统请复制 `docs/90-templates/system-design-template.md`。
> **当前阶段：架构冻结。** 主契约见 `../30-tech/33-architecture-core-rules-freeze-v0.1.md`。

## 规则

1. **一个系统一份文档**，文件名 `2X-系统名.md`
2. 总览页只放最高层大纲，细节一律放在本目录
3. 文档没定稿不写代码；**架构冻结阶段默认不写代码**
4. 每份文档头部必须有：状态、优先级、最后更新日期
5. 所有字段命名以 `docs/00-project/03-glossary.md` 为准
6. 各系统自己的未决问题写在自己文档末尾；跨系统的写在总览第九节
7. 与 `33` 冲突时，以 `33` 冻结条文为准，并回头修订本目录文档

## 系统清单

| 编号 | 系统 | 优先级 | 状态 | 说明 |
|---|---|---|---|---|
| 20 | [开局体验](20-opening-experience.md) | P0 | 40分～1小时入炼气；隐藏修士 | 凡人觉醒→隐藏修士；离开高风险；敛息 |
| 21 | [核心循环与统一时间](21-core-loop-and-time.md) | P0 | Tick 已冻结于 `33` | 1 Tick=15分；96 Tick/日；时间表权限 |
| 22 | [境界与机制能力](22-realms-and-abilities.md) | P0 | 炼气四能力方向已冻结 | 飞行／踏空／空间仍待定 |
| 23 | [战斗](23-combat.md) | P0 | 小队框架已定方向 | 小队级 RTS；细则待展开 |
| 24 | [世界与据点](24-world-and-settlements.md) | P0 | 三级结构与格子方向已定 | 大陆→城市区域→格子 |
| 25 | [修炼与突破](25-cultivation-and-breakthrough.md) | P0 | 突破=事件已冻结方向 | 第一次炼气待细化 |
| 26 | [领地经营](26-territory-management.md) | P0 | 夺取控制权 + 时间表 | 斩首／瓦解／外交 |
| 27 | [角色、修士与凡人人口](27-characters-and-population.md) | P0 | 四层边界已冻结于 `33` | 全模拟／关键／群体／统计 |
| 28 | [江湖关系](28-jianghu-relations.md) | P0 | 草稿 | 关系是规则，不是资料 |
| 29 | [世界观哲学](29-karma-and-consequence.md) | P1 | 设计方向已定 | 力量越大因果越重 |
| 2A | 势力与战争 | P1 | 未开始 | 占领、统治、外交 |
| 2B | [角色属性与修仙成长](2B-attributes-and-affinity.md) | P0 | 底层规则已定方向 | 统一属性；不做传统五行相克 |
| 2C | [属性与 Modifier 管道](2C-attributes-and-modifier-pipeline.md) | P0 | 形状已冻结；数据待展开 | 见 `33` §1 |
| 2D | [功法、斗技与装备](2D-manuals-arts-and-equipment.md) | P0 | 设计方向已定 | 装备降权 |
| 2E | 事件与世界状态记账 | P0 | 未开始 | 下一设计轮次 |
| 2F | [义务、配额与隐匿](2F-obligation-and-concealment.md) | P0 | 隐匿三层已冻结 | 见 `33` §6 |
| 2G | [第一章流程](2G-first-chapter-flow.md) | P0 | 体验草案 | 感应境→炼气 |
| 2H | [功法系统规则](2H-manual-system-rules.md) | P0 | 核心规则已定方向 | 黄玄地天；双轴 |

## 架构文档（`30-tech`）

| 文档 | 说明 |
|---|---|
| [31 技术架构](../30-tech/31-architecture.md) | 程序集、数据驱动、工程约定 |
| [32 Demo→正式桥接](../30-tech/32-prototype-to-product-bridge.md) | 已验证语义 → 接口需求 |
| [33 架构核心规则冻结 v0.1](../30-tech/33-architecture-core-rules-freeze-v0.1.md) | **主契约** |

## 依赖关系

```
33 架构冻结（Modifier／Tick／四层／隐匿分层）
 └── 21 核心循环与时间
      ├── 2F 义务与隐匿 ──→ 20 开局体验 ──→ 2G 第一章流程
      ├── 22 境界与机制能力 ──→ 23 战斗
      │                     └─→ 24 世界与据点
      ├── 2B 属性 · 2C Modifier · 2H 功法 · 2D 边界 ──→ 25 突破 ──→ 26 领地
      └── 27 角色与人口 ──→ 28 江湖关系 ──→ 29 天道因果

基础层：2B · 2C · 2H · 2D · 2E（事件账本，待写）
```

先守 `33`，再定 `2C`／`2E`／第一次突破细则，最后实现期编码。
