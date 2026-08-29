# 163 — RPG-First 架构审计与代码迁移计划（2026-08-25）

> **⚠️ 2026-08-30 · PresenceHex 相关迁移条目部分被 [ADR-0027](43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md) SUPERSEDED（改 derived，不再作为固定世界位置代理）。** 本页其余审计结论保持。

> **类型：** 文档／架构审计（**禁止改代码**）  
> **真源：** [2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) + [ADR-0026](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)  
> **基线 HEAD（审计时）：** `0a40a86`（Multi-Hex footprint）之上文档提交  

---

## 1. New Source of Truth

| 文档 | 角色 |
|------|------|
| **[2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)** | **最高优先级产品真源**：Active／Party／Policy／Background／Army 边界／连续世界／PresenceHex／Succession／**§5.8 Continuous World Movement** |
| **[ADR-0026](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)** | 架构决策记录 |
| [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) | 外交／War／Capture／真实成员等仍有效；**「Army=唯一移动载体」已 supersede** |
| [2J](../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md) | Territory／Footprint／Bandit；**PresenceHex 由 2K 补充** |
| ADR-0024 | 真实修士 + LOD；**跨点必须 Army 已部分 supersede** |
| ADR-0025 | Pure Hex 空间；由 2K 扩展为「世界本身」 |

---

## 2. Rules Superseded（摘要）

详见 2K §12。核心：

| Old | New |
|-----|-----|
| 单人跨 Hex 必须组成 Army | 普通 Character 可 World Travel；FormalArmy 只承担正式军事远征 |
| 玩家可框选／RTS 多单位即时控制（正式长期模型） | 仅 1 Active；Followers AI；多选为 Legacy Prototype |
| WorldMap 战略角色皆 Army Avatar | Party=Active 头像；Army=Leader 头像；Background 不常驻 |
| 远方 Army 可切入手动战 | 默认 Auto；Party≤1 Hex 可介入且不接管 Army |
| WorldMap／LocalMap 割裂两套空间 | HexWorld 唯一拓扑；LocalMap 近景；WorldMap 总览 |
| 个人战斗可改 Site Owner | 仅 Party／FormalArmy 可 Capture |

---

## 3. Current Code Conflicts（只读）

审计范围：`Assets/Scripts` Host／Core／World／Strategic（不改文件）。

| Module | Current Behavior | New Required Behavior | Conflict | Risk | Phase |
|--------|------------------|----------------------|----------|------|-------|
| **HostSelectionController / LocalMap 多选** | 支持多选我方单位、多目标近战（见 141／RTS 路径） | 仅 Active 接受即时输入；Followers AI | **HIGH** — 多人 RTS 控制假定 | High | 1 |
| **HostWorldMapPanel 选中 FormalArmy** | 左选右令：`MoveArmy`／`AttackStack`；路径预览绑选中 Army | Party 旅行 ≠ 必须选 Army；Army 仍可战略下令但远战默认 Auto | **HIGH** — WorldMap 操作中心是 Army | High | 1–3 |
| **ArmyService.CreateArmy / 编组** | 己方 Site 组军（已 Hex）；旅行资格历史上绑 Army | 组军仅军事；旅行不再要求 Army | **MED** — 规则已半迁移，产品语义未改完 | Med | 3 |
| **「跨点必须 Army」文档 vs Runtime** | Runtime 已允许 AtSite Presence；Host 仍以 Army 为大地图主操作 | Character／Party 独立 World Travel | **MED** — 文档与 UX 落后／错位 | Med | 2–3 |
| **WorldPresence / PartyWorldPresence** | AtSite／AtHex／InEncounter；PartyWorld 为镜头焦点 Site | 需区分 Party／Background／Army 存在态；PresenceHex | **HIGH** — 数据语义不足 | High | 2 |
| **LocalMap 进出** | EnterWorldSiteScene／退大地图；关卡感强 | 边缘跨 Hex 连续过渡 | **HIGH** — 空间模型割裂 | High | 5 |
| **BattleOffer / Manual Encounter** | 可对手动战；远方接战入口存在 Prototype 路径 | 远方 Army 默认 Auto；≤1 Hex 介入 | **MED–HIGH** | Med | 4 |
| **CaptureObjective** | Site Owner 易主；入口多为 Army／玩家在场 | Party 或 Army 才有 Capture 权；**Party 仍须 War + CaptureObjective**；Background 禁止 | **MED** — 需权限闸 | Med | 3–4 |
| **WorldMap Markers** | Army／残留／Site 为主；角色列表可跳转 | Background 不常驻头像；禁远距离附身切换 | **MED** | Med | 1–2 |
| **Snapshot v6** | FormalArmy／SiteOwner／Residual／Membership | 未来 Party／Policy／PresenceHex／Background travel | **LOW now** — schema 未破；实现时扩展 | Low→Med | 2+ |
| **HexWorld / Multi-Hex / Footprint** | OccupiesHex／Anchor；无 PresenceHex 字段 | 增加 PresenceHex 语义 | **LOW–MED** — 扩展非推翻 | Med | 2 / Editor |
| **Character AI / Schedule** | Schedule／劳动／LocalMap AI；无 FactionTerritory Policy | Character Policy V1 | **MED** — 新系统 | Med | 8 |
| **WorldGraphEditor** | Hex／Site／Footprint | PresenceHex 编辑 | **LOW** | Low | Editor w/ Phase 2 |

### 特别说明

- **不要现在删** RTS／Army Runtime；标 Legacy 并按 Phase 迁移。  
- **FormalArmy 核心 Domain**（真实成员、Hex 位置、追击、Capture 链）多数**仍兼容**，冲突主要在**控制权与移动资格语义**。

---

## 4. Migration Plan（仅规划，不实施）

### Phase 0 — Docs + Audit（本轮）

- **Goal：** 真源一致；旧规则 supersede  
- **Must Not Break：** 现有可玩 Prototype  
- **Acceptance：** 2K／ADR-0026／Glossary／Roadmap／本页齐备  

### Phase 1 — Single Active + PlayerParty 控制模型

- **Goal：** LocalMap／输入层仅 Active 即时控制；Follow=入队；Party≤6；禁远距离附身  
- **Affected：** Selection、Input、Host UI、Party 数据（新产品语义）  
- **Must Not Break：** 单人可玩弧、对话、基础战斗、Save 基本可用  
- **Acceptance：** 无法框选多单位下令；只能在 Party 内切换 Active  

### Phase 2 — Background Presence / Simulation

- **Goal：** 非 Party 角色世界存在；WorldMap 不常驻头像；PresenceHex；低频率模拟骨架  
- **Affected：** WorldPresence、Snapshot、Site 进入时世界坐标  
- **Must Not Break：** Party／Army 现有 Hex 位置  
- **Acceptance：** Background 可数据层定位；无 Capture 权断言  
- **2026-08-25 进度：** **Phase 2A 已封板**（PresenceHex Content／Runtime／Editor／Query／最小 Snapshot；不含 Travel／Combat；人工验收通过）。详见 [roadmap](41-roadmap.md)／[devlog](42-devlog.md)。  
- **2026-08-25 Phase 2B：** **已封板** — PlayerParty World Travel MVP（非 Fake Army；30×15 测试世界；Wilderness Fallback；Materialize 闭环）。  
- **2026-08-26 Phase 2C（进行中／竖切入仓）：** Continuous World Movement — 契约见 [2K §5.8](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)／ADR-0026 #12。已落地：Continuous WorldPosition；Edge Transition（Site／Wilderness）；Ping-Pong Guard；**Canonical Exit Trigger Geometry**（`ExitTriggerDepth`；Geometry∥Availability；Detection＝Presentation）；实现索引 [164](164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md)。仍待：全 Phase 2C 最终人工封板。Background／FormalArmy continuous Deferred。  

### Phase 3 — FormalArmy 职责迁移

- **Goal：** Army=军事；旅行不强制 Army；组／解散仍在己方 Site  
- **Affected：** Army UI、WorldMap 命令入口、文档对齐 Runtime  
- **Must Not Break：** Army vs Army／Site Capture／War  
- **Acceptance：** 无 Army 的 Party 可世界移动；隔空组军仍失败  
- **2026-08-28 封板：Accepted / Sealed**  
  - 实现索引：[166](166-phase-3-formal-army-continuous-world-2026-08-27.md) + [167](167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md)  
  - 核心目标已完成：FormalArmy 军事层收敛；PlayerParty 独立旅行 Authority；Continuous WorldPosition／Travel／Presence／Save-Load／Authority 边界  
  - 验证：LevelTester 持续使用 + Phase 4 开发／人工验收实际依赖；**2026-08-28 用户正式确认封板**  
  - 原计划 F11 TEST 1–10／167 验收 1–12 逐条签字表未单独归档 → 不再阻塞  
  - **Backlog / Deferred：** FormalArmy WorldMap Marker 连续表现、Autonomous AI Order、更复杂 Army AI／主动战争／Army Capacity 等  
  - **Phase 4：** 已封板；**Phase 5：** Not Started

### Phase 4 — Manual Battle Permission

- **Goal：** 远方 Army 默认 Auto；邻格介入且不接管 Army  
- **Affected：** BattleOffer、Encounter 入口、Host 打断  
- **Must Not Break：** ADR-0023 WorldTick 冻结、战损回写  
- **Acceptance：** 远距离无法切入手动；邻格可介入仅控 Active  
- **2026-08-28 封板：Accepted / Sealed**  
  - 正式真源：[171](171-phase-4-battle-authority-2026-08-28.md) §1  
  - Battle Trigger＝共边相邻（禁 WorldPosition 距离）；BattleArea＝Defender Hex（多 Hex Site＝全 Footprint）；SupportArea＝BattleArea∪共边邻格  
  - Participants 按 SupportArea + 交战方；Manual 仅 Player 实际入场  
  - Hex topology Authority（Odd-R↔axial；含 CollectHexLine）已修（Hex 真源修复，非 Phase 4 特补丁）  
  - LevelTester 人工验收通过；EditMode 用例已入仓  
  - **Deferred / Future Regression：** 敌军主动攻击 Retreat 人工验收；AI vs AI 主动接战人工验收（缺战略 AI）  
  - **Deferred（原范围）：** Legacy 战斗入口删除、PlayerParty 作 Initiator  
  - **Phase 5：** Not Started（本轮不启动）

### Phase 5 — Continuous LocalMap ↔ Hex

- **Goal：** 边缘过渡逻辑连续（Fade+邻接 Hex；**不** snap 邻格中心）；与 Phase 2C 契约对齐  
- **Affected：** Map load、Travel、Presentation  
- **Must Not Break：** Site LocalMap 玩法、Encounter 图  
- **Acceptance：** 走出边缘进入邻接世界语义成立；Continuous WorldPosition 连续  
- **Note：** 产品契约已在 Phase 2C／2K §5.8 锁定；本 Phase 侧重实现落地与表现。  

### Phase 6 — WorldMap Auto Travel

- **Goal：** Party 选 **Hex／WorldSite**、`AutoTravel`、Continuous 移动、时间流逝；关 WorldMap＝Cancel＋Expand  
- **Must Not Break：** 手动展开 LocalMap、中断保位  
- **Acceptance：** 非传送、非 PreciseWorldDestination 的路径旅行可演示  
- **Note：** 与 Phase 2C 目标重叠时以 2K §5.8 为准合并验收。  

### Phase 7 — Wilderness LocalMap

- **Goal：** 普通 Terrain Hex 可展开近景  
- **Must Not Break：** Fixed Site LocalMap  
- **Acceptance：** 至少 1～2 种 Terrain stub  

### Phase 8 — Character Policy V1

- **Goal：** LeaveTerritory／修炼优先／征召  
- **Must Not Break：** 玩家直接控制 Active  
- **Acceptance：** OFF 时 AI 不主动出界  

### Future

Flight、Sect Mission Board、Advanced AI、Territory Tint、Dynamic Bandit 等（2K §13）。

---

## 5. Open Questions（产品级）

### 已关闭（2026-08-25）

| 原问题 | 决议 | 真源 |
|--------|------|------|
| Succession「合格角色」条件 | **V1 已拍板**：同 Faction、Alive、可正常行动、未 Captured、不在出征 FormalArmy、位于玩家控制 WorldSite；**无境界门槛** | [2K §4](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) |
| PlayerParty 攻占据点是否走 War + CaptureObjective | **是**。PlayerParty 特权仅为不必转 FormalArmy + 可 LocalMap 手动战；**不能**跳过 War／CaptureObjective／正式 Capture | [2K §9](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) |

### 仍 Deferred

- **Background Simulation Battle 通知／日志 UX 粒度** — 后台战斗与战损回写为架构真源；具体通知玩家方式、暂停、弹窗属后续 UX；**不阻塞** Background Simulation 架构。

```text
No hard product-level blockers for starting Phase 1 implementation planning.
Architecture is ready for implementation planning.
```

---

## 6. Docs Touched（本轮）

见提交说明；核心新增 2K、ADR-0026、本文；更新 Glossary／Roadmap／Reading Guide／2A／ADR-0024／2J／Overview／AGENTS／ADR 索引／Devlog。
