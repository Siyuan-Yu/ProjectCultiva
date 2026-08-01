# Vertical Slice 0.3 Plan v0.1（设计确认修订）

> 状态：**设计确认中（待批准）**｜最后更新：2026-08-01  
> 类型：垂直切片实施计划｜**只规划，不编码**  
> 前置：[VS0.2 验收](56-vertical-slice-0.2-acceptance-report.md) **已通过**；VS0.1 Cultivation Slice 可用  
> 依据：`20`、`21`、`2F`、`35`、`33` v0.2、`36`；阶段叙事口径见 `2I`（非固定章节脚本）；协作 `52`  
> **不修改 Core Freeze 正文**。

---

## 0. 目标与硬约束

### 0.1 目标

设计并（批准后）实现**「第一天完整体验闭环」**——规则可证明、玩家可 RTS 干预的一日循环：

```text
日循环推进
  → Schedule 默认劳役／休息（无人下令时）
  → 玩家 RTS 直接选人下令（Override 优先）
  → Observe 发现抽象 OpportunitySite
  → 秘密修炼（闸门后复用既有 Cultivate）
  → 日终消费 Quota／Deviation 给出可读结果
```

这是**系统闭环验证**，不是可玩产品壳，也不是第一章过关。

### 0.2 必须遵守（冻结语义）

| 约束 | 口径 |
|---|---|
| **非固定剧情** | 无过场脚本强迫玩家按节拍点「下一句」；流程由时间＋玩家命令涌现 |
| **非章节脚本** | 不实现 `2G` 第一章阶段机／关卡导演；不用 Timeline 锁死第一天 |
| **玩家 RTS 直接控制** | 选中角色 → 下令 → `PlayerInput`→`Order`；禁止菜单式「今日行动」替代 |
| **Schedule＝NPC／凡人默认规则** | 无玩家 Order 时灌计划行为；**不是**限制玩家的锁 |
| **Player Override 优先** | `PlayerOrder > ScheduleOrder`；打断规则继承 VS0.2 |

### 0.3 成功判据（窄）

同一次 EditMode 整合测可断言：日界事件 →（可选守规矩 Labor）→ Observe 得 Site → Cultivate 涨 Progress → `DayEnded` 产生 Quota 后果事件／标记。全程不经 UI 直改状态。

---

## 1. VS0.3 范围（仅 A–D）

| 包 | 名称 | 要验证的事 |
|---|---|---|
| **A** | 时间／日循环 | Day／Hour／Tick 派生；`DayStarted`／`DayEnded`；日切钩子 |
| **B** | Observe＋OpportunitySite | RTS Observe Action；抽象 Site；KnownSites；发现事件 |
| **C** | 秘密修炼发现流程 | 有 Site 才可偷修；接入既有 Cultivate；偏离计划有代价信号 |
| **D** | 日终 Quota 结果 | 消费 VS0.2 的 Deviation／完成度；日终后果事件＋薄标记；日切重置 |

**范围外默认不做：** 产品 UI、地图、战斗、主管 Boss、完整关系、章节导演。  
**Exposure／PersonalConcealmentRisk：** 作为 **C 的可选薄支撑**（偷修代价）；见 §5.3——确认时二选一，默认**建议进入**。

---

## 2. 包 A — 时间／日循环

### 2.1 Day／Hr／Tick（对齐 `21`／`WorldTick.TicksPerDay=96`）

| 单位 | 定义 |
|---|---|
| Tick | 1 Tick＝15 游戏分钟 |
| Hour | 4 Tick＝1 游戏小时 |
| 时辰 | 8 Tick＝2 游戏小时（只读派生，逻辑仍认 Tick） |
| Day | 96 Tick＝1 游戏日 |

```text
tickInDay = WorldTick % 96
dayIndex  = WorldTick / 96
hourOfDay = tickInDay / 4   // 0..23
```

### 2.2 第一天「骨架」而非剧本

沿用／配置劳役 `ScheduleDefinition`（现有默认块可作样本）。下表是**默认行为示意**，不是必须触发的剧情节拍：

| TickInDay | 默认 Schedule | 玩家可做什么（RTS） |
|---|---|---|
| 0–8 | Rest | Override：Observe／Rest／… |
| 8–48 | Labor | Override：任意 Player Order（含偷修，有代价） |
| 48–56 | Rest | Observe 友好窗 |
| 56–80 | Labor | 同上 |
| 80–96 | Rest | 主偷修窗（仍非强制） |
| 跨日 | — | `DayEnded` → 结算 → `DayStarted` |

玩家完全可以全天只干活、或白天狂 Observe——**系统不得用章节脚本纠正**。

---

## 3. 包 B — ObserveAction＋OpportunitySite

### 3.1 Observe

```text
PlayerInput(Observe) → Player Order → ObserveAction → ObservationResolved
```

- 耗 ActionClock；工时段＝Override，可打 QuotaDeviation（VS0.2 规则）。  
- 产出：可能揭示 Site，或一无所获（`IRandomSource`；测试可强制成功）。  
- **禁止**对话树／FOV／地图扫描小游戏。

### 3.2 OpportunitySite（抽象）

| 字段（最小） | 说明 |
|---|---|
| `OpportunitySiteId` | 如 `base:site_abandoned_cave` |
| `AllowsCultivation` | 是否解锁偷修闸门 |
| KnownSites | 每实体（或小队共享，VS0.3 建议**每实体**）已发现集合 |

**无坐标、无 LocalMap、无寻路。** 发现＝写 KnownSites＋事件，不是「走到格子上」。

---

## 4. 包 C — 秘密修炼发现流程

```text
Observe 成功 → KnownSites 含可修炼 Site
  → 玩家 RTS 下达 Cultivate
  → CultivationAttemptGate（Site 已知？已学 Manual？）
  → 既有 CultivateAction → Progress↑
  →（建议）PersonalConcealmentRisk 累加
  → 若打断 Labor → QuotaDeviation（已有）
```

| 规则 | VS0.3 |
|---|---|
| 无 Site | 拒绝，可测原因 |
| 功法 | **建议**日始／开局 `LearnManual` 青云诀（减步骤；非剧情演出） |
| 突破 | 不强制；不作验收门禁 |
| 公式 | **禁止**重写修炼；只复用 VS0.1 Cultivate |

「发现流程」＝**规则闸门＋玩家选择**，不是「到达剧情点自动播放偷修」。

---

## 5. 包 D — 日终 Quota 结果

### 5.1 为何进入

VS0.2 已记账 `QuotaDeviationCreated`，无消费者则闭环缺最后一环。

### 5.2 薄后果（进入）

| 做 | 不做 |
|---|---|
| `DayEnded` 读完成度／Deviation | 主管实体、训斥对话、Boss 战 |
| `QuotaConsequenceApplied` 事件 | 完整愤怒条／Suspicion AI |
| 薄标记（如 `LaborStanding` 或 `PendingReprimand`） | 没收、GameOver、关系网演算 |
| 日切重置当日 Completed／Deviation（规则写清） | 处罚写进 `LaborAction` 内部 |

### 5.3 PersonalConcealmentRisk（C 的代价信号）

| 选项 | 说明 |
|---|---|
| **建议：进入** | 组件数值 0–100；偷修／工时偏离累加；正式名 `PersonalConcealmentRisk`（`03`：ExposureRisk 为展示映射） |
| 备选：推迟 | 本切片只做 A/B/C 闸门＋D Quota；偷修仅靠 Deviation 表达代价 |

**不进入：** 主管巡逻「被发现」演出、完整三层隐匿。

---

## 6. Core／Data／Narrative 分层

### 6.1 Core 新增（规则／仿真）

| 项 | 包 | 说明 |
|---|---|---|
| `DayClock`（或等价纯函数） | A | 由 WorldTick 派生 day／tickInDay／hour |
| DayDriver／Loop 钩子 | A | `DayStarted`／`DayEnded`；日切调用结算 |
| `ObserveAction`＋Order 类型／翻译 | B | PlayerInput 意图扩展 |
| `OpportunitySite` 运行时模型＋`KnownSites` 组件 | B | 无坐标 |
| `CultivationAttemptGate` | C | 校验后转发既有 Cultivate Order／Action |
| PlayerInput Cultivate 意图接线 | C | Factory→既有 Cultivate |
| （建议）`PersonalConcealmentRisk` 组件＋累加规则 | C | 0–100 |
| `QuotaConsequence` 结算器 | D | 只在 DayEnded 消费 Deviation |
| 相关 `EventType`＋Snapshot 字段 | A–D | 可存读 |
| EditMode 整合测 | — | 非固定剧本：用命令序列断言，不写死「必须午时发生」 |

**Core 复用、不当新系统重做：** ScheduleDriver、PlayerInputPort、Labor／Rest、Override、DailyTask／Deviation、CultivateAction、CultivationService、SimulationLoop、PRNG。

### 6.2 Data 新增（定义／加载）

| 项 | 包 | 说明 |
|---|---|---|
| `OpportunitySiteDefinition` DTO＋JSON 样本 | B | 如 `sites.json` 或并入既有包；严格 Loader |
| （可选）Observe 耗时／发现权重字段 | B | 可先 Core 常量，第二刀再进 Content |
| （可选）Schedule／日配额 Content 化 | A/D | 非门禁；测试夹具仍可用 |
| Registry 查询 API | B | `TryGetSite(DefinitionId)` |
| **不新增** | — | 地图 chunk、战斗表、主管 Boss 表、关系网表 |

功法／角色定义：**复用**既有 `cultivation.json`／`characters.json`，VS0.3 不扩境界体系。

### 6.3 Narrative 内容（文案／样本，非系统）

| 项 | 说明 |
|---|---|
| Site 名称／短描述 NameKey 或中文样本 | 「废弃洞口」类；**无**强制对话树 |
| Observation 结果文案键（可选） | 供日后 UI；Core 只发事件＋SiteId |
| 日终后果提示文案键（可选） | 「今日劳役亏空」类；**无**训斥演出脚本 |
| **明确不是 Narrative 交付** | 第一章分幕、半固定背景、主管 Boss 战脚本、过场 Timeline |
| 与 `2I` | 荒村杂役为**阶段叙事**（状态／触发／反馈）；VS0.3 只落地其中可规则化的一日闭环钩子，不把 `2I` 事件表整包实现 |

Narrative 角色本切片**不写新流程导演**；若需样本句，只进 Content 字符串／Key，不进 Core 分支剧情。

---

## 7. 明确不做

| 禁止 | 说明 |
|---|---|
| 战斗 | 伤害、站位、技能、遭遇战 |
| 地图系统 | LocalMap 玩法、格子世界 |
| 寻路 | Move 产品化、路径搜索 |
| NPC 高级 AI | 效用决策、目标栈、战术 |
| 主管 Boss 战 | 任何 Boss／决斗设计 |
| 完整关系系统 | RelationshipLedger 产品演算；仅可发空接口事件（默认**连接口事件也不做**，除非确认要求） |
| 固定剧情／章节脚本 | `2G` 阶段机、强制节拍 |
| 菜单替代 RTS | 「今日三选一」行动菜单 |
| 产品 RTS UI | 镜头／框选壳（可选调试 Host 不阻塞） |
| 改 Freeze／Demo／ProjectSettings／Packages | — |

---

## 8. 实施阶段（对齐 A–D）

| 阶段 | 对应 | 交付 | 门禁 |
|---|---|---|---|
| **V3-A** | A | DayClock；DayStarted／DayEnded；日切钩子（结算可空实现） | 跨日事件；hour／tickInDay 单测 |
| **V3-B** | B | ObserveAction；Site 定义＋KnownSites；发现事件 | 强制发现夹具 PASS；无地图 |
| **V3-C** | C | Gate＋Cultivate 接线；（建议）Risk 累加 | 无 Site 拒绝；有 Site Progress↑ |
| **V3-D** | D | DayEnded QuotaConsequence＋日切重置 | 有 Deviation 必出后果事件 |
| **V3-E** | 整合 | 非脚本化命令序列整合测 | EditMode PASS |
| **V3-F** | 可选 | 调试 Host 调 Port | 不阻塞；禁产品 UI |

每阶段：编译＋测试＋文件列表＋**停等确认**。

---

## 9. Cursor 任务模板

### Task V3-A — 日循环
```text
只做 VS0.3 V3-A（57 计划 §8）。DayClock + DayStarted/DayEnded + 日切钩子。
禁止：Observe、Site、Cultivate 扩展、地图、战斗、NPC AI、改 Freeze、扩 Demo。
完成后停止等待确认。
```

### Task V3-B — Observe＋Site
```text
只做 V3-B：ObserveAction + OpportunitySite/KnownSites + Data 样本（若本阶段含 Loader）。
禁止寻路/地图坐标、进 V3-C 偷修。
```

### Task V3-C — 秘密修炼流程
```text
只做 V3-C：CultivationAttemptGate + PlayerInput→既有 CultivateAction；按确认加入 PersonalConcealmentRisk。
禁止重做功法/突破验收门禁、主管 AI。
```

### Task V3-D — 日终 Quota
```text
只做 V3-D：DayEnded 消费 Deviation→QuotaConsequenceApplied + 薄标记 + 日切重置。
禁止主管 Boss、完整关系、训斥剧情。
```

### Task V3-E — 整合
```text
只做非固定剧本的命令序列整合测（A–D）。禁止章节导演与产品 UI。
```

---

## 10. 验收标准

- [ ] A：日界与 Day／Hour／Tick 派生可测  
- [ ] B：Observe 经 Order；Site 可发现；无坐标／地图  
- [ ] C：无 Site 不可偷修；有 Site 走既有 Cultivate 且 Progress 增  
- [ ] C：（若批准）PersonalConcealmentRisk 随偷修变化  
- [ ] D：日终 Quota 后果事件／标记可断言（非仅 VS0.2 记账）  
- [ ] 无固定剧情导演；Override 仍优先于 Schedule  
- [ ] 无战斗／地图／寻路／NPC 高级 AI／主管 Boss／完整关系／Freeze／Demo 污染  
- [ ] EditMode 整合测 PASS  

---

## 11. 风险点

| 风险 | 缓解 |
|---|---|
| 把「第一天闭环」写成章节脚本 | 整合测只用 RTS 命令序列；Core 无「必须午时」分支 |
| Site 滑向地图 | 禁止坐标字段进入定义 |
| Narrative 抢写流程 | 只允 Content 文案 Key；导演归后续切片 |
| Exposure 命名分叉 | 组件用 `PersonalConcealmentRisk`；UI 可映射 ExposureRisk |
| Quota 后果膨胀成主管系统 | 只事件＋一两个标记字段 |

---

## 12. 设计确认清单（请批复）

1. **范围 A–D** 是否批准为 VS0.3 唯一编码范围？  
2. **PersonalConcealmentRisk：** 随包 C 进入，还是推迟？  
   - 我建议 **进入**，否则秘密修炼缺少独立代价轴（仅有 Quota）。  
3. **学法：** 开局／日始自动青云诀，还是玩家第一次偷修前手动 Learn？  
   - 我建议 **自动学**，避免把 Learn 做成伪剧情步骤。  
4. **Narrative：** 是否同意本切片仅 Content 短文案／NameKey，不写第一章导演？  
5. 批准后是否按 V3-A→E 串行、每阶段停等（仍不在确认前编码）？

**确认前禁止编码。**
