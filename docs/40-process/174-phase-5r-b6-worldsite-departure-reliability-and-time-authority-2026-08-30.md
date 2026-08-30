# Phase 5R-B6 系列 — WorldSite 本地离场、Crossing 可靠性、Route/Goal 权威、执行与时间权威统一

- 日期：2026-08-30
- 基线：`dev @ 690d57f`（B6/B6.1/B6.2 已封板）
- 状态：**本批次（B6.3/B6.3A/B6.4/B6.5）实现完成，待人工验收**
- 人工验证场景：`Assets/Scenes/LevelTester.unity`

## 阶段链（已提交）

```
5591408 B3C3/C3.1 可逆空间映射与边界权威收敛
d8f54bc V2_01~07 测试落库
a7809ba B4 本地移动与 Canonical 同步
774a35e B5 Canonical 查询与大地图位置显示
690d57f B6/B6.1/B6.2 本地离场与战略旅行衔接
```

## 1. B6.3 — WorldSite SurfaceExit Crossing Reliability

### 现象
人工反馈约 20% 概率：Character 正确走进可见透明 SurfaceExit 区域，但不 crossing / 不切换到 Wilderness。

### 结论：20% 不是随机，是确定性 2/10
真实数据（荒村 10 条正式 connection，W3/E3/N2/S2）+ 真实 Core + 真实 A*（GridPathfinder, goalSnapRadius=4, arriveEpsilon=0.2）建立的 **Reliability Matrix**：

| # | side | dst hex | approach∈Slot | predicate | walkable | A*可达 | 最坏停点∈Slot | 状态 |
|---|---|---|---|---|---|---|---|---|
| 0 | W | (79,51) | ✓ | ✓ | ✓ | ✓5/5 | ✓ | OK |
| 1 | W | (79,52) | ✓ | ✓ | ✓ | ✓5/5 | ✓ | OK |
| 2 | W | (79,53) | ✓ | ✓ | ✗(OOB) | ✗ | ✗ | **FAIL** 角/双邻接 |
| 3 | S | (80,50) | ✓ | ✓ | ✓ | ✓5/5 | ✓ | OK |
| 4 | N | (80,53) | ✓ | ✓ | ✓ | ✓5/5 | ✓ | OK |
| 5 | S | (81,50) | ✓ | ✓ | ✓ | ✓5/5 | ✓ | OK |
| 6 | N | (81,53) | ✓ | ✓ | ✓ | ✓5/5 | ✓ | OK |
| 7 | E | (82,50) | ✓ | ✓ | ✓ | ✓5/5 | ✗ | **FAIL** 角/双邻接 |
| 8 | E | (82,51) | ✓ | ✓ | ✓ | ✓5/5 | ✓ | OK |
| 9 | E | (82,52) | ✓ | ✓ | ✓ | ✓5/5 | ✓ | OK |

**2/10 = 20%，与人工观测精确吻合。**

### 根因
`ResolveWorldSiteExitApproachLocalPoint`（B6.2 引入）对角 connection 的 approach 目标不可靠：

- (a) 从 ExitCenter 沿 inward 退 inset → 斜对角方向（|dirX|≈|dirY|）ExitCenter 打在相邻边 → clamp 后贴 SlotRect 内边缘（停点余量 0），`arriveEpsilon=0.2` 停点偏内即出触发带（predicate 容差仅 0.0001）
- (b) 沿边坐标取 ExitCenter 分量 → 落在 playable bounds 外（OOB cell）→ A* goal 不可达

参与面：stopping distance **参与**（核心）；tick 顺序 / SurfaceEdgeGate / cooldown **不参与**（静态排除）。

### 修复（确定性，非补丁）
`ResolveWorldSiteExitApproachLocalPoint` 重写为纯几何（+32/−25）：

```
approach = SlotRect 深度方向中点（窄维度） + 沿边方向 = SlotRect ∩ playable bounds 中点
```

- 深度中点距两缘 = depth/2 = 0.625 > 停点余量（0.2+0.1）→ 停点区间恒 ⊆ SlotRect
- 沿边中点恒在 walkable bounds 内 → 无 OOB、A* 恒可达
- 无 magic offset、不追 exact perimeter pixel、不 teleport、不依赖 LocalDirection inset

修复后矩阵：**10/10 全绿**（approach∈Slot ∧ predicate ∧ walkable ∧ A*5起点 ∧ 最坏停点∈Slot，edgeDist=0.625）。

## 2. B6.3A — Departure Route / Exit Authority Consistency

### 现象
WorldMap 中战略路线前缀"先向左上 / Site 内部绕行，再转向右下目标"，与 Local 实际出口方向视觉上不一致。

### 根因（绘制层分裂，数据层从未分裂）
- WorldMap route 渲染 `RefreshPlayerPartyPathPreview` 起点 = `motion.CurrentHex`
- **AtWorldSite 期间 CurrentHex 冻结为进入时值（presenceHex）**——B4 sync（`TryUpdateWorldPositionWithinSite`）只写 WorldPosition 不写 CurrentHex
- route 数据起点（`TryBuildPathLeavingSite` 的 startHex）= `PlayerPartyWorldLocationQuery.TryResolve` 的 DerivedHex（Canonical 派生）
- → 绘制层起点（presence 冻结值）≠ 数据层起点（DerivedHex）→ 第一段从 presence 画到真实位置 = 视觉"内部异常绕行前缀"

### 验证
harness 72 项检查证实数据层一致：`Route first outside hex == SiteDepartureExitHex == FormalConnection.DestinationHex`、`SiteDepartureFootprintHex == Connection.SourceHex`、`startHex == DerivedHex == path[0]`；`Canonical→WorldToHex` 全部 (in) footprint。Presence 修复前间接参与 route 起点；Anchor 全程不参与。

### 修复
- `PlayerPartyWorldLocationQuery.TryResolveRouteStartHex`（+52）：AtWorldSite + departure + valid Canonical → `WorldToHex(Canonical)`，跳过重复 path[0]；非 departure 恒返回 CurrentHex
- `HostWorldMapPanel.RefreshPlayerPartyPathPreview` 消费该 helper（+13/−6）→ route 起点唯一 authority = Canonical 派生

20% crossing failure 与 route 前缀**不相关**（B6.3 修的是 approach 几何，B6.3A 修的是 renderer 绘制起点，两个独立问题）。

## 3. B6.4 — Multi-Hex WorldSite Route Endpoint Authority

### 问题
多格 WorldSite 的战略目标语义：玩家点击整个 Site，planner 不得把 Site 压缩成 AnchorHex / PresenceHex / 单一 footprint hex。

### 审计结论
- **去 Site**：`ResolveDeterministicSiteApproachHex` 枚举整个 footprint（非单 hex），但选择准则原为 `HexMath.Distance`（hex 直线距离）→ 真实地形下**4 处真次优**（site_daoguan 选 A*cost=57 而最优 56；site_b 选 70 而最优 67）
- **离 Site**：`BackgroundCharacterSiteDepartureResolver` 枚举全 perimeter（`CollectTraversableOutsideNeighbors` + A* 最短）✓，10/10 `TryResolveDepartureFootprintHex == connection.SourceHex` ✓
- **clicked hex**：`WorldMapPartyTravelCommand.TryResolve` 只把 clicked hex 归化为 `TargetSiteId`，不强制 destination ✓
- **Anchor/Presence**：Anchor 不参与；`WorldMapPartyTravelCommand.ResolveDeterministicApproachHex` 的 from 曾用 `CurrentHex`（presence 冻结）→ **已改 DerivedHex**

### 修复
- `ResolveDeterministicSiteApproachHex`（HexTravelService）：选择准则改为 **A* 实际路径代价最小**（tie 取 hex 距离近者）；blocked（`BuildNonDestinationSiteBlockedHexes` + 出发 Site 豁免）提前到 goal 解析前统一构建
- `WorldMapPartyTravelCommand`：from 改 `PlayerPartyWorldLocationQuery.TryResolve` 的 DerivedHex
- 同款 hex 距离复制实现仍在 `BackgroundCharacterTravelService:750` 与 `FormalArmyContinuousTravelService:492`——记录为后续统一处理（非 PlayerParty 本轮范围）

## 4. B6.5 — Travel Execution & Time Authority Unification

### 子问题 A：Multi-Hex WorldSite Egress Continuation

#### 现象
多格 Site 内（尤其 Canonical 位于两个 OccupiedHex 内部交界附近）出发：LocalDeparture → crossing 成功 → 出了 Site 就停下，不再沿原 Route 继续。

#### 根因（双重）
1. `TryCrossWorldSiteEdgePreservingLocalVisibleAutoTravel` 用 `SetSegment(SegmentIndex+1, 0)` 推进 route，但 **LocalVisible departure 全程 SegmentIndex 恒 0**（`SyncSegmentProgressFromWorldPosition` 只写 progress 不推进段）。当 `TryBuildPathLeavingSite` 的 HexPath 前部含多个 footprint hex（seam/非出口侧出发）时，+1 后段仍指向 footprint 内部段 → `TryResolveWildernessExitConnection` 匹配不到 → `NoExit` → 停在出口后。
2. `SetWorldPositionInternal(boundary, WorldToHex(boundary))`：BoundaryContact 恰在 hex perimeter（共享边中点），multi-hex seam/corner 时 WorldToHex 天然 tie 回 footprint 格 → `CurrentHex` ≠ 已提交的 `connection.DestinationHex`。

#### 修复
- 新增 `AlignRouteProgressAfterSiteEgress(motion, committedOutsideHex)`（Core，LocalVisible crossing 与 World commit 共用）：在 HexPath 定位 `FormalConnection.DestinationHex`（first outside hex）→ `SetSegment(idx, 0)` 对齐到以其为起点的下一段；找不到才退回旧 +1。**exactly once、不重复、不跳过、不依赖 WorldToHex tie-break**。
- crossing 处 `SetWorldPositionInternal(boundary, connection.DestinationHex)`（**Canonical=boundary / route hex=DestinationHex 分离**）
- 另修段内推进：`AdvanceDistanceBudget` 段中分支的 derived 由 `WorldToHex(pos)` 改为 `motion.CurrentHex`（段内保持段起点 route truth，消除 boundary 点被 WorldToHex 拉走的 tie 类问题；跨段在段完成分支用 toPos=hex center 无 tie）

### 子问题 B：World Pause / Travel Executor Authority

#### 问题
AtWorldSite 开 WorldMap（显示 Running）下达 World Travel → Character 不动，只有关图才 LocalDepartureApproach；Wilderness 则立刻推进。行为不一致。

#### 正式拍板规则
1. LocalMap → 打开 WorldMap：一次性 `ManualPaused = true`
2. WorldMap 打开后 Space / Pause-UI 自由切换
3. LocalMap 中同样可切换
4. 任何 Travel Order（PlayerParty / FormalArmy）**绝不修改暂停状态**
5. Paused：Order 可创建，movement/simulation progress = 0
6. Running：Order 创建后下一 simulation tick 立即执行
7. WorldMap → LocalMap：**不修改 ManualPaused**（继承关闭前状态）
8. 再次打开 WorldMap：再次强制 `ManualPaused=true`

#### 实现
- `PlayableHostSession`：`ManualPaused` + `ModalHardPaused`（Push/Pop 计数），`IsPaused => ManualPaused || ModalHardPaused`；setter 兼容写 `ManualPaused`（现有 modal `_holdingPause` + `IsPaused=` 代码不重写仍工作）
- bootstrap Space 切换加 `!ModalHardPaused` 检查 → Modal 期间 Space 不能解除；Pop 后恢复底层 ManualPaused（**未重写任何 Popup**）
- `HostWorldMapPanel.Open()`：一次性 edge 设 `Session.ManualPaused = true`
- **AtWorldSite World executor**：`AdvanceWorldSiteDepartureCanonical`（Core）——WorldMap open + Running（ExecutionMode=World，`ResumeWorldTravelExecutionIfNeeded` 已删 departure 特例 → reopen 切 World）→ Canonical 沿直线朝正式 `SiteDepartureBoundaryEntry` 消耗 distance budget 推进（复用 `TryUpdateWorldPositionWithinSite`，AtWorldSite context 保留）；到达后 `CommitSiteDepartureBoundaryCrossing`（AtWorldPosition + CurrentHex=exitHex + Align），剩余预算递归推进后续 AtWorldPosition 段。不使用 SiteDepartureVirtualPosition、不 teleport hex center、不新增第二套 route。
- 全仓审计：Core Travel 方法零 Pause 副作用（`PlayerPartyHexTravelService` / `PlayerPartyLocalVisibleAutoTravelService` / `WorldMapPartyTravelCommand` / `ArmyHexTravelService` / `FormalArmyContinuousTravelService` 均无 `IsPaused`/`timeScale` 写入）；仅 Host 战术 LocalMap 手动指令（`ResumeTime`/`ResumeSession`/`IssueFocus`/`Resume`）有 `IsPaused=false`，属"玩家手动下令恢复运行"的既有 RPG 输入语义，保留。
- close mid-departure handoff：`CloseWorldMapTakeover`（ExecutionMode→LocalVisible + `CanonicalizeTakeoverHexToActiveSegment`）未改，继续同一 FormalConnection departure，不重新规划、不回 StartLocation。

## 5. 修改文件（本批次 B6.3/B6.3A/B6.4/B6.5，未提交）

```
M  Assets/Scripts/Core/World/Strategic/PlayerPartyHexTravelService.cs
M  Assets/Scripts/Core/World/Strategic/PlayerPartyLocalVisibleAutoTravelService.cs
M  Assets/Scripts/Core/World/Strategic/PlayerPartyWorldLocationQuery.cs
M  Assets/Scripts/Core/World/Strategic/WorldMapPartyTravelCommand.cs
M  Assets/Scripts/Unity/Host/HostWorldMapPanel.cs
M  Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs
M  Assets/Scripts/Unity/Host/PlayableHostSession.cs
M  Assets/Tests/EditMode/WorldSiteDepartureTests.cs（B6_04/B6_15 更新为新语义）
A  Assets/Tests/EditMode/WorldSiteSurfaceExitReliabilityTests.cs(+.meta)   B6_3_01~06
A  Assets/Tests/EditMode/WorldSiteDepartureRouteConsistencyTests.cs(+.meta) B6_3A_01~05
A  Assets/Tests/EditMode/WorldSiteMultiHexGoalAuthorityTests.cs(+.meta)   B6_4_01~05
A  Assets/Tests/EditMode/WorldSiteEgressContinuationTests.cs(+.meta)      B6_5_01~05
```

## 6. 测试结果

dotnet 反射实跑（真实 Core 全编译 + 真实荒村/ch01 数据 + 真实 A*）：

- **B6.5：TOTAL PASS=62 FAILURES=0**（B6_5 5 + B6.4 5 + B6.3 6 + B6.3A 5 + B6.2 6 + B5 12 + B6 16 + V2 7）
- B6.3/B6.3A/B6.4 各自批次回归：B6.3 31/31、B6.3A+B6.3+B6.2+B5 全绿、B6.4 34/34
- **Host 全链编译（真实 Unity dll + Core + Data + 全部 119 Unity 脚本）：0 错误**（2 个既有无关 warning：HostWorldMapPanel:740 不可达代码、HostFormalHud:123 未使用字段）
- 全仓 audit：唯一 party marker `DrawPlayerPartyMarker`（HostWorldMapPanel:3057）只读 `resolved.WorldPosition`（:3070-71），无第二套 marker authority

## 7. LevelTester 最小人工验收步骤

1. **Crossing 可靠性**：荒村 → 右键西南（(79,53)）与东北（(82,50)）目标 → 均应 crossing；连续 10+ 次不同方向 → 0 次卡在透明方块
2. **Route 前缀**：Site 内走到明显位置（不在入口）→ 右键右下目标 → 蓝线从当前 Canonical 位置开始
3. **去多格 Site**：右键东侧 site_b → 路线直奔可达侧（不再绕向次优 footprint 格）；点击 Site 内部任意格 → 目标语义相同
4. **Crossing 连续性**：内部 seam 附近站定 → 右键远处目标 → crossing 后继续沿原路线（不停在出口后第一格）
5. **WorldMap 内推进**：WorldMap open（自动暂停）→ 下达 Travel → 按 Space Resume → avatar 朝出口/路线移动；再暂停 → 静止
6. **Pause 语义**：open 自动暂停；close 保持；reopen 再暂停；下达 Travel/Army order 前后 Pause 值不变
7. **close mid-departure**：WorldMap 内 Resume 走到一半 → close → 角色从当前 Canonical 继续同一出口
8. **回归**：egress 后 one-shot recenter、普通 WorldMap open/close 自由镜头、ingress/SafeLanding、marker 跟随 Canonical

## 8. Known Issue / 待办

- **WorldSite SurfaceExit edge-case**（记录，未处理）：极少数 WorldSite 周边"进入透明出口未 crossing"，归入后续 Surface Navigation / WorldSite Context Unification 统一处理
- `BackgroundCharacterTravelService:750` / `FormalArmyContinuousTravelService:492` 的 hex 距离版 `ResolveDeterministicSiteApproachHex` 复制实现（同类问题族，非 PlayerParty 范围）
- 0.45 legacy fallback 清理（`PlayerPartyHexTravelService` PresenceHex legacy 注释标注随 5R-D 删除）
- LocalVisible AutoTravel stall（B4 已确保同一 sync authority，未根治）
- `site=huangcun / hex=(4,7)` 与 footprint 归属一致性
- B3B/B3B.1/B3B.2 合并封板、5D-B1 验收

## 9. 关键源码位置索引

- `PlayerPartyHexTravelService.cs`：`AdvanceDistanceBudget`（段内 route hex 保持）、`AdvanceWorldSiteDepartureCanonical`、`AlignRouteProgressAfterSiteEgress`、`CommitSiteDepartureBoundaryCrossing`、`ResolveDeterministicSiteApproachHex`（A* 代价）
- `PlayerPartyLocalVisibleAutoTravelService.cs`：`ResolveWorldSiteExitApproachLocalPoint`（B6.3）、`TryCrossWorldSiteEdgePreservingLocalVisibleAutoTravel`（crossing: Canonical=boundary / route hex=DestinationHex / Align）
- `PlayerPartyWorldLocationQuery.cs`：`TryResolveRouteStartHex`（B6.3A，文件尾）
- `WorldMapPartyTravelCommand.cs`：from 改 DerivedHex（B6.4）
- `PlayableHostSession.cs`：`ManualPaused` / `ModalHardPaused` / `IsPaused` 合成（B6.5-B）
- `PlayableHostBootstrap.cs`：Space 切换 `!ModalHardPaused` + `ManualPaused`
- `HostWorldMapPanel.cs`：`Open()` 强制 `ManualPaused=true`、`RefreshPlayerPartyPathPreview`（route 起点）、`DrawPlayerPartyMarker` :3057
- `SurfaceExitZoneCalculator.cs`：`TryResolveFormalExitConnection`（真实 bounds）、`PointBelongsToConnection`（只比 SlotRect，容差 0.0001）
- 真实数据：base:site_huangcun footprint=4 hex (80,51)(81,51)(80,52)(81,52) presence=(80,52)；huangcun_01 bounds [-40,40]×[-25,25]（80×50）exitTriggerDepth=1.25；map_ch01_reference bounds [-40,160]×[-25,75] cellSize=1
