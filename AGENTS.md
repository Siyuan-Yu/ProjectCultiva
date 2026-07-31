# AI 协作约定

本文件供 Cursor / AI 助手在本项目中自动读取。目标是让**不同时间、不同设备、不同 AI 会话**产出一致的结果。

## 当前阶段：架构冻结文档包（待人工审核）

**已于 2026-07-31 结束 Demo 功能扩展，进入架构冻结。本轮已写入文档包，审核前不编码。**

- **只写／改设计文档**，不要开始正式 Core 编码，也不要继续堆 Demo 功能。
- 主契约：`docs/30-tech/33-architecture-core-rules-freeze-v0.1.md`（含死亡、PlayerAgency、Mod Ready）
- 必读展开：`34`、`35`、`36-content-package-and-mod-architecture.md`、`2C`、`2E`、`32`
- Demo 快照：`docs/40-process/49-demo-v0.1-prototype-status.md`
- 贵重决策：ADR-0002～0008、0010～0016（UI=0009 预留）
- 未写入冻结文档的内容仍标「待确定」，不得为了推进而自行拍板。
- 变更已冻结规则必须：升版本／写 ADR／记入 `42-devlog.md`。

## 开工前必读

1. 最新的 `docs/40-process/44-session-handoff-*.md`（若有）
2. `docs/00-project/00-overview.md`
3. `docs/30-tech/33-architecture-core-rules-freeze-v0.1.md`
4. `docs/30-tech/34-entity-and-component-model.md`、`35-order-and-action-system.md`、`36-content-package-and-mod-architecture.md`
5. `docs/20-systems/2C-attributes-and-modifier-pipeline.md`、`2E-events-and-world-state.md`
6. `docs/30-tech/32-prototype-to-product-bridge.md`
7. `docs/10-benchmark/14-borrow-and-differentiate.md`
8. `docs/40-process/42-devlog.md` 最新 2～3 条

## 硬性规则

1. **文档先于代码。** 架构冻结阶段默认**不写代码**。
1.1 **总览只放大纲。** 细节进 `20-systems/` 或 `30-tech/`。
2. **命名走术语表。** AttributeModifier；Order／Action（无 Intent）；DefinitionId=`namespace:local_id`。
3. **逻辑层不许引用 UnityEngine。** 随机走 `IRandomSource`；时间走 WorldTick／ActionClock。
4. **数值不写死。** CSV／JSON 真源；官方内容走 ContentPackage，禁止专用硬编码加载路径。
5. **数值必须可溯源。** 禁止直接改 Final；Mod 效果须经正式契约，禁止任意 C# Mod（现阶段）。
6. **隐匿三层不合并。**
7. **死亡默认永久。** `IsStoryImportant ≠ CannotDie`；仅显式 TemporaryProtection。
8. **控制权分离。** 禁止 IsPlayerCharacter／单一 FactionId 包办；始终有 FocusCharacter。
9. **改了实质内容就更新 devlog。**
10. **贵的决定要写 ADR。**
11. **明确不做的东西不要提议**（完整 ECS、完整回放、完整 GOAP、现阶段 Workshop／任意脚本 Mod 等）。

## 范围纪律

- 优先垂直切片可验证的最小设计。
- 架构边界已冻结：程序集分离、组合实体、Modifier、双层时间、Order/Action、快照存档、地图四类、多队分级模拟。

## 代码约定（实现期生效；当前阶段不编码）

- C#；逻辑层可单测；注释写为什么。
- 配置文本真源；校验失败阻止进游戏。

## 回答语言

中文。
