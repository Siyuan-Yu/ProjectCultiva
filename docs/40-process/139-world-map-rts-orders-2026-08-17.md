# 139 · 大地图 RTS 下令与部队交互（2026-08-17）

> 状态：**已实现（纯 RTS Prototype；时间纪律见 ADR-0023）**｜日期：2026-08-18；**2026-08-21 修订**；**2026-08-22 target-model 注记**  
> 相对：[138 接战弹窗计划](138-world-strategic-battle-offer-plan-2026-08-17.md)｜[140 收束](140-world-map-rts-battle-return-rollup-2026-08-18.md)｜[ADR-0023](43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)｜[ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)｜[2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md)｜[144](144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)  
> **进出／Modal 遭遇：** 以 ADR-0023／144 为准；[143](143-localmap-worldmap-interaction-behavior-spec-2026-08-20.md) 中「回战场」等已 superseded  
> 飞书：https://my.feishu.cn/docx/RgkxdiGNSoNd11xOXoncl7QnnCg

---

## ⚠️ Target Model 注记（2026-08-22 · ADR-0024）

**以下 §0 描述的是当前 Host 已验收的 Prototype 行为（historical），继续有效。**

**正式产品目标（superseded target）：** Character **不能**直接作为大地图战略移动单位。须先编入／创建 **Army** → Army 成为 WorldMap 战略单位 → Army 移动。见 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) §4／§14。

**本轮禁止** refactor 139 对应现有代码。

## 0. Prototype 产品模型（historical · 2026-08-17；时间纪律 2026-08-21）

大地图 = **纯 RTS 宏观层**（Prototype）：
|------|------|
| **下令移动** | 选中人 → 确认 → **立刻**从当前 LocalMap Despawn → 大地图上路 |
| **改目标／打断** | 路上再点别处 → 直接改宏观目标（随时可打断） |
| **视线** | 非战斗时：全员离开后可不卸图、不挪镜头 |
| **遇敌** | 追击／主动攻击抵达 → **BattleOffer**；立即 **冻结 WorldTick**（ADR-0023） |
| **手动战** | Modal Encounter；锁图；禁战略令；Tick 冻结至 Resolve |
| **打完** | FieldCleared → PostBattle → 结束战斗 Resolve；**不**默认挂起 InEncounter 回战场 |
| **进场景** | 非 Modal 时：节点上有我方即可进入 |
| **到站** | 非追击的最终目的地 → **到站弹窗**；追击／攻击目标不弹「是否查看」 |

**已废弃：** LocalMap「走到边缘再上路」；战斗期间战略世界继续跑；清场后多人挂起回战场。

---

## 1. 玩家操作（大地图 M）

| 操作 | 行为 |
|------|------|
| **左键头像** | 选中/多选己方角色 |
| **右键节点** | 确认弹窗 → **立刻**出发到该节点（多段路径自动续走） |
| **右键道路** | 确认弹窗 → **立刻**出发到路上指定进度 |
| **右键敌军/他方栈** | 上下文菜单（见下） |
| **Space / 倍速** | 全局时间（与 LocalMap 共用同一时钟） |

### 1.1 右键他方 ArmyStack 菜单

| 菜单项 | 行为 |
|--------|------|
| **攻击** | 登记追击 → 已重合则 BattleOffer；否则立刻上路，**每 tick 贴敌军栈当前宏观位置**，追上再弹窗。先到接战，后到可加入 |
| **查看详情** | 状态栏摘要 |

（战略外交／交谈占位已关掉，菜单不再区分敌对／非敌对。）

### 1.2 移动 vs 跟随

| 目标类型 | 到达后 |
|----------|--------|
| 节点 / 道路点 | **停下** |
| 他方栈（跟随，菜单暂无） | 保持跟随 |

---

## 2. 代码入口

| 模块 | 职责 |
|------|------|
| `WorldTravelTarget` | 节点／道路进度目标 |
| `WorldTravelPathService` | BFS 多段、改目标、队列续走 |
| `StrategicEngageRules` | 接战空间重合 |
| `StrategicPursuitService` | 攻击追击 |
| `HostWorldMapPanel` | 选队、右键、攻击 UI |
| `HostWorldTravelConfirmPrompt` | 确认 → `BeginMacroOrder` |
| `HostWorldTravelDeparture` | **仅** Hide + 宏观 `StartAgentTravel`／追击；无边缘链 |

---

## 3. 接战规则（与 138 对齐）

**会弹 BattleOffer：** 攻击已重合，或追击抵达。  
**不会弹：** 普通路过敌军、暗雷（已删）。  
先到接战、后到加入。

---

## 4. 已知限制（后续）

- [ ] 交谈 ContentEvent
- [ ] 跟随菜单加回
- [ ] （可选）边缘离场演出另开刀

---

## 5. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-08-18 | **追击贴敌＋多选近战**：攻击后每 tick 贴敌军栈；LocalMap 多选全员一起打。见 [141](141-pursuit-stick-and-multi-melee-2026-08-18.md) |
| 2026-08-18 | **清旧**：去掉大地图外交面板／交谈占位／CaptureNode／自动胜利 stub／DepartingLocalMap 分支 |
| 2026-08-18 | **清场回程**：InEncounter 路中点回原端可达；暂关节点势力染色／默认 Owner |
| 2026-08-18 | **增援到站**：手动开战只清进场者追击标记，路上保留 CombatPursuit；到后只弹「加入战斗」不弹到站查看 |
| 2026-08-17 | **打完离场**：敌清空 FieldCleared；无结算不弹图；参战者可宏观移动；中途可看图增援不可撤 |
| 2026-08-17 | **到站弹窗**：最终目的地 ArrivalNotice；遇敌仍走 BattleOffer；去查看→开大地图 |
| 2026-08-17 | **视线保留**：全员上路不卸图、不挪镜头；删 MarkDeparting／TryUnload／EnsureSpawned／Departing stub 等边缘残留 |
| 2026-08-17 | **重切纯 RTS**：删 Host 边缘离场整链；下令即 Despawn+上路；路上可随时改目标 |
| 2026-08-17 | 攻击追击对齐派遣；重做遇敌规则 |
