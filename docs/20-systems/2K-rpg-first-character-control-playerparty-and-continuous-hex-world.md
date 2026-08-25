# RPG-First：Active Character、PlayerParty、连续 Hex 世界与 FormalArmy 军事层

> 状态：**设计规则已拍板（2026-08-25）**｜优先级：P0｜最后更新：2026-08-25  
> 上级：`docs/00-project/00-overview.md`  
> 关联：`2A`、`2J`、`24`、`27`、`23`、`ADR-0020`、`ADR-0024`、`ADR-0025`、`ADR-0026`  
> 被引用：`03-glossary.md`、`04-reading-guide.md`、`41-roadmap`、`AGENTS.md`  
> **本页是玩家控制模型、PlayerParty、世界存在状态、连续 Hex 世界与 FormalArmy 职责边界的正式产品真源。**  
> **本阶段不写实现代码。** 当前 Host 的 RTS 多选、Army-required World Travel、远距离切换控制等视为 **Prototype / Legacy 待迁移**。

---

## 0. 产品本质

> **游戏本质首先是修仙 RPG，而不是 4X / Total War。**

玩家始终在扮演**具体的真实 Character**。宗门、修士、WorldSite、领土、FormalArmy、战争是角色成长以后**拥有的东西**，不取代 Character 成为玩家本体。

所有新系统设计优先判断：

> 这是否仍让玩家觉得「我正在扮演一个修仙者生活在这个世界中」？

禁止无意识强化多单位 RTS / 4X 操作作为默认长期体验。

---

## 1. ActiveControlledCharacter

正式概念：

```text
ActiveControlledCharacter
```

**任何时刻：玩家最多只能直接即时控制 1 名 Character。**

直接控制包括：LocalMap 移动、探索、对话、战斗、技能、互动、未来飞行，及其他直接 RPG 操作。

其他我方角色**不是 RTS 单位**，不接受逐步即时点选操作。

与 [ADR-0020](../40-process/43-decisions/ADR-0020-focus-vs-control-authority.md) 关系：

| 旧术语 | 与本页关系 |
|--------|------------|
| `DirectControl` | 语义对齐 **ActiveControlledCharacter** 的即时控制 |
| `FocusCharacter` | 镜头／叙事焦点；**可与 Active 短暂分离**（见 §1.1 Camera）；不得绕过 Active 切换规则 |

### 1.1 LocalMap Camera（最终规则，2026-08-25）

> **Supersede：** 此前「RTS／Click Move 默认 Camera Follow，中键可打断」作废。

| 规则 | 说明 |
|------|------|
| **仅 WASD** | Active 收到有效 **WASD Direct Movement** 时：Camera **立即 Snap** 到 Active，持续期间 **Hard Follow** |
| **松开 WASD** | Hard Follow 解除；Camera 停在当前位置，恢复自由观察 |
| **RTS／右键寻路** | Active 正常走路径；Camera **不 Snap、不 Follow、不因新 RTS 命令回 Active** |
| **中键** | 仅自由 Pan；**不**绑定／取消任何 Camera Follow 状态（RTS 已与镜头解耦） |
| **判断依据** | 玩家 WASD Direct Input；**禁止**用 `Character.IsMoving` 驱动跟随 |
| **切换 Active** | 一次性 Snap／Focus 到新 Active；之后静止／RTS 均为 Free；仅 WASD 才 Hard Follow |
| **RTS → WASD** | 取消 RTS Path；WASD 接管；Camera 立即 Snap + Hard Follow |
| **WASD → RTS** | 松开 WASD 后镜头自由；再右键寻路时 Active 走、Camera 不跟 |

仍仅 Active 可 WASD／RTS Move／Route Preview；非 Active 不可移动命令、不可 fallback 控 Active。

---

## 2. PlayerParty

正式概念：

```text
PlayerParty
= 当前玩家本人所在的少人数 RPG 冒险队
```

```text
PlayerParty
├── 1 ActiveControlledCharacter
└── 0~5 Followers
```

### 硬规则

| ID | 规则 |
|----|------|
| **PP01** | PlayerParty 人数上限 **6**（V1 固定；日后可配置，当前正式规则=6） |
| **PP02** | 永远只有 **1** Active；其余为 **AI Controlled Followers** |
| **PP03** | 同 LocalMap 对我方角色选 **Follow / 跟随** ≡ **加入 PlayerParty**；禁止平行「Follow Group」 |
| **PP04** | Follower：自动跟随 Active；跨 LocalMap；参与世界旅行；战斗中 AI 控制；**禁止**玩家 RTS 操作 |

**PlayerParty ≠ FormalArmy。** 二者都可在 HexWorld 移动、战斗、进攻 WorldSite，但组织语义不同（见 §7）。

---

## 3. Active 切换

正常情况下：

> **只能在当前 PlayerParty 成员之间切换 Active Character。**

不允许把同 LocalMap、同势力、但未加入 Party 的角色直接设为 Active。  
**禁止**远距离点击头像 → 加载远方角色 → 接管控制（废除上帝附身模型）。

正常 RPG 视角：**切换 Active 时一次性聚焦新主控**；日常探索镜头规则见 [§1.1](#11-localmap-camera最终规则2026-08-25)（仅 WASD Hard Follow；RTS 不控镜头）。

---

## 4. Succession（主控死亡 / 继承）

### 情况 A：Active 死亡，Party 有幸存者

从当前 PlayerParty 幸存者中选择新 Active；游戏继续。

### 情况 B：PlayerParty 全灭

**不要**默认 Game Over。进入特殊 **Succession / 继承控制**：

- 从**合格角色**中选择一人成为新 ActiveControlledCharacter
- 建立新的 PlayerParty
- 游戏继续

这是普通切换规则的**特殊例外**。

### Succession V1 合格角色（已拍板）

PlayerParty 全灭后，候选角色**必须同时满足**：

| 条件 | 说明 |
|------|------|
| 属于玩家当前 Faction | `Character.FactionId == PlayerFactionId` |
| **Alive** | 存活 |
| **可以正常行动** | 非 Dying／Incap 等不可主控状态 |
| **未被 Captured** | 排除被俘角色 |
| **不在正在执行任务的 FormalArmy 中** | 已编入出征 Army 者不可 Succession 接管 |
| **当前位于玩家控制的 WorldSite** | 物理位于 `OwnerFactionId == PlayerFactionId` 的 Site |

**V1 不设置境界最低要求。**

流程：

```text
PlayerParty 全灭
→ 从合格角色列表选择一人
→ 新 ActiveControlledCharacter
→ 建立新 PlayerParty
→ 游戏继续
```

---

## 5. HexWorld / WorldMap / LocalMap

### 5.1 HexWorld = 世界本身

Pure Hex **保留**。正式定义：

> **HexWorld 是整个游戏唯一的世界地理拓扑。**

不再仅理解为 FormalArmy 战略棋盘。

### 5.2 三层关系

| 概念 | 定义 |
|------|------|
| **HexWorld** | 唯一世界空间 / 世界拓扑 |
| **WorldMap** | HexWorld 的缩略观察与旅行视图 |
| **LocalMap** | 某个世界位置的 RPG 近景展开 |

禁止把 WorldMap 与 LocalMap 理解成两套互相割裂的位置空间。

### 5.3 LocalMap 逻辑连续（目标体验）

旧体验（关卡进出）：进 LocalMap → 退回 WorldMap → 战略跳点 → 再进下一张。**Superseded（目标态）**。

目标：

> Character 走出 LocalMap = 真正离开当前位置并进入相邻世界空间。

V1 允许：

```text
走到边缘 → Fade/Loading → 世界位置跨到邻接 Hex → 加载对应 LocalMap → 从对应边缘出现
```

**不要求** Unity 无缝开放世界；**要求**逻辑连续。实现 **Deferred（Roadmap Phase 5）**。

### 5.4 普通 Hex 也是世界

无 WorldSite 的 Forest / Plains / Mountain 等 Hex **本身也是世界**。WorldSite 是 POI／聚落／洞府，不是世界本身。

### 5.5 Wilderness LocalMap（Future）

普通 Hex → Wilderness LocalMap（按 Terrain / Seed 选或生成）。本轮不实现；架构**不得阻止**任意普通 Hex 展开为 Wilderness。

### 5.6 WorldMap 长期职责

1. 观察世界  
2. 查看当前位置  
3. 选择远距离目的地  
4. Auto Travel（Future）  
5. 查看 FormalArmy  
6. 查看 WorldSite  

**不是**「所有角色必须进入战略单位模式才能移动」。

### 5.7 Auto Travel / TravelMode（Future）

PlayerParty 可选目标 → 沿 Hex 路径真实逐格移动 → 世界时间流逝；途中可遭遇／停止／展开 LocalMap。  
预留 **TravelMode**（地面／未来飞行等）；**本轮不实现飞行**，但文档禁止把旅行写死为「仅 FormalArmy + Ground」。

---

## 6. Multi-Hex WorldSite 与 PresenceHex

已落地的 Multi-Hex Footprint **不推翻**（见 [2J](2J-hex-territory-worldsites-and-dynamic-bandits.md)）：

```text
1 WorldSiteId · 1 LocalMapId · 1 OwnerFactionId · 1 LocalMap
Footprint = 世界尺度占地，≠ LocalMap 数量
```

### PresenceHex（新增）

每个 Multi-Hex WorldSite 有生成／Authoring 后**固定**的 **PresenceHex**：

| 概念 | 职责 |
|------|------|
| **AnchorHex** | 名字、主图标、Presentation 中心 |
| **PresenceHex** | Character 位于该 Site LocalMap 时的**世界位置代理** |

规则：

| ID | 规则 |
|----|------|
| **PH01** | `FootprintHexes.Contains(PresenceHex)` |
| **PH02** | Content 固定；Runtime **不**动态按 LocalMap 内坐标重算所属 Hex |
| **PH03** | Anchor 与 Presence **可同可不同** |
| **PH04** | WorldGraphEditor 应能编辑／查看 PresenceHex（实现 Deferred） |

Character 在「青石镇 LocalMap」时，世界层统一视为 `Character World Hex = Site.PresenceHex`。

---

## 7. 三种主要世界存在状态

### A. PlayerParty（玩家本人）

- 最多 6 人；1 Active + Followers AI  
- WorldMap：**Active Character Avatar** 作为 Party Marker  
- 具备完整世界旅行、LocalMap／Wilderness、手动战斗、**Attack／Capture WorldSite**、亲自参战  

### B. Background Character（普通后台角色）

不属于当前 PlayerParty，也未编入 FormalArmy。

- 可世界旅行、遭遇、**Simulation Battle**、受伤／死亡  
- WorldMap **不常驻**个人头像  
- **无** AttackWorldSite / CaptureWorldSite 政治征服权限  
- 远方走 **Low Frequency / Data Simulation**（不必实时加载每张 LocalMap）

#### 世界旅行 ≠ 远程 RTS 控制（硬规则）

Background Character **可以**在 HexWorld 中进行 World Travel，**不代表**玩家可以远程选中他并直接指定 Hex／路径／具体移动命令。

| 实体 | 玩家可下达的世界层命令 |
|------|------------------------|
| **PlayerParty** | 直接世界旅行（WorldMap 选点、Auto Travel Future、LocalMap 连续移动等） |
| **FormalArmy** | 战略军事命令（移动、Attack Army／Site、驻扎等） |
| **Background Character** | **无**远程逐步移动命令 |

Background Character 的移动**仅由**以下驱动：

- 自身 AI 目标  
- Character Policy（§10）  
- 未来 Sect Mission（Deferred）  
- 剧情  
- 返回／工作／修炼等明确系统原因  

**禁止**让「普通 Character 可以 World Travel」重新演变成隐藏版 RTS 单位移动。

#### Background Battle（架构 vs UX）

**已明确（架构）：**

- 后台战斗**真实发生**  
- 战损**真实回写**（HP、灵气、Injury、Dying、Death 等）  
- Character 可以受伤／重伤／死亡  

**Deferred（UX）：** 具体哪些事件通知玩家、是否暂停、是否弹窗——属于后续叙事 UX 设计；**不阻塞** Background Simulation 架构。

### C. FormalArmy（正式军事组织）

- WorldMap：**Leader Avatar / Army Marker** 常驻  
- 战略移动、公开远征、Attack Enemy Army／WorldSite、Capture、驻扎、战争  
- 默认 **AI 战略 + Auto Battle**  
- 成员仍是真实 Character；战损必须回写  

---

## 8. FormalArmy 职责边界（Supersede 旧「移动资格」）

### 废除

旧规则「Character 跨 Hex 必须先组成至少 1 人 Army」→ **正式废除**。

普通 Character **可以**在世界中移动。FormalArmy **不再是**世界移动资格，而是：

> **正式军事远征组织。**

### 仍有效（勿推倒）

- 成员 = 真实 Character；禁止匿名修士兵力  
- 一名 Character 同时最多属于一支 FormalArmy  
- 不跨 Faction 混编  
- Leader／替补／全灭消失  
- CaptureObjective、War、Ownership、Faction、Pure Hex、Multi-Hex Site、Hex pathing、Army Capacity、驻扎、Auto Battle、Snapshot  

### 组建／解散

- **只能在我方控制 WorldSite** 内正式组建／解散  
- 被编入者必须**真实位于该 Site**；禁止隔空组军  
- 不默认支持荒野一键全员散人  

### 战斗权限

| 场景 | 规则 |
|------|------|
| FormalArmy 接战且 PlayerParty **不在**附近 | **仅 Auto Battle**；禁止点击远方 Army 切入手动 |
| PlayerParty 自己遭遇 | 手动战斗：仅控制 Active；Followers AI；禁止 RTS 框选多人下令 |
| FormalArmy 接战且 `HexDistance(PlayerParty, BattleHex) ≤ 1` | 允许**手动介入**；玩家只控制 Active；Followers AI；**Army 全体仍 AI**（玩家赶到战场，不是接管 Army） |
| V1 判断时机 | Engagement **正式生成时**判断距离；中途动态加入 Deferred |

进攻与防守对称：敌攻我方 Site 时同样用距离 ≤1 决定是否可亲自防守。

---

## 9. 政治控制 vs 个人战斗

**铁则：**

> 个人战斗 ≠ 政治战争。

普通 Background Character 即使杀敌，**不**自动 Capture／改 Owner／改 Territory。

只有：

```text
PlayerParty  或  FormalArmy
```

拥有 **AttackWorldSite / CaptureWorldSite**。

### PlayerParty 攻占据点（与 FormalArmy 对称门槛）

PlayerParty **不必**转换成 FormalArmy 即可亲自攻占据点（RPG 亲自夺点）。  
远程派兵夺点 → 必须 FormalArmy。

PlayerParty 的**特殊权限仅限**：

- **不需要**转换成 FormalArmy  
- 玩家本人可以**亲自进入 LocalMap 手动战斗**  

**不能绕过**现有 [2A](2A-factions-armies-diplomacy-and-capture.md) 正式流程：

```text
PlayerParty Attack Enemy WorldSite
→ Valid War State（合法战争状态）
→ Battle
→ CaptureObjective
→ Capture
```

即：PlayerParty 享有 Capture **资格**与**亲自参战**体验，**不**享有跳过 War／CaptureObjective 的特权。

---

## 10. Character Policy（非即时命令）

对非 Active 角色的主控制方式：

> **权限 + 长期行为倾向（Character Policy）**，不是当前动作命令。

禁止：`去 Hex(31,42)`、`杀掉李四`、`向东走两格` 等远程逐步命令。

### V1 Policies

| Policy | 含义 |
|--------|------|
| **AllowLeaveFactionTerritory** ON/OFF | OFF 时 AI **绝不主动**进入 `Hex.ControlFactionId != Character.FactionId` 的 Hex；唯一标准是 ControlFactionId；第一版故意呆板（无逃命破例） |
| **修炼优先** | 影响 AI 权重；不是命令去某坐标打坐 |
| **AllowMilitaryConscription** | 是否允许编入 FormalArmy |
| **AllowSectMissionParticipation** | Future 预留 |

**Follow ≠ Character Policy**（Follow = Party 组织关系）。

### 宗门任务看板（Future）

具体任务（杀某人、探索、护送等）经 **Sect Mission Board** 发布 → 合格角色 AI 接取；**当前不实现**。

### 社交跨世界旅行

第一版禁止「想朋友」驱动跨几十 Hex；同 LocalMap 社交可丰富。

### 宗门资源

**Faction / Sect Storage 由玩家掌控**；Character **默认无自主领取权**（如筑基丹须玩家分配）。  
NPC **不得**自主领取或使用宗门公共资源。  
未来若开放某类资源允许弟子自行领取，**必须**来自玩家建立的明确授权规则——**不是** NPC 默认权限。  
Personal Inventory AI 使用 Deferred。

### 势力范围判断

`AllowLeaveFactionTerritory=OFF` 的唯一空间标准：

```text
hex.ControlFactionId == character.FactionId
```

Territory 未来是 AI 合法边界，不只是涂色；本轮**不做** Territory Tint。

---

## 11. 模拟精度分层

| 层 | 对象 | 方式 |
|----|------|------|
| Full Realtime | 当前 PlayerParty LocalMap | 输入、AI、实时战斗、互动 |
| Low Frequency / Data | 远方 Background Character | Travel、Activity、Encounter、Battle、Injury、Death |
| Strategic Hex | FormalArmy | 路径、移动、接战、Site Attack、Capture、Auto Battle |

---

## 12. Supersede 清单（旧规则）

| ID | 旧规则 | 新规则 |
|----|--------|--------|
| **OLD-01** | 所有 Character 跨 Hex 必须组成 Army | 普通 Character 可 World Travel；Army = 军事远征 |
| **OLD-02** | 单人移动也必须 1 人 Army | 废除 |
| **OLD-03** | 玩家可直接控制／框选多名我方角色（长期模型） | 仅 1 Active；多选 RTS 为 Legacy Prototype |
| **OLD-04** | WorldMap 上战略移动角色都必须 Army Avatar | 仅 Party Active Avatar + Army Leader Avatar 常驻 |
| **OLD-05** | 远方 FormalArmy 可直接切入手动战 | 默认 Auto；仅 Party 距离 ≤1 可介入且不接管 Army |
| **OLD-06** | LocalMap 与 WorldMap 是两套割裂位置空间 | HexWorld 唯一拓扑；LocalMap=近景；WorldMap=总览 |
| **OLD-07** | 普通 Character 战斗胜利可改 Site Owner | 仅 Party／FormalArmy 可 Capture |

权威冲突时：**本页 + [ADR-0026](../40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) 优先于** [2A](2A-factions-armies-diplomacy-and-capture.md) 中「Army 是唯一跨点载体」等条文；2A 中仍有效的外交／Capture／真实成员规则继续适用。

---

## 13. Deferred / Future（本轮不展开实现）

Sect Mission Board 完整玩法、高级 Personality AI、Policy 紧急破例、社交驱动跨世界旅行、**Background Battle 通知／日志 UX 粒度**、复杂 Wilderness 程序生成、大型城市 LocalArea、精确 Site 四向入口、Flight 正式实现、Territory Tint／Border、Diplomacy 扩展、Economy／Supply、Fog of War、Dynamic Bandit。

---

## 14. 未决（产品级）

见过程文档 [163](../40-process/163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md) §Open Questions。

**Succession V1 合格条件**与 **PlayerParty Capture 须走 War + CaptureObjective** 已于 2026-08-25 拍板（见 §4、§9）。  
**Background Battle 通知粒度** 仍为 Deferred，不阻塞架构。

```text
No hard product-level blockers for starting Phase 1 implementation planning.
Architecture is ready for implementation planning.
```

---

## 15. 架构速览

```text
Player
 ↓
PlayerParty (≤6)
 ├─ ActiveControlledCharacter   ← 唯一即时控制
 └─ Followers (AI)

Background Characters
 ↓ Low Frequency / Data Simulation
 （可战斗／可死亡；无 Capture；WorldMap 不常驻头像）

FormalArmy
 ↓ Strategic Hex Simulation
 （军事远征；默认 Auto Battle；我方 Site 组／解散）

HexWorld = 唯一世界拓扑
 ├─ WorldSite (+ Footprint + PresenceHex + AnchorHex)
 ├─ Wilderness Hex（未来 LocalMap）
 ├─ PlayerParty Presence
 ├─ Background Presence
 └─ FormalArmy Presence

LocalMap  = RPG Close-up
WorldMap  = Overview / Travel View
```
