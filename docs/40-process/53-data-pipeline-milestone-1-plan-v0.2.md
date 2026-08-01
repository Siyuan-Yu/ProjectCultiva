# Data Pipeline Milestone 1 Plan v0.2

> 状态：**已批准（决策已冻结）；编码须另开任务** | 最后更新：2026-08-01  
> 类型：实施计划  
> 前置：Core Milestone 1 **已完成并通过验收**  
> 依据：ADR-0004、ADR-0014／0015、`36`、`2C`／`2E`／`34`、Core M1 Registry／Loader  
> 上级：[路线图](41-roadmap.md)｜[Core M1 Plan v0.2](51-core-milestone-1-implementation-plan-v0.2.md)  
> 旧版：[v0.1](53-data-pipeline-milestone-1-plan-v0.1.md)

## 0. 目的

在已冻结的 Core 骨架上，接入**第一批真实游戏数据**（ContentPackage／BaseGame），验证：

```text
磁盘定义 → 严格校验 → DefinitionRegistry
  → 创建实体／由 Core 挂 Modifier
  → DomainEvent／Order 闭环可读
```

**不做：** 大型编辑器、Mods/ 加载、改 Freeze 架构、完整 Localization 运行时、战斗／修炼完整数值表、Unity 场景内容管线重构、Demo 污染。

### 0.1 人工确认决策（2026-08-01，冻结）

| # | 议题 | 决定 |
|---|---|---|
| 1 | 数据格式 | 运行时 **JSON 为主**；**CSV 为辅助输入**；**Excel 仅策划编辑源**，不作运行时格式 |
| 2 | 未知字段 | **严格模式**：未知字段、重复 ID、非法引用 **必须阻断加载** |
| 3 | Localization | **不实现**完整 Localization；结构预留 `NameKey`／`displayNameKey` 等扩展点即可 |
| 4 | Modifier | **规则与计算全部在 Core**；Data **只**提供 Modifier 配置定义；**禁止** Data 做玩法结算 |

---

## 1. 数据源格式

| 用途 | 格式 | 规则 |
|---|---|---|
| 运行时真源 | **JSON** | Definition 表（characters／manuals／items） |
| 辅助输入 | **CSV** | 可选扁平导入；入库或加载前转为／并入约定结构；不得绕过校验 |
| 策划编辑 | Excel／飞书表 | **仅作者态**；导出 JSON／CSV；**禁止**运行时读 xlsx |

- 不引入 Excel 运行时库；不擅自改 `Packages/`。  
- Schema／包 Version 写入 manifest 或表级 `schemaVersion`。

---

## 2. ContentPackage 扩展

```text
Content/BaseGame/
  manifest.json
  Data/
    characters.json
    manuals.json
    items.json
  Localization/          （本里程碑可空目录；无完整本地化系统）
```

| 做 | 不做 |
|---|---|
| 多 JSON 表 → 同一 Registry | Mods/ 扫描、第二包、Patch 合并、热重载编辑器 |

Loader：显式路径列表；按约定文件名 `RegisterCharacter`／`RegisterManual`／`RegisterItem`。

---

## 3. Definition 数据结构

`DefinitionId`＝`namespace:local_id`（官方 `base`）。Data 只持 DTO。

### 3.1 CharacterDefinition

| 字段 | 说明 |
|---|---|
| `id` | DefinitionId |
| `displayNameKey`／`nameKey` | **预留**本地化键（可暂存可读字符串，非完整 Loc 系统） |
| `baseAttributes` | AttributeId 名 → int |
| `tags` | 可选 |
| `defaultLifecycle` | 可选，默认 Alive |

### 3.2 ManualDefinition

| 字段 | 说明 |
|---|---|
| `id` | 如 `base:manual_basic_breath` |
| `displayNameKey`／`nameKey` | 预留 |
| `grantedModifiers` | `targetAttribute`、`operation`、`value`、可选 `stackingKey` |
| `sourceKind` | 默认 `Manual`（供 Core 建 SourceRef） |

### 3.3 ItemDefinition

| 字段 | 说明 |
|---|---|
| `id` | 如 `base:item_rough_wood` |
| `displayNameKey`／`nameKey` | 预留 |
| `maxStack` | int≥1 |
| `tags` | 可选 |

### 3.4 Registry

`Characters`／`Manuals`／`Items`；`TryGet*`；重复 ID → Fail。

---

## 4. 数据验证（严格模式）

```text
读文件 → JSON 语法 → 必填字段
  → 拒绝未知字段
  → DefinitionId／AttributeId／Operation 合法
  → 数值范围 → 非法引用失败
  → 注册（禁止静默覆盖）
```

失败 → `ValidationReport`／`Result`，**阻断**进入可玩状态。  
单测：未知字段、重复 ID、非法 Attribute／引用 → 必须失败。

---

## 5. 第一个数据闭环

1. 加载含角色／功法／物品的 BaseGame。  
2. Core 按 Character 定义创建实体并设 Base。  
3. **Core** 读取 Manual 的 Modifier **配置**并 `AddModifier`／管道计算 Final（`2C`）。  
4. 物品可查询即可（无背包系统要求）。  
5. DomainEvent + Wait Order + Snapshot 往返后 Final 仍正确。

样本 ID（定稿后勿随意改名）：`base:character_labor_disciple`、`base:manual_basic_breath`、`base:item_rough_wood`。

---

## 6. 与 Core 的连接（职责锁死）

| 层 | 允许 | 禁止 |
|---|---|---|
| **Data** | 解析 JSON／CSV 辅助输入、DTO、校验结构、填 Registry | 改 Final、跑公式、推进 Tick、发玩法副作用结算 |
| **Core** | `AttributePipe`、Add／Remove Modifier、SourceRef、Event、实体生命周期 | 从磁盘读包（可由 Data 注入已加载 Registry） |

建议形态：Core 提供依据 Manual DTO／已注册定义应用 Modifier 的领域 API；Data Loader 结束后把 Registry 交给 Core／Simulation。

```text
JSON/CSV(辅助) → Data Loader（严格校验）→ Registry
    → Core：CreateCharacter + ApplyModifiersFromDefinition
    → Events / Order / Snapshot
```

---

## 7. 建议实施阶段（另开编码任务后）

| 阶段 | 内容 | 门禁 |
|---|---|---|
| D1 | Schema＋样本 JSON（含 manuals／items） | 样本可解析 |
| D2 | Registry／Loader＋**严格**校验 | 坏数据／未知字段失败测 |
| D3 | Core：自定义 Modifier 应用 API | Final 黄金例 |
| D4 | 整合闭环测 | PASS |
| D5 | （可选）CSV 辅助导入路径 | 不阻塞主闭环 |

纪律：每阶段编译 + EditMode + 文件列表 + 等确认；Demo／ProjectSettings／Packages／冻结 ADR 禁擅改。

---

## 8. 风险

| 风险 | 缓解 |
|---|---|
| 枚举 AttributeId 不够 | 本里程碑优先用已有 MaxHp／Attack／Defense／Speed；扩展须确认 |
| Excel 被误当运行时 | 文档＋加载器不认 xlsx |
| Data 偷算 Final | Code Review／程序集职责测试 |

---

## 9. 完成标准（实现后）

- [ ] BaseGame 含角色／功法／物品定义  
- [ ] 严格校验阻断坏数据  
- [ ] Core 结算 Modifier；Data 无玩法计算  
- [ ] EditMode 闭环；无编辑器／Mods／Freeze 改动／Demo 污染
