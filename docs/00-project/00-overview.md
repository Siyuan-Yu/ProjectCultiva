# 修仙游戏策划案总览

> 状态：概念框架 v0.9｜Architecture Freeze v0.2＋ADR-0038｜MAP-01～04、SPACE-01、LEGACY-FINAL-A／B／C 及 Hex／Army 正式运行依赖退役与 021915 统一收尾均已封板 | 最后更新：2026-09-22
> **本页只放最高层大纲。** 细节进专题页；**怎么读整套文档**见 [通读指南](04-reading-guide.md)。
> 本地 Markdown 与飞书文档一一对应（真源在本地，飞书为阅读层）。

## 〇、当前项目阶段

**Design: Confirmed｜Implementation: Continuous Outdoor、统一 Squad／CharacterEncounter、Actual Administrative Control 与 Separate Space 正式主线已落地。CW-04～CW-10.5、MAP-01～MAP-04、SPACE-01、LEGACY-FINAL-A／B／C，以及正式运行对旧 Hex／Army 依赖的最终退役与 021915 统一收尾均已完成，并由制作人人工验收后 **Producer Accepted / Sealed**。正常产品不再编译旧 Hex 几何，Runtime Loader 拒绝旧 `formalArmy`／`hexWorld`；离线转换器只无损处理 FormalArmy 与 current authority 完整的 hybrid Snapshot，`hexWorld` 须走现有 WorldComposer／SurfaceAuthoring Legacy migration 路径，无样例时不猜。后续功能方向尚未批准。**
Continuous Outdoor、SiteCore、同源独立遭遇、人物／建筑冲突、控制继承和飞舟运输由 [ADR-0032](../40-process/43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md)～[0034](../40-process/43-decisions/ADR-0034-conflict-control-succession-and-airship-role.md) 定向修订 Freeze v0.2。旧阶段人工验收继续有效，但不证明新目标已经实现或验收。
当前代码、Content、存档、兼容层与制作人反馈的统一状态见 [247](../40-process/247-project-handoff-current-state-2026-09-18.md)；最终 authority／compatibility 矩阵见 [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。[216](../40-process/216-continuous-world-final-design-documentation-alignment-2026-09-12.md) 与 [230](../40-process/230-recent-development-alignment-and-handoff-2026-09-14.md) 保留为迁移前历史对齐记录。

本专项不表示整个游戏已完成或不存在潜在缺陷，也不要求历史文档、稳定 wire key 或离线转换器中的 `Hex`／`Army` 字符串清零。正常产品中的旧运行依赖已经退役；不得把新增 `Legacy*` runtime adapter 当作完成标准。

**状态阅读纪律：** `Design Confirmed` 只表示制作人已确认规则；`Implemented` 表示当前代码已接通；`Producer Accepted` 必须有明确人工验收；`Committed / Sealed` 必须对应真实 Git 提交与封板授权；`Proposed / Not Implemented` 只是待讨论范围。当前封板提交是 `9b32fe0`。各系统的实际能力、持久化边界、验收／提交依据和待讨论项集中在 [247 当前系统现状总表](../40-process/247-project-handoff-current-state-2026-09-18.md#当前系统现状总表2026-09-22)，不要从旧路线图推断。

**下一步仅为 Proposal：** 优先讨论 Quest、Flags、ContentEvents、Chapters、ContentCounters、ContentDaily 的正式磁盘持久化，以及待选择事件弹窗打开时的保存策略；尚未授权实施。进度可靠保存后，再考虑用现有能力串接一段完整探索成长流程，而不是继续增加底层框架。

**当前静态差异（不等于已授权任务）：** Separate Space session 尚未进入 JSON serializer wire；Snapshot restore 未重注册 Quest／Event／Chapter definitions；WorldMap Player／NPC Squad marker 仍为固定像素而确认规则要求战略图标随 zoom 投影缩放。详见 [247](../40-process/247-project-handoff-current-state-2026-09-18.md#当前系统现状总表2026-09-22)。

**Continuous Surface 地图进度：** [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) 锁定 Surface Cell／Runtime Chunk／World Editor Cell 的职责。MAP-01～MAP-04 均已实施、人工验收并封板；正常 Gameplay authority 已切换到 Surface。当前正式产品程序集已物理移除 Core Hex 目录与 `SimulationWorld` 的 Hex 容器；旧 `formalArmy`／`hexWorld` 只在 Loader 边界被拒绝并指向离线转换流程，不存在正常 runtime 自动 migration。

**Editor 工具链：** [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) 的 manifest、平铺 `Apps/` 与 staging 发布已落地。WorldComposer／FineEditor 是现行 Surface authoring 工具；WorldGraphEditor／RegionEditor 已退休。独立 Cave／Interior／Encounter 地图继续合法保留。

**建议先读：** [通读指南](04-reading-guide.md) → [33 定向补丁](../30-tech/33-architecture-core-rules-freeze-v0.2.md) → [ADR-0032～0034](../40-process/43-decisions/README.md) → [24 世界](../20-systems/24-world-and-settlements.md)／[23 战斗](../20-systems/23-combat.md)／[2K 控制](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)。

- 主契约：[33 架构冻结 v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md)
- 实体／Order／Mod：[34](../30-tech/34-entity-and-component-model.md)、[35](../30-tech/35-order-and-action-system.md)、[36](../30-tech/36-content-package-and-mod-architecture.md)
- Modifier／事件：[2C](../20-systems/2C-attributes-and-modifier-pipeline.md)、[2E](../20-systems/2E-events-and-world-state.md)
- 桥接／审计：[32](../30-tech/32-prototype-to-product-bridge.md)、[50 审计报告](../40-process/50-architecture-freeze-review-report-v0.1.md)
- Core M1：[实施计划 v0.2（已完成）](../40-process/51-core-milestone-1-implementation-plan-v0.2.md)／[ADR-0022](../40-process/43-decisions/ADR-0022-core-milestone-1-scope.md)
- VS1.0 Demo 验收：[74](../40-process/74-vertical-slice-1.0-acceptance-report.md)（计划 [73](../40-process/73-vertical-slice-1.0-demo-plan-v0.1.md)）
- VS0.7～0.9 验收：[68](../40-process/68-vertical-slice-0.7-acceptance-report.md)／[70](../40-process/70-vertical-slice-0.8-acceptance-report.md)／[72](../40-process/72-vertical-slice-0.9-acceptance-report.md)
- 全部决策：[ADR 索引](../40-process/43-decisions/README.md)（UI＝0009 预留）

v0.2 修补要点仍包括 RelationshipLedger 权威、WorldTick／ActionClock、Dead≠Removed、FocusCharacterUnavailable 与开局宗门劳役 Membership；旧 World／Region／LocalMap 户外结构已由 ADR-0031／0036／0038 定向替代。


## 一、一句话定位

一款以**修仙成长**为核心的**实时暂停式战略 RPG**，2D 单机。

它同时融合五条线：个人修仙 RPG、RTS 式的世界与战斗、领地经营、宗门经营、人物关系与江湖系统。玩家从凡人小人物起步，通过探索、关系、修炼与战斗获得会实际改变操作规则的超凡能力，占领并经营村镇，招募核心修士，最终成长为拥有领地与宗门、能影响天下格局的修仙势力。

## 二、玩家身份的五个阶段

| 阶段 | 身份 | 玩家在做什么 |
|---|---|---|
| 1 | 感应境劳役 | 三名初始角色组成可轮换小队；同一时刻只直接控制一名 Active，其余由 AI 行动；开局隶属压迫宗门 |
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

> **当前权威：** 普通 Outdoor 每大陆一个 Continuous Outdoor World Surface；WorldSite 是 SiteCore／行政上下文，Hex 仅为派生或兼容摘要，Chunk 是加载单位。不同大陆、Interior 和临时独立遭遇仍可切换。

**普通 Outdoor：一大陆一张 Continuous Surface。**

- `SurfaceId + exact WorldPosition` 是人物与小队的正常户外空间真源。
- 正常 NPC group 使用 `Squad + SquadWorldMotion`，现代战斗使用 `CharacterEncounter`。
- WorldSite／Actual Administrative Control 是连续 world-space 上的地点与行政上下文。
- Runtime Chunk 只负责 streaming；不会改变位置 authority。
- Cave／Interior／Dungeon、不同大陆与临时独立 Encounter 仍可作为 Separate Space／独立场景切换。

详见 `33` §8、`24`。

## 六、文档索引

| 编号 | 系统 | 优先级 | 状态 |
|---|---|---|---|
| 20 | [开局体验](../20-systems/20-opening-experience.md) | P0 | |
| 21 | [核心循环与时间](../20-systems/21-core-loop-and-time.md) | P0 | WorldTick+ActionClock |
| 22 | [境界与机制能力](../20-systems/22-realms-and-abilities.md) | P0 | |
| 23 | [战斗](../20-systems/23-combat.md) | P0 | CharacterEncounter 正式主线已实现并封板；Future 战斗扩展仍未实现 |
| 24 | [世界与据点](../20-systems/24-world-and-settlements.md) | P0 | Continuous Outdoor + SiteCore 已落地；旧地图层仅兼容 |
| 25 | [修炼与突破](../20-systems/25-cultivation-and-breakthrough.md) | P0 | |
| 26 | [领地经营](../20-systems/26-territory-management.md) | P0 | |
| 27 | [角色与人口](../20-systems/27-characters-and-population.md) | P0 | |
| 28 | [江湖关系](../20-systems/28-jianghu-relations.md) | P0 | Ledger 真源；VS0.5 已验收 |
| 29 | [天道因果](../20-systems/29-karma-and-consequence.md) | P1 | |
| 2B | [属性与成长](../20-systems/2B-attributes-and-affinity.md) | P0 | |
| 2C | [Modifier 管道](../20-systems/2C-attributes-and-modifier-pipeline.md) | P0 | |
| 2D | [功法斗技装备](../20-systems/2D-manuals-arts-and-equipment.md) | P0 | |
| 2E | [事件与世界账本](../20-systems/2E-events-and-world-state.md) | P0 | |
| 2F | [义务与隐匿](../20-systems/2F-obligation-and-concealment.md) | P0 | |
| 2G | [第一章流程](../20-systems/2G-first-chapter-flow.md) | P0 | 开局 Membership 已冻 |
| 2H | [功法规则](../20-systems/2H-manual-system-rules.md) | P0 | |
| 2I | [荒村杂役阶段叙事（v0.1）](../20-systems/2I-huangcun-labor-phase-narrative-v0.1.md) | P0 | Draft；状态／触发／反馈 |
| 2M | [角色社会关系 V1](../20-systems/2M-character-social-relations-v1.md) | P0 | **已实现／已人工验收／已封板**（[记录 202](../40-process/202-character-social-relations-and-profile-ui-v1-sealed-2026-09-07.md)） |
| 2N | [连续世界制作、合成与去 Hex 产品方向](../20-systems/2N-continuous-surface-world-authoring-and-composition.md) | P0 | **MAP-01～04 已实现、验收并封板**；自动水文等仍为 Future |
| — | [ADR-0037 Editor 工具链与旧地图 Content 迁移方向](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) | P0 | 工具链与旧 Outdoor Content 迁移已按 MAP 阶段落地；Future 扩展不视为已实现 |

**项目与过程：**

| 文档 | 说明 |
|---|---|
| [通读指南](04-reading-guide.md) | 阅读顺序与角色最短路径 |
| [愿景](01-vision.md)／[范围](02-scope-and-constraints.md)／[术语表](03-glossary.md) | 总纲三件套 |
| [系统设计索引](../20-systems/README.md) | 系统清单与依赖 |
| [路线图](../40-process/41-roadmap.md)／[开发日志](../40-process/42-devlog.md) | 阶段与记录 |
| [247 当前交接](../40-process/247-project-handoff-current-state-2026-09-18.md)／[ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) | **现行状态／最终 authority 与兼容边界** |
| [62 项目现状 2026-08-01](../40-process/62-project-status-2026-08-01.md) | 历史阶段记录 |
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

> **历史范围说明：** 早期 M1／VS0.7～1.0 与 216／230 迁移记录的阶段约束和验收事实继续有效，但不代表当前 WIP。现行 runtime authority 与 compatibility 以 ADR-0038、247 和系统正文为准；任何 Future 功能仍需另行授权。

## 九、跨系统未决（摘录）

飞行境界、炼气术法清单、突破事件细则、TemporaryProtection 事件模板库等仍见各系统文档。队内顺序接替和全队死亡继承原则已经确定；仅全员弥留安全出口接线与空势力终局（明确延期）仍需区分处理。

## 十、当前交付与未来范围

1. 当前仅验收 Final Seal 的兼容隔离、dead API 与文档对齐；不重跑 A／B／C、MAP 或 SPACE 的完整历史验收。
2. 飞舟、自动攻城、NPC 对 NPC 战斗、完整修炼／继承等仍是 Future / Not Implemented，不因迁移封板而自动成为现行功能。
3. 新开发必须从 [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) 的 authority／wire／offline-conversion 边界出发，禁止恢复旧 runtime、旧 Hex 几何编译依赖或运行时自动迁移。
