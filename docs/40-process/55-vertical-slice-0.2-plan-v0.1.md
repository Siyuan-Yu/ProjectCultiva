# Vertical Slice 0.2 Plan v0.1

> 状态：**规划草案（待人工确认）** | 最后更新：2026-08-01  
> 类型：垂直切片实施计划｜**只规划，不编码**  
> 前置：[VS 0.1 验收报告](54-vertical-slice-0.1-acceptance-report.md) **已通过**；Core M1／Data Pipeline／Cultivation Slice 0.1 可用  
> 依据：`2G`、`2F`、`21`、`35`、`33` v0.2、ADR-0018／0011／0012  
> **不修改 Core Freeze 正文**；不设计完整战斗／NPC AI／地图系统。

## 0. 切片目标

做出**第一个玩家可感的「杂役弟子第一天」闭环**（EditMode + 可选薄 Unity Host）：

```text
开局三杂役
  → 日程驱动白天劳役 Order
  → 玩家可 Override 下令（工作／停手／偷修）
  → 夜间或空档接入既有 Cultivate／学法闭环
  → 日终结算（配额完成度 + 可选暴露读数）
  → Snapshot 可续同一天或进入次日 Tick
```

**成功判据（产品感，非完整第一章）：**  
玩家能理解「白天不自由、晚上才有缝隙偷修」，并在同一套 Order／Action 上完成至少一次劳役与一次修炼切换。

**非目标：** 完整第一章 40～60 分钟叙事、夺府、战斗、地图探索、主管追捕 AI。

---

## 1. 玩家输入如何进入 Order／Action

### 1.1 原则（对齐 `35`）

- 玩家与日程／脚本的区别**只在 Order 来源与优先级**，执行仍走同一 `IOrderTranslator` → `IAction` → `SimulationLoop`。  
- Unity／调试壳**不得**直接改 Entity 组件绕过 Order。

### 1.2 建议适配层（薄）

| 层 | 职责 |
|---|---|
| `PlayerCommandPort`（Core 侧接口或纯 DTO 入站） | 接收「对 EntityId 下某类命令」 |
| `PlayerOrderFactory` | 把命令变成 `Order`（`OrderSource.Player`，高优先级） |
| `XianXia.Unity` 或 EditMode 驱动器 | 按钮／快捷键／测试 API → Port；读 Snapshot／Event 做只读 HUD |

### 1.3 VS0.2 允许的玩家命令（最小集）

| 命令 | 生成 Order／效果 |
|---|---|
| 指定劳役工作 | `LaborOrder` → `LaborAction`（见 §2／§5） |
| 停止／待机 | `WaitOrder` 或 Cancel ActiveAction（规则见 §3） |
| 学习功法 | 调用既有 `CultivationService.LearnManual`（可包成 Order 或同步服务调用；**建议**学法保持服务调用、修炼用 Order） |
| 开始偷修 | 既有 `CultivateOrder` → `CultivateAction` |
| （可选）使用敛息草 | 仅当暴露进本切片时；减 `PersonalConcealmentRisk` 的配置消耗 |

### 1.4 明确不做

- 正式 UGUI 产品壳、镜头 RTS 框选（可借用调试按钮）  
- 改 Demo Runtime 玩法代码当正式输入层（Demo 继续冻结）  
- 移动／寻路类 Order（无地图）

---

## 2. Schedule（日程）最小模型

### 2.1 形状

不实现「玩家改时间表权限」（`21`：前期只能查看）。VS0.2 只要：

```text
ScheduleDefinition（Content JSON，可选硬编码测试表）
  → 按 WorldTick 映射到 DayPhase
  → ScheduleDriver 在相位切换或每 Tick 评估
  → 若实体无更高优先级意图 → 入队 Schedule 源 Order
```

| 概念 | VS0.2 最小字段 |
|---|---|
| `DayPhase` | `Sleep`／`Work`／`Meal`／`Free`／`Curfew`（可再砍到 Work／Free／Sleep 三态） |
| `ScheduleBlock` | `startTickInDay`、`endTickInDay`、`phase`、`defaultOrderType`（Labor／Wait） |
| `ScheduleBinding` | `EntityId` 或角色 Tag（`labor`）→ `ScheduleDefinitionId` |
| `ScheduleDriver` | 读 `WorldTick`（1 日＝96 Tick，见 `31`／`21`）取当日相位 |

### 2.2 与 Action 的关系

- 日程**不**直接改属性；只产生／替换 **低优先级** Order。  
- `LaborAction`：消耗 ActionClock；累加「今日劳役进度」计数（配额用，见 §5）；完成发 `DomainEvent`（如 `LaborProgressed`／`DailyQuotaUpdated`）。  
- Work 相位默认：`LaborOrder`；Free／Sleep：默认 `Wait` 或空闲（不强制修炼）。

### 2.3 配置位置

- 建议：`Content/BaseGame/Data/schedules.json`（严格字段，走 Loader）  
- 或 VS0.2 第一刀用 Core 测试夹具表，第二刀再进 Content（实施阶段拆分见 §9）。

---

## 3. Player Override 机制

### 3.1 优先级（对齐 `35` §5，VS0.2 落地子集）

| 优先级（高→低） | 来源 |
|---|---|
| 1 | 玩家紧急／普通玩家 Order |
| 2 | （本切片不做）生存／战斗反应 |
| 3 | 强制社会义务（日终未交配额的惩罚脚本——本切片可砍） |
| 4 | Schedule 默认 Order |
| 5 | 待机 |

### 3.2 Override 规则（建议冻结进实现）

1. 玩家下单时：插入／替换该实体 `OrderQueue` 前端；`OrderSource.Player`。  
2. 若当前 `ActiveAction` 来自 Schedule（Labor／Wait）且新玩家 Order 的 `CanStart` 成功 → **中断**当前 Action（`Interrupted`），再启动玩家 Action。  
3. 若当前 Action 为玩家发起的 `Cultivate`，日程相位切换**默认不打断**（避免偷修刚开始就被日程踢掉）；改为：相位切换仅在「无 ActiveAction 或 Active 为 Schedule 源」时灌入新 Schedule Order。  
4. Override **不**修改 ScheduleDefinition 本身（玩家仍无改表权）。  
5. 失败必须 `Result`／`OrderRejected` 事件，UI／测试可断言原因。

### 3.3 明确不做

- 多并行动作槽  
- 完整中断损失矩阵（资源浪费百分比等）  
- AI 与玩家抢优先级的复杂仲裁

---

## 4. 三个杂役角色初始化

复用 VS0.1 Bootstrap，不新开「角色系统」：

| 角色 | DefinitionId（已有样本方向） | 控制 |
|---|---|---|
| 劳役甲（主角位） | `base:character_protagonist`（或现用 id） | DirectControl + Focus 候选 |
| 劳役乙 | `base:character_companion_a` | DirectControl |
| 劳役丙 | `base:character_companion_b` | DirectControl |

开局步骤（逻辑）：

1. 加载 BaseGame ContentPackage。  
2. `ContentGameStart`／`GameStartBootstrap` 生成三实体；FactionMembership／Role **数据字段**按 Freeze：压迫宗门＋杂役／劳役（若尚未写入组件，VS0.2 用 Tag／InitData 占位，**不**做完整势力领导）。  
3. 绑定同一 `ScheduleDefinition`（劳役表）。  
4. 预学或可学「青云诀／基础吐纳」之一（沿用 Cultivation Slice 数据）。  
5. 当日配额计数器归零（见 §5）。

**不做：** 半固定背景创建 UI、灵根抽卡、改名流程产品化。

---

## 5. 第一天事件流程

以「一个游戏日＝96 Tick」为骨架（可配置缩短测试日）。叙事用 **DomainEvent＋只读日志**，不做过场动画系统。

| 阶段 | Tick 带（示意） | 系统行为 | 玩家可感目标 |
|---|---|---|---|
| 日始 | 0 | `DayStarted`；发布今日配额（如木材折算为 Labor 点数 20） | 知道今天要交差 |
| 上午工 | Work | Schedule → Labor；进度计数 | 三人可分工或单控 |
| 午／工 | Work | 同上 | — |
| 薄暮／自由 | Free | Schedule 停止灌 Labor；允许玩家 Cultivate／Wait | 第一次明确「缝隙」 |
| 宵禁／夜 | Curfew／Sleep 或 Free-Night | 默认可偷修窗口（配置） | 偷修主窗口 |
| 日终 | 日界 | `DayEnded`：配额完成？；暴露快照；清空或滚动日计数 | 闭环反馈 |

**最小配额：** 不接真实物资库存亦可——`LaborAction` 只加 `DailyLaborPoints`；日终 `points >= quota` 则 `DailyQuotaMet`，否则 `DailyQuotaFailed`（**惩罚可空实现**，只发事件）。

**脚本事件（可选 1～2 条）：** 「主管巡视提醒」纯文本／事件，无 AI 寻路。

---

## 6. 偷修如何接入现有 Cultivation Slice

### 6.1 已有资产（VS0.1）

- `LearnManual`／`CultivateAction`／Progress／凡人→炼气 Breakthrough／Snapshot  
- Content：`CultivationDefinition`＋Mapper → `CultivationManualSpec`

### 6.2 VS0.2 接入点

```text
玩家在 Free／夜窗 Override
  → CultivateOrder（既有）
  → CultivateAction 推进（既有）
  → （新）若启用暴露：按相位累加 PersonalConcealmentRisk
  → 突破仍走既有 CultivationService（第一天不强制突破，允许只涨 Progress）
```

| 规则 | 建议 |
|---|---|
| 学法时机 | 日始自动学「青云诀」或 Free 时段玩家点一次 Learn（测两路径择一作默认） |
| 与 Labor 互斥 | 单 `ActiveAction` 已保证；Override 规则见 §3 |
| 工时偷修 | **允许**但若启用暴露则高风险（见 §7）；体现压迫感 |
| 第一天必破境界？ | **不要求**；验收以「完成 Labor 进度 + 至少 N Tick 修炼」即可 |

### 6.3 明确不做

- 多境界、天劫、洞府、丹药  
- 改 Cultivation 公式／把 Progress 重定义为另一套资源（若要改，先 ADR，见 VS0.1 报告观察项）

---

## 7. 暴露风险是否进入本阶段

### 7.1 判断

第一天闭环的**情绪核心**含「怕被发现」，但完整三层隐匿（`2F`／`33`：PersonalConcealmentRisk／Suspicion／FactionHostility）+ 主管 AI 过重。

### 7.2 建议（推荐选项）

**建议：进入，但只做「个人暴露条」薄实现（V2 暴露薄层）。**

| 做 | 不做 |
|---|---|
| `PersonalConcealmentRisk` 数值（组件或 World 侧账户） | Suspicion／FactionHostility 完整规则 |
| Cultivate 时按相位加风险（Work 高／Free 夜低） | 主管巡逻发现、追捕、没收演出 |
| 可选：消耗「敛息草」ItemDefinition 减风险（只改数值） | 搜身、藏匿容量、举报链 |
| DomainEvent：`ConcealmentChanged` | 暴露导致 GameOver |

**备选：** 本阶段完全不做暴露，只做日程＋劳役＋偷修互斥——切片更短，但「第一天」张力偏弱。

**请人工二选一；默认按「薄暴露」写入实施任务。**

---

## 8. 本阶段明确不做

- 修改 `33` Freeze／已采纳 ADR 正文（增量观察另开 ADR）  
- 完整战斗、伤害、站位、技能栏  
- 完整 NPC AI／效用决策／主管追捕  
- 地图：格子、寻路、LocalMap 加载、Region 旅行  
- 聚落经营、资源物流真库存（可用虚拟 Labor 点）  
- Mods/、Excel 运行时、产品级 UI、Demo 扩玩法  
- 玩家修改日程表权限  
- 完整第一章叙事与炼气后隐藏线  

---

## 9. 实施阶段拆分

| 阶段 | 目标 | 主要交付 | 门禁 |
|---|---|---|---|
| **V2-A** | 日程数据＋相位 | `DayPhase`／`ScheduleDefinition`（Content 或测试表）；Tick→Phase | 单测相位边界 |
| **V2-B** | Labor 最小 Action | `LaborOrder`／`LaborAction`；日配额计数；事件 | EditMode：Labor 满配额 |
| **V2-C** | ScheduleDriver | 按相位灌 Schedule Order；不覆盖玩家 Active Cultivate | 无玩家时自动 Labor；有 Cultivate 不打断 |
| **V2-D** | Player Override 端口 | `PlayerCommandPort`＋优先级／中断规则；调试下达 | 玩家可打断 Labor 改 Cultivate |
| **V2-E** | 第一天编排 | DayStarted／DayEnded；三杂役绑定；默认学法；整合测「一天」 | 整合测 PASS |
| **V2-F** | 暴露薄层（若批准 §7） | Risk 累加＋可选敛息草；只读断言 | 工时修炼风险＞夜修 |
| **V2-G** | （可选）薄 Host | 调试按钮：选角色／Labor／Cultivate／Tick／日终 | 非阻塞；EditMode 仍为完成标准 |

每阶段：编译 + EditMode + 文件列表 + **等确认**；Demo／ProjectSettings／Packages／Freeze 禁擅改。

---

## 10. Cursor 开发任务清单（复制即用）

### Task V2-A — Schedule 相位模型
```text
角色：Development AI。遵守 AGENTS.md 与 52 协作规范。
只做 Vertical Slice 0.2 阶段 V2-A（见 docs/40-process/55-vertical-slice-0.2-plan-v0.1.md）。
实现 DayPhase + 按 WorldTick 映射；单测相位边界。
禁止：地图、战斗、NPC AI、改 Freeze、扩 Demo、进 V2-B。
完成后：测试 + 文件列表 + 停止等待确认。
```

### Task V2-B — Labor Action
```text
只做 V2-B：LaborOrder/LaborAction + DailyLaborPoints/Quota + DomainEvent。
不接真实库存与地图工位。禁止进 V2-C。
```

### Task V2-C — ScheduleDriver
```text
只做 V2-C：相位驱动入队 Schedule 源 Order；遵守「不打断玩家 Cultivate」规则。
禁止 PlayerPort 产品化以外的输入层。
```

### Task V2-D — Player Override
```text
只做 V2-D：PlayerCommandPort + 优先级/中断 Labor。
可对 EditMode 测试直接调 Port。禁止正式 UI 工程。
```

### Task V2-E — Day-1 编排整合
```text
只做 V2-E：三杂役绑定日程、日始/日终、默认学法、一天整合测。
偷修走既有 Cultivate。禁止完整暴露三层（除非已批准 V2-F）。
```

### Task V2-F — 暴露薄层（仅当批准 §7 推荐项）
```text
只做 PersonalConcealmentRisk 累加与可选敛息草数值。
禁止 Suspicion/FactionHostility/主管 AI/GameOver。
```

### Task V2-G — 可选 Host 烟测
```text
可选：XianXia.Unity 调试按钮驱动 Port + 只读状态。
不阻塞 VS0.2 逻辑验收；禁止改 Demo Runtime。
```

---

## 11. 待人工确认（编码前）

1. **暴露风险：** 采用 §7.2「薄暴露（推荐）」还是本阶段完全不做？  
2. **学法默认：** 日始自动学会青云诀，还是玩家第一次 Free 时段手动 Learn？  
   - 建议：日始自动学，减少第一天操作步骤。  
3. **Labor 验收：** 虚拟点数即可，还是必须挂钩 `item_rough_wood` 库存？  
   - 建议：虚拟点数；物品库存留到资源切片。  
4. **日程数据：** 第一刀 Content JSON，还是先测试夹具再迁 Content？  
   - 建议：先夹具（V2-A）→ V2-E 前迁入 JSON。

---

## 12. 与 VS0.1 的关系

| VS0.1 已有 | VS0.2 使用方式 |
|---|---|
| Bootstrap 三角色 | 开局直接复用 |
| Cultivate／Breakthrough | 偷修窗口调用 |
| Content 功法／角色 | 不改 Freeze；可补 schedule／quota 字段 |
| 无输入／无日程 | 本切片补齐最小可玩缺口 |

---

## 13. 完成标准（实现并验收后）

- [ ] 三杂役开局并绑定劳役日程  
- [ ] 无玩家输入时白天自动 Labor，配额可完成  
- [ ] 玩家可 Override：打断劳役并 Cultivate  
- [ ] 第一天日始／日终事件可测  
- [ ] （若批准）暴露薄层在工时／夜修有差异  
- [ ] 无战斗／地图／NPC AI／Freeze 改动／Demo 污染  
- [ ] EditMode 整合测 PASS
