# 系统设计索引

> **当前状态（2026-09-22）：** MAP-01～04、SPACE-01、LEGACY-FINAL-A／B／C，以及 Hex／Army 正式运行依赖退役与 021915 统一收尾均已 Producer Accepted / Sealed；封板提交 `9b32fe0`。正常 Outdoor、NPC Squad、PlayerParty、CharacterEncounter 与 Actual Administrative Control 的 authority／compatibility 边界以 [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) 为准。
> **Editor Toolchain / Legacy Content：** [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) 的 MAP 范围已落地；旧 Hex／Outdoor LocalMap 仅保留明确迁移、兼容、工具或测试消费者。

> 最后更新：2026-09-22
> 上级：`docs/00-project/00-overview.md`（最高层大纲）
> 通读顺序见 [`../00-project/04-reading-guide.md`](../00-project/04-reading-guide.md)。
> 新增系统请复制 `docs/90-templates/system-design-template.md`。
> **当前阶段：** Final Seal 已验收并提交；不重新开启 CW／MAP／SPACE／LEGACY-FINAL 分期，也不按 Legacy 关键词重开清理。各系统实际能力、持久化边界和 Proposal 见 [247 当前系统现状总表](../40-process/247-project-handoff-current-state-2026-09-18.md#当前系统现状总表2026-09-22)。后续功能方向尚未批准。

## 规则

1. **一个系统一份文档**，文件名 `2X-系统名.md`
2. 总览页只放最高层大纲，细节一律放在本目录
3. 文档没定稿不写代码；**架构冻结阶段默认不写代码**
4. 每份文档头部必须有：状态、优先级、最后更新日期、依赖、被引用
5. 所有字段命名以 `docs/00-project/03-glossary.md` 为准
6. 各系统自己的未决问题写在自己文档末尾；跨系统的写在总览第九节
7. 与 `33` 冲突时，以 `33` 顶部列出的较新 ADR 定向补丁为准；旧冲突段必须保留准确 supersession 指向

## 系统清单

| 编号 | 系统 | 优先级 | 状态 | 说明 |
|---|---|---|---|---|
| 20 | [开局体验](20-opening-experience.md) | P0 | 40分～1小时入炼气；隐藏修士 | |
| 21 | [核心循环与统一时间](21-core-loop-and-time.md) | P0 | WorldTick／ActionClock 与 CharacterEncounter 冻结边界已落地 | |
| 22 | [境界与机制能力](22-realms-and-abilities.md) | P0 | 炼气四能力方向已冻结 | |
| 23 | [战斗](23-combat.md) | P0 | CharacterEncounter 正式主线已实现并封板；Future 扩展除外 | ADR-0033/0035/0038 |
| 24 | [世界与据点](24-world-and-settlements.md) | P0 | Continuous Outdoor + SiteCore 已落地；旧结构仅兼容 | ADR-0031/0032/0038 |
| 25 | [修炼与突破](25-cultivation-and-breakthrough.md) | P0 | 突破=事件已冻结方向 | |
| 26 | [领地经营](26-territory-management.md) | P0 | SiteCore／Actual Administrative Control 已落地；未来经济扩展未实现 | ADR-0032/0038 |
| 27 | [角色、修士与凡人人口](27-characters-and-population.md) | P0 | 四层与当前 Party／Squad 生命周期、Emergency Handoff、True-Death Succession 已落地并封板 | ADR-0039 |
| 28 | [江湖关系](28-jianghu-relations.md) | P0 | Ledger V1 已验收；预警／敌情明确为 Future | |
| 29 | [世界观哲学](29-karma-and-consequence.md) | P1 | 设计方向已定 | |
| 2A | [势力、军队、外交与战略占领](2A-factions-armies-diplomacy-and-capture.md) | P0 | WorldSite／Claim／Actual Control 与现行冲突接管已落地；旧 Army 段落为 Historical | ADR-0033/0034/0038 |
| 2B | [角色属性与修仙成长](2B-attributes-and-affinity.md) | P0 | 底层规则已定方向 | |
| 2C | [属性与 Modifier 管道](2C-attributes-and-modifier-pipeline.md) | P0 | **公式与字段已冻结** | |
| 2D | [功法、斗技与装备](2D-manuals-arts-and-equipment.md) | P0 | 设计方向已定 | |
| 2E | [事件与世界状态记账](2E-events-and-world-state.md) | P0 | 设计已冻结；部分 runtime board 已实现，通用内容状态磁盘持久化仍为 Proposal | 见 247 |
| 2F | [义务、配额与隐匿](2F-obligation-and-concealment.md) | P0 | 隐匿三层已冻结 | |
| 2G | [第一章流程](2G-first-chapter-flow.md) | P0 | 开局 Membership 已冻 | |
| 2H | [功法系统规则](2H-manual-system-rules.md) | P0 | 核心规则已定方向 | |
| 2I | [荒村杂役阶段叙事](2I-huangcun-labor-phase-narrative-v0.1.md) | P0 | **Draft v0.1／待审核**；非线性阶段框架 | |
| 2J | [Hex Territory、Multi-Hex WorldSite 与动态山贼](2J-hex-territory-worldsites-and-dynamic-bandits.md) | P0 | **Historical / Superseded**；当前 Territory／Site／Bandit 边界见 26／2A／2K | ADR-0032/0038 |
| 2K | [RPG-First：Active／PlayerParty／Continuous Surface／Legacy FormalArmy](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) | P0 | PlayerParty／Squad／Surface authority 已封板；飞舟等仍为 Future | ADR-0034/0038 |
| 2L | [LocalMap 建造系统 V1](2L-local-map-construction-v1.md) | P0 | **已实现／已人工验收／已封板** | 建筑目录、材料事务与主动拆除 |
| 2M | [角色社会关系 V1](2M-character-social-relations-v1.md) | P0 | **已实现／已人工验收／已封板** | Social Bond、五维态度、社会事件、击杀后果与统一人物档案 |
| 2N | [连续世界制作、合成与去 Hex 产品方向](2N-continuous-surface-world-authoring-and-composition.md) | P0 | **MAP-01～04 已验收封板** | Composer、Fine Editor、Final Surface、WorldMap LOD、兼容隔离 |

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
      └── 2A（战略势力／Army 军事／外交／占点）
           ├── 2J（Hex Territory／Multi-Hex Site／Dynamic Bandit）
           └── 2K（RPG-First：Party／连续世界／Army 职责边界）← **控制模型真源**
```
