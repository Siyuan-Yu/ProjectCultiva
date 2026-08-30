# Phase 5R-B7A — WorldSite Surface Passability Unification

- Date: 2026-08-30
- Baseline: `dev @ 8f7e673`
- Status: **实现完成，待 LevelTester 人工验收；未提交**
- Scope: PlayerParty Surface Travel；不改 FormalArmy／Battle／SupportRing／外交／Gate system

## 1. 截图现象静态 A/B 结论

截图对应的 `travel_mvp_hex_world_30x15` 青石荒村类场景，取 Canonical／Derived start
`(3,7)`、右下 destination `(6,9)`：

| 规则 | HexPath | total cost |
|---|---|---:|
| A：非目标 Site blocked | `(3,7)→(4,7)→(5,7)→(6,8)→(6,9)` | 4.4 |
| B：Site 按普通 Surface passability | `(3,7)→(4,7)→(5,7)→(6,8)→(6,9)` | 4.4 |

- blocked footprint（已豁免出发荒村）：`(10,7)(11,7)(10,8)(11,8)(28,7)(12,11)(22,6)(7,5)(16,4)(17,4)(16,5)(17,5)(4,6)`
- MandatoryTransit probe：`false`
- 因此截图的可见绕向 **不是** non-target Site blocking，也没有触发 MandatoryTransit。

真实分叉：`BeginTravel` 的 Site departure route 会把“Canonical start → departure footprint →
outside exit”的战略 hex-center 前缀拼进 `HexPath`；但 B6.5 World executor 与 LocalVisible
executor 都从当前 Canonical 直接走正式 `BoundaryContact`，不会逐格执行该 footprint 内前缀。
旧 Preview 却完整绘制前缀，故产生“蓝线先绕、实际角色直走”的权威分裂。

## 2. B7A 正式规则

- PlayerParty 普通战略寻路不再构造 non-target WorldSite blocked set。
- WorldSite footprint 是否可通过只读 HexWorld terrain / `IsPassable` / movement cost。
- WorldSite 的 SiteId、footprint、LocalMap、Owner、selection、ingress/egress、battle context 均保留；Site 仍是 Context overlay。
- 目标 Site 仍为 `DestinationSiteId + whole-footprint goal-set`，planner 按 A* 实际代价选择入口。
- 非目标 Site 可成为同一条 HexPath 的中间 Surface；进入/离开不改 Destination、不 CompleteMove。

## 3. 收敛内容

- `PlayerPartyHexTravelService.BeginTravel`：删除 non-target footprint blocked 与 NoRoute→MandatoryTransit gateway leg 两条 PlayerParty 分支。
- `WorldSiteTransitPolicy`：保留 FormalArmy 仍在使用的 legacy blocked builder；删除已无调用者的 Dynamic MandatoryTransit probe。
- `HostWorldMapPanel`：删除 BeginTravel 失败后弹 Mandatory Gateway confirm 的调用入口（旧 UI 壳不再可打开）。
- `PlayerPartyWorldMotion.PlanThroughSiteDeparture`：只建立 transient departure plan，保留同一 Path／Destination／ExecutionMode。
- `TryCommitThroughSitePassage`：从当前 HexPath 的连续 Site footprint 段解析最后 footprint hex + first outside hex，作为 World/LocalVisible 共用 egress authority。
- World executor：进入非目标 Site 后保留 AutoTravel；Canonical 直达正式 egress boundary，再对齐原 route 继续。
- LocalVisible：不再 `WorldSiteAhead(StandStill)`；非目标 Site ingress 保留 Travel，加载对应 Site LocalMap，随后走正式 departure 再回 Wilderness。
- `TryResolveRouteStartHex`：Site departure Preview 从正式 outside exit 开始绘制，跳过 executor 不执行的内部 hex-center 前缀。
- WorldMap close：若 Canonical Context 已在 through-Site，直接加载该 Site LocalMap，并以 Canonical WorldToLocal 接管，不回 Anchor／Presence。

## 4. 自动验证（未启动 Unity）

真实 Core 全源码通过临时 .NET harness 编译并执行：

- `B7A_01` non-target Site 可直接经过
- `B7A_02` Site overlay 有无不改变 path/cost
- `B7A_03` Anchor 不影响 through-route
- `B7A_04` Presence 不影响 through-route
- `B7A_05` target Site 保持 whole-footprint 最优入口
- `B7A_06` World executor 经过 Site 不 CompleteMove，最终到原目标
- `B7A_07` Preview 与 executor 共用正式 departure exit
- `B7A_08` LocalVisible through plan 与 HexPath 共用 egress
- `B7A_09` Water 仍不可通过
- `B7A_10` explicit `IsPassable=false` 仍不可通过

结果：**TOTAL PASS=10 FAIL=0**。另有 1 个既有无关 warning：
`StrategicEncounterResolveService.cs:543` local `slot` 未使用。

真实 Core + Data + 全部 Unity Runtime Host 源码的非 Unity 编译：**0 error**；14 个 warning
均来自既有不可达代码、未使用/未序列化字段及旧 nullable 判断，本批没有新增编译错误。

Unity 未运行；Host 运行时仍需 LevelTester 人工验收。

## 5. LevelTester 最小人工验收

1. 青石荒村内站到截图位置，WorldMap 右键右下同一目标：蓝线首段应从当前 Canonical 直接朝正式出口，不再画 Site 内部假前缀。
2. 选择一条自然穿过非目标 WorldSite 的目标：蓝线应经过 footprint，不弹 MandatoryTransit，不在 Site 前停住。
3. WorldMap Running：观察 Marker 进入非目标 Site 后继续离开，Destination 不变，最终到原目标。
4. Marker 位于非目标 Site footprint 内时关闭 WorldMap：应加载该 Site LocalMap，使用 Canonical WorldToLocal；角色继续向正式 egress。
5. LocalVisible 完整观察 `Wilderness→Site→Wilderness`：进入 Site 不 CompleteMove，走真实出口后继续原路线。
6. 点击 Site 本身：仍在 ingress 时结束 Travel，并停留该 Site，而非自动穿出。
7. 对 Water／不可通行 Mountain／`IsPassable=false` 目标做回归：仍应 NoRoute／不可通行。

## 6. 风险与边界

- 本轮未运行 Unity；Materialize／Safe Landing／SurfaceEdgeGate 的真实帧序验收仍是主要风险。
- FormalArmy 仍保留旧 non-target Site blocked policy，未纳入 B7A PlayerParty 范围。
- 旧 Gateway UI 字段/绘制壳仍在 Host，但已无任何打开入口；后续可作纯 cleanup，不影响路径权威。
