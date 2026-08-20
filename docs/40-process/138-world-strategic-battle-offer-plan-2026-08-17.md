# 138 · 大地图战略层与接战弹窗计划（2026-08-17）

> 状态：**Phase 1～4 已实现；接战/移动/跟随见 [139](139-world-map-rts-orders-2026-08-17.md)**｜日期：2026-08-17  
> 相对：[113 WorldGraph 架构](113-world-graph-local-map-architecture-revision-v0.1.md)（A～D／F 已落地，E 待做）｜[129 Host 出行](129-world-graph-host-travel-scene-isolation-2026-08-16.md)｜[28 江湖关系](../20-systems/28-jianghu-relations.md)｜[26 领地经营](../20-systems/26-territory-management.md)  
> 飞书：https://my.feishu.cn/docx/FSOcd9I2oosbBWx82CXcKMkZnod

---

## 1. 一句话

在 **WorldGraph 战略层**上补齐「文明式」帮派／外交／占点，军事遭遇采用 **全战三国式接战弹窗**：战力对比 + 自动战胜率 → 玩家选 **自动结算** 或 **进 LocalMap 手动战**。

---

## 2. 背景：现在有什么、缺什么

### 2.1 已有（代码／文档）

| 能力 | 状态 |
|------|------|
| WorldGraph 30 节点 + 编辑器 | 已落地（128） |
| 玩家小队宏观 Travel／进 LocalMap | 已落地（129） |
| `WorldNode.ownerId`／`state` 字段 | SCHEMA + Runtime 有；Ch01 内容**未填** |
| 个人势力 `FactionMembership`、个人 `RelationshipLedger` | VS0.5 已落地 |
| LocalMap 近战／远程／斗技／纱衣 | 125～137 已落地 |
| 路上遭遇 LocalMap（113 阶段 E） | **未做** |

### 2.2 缺什么（本轮要设计的）

| 能力 | 说明 |
|------|------|
| 帮派 ↔ 帮派关系 | 友好／中立／敌对／战争；非仅 NPC `hostile` 标签 |
| 节点归属可视化 | 大地图颜色／tooltip 显示 `ownerId` |
| 战略军事 Actor | 除玩家小队外，敌对帮派可派 **ArmyStack** 在 Node／Route 上 |
| **接战弹窗 BattleOffer** | 相遇时暂停战略层，展示战力与胜率，选自动／手动 |
| 自动战 AutoResolve | Core 当场结算，不刷 LocalMap |
| 手动战 | 加载遭遇 LocalMap 或节点战场，复用现有战斗 |

路线图 41／62 仍标 **「大地图战争」未做**；本文是该项的 **第一版设计真源**。

---

## 3. 设计目标（玩家体验）

```text
大地图（WorldGraph）
  → 看见邻邦占点、关系好坏
  → 旅行／派兵途中与敌对栈相遇
  → 【接战弹窗】双方战力 + 自动战胜率
       ├─ 自动战斗 → 当场胜负／伤亡 → 回到 Route／Node
       └─ 手动战斗 → 进 LocalMap 打 → 结束卸图 → 回到战略层
```

**参考：** 全战三国「是否接战、战力条、自动／手动」；**不是**大地图实时 RTS 微操，也**不是**纯文明式全抽象战。

**已确认规则：**

- 选 **自动战斗** 后认结果，**不提供**「自动输了再进手动」反悔（第一刀）。
- 第一章战略演示用 **Ch01 小子图 4～6 节点 + 2～3 帮派**，不一次填满 30 节点。
- 重要占点／守城战优先 **手动 LocalMap**；可见 ArmyStack 接战可自动或手动。
- **不做 Route danger 随机暗雷**；路上不会凭空弹「路遇险情」。

### 3.1 统一世界时间纪律（已确认 · **2026-08-21 ADR-0023 修订**）

**原则：全世界只有一套时钟**（`SimulationWorld.Tick`）；LocalMap 与 WorldGraph **不各跑各的**。  
战略接战另加：**冻结推进**，不是第二套时钟。见 [ADR-0023](43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)／[144](144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)。

| 场景 | 时间行为 |
|------|----------|
| **打开大地图（M）瞬间** | **自动暂停** — 含当前 LocalMap 内 Action／Schedule／Travel |
| **大地图内取消暂停（Space）** | **全局继续**（若未处于战略战斗冻结） |
| **倍速 `[` `]`** | 与 LocalMap 同一套；作用于整个世界（冻结期间不推进 Tick） |
| **关闭大地图** | 不单独改暂停态 |
| **BattleOffer 产生** | **立即冻结 WorldTick**；强制战术／UI 暂停展示 Offer |
| **选 AutoResolve** | 瞬时结算，**不**额外推进 Tick；Resolve 后恢复开战前 pause／倍速 |
| **选 Manual** | 进入 Modal Encounter；**整场＋PostBattle 期间 WorldTick 保持冻结**；战术 RTS 可用表现时钟 |
| **Encounter Resolve（结束战斗）** | 清理遭遇；**恢复**开战前 pause／time scale |
| **ContentEvent 等 CIF** | 同级打断；均不得在战斗冻结期间偷跑战略 Tick |

```text
遇 BattleOffer → 冻结 WorldTick
  ├─ 自动 → 结算 → Resolve → 恢复 pause／倍速
  └─ 手动 → Modal 遭遇图（锁图、禁战略令）
        → 清场 → PostBattle（仍冻结）
        → 结束战斗 → Resolve → 恢复
```

**已废弃（相对本文件旧稿）：** 「选完自动/手动后立刻恢复打开弹窗前的暂停，手动战期间战略世界继续走」。

**实现要点（Host／Core）：**

- Core：`StrategicClockFreeze`（Offer／Manual／PostBattle）  
- Host auto-tick／StepTick：冻结时不推进 `WorldTick`  
- 战术 `IsPaused` 可在 Manual 内切换，**不等于**解除战略冻结  
- 禁止「战略层冻结、LocalMap Schedule／Travel 偷偷走」  

**AI 动兵：** 敌对 ArmyStack 仍消费同一世界 tick；冻结期间不推进。

---

## 4. 接战弹窗 BattleOffer（核心 UX）

### 4.1 触发条件（第一刀 · 已与 139 对齐）

| 场景 | 触发 |
|------|------|
| 玩家大地图 **右键攻击** 敌军栈 | 已重合则立刻弹窗；否则追击，抵达后再弹 |
| 追击中抵达敌军位置（同节点／同路进度近） | 弹窗 |
| ~~玩家小队 Travel 中与敌对栈同 Route~~ | **不**自动弹窗（过路自由） |
| 敌对栈主动攻击玩家 | 第一刀未做 |

第一刀范围：**玩家小队 ↔ 敌对帮派栈**。帮派互打 AI 后补。

### 4.2 弹窗内容

| 区块 | 内容 |
|------|------|
| 标题 | 遭遇地点（Route 名／Node 名） |
| 己方 | 头像／人数／**战力值**（见 §5） |
| 敌方 | 帮派名／栈规模／战力值 |
| 对比条 | 双方战力条形对比 |
| 自动战胜率 | 预估胜率（如「约 62%」） |
| 按钮 | **自动战斗**｜**手动战斗**｜**撤退**（可选，第一刀可仅前两个） |

战略层 **强制暂停**（类似 ContentEvent 打断，见 §3.1）；选完并结算后恢复 Travel／AtNode 与全局暂停态。

### 4.3 手动战斗落地

| 遭遇类型 | LocalMap 来源 |
|----------|----------------|
| **路上** | 临时 Encounter LocalMap（山谷／官道模板；113 阶段 E） |
| **节点** | 节点 `localMapId`；无则 `base:map_world_node_stub` 战场变体 |

流程：

1. 保存 `PartyWorldPresence`／Route 进度／遭遇上下文  
2. `EnterEncounterLocalMap` → 按现有 Host 战斗刷敌  
3. 战斗结束 → `ResolveEncounter` → 卸图 → 恢复战略状态  

### 4.4 自动战斗落地

- Core：`BattleAutoResolveService`（名待定）  
- 输入：双方 `CombatPowerSnapshot` + 随机种子（可复现）  
- 输出：胜／负／平局、伤亡比例、是否俘虏（第一刀可只做胜败 + 栈消灭）  
- 发布 `DomainEvent`：`StrategicBattleResolved`（类型待定）  
- **不**加载 LocalMap  

---

## 5. 战力与胜率（第一版公式）

**原则：** 先手操可读，再迭代接体魄／功法／熟练度。

### 5.1 战力 CombatPower（单栈）

```text
CombatPower =
  Σ 成员 ( 境界档权重 × 人数修正 )
  × 斗技／纱衣简单修正（有则 ×1.1～1.2）
  × 地形／防守修正（节点战可选）
```

| 境界档（示例） | 权重 |
|----------------|------|
| 感应／凡人 | 1 |
| 炼气 | 3 |
| 筑基 | 10 |
| 金丹+ | 30（后续扩） |

### 5.2 自动战胜率

```text
winRate = clamp( 0.05, 0.95, 己方战力 / (己方战力 + 敌方战力) × 调参系数 )
```

- 展示为「约 xx%」；结算用同一公式 + 随机 roll  
- 详情配置可进 `Content/BaseGame/Data/` 新表或 JSON 常量（实现阶段定）

---

## 6. 战略层模块拆分

```text
WorldGraphBoard（已有）
  ├── WorldNode.ownerId / state     ← Phase 1 占点
  ├── FactionBoard（新）            ← 帮派定义 + 运行时归属
  ├── FactionDiplomacyBoard（新）   ← 帮派 ↔ 帮派 stance
  ├── ArmyStackBoard（新）          ← 战略军事单位
  └── PartyWorldPresence（已有）    ← 玩家小队

BattleOfferService（新）
  ├── DetectEncounter               ← Route／Node 相遇检测
  ├── BuildOfferSnapshot            ← 战力 + 胜率
  ├── ResolveAuto                   ← 自动战
  └── BeginManualEncounter          ← 进 LocalMap

HostWorldMapPanel（扩）
  ├── 节点归属色 / tooltip
  ├── 栈图标（ArmyStack）
  └── HostBattleOfferPanel（新）    ← 接战弹窗
```

**与现有系统桥接：**

| 已有 | 用法 |
|------|------|
| `RelationshipLedger` | 个人外交事件 → 可影响 `FactionDiplomacy`（28） |
| `ContentEvent` / Quest | 战争借口、使者、占点任务 |
| `MeleeCombatService` 等 | 仅 **手动战** LocalMap 内 |
| `WorldTravelService` | Travel tick 中插入 **ArmyStack 接战**检测（非 danger roll） |

---

## 7. 落地阶段（相对 113 的 E 之后）

| 阶段 | 交付 | 验收 | Git |
|------|------|------|-----|
| ~~**0 遭遇底座 E**~~ | ~~Route `danger` roll~~ | **已废弃**（暗雷） | — |
| **0.5 统一时间** | 开 M 暂停；大地图 Space／倍速；删 `DriveTravelWhilePaused` | 开图 LocalMap 停；Space 全局走 | `feat(strategic): phase0.5 unified pause` |
| **1 占点可视化** | Ch01 节点 `ownerId` + 大地图归属色 | 荒村／邻点有色 | `feat(strategic): phase1 node ownership colors` |
| **2 帮派外交** | 四档 stance + 大地图外交区 | 宣战→ hostile | `feat(strategic): phase2 faction diplomacy` |
| **3 接战 MVP** | ArmyStack + BattleOffer + Auto／Manual | 遇栈弹窗打一场 | `feat(strategic): phase3 battle offer` |
| **4 AI 派兵** | 日界 AI 栈 + 同 Route 触发接战 | 邻帮派兵一次 | `feat(strategic): phase4 ai army stacks` |

**实现状态（2026-08-17）**：Phase 0.5～4 已落地；**Route danger 暗雷已删除**；EditMode `StrategicPhaseTests` 通过；Host：`HostStrategicInterruptPresenter`（接战弹窗）、大地图暂停／倍速／归属色／外交区／Army 图标。

**垂直切片 VS-WorldStrategic-0.1（建议验收口径）：**

> Ch01 子图（荒村—矿山—渔村—青石关），3 帮派；玩家占荒村；邻帮 hostile；旅行遇 **1 次** 接战弹窗；可 **自动** 或 **手动 LocalMap** 完成。

---

## 8. 数据与 SCHEMA（预留）

实现阶段在 `SCHEMA.md` 增补（本文先定语义）：

| type / 概念 | 用途 |
|-------------|------|
| `faction`（或扩展现有 sect） | 帮派 id、显示名、默认色 |
| `WorldNode.ownerId` | 占点归属 |
| `WorldRoute.state` | 畅通／封锁 |
| `encounterPool` + `encounterTemplate` | **预留**；接战 LocalMap 模板（由 ArmyStack 驱动，非随机池） |
| `armyStack`（运行时为主，可选 definition） | 绑 faction、成员快照、位置 |

**Snapshot：** 第一期存 `ownerId`、stance、栈位置；Encounter 中盘可后补。

---

## 9. 明确不做（第一刀）

- 大地图实时单位微操、无缝开放世界  
- 自动战输了再进手动  
- **文明式复杂度**：无科技树、无时代升级、无兵种克制矩阵、无多城产能／科研 loop  
- 全图 30 节点同时填帮派／外交（第一章只用小子图 2～3 帮派）  
- 帮派互打全自动 AI 大战（Phase 4 以后）  
- LocalMap 与 WorldGraph **双时钟**（必须 §3.1 统一 tick）  
- 改 Architecture Freeze；战略层仍走 Core + Host 适配  

---

## 10. 手操验收清单（实现后）

1. 大地图见节点归属色（至少荒村／邻点）  
2. 旅行中与敌对栈相遇 → **接战弹窗**出现，战力与胜率可读  
3. 选 **自动战斗** → 不出 LocalMap，有胜负结果，Travel 继续或终止符合规则  
4. 选 **手动战斗** → 进 Encounter LocalMap，打完卸图回大地图  
5. （Phase 2+）外交面板可见邻帮 stance； hostile 后更易触发接战  
6. 按 M 开大地图 → LocalMap 停；Space 继续 → 路上 Travel 与 LocalMap **同时**走；倍速全局一致  

---

## 11. 相关文档更新

| 文档 | 变更 |
|------|------|
| [113](113-world-graph-local-map-architecture-revision-v0.1.md) | §6 增补 Phase G 指向本文 |
| [62 现状](62-project-status-2026-08-01.md) | 下一步改为战略层／接战 |
| [00-overview](../00-project/00-overview.md) | 下一步摘要 |

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-17 | §3.1 统一世界时间：开大地图自动暂停、Space 全局继续、倍速一致；§9 明确无科技/克制 |
| 2026-08-17 | 初版：战略层分期、全战式接战弹窗、战力／胜率、VS-WorldStrategic-0.1 验收口径 |
