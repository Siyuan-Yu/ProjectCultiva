# Core Milestone 1 Implementation Plan v0.2

> 状态：**已完成并通过验收** | 最后更新：2026-08-01  
> 类型：实施计划  
> 依据：`33` v0.2 §17、`34`、`35`、`36`、`2C`、`2E`、ADR-0022、`31`  
> 上级：[路线图](41-roadmap.md)｜[通读指南](../00-project/04-reading-guide.md)  
> 旧版：[v0.1](51-core-milestone-1-implementation-plan-v0.1.md)（已被本文件取代）  
> 后续：[Data Pipeline M1 计划 v0.1](53-data-pipeline-milestone-1-plan-v0.1.md)

## 0. 文档目的

在 Architecture Freeze v0.2 与人工确认前提下，指导正式 `XianXia.Core` **骨架**按阶段落地。  
**唯一目标：** 证明未来玩法系统都可以建立在统一 Core 上。

### 0.1 人工确认决策（2026-08-01，冻结入本版）

| # | 议题 | 决定 |
|---|---|---|
| 1 | Domain | **M1 保持在 `XianXia.Core` 内**作为命名空间／目录，**不拆**独立 asmdef |
| 2 | Snapshot | **JSON** 序列化（调试／可读／开发期正确性优先） |
| 3 | Random | 存档保存 **完整 PRNG 状态**（恢复一致性；实现简单） |
| 4 | AttributeId | M1 用**小枚举**验证管道：`MaxHp`、`Attack`、`Defense`、`Speed` 等；未来可扩展为 DefinitionId 驱动 |
| 5 | Unity Host | **EditMode 测试通过 = M1 逻辑完成**；Unity Host 仅可选烟测，**不阻塞** |

### 0.2 编码纪律（补充执行规则，2026-08-01）

1. **Demo 冻结**  
   - `Demo_v0_1`／`Assets/Scripts/Runtime/**` **只作参考**。  
   - **禁止**迁移、重构、删除 Demo Runtime；**禁止**改 Demo 场景。

2. **每阶段独立提交与门禁**  
   阶段完成后必须同时满足：  
   - 编译通过  
   - EditMode 测试通过  
   - 输出修改文件列表  
   - **等待人工确认**后再进入下一阶段  
   - 建议：每阶段一次独立 git commit（确认后或按负责人指示提交）

3. **未经批准禁止修改**  
   - ADR／Freeze **已冻结规则正文**（须走 ACR + 文档升版）  
   - `ProjectSettings/`  
   - `Packages/`（含 `manifest.json`；阶段 1 已批准加入 Test Framework 为例外，之后再改须另批）  
   - Demo 场景与 Demo Runtime（同条 1）

4. **设计阻塞则停码**  
   若发现：当前设计无法满足需求／须改冻结架构／须新增核心概念 → **立即停止编码**，提交设计问题（ACR／书面疑问），**不得自行拍板后继续实现**。

5. **范围禁区（重申）**  
   不扩展 Demo；不加战斗、修炼、NPC AI、势力系统、跨 Region 离屏、Mods/ 加载。

### 0.3 全局硬约束（全程适用）

| 必须 | 禁止（M1） |
|---|---|
| 普通 C# 组合模型（ADR-0002） | 完整 Unity ECS / DOTS |
| Core／Data **零** `UnityEngine`（`noEngineReferences`） | 逻辑层引用 Unity API／`UnityEngine.Random` |
| WorldTick 唯一世界时间轴；ActionClock＝Duration | 两套独立时间推进 |
| Result／ErrorCode 表达业务失败 | 用未捕获异常驱动模拟分支 |
| ContentPackage **基础结构**（官方包即可） | `Mods/` 文件夹加载、任意脚本 Mod |
| 单 Region 内存运行验证 | 跨 Region 离屏、大战、真战斗、修炼玩法、完整 NPC AI、完整势力领导 |
| Snapshot＝JSON；Random＝完整状态 | 二进制存档／仅 seed+计数（M1） |
| EditMode 为完成标准 | 以 Unity Host 阻塞 M1 |

### 0.4 M1 成功判据（总）

1. `XianXia.Core`／`XianXia.Data` 编译且 **无** `UnityEngine` 引用。  
2. 单元测试覆盖：Id、Tick、Random、Modifier、Event、Order→Action、Snapshot 往返。  
3. （可选）薄 Unity Host 烟测；**非**阻塞。  
4. 无战斗／修炼／跨区／Mods 加载代码进入主路径。

### 0.5 阶段进度

| 阶段 | 状态 |
|---|---|
| 1 工程结构 | **已确认完成** |
| 2 基础类型 | **已确认完成** |
| 3 Result／Validation | **完成** |
| 4 Random | **完成** |
| 5 ContentPackage | **完成** |
| 6 Entity | **完成** |
| 7 AttributeModifier | **完成** |
| 8 DomainEvent | **完成** |
| 9 Order／Action | **完成** |
| 10 Snapshot | **已完成并通过验收**；整合烟测 PASS |

---

## 第一阶段：工程结构

### 1. 目标

建立强制分层的 asmdef 与目录，使「逻辑进 Core、配置进 Data、表现进 Unity」在编译期不可破坏。

### 2. 文件路径

```text
Assets/
  Scripts/
    Core/                         → XianXia.Core.asmdef（noEngineReferences）
      Domain/
      Simulation/
      Entities/
      Attributes/
      Events/
      Orders/
      Actions/
      Random/
      Results/
      Persistence/
    Data/                         → XianXia.Data.asmdef（noEngineReferences）
      Content/
      Serialization/
    Unity/                        → XianXia.Unity.asmdef（M1 薄宿主，可选）
      Host/
  Tests/
    EditMode/                     → XianXia.Tests.asmdef
Content/
  BaseGame/                       （目录预留；加载实现见阶段 5）
```

> Demo：`Assets/Scripts/Runtime/**` **不迁入 Core**，M1 不扩展。

### 3. 类／程序集职责

| 程序集 | 职责 | 可引用 | **禁止** |
|---|---|---|---|
| **XianXia.Core** | 规则与 Domain 命名空间 | BCL | **UnityEngine** |
| **XianXia.Data** | ContentPackage 读取／JSON Snapshot 序列化实现 | Core、BCL | **UnityEngine**；玩法结算 |
| **XianXia.Unity** | 可选 Host／表现 | Core、Data、UnityEngine | 写业务规则 |
| **XianXia.Tests** | EditMode 单测 | Core、Data、Test Framework | 场景依赖的核心断言 |

**Domain：** 仅 Core 内文件夹／命名空间（已确认）。

### 4～7

同 v0.1：输入为工程脚手架；输出四 asmdef + 门禁测试；完成标准为四程序集可编译且 Core／Data 无 UnityEngine；不做 Demo 搬家、不做 DOTS。

---

## 第二阶段：基础类型

同 v0.1（EntityId、DefinitionId、SourceRef、WorldTick、ActionClock、ActionId、EventId、SnapshotId）。  
RegionId 占位即可。

---

## 第三阶段：Result／Validation

同 v0.1。业务失败走 Result；Tick 内禁止用异常表达玩家命令失败。

---

## 第四阶段：随机系统

同 v0.1，并锁定：**Snapshot 持久化完整 PRNG 状态**（已确认）。

---

## 第五阶段：ContentPackage 基础

同 v0.1。仅显式加载 BaseGame；不做 Mods/ 扫描。

---

## 第六阶段：Entity 基础

同 v0.1。最小组件白名单；不实现完整 `34` 可选模块。

---

## 第七阶段：AttributeModifier

同 v0.1，并锁定：**AttributeId 小枚举**（至少 `MaxHp`、`Attack`、`Defense`、`Speed`）；公式锁 `2C` 黄金例。  
未来允许扩展为 DefinitionId 驱动（非 M1 范围）。

---

## 第八阶段：DomainEvent

同 v0.1。最小事件队列；不做完整 Ledger。

---

## 第九阶段：Order／Action

同 v0.1。Wait／样例 Action + SimulationLoop；双时间职责单测锁定。

---

## 第十阶段：Snapshot

同 v0.1，并锁定：**JSON**（`XianXia.Data` 序列化实现）；往返以 EditMode 测试为准。  
Unity Host 存读为可选烟测。

---

## 推荐开发顺序与风险

同 v0.1 阶段顺序与风险表。随机策略风险已关闭（完整状态）；Snapshot 格式风险已关闭（JSON）。

---

## 附录 A — ADR-0022 对照

同 v0.1 对照表；实施选项见 ADR-0022「M1 实施确认」节。

## 附录 B — 下一步

1. 按阶段编码；每阶段等待确认。  
2. 禁止顺手扩 Demo／战斗／修炼／NPC AI／势力。
