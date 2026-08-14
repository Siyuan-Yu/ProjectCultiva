# 修仙游戏策划案总览

> 状态：概念框架 v0.9｜**Architecture Freeze v0.2**｜**Content Studio＋Ch01 手操弧进行中** | 最后更新：2026-08-14  
> **本页只放最高层大纲。** 细节进专题页；**怎么读整套文档**见 [通读指南](04-reading-guide.md)。  
> 本地 Markdown 与飞书文档一一对应（真源在本地，飞书为阅读层）。

## 〇、当前项目阶段

**架构冻结 v0.2。** Core／Data／Host／**VS 0.1～1.0 Demo 自动化已验收**。  
其上已叠加：样例关、内容打断、RTS 手操、Navigation／NPC 底座、**Content Studio**、**Level Tester**、**Ch01 三环手操**、**NPC onTalk 对话框（UGUI）**、任务失败／时间流速约定。  
最新增量见 [117](../40-process/117-npc-dialogue-host-ux-rollup-2026-08-14.md)（对话／失败／流速）；上一轮 [116](../40-process/116-recent-updates-rollup-2026-08-14.md)（背包／劳动）；工具链 [115](../40-process/115-recent-updates-rollup-2026-08-13.md)。  

**下一步：** 制作人手操签收对话框＋Ch01；美术换皮／定期发任务／战斗占位另开。  
Demo Runtime 停扩。完整进度见 [62 项目现状](../40-process/62-project-status-2026-08-01.md)。

**建议先读：** [通读指南](04-reading-guide.md) → 本页 → [117 本轮收束](../40-process/117-npc-dialogue-host-ux-rollup-2026-08-14.md) → [116](../40-process/116-recent-updates-rollup-2026-08-14.md) → [62 现状](../40-process/62-project-status-2026-08-01.md) → [110 任务编辑器](../40-process/110-content-studio-quest-editor-usage.md) → [105 手操](../40-process/105-demo-0.1-producer-playbook-30min.md) → [33](../30-tech/33-architecture-core-rules-freeze-v0.2.md)。

- 主契约：[33 架构冻结 v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md)
- 实体／Order／Mod：[34](../30-tech/34-entity-and-component-model.md)、[35](../30-tech/35-order-and-action-system.md)、[36](../30-tech/36-content-package-and-mod-architecture.md)
- Modifier／事件：[2C](../20-systems/2C-attributes-and-modifier-pipeline.md)、[2E](../20-systems/2E-events-and-world-state.md)
- 桥接／审计：[32](../30-tech/32-prototype-to-product-bridge.md)、[50 审计报告](../40-process/50-architecture-freeze-review-report-v0.1.md)
- Core M1：[实施计划 v0.2（已完成）](../40-process/51-core-milestone-1-implementation-plan-v0.2.md)／[ADR-0022](../40-process/43-decisions/ADR-0022-core-milestone-1-scope.md)
- VS1.0 Demo 验收：[74](../40-process/74-vertical-slice-1.0-acceptance-report.md)（计划 [73](../40-process/73-vertical-slice-1.0-demo-plan-v0.1.md)）
- VS0.7～0.9 验收：[68](../40-process/68-vertical-slice-0.7-acceptance-report.md)／[70](../40-process/70-vertical-slice-0.8-acceptance-report.md)／[72](../40-process/72-vertical-slice-0.9-acceptance-report.md)
- 全部决策：[ADR 索引](../40-process/43-decisions/README.md)（UI＝0009 预留）

v0.2 修补要点：RelationshipLedger 权威；WorldTick／ActionClock；Dead≠Removed；FocusCharacterUnavailable；开局宗门劳役 Membership；地图 World／Region／LocalMap。


## 一、一句话定位

一款以**修仙成长**为核心的**实时暂停式战略 RPG**，2D 单机。

它同时融合五条线：个人修仙 RPG、RTS 式的世界与战斗、领地经营、宗门经营、人物关系与江湖系统。玩家从凡人小人物起步，通过探索、关系、修炼与战斗获得会实际改变操作规则的超凡能力，占领并经营村镇，招募核心修士，最终成长为拥有领地与宗门、能影响天下格局的修仙势力。

## 二、玩家身份的五个阶段

| 阶段 | 身份 | 玩家在做什么 |
|---|---|---|
| 1 | 感应境劳役 | 直接控制 3 名感应境角色完成每日配额；开局隶属压迫宗门（杂役／劳役） |
| 2 | 初入仙途 | 偷时间探索，获功法入炼气 |
| 3 | 一地之主 | 取得第一个村落或洞府 |
| 4 | 修仙势力 | 多据点、招募、外交与战争 |
| 5 | 一方道统 | 争夺城镇与高阶洞天 |

## 三、设计支柱

| 支柱 | 一句话 | 详细文档 |
|---|---|---|
| 1. 境界玩法质变 | 大境界解锁操作能力 | `22` |
| 2. 角色与领地供养 | 领地供养修炼，修炼推动扩张 | `26` |
| 3. 江湖关系真后果 | 关系由 Ledger 事件累积 | `28`／`2E` |
| 4. 修炼要准备 | 突破挑时辰地点资源 | `25` |
| 5. 规模不增微操 | 四层模拟 | `27`／`34` |
| 6. 力量越大因果越重 | 非简单正邪二分 | `29` |

## 四、三层玩法结构

| 层 | 内容 | 文档 |
|---|---|---|
| 角色层 | 个人 RPG | `27` |
| 战术层 | 世界地图 RTS+暂停 | `23` |
| 战略层 | 领地／宗门／外交 | `26` |

## 五、世界结构

**World → Region → LocalMap**（Freeze v0.2／ADR-0021）。

- Region：连续城市区域体验（荒村／矿／林／田／城心等）；尺寸可变。  
- LocalMap：洞／秘境／洞府等独立加载。  
- 跨 Region：Route，非整大陆无缝。  

详见 `33` §8、`24`。

## 六、文档索引

| 编号 | 系统 | 优先级 | 状态 |
|---|---|---|---|
| 20 | [开局体验](../20-systems/20-opening-experience.md) | P0 | |
| 21 | [核心循环与时间](../20-systems/21-core-loop-and-time.md) | P0 | WorldTick+ActionClock |
| 22 | [境界与机制能力](../20-systems/22-realms-and-abilities.md) | P0 | |
| 23 | [战斗](../20-systems/23-combat.md) | P0 | M1 不做真战斗 |
| 24 | [世界与据点](../20-systems/24-world-and-settlements.md) | P0 | **三层地图已对齐 v0.2** |
| 25 | [修炼与突破](../20-systems/25-cultivation-and-breakthrough.md) | P0 | |
| 26 | [领地经营](../20-systems/26-territory-management.md) | P0 | |
| 27 | [角色与人口](../20-systems/27-characters-and-population.md) | P0 | |
| 28 | [江湖关系](../20-systems/28-jianghu-relations.md) | P0 | Ledger 真源；VS0.5 落地中 |
| 29 | [天道因果](../20-systems/29-karma-and-consequence.md) | P1 | |
| 2B | [属性与成长](../20-systems/2B-attributes-and-affinity.md) | P0 | |
| 2C | [Modifier 管道](../20-systems/2C-attributes-and-modifier-pipeline.md) | P0 | |
| 2D | [功法斗技装备](../20-systems/2D-manuals-arts-and-equipment.md) | P0 | |
| 2E | [事件与世界账本](../20-systems/2E-events-and-world-state.md) | P0 | |
| 2F | [义务与隐匿](../20-systems/2F-obligation-and-concealment.md) | P0 | |
| 2G | [第一章流程](../20-systems/2G-first-chapter-flow.md) | P0 | 开局 Membership 已冻 |
| 2H | [功法规则](../20-systems/2H-manual-system-rules.md) | P0 | |
| 2I | [荒村杂役阶段叙事（v0.1）](../20-systems/2I-huangcun-labor-phase-narrative-v0.1.md) | P0 | Draft；状态／触发／反馈 |

**项目与过程：**

| 文档 | 说明 |
|---|---|
| [通读指南](04-reading-guide.md) | 阅读顺序与角色最短路径 |
| [愿景](01-vision.md)／[范围](02-scope-and-constraints.md)／[术语表](03-glossary.md) | 总纲三件套 |
| [系统设计索引](../20-systems/README.md) | 系统清单与依赖 |
| [路线图](../40-process/41-roadmap.md)／[开发日志](../40-process/42-devlog.md) | 阶段与记录 |
| [62 项目现状 2026-08-01](../40-process/62-project-status-2026-08-01.md) | **现行进度总表** |
| [75 VS0.7→1.0 交付总结](../40-process/75-vs0.7-to-1.0-delivery-summary-2026-08-01.md) | **本轮交付总览** |
| [74 VS1.0 Demo 验收](../40-process/74-vertical-slice-1.0-acceptance-report.md) | **已通过** |
| [Core M1 实施计划 v0.2](../40-process/51-core-milestone-1-implementation-plan-v0.2.md) | **已完成验收** |
| [VS 0.4～0.6](../40-process/61-vertical-slice-0.4-acceptance-report.md)／[63](../40-process/63-vertical-slice-0.5-alpha-acceptance.md)／[65](../40-process/65-vertical-slice-0.6-acceptance-report.md) | Host／社会／Social Host |
| [VS 0.7～0.9 验收](../40-process/68-vertical-slice-0.7-acceptance-report.md)／[70](../40-process/70-vertical-slice-0.8-acceptance-report.md)／[72](../40-process/72-vertical-slice-0.9-acceptance-report.md) | 内容／据点／世界 |
| [Data SCHEMA](../../Content/BaseGame/Data/SCHEMA.md) | 运行时 JSON 字段 |
| [Data Pipeline M1 计划 v0.2](../40-process/53-data-pipeline-milestone-1-plan-v0.2.md) | **已实现**（M1-A／M1-B） |
| [ADR 决策索引](../40-process/43-decisions/README.md) | 全部已采纳决策 |

**架构文档：**

| 文档 | 说明 |
|---|---|
| [33 冻结 v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md) | **当前主契约** |
| [34 实体](../30-tech/34-entity-and-component-model.md) | 含 PersonalityProfile（VS0.5-A） |
| [35 Order/Action](../30-tech/35-order-and-action-system.md) | |
| [36 ContentPackage](../30-tech/36-content-package-and-mod-architecture.md) | |
| [31 技术架构](../30-tech/31-architecture.md) | 程序集与工程约定 |
| [32 桥接](../30-tech/32-prototype-to-product-bridge.md) | Demo→正式 |
| [50 审计报告](../40-process/50-architecture-freeze-review-report-v0.1.md) | Freeze 一致性审计 |
| [37 飞书同步](../30-tech/37-feishu-sync.md) | 本地↔飞书规则 |

## 七、系统依赖顺序

```
33 v0.2 主契约
 ├── 34 · 35 · 2C · 2E · 36
 └── 21 时间
      ├── 2F → 20 → 2G
      ├── 22 → 23；24
      ├── 2B · 2H · 2D → 25 → 26
      └── 27 → 28 → 29
```

## 八、范围控制

第一阶段玩法仍只验证：感应→炼气质变、角色↔领地闭环、暂停战斗构筑（**实现上仍不含真战斗**）。  
**VS0.7～1.0 已完成**：内容化开局、据点日循环、抽象地点探索、Demo 成长闭环可手操。不扩 Demo Runtime；不改 Freeze。关系／据点／地点入 Snapshot 前须硬停。

## 九、跨系统未决（摘录）

飞行境界、炼气术法清单、突破事件细则、Focus 失能后继承 UI、TemporaryProtection 事件模板库等——见各系统文档；**不在未冻时自行拍板。**

## 十、下一步

1. 制作人按 [74 §4](../40-process/74-vertical-slice-1.0-acceptance-report.md) 手操 Demo 路径签收。  
2. 若要关系／据点／地点入档 → **先确认 Snapshot schema**。  
3. 正式 UI／内容扩量／战斗等另开切片。  
4. 不扩展 Demo Runtime；不改 Freeze 正文。
