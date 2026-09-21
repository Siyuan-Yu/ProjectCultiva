# 文档通读指南

> **当前入口：** [ADR-0038 最终冻结](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) → [247 当前交接](../40-process/247-project-handoff-current-state-2026-09-18.md) → [2N Continuous Surface](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)／[2K 控制与 PlayerParty](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)／[23 CharacterEncounter](../20-systems/23-combat.md)。

> 状态：现行 | 最后更新：2026-09-22
> 上级：[`00-overview.md`](00-overview.md)
> **本页说明怎么读整套策划／架构文档。** 正文仍以各专题页为准；本地 Markdown 与飞书同步页一一对应。

## 1. 文档怎么排布（与常见大型立项案对齐）

本仓库采用「**总纲枢纽 + 编号专题 + 决策 ADR**」结构，接近中大型团队开工方案／GDD 的常见组织方式：

| 层级 | 目录 | 作用 |
|---|---|---|
| 总纲 | `00-project/` | 定位、范围、术语、通读路径 |
| 竞品 | `10-benchmark/` | 借鉴与差异化依据 |
| 系统 | `20-systems/` | 玩法与体验设计（一篇一系统） |
| 技术契约 | `30-tech/` | 架构冻结、实体／指令、内容包、同步说明 |
| 过程 | `40-process/` | 路线图、日志、Demo 记录、审计、**ADR 决策**、编辑器计划 |

阅读原则：

1. **总览只放大纲**；细节进专题页，避免一篇万字墙。
2. **冲突时以 `33` 及其明确列出的更新 ADR 为准**；2026-09-12 的定向补丁优先于旧 Freeze 冲突段。
3. **ADR 只记已拍板决策**；未决问题留在各页末尾或总览第九节。
4. **飞书 = 阅读层**；本地 `.md` = 唯一真源（见 `37-feishu-sync.md`）。

当前结构已经适合「仔细通读 + 交叉跳转」。本轮整理重点是：补齐飞书映射、加强入口导航，**不重写已冻结规则正文**。

## 1.1 当前制作人入口（2026-09-22）

| 目的 | 读什么 |
|------|--------|
| **当前状态与交付** | [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) 冻结现代 authority／compatibility 矩阵；[247](../40-process/247-project-handoff-current-state-2026-09-18.md) 是唯一当前交接。A／B／C、MAP-01～04、SPACE-01 及 Legacy 清理／Hex·Army／WorldSite 命名边界专项均已 **Sealed**；下一步等待制作人讨论，尚未授权新功能实施 |
| **旧 Editor 生命周期（Legacy Compatibility）** | [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) §5～§6；用法页顶部 banner：[128 WorldGraph](../40-process/128-world-graph-editor-usage.md)／[112 MapEditor](../40-process/112-map-editor-usage.md)／[109 RegionEditor](../40-process/109-content-studio-region-editor-usage.md)／[130 LocalPlaceEditor](../40-process/130-local-place-editor-usage.md)。Active 工具（PackageBrowser／CharacterNpcEditor／ManualArtEditor／QuestEditor／EventEditor／WorkAreaEditor）不在此列 |
| **连续世界地图当前进度** | [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) → [2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md) → [245 MAP-04 历史审计](../40-process/245-map-04-physical-legacy-cleanup-2026-09-17.md)。MAP-01～04 均已验收封板 |
| **Editor 工具链／旧地图 Content** | [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) 的 MAP 范围已落地；WorldComposer／FineEditor 为现行 Surface authoring，旧 WorldGraph／Region 工具已退休。自动水文等后续能力仍是 Future |
| **CW-04 已封板范围** | [223](../40-process/223-cw-04-territory-claim-administrative-control-2026-09-14.md)～[229](../40-process/229-cw-04-flag-placement-player-camp-retirement-2026-09-14.md)（Producer Accepted / Sealed） |
| **最终规则、替代矩阵与兼容边界** | [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) |
| **当前代码命名／wire 兼容对照** | [248](../40-process/248-legacy-final-a-unified-npc-squad-world-motion-2026-09-20.md) 的现行实现注记 + [247](../40-process/247-project-handoff-current-state-2026-09-18.md) §25；内部 `Legacy*` 命名不改外部 `formalArmy`／`initialFormalArmyIds`、Snapshot 与 JSON wire key |
| **SiteCore／实际行政与建设范围** | [24](../20-systems/24-world-and-settlements.md) + [26](../20-systems/26-territory-management.md) + [ADR-0032](../40-process/43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md) |
| **同源独立遭遇／回位保战果** | [23](../20-systems/23-combat.md) + [ADR-0033](../40-process/43-decisions/ADR-0033-source-faithful-independent-encounter-and-world-anchor-return.md) |
| **冲突／继承／飞舟与地图移动** | [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) + [2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) + [ADR-0034](../40-process/43-decisions/ADR-0034-conflict-control-succession-and-airship-role.md) |
| **RPG-First 控制／Party／连续世界／Legacy Army 边界（最新真源）** | [2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) + [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)；ADR-0026 为被后续决策定向替代的历史基线 |
| **迁移计划／代码冲突审计** | [163](../40-process/163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md) |
| **战略势力层（外交／Capture／Army 军事仍有效部分）** | [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md)（跨点必须 Army 已 supersede） |
| **Hex Territory／Multi-Hex Site（历史／旧输入）** | [2J](../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)；normal runtime 已由 ADR-0038 的 Actual Control 替代 |
| **修士真实 Character（部分 superseded）** | [ADR-0024](../40-process/43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md) |
| **Pure Hex 收束** | [162](../40-process/162-pure-hex-final-audit-and-snapshot-v6-json-2026-08-24.md)／[161](../40-process/161-pure-hex-legacy-purge-and-post-fix-rollup-2026-08-24.md) |
| **LocalPlaceEditor** | [130](../40-process/130-local-place-editor-usage.md) |
| **上一轮（WorldGraph Host 出行／隔离）** | [129](../40-process/129-world-graph-host-travel-scene-isolation-2026-08-16.md) |
| **WorldGraph 架构** | [113](../40-process/113-world-graph-local-map-architecture-revision-v0.1.md) |
| **WorldGraph 编辑器** | [128](../40-process/128-world-graph-editor-usage.md) |
| **上一轮（击败瞬移／洞相机／刷怪表 GUI）** | [127](../40-process/127-defeat-teleport-cave-camera-spawn-table-gui-2026-08-16.md) |
| **上一轮（府近战／追击／刷怪区）** | [126](../40-process/126-control-core-chase-spawn-zone-rollup-2026-08-16.md) |
| **上一轮（将老／洞府／秘籍）** | [124](../40-process/124-jiang-lao-cave-manual-rollup-2026-08-15.md) |
| **功法任务接口契约** | [123](../40-process/123-quest-manual-api-interfaces-2026-08-15.md) |
| **上一轮（境界／打坐／突破仪式）** | [122](../40-process/122-cultivation-breakthrough-host-ritual-2026-08-15.md) |
| 上一轮（住房／主管府占领／Import） | [121](../40-process/121-housing-assignment-and-control-core-2026-08-15.md) |
| 再前（人物／工区／名册／倍速） | [120](../40-process/120-character-roster-editors-and-timescale-rollup-2026-08-15.md) |
| MapEditor（含刷怪区） | [112](../40-process/112-map-editor-usage.md) |
| 工区／人物编辑器 | [118](../40-process/118-npc-behavior-editor.md)／[119](../40-process/119-npc-character-vs-role-template-editors.md) |
| 对话／失败／流速 | [117](../40-process/117-npc-dialogue-host-ux-rollup-2026-08-14.md) |
| Ch01／背包／劳动 | [116](../40-process/116-recent-updates-rollup-2026-08-14.md) |
| 现在做到哪 | [62 现状](../40-process/62-project-status-2026-08-01.md) |
| 事件编辑器（含 onTalk） | [111](../40-process/111-content-studio-event-editor-usage.md) |
| 任务编辑器 | [110](../40-process/110-content-studio-quest-editor-usage.md) |
| 手操 Demo 0.1 | [105](../40-process/105-demo-0.1-producer-playbook-30min.md) |
| 编辑器总计划 | [106](../40-process/106-content-authoring-editors-plan-v0.1.md) |

## 2. 建议通读顺序（当前正式设计）

按顺序读，约可建立完整心智模型：

| 步 | 读什么 | 目的 |
|---|---|---|
| 1 | [策划案总览](00-overview.md) | 阶段、定位、支柱、索引 |
| 2 | [术语表](03-glossary.md) | 统一用词（Focus／Ledger／Region 等） |
| 3 | [架构冻结 v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md) | **主契约**；先读顶部 2026-09-12 定向补丁 |
| 4 | [ADR-0032](../40-process/43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md) → [0033](../40-process/43-decisions/ADR-0033-source-faithful-independent-encounter-and-world-anchor-return.md) → [0034](../40-process/43-decisions/ADR-0034-conflict-control-succession-and-airship-role.md) | 本轮跨系统决策与旧条款替代 |
| 5 | [实体模型](../30-tech/34-entity-and-component-model.md) → [Order/Action](../30-tech/35-order-and-action-system.md) | 世界对象与指令语义 |
| 6 | [Modifier](../20-systems/2C-attributes-and-modifier-pipeline.md) → [事件与账本](../20-systems/2E-events-and-world-state.md) | 数值与因果记账 |
| 7 | [世界](../20-systems/24-world-and-settlements.md) → [领地](../20-systems/26-territory-management.md) → [战斗](../20-systems/23-combat.md) → [势力](../20-systems/2A-factions-armies-diplomacy-and-capture.md) → [控制](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) | 当前规则的五个权威正文 |
| 8 | [第一章](../20-systems/2G-first-chapter-flow.md) → [开局](../20-systems/20-opening-experience.md) → [义务隐匿](../20-systems/2F-obligation-and-concealment.md) | 开局 Membership 与体验弧 |
| 9 | [路线图](../40-process/41-roadmap.md) → [VS 0.1 验收](../40-process/54-vertical-slice-0.1-acceptance-report.md) → [VS 0.2 计划](../40-process/55-vertical-slice-0.2-plan-v0.1.md) | 已完成边界与下一步（确认前不编码） |
| 10 | [Core M1（已完成）](../40-process/51-core-milestone-1-implementation-plan-v0.2.md)／[Data Pipeline（已实现）](../40-process/53-data-pipeline-milestone-1-plan-v0.2.md) | 骨架与数据管线 |
| 11 | 其余系统按总览索引按需深读 | 战斗／功法／领地等 |

## 3. 按角色的最短路径

| 你是… | 最少读这些 |
|---|---|
| 负责人通读方案 | 总览 → 术语 → `33` → ADR 索引 → 第一章 → 路线图 |
| 玩法策划 | 总览 → `2G`／`20`／`21`／`22`／`24`／`28`／`2F` |
| 程序准备开 Core／切片 | `33` → `54` 验收 → `55`（若做 VS0.2）→ `34`／`35`／`2C`／`36` |
| 只看 Demo | `45`～`49`；正式语义以 `32`＋`33` 为准 |

## 4. 状态怎么读

文档头常见标记：

- **已冻结**：实现不得擅自改语义；要改就升版 + ADR + Devlog。
- **形状／方向**：边界已定，细则可后补。
- **草稿／待定**：未冻；不要当实现依据。

## 5. 本地与飞书一致规则

1. 改文档只改本地 `.md`。
2. 新文档必须写入 `tools/feishu-map.json`，再 `--provision`／同步。
3. 飞书页底部「文档导航」由同步脚本根据映射生成，保证交叉链接在飞书可点。
4. 不要在飞书正文里直接改字（下次同步会覆盖）。
