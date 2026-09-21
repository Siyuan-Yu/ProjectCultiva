# 技术架构

> **2026-09-21 Final Seal：** [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) 冻结统一 Squad／PlayerParty、CharacterEncounter、Continuous Surface 与 Actual Administrative Control 的 runtime authority。旧 FormalArmy／ArmyStack／TerritoryRegion／StrategicEncounter runtime 已退休，只允许明确旧输入迁移；不得重建第二份 membership、位置或战斗 authority。

> **2026-09-22 命名闭包：** 当前内部 compatibility API 必须使用真实代码现名：`LegacyFormalArmyDefinition`、`LegacyArmyContentToSquadMigration`、`InitialLegacyFormalArmyIds`、`SimulationWorld.LegacyHexWorld`、`LegacyHexMetadataProjection` 与 `PlayerPartyWorldMotion.Legacy*`。外部 JSON／Snapshot wire key（`formalArmy`、`initialFormalArmyIds`、`sourceFormalArmyId`、`currentHexQ/R`）保持稳定，不随 C# 改名。`HexCoord`／`HexMath`／Odd-R Q/R 和 `HexWorld` 是合法几何／工具类型；正常 authority 仍是 Surface exact position、SquadWorldMotion 与 CharacterEncounter。

> 主契约：[`33-architecture-core-rules-freeze-v0.2.md`](33-architecture-core-rules-freeze-v0.2.md)
> 桥接：[`32-prototype-to-product-bridge.md`](32-prototype-to-product-bridge.md)
> **Architecture Freeze v0.2＋ADR-0038。** 新功能仍需授权；当前 Final Seal 只允许 compatibility／dead-code／文档收尾。
> **2026-09-12 补丁：** Freeze 的定向修订由 ADR-0032～0034 管理。Continuous Surface 保存世界空间身份与连续位置；临时 Encounter 使用独立战术坐标，并在一次结算中恢复各自战前世界锚点、保留当前领域结果。SiteCore 行政覆盖、Encounter 实例和 WorldMap UI 都不得成为第二份位置真源。

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
| 存档 | JSON（建议先用）／二进制 | 待定 |
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

## 6. Legacy adapter 工程边界

- 旧 Content 启动链为外部 `initialFormalArmyIds` → 内部 `InitialLegacyFormalArmyIds` → `LegacyArmyContentToSquadMigration` → `NpcSquadContentBootstrap`；不得创建 FormalArmy／ArmyStack。
- `LegacySquadMigrationIdentity.SquadIdFromLegacyArmyId` 必须保留稳定 `squad:army:` identity；`LegacyFormalArmyWorldMotion = 2`、Encounter spatial `LegacyFormalArmy = 2` 属于稳定数值协议。
- `EncounterCharacter.LegacySourceFormalArmyId` 只映射 wire `sourceFormalArmyId`；现代 producer 写 Squad identity。
- `ContinuousWorldMovementScale.Resolve` 对 `LegacyHexWorld.HexSize` 是只读尺度适配，不代表依赖已消除；不得据此把 Legacy Hex grid 提升为路由或位置 authority。
- `LegacyPartyFocusCompatibility.SyncPartyFocus` 只服务旧 EditMode fixture；`ModuleId.Army` 与 `armyOpen` 已删除，不得恢复 Host Army 产品入口。
