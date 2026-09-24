# 技术架构

> **2026-09-21 Final Seal：** [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) 冻结统一 Squad／PlayerParty、CharacterEncounter、Continuous Surface 与 Actual Administrative Control 的 runtime authority。旧 FormalArmy／ArmyStack／TerritoryRegion／StrategicEncounter runtime 已退休，只允许明确旧输入迁移；不得重建第二份 membership、位置或战斗 authority。

> **2026-09-22 正式运行依赖退役：** `SimulationWorld` 无 HexWorld，Core Hex 目录已删除，正常产品程序集不再编译旧 Hex 几何或运行时迁移 API。Runtime Loader 明确拒绝 `formalArmy`／`hexWorld`。`LegacyRuntimeConverter` 只无损转换 FormalArmy 与 current authority 完整的 hybrid Snapshot；`hexWorld`／`openingHexWorldId` 只检测并拒绝，须走现有 WorldComposer／SurfaceAuthoring Legacy migration 路径且无样例时不猜。外部 JSON／Snapshot 旧 wire key、稳定 ID 规则与已移除枚举值留下的数值空洞只在检测和离线转换边界保留。

> 主契约：[`33-architecture-core-rules-freeze-v0.2.md`](33-architecture-core-rules-freeze-v0.2.md)
> 桥接：[`32-prototype-to-product-bridge.md`](32-prototype-to-product-bridge.md)
> **Architecture Freeze v0.2＋ADR-0038。** Hex／Army 正式运行依赖退役与 021915 统一收尾已在 `9b32fe0` 封板。新功能仍需制作人授权；不得按 Legacy 关键词重开清理。
> **2026-09-12 补丁：** Freeze 的定向修订由 ADR-0032～0034 管理。Continuous Surface 保存世界空间身份与连续位置；临时 Encounter 使用独立战术坐标，并在一次结算中恢复各自战前世界锚点、保留当前领域结果。SiteCore 行政覆盖、Encounter 实例和 WorldMap UI 都不得成为第二份位置真源。

> **2026-09-24 Player Control Continuity Seal：** [ADR-0039](../40-process/43-decisions/ADR-0039-external-faction-control-handoff.md) 冻结 Party 内顺序接替、Emergency External Handoff 与 True-Death Succession。Snapshot 保持 v8；Squads、ControlledSquadId、PlayerParty runtime、PlayerPartyWorldMotion、CharacterWorldPresence 与 Separate Space state 已足够表达成功态和无候选等待态。

## 0. 文档分工

| 文档 | 内容 |
|---|---|
| **`33` v0.2** | 主契约 |
| **`34`～`36`／`2C`／`2E`** | 展开 |
| **本文 `31`** | 程序集与工程约定 |
| **`37` 飞书** | 同步工具 |

## 1. 已定原则

### 1.1 逻辑与表现分离（最重要）

游戏核心逻辑（境界、属性、事件、战斗结算）写成**不依赖 UnityEngine 的纯 C#**，放在独立程序集里。

```
XianXia.Core/        纯 C#，无 UnityEngine 引用，游戏规则全在这
XianXia.Data/        配置表定义与加载
XianXia.Unity/       表现层：MonoBehaviour、UI、输入、渲染
XianXia.Tests/       针对 Core 的单元测试
```

用 Assembly Definition (`.asmdef`) 强制这个边界，让"不小心 using UnityEngine"直接编译报错。

### 1.2 数据驱动

- 所有可增删的内容（功法、神通、词条、丹药、事件、妖兽、掉落）走配置表
- 配置源用 **CSV/JSON 文本格式**，不用纯 ScriptableObject 资产作为真源
- 每张表有版本号与校验步骤，加载失败要报出具体行号

### 1.3 数值可溯源（细节见 `33` §1）

属性计算走统一 AttributeModifier 管道；禁止直接改 Final。

### 1.4 确定性与随机

- 所有随机走可注入的随机源（seed 可保存）
- 禁止在逻辑层直接调 `UnityEngine.Random`

### 1.5 时间推进（细节见 `33` §2）

- **1 Tick = 15 游戏分钟；1 日 = 96 Tick**
- 逻辑层只认 Tick；`GameClock` 为表现层
- 禁止各系统自行用真实时间做逻辑结算

### 1.6 实体分层（细节见 `33` §3）

四层：可控修士全模拟／关键 NPC 全模拟／其他修士为持久真实 Character + LOD／凡人可统计聚合。

## 2. 待定项（工程选项，非玩法形状）

| 项 | 选项 | 状态 |
|---|---|---|
| Unity 版本 | 2022.3.6f1 | 已定，见 ADR-0001 |
| 渲染管线 | Built-in | 已定，见 ADR-0001 |
| UI 方案 | UGUI／UI Toolkit | 待定（ADR-0002） |
| 存档 | JSON Snapshot（`WorldSnapshot` + `JsonSnapshotSerializer`） | schema v8 已实现 Content Progress、动态 Quest Instance 与 External Control Handoff authority；成功态不重复 handoff，无候选态在 world shell 完整恢复后重试；见 257／258／259／260 |
| 事件脚本化 | 纯配置表／轻量表达式 | 待定；依赖 `2E` |

## 3. 工程约定

- 目录结构、命名规范：待 `34-conventions.md` 补充
- Git：`.gitignore` 用 Unity 官方模板；大文件考虑 Git LFS
- 分支：个人开发用 `main` + 功能分支即可
- 提交信息带类型前缀（`feat/fix/docs/refactor/data`）

## 4. 跨设备开发方案

1. 全部内容（含 docs）在同一个 Git 仓库
2. 远端私有仓库
3. Unity 的 `Library/`、`Temp/`、`Logs/` 不入库
4. Unity 版本锁定，避免 Hub 自动升级

## 5. 实现期入口顺序（确认规则后再做）

见 `32` 第 5 节：asmdef → Tick → Modifier → Action → 实体分层 → 第一次突破。

## 6. 历史输入与离线转换边界

- 正式 Runtime Data 只加载当前 `outdoorSurface`／`npcSquad`；旧 `formalArmy`、`initialFormalArmyIds`、`hexWorld` 与旧 Army／Hex Snapshot 不能进入自动 bootstrap。
- 唯一转换入口是 `ExternalTools/ContentAuthoring/LegacyRuntimeConverter`。输入只读；输出必须是不同且尚不存在的独立文件。转换失败不得生成部分输出。
- ID 三规则严格分离：旧 Content 有 `runtimeArmyId` 时输出 `squad:migrated:<normalizedRuntimeArmyId>`；缺失时输出 `squad:legacy:<normalizedDefinitionId>`；旧 Snapshot 输出 `squad:army:<armyId>`。
- `ContinuousWorldMovementScale.Resolve` 只读 `SimulationWorld.ContinuousWorldMovementScale`；该值由当前 opening `outdoorSurface.movementScale` 唯一注入。当前 BaseGame 显式为 `1.0`，它不是 `cellSize`。
- 旧 wire key、数值空洞与历史 DTO 可用于明确拒绝和离线识别；不得为它们重新增加 runtime enum 成员、Hex 几何依赖或 compatibility adapter。

## 7. 当前 Snapshot 与内容状态边界

- 磁盘 authority 是 `WorldSnapshot` 经 `SnapshotService`／`JsonSnapshotSerializer` 的 capture／restore 链；某个 Board 仅有 `CaptureRuntime`／`RestoreRuntime` 或事务 memento，不代表它已经进入磁盘 Snapshot。
- 当前已接线实体、空间、PlayerParty／Squad、CharacterEncounter、Separate Space、背包、关系、随机、WorldOpportunity／WorldActivity 与洞府 taken-loot 等既有字段；完整清单以 [247 系统现状总表](../40-process/247-project-handoff-current-state-2026-09-18.md#当前系统现状总表2026-09-22) 和 serializer 实际 wire 为准。
- SAVE-01 的 v7 Content Progress authority 继续保留；QUEST-INSTANCE-01 将当前格式升为 v8，Quest runtime 以稳定实例身份保存发布者、Opportunity 来源、接取者、期限、交付与失败原因以及下一实例序列。Active dialogue 仍不保存；v1～v7 不猜测动态委托发布者，统一要求新开局。详见 [257](../40-process/257-save-01-content-progress-persistence-v1-2026-09-23.md)／[258](../40-process/258-quest-instance-01-dynamic-character-commissions-v1-2026-09-24.md)。
