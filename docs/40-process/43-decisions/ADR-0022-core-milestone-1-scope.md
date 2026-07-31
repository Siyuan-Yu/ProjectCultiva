# ADR-0022：Core Milestone 1 范围冻结

- 状态：**已采纳**
- 日期：2026-07-31
- 决策者：项目负责人（Freeze v0.2）
- 修订：2026-08-01（M1 实施确认）

## 背景

审计要求限制第一阶段实现，避免偷做多系统。

## 决策

**M1 只验证统一 Core 骨架：** Id、WorldTick、IRandomSource、ContentPackage 基础、Entity 基础、AttributeModifier、DomainEvent、Order／Action、Snapshot、单 Region。

**M1 不做：** 跨 Region 离屏、完整势力领导、真战斗、完整 NPC AI、Mods/ 加载、大地图战争。

## M1 实施确认（2026-08-01）

人工审核 Implementation Plan 后确认：

| 项 | 决定 |
|---|---|
| Domain | 留在 `XianXia.Core` 内命名空间／目录，不拆独立 asmdef |
| Snapshot | JSON |
| Random 存档 | 完整 PRNG 状态 |
| AttributeId | 小枚举（`MaxHp`／`Attack`／`Defense`／`Speed` 等）；未来可 DefinitionId 化 |
| 完成标准 | **EditMode 测试通过**即算逻辑完成；Unity Host 可选、不阻塞 |

编码按 Plan v0.2 分十阶段推进；每阶段测试通过后待确认再进入下一阶段。

**补充执行规则（与 Plan v0.2 §0.2 一致）：** Demo Runtime 冻结；每阶段独立门禁与确认；未经批准不改 `ProjectSettings`／`Packages`／冻结 ADR 正文／Demo 场景；设计冲突停码提 ACR。

详细步骤见：`docs/40-process/51-core-milestone-1-implementation-plan-v0.2.md`。

## 影响

见 `33` v0.2 §17、`41-roadmap`、Plan v0.2。
