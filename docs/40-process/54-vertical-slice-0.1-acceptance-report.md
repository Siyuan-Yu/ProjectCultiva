# Vertical Slice 0.1 验收报告

> 状态：**验收快照（只读总结，本文件不启动下一阶段）**  
> 日期：2026-08-01  
> 前置完成：Core M1、Data Pipeline M1、VS0.1 Bootstrap、Cultivation Slice 0.1  
> 相关提交（相对 Core M1 之后）：
>
> - `3ee16e1` feat(data): complete data pipeline m1-a definitions  
> - `90f89ea` feat(data): complete data pipeline m1-b import validation  
> - `6897807` feat(core): prepare vertical slice 0.1 bootstrap  
> - `64cb3ab` feat(core): cultivation vertical slice  
>
> 测试门禁（Cultivation Slice 完成后）：EditMode **73/73 Passed**

---

## 1. 当前已实现能力

### 1.1 Core 能力

| 能力 | 说明 |
|---|---|
| Id / Result / ValidationReport | `DefinitionId`、`EntityId`、业务错误码、聚合校验报告 |
| WorldTick / ActionClock | 世界时间由 Loop 推进；Action 只消耗自身 Clock |
| DeterministicRandom | 可捕获／恢复完整 PRNG 状态 |
| Entity + 组件白名单 | Identity／Attributes／Lifecycle／ActionState／**Cultivation** |
| AttributePipe + Modifier | Fixed／Percentage；SourceRef（含 Manual） |
| DomainEventQueue | 含 EntityCreated、ModifierAdded、ActionCompleted／Failed、OrderRejected、WorldInitialized、**Breakthrough** |
| Order → Action | Wait、ApplyModifier、**Cultivate** |
| SimulationLoop | 单 Region 入队、启动、Tick、完成清理 |
| Snapshot | JSON 往返；含修炼进度／境界／进行中 Cultivate |

### 1.2 数据能力

| 能力 | 说明 |
|---|---|
| ContentPackageLoader | 显式路径加载；严格未知字段拒绝 |
| DefinitionRegistry | Character／Cultivation／Item；跨类型重复 ID 阻断 |
| Runtime JSON | `characters.json`／`cultivation.json`／`items.json` |
| CSV→JSON（M1-B） | 仅 Authoring；校验失败不写盘；输出 ValidationReport |
| CultivationManualMapper | Content DTO → Core `CultivationManualSpec`（Data 不算 Final） |

样本定义含：劳役弟子、主角／同伴、基础吐纳、**青云诀**、粗木等。

### 1.3 World 初始化（Bootstrap）

| 能力 | 说明 |
|---|---|
| WorldInitData | Region／LocalMap／Settlement **占位数据结构**（无玩法） |
| GameStartBootstrap | 建 World → 建角色 Entity → 设 Base → 发初始化事件 |
| ContentGameStart | Content 加载 → 三角色 spawn → Core bootstrap |
| 初始角色 Definition | protagonist／companion_a／companion_b（性格 Tag、灵根／境界占位字段） |

**不是：** 地图加载、移动、聚落经营、开局剧情演出。

### 1.4 修炼闭环（Cultivation Slice 0.1）

```text
学习青云诀 → CultivateAction（ActionClock）
  → 每 tick +CultivationSpeed → Progress
  → Progress ≥ 阈值 → Breakthrough
  → Realm: Mortal → QiRefining
  → Snapshot 可恢复一致
```

| 能力 | 说明 |
|---|---|
| 学功法 | `CultivationService.LearnManual`；挂 Manual Modifier |
| 修炼 Action | Start → 耗 Clock → 加 Progress → Complete |
| 突破 | 仅凡人→炼气；`EventType.Breakthrough` |
| 数据字段 | RequiredRealm、CultivationSpeed、BreakthroughProgress、GrantedModifiers |

**明确不做：** 多境界、天劫、丹药、洞府、战斗挂钩。

---

## 2. 当前不可实现能力

以下能力**故意未做**；现有 API／数据不足以当成可玩垂直切片产品：

| 能力 | 缺口 |
|---|---|
| 玩家输入 | 无 Unity Input→Order 正式桥；Demo 未接入本 Core 闭环 |
| 工作系统 | 无 Labor／WorkZone／产量／劳役玩法 Action |
| 时间表 | 无 Schedule／时段驱动；OrderSource.Schedule 仅为枚举占位 |
| NPC AI | 无效用／目标选择；无自主下单 |
| 战斗 | 无战斗 Action／伤害／站位；Attribute 未服务战斗结算 |
| 地图 | WorldInitData 仅为结构占位；无格子、寻路、Region 流式、LocalMap 实例状态机 |

另：**完整 Localization、Mods 扫描、Excel 运行时、编辑器工具、UI 产品壳**均未实现。

---

## 3. 当前架构观察

### 3.1 未来可能拆分的组件／模块

| 现状 | 观察 |
|---|---|
| `CultivationComponent` 聚合 Realm＋Progress＋Manual＋Speed | 境界成长与“当前功法会话”可能拆为 RealmState／ManualLearning／CultivationSession |
| `CultivationService` 兼学法＋突破 | 突破规则膨胀后宜拆 `BreakthroughRule`／RealmTransition |
| `SimulationWorld` 承载 Layout＋实体＋队列＋随机 | Layout／Content 句柄与仿真运行态可分容器 |
| `Order` 可选字段堆叠（Wait／Modifier／Cultivate） | Order 类型增多后宜改为负载 DTO 或分类构造，避免巨型构造参数 |
| `Entity` 组件白名单硬编码 | 随系统增加需策略化（仍保持显式白名单，避免任意组件） |
| Snapshot DTO 字段持续膨胀 | 可按领域分片序列化（Cultivation／Action／Identity） |

### 3.2 可能需要扩展的接口

| 接口／点 | 可能扩展 |
|---|---|
| `IAction` / `IOrderTranslator` | 工作、移动、交互等新 OrderType；Snapshot Kind 恢复矩阵 |
| `EventType` | 学法专用事件、工作完成、日程触发等（避免滥用 payload 字符串） |
| `WorldInitData` → Snapshot | 布局目前不进存档；若开局世界需续档，要定持久化契约 |
| `RealmStage` | 仅 Mortal／QiRefining；完整境界须另案（禁静默扩枚举当完整系统） |
| `AttributeId` | 修为／灵力等若进入管道，需扩展枚举或并行资源账户 |
| Content ↔ Core | `CultivationManualSpec` 模式可推广到 Item／Building；避免 Core 引用 Data |
| `ValidationReport`／引用校验 | Realm Definition 真源、交叉引用表尚未体系化 |

### 3.3 是否存在需要 ADR 记录的问题

建议在进入下一阶段前**单独开 ADR／决策记录**（本报告不改 ADR 文件）：

1. **功法命名：CultivationDefinition vs Plan 中的 ManualDefinition**  
   实现与夜间任务用 Cultivation／`cultivation.json`；Plan v0.2 写 Manual／`manuals.json`。需正式统一术语与样本 ID 策略。

2. **WorldLayout 是否进入 Snapshot**  
   角色可存读；Region／LocalMap／Settlement 布局仅 bootstrap 注入。续档语义未定。

3. **境界模型边界**  
   Slice 用双值 `RealmStage` 验证闭环；与世界观完整境界／突破／天劫文档如何衔接，需 ADR 冻结“下一扩一阶”的规则，避免组件变万能境界系统。

4. **Progress 语义**  
   当前 Progress 为功法会话累计、每 tick 加 Speed；与“修为资源／经验池／隐匿修炼”等长期设计是否同一条轴，需决策。

5. **Demo／产品壳接入点**  
   Core 闭环已在 EditMode 验证；正式玩家输入与呈现层挂载点（禁止污染 Demo 临时逻辑）需桥接 ADR 或技术备忘。

**Freeze／已采纳 ADR：** 本阶段编码未改 `33`／`43-decisions` 正文；上述为**增量观察**，不是已批准变更。

---

## 4. 下一阶段建议（只提案，不编码）

优先级按“验收可玩感”与“架构风险”权衡，**任选其一开任务**，不要并行铺开：

### 方案 A — Presentation／输入最小桥（推荐若目标是“能看见闭环”）

- Unity／Runtime 薄适配：按钮或调试命令 → `CreateCultivateOrder`／Learn  
- 只读 HUD：Realm、Progress、Tick、最近 Breakthrough  
- **不做**完整 UI、地图、Demo 玩法扩张  

### 方案 B — 数据闭环补完（Data Pipeline D3／D4 精神）

- Core：从 Registry 定义一键 ApplyModifiers／学法封装稳定 API  
- 统一 Manual／Cultivation 命名与样本 ID  
- 引用校验（RequiredRealm 真源）与 CSV 列补齐  

### 方案 C — 凡人日常最小动作（工作，非战斗）

- 一个 `Work`／Labor Action + 简单产出计数  
- 与修炼抢 Action 槽（同一主体单 ActiveAction）验证互斥  
- **不做**时间表 AI、聚落经济完整系统  

### 方案 D — 架构决策周

- 先写／批 ADR：命名、Snapshot 布局、境界扩展纪律、Progress 语义  
- 再开编码；降低返工  

**建议组合：** 先 **D（短）** 定命名与 Snapshot／境界纪律 → 再 **A 或 C** 选一条可玩触点；地图／战斗／NPC AI 继续后置。

---

## 5. 验收结论

| 项 | 结论 |
|---|---|
| Core M1 骨架 | 已具备并可被后续切片复用 |
| Data Pipeline M1 | JSON 真源 + CSV 辅助导入 + 严格校验可用 |
| VS0.1 Bootstrap | 开局数据／三角色／事件框架可用（非玩法） |
| Cultivation Slice 0.1 | **凡人→炼气** 逻辑闭环 + Snapshot 一致，EditMode 通过 |
| 产品可玩垂直切片 | **未达成**（缺输入、呈现、日常、地图） |
| Freeze／ADR 正文 | 本阶段未改；存在待 ADR 的命名与持久化问题 |

**本报告止。不进入下一阶段编码。**
