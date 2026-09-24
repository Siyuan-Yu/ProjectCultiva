# RPG-First：Active Character、PlayerParty、Continuous Surface 与 Legacy Compatibility

## SOCIAL-QUEST-01 最终封板（2026-09-25）

**Producer Accepted / Sealed**。制作人已验收主流程和最终不可控、不可手动停止跟随 P1。

Temporary quest companion participates in party travel and combat, but is not a player-controllable character.
临时同行跟随受控队伍、随队进入 Separate Space、通过 NPC AI 参战并占用容量；可以选中查看，不能成为 ActiveCharacter、不能接受玩家手动战斗命令或普通 Stop Follow。可控性从永久 Character roster / 既有玩家势力管理 authority 派生，首先排除 QuestCompanion binding；UI、手动命令后端、自动 Active 候选和恢复共用判定。成员、空间和自动战斗 authority 保留。

Snapshot v11 保留既有调度字段并完整保存临时绑定；严格校验实体/实例/受控 Squad，恢复不重放邀请、不替换同模板实体、不新增 controllability bool。v1～v10 严格拒绝。任务 ReadyToClaim/Completed/Failed/放弃后锁定 PendingDeparture，安全普通 Surface 经私有生命周期入口离队，共用精确位置与工作清理；原 Squad 仍合法、同 Surface 同落点且可接纳才恢复，否则 singleton。


> **2026-09-22 正式运行依赖退役落实：** 正常 runtime authority 仍为 `SurfaceId + exact WorldPosition`，且旧依赖已从“运行时 quarantine”推进到物理退役：`SimulationWorld` 无 HexWorld，Core Hex 目录已删除，PlayerParty／WorldPresence 无 Hex 参数或缓存，正常产品不编译旧 Hex 几何。Runtime Loader 拒绝 `formalArmy`／`hexWorld`；`LegacyRuntimeConverter` 只无损处理 FormalArmy 与 current authority 完整的 hybrid Snapshot，`hexWorld`／`openingHexWorldId` 会被检测并拒绝，必须使用现有 WorldComposer／SurfaceAuthoring Legacy migration 路径且无样例时不猜。新增 `Legacy*` adapter 不再是终态。见 [ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。

> **2026-09-21 LEGACY-FINAL-C Seal：** 现代 Character residual 统一为 `AtWorldPosition + SurfaceId + exact WorldPosition`；AtHex 只允许旧档、旧 Outdoor LocalMap／Hex travel 与非连续兼容输入。旧 StrategicEncounter、RetreatingArmy 与 LingeringBattlefield runtime 已退出，旧玩家 travel helper 使用明确 Legacy compatibility 名称。状态：**Producer Accepted / Sealed**，见 [250](../40-process/250-legacy-final-c-final-strategic-runtime-retirement-2026-09-21.md)。

> **2026-09-21 LEGACY-FINAL-B 历史 Seal：** 当时正常 PlayerParty Outdoor authority 已收口为 `SurfaceId + exact WorldPosition`，尚存的 `LegacyCurrentHex`／Outdoor LocalMap compatibility 随 2026-09-22 正式运行依赖退役被物理删除。里程碑记录见 [249](../40-process/249-legacy-final-b-playerparty-continuous-surface-travel-authority-cutover-2026-09-21.md)，不得把当时保留项当作当前 API。

> **2026-09-21 LEGACY-FINAL-A Seal：** 正常 NPC group 已统一为 Squad + SquadWorldMotion，FormalArmy／ArmyStack／ArmyMembership runtime 已退休并经制作人人工验收。A 正式 **Accepted / Sealed**；Hex／Outdoor LocalMap 只保留旧档／旧内容兼容边界。

> **2026-09-20 MAP-04 历史边界：** 当时 normal Outdoor 已切 exact `WorldPosition`，仍保留的 AtHex／Outdoor LocalMap compatibility 已由 2026-09-22 正式运行依赖退役取代。Separate Space 使用 `InSeparateSpace + LocalMap`，并已随 SPACE-01 封板。

> **2026-09-16 MAP-02 Accepted：** 主 Continuous Surface 的 WorldMap 已使用同源 Surface strategic view 与 exact WorldPosition 投影；PlayerParty 的实际旅行仍含 Hex compatibility。`PlayerPartyHexTravelService.BeginContinuousSurfaceTravel()` 与 `PlayerPartyWorldMotion` 的 Hex 路径字段须在后续 MAP-03 逐项迁出；本页连续 Hex 内容仍是 compatibility 或历史迁移语境。

> **2026-09-14 历史补丁：** ADR-0035 当时将成员与人物遭遇迁移到统一 Squad，并退役玩家 FormalArmy 产品入口；当时保留的旧 Content／Save adapter 随 2026-09-22 正式运行依赖退役改为 runtime 拒绝 + 独立离线转换。

> 状态：PlayerParty／Squad／Surface travel authority、Emergency Handoff 与 True-Death Succession 已实现、人工验收并封板；飞舟仍为 Future｜优先级：P0｜最后更新：2026-09-24
> 上级：`docs/00-project/00-overview.md`
> 关联：`2A`、`2J`、`24`、`27`、`23`、`ADR-0020`、`ADR-0024`、`ADR-0025`、`ADR-0026`、`ADR-0027`、`ADR-0031`
> 被引用：`03-glossary.md`、`04-reading-guide.md`、`41-roadmap`、`AGENTS.md`
> **本页是玩家控制模型、PlayerParty、世界存在状态、Continuous Surface 与 Legacy Compatibility 边界的正式产品真源。**
> 旧 RTS 多选、Army-required World Travel 与远距离切换控制均已退休；历史段落不得作为恢复这些入口的依据。
> **2026-09-24 当前 WorldMap／控制规则：** `1 Hex = 1 LocalMap` 与切换 Hex executor 只作历史记录；WorldMap 是 planning overlay，打开时冻结 Surface path 表现，选择目标后关闭地图再由同一 Surface travel authority 继续旅行。控制连续性以 [ADR-0039](../40-process/43-decisions/ADR-0039-external-faction-control-handoff.md) 为准；冲突与飞舟未冲突部分继续见 ADR-0034。

> **当前实现闭包（2026-09-22）：** 上述旧 runtime 容器、PlayerParty／WorldSite Hex API 与 Core Hex 几何类型均已退出正式产品代码；历史段落中的名称只说明迁移过程，不是当前可调用 API。稳定旧 wire key／数值仅用于拒绝诊断和离线转换，不在 runtime 自动迁移。
>
> **尺度现状：** `ContinuousWorldMovementScale.Resolve` 只读 `SimulationWorld.ContinuousWorldMovementScale`；唯一注入点由当前 opening `outdoorSurface.movementScale` 提供。当前 BaseGame 显式为 `1.0`。该字段是 movement budget 尺度，**不是** `cellSize`；既有速度公式与 tick 行为保持不变。

---

## 0. 产品本质

> **游戏本质首先是修仙 RPG，而不是 4X / Total War。**

玩家始终在扮演**具体的真实 Character**。宗门、同行者、WorldSite、领土、资源与战争是角色成长以后**拥有或参与的东西**，不取代 Character 成为玩家本体。

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

**当前规则：唯一行动小队。** 每个正常活动人物属于唯一 Squad，单人也是小队；PlayerParty 是玩家所控 Squad 及 Active 的控制投影。旧 FormalArmy 只允许由独立离线转换器处理有明确无损规则的输入；Runtime Loader 不读取或自动迁移它。

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
## 4. Active 自动接替与外部势力控制转移（2026-09-24）

### 情况 A：当前 Active 失能，Party 仍有可控成员

按 Party 固定顺序从第二位开始依次选择下一名可控成员并自动切换。这里不按战力排序，也不弹继承选择窗口。每个成员保留自己的位置和状态。

### 情况 B：Party 无可控成员但仍有生者（Emergency Takeover）

当前 Party 没有任何 `CanActAsActive` 成员、但仍有 Alive／Incapacitated 成员时，可从玩家势力其它合法人物中执行紧急接管。必须先正式提交战斗、伤亡和战报并清除 Encounter participant authority；战斗过程中不得切走。

旧 Party 的存活成员留在原地组成 idle Recovery Squad，保留伤势、势力与精确位置，不治疗、不复活、不传送、不自动跟随新主控。其后恢复也不会自动抢回控制；玩家可通过正常 Party 管理重新接纳。没有外部候选时不拆旧 Party，继续 `TemporarilyUnavailable`，等待原成员恢复。

### 情况 C：Party 全员真正死亡（Succession）

当前 Party 全部真正 Dead／Removed 时触发势力继承。Dead／Removed 成员继续使用现有 singleton/corpse retirement，不进入可移动 Recovery Squad。

继承者必须属于玩家势力、存活且当前可担任主控，并按项目现有战力口径自动选择最高者；并列使用稳定结果。旧“必须位于己方 Site／不得在出征 FormalArmy”限制由 ADR-0034 替代，不得据此排除实际最强合格者。

Emergency 与 Succession 共用同一外部候选规则：实际 Character、玩家势力、非旧 Party、Alive、`CanActAsActive`、不受未结束 Encounter 持有，并由 `CharacterWorldPresenceQuery` 解析出 finite 的合法 `SurfaceId + exact WorldPosition`。AtWorldPosition、带 exact anchor 的 AtWorldSite 与当前 SquadWorldMotion 均可；禁止 Site center、arrival point、旧 Hex、旧战场或旧 Party 坐标猜测。按 `CombatPowerCalculator.ForEntity` 最高者选择，同值按稳定 EntityId；不使用距离、名称、字典顺序或随机。

```text
先提交旧 Party 的真实伤亡、消耗与遭遇结果
→ 在同一候选规则中选择玩家势力最强合格者
→ 按 Emergency Takeover／Succession 处理旧成员
→ 在接管者自己的实际世界位置取得控制
→ 建立只含接管者的 PlayerParty
```

不得把继承者传送到旧战场、复活旧队或自动补齐六人。玩家势力无人时的终局明确延期：本阶段不定义 GameOver、强制重开、免费复活或凭空继承者。

接管者若原属于 NPC Squad，只拆出本人；其余 roster、原位置与 motion 保留，原 Leader 被拆出时按稳定成员顺序补 Leader。接管前必须先解析并捕获其真实位置，再解除来源 Squad／BackgroundTravel authority、重建单人 `squad:player` 并设置 PlayerParty motion，禁止先 detach 后猜位置。

表现交接继续使用唯一 player-centered Continuous Surface streaming。跨 Surface，或同 Surface 但接管点在旧 loaded 5×5 外时，执行 hard re-anchor 并以接管者 chunk 为中心同步构建新邻域；已在当前邻域内则避免无意义重建。Surface、正确 ActiveSurfaceId、接管点 loaded、邻域人口 reconcile 与 successor EntityView 全部就绪后，才能切 Camera／Selection。

---

## 5. Continuous Surface / WorldMap / Separate Space

### 5.1 Continuous Surface = 普通户外世界

普通 Outdoor 以 `SurfaceId + exact WorldPosition + SurfaceGroundNavigation` 为唯一物理地理 authority。`SimulationWorld` 无 HexWorld，正式产品不编译旧 Hex 几何。旧 `hexWorld`／Q-R 只可出现在格式识别与拒绝、稳定 wire、独立离线转换或历史资料中。

### 5.2 三层关系

| 概念 | 定义 |
|------|------|
| **Continuous Surface** | 普通 Outdoor 的唯一世界空间、地形、通行与精确位置真源 |
| **WorldMap** | 同一 Surface 的战略缩放／观察／选点与旅行命令视图，不建立第二套位置 |
| **Separate Space / LocalMap** | 真正独立的 Cave／Interior／Dungeon 或 Encounter 战术空间；有自己的局部位置与明确回程 authority |

普通城市、村镇、荒野与 Site 均在同一 Surface 上连续移动；只有真正 Separate Space 才切换空间。

### 5.3 LocalMap 逻辑连续（目标体验）

旧体验（关卡进出）：进 LocalMap → 退回 WorldMap → 战略跳点 → 再进下一张。**Superseded**。

目标：

> Character 走出 LocalMap = 真正离开当前位置并进入相邻世界空间。

**正式不可逆契约见 [§5.8](#58-continuous-world-movementphase-2c-正式契约)。**
V1 允许 Fade／Loading；**不要求** Unity 无缝开放世界；**要求**逻辑连续，且邻格过渡**不** snap 到邻格中心。

### 5.4 普通户外也是世界

无 WorldSite 的森林、平原、山地与道路仍属于 Continuous Surface。WorldSite 是地点／行政 Context，不是承载普通户外的独立地图。

### 5.5 Wilderness LocalMap

~~普通／荒野 Hex → 1 Hex = 1 逻辑 LocalMap 实例。~~ **SUPERSEDED（2026-09-12）：** 普通户外属于 Continuous Outdoor World Surface；Hex 只作战略叠加。旧 LocalMap 展开仅为历史实现。

### 5.6 WorldMap 长期职责

1. 观察世界
2. 查看当前位置
3. 选择远距离目的地：有效地面点击经统一投影解析为连续目标；Site 标记解析到该 Site 的真实合法抵达点
4. 提交 Surface Auto Travel；地图关闭后由正式旅行链执行
5. 查看 PlayerParty／NPC Squad 与地点／控制标记
6. 查看 WorldSite

**不是**「所有角色必须进入战略单位模式才能移动」。
WorldMap 不保存第二套坐标真源；但这不禁止把有效地面点击通过统一投影解析为既有 `WorldPosition`。导航覆盖不足时可只承诺目标提交，不得假称全大陆精确路线已经可用。

### 5.7 Surface Auto Travel

PlayerParty 在 WorldMap 选择有效 Surface 点或 WorldSite 合法抵达点，关闭地图后进入 Surface Auto Travel，沿 `SurfaceId + WorldPosition` 路线真实移动并消耗世界时间。取消保留当前位置；重放置清旧 Site context；到达保留目的 Site context。正常旅行不创建 Hex 路径，也不展开 Outdoor LocalMap。

---

## 5.8 Continuous World Movement（Phase 2C 正式契约）

> **本小节 = 当前 Continuous World Movement 产品真源。**
> 早期 Phase 2B／2C／5R 的 Hex 与 Outdoor LocalMap 字段、类型和 adapter 已由 ADR-0036／0038 物理退役；以下只保留仍适用于 Surface authority 的规则。

### 5.8.1 三层职责（不可逆）

| 概念 | 唯一职责 |
|------|----------|
| **Continuous Surface** | 普通 Outdoor 的地形、通行、精确位置与 Surface route authority |
| **WorldMap** | 同一 Surface 的总览／选点／AutoTravel UI |
| **Separate Space / LocalMap** | 真正独立空间的局部位置 authority；不承载普通 Outdoor |

禁止把 WorldMap 或 LocalMap 当成第二套世界坐标真源。

<a id="worldmap-target-resolution"></a>
### 5.8.2 WorldMap 目标解析（2026-09-12）

| 输入 | 当前正式语义 |
|---|---|
| 有效地面点击 | 通过 WorldMap 与 Continuous Surface 的统一投影解析为连续 `WorldPosition` 目标；点击结果是对既有位置真源的命令，不建立第二套坐标 |
| Site 标记 | 解析为该 Site 内真实、合法、可达的抵达点，不吸附 AnchorHex／格心 |
同一 Surface 内两个不同合法点击点必须保持不同物理目标。路线能力尚未覆盖时应明确 goal-only，不把扩大 Streaming 窗口冒充全局精确导航。

### 5.8.3 Runtime 真源：Canonical World Surface Position（ADR-0027）

> 5R 正式名：**CanonicalWorldSurfacePosition**（= 本节原 Continuous WorldPosition 唯一真源语义，扩展到 WorldSite 内，见 [ADR-0027](../40-process/43-decisions/ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md)）。

| 概念 | 规则 |
|------|------|
| **CanonicalWorldSurfacePosition** | PlayerParty 在整个连续世界表面（Wilderness 与 WorldSite 内）的**唯一物理位置真源** |
| **LocalPosition** | 真正 Interior／洞府／地下及临时 Encounter 的局部执行坐标；普通 Outdoor 直接使用 Continuous Surface 世界位置，不再依赖 Site LocalMap 投影 |
| **SurfaceId** | 精确位置所属 Surface 的身份；重叠 Surface 时不得省略或猜测覆盖 |
| 禁止 | 以离散格、Site 中心、控制点或 `(0,0)` 猜连续位置；多个可独立漂移的位置字段并存争夺真源 |

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
| Legacy / Interior | 历史 Hex footprint／Outdoor LocalMap mapping 已退出正式程序集；Interior 只使用 Separate Space 自身 MapLayout／EntityLocation，不从旧 footprint 推导 |

`AtWorldSite{SiteId}` 仅可作为兼容 Context；当前 Site 应随实际位置和有效控制重算。Site 易主或范围改变不得移动人物。

### 5.8.6 普通／荒野 Hex

> **历史实现，已由 ADR-0031 和 2026-09-12 当前目标替代。** 普通户外不再以 1 Hex 一张探索图为最终规则。

| 规则 | 说明 |
|------|------|
| 位置模型 | 使用 **Continuous WorldPosition**（非 Site 聚合） |
| Legacy LocalMap | 旧实现曾按 1 Hex 展开一张图；仅用于迁移核查，不是当前普通 Outdoor 契约 |
| 世界投影 | 由 ContinuousPosition 经 `WorldToHex` 派生 |

### 5.8.7 Legacy SurfaceExit（已退役）

旧 Outdoor LocalMap 的边缘区、Neighbor Hex、SurfaceExit 与 footprint availability 链已经物理删除，不再是当前 Runtime API。普通 Outdoor 跨 Chunk／Site 边界始终留在同一 Continuous Surface。

真正 Interior／Cave 使用 Separate Space 的 authored entrance、`HostSeparateSpaceExitTrigger` 与 `SeparateSpaceExitEdgeTrigger`；它恢复 exact outdoor return，不使用 Hex 边缘、Site 中心或旧 SurfaceExit zone。旧实现过程仅见历史索引 [164](../40-process/164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md)。

### 5.8.8 打开／关闭 WorldMap

WorldMap 打开时成为 planning overlay，冻结当前 Surface path 表现但不取消目标或改写物理位置；有效选点可更新同一 Surface travel 计划。关闭地图后从当前 exact position 恢复／重求必要 subgoal 并继续旅行，不展开另一套 LocalMap、不吸附旧格心。

### 5.8.9 PlayerParty 与延期项

| 规则 | 说明 |
|------|------|
| **Party travel authority** | `PlayerPartyWorldMotion` 提供当前同行旅行计划；每名成员仍保有真实 Character presence，不因 Party 身份被抹成一个坐标 |
| **无 Fake Army** | Party 连续旅行 **不是** FormalArmy；禁止伪装成 Army 单位旅行 |
| **PlayerParty Surface travel** | 已实现并封板 |
| **Background／NPC Squad travel** | 独立人物与 `SquadWorldMotion` 的当前 Continuous travel 已实现 |
| **FormalArmy continuous** | 已退役，不是延期功能 |

### 5.8.10 V1 目的地解析

| WorldMap 目标 | V1 到达语义 |
|---------------|-------------|
| **有效地面点击** | 统一 Surface 投影得到 exact world target；不同点击点保持不同物理目标 |
| **WorldSite** | 解析该 Site 的真实合法抵达点；WorldMap 投影继续使用 exact WorldPosition，不固定到历史格心 |

路径行进过程中位置真源始终是 `SurfaceId + exact WorldPosition`。Site 只提供目的 context／arrival intent，不能覆盖物理位置或令到达跳到 Site 中心。

### 5.8.11 与既有条款的关系

| 条款 | 关系 |
|------|------|
| §5.1–5.2 | Continuous Surface／WorldMap／Separate Space 三层是当前正式结构 |
| §6 | 旧 Hex 摘要与 footprint runtime 已退役；只保留历史说明 |
| §7 | PlayerParty／Background／NPC Squad 使用当前 Surface authority；FormalArmy runtime 已退役 |
| OLD-06 | WorldMap 把有效点击投影为连续目标，但不能建立第二套位置真源 |

---

## 6. Strategic Hex／Footprint（历史，runtime 已退役）

旧 `LegacyAnchorHex`／`LegacyPresenceHex`／`DerivedPresenceHex`、WorldSite Hex footprint、validator、bake transform 与 spatial mapping 均已退出正式 runtime。当前 opening、WorldMap、Site range、arrival 与 movement 直接使用 authored Surface placements／anchors、exact world positions 和 Actual Administrative Control。

外部旧 Hex Content 的 Q/R、anchor、presence 与 footprint 只用于格式识别或交给 WorldComposer／SurfaceAuthoring Legacy migration；正常 Loader 不读取或自动迁移。旧几何规则留在历史 ADR／过程文档中，不是当前 API，也不约束 Separate Space。

---

## 7. 世界存在与当前 Surface 投影

### A. PlayerParty（玩家本人）

- 最多 6 人；1 Active + Followers AI
- WorldMap：**Active Character Avatar** 作为 Party Marker
- 具备世界旅行与亲自参战；建筑攻击和接管仍须满足同一战争授权与实际交互条件

### 大地图攻击入口退役（CW-U4.1 已验收／封板）

WorldMap 保留查看、选点和 PlayerParty 普通前往；不产生 AttackArmy、追击后自动攻击、宣战接战或 FormalArmy 移动命令。旧多人战略标记只作“NPC 小队”只读投影，不显示 ArmyId，不抢占 PlayerParty 命令权。玩家实际冲突从地面 Character/Squad hostile action 开始。

### B. Background Character（普通后台角色）

不属于当前 PlayerParty 的真实角色；可属于普通 NPC Squad，不以 FormalArmy 区分人物类型。

- 可由个人／NPC Squad authority 进行 Continuous Surface travel；生命周期、受伤／死亡状态是真实 Character 状态
- WorldMap **不常驻**个人头像
- 组织类型本身不赋予或剥夺政治权；远方后台角色没有玩家隔空手操权限
- 远方走 **Low Frequency / Data Simulation**（不必实时加载每张 LocalMap）

#### 世界旅行 ≠ 远程 RTS 控制（硬规则）

Background Character 可以在 Continuous Surface 中由自身 authority 旅行，**不代表**玩家可以远程选中他并直接指定路径／具体移动命令。

| 实体 | 玩家可下达的世界层命令 |
|------|------------------------|
| **PlayerParty** | 直接世界旅行（有效 Surface 目标或 Site 真实抵达点） |
| **Legacy FormalArmy input** | Runtime 拒绝；先离线转换为 `npcSquad`／当前 Snapshot 独立副本 |
| **Background Character** | **无**远程逐步移动命令 |

Background Character 的移动**仅由**以下驱动：

- 自身 AI 目标
- Character Policy（§10）
- 未来 Sect Mission（Deferred）
- 剧情
- 返回／工作／修炼等明确系统原因

**禁止**让「普通 Character 可以 World Travel」重新演变成隐藏版 RTS 单位移动。

#### Background Battle（架构 vs UX）

**Design Confirmed：**

- 未来后台战斗使用真实 Character，并把战损写回 HP、灵气、Injury、Incapacitated／Dead 等正式状态。
- 当前 Character 生命周期与位置 authority 已存在；完整 NPC 对 NPC 自主接战／后台战斗执行仍为 Future，不能因设计确认而写成已运行。

**Deferred（UX）：** 具体哪些事件通知玩家、是否暂停、是否弹窗——属于后续叙事 UX 设计；**不阻塞** Background Simulation 架构。

### C. FormalArmy（Legacy input only）

- Runtime Loader 拒绝旧 FormalArmy／ArmyMembership／motion authority；WorldMap 只显示 current NPC Squad。
- 独立 `LegacyRuntimeConverter` 只转换有明确无损规则的 FormalArmy Content 或 current authority 完整的 hybrid Snapshot，并输出不同的新文件。
- 成员唯一权威是 Squad；FormalArmy 不可重新获得成员、位置、战斗或玩家命令权。

---

## 8. Legacy FormalArmy 输入边界

### 废除

旧规则「Character 跨 Hex 必须先组成至少 1 人 Army」→ **正式废除**。

普通 Character **可以**在世界中移动。FormalArmy **不再是**世界移动资格或玩家正式组织，只是：

> 独立离线转换器可识别的有界旧输入；正常 runtime 明确拒绝。

### 仍有效（勿推倒）

- 投影成员必须是真实 Character；禁止匿名修士兵力
- Squad 是成员权威；FormalArmy 只能映射，不能反向成为第二份成员真源
- 稳定旧 wire key／数值可用于检测和离线转换，但不得恢复对应 runtime enum、board、service 或 Host 入口
- 玩家 UI 不可创建、选中、直接移动或攻击 FormalArmy

### 组建／解散（历史规则；无当前 runtime 入口）

- **2026-09-06 SUPERSEDED：** Site-only 地点限制由 [ADR-0028](../40-process/43-decisions/ADR-0028-formalarmy-formation-and-roster-use-effective-territory.md) 取代。
- 旧 Create／Add／Remove／ChangeLeader／Disband、Effective Territory Hex 与 Garrison 规则只保留在历史记录；正式 runtime 和玩家 UI 均无这些入口。

### 当前战斗权限

PlayerParty 自己遭遇时只控制 Active，Followers AI；现场遭遇或有限介入按真实位置、同行、守备、关系候选、状态与可达性处理，不使用旧 Hex 距离或 FormalArmy 类型授予资格。完整 NPC 对 NPC 后台战斗仍为 Future。

<a id="airship-worldmap-movement"></a>
### 8.1 飞舟与统一 WorldMap 移动（2026-09-12）

- 飞舟只运输真实人物，不是另一套 FormalArmy 战斗实体，也没有宣战、占领或独占移动资格。运抵后由真实修士战斗、交互和接管。
- 主控可乘或不乘。登船后人物物理位置随舟，下船从合法部署点恢复地面移动；禁止人物船地两份、船开走人留原地或乘船继续原工作区生产。
- 飞舟不依赖地面过桥路线。V1 不做途中拦截、敌船、舰炮、空战、甲板／登船战或船内自由行走。
- WorldMap 只是同一世界的观察与下令视图。打开时冻结当前 Surface travel 表现用于规划，关闭后从 exact position 恢复；不切换 authority、不吸附格心、不丢目标。
- 飞舟尚未实现；载员、速度、数量、费用、设施名、停靠／上下客／返航 UI 均后置。

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

Character Policy 是对非 Active 角色的长期权限／倾向设计，不是远程逐步命令；Follow 仍是 Party 组织关系，不是 Policy。当前仓库未发现 `CharacterPolicy`、`AllowLeaveFactionTerritory`、`AllowMilitaryConscription` 或 `AllowSectMissionParticipation` 的正式 runtime 类型，因此本节状态为 **Design Confirmed / Not Implemented**。

未来实现时：

- 禁止 `去某坐标`、`杀掉某人`、`向东走两格` 等隔空 RTS 微操；
- 是否离开己方领地必须查询当前 Surface 的 Actual Administrative Control，不得恢复 `Hex.ControlFactionId`；
- 不得恢复 FormalArmy conscription；军事实体组织仍使用 Squad／真实 Character；
- Sect Mission Board、社交驱动远行、NPC 自主领取宗门资源与 Personal Inventory AI 均需另行授权。

---

## 11. 模拟精度分层

> FormalArmy strategic runtime 已退休。下表第三行只描述旧输入的迁移目标，不是当前模拟层。

| 层 | 对象 | 方式 |
|----|------|------|
| Full Realtime | 当前 loaded Surface／Separate Space／CharacterEncounter | 输入、AI、实时互动与已接入战斗 |
| Low Frequency / Data | 远方 Background Character／NPC Squad | 当前 Travel／Activity／位置与生命周期；完整自主 Encounter／Battle 仍 Future |
| Legacy migration input | FormalArmy DTO／旧 Hex state | Runtime Loader 拒绝；只有有明确无损规则的 FormalArmy／hybrid Snapshot 可先由独立 converter 输出 current 副本；旧地图走 WorldComposer／SurfaceAuthoring migration |

---

## 12. Supersede 清单（旧规则）

| ID | 旧规则 | 新规则 |
|----|--------|--------|
| **OLD-01** | 所有 Character 跨 Hex 必须组成 Army | 普通 Character／Squad 使用 Surface world travel；FormalArmy runtime 退役 |
| **OLD-02** | 单人移动也必须 1 人 Army | 废除 |
| **OLD-03** | 玩家可直接控制／框选多名我方角色（长期模型） | 仅 1 Active；多选 RTS 为 Legacy Prototype |
| **OLD-04** | WorldMap 上战略移动角色都必须 Army Avatar | PlayerParty 与 NPC Squad 使用各自 exact Surface position 投影 |
| **OLD-05** | 远方 FormalArmy 可直接切入手动战 | FormalArmy 入口退役；当前 CharacterEncounter 只按真实现场与有限关系／守备条件授权，完整远方 NPC 战斗 Future |
| **OLD-06** | LocalMap 与 WorldMap 是两套割裂位置空间 | 普通 Outdoor 与 WorldMap 共用 Continuous Surface；真正 Interior／Encounter 才是独立空间 |
| **OLD-07** | 普通 Character 战斗胜利可改 Site Owner | 接管需正式战争／SiteCore 条件；Character／Squad／载具类型本身不授予 Capture 特权 |

权威冲突时，以本页顶部 Final Seal、[ADR-0038](../40-process/43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md) 与 [2A](2A-factions-armies-diplomacy-and-capture.md) 的现行外交／接管段落为准；ADR-0026 及旧 Army 条文只保留历史决策身份。

---

## 13. Deferred / Future（本轮不展开实现）

Sect Mission Board 完整玩法、高级 Personality AI、Policy 紧急破例、社交驱动跨世界旅行、**Background Battle 通知／日志 UX 粒度**、复杂 Wilderness 程序生成、大型城市 LocalArea、精确 Site 四向入口、Flight 正式实现、Territory Tint／Border、Diplomacy 扩展、Economy／Supply、Fog of War、Dynamic Bandit。Background Continuous Travel 已有当前实现；FormalArmy Continuous Movement 已退役，不是 Future。

PlayerParty、NPC Squad 与独立 Background Character 的当前 Continuous movement authority 已实现并封板；上列只保留真正 Future 的产品能力。

---

## 14. 未决（产品级）

见过程文档 [163](../40-process/163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md) §Open Questions。

队内顺序接替、全队真正死亡后的势力最强合格继承，以及真人按战争授权在场内接管已由 2026-09-12 规则替代旧 Succession／Capture 类型资格（见 §4、§9）。
**Background Battle 通知粒度** 仍为 Deferred，不阻塞架构。

上述当前控制、Surface movement、Squad 与 CharacterEncounter 主线均已实现、人工验收并封板；只对列明的 Future 项保持未授权。

---

## 15. 架构速览

```text
Character（世界中的真实人）
 ├─ PlayerParty Member：Active / Follower
 ├─ Background Character：个人或 NPC Squad authority
 └─ CharacterEncounter Participant：临时场次身份，不改永久组织

Squad（正常活动人物唯一成员组织；单人也是 Squad）
 ├─ PlayerParty = 玩家 Squad + Active 控制投影
 └─ NPC Squad = SquadWorldMotion / task authority

Continuous Surface（普通户外唯一地理）
 ├─ SurfaceId + exact WorldPosition = 位置真源
 ├─ WorldSite = SiteCore / Claim / Actual Control context
 ├─ PlayerParty Surface travel
 └─ Background Character / NPC Squad continuous movement

WorldMap = 同一 Surface 的 planning／缩放视图，不是第二套坐标
Separate Space / LocalMap = Cave／Interior／Dungeon 的局部空间与 exact outdoor return
CharacterEncounter = 临时战术空间 + participant origin／return／tactical authority

Retired：HexWorld、Hex path、Outdoor LocalMap、FormalArmy runtime
```

## SOCIAL-QUEST-01 Temporary Quest Companion（2026-09-25）

Producer Accepted / Sealed；见 [264](../40-process/264-social-quest-01-secret-realm-social-topic-and-temporary-companion-2026-09-25.md)。QuestCompanionBoard 保存 CompanionEntityId、QuestInstanceId、OriginalSquadId、Active/PendingDeparture，membership 仍来自受控 Squad。仅专用受控加入入口绕过永久 roster，容量/空间/生命/battle lock 共用。原 Squad 保留剩余成员，命令目标失效则转 leader，空源 motion 删除。
Host 共用加入 follow、头像/Active、空间转场与 CharacterEncounter；Selection/CommandBridge 投影当前 Party，不写 session.CharacterIds。
任务结束先 PendingDeparture；回普通 Continuous Surface 且无独立空间/战斗/Dialogue/transition/modal，并有另一合法 Active 才离队。本人 Active 时先按 Party 稳定顺序切换；本人失能则等待。复用 TryStopFollow 保留 view 精确落点、清跟随和工作；原 Squad 合法、未满、无锁、同 Surface 同落点才恢复，否则 singleton，不 teleport。
死亡/Removed 清绑定；既有 Emergency handoff 使成员离开受控 Squad 时清绑定，不干预其恢复 Squad/死亡/继承规则。Snapshot 必须匹配受控 Squad；重复/非法绑定拒绝，恢复不重新邀请。


### SOCIAL-QUEST-01 最终 P1 语义更正（2026-09-25）

Temporary quest companion participates in party travel and combat, but is not a player-controllable character.
临时同行保留 Party/Squad presence、容量、转场与自动战斗；不可切 Active，不接受玩家手动战斗指令。可控性由永久 Character roster / 既有玩家势力管理身份派生，并排除临时绑定；UI 与后端共用判定，读档/自动切换也执行。此规则替代此前临时同行可切 Active 的描述。Snapshot v11 shape 和绑定生命周期不变。制作人主流程已验收；P1 与封板详情见 process 264。
