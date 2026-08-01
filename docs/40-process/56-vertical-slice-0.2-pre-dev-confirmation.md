# Vertical Slice 0.2 开工前确认报告

> 状态：**已批准编码**｜Phase A **已完成**（等确认进 B）｜日期：2026-08-01  
> 类型：开发前确认  
> 已读：`54`、`55`、`20`、`21`、`2F`、`2G`  
> 控制方式：**已冻结为 RTS**（见下文）；不以菜单式行动选择替代。

---

## 0. 结论摘要

| 项 | 结论 |
|---|---|
| VS0.1 | 已验收；缺输入桥、Schedule、日常 Action、惩罚反馈 |
| VS0.2 目标（本确认后） | 验证 **RTS PlayerOrder + NPC Schedule + Player Override + 规则惩罚** |
| 第一阶段编码范围 | 仅 Port／Factory／Schedule／Labor·Rest·Observe／最小 Override |
| ExposureRisk | **目标明确，但不进第一阶段**；为实现顺序第 4 步 |
| OpportunitySite | **概念明确，第一阶段不做**；属后续接口切片 |
| 可否开工编码 | **是**——本报告已审核批准；按 Phase A→B→C 串行，每阶段停等确认 |

相对 `55` 草案：本报告以本次补充为准，**收窄第一阶段**，并修正 Schedule 语义（默认行为 ≠ 菜单限制玩家）。

---

## A. 本阶段目标是否明确

### A.1 控制方式（已冻结 · 不重设计）

来自 `20`／`33`／本次确认，**不再讨论替代方案**：

```text
玩家直接选择角色（RTS 点选／切换）
  → 玩家直接下达命令
  → 命令进入 Order 系统（PlayerOrder）
  → 角色自动执行（ActionClock）
```

- 可同时给多名角色下令；无每日命令次数上限（`20`／`21`）。  
- **禁止**用菜单式「今日行动选择」替代 RTS 下令。  
- VS0.2 **不实现**完整 RTS UI／镜头／框选产品壳；EditMode／调试调用模拟「选中 EntityId + 下令」即可验证桥。

### A.2 六块目标清晰度

| 概念 | 是否明确 | 开工前口径 |
|---|---|---|
| **Schedule** | 明确（语义修正） | **凡人／NPC 默认行为规则**；无玩家 Order 时按表灌计划 Order。**不是**「限制玩家只能点菜单」的系统；玩家仍可随时 RTS Override |
| **PlayerInput** | 明确 | Core 侧入站 Port：`EntityId + 命令意图`；唯一允许的玩家改行为入口 |
| **Order 桥** | 明确 | `PlayerInput → PlayerOrderFactory → Order(Source=Player) → Translator → Action → Result/Event`；禁止 UI／测试产品路径直改组件 |
| **Override** | 明确 | 玩家命令优先于 Schedule（对齐 `21`／`35`）；打断／偏离计划后走 **规则惩罚**（任务影响等）；不是免费成功 |
| **ExposureRisk** | 明确 · **延后实现** | `2F`／Freeze：`PersonalConcealmentRisk` 层；VS0.2 最小 0–100。列入实现顺序第 4 步，**第一阶段不编码** |
| **OpportunitySite** | 明确 · **延后实现** | 抽象机会位点（无地图）；服务后续偷修接口。**第一阶段不编码** |

### A.3 与策划文档对齐（无冲突则采纳）

| 文档 | 对齐点 | 备注 |
|---|---|---|
| `54` | 缺口正是 Input／工作／Schedule | VS0.2 吃掉这些缺口的最小集 |
| `20` | 三人 RTS 同时下令、劳役白天任务 | 第一章完整 40～60 分钟／突破 **不在** VS0.2 |
| `21` | 优先级：玩家 > 紧急 > 时间表 > 待机；1 日 96 Tick | VS0.2 不做紧急／战斗；劳役期无命令时走 **Schedule**（优于空待机空转） |
| `2F` | 明面义务 + 暗面暴露；前期一条主风险反馈 | 完整 Suspicion／主管 AI **不做**；惩罚先用任务／规则事件，Exposure 第二刀 |
| `2G` | 杂役三人 DirectControl；压迫宗门 Membership | 开局身份已冻结；VS0.2 不实现完整第一章流程 |
| `55` | 管道与禁止项大体可用 | 第一阶段按本报告 **再砍** Move／CultivationAttempt／日终大而全／可选 Host |

---

## B. 第一阶段实现范围（仅此）

**只实现：**

| 交付 | 职责 |
|---|---|
| `PlayerInput` Port | 接收「对某 Entity 的 RTS 命令意图」 |
| `PlayerOrderFactory` | 意图 → `Order`（`OrderSource.Player`） |
| `ScheduleDefinition` | 配置化日计划块（示例：起／工／休／工／返／睡） |
| `ScheduleDriver` | 无更高优先意图时灌入 Schedule 源 Order |
| `LaborAction` | 工作：耗时 + 日任务虚拟进度 |
| `RestAction` | 休息：耗时 |
| `ObserveAction` | 观察：耗时 + 只读情报 Event（**不**做 OpportunitySite 发现链） |
| 最小 Override | 玩家 Order 优先；可中断 Schedule 源 ActiveAction；失败走 Result／Event |

**第一阶段实现顺序（编码时严格串行）：**

1. Player Input → Order Bridge（Port + Factory + 至少一种命令如 Labor／Rest 跑通）  
2. Schedule 默认行为（Definition + Driver + 无玩家时自动 Labor／Rest）  
3. Player Override 机制（优先级 + 中断 + **规则惩罚**最小：如任务进度受损／Quota 偏差事件）  
4. （**下一刀，非第一阶段内顺带**）ExposureRisk 最小验证  

**第一阶段验收（窄）：**

- EditMode：选中三角色之一「下令」仅经 Port→Order→Action。  
- 无玩家命令：按 Schedule 消耗时间并推进 Labor 点。  
- 有玩家命令：覆盖 Schedule；可测任务进度差（规则惩罚最小集）。  
- 无地图／寻路／战斗／NPC AI／完整修炼／UI 系统。

---

## C. 明确禁止（第一阶段 + VS0.2 近端）

| 禁止 | 说明 |
|---|---|
| 地图系统 | 无 LocalMap 玩法、无格子 |
| 寻路 | 无路径；不做 MoveAction 产品化（抽象移动亦延后） |
| 战斗 | 无伤害／站位／战斗 AI |
| NPC AI | 无效用决策；Schedule ≠ AI |
| 完整修炼 | 不扩功法／境界／突破验收；不接 CultivationAttempt 产品闭环 |
| UI 系统 | 无菜单式行动 UI、无产品 RTS 壳；Demo Runtime 不扩玩法 |
| 另禁 | 守卫巡逻、宗门外交、占点、城市、改 Freeze／ProjectSettings／Packages |

---

## D. 需要 ADR（或正式决策）的问题

下列问题**影响边界或不可逆命名**；建议开工前短批，或 V2-A 前并行写草案（仍不编码玩法）：

### D1. 玩家输入桥挂载点（VS0.2 阻塞级）

EditMode 测试 API vs 未来 Unity Host 的正式边界：Port 是否纯 Core、DTO 形状、是否允许 Unity 程序集依赖 Core-only 接口。  
（承接 `54`「Demo／产品壳接入点」。）

### D2. `ExposureRisk` vs `PersonalConcealmentRisk` 命名

Freeze／`2F` 正式名为 PersonalConcealmentRisk；计划用 ExposureRisk 0–100。  
需 ADR：**同一字段别名**还是展示名／内部名分离——**禁止静默分叉两套数值**。

### D3. 「规则惩罚」最小语义（VS0.2 阻塞级）

无主管 AI／Suspicion 时，Override 的惩罚真源是什么？建议冻结为：

- 日任务／配额进度受损（必做）  
- DomainEvent（QuotaMiss／ScheduleDeviated）  
- ExposureRisk 增量（第 4 步）  

是否引入「主管愤怒」占位条：**建议 VS0.2 不做**，避免与 `2F` 方案 A/B 未决纠缠。

### D4. 无玩家命令时：Schedule vs 待机（与 `21` §5 字面）

`21` 曾写「无命令默认待机」；劳役压迫与 `21` §3／优先级表更支持「无命令走 Schedule」。  
**建议 ADR 一句冻结：** 劳役／凡人身份默认 Schedule；通用待机仅在无 ScheduleBinding 时。

### D5. Order 负载形状

`54` 已观察 Order 可选字段堆叠。Labor／Rest／Observe 加入后，是否仍扩展巨型 Order，还是引入负载 DTO／分类型工厂——建议 **短 ADR**，避免三个 Action 后再大拆。

### D6. 仍挂账（来自 `54`，不阻塞 VS0.2 第一阶段）

- CultivationDefinition vs ManualDefinition 命名  
- WorldLayout 是否进 Snapshot  
- RealmStage 扩展纪律  
- Progress 语义（会话进度 vs 修为资源）  

---

## E. 相对 `55` 的范围调整（待批）

若本报告确认，则实施真源调整为：

| `55` 原述 | 本确认后 |
|---|---|
| 六条体验一次铺开（含机会／偷修接口） | VS0.2 **近端目标**改为四元组验证；机会／偷修接口后置 |
| V2-A～H 含 Move／Attempt／Host | **第一阶段**仅 B 节清单；Exposure 为顺序第 4 步单独刀 |
| Schedule「限制」语感 | 改为 **NPC／凡人默认行为**；RTS 玩家随时可 Override |
| 可选调试 Host | 不作为第一阶段交付；禁止 UI 系统 |

批准后应回写 `55` 状态为「已按 56 确认修订」或出 `55` v0.2——**本文件不改代码；回写文档等你一声。**

---

## F. 等待确认

请确认或逐条改正：

1. **RTS 控制冻结**与「禁止菜单替代」——是否照此执行？  
2. **第一阶段范围**是否严格等于 §B 清单（Observe 进第一阶段、Exposure／OpportunitySite 不进）？  
3. **规则惩罚**最小集是否采纳 §D3 建议（配额／事件；不做主管愤怒条）？  
4. **D4 Schedule 默认**是否批准（覆盖 `21`「默认待机」在劳役身份上的字面）？  
5. 确认后是否允许：**只改文档**（更新 `55`／路线图）——仍不编码？

**在你确认前：不开始 VS0.2 编码。**

---

## G. 批准记录（2026-08-01）

已批准进入编码。冻结：RTS；Schedule=默认行为可 Override；报告 B 范围；Phase A→B→C 串行每阶段停等确认。

| Phase | 内容 | 状态 |
|---|---|---|
| A | PlayerInput Port／Factory／Player Order／Labor（+Rest）链路 | **完成** `548a095`；等确认进 B |
| B | ScheduleDefinition／Driver | 未开始 |
| C | Override + Quota 偏差／Event | 未开始 |
| — | ExposureRisk／OpportunitySite／地图／UI… | 不做 |
