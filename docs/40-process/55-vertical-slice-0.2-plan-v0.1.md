# Vertical Slice 0.2 Plan v0.1（范围收紧修订）

> 状态：**规划草案（待人工确认）** | 最后更新：2026-08-01  
> 类型：垂直切片实施计划｜**只规划，不编码**  
> 前置：[VS 0.1 验收报告](54-vertical-slice-0.1-acceptance-report.md) **已通过**  
> 依据：`2G`、`2F`、`21`、`35`、`33` v0.2、ADR-0018／0011／0012  
> **不修改 Core Freeze 正文**。  
> **本切片 ≠ 第一章完整实现**；只验证「杂役弟子第一天」**核心循环**。

---

## 1. Vertical Slice 目标

### 1.1 一句话

在**严格范围**内，用 EditMode（+ 可选薄调试 Host）验证：低境界杂役弟子在宗门日程下，可观察、可干预、干预有代价、能发现隐藏机会入口，并为后续秘密修炼循环留下 Action 接口。

### 1.2 核心体验目标（仅此六条）

玩家作为一个低境界杂役弟子，本阶段只验证：

| # | 体验 | VS0.2 对应验证 |
|---|---|---|
| 1 | 受到宗门日程限制 | Schedule 给出计划行为；无 Override 时角色按计划消耗时间 |
| 2 | 可以观察世界和角色状态 | 只读 Snapshot／Event／调试查询；`Observe` 走 Order→Action→Result |
| 3 | 可以主动干预角色行为 | `PlayerInput` → Player Order 覆盖 Schedule |
| 4 | 主动行为会产生代价 | Override 必有：时间消耗、任务影响、风险变化 |
| 5 | 可以发现隐藏机会 | 抽象「特殊地点／机会」标记（**无地图**）；发现后可提交修炼尝试 Order |
| 6 | 为后续秘密修炼循环建立接口 | `CultivationAttempt` 进入现有 Action 体系；**不**扩完整修炼／突破／功法 |

### 1.3 成功判据（窄）

- 同一套 `PlayerInput → Order → Action → Result/Event` 跑通：工作／休息／观察／修炼尝试（及无地图的抽象移动）。  
- 无玩家输入时，三人按日程消耗时间并推进任务计数。  
- 玩家 Override 后：**时间被扣、任务进度受影响、ExposureRisk 变化**（非「点一下直接成功」）。  
- 日终可产出：资源／任务／风险读数 + **关系变化接口事件**（可空实现）。  
- **不追求**剧情完整、第一章时长、叙事演出。

---

## 2. 系统依赖关系

```text
[已有 · 只读复用]
  Core M1：World／Entity／SimulationLoop／OrderQueue／ActionClock／WorldTick／Snapshot／DomainEvent／PRNG
  Data Pipeline：Character／Item／Cultivation Definition 加载（本切片不扩功法系统设计）
  VS0.1 Bootstrap：三杂役 Entity 生成入口
  VS0.1 CultivateAction／CultivationService：仅作「修炼尝试」的可选下游适配点
                     （本切片验收不要求突破／学法产品闭环）

[本切片新增 · 薄]
  PlayerInput Port          → 唯一玩家入站
  PlayerOrderFactory        → Order（Source=Player，高优先）
  ScheduleDefinition/Driver → 计划行为（低优先 Order），非强制 AI
  Labor / Rest / Observe / Move(抽象) / CultivationAttempt Actions
  DailyTaskQuota / 虚拟资源计数
  ExposureRisk (0–100)      → Override／异常时间来源
  OpportunitySite（抽象）   → 发现机会 → 允许提交 CultivationAttempt
  DayStarted / DayEnded 编排
  RelationshipDelta 接口事件（ledger 调用可 stub）

[明确不依赖／不建设]
  地图／寻路／LocalMap · 战斗 · 完整 NPC AI · 守卫巡逻 · 完整修炼／突破产品层
```

**依赖原则：** UI／调试壳只调 `PlayerInput` 与只读查询；**禁止** UI 直接改 Entity 组件或属性。

---

## 3. Order／Action 接入

### 3.1 强制管道

```text
PlayerInput
  → Order（OrderSource.Player）
  → IOrderTranslator → IAction
  → ActionClock 消耗 / CanStart·Tick·Complete
  → Result + DomainEvent
```

日程路径相同，仅 `OrderSource.Schedule` 且优先级更低。

### 3.2 VS0.2 玩家输入语义（最小集）

| 输入意图 | Order／Action | 说明 |
|---|---|---|
| 移动 | `MoveOrder` → `MoveAction` | **无格子地图**：只改抽象 `SiteTag`（如 LaborYard／RestArea／HiddenCorner）并耗时 |
| 工作 | `LaborOrder` → `LaborAction` | 推进日任务虚拟点数 |
| 休息 | `RestOrder` → `RestAction` | 耗时；可轻微影响风险或疲劳占位 |
| 修炼尝试 | `CultivationAttemptOrder` → `CultivationAttemptAction` | 见 §6；**不是**直接突破成功 |
| 观察 | `ObserveOrder` → `ObserveAction` | 耗少量时间；产出只读情报 Event（含是否揭示机会位点） |

### 3.3 禁止

- UI／MonoBehaviour 直接改 HP、属性、库存、风险数值  
- 绕过 Order 的「调试作弊写组件」作为正式路径（测试可造夹具，产品路径仍走管道）  
- 改 Demo Runtime 当正式输入层  

---

## 4. Schedule 最小模型

### 4.1 定位（冻结语义）

**Schedule 只提供计划行为，不是强制 AI。**

- 无更高优先级 Order／Active 玩家 Action 时：Driver 灌入对应计划 Order。  
- 有 Player Override：计划被压制，**不**改写 ScheduleDefinition。  
- 不做效用决策、不做主管 AI、不做玩家改表权限。

### 4.2 第一阶段示例日（映射到 Tick）

以 1 日＝96 Tick、每时辰≈4 Tick 对齐既有时间约定（测试日可缩短，语义表不变）：

| 钟点 | 计划相位 | 默认计划 Order |
|---|---|---|
| 06:00 | Wake | Rest／Wait（起床过渡） |
| 08:00 | Work | Labor |
| 12:00 | Rest | Rest |
| 13:00 | Work | Labor |
| 18:00 | Return | Move→RestArea 或 Wait（返回） |
| 22:00 | Sleep | Rest／Sleep Wait |

### 4.3 最小数据形

| 概念 | 字段（最小） |
|---|---|
| `ScheduleBlock` | `startTickInDay`、`endTickInDay`、`phase`、`plannedOrderType` |
| `ScheduleDefinition` | `id` + blocks[] |
| `ScheduleBinding` | EntityId → ScheduleDefinitionId |
| `ScheduleDriver` | 读 WorldTick → 当前 block → 条件满足则入队 Schedule Order |

数据落点：先测试夹具，整合前可迁 `schedules.json`（严格 Loader）。

---

## 5. Player Override

### 5.1 优先级（冻结）

**玩家命令优先于 Schedule。**  
日程相位切换**不得**在无规则地撕毁正在执行的玩家 Action；仲裁以 `35` 优先级表的 VS0.2 子集为准。

### 5.2 Override 必须产生代价（冻结）

Override **不是**免费成功开关。任何玩家覆盖日程的路径须同时满足：

| 代价维 | VS0.2 最小实现 |
|---|---|
| 时间消耗 | 经 ActionClock／WorldTick 推进；无「零时长瞬成」 |
| 任务影响 | 打断／缺席 Labor → 日任务点数低于计划预期（可测） |
| 风险变化 | `ExposureRisk` 增减（非日程行动、时间异常等来源，见 §7） |

示意（偷跑修炼尝试）：

```text
PlayerInput(CultivationAttempt)
  → Override 入队（压制 Schedule Labor）
  → CultivationAttemptAction
  → 消耗时间
  → 检查／累加 ExposureRisk
  → Result（可能：机会不足／风险过高拒绝／仅部分推进接口状态）
```

**禁止：** Override 直接改境界、直接学满功法、直接 `DailyQuotaMet=true`。

### 5.3 明确不做

- 完整中断损失经济矩阵  
- 多并行动作槽  
- AI 与玩家抢权的复杂仲裁  

---

## 6. 三个杂役角色

### 6.1 范围

只做**三个初始 Entity**。不写复杂背景、半固定创建 UI、灵根抽卡。

| 需要 | 说明 |
|---|---|
| ID | DefinitionId + EntityId |
| 基础属性 | 复用既有 Attribute 最小集 |
| 性格标签接口 | `PersonalityTags`（string／enum 列表占位）；**无**性格驱动 AI |
| Schedule | 三人绑定同一劳役 `ScheduleDefinition` |

### 6.2 控制

| 角色 | 控制 |
|---|---|
| 杂役 A | **FocusCharacter** + DirectControl |
| 杂役 B | DirectControl（可切换下令） |
| 杂役 C | DirectControl（可切换下令） |

复用 VS0.1 Bootstrap 入口；补 ScheduleBinding／ExposureRisk 初值／PersonalityTags 空或 1～2 个标签即可。

---

## 7. 第一天最小流程

```text
开始：三人进入杂役生活（Bootstrap + 绑定日程 + DayStarted）
  → 上午：第一次任务（Schedule → Labor）
  → 中途：玩家可遵守安排，或 Override（工作／休息／观察／抽象移动／修炼尝试）
  → 晚上：DayEnded 结算
```

### 7.1 日终结果（最小）

| 结果 | VS0.2 |
|---|---|
| 资源变化 | 虚拟劳役点／占位资源计数（不接真物流） |
| 任务完成情况 | `DailyQuotaMet`／`Failed` 事件 |
| 风险变化 | `ExposureRisk` 日终快照 Event |
| 关系变化接口 | 发 `RelationshipDeltaRequested`（或等价）事件；**可 stub**，不实现完整关系网演算 |

**不要**完整剧情、过场、主管对话树、夺府线。

---

## 8. 偷修接入（仅接口）

### 8.1 本阶段要验证的

```text
（可选）Observe / 脚本夹具 → 发现特殊地点（OpportunitySite 标记）
  → PlayerInput 提交 CultivationAttemptOrder
  → CultivationAttemptAction 进入 Action 体系
  → Result/Event（含耗时、风险、接口状态）
```

### 8.2 本阶段明确不实现

- 完整突破流程（验收不要求凡人→炼气）  
- 境界系统设计／扩展  
- 功法系统设计／扩展（不新增功法产品层；不把 LearnManual 当本日必经）  
- 「偷修直接成功」的捷径  

### 8.3 与 VS0.1 Cultivation Slice 的关系

- VS0.1 的 `CultivateAction`／`CultivationService` **可**作为 `CultivationAttemptAction` 的下游适配（例如内部转发一次既有 Cultivate Tick），以便将来秘密修炼循环复用。  
- **VS0.2 验收不绑定**学法、Progress 阈值、Breakthrough。  
- 「特殊地点」= 抽象 Site／Flag，**不是**地图系统。

---

## 9. 暴露风险：是否进入 VS0.2

### 9.1 判断

核心体验第 4 条（主动行为有代价）与 Override「风险变化」、日终「风险变化」均要求有可读数值。完整三层隐匿 + 被发现演出过重。

**结论：进入 VS0.2，但只做最小 `ExposureRisk`。**

### 9.2 最小模型

| 项 | 规定 |
|---|---|
| 名称 | `ExposureRisk`（数值 **0–100**；与 Freeze 中 PersonalConcealmentRisk 语义对齐时可做别名／同一字段，**不改 Freeze 正文**） |
| 来源（本切片） | 非日程行动（Override 偏离计划）；时间异常（工时段去做非计划事）；「被发现」用**规则／夹具触发**一次加分，**无**巡逻 AI |
| 不做 | 复杂怀疑 AI、Suspicion／FactionHostility 完整链、守卫追捕、暴露 GameOver |

可选：观察或休息对风险的微弱下降（非必须）。

---

## 10. 明确延期／不做（本阶段禁止加入）

| 延期／禁止 | 说明 |
|---|---|
| 战斗系统 | 含伤害、站位、技能栏 |
| 完整 NPC AI | 效用决策、主管行为树 |
| 守卫巡逻 | 寻路巡逻、发现演出 |
| 宗门外交 | — |
| 占领据点 | — |
| 城市系统 | — |
| 地图系统 | 格子、LocalMap、Region 旅行；移动仅为抽象 Site |
| 完整修炼／完整突破 | 功法／境界产品层；本日不验收突破 |
| 第一章完整叙事 | 40～60 分钟剧本、半固定创建、夺府线 |
| 改 Freeze／Demo／ProjectSettings／Packages | 禁擅改 |
| 玩家修改日程表 | 仅可查看计划 |

---

## 11. 实施阶段拆分

| 阶段 | 目标 | 主要交付 | 门禁 |
|---|---|---|---|
| **V2-A** | 日程相位 | `ScheduleBlock`／Tick→相位；示例日表 | 单测：06/08/12/13/18/22 边界 |
| **V2-B** | 计划 Action | Labor／Rest／抽象 Move；日任务虚拟点 | EditMode：纯 Schedule 可推进任务点 |
| **V2-C** | ScheduleDriver | 计划 Order 入队；非强制 AI | 无玩家时按表走；有玩家 Active 不无代价撕毁规则见 V2-D |
| **V2-D** | PlayerInput＋Override 代价 | Port；优先级；耗时＋任务影响＋风险 | Override 偷修尝试必改三点代价 |
| **V2-E** | Observe＋机会位点 | ObserveAction；OpportunitySite 标记 | 可发现 → 允许 CultivationAttempt |
| **V2-F** | CultivationAttempt 接口 | Order→Action→Result；可选适配 VS0.1 Cultivate | **不**要求突破／学法；管道断言 PASS |
| **V2-G** | 三角色＋第一天编排 | Focus+两可控；DayStarted/Ended；关系接口事件 | 一天整合测 PASS |
| **V2-H** | （可选）薄 Host | 调试按钮＋只读状态 | 不阻塞 EditMode 验收 |

每阶段：编译 + EditMode + 文件列表 + **停等确认**。禁止顺手做延期表内系统。

---

## 12. 每阶段 Cursor 开发任务（复制即用）

### Task V2-A — Schedule 相位
```text
角色：Development AI。遵守 AGENTS.md 与 52。
只做 VS0.2 V2-A（docs/40-process/55-vertical-slice-0.2-plan-v0.1.md）。
实现示例日相位映射（06起床/08工/12休/13工/18返/22睡）与单测。
禁止：地图、战斗、NPC AI、改 Freeze、扩 Demo、进 V2-B。
完成后停止等待确认。
```

### Task V2-B — Labor／Rest／抽象 Move
```text
只做 V2-B：Labor/Rest/抽象 MoveAction + 日任务虚拟点 + DomainEvent。
无真实库存、无寻路。禁止进 V2-C。
```

### Task V2-C — ScheduleDriver
```text
只做 V2-C：Schedule 只灌计划 Order，不是 AI。
禁止产品 UI、进 V2-D 以外的输入层。
```

### Task V2-D — PlayerInput 与 Override 代价
```text
只做 V2-D：PlayerInput→Order；玩家优先于 Schedule；
Override 必须产生时间消耗、任务影响、ExposureRisk 变化。
禁止直接改状态、禁止进完整修炼。
```

### Task V2-E — Observe 与机会
```text
只做 V2-E：ObserveAction + 抽象 OpportunitySite 发现。
禁止地图系统、守卫巡逻。
```

### Task V2-F — CultivationAttempt 接口
```text
只做 V2-F：CultivationAttempt 进入 Action 体系；可适配既有 Cultivate Tick。
禁止完整突破/境界/功法产品实现；验收不要求突破。
```

### Task V2-G — 第一天整合
```text
只做 V2-G：三杂役（1 Focus + 2 DirectControl）、性格标签接口、
DayStarted/Ended、资源/任务/风险/关系接口事件。禁止完整剧情。
```

### Task V2-H — 可选调试 Host
```text
可选：只读观察 + 按钮走 PlayerInput。不阻塞验收。禁止改 Demo Runtime。
```

---

## 13. 验收标准

- [ ] 所有玩家意图均经 `PlayerInput → Order → Action → Result/Event`；无 UI 直改状态  
- [ ] Schedule 仅提供计划行为；示例日相位可测  
- [ ] 无 Override 时三人按计划推进上午任务  
- [ ] Override 相对遵守安排：可测的时间差、任务点差、ExposureRisk 差  
- [ ] Observe 可揭示（或夹具设定）抽象特殊地点，并允许提交 CultivationAttempt  
- [ ] CultivationAttempt 走 Action 管道；**不**验收完整突破／功法／境界扩展  
- [ ] ExposureRisk 0–100；来源含非日程行动／时间异常／（夹具）被发现  
- [ ] 日终：资源变化、任务完成、风险、关系**接口**事件可断言  
- [ ] 一 Focus + 两可控制；PersonalityTags 接口存在  
- [ ] 无战斗／完整 NPC AI／守卫巡逻／外交／占点／城市／地图／Freeze／Demo 污染  
- [ ] EditMode 整合测 PASS  

---

## 14. 明确延期内容（汇总）

见 §10。另延期至后续切片／第一章：

- 秘密修炼完整循环（多日、藏匿点经营、真正突破节奏）  
- Suspicion／FactionHostility 与主管发现演出  
- 真资源物流与聚落库存  
- 关系网真实演算（本切片只留接口事件）  
- 产品级 UI／镜头／RTS 操作  

---

## 15. 风险点

| 风险 | 影响 | 缓解 |
|---|---|---|
| 与 VS0.1「偷修＝完整 Cultivate／突破」预期混淆 | 范围膨胀 | 本文 §8 冻结：本日只做 Attempt 接口 |
| 「移动／特殊地点」滑向地图系统 | 工期炸 | SiteTag／OpportunitySite 抽象；禁 LocalMap |
| Override 做成免费成功 | 核心体验第 4 条失败 | V2-D 门禁强制三维代价断言 |
| ExposureRisk 与 Freeze `PersonalConcealmentRisk` 命名分叉 | 术语债 | 实现时同一数值字段或文档别名表；不改 Freeze 正文 |
| Schedule「计划」被做成强制 AI | 违背 §4 | Driver 只入队 Order；无效用函数 |
| 关系／性格接口被做成完整系统 | 超范围 | 只留 Tags + Delta 事件 stub |
| 复用 CultivateAction 时顺手验收突破 | 违反「不完整修炼」 | 整合测断言不含 Breakthrough |

---

## 16. 编码前确认

范围已按本修订收紧。编码启动前只需：

1. **批准本计划**作为 VS0.2 实施真源（或列出要改的条目）。  
2. 确认 ExposureRisk **进入**（本文 §9 已采纳）与 CultivationAttempt **仅接口**（§8）无异议。

确认前：**禁止编码。**
