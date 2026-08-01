# 文档通读指南

> 状态：现行 | 最后更新：2026-08-01  
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
| 过程 | `40-process/` | 路线图、日志、Demo 记录、审计、**ADR 决策** |

阅读原则：

1. **总览只放大纲**；细节进专题页，避免一篇万字墙。  
2. **冲突时以 `33` 冻结条文为准**，再回头改系统页。  
3. **ADR 只记已拍板决策**；未决问题留在各页末尾或总览第九节。  
4. **飞书 = 阅读层**；本地 `.md` = 唯一真源（见 `37-feishu-sync.md`）。

当前结构已经适合「仔细通读 + 交叉跳转」。本轮整理重点是：补齐飞书映射、加强入口导航，**不重写已冻结规则正文**。

## 2. 建议通读顺序（审核 Freeze v0.2）

按顺序读，约可建立完整心智模型：

| 步 | 读什么 | 目的 |
|---|---|---|
| 1 | [策划案总览](00-overview.md) | 阶段、定位、支柱、索引 |
| 2 | [术语表](03-glossary.md) | 统一用词（Focus／Ledger／Region 等） |
| 3 | [架构冻结 v0.2](../30-tech/33-architecture-core-rules-freeze-v0.2.md) | **主契约**；先读冻结清单速查 |
| 4 | [ADR 决策索引](../40-process/43-decisions/README.md) | 扫一眼已采纳决策；细节按需点开 |
| 5 | [实体模型](../30-tech/34-entity-and-component-model.md) → [Order/Action](../30-tech/35-order-and-action-system.md) | 世界对象与指令语义 |
| 6 | [Modifier](../20-systems/2C-attributes-and-modifier-pipeline.md) → [事件与账本](../20-systems/2E-events-and-world-state.md) | 数值与因果记账 |
| 7 | [时间](../20-systems/21-core-loop-and-time.md) → [地图](../20-systems/24-world-and-settlements.md) → [关系](../20-systems/28-jianghu-relations.md) | 双时间、三层地图、关系真源 |
| 8 | [第一章](../20-systems/2G-first-chapter-flow.md) → [开局](../20-systems/20-opening-experience.md) → [义务隐匿](../20-systems/2F-obligation-and-concealment.md) | 开局 Membership 与体验弧 |
| 9 | [路线图](../40-process/41-roadmap.md) → [Core M1（已完成）](../40-process/51-core-milestone-1-implementation-plan-v0.2.md) → [Data Pipeline v0.2](../40-process/53-data-pipeline-milestone-1-plan-v0.2.md) | 下一步边界 |
| 10 | 其余系统按总览索引按需深读 | 战斗／功法／领地等 |

## 3. 按角色的最短路径

| 你是… | 最少读这些 |
|---|---|
| 负责人通读方案 | 总览 → 术语 → `33` → ADR 索引 → 第一章 → 路线图 |
| 玩法策划 | 总览 → `2G`／`20`／`21`／`22`／`24`／`28`／`2F` |
| 程序准备开 Core | `33` → `34`／`35`／`2C`／`2E`／`36` → ADR-0022 |
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
