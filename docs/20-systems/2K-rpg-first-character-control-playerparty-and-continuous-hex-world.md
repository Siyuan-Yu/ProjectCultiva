# RPG-First：Active Character、PlayerParty、连续 Hex 世界与 Legacy FormalArmy Adapter

> **2026-09-21 FINAL-SEAL freeze：** 正常 runtime authority 与 Legacy quarantine 已冻结，见 [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。任何新玩法禁止依赖 FormalArmy、ArmyStack、TerritoryRegion、AtHex、Outdoor LocalMap 或 Hex travel 作为 authority。状态：**Implementation Complete / Producer Acceptance Pending**。

> **2026-09-21 LEGACY-FINAL-C Seal：** 现代 Character residual 统一为 `AtWorldPosition + SurfaceId + exact WorldPosition`；AtHex 只允许旧档、旧 Outdoor LocalMap／Hex travel 与非连续兼容输入。旧 StrategicEncounter、RetreatingArmy 与 LingeringBattlefield runtime 已退出，旧玩家 travel helper 使用明确 Legacy compatibility 名称。状态：**Producer Accepted / Sealed**，见 [250](../40-process/250-legacy-final-c-final-strategic-runtime-retirement-2026-09-21.md)。

> **2026-09-21 LEGACY-FINAL-B Seal：** 正常 PlayerParty Outdoor authority 已收口为 `SurfaceId + exact WorldPosition`；现代 WorldMap travel 使用 Surface route 与 `SurfaceVisible`，不创建 HexPath。正常 WorldSite 内仍为 `AtWorldPosition`，Site 只作为 `CurrentOutdoorWorldSiteId` 空间 context。`CurrentHex` 仅为单向派生 compatibility metadata；`LocalVisible` 与 Hex/Wilderness transition 仅保留旧 Outdoor LocalMap／旧档兼容。制作人已人工验收，状态：**Producer Accepted / Sealed**，详见 [249](../40-process/249-legacy-final-b-playerparty-continuous-surface-travel-authority-cutover-2026-09-21.md)。当前进入 LEGACY-FINAL-C。

> **2026-09-21 LEGACY-FINAL-A Seal：** 正常 NPC group 已统一为 Squad + SquadWorldMotion，FormalArmy／ArmyStack／ArmyMembership runtime 已退休并经制作人人工验收。A 正式 **Accepted / Sealed**。当前进入 LEGACY-FINAL-B，将 PlayerParty 正常旅行进一步收口为 `SurfaceId + exact WorldPosition + SurfaceVisible`；Hex／Outdoor LocalMap 保留为旧档／旧内容兼容边界。

> **2026-09-20 MAP-04 current boundary:** 正常 Outdoor 已只使用 exact `WorldPosition` / Continuous Surface navigation；`PartyWorld.AtHex` 与本页 Hex travel 只属于旧档、retired Outdoor LocalMap、Independent Battle / residual compatibility。`ContinuousWildernessPair` runtime 已删除。Separate Space 使用 `InSeparateSpace + LocalMap`，并已随 SPACE-01 在 `49f8650` 封板。

> **2026-09-16 MAP-02 Accepted：** 主 Continuous Surface 的 WorldMap 已使用同源 Surface strategic view 与 exact WorldPosition 投影；PlayerParty 的实际旅行仍含 Hex compatibility。`PlayerPartyHexTravelService.BeginContinuousSurfaceTravel()` 与 `PlayerPartyWorldMotion` 的 Hex 路径字段须在后续 MAP-03 逐项迁出；本页连续 Hex 内容仍是 compatibility 或历史迁移语境。

> **2026-09-14 现行补丁：** [ADR-0035](../40-process/43-decisions/ADR-0035-unified-squads-and-encounter-scope.md) §1、2、5 为现行规则。CW-U1～U4 已将成员与人物遭遇迁移到统一 Squad；CW-U4.1 退役玩家 FormalArmy 产品层入口。FormalArmy 仅作旧 Content／Save／NPC 任务与移动 adapter，不是玩家可选、可编组、可攻击或可下令的单位。

> 状态：旧 Phase 2B／2C 验收保留；最终控制／移动／飞舟设计已确认，新行为待迁移／核查与制作人验收｜优先级：P0｜最后更新：2026-09-12
> 上级：`docs/00-project/00-overview.md`
> 关联：`2A`、`2J`、`24`、`27`、`23`、`ADR-0020`、`ADR-0024`、`ADR-0025`、`ADR-0026`、`ADR-0027`、`ADR-0031`
> 被引用：`03-glossary.md`、`04-reading-guide.md`、`41-roadmap`、`AGENTS.md`
> **本页是玩家控制模型、PlayerParty、世界存在状态、连续 Hex 世界与 FormalArmy 职责边界的正式产品真源。**
> **本文件只锁契约与产品规则；不写 Runtime C#。** 当前 Host 的 RTS 多选、Army-required World Travel、远距离切换控制等视为 **Prototype / Legacy 待迁移**。
> **2026-09-12 当前目标：** [ADR-0031](../40-process/43-decisions/ADR-0031-continuous-outdoor-world-surface-architecture.md) 已是 Continuous Outdoor 正式目标；旧 `1 Hex = 1 LocalMap`、关图才出发和切换 Executor 只作历史实现记录。控制继承、冲突与飞舟职责以 [ADR-0034](../40-process/43-decisions/ADR-0034-conflict-control-succession-and-airship-role.md) 为准。

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

小队实际同行、共享行动目标，但成员各自移动、受伤、持物和保持真实位置。落后、绕路、弥留不自动离队或免费传送。保留单 Active、固定顺序接替和六人上限；组织变化不改 Faction、HomeSite、家庭、私人关系。飞舟不替代小队，本轮运输延期；目的地倾向建筑只记后续方向。

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

**当前目标：唯一行动小队。** 每个正常活动人物属于唯一小队，单人也是小队；玩家、NPC、旧巡逻／守备共用组织模型。PlayerParty 最终仅为玩家所控小队及 Active 的控制投影，不再单独维护可写成员名单。旧 FormalArmy 仅迁移适配，不是特殊战斗入口。

**生命状态 membership 规则（SPACE-01 acceptance fix）：** PlayerParty 成员失去战斗行动能力时，若仍有 Alive／CanFight successor，则先切换 Active，再将失能成员脱离为 singleton squad；恢复生命不会自动重新加入。若无人可行动，为 terminal control／recovery／succession 暂留必要 membership，但这些成员不拥有 follow、travel、跨空间 transition 或 Party materialization authority。

---

## 3. Active 切换

正常情况下：

> **只能在当前 PlayerParty 成员之间切换 Active Character。**

不允许把同 LocalMap、同势力、但未加入 Party 的角色直接设为 Active。
**禁止**远距离点击头像 → 加载远方角色 → 接管控制（废除上帝附身模型）。

正常 RPG 视角：**切换 Active 时一次性聚焦新主控**；日常探索镜头规则见 [§1.1](#11-localmap-camera最终规则2026-08-25)（仅 WASD Hard Follow；RTS 不控镜头）。

---

<a id="control-succession"></a>
## 4. Active 自动接替与势力继承（2026-09-12）

### 情况 A：当前 Active 失能，Party 仍有可控成员

按 Party 固定顺序从第二位开始依次选择下一名可控成员并自动切换。这里不按战力排序，也不弹继承选择窗口。每个成员保留自己的位置和状态。

### 情况 B：Party 全员真正死亡

只有当前 Party **全部真正死亡**后才触发势力继承。全员弥留、倒地或暂时不可操作但仍有生者，不等于全灭，应进入已有战败／等待恢复出口；该出口完整性在实现前核查。

继承者必须属于玩家势力、存活且当前可担任主控，并按项目现有战力口径自动选择最高者；并列使用稳定结果。旧“必须位于己方 Site／不得在出征 FormalArmy”限制由 ADR-0034 替代，不得据此排除实际最强合格者。

```text
先提交旧 Party 的真实伤亡、消耗与遭遇结果
→ 选择玩家势力最强合格者
→ 在继承者自己的实际世界位置取得控制
→ 建立以其为 Active 的新 PlayerParty
```

不得把继承者传送到旧战场、复活旧队或自动补齐六人。玩家势力无人时的终局明确延期：本阶段不定义 GameOver、强制重开、免费复活或凭空继承者。

---

## 5. HexWorld / WorldMap / LocalMap

### 5.1 HexWorld = 世界本身

Pure Hex **保留**（**Legacy Compatibility**）。正式定义：

> **HexWorld 是整个游戏唯一的世界地理拓扑。**（历史／compatibility 定义；future product authority 见下方 2026-09-15 补注）

2026-09-12 修订：此处“拓扑”指 Hex 的战略叠加／摘要职责。普通户外物理世界以 Continuous Outdoor World Surface 和连续 `WorldPosition` 为准；Hex 不规定真实河桥、Site 精确边界或战场裁切。

**2026-09-15 补注（Legacy Compatibility）：** 本节定义属 legacy；WorldMap Hex shell、`hexWorld` Content、`DerivedPresenceHex` 等 current consumers 仍在运行，但 future 地图 authority 已由 [ADR-0036](../40-process/43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) supersede（工具链与旧 Content 迁移见 [ADR-0037](../40-process/43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)）。

不再仅理解为 FormalArmy 战略棋盘。

### 5.2 三层关系

| 概念 | 定义 |
|------|------|
| **HexWorld** | 唯一世界空间 / 世界拓扑（Legacy Compatibility；见 §5.1 补注） |
| **WorldMap** | HexWorld 的缩略观察与旅行视图 |
| **LocalMap** | 某个世界位置的 RPG 近景展开 |

禁止把 WorldMap 与 LocalMap 理解成两套互相割裂的位置空间。

### 5.3 LocalMap 逻辑连续（目标体验）

旧体验（关卡进出）：进 LocalMap → 退回 WorldMap → 战略跳点 → 再进下一张。**Superseded**。

目标：

> Character 走出 LocalMap = 真正离开当前位置并进入相邻世界空间。

**正式不可逆契约见 [§5.8](#58-continuous-world-movementphase-2c-正式契约)。**
V1 允许 Fade／Loading；**不要求** Unity 无缝开放世界；**要求**逻辑连续，且邻格过渡**不** snap 到邻格中心。

### 5.4 普通 Hex 也是世界

无 WorldSite 的 Forest / Plains / Mountain 等 Hex **本身也是世界**。WorldSite 是 POI／聚落／洞府，不是世界本身。

### 5.5 Wilderness LocalMap

~~普通／荒野 Hex → 1 Hex = 1 逻辑 LocalMap 实例。~~ **SUPERSEDED（2026-09-12）：** 普通户外属于 Continuous Outdoor World Surface；Hex 只作战略叠加。旧 LocalMap 展开仅为历史实现。

### 5.6 WorldMap 长期职责

1. 观察世界
2. 查看当前位置
3. 选择远距离目的地：有效地面点击经统一投影解析为连续目标；Site 标记解析到该 Site 的真实合法抵达点
4. Auto Travel（`MovementState.AutoTravel`；Phase 2C 契约）
5. 查看 FormalArmy
6. 查看 WorldSite

**不是**「所有角色必须进入战略单位模式才能移动」。
WorldMap 不保存第二套坐标真源；但这不禁止把有效地面点击通过统一投影解析为既有 `WorldPosition`。导航覆盖不足时可只承诺目标提交，不得假称全大陆精确路线已经可用。

### 5.7 Auto Travel / TravelMode

PlayerParty 选 **Hex 或 WorldSite** 目标 → 进入 `MovementState.AutoTravel` → 沿路径以 **Continuous WorldPosition** 真实移动 → 世界时间流逝；途中可遭遇／取消／展开 LocalMap。
预留 **TravelMode**（地面／未来飞行等）；**本轮不实现飞行**。完整契约见 §5.8。

---

## 5.8 Continuous World Movement（Phase 2C 正式契约）

> **本小节 = Continuous World Movement 的正式产品真源。**
> Phase **2B 已封板**；Phase **2C 契约锁定于 2026-08-26**；Phase **5R 契约锁定于 2026-08-30（[ADR-0027](../40-process/43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md)）**。
> **只锁规则；不写 Runtime C#。** §5.8.3／§5.8.5／§6 由 **ADR-0027** 扩展：Canonical World Surface Position 统一真源（Wilderness 与 WorldSite 内）、WorldSiteSpatialMapping、PresenceHex 改 derived。§7 PlayerParty／Background／Army 边界继续有效。

### 5.8.1 三层职责（不可逆）

| 概念 | 唯一职责 |
|------|----------|
| **HexWorld** | **唯一**世界拓扑（邻接、距离、Footprint、路径图）（Legacy Compatibility；见 §5.1） |
| **WorldMap** | HexWorld 的**总览／AutoTravel UI** |
| **LocalMap** | 某一世界位置的 **RPG 近景** |

禁止把 WorldMap 或 LocalMap 当成第二套世界坐标真源。

<a id="worldmap-target-resolution"></a>
### 5.8.2 WorldMap 目标解析（2026-09-12）

| 输入 | 当前正式语义 |
|---|---|
| 有效地面点击 | 通过 WorldMap 与 Continuous Surface 的统一投影解析为连续 `WorldPosition` 目标；点击结果是对既有位置真源的命令，不建立第二套坐标 |
| Site 标记 | 解析为该 Site 内真实、合法、可达的抵达点，不吸附 AnchorHex／格心 |
| Hex 摘要 | 可作为粗粒度观察、筛选或 goal-only 入口；不能覆盖点击得到的真实物理目标 |

旧“WorldMap 永久只允许 Hex／Site，`PreciseWorldDestination` 永久禁止”由本节替代。同一 Hex 内两个不同合法点击点必须保持不同物理目标。路线能力尚未覆盖时应明确 goal-only，不把扩大 Streaming 窗口或格心路径冒充全局精确导航。

### 5.8.3 Runtime 真源：Canonical World Surface Position（ADR-0027）

> 5R 正式名：**CanonicalWorldSurfacePosition**（= 本节原 Continuous WorldPosition 唯一真源语义，扩展到 WorldSite 内，见 [ADR-0027](../40-process/43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md)）。

| 概念 | 规则 |
|------|------|
| **CanonicalWorldSurfacePosition** | PlayerParty 在整个连续世界表面（Wilderness 与 WorldSite 内）的**唯一物理位置真源** |
| **LocalPosition** | 真正 Interior／洞府／地下及临时 Encounter 的局部执行坐标；普通 Outdoor 直接使用 Continuous Surface 世界位置，不再依赖 Site LocalMap 投影 |
| **CurrentHex** | **混合语义，禁止立即删除/全局替换（5R-0.1 修正 #3）**：① `PhysicalDerivedHex = WorldToHex(WorldPosition)`；② `RouteCommittedHex = HexPath[SegmentIndex]`（Travel 正式提交格）；③ `CurrentWildernessHex = 当前 Wilderness LocalMap/Surface Context`。Hex 边界附近 ①② 可暂时不同（Phase 5C takeover 分叉即证据）；先审计调用点分类（5R-C），再逐步退役 |
| **DerivedPresenceHex** | `WorldToHex(CanonicalWorldSurfacePosition)` 的战略派生／缓存；不得 clamp 到 Site footprint，也不得决定 CurrentSite 或建筑归属 |
| 禁止 | 以离散 CurrentHex 为唯一真源再「猜」连续位置；多个可独立漂移的位置字段并存争夺真源；AtSite 时跳 Anchor／PresenceHex／ingress center（修正 #5） |

### 5.8.4 WorldLocation vs MovementState（分离）

**WorldLocation**（在哪）与 **MovementState**（是否在自动旅行）**必须分离**：

```text
WorldLocation =
  | AtWorldSite { SiteId }
  | AtWorldPosition { ContinuousPosition }

MovementState =
  | Idle
  | AutoTravel
```

- `AtWorldSite`：位于某 WorldSite（聚合态；见下）
- `AtWorldPosition`：位于普通／荒野连续世界坐标
- `Idle`：未在 AutoTravel
- `AutoTravel`：正沿 WorldMap 下达的 Hex／Site 目标自动移动

二者正交：例如 `AtWorldPosition + AutoTravel`、`AtWorldSite + Idle`。

### 5.8.5 WorldSite：由真实位置解析的行政 Context

> **旧「Aggregated：LocalMap 只改 LocalPosition；WorldMap 投影恒 = PresenceHex；禁止按 Local 坐标投影」由 [ADR-0027](../40-process/43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md) SUPERSEDED。**

| 规则 | 说明 |
|------|------|
| Site Context | 由 Continuous `WorldPosition` 与当前唯一有效行政控制解析；不覆盖物理位置 |
| Site 内 Outdoor 移动 | 直接改变同一 Continuous Surface 上的世界位置，不切 Outdoor LocalMap |
| WorldMap 投影 | 使用同一个 Canonical WorldPosition；不跳 Anchor、不固定 PresenceHex |
| 进入／离开 Site 范围 | 只是行政 Context 变化，不造成切图、传送或坐标重写 |
| Legacy / Interior | `WorldSiteSpatialMapping` 只服务仍待迁移的旧 Outdoor LocalMap 或真正独立 Interior；不得作为新普通 Outdoor 的范围真源 |

`AtWorldSite{SiteId}` 仅可作为兼容 Context；当前 Site 应随实际位置和有效控制重算。Site 易主或范围改变不得移动人物。

### 5.8.6 普通／荒野 Hex

> **历史实现，已由 ADR-0031 和 2026-09-12 当前目标替代。** 普通户外不再以 1 Hex 一张探索图为最终规则。

| 规则 | 说明 |
|------|------|
| 位置模型 | 使用 **Continuous WorldPosition**（非 Site 聚合） |
| Legacy LocalMap | 旧实现曾按 1 Hex 展开一张图；仅用于迁移核查，不是当前普通 Outdoor 契约 |
| 世界投影 | 由 ContinuousPosition 经 `WorldToHex` 派生 |

### 5.8.7 Legacy SurfaceExit（仅旧 Outdoor 实现／真正 Interior）

> 普通 Continuous Outdoor 跨 Chunk／Hex／Site 边界不切图，也不使用 SurfaceExit。下述 Exit Trigger 仅说明尚未退役的 Legacy Outdoor LocalMap；真正 Interior 仍使用其明确入口／出口，不以 Hex 边缘强制过渡。

```text
走到当前 LocalMap 边缘
→ 过渡到 Neighbor Hex
→ Continuous WorldPosition 连续进入邻格
→ 不 snap 到邻格中心
```

| 要求 | 禁止 |
|------|------|
| 逻辑连续跨 Hex | Snap 到 Neighbor Hex.Center 作为过渡终点 |
| 可 Fade／Loading | 把跨格做成「传送到邻格中心再展开」的战略跳点体验 |

#### Surface Exit Trigger Zone（正式）

Surface LocalMap（WorldSite Surface 与 Wilderness 共用）在可玩边界内侧有固定深度的 **Exit Trigger Zone**：

```text
Canonical Geometry（固定）
= PlayableBounds + ExitTriggerDepth + Hex Direction

Runtime Availability（可变）
= Neighbor／Site 出站合法性／Terrain passable

Visible Overlay
= Geometry ∩ Availability
```

| 规则 | 说明 |
|------|------|
| Geometry 真源 | **只**由当前 LocalMap 的 PlayableBounds + `ExitTriggerDepth` 决定 |
| 确定性 | 同一 LocalMap：首次进入／返回／SaveLoad／任意 EntryDirection／角色站位 → Geometry **完全相同** |
| Availability | CurrentHex／Site Footprint 等**只**决定某方向 Enabled／Disabled，**不得**改 Bounds |
| Detection | 已在 Enabled Zone 内 + 继续向外 intent → Transition；刚踏入 Zone **不**自动传送 |
| Presentation | Overlay **精确**覆盖 Trigger Geometry（可简陋半透明块）；禁止另估「宽边提示带」 |
| Interior | 洞窟／室内等 Interior：**不**显示 Surface Exit Zone，**不**启用 Hex Edge Transition |
| 禁止 | 每张地图手摆 Portal Prefab；用 Prefab 位姿作 Exit 判定真源 |

`ExitTriggerDepth` 为 **Gameplay** 参数（MapLayout 可配置）；默认应是明显但较窄的边缘带，不得随地图半宽比例膨胀到覆盖大片地图。

实现索引见 [164](../40-process/164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md)。

### 5.8.8 打开／关闭 WorldMap

开关 WorldMap 只改变观察视图，默认不改变既有暂停状态，也不取消、重建或改写移动计划。AutoTravel 保持同一目标、路径进度、Continuous WorldPosition 与物理执行器；不得关图改 Idle、展开另一套 LocalMap、吸附 Hex 中心或丢失路线。

### 5.8.9 PlayerParty 与延期项

| 规则 | 说明 |
|------|------|
| **共用 WorldLocation** | 整个 PlayerParty **共享一个** `WorldLocation` |
| **无 Fake Army** | Party 连续旅行 **不是** FormalArmy；禁止伪装成 Army 单位旅行 |
| Phase 2C 范围 | **PlayerParty** 连续世界移动／AutoTravel |
| **Background Continuous Travel** | **Deferred** |
| **FormalArmy continuous** | **Deferred**（Army 仍用既有战略层；本契约不授权 Army 连续位姿） |

### 5.8.10 V1 目的地解析

| WorldMap 目标 | V1 到达语义 |
|---------------|-------------|
| **有效地面点击** | 统一投影得到的连续世界目标；同 Hex 内不同点击点保持不同目标 |
| **TargetHex 摘要** | 若入口只给 Hex，则解析一个明确合法目标；格心只能是显式粗粒度 fallback，不能覆盖地面点击 |
| **WorldSite** | 解析该 Site 的真实合法抵达点；WorldMap 投影继续使用 Canonical WorldPosition，不固定 Anchor／PresenceHex |

路径行进过程中位置真源始终是 **CanonicalWorldSurfacePosition**（Site 目标在**完成进入**前可走连续路径；**入站完成**后切 **AtSite Context**，物理位置继续由 Spatial Mapping 保持 Canonical，不跳 Anchor；ADR-0027）。

### 5.8.11 与既有条款的关系

| 条款 | 关系 |
|------|------|
| §5.1–5.2 HexWorld／三层 | Hex 保留战略摘要；普通 Outdoor 物理真源为 Continuous Surface |
| §6 PresenceHex | 仅作为 `WorldToHex(WorldPosition)` 派生摘要；不得 clamp 或决定 Site |
| §7 PlayerParty／Background／Army | **保持**；2C 只做 Party 连续旅行 |
| OLD-06 | **修订**：WorldMap 可把有效地面点击投影为连续目标，但不能建立第二套位置真源 |

---

## 6. Strategic Hex 摘要、AnchorHex 与 Legacy SpatialMapping

> **旧 §6「固定 PresenceHex 作 WorldMap 投影真源」由 [ADR-0027](../40-process/43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md) SUPERSEDED。** 多 Hex Footprint 本身不推翻（见 [2J](2J-hex-territory-worldsites-and-dynamic-bandits.md)）：

```text
1 WorldSiteId · 1 SiteCore · 1 当前 Owner
Footprint / AnchorHex = 战略显示与索引摘要，≠ 物理或行政精确范围
```

### AnchorHex（职责保留）

| 概念 | 职责 |
|------|------|
| **AnchorHex** | 名字、主图标、编辑器参考点、默认镜头焦点、Site 数据锚点 |

**禁止** 用 AnchorHex 代表 PlayerParty 在 Site 内的实际位置（ADR-0027）。

### Derived PresenceHex（新规则，取代固定 PresenceHex）

| 概念 | 规则 |
|------|------|
| **CanonicalWorldSurfacePosition** | PlayerParty 唯一物理位置真源；普通 Outdoor 无 Site 内外两套坐标 |
| **DerivedPresenceHex** | `WorldToHex(CanonicalWorldSurfacePosition)` 的派生摘要；不要求位于 Site footprint |
| 缓存 | 若为性能缓存 DerivedPresenceHex，须明确为 cache、可重建，**不能成为 authority** |

规则：

| ID | 规则 |
|----|------|
| **PH01（当前）** | Runtime 直接由 Canonical WorldPosition 派生 Hex；禁止为了满足旧 footprint 做 project／clamp |
| **PH02（Legacy）** | LocalMap → WorldSiteSpatialMapping → footprint 的旧投影仅适用于尚未迁移的 Outdoor LocalMap 或明确独立空间适配，不约束新 Continuous Outdoor |
| **PH03（新）** | Anchor 与 Derived 可同可不同（由派生结果决定，不强制） |
| **PH04（旧，保留）** | WorldGraphEditor 可查看 AnchorHex（实现 Deferred） |

> **Phase 5R 历史实现：** WorldSite LocalMap 与 footprint 的 normalized mapping 仅作迁移记录；当前普通 Outdoor 不再以此建立 Site 物理空间。

> **Phase 2C 历史：** 旧 Aggregated 规则（站内只改 LocalPosition、WorldMap 投影恒为 PresenceHex）已由 ADR-0027 取代；1-Hex Site 的 DerivedPresenceHex = 其唯一 Footprint Hex。

---

## 7. 世界存在与旧适配投影

### A. PlayerParty（玩家本人）

- 最多 6 人；1 Active + Followers AI
- WorldMap：**Active Character Avatar** 作为 Party Marker
- 具备世界旅行与亲自参战；建筑攻击和接管仍须满足同一战争授权与实际交互条件

### 大地图攻击入口退役（CW-U4.1 实施完成／待验收）

WorldMap 保留查看、选点和 PlayerParty 普通前往；不产生 AttackArmy、追击后自动攻击、宣战接战或 FormalArmy 移动命令。旧多人战略标记只作“NPC 小队”只读投影，不显示 ArmyId，不抢占 PlayerParty 命令权。玩家实际冲突从地面 Character/Squad hostile action 开始。

### B. Background Character（普通后台角色）

不属于当前 PlayerParty 的真实角色；可属于普通 NPC Squad，不以 FormalArmy 区分人物类型。

- 可世界旅行、遭遇、**Simulation Battle**、受伤／死亡
- WorldMap **不常驻**个人头像
- 组织类型本身不赋予或剥夺政治权；远方后台角色没有玩家隔空手操权限
- 远方走 **Low Frequency / Data Simulation**（不必实时加载每张 LocalMap）

#### 世界旅行 ≠ 远程 RTS 控制（硬规则）

Background Character **可以**在 HexWorld 中进行 World Travel，**不代表**玩家可以远程选中他并直接指定 Hex／路径／具体移动命令。

| 实体 | 玩家可下达的世界层命令 |
|------|------------------------|
| **PlayerParty** | 直接世界旅行（有效地面连续目标、Hex 摘要或 Site 真实抵达点） |
| **Legacy FormalArmy adapter** | 无玩家即时命令；仅承载未迁移的 NPC 任务／移动计划 |
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

### C. FormalArmy（Legacy adapter）

- WorldMap 若复用旧数据源，只显示为 **NPC 小队**只读标记
- 旧 NPC 任务、后台移动与自动战可继续使用 `FormalArmyWorldMotion` adapter
- 成员唯一权威是 Squad；FormalArmy 不可重新获得成员或玩家命令权

---

## 8. Legacy FormalArmy Adapter 边界

### 废除

旧规则「Character 跨 Hex 必须先组成至少 1 人 Army」→ **正式废除**。

普通 Character **可以**在世界中移动。FormalArmy **不再是**世界移动资格或玩家正式组织，而是：

> **旧 Content／Save／NPC 任务与移动的兼容 adapter。**

### 仍有效（勿推倒）

- 投影成员必须是真实 Character；禁止匿名修士兵力
- Squad 是成员权威；FormalArmy 只能映射，不能反向成为第二份成员真源
- 旧 NPC `FormalArmyWorldMotion`、Hex pathing、后台 Auto Battle 和 Snapshot 兼容可继续存在
- 玩家 UI 不可创建、选中、直接移动或攻击 FormalArmy

### 组建／解散（历史规则／仅兼容工具）

- **2026-09-06 SUPERSEDED：** Site-only 地点限制由 [ADR-0028](../40-process/43-decisions/ADR-0028-formalarmy-formation-and-roster-use-effective-territory.md) 取代。
- 下列 Create／Add／Remove／ChangeLeader／Disband 是旧内容与开发工具兼容规则，正常玩家 UI 不暴露这些命令。
- 旧工具的操作仍限于 Army faction 的 **Effective Territory Hex**；WorldSite 与 FactionFlag 派生领地一视同仁。
- 被编入者必须由 Domain presence query 解析到**同一个 Hex**；禁止隔空组军、自动集合或 teleport。
- Garrison 仍只允许所属势力拥有的 WorldSite；Wilderness Territory 不提供驻扎设施。

### 战斗权限

| 场景 | 规则 |
|------|------|
| FormalArmy 接战且 PlayerParty **不在**附近 | **仅 Auto Battle**；禁止点击远方 Army 切入手动 |
| PlayerParty 自己遭遇 | 手动战斗：仅控制 Active；Followers AI；禁止 RTS 框选多人下令 |
| PlayerParty 现场遭遇或有限介入 | 按真实位置、同行、守备、关系候选、状态与可达性处理；不使用 `HexDistance ≤ 1` 或 FormalArmy 类型授予资格 |

<a id="airship-worldmap-movement"></a>
### 8.1 飞舟与统一 WorldMap 移动（2026-09-12）

- 飞舟只运输真实人物，不是另一套 FormalArmy 战斗实体，也没有宣战、占领或独占移动资格。运抵后由真实修士战斗、交互和接管。
- 主控可乘或不乘。登船后人物物理位置随舟，下船从合法部署点恢复地面移动；禁止人物船地两份、船开走人留原地或乘船继续原工作区生产。
- 飞舟不依赖地面过桥路线。V1 不做途中拦截、敌船、舰炮、空战、甲板／登船战或船内自由行走。
- WorldMap 只是同一世界的观察与下令视图。开关地图不切换位置或移动 Executor，不吸附格心、不丢路径；所有合法人物／飞舟计划按自身方式推进并统一服从世界暂停。
- 默认开关 WorldMap 不改变已有暂停状态。未来可加统一的可选开图暂停设置，不按玩家、船或 Army 分别造例外。
- 旧“PlayerParty 关图才出发”是已验收的阶段实现记录，不再是最终目标。载员、速度、数量、费用、设施名、停靠／上下客／返航 UI 均后置调参。

---

## 9. 政治控制 vs 个人战斗

### 9.0 当前边界（2026-09-12）

- 人物攻击不自动宣战；实际同行小队按遭遇纳入。私人敌对、本场交战、势力态度和 War 分开。
- 攻击任何势力有效拥有的建筑都必须先处理战争后果；已在人物战中也要暂停确认，确认后在同一战场升级，不重置战斗。
- 政治结果来自真人按正式条件完成的接管。Background Character、PlayerParty、FormalArmy 或飞舟的**类型本身**不提供 Capture 特权；远方日常角色仍不得被玩家隔空手操。

细则以 [2A §19.4](2A-factions-armies-diplomacy-and-capture.md)、[23 §12.2](23-combat.md) 与 ADR-0034 为准。下文旧“仅 PlayerParty／FormalArmy 拥有 Attack/Capture”只作历史迁移背景。

**铁则：个人战斗 ≠ 政治战争。** 普通 Character 的人物冲突不会自动 Capture／改 Owner；攻击势力有效拥有的建筑必须先完成适用战争授权，之后由实际修士在战场内完成正式接管。PlayerParty 不必改组为 FormalArmy，FormalArmy 也不因组织类型自动拥有接管权；飞舟只负责运送人物。

> **历史适用范围（2026-08-25～2026-09-11）：** 旧版本曾只给 PlayerParty／FormalArmy `AttackWorldSite`／`CaptureWorldSite`，并用 FormalArmy 承担远程派兵。该实现记录不再是产品门槛；迁移时保留“远方非主控战斗自动处理”和“不可隔空逐人 RTS 控制”。

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
| **OLD-05** | 远方 FormalArmy 可直接切入手动战 | 远方非主控战斗默认 Auto；玩家介入按真实现场与有限关系／守备条件，不按固定 Hex 距离授权 |
| **OLD-06** | LocalMap 与 WorldMap 是两套割裂位置空间 | HexWorld 唯一拓扑；LocalMap=近景；WorldMap=总览 |
| **OLD-07** | 普通 Character 战斗胜利可改 Site Owner | 仅 Party／FormalArmy 可 Capture |

权威冲突时：**本页 + [ADR-0026](../40-process/43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) 优先于** [2A](2A-factions-armies-diplomacy-and-capture.md) 中「Army 是唯一跨点载体」等条文；2A 中仍有效的外交／Capture／真实成员规则继续适用。

---

## 13. Deferred / Future（本轮不展开实现）

Sect Mission Board 完整玩法、高级 Personality AI、Policy 紧急破例、社交驱动跨世界旅行、**Background Battle 通知／日志 UX 粒度**、复杂 Wilderness 程序生成、大型城市 LocalArea、精确 Site 四向入口、Flight 正式实现、Territory Tint／Border、Diplomacy 扩展、Economy／Supply、Fog of War、Dynamic Bandit、**Background Continuous Travel**、**FormalArmy Continuous Movement**。

> Phase 2C **已锁定** PlayerParty Continuous World Movement 契约（§5.8）；上列 Background／Army 连续移动仍 Deferred。

---

## 14. 未决（产品级）

见过程文档 [163](../40-process/163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md) §Open Questions。

队内顺序接替、全队真正死亡后的势力最强合格继承，以及真人按战争授权在场内接管已由 2026-09-12 规则替代旧 Succession／Capture 类型资格（见 §4、§9）。
**Background Battle 通知粒度** 仍为 Deferred，不阻塞架构。

```text
Design confirmed; implementation migration requires separately authorized phases.
Producer acceptance remains pending for the 2026-09-12 behavior.
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
 （可战斗／可死亡；不可远程逐个附身；WorldMap 不常驻可操控头像）

FormalArmy
 ↓ Strategic Hex Simulation
 （军事组织；默认远方 Auto Battle；无地图移动／宣战／占领类型特权）

Continuous Outdoor World Surface = 普通户外物理世界
 ├─ WorldSite（由 SiteCore 实际行政范围解析的 Context）
 ├─ Strategic Hex（摘要／索引，不决定物理边界）
 ├─ PlayerParty：共用 WorldLocation + MovementState（Phase 2C）
 ├─ Background Presence（连续旅行 Deferred）
 └─ FormalArmy Presence（连续位姿 Deferred）

位置真源 = CanonicalWorldSurfacePosition（Wilderness 与 WorldSite 内统一）
         | AtSite(SiteId) = 战略 Context（不覆盖物理位置）
DerivedPresenceHex = WorldToHex(...)（纯战略派生，不 clamp 到 Site footprint）
CurrentHex = 混合语义（PhysicalDerivedHex / RouteCommittedHex / CurrentWildernessHex），5R-C 分类后退役

LocalMap  = Interior／洞府／地下或 Legacy Outdoor 展开；普通 Outdoor 不因边界切图
WorldMap  = 同一位置权威的总览／下令 UI（有效地面点击可解析连续目标）
```
