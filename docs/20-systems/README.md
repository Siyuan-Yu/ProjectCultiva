# 系统设计索引

> **MAP 状态（2026-09-16）：** [MAP-01](../40-process/242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md) 与 [MAP-02](../40-process/243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md) 已 Producer Accepted / Sealed：前者交付 Composer/FineEditor、authoring/bake 与 compatibility publish，后者交付主 Continuous Surface 的 WorldMap strategic view。MAP-03/04 尚未开始。
> **Editor Toolchain / Legacy Content Direction：** [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) 继续锁定 Authoring Source ≠ Runtime Content、旧 Content 迁移及后续 legacy retirement；现有 Hex/LocalMap consumers 仍是 compatibility，不因 MAP-02 自动退役。

> 最后更新：2026-09-15（ADR-0036／ADR-0037 生命周期与术语一致性清理；实现与制作人验收按各页状态）
> 上级：`docs/00-project/00-overview.md`（最高层大纲）
> 通读顺序见 [`../00-project/04-reading-guide.md`](../00-project/04-reading-guide.md)。
> 新增系统请复制 `docs/90-templates/system-design-template.md`。
> **当前阶段：** CW-04～CW-09.5 与 [238 恢复处／队伍战斗作弊](../40-process/238-recovery-spot-and-party-combat-cheats-2026-09-15.md) 已 Producer Accepted / Sealed；[CW-10](../40-process/237-cw-10-site-economy-automated-administration-migration-2026-09-15.md)／[CW-10.5](../40-process/239-cw-10-5-strategic-resource-access-and-storage-room-2026-09-15.md) 为 Implementation Completed / Producer Acceptance Pending。MAP-01/MAP-02 已封板，MAP-03 未开始；旧 Editor 生命周期见 [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)。** 当前状态与依赖见 [216](../40-process/216-continuous-world-final-design-documentation-alignment-2026-09-12.md)，主契约见 `../30-tech/33-architecture-core-rules-freeze-v0.2.md`；[163](../40-process/163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md) 仅为旧阶段路线。

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
| 21 | [核心循环与统一时间](21-core-loop-and-time.md) | P0 | 设计确认／文档更新；Encounter 时间实施待核查 | |
| 22 | [境界与机制能力](22-realms-and-abilities.md) | P0 | 炼气四能力方向已冻结 | |
| 23 | [战斗](23-combat.md) | P0 | 同源独立遭遇设计确认；实施／验收待完成 | ADR-0033 |
| 24 | [世界与据点](24-world-and-settlements.md) | P0 | Continuous Outdoor + SiteCore 设计确认；部分实施 | ADR-0031/0032 |
| 25 | [修炼与突破](25-cultivation-and-breakthrough.md) | P0 | 突破=事件已冻结方向 | |
| 26 | [领地经营](26-territory-management.md) | P0 | SiteCore 管理设计确认；新范围待迁移 | ADR-0032 |
| 27 | [角色、修士与凡人人口](27-characters-and-population.md) | P0 | 四层已存在；接替／继承待迁移 | ADR-0034 |
| 28 | [江湖关系](28-jianghu-relations.md) | P0 | Ledger V1 已验收；预警／敌情待实现 | |
| 29 | [世界观哲学](29-karma-and-consequence.md) | P1 | 设计方向已定 | |
| 2A | [势力、军队、外交与战略占领](2A-factions-armies-diplomacy-and-capture.md) | P0 | 旧 Control Asset 已验收；新冲突／接管待迁移 | ADR-0033/0034 |
| 2B | [角色属性与修仙成长](2B-attributes-and-affinity.md) | P0 | 底层规则已定方向 | |
| 2C | [属性与 Modifier 管道](2C-attributes-and-modifier-pipeline.md) | P0 | **公式与字段已冻结** | |
| 2D | [功法、斗技与装备](2D-manuals-arts-and-equipment.md) | P0 | 设计方向已定 | |
| 2E | [事件与世界状态记账](2E-events-and-world-state.md) | P0 | **三层+分册；关系 Ledger 真源** | |
| 2F | [义务、配额与隐匿](2F-obligation-and-concealment.md) | P0 | 隐匿三层已冻结 | |
| 2G | [第一章流程](2G-first-chapter-flow.md) | P0 | 开局 Membership 已冻 | |
| 2H | [功法系统规则](2H-manual-system-rules.md) | P0 | 核心规则已定方向 | |
| 2I | [荒村杂役阶段叙事](2I-huangcun-labor-phase-narrative-v0.1.md) | P0 | **Draft v0.1／待审核**；非线性阶段框架 | |
| 2J | [Hex Territory、Multi-Hex WorldSite 与动态山贼](2J-hex-territory-worldsites-and-dynamic-bandits.md) | P0 | Hex 战略摘要保留；旧 Footprint 精确范围已被 SiteCore 实际范围替代 | ADR-0032 |
| 2K | [RPG-First：Active／PlayerParty／连续 Hex／FormalArmy](2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) | P0 | 旧阶段已验收；继承／飞舟／统一移动待迁移 | ADR-0034 |
| 2L | [LocalMap 建造系统 V1](2L-local-map-construction-v1.md) | P0 | **已实现／已人工验收／已封板** | 建筑目录、材料事务与主动拆除 |
| 2M | [角色社会关系 V1](2M-character-social-relations-v1.md) | P0 | **已实现／已人工验收／已封板** | Social Bond、五维态度、社会事件、击杀后果与统一人物档案 |
| 2N | [连续世界制作、合成与去 Hex 产品方向](2N-continuous-surface-world-authoring-and-composition.md) | P0 | **已锁定未来方向／未实现** | Composer、Fine Editor、Final Surface、WorldMap LOD |

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
