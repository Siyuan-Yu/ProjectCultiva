# 近期开发对照与统一交接（2026-09-10～2026-09-14）

> 状态：当前仓库事实对照完成｜最后更新：2026-09-14
> 对照基线：`dev_openworld` / `1e48464`
> 性质：实现、Content、存档、表现层与制作人反馈的汇总索引；不替代系统正文、ADR 或各阶段详细交接
> 当前结论：Continuous Outdoor／统一 Squad／CharacterEncounter 已在制作人确认范围内通过；CW-04 已实现并进入制作人验收，CW-05 尚未开始

## 1. 为什么需要本页

近期工作横跨 Continuous Outdoor、NPC、AutoTravel、Outdoor 状态存档、统一 Squad、独立人物遭遇、残留人物空间权威和 CW-04 行政控制。详细证据已经分别写入 `208`～`229`，但部分早期页面仍保留当时的“未提交／待验收”措辞，无法单独代表当前状态。

本页以当前代码、Content、Git 历史、既有过程文档和制作人后续 Play 反馈重新对照，提供一个新的恢复入口。历史页面中的故障现场、根因和当轮验证记录仍保留；状态冲突时，以本页及更晚的 Devlog 为准。

## 2. 当前状态总表

| 工作块 | 当前实现事实 | 制作人状态 | 详细记录 |
|---|---|---|---|
| Continuous Outdoor 主链 | 普通 Outdoor 采用同一 Continuous Surface；WorldPosition、Chunk streaming、WorldSite 连续表现已接线 | 当前观察范围通过 | [208](208-continuous-world-outdoor-worldsite-surface-migration-v1-2026-09-10.md)、[213](213-continuous-outdoor-world-handoff-2026-09-11.md) |
| Opening authored spawn | Opening NPC、同伴与地点使用 checked-in authored anchor／稳定 spawn identity；已存在 View 会对齐权威落点 | **PASS** | [209](209-continuous-outdoor-opening-population-bootstrap-2026-09-10.md)～[212](212-continuous-outdoor-opening-placement-and-party-copresence-2026-09-11.md) |
| NPC Schedule realtime movement | Schedule 使用 Continuous SitePlace、CompositeWalkGrid 与真实到达提交；不可达只诊断，不伪造到达 | **PASS** | [212](212-continuous-outdoor-opening-placement-and-party-copresence-2026-09-11.md)、Devlog 2026-09-11 |
| PlayerParty Follow | Continuous 同屏以 Surface presentation scope 判断，不再要求 Legacy LocalMap；停止跟随后保留个人精确位置 | **PASS** | [212](212-continuous-outdoor-opening-placement-and-party-copresence-2026-09-11.md) |
| Chunk streaming seam | staged build／commit／retire，逻辑邻域与临时表现 cache 分离；WalkGrid lattice tolerance 只吸收浮点噪声 | 制作人当前表现可接受，停止继续优化 | [214](214-continuous-outdoor-streaming-seam-v1-2026-09-11.md) |
| Surface Geography 与 AutoTravel | 实际旅行读取 Surface navigation，水域、桥与 solid 仍由同一物理路线约束；不会为预览重写第二套寻路 | 实际避水走桥已确认 | [215](215-surface-geography-w2a-handoff-2026-09-11.md) |
| WorldMap Planning-Only／路线预览 | 开图只冻结本地执行、保留目的地；关闭后继续出发。存在完整 Surface route 时画实际路线；数据不足时明确显示“目的地已设定｜暂无完整地形路线预览”，不画穿河假直线 | 兼容状态已通过 | 本页 §3.3；`HostWorldMapPanel` 当前实现 |
| Outdoor stateful objects | destructible／farm state 参与表现与 WalkGrid；`outdoorDestructibles`、`outdoorFarmPlots` 已完成 Capture→JSON→Deserialize→Restore，旧存档缺字段为空集合 | 实现闭环；未扩大成人工全回归结论 | [217](217-cw-01-outdoor-stateful-object-closure-2026-09-12.md) |
| 暂停与失能安全出口 | ManualPaused 与 modal owner 分离；战斗／报告／读档各自释放 owner；全员暂时失能与真正死亡分开 | CW-02 当前交付范围通过 | [218](218-cw-02-pause-ownership-and-party-incapacitation-safety-exit-2026-09-12.md) |
| 统一 Squad | `SquadState` 是人物组织与成员顺序真源；PlayerParty／旧 FormalArmy 读取同一成员权威，旧档可迁移 | 主线当前观察范围通过 | [220](220-cw-u0-design-and-manual-entry-placement-2026-09-13.md)～[222](222-cw-u1-u4-continuous-implementation-2026-09-13.md) |
| CharacterEncounter 独立场 | 同源地形、固定初始双方、有限介入、战报、输入归属、失败回滚与恢复链已接线；ReadyToEnd／VictoryAvailable 可收尾 | 主线及后续生命周期修复当前观察范围通过 | [222](222-cw-u1-u4-continuous-implementation-2026-09-13.md)、Devlog 2026-09-14 |
| residual spatial authority | 首次弥留冻结个人位置；弥留→尸体不再重锚；AtSite／AtWorldPosition／AtHex／Encounter authority 与 Surface provenance 可恢复 | 当前观察范围通过 | [222](222-cw-u1-u4-continuous-implementation-2026-09-13.md)、Devlog 2026-09-14 |
| 玩家层 Legacy Army 退役 | WorldMap 和角色列表不再提供玩家组军、军团移动或人物 Army 攻击入口；旧 Domain／Content／Save／NPC 运动仅作兼容 | CW-U4.2 已验收 | [222](222-cw-u1-u4-continuous-implementation-2026-09-13.md) |
| CW-04 实际行政控制 | `TerritoryClaim` 保存取得顺序；理论范围可重叠，实际管理按最早有效 Claim；WorldMap 直接画实际边界 | 已实现，待制作人验收 | [223](223-cw-04-territory-claim-administrative-control-2026-09-14.md)～[226](226-cw-04-scale-preset-core-coverage-2026-09-14.md) |
| 预设／玩家势力旗 SiteCore | 正式旗具有精确 Surface Core、稳定 Site 与 Claim；Preview／提交共用放置预检；TEST Player Camp 已退役并有旧档定向迁移 | 已实现，待制作人验收 | [227](227-cw-04-existing-flag-core-content-migration-2026-09-14.md)～[229](229-cw-04-flag-placement-player-camp-retirement-2026-09-14.md) |

## 3. 已对照的关键闭环

### 3.1 Outdoor 动态物件 JSON persistence

当前正式链路为：

```text
OutdoorStatefulObjects Runtime
→ SnapshotService Capture
→ WorldSnapshot
→ JsonSnapshotSerializer
   ├─ outdoorDestructibles { stableId, currentHp, destroyed }
   └─ outdoorFarmPlots { stableCellId, cropId, cropStage, growth }
→ JsonSnapshotSerializer Deserialize
→ WorldSnapshot
→ SnapshotService Restore
→ OutdoorStatefulObjects Runtime
```

两个 root 字段均为 additive optional field。旧 v6 存档缺字段时保留空集合，不因缺字段报 `SnapshotInvalid`，也没有仅为这两个字段提升 schema version。稳定 identity 继续是 `StableId`／`StableCellId`。

### 3.2 Continuous 空间权威分层

| 权威 | 回答的问题 | 明确不负责 |
|---|---|---|
| `OutdoorSurfaceSpatialAuthority` | Surface identity、origin、cell/chunk metric、完整 authored coverage membership | 河桥寻路、动态阻挡 |
| `SurfaceGroundNavigation` | checked-in geography 覆盖内的水、桥、道路、solid 与路径 | 代表整个 Surface 的行政 membership |
| Host `CompositeWalkGrid` | 当前 loaded chunks 的真实落点／占地是否合法 | 全世界行政归属 |
| `TerritoryClaim` + resolver | 理论范围重叠后，精确 WorldPosition 由哪个 Site 实际管理 | 物理可走性、路线规划 |
| Strategic Hex projection | 战略摘要、兼容查询与诊断 | WorldMap 实际行政边界真源 |

这组分层解释了两类近期根因：局部 `SurfaceGroundNavigation` 不能充当完整 Surface coverage；Strategic Hex 摘要也不能充当连续世界的精确行政边界。

### 3.3 PlayerParty 大地图规划与路线显示

当前 WorldMap 是 PlayerParty 的 planning overlay：

```text
打开 WorldMap
→ 保留当前目的地和旅行状态
→ Hold LocalVisible execution
→ 选择／重选目的地只更新 PlayerParty 规划
→ 关闭 WorldMap
→ rearm 同一条 LocalVisible travel
```

路线预览读取 `PlayerPartyWorldMotion` 中供实际旅行使用的 Continuous Surface route。若当前只有目标而没有完整物理路线数据，则只画目标状态并显示兼容提示，不用旧 HexPath 或起终点直线冒充可通行路线。因此实际 AutoTravel 的过桥／避水规则保持不变，打开地图期间 Party 也不会在背景移动。

### 3.4 Squad、Encounter 与人物残留位置

统一后的职责链为：

```text
SquadState（成员／顺序／队长／共享命令）
→ CharacterEncounter（真实人物双方／候选介入／阶段）
→ Independent Field（同源导航与表现）
→ LifeState commit + ManualBattleReport
→ ResidualSpatialAuthority（人物自己的返回／弥留／尸体位置）
```

非存活 Squad 成员仍可保留组织身份，但不会进入新的遭遇 roster，也不会令整场准备因 `Necessary squad member unavailable` 失败。首次弥留可以冻结本人位置；之后确认死亡只改变生命周期，不重新用 Party、Army、Site 或 Legacy LocalMap 锚点覆盖已有个人空间权威。

### 3.5 CW-04 实际控制与旗核心

当前 SiteCore 尺度以 Surface cells author：Level 1 为 `150×150 cells`；Main Surface `cellSize=0.028`，解析为 `4.2×4.2 world`。Wilderness Encounter 单独保持 `500×500 cells`，不混用两种用途。

正式控制链为：

```text
WorldSite + CoreAsset
→ TerritoryClaim（AcquiredOrder 历史）
→ 精确 WorldPosition resolver
→ Actual manager
→ WorldMap world-space overlay
→ Strategic Hex compatibility projection
```

玩家插旗先由 Host 用当前 `CompositeWalkGrid`、4×4 footprint、loaded area、Surface membership 和操作距离预检，再由 Core 校验精确同点与其它势力实际控制；同势力理论范围重叠允许。旧 `test:site_player_camp`／`test:region_player_camp` 已从正常 Content 退役，`base:flag_player_origin` 使用原 canonical 到达坐标建立正式玩家起始 SiteCore；人物 opening／snapshot 位置不随旗迁移。

## 4. 兼容层与明确未完成项

- `FormalArmy`／`ArmyStack`、旧 BattleOffer、旧 Hex support、旧 `type=formalArmy` Content、siege 和旧会话恢复仍是兼容／后台路径；它们不是当前玩家人物移动与攻击产品入口。
- `base:map_player_camp`／`base:places_player_camp` 只保留给旧档或显式 fixture；正常 HexWorld、Surface 与 scenario 不再引用 TEST Player Camp。
- W2A checked-in geography 只覆盖其 authored 范围；完整 Surface 内没有路线数据时，WorldMap 必须显示“不具备完整地形路线预览”，不能生成假路线。
- CW-04 当前是 **Implementation Completed / Producer Acceptance Pending**。不能因为离线编译和静态探针通过就写成制作人验收通过。
- CW-05 未开始；本次文档整理没有新增任何 CW-05 代码或规则。
- 当前仓库根下的 `Assets/Scripts.zip` 是未跟踪本地审查包，不属于当前产品源码，也不纳入提交。

## 5. Git 与验证事实

- 2026-09-10～2026-09-14 的连续实现已形成线性 Git 历史；当前代码／Content 汇总 checkpoint 为 `1e48464`。
- `1e48464` 已包含 CW-04 `223`～`229` 对应实现和文档；这些页面里早先的“修改未提交”只代表写作当时，不能继续当当前状态。
- 最近一次实现交付记录为 Core／Data／Unity Host offline compile **0 error**，共 **8 个既有 warning**；未运行 Unity、PlayMode、TestRunner、batchmode 或 Bake。
- 本次整理只修改文档；提交前执行文档链接／状态静态检查与 `git diff --check`，不借文档任务重跑大型测试。

## 6. 下一步恢复顺序

1. 先读本页掌握当前全局状态。
2. 处理 Continuous／Opening／Schedule 时读 [213](213-continuous-outdoor-world-handoff-2026-09-11.md) 及 [214](214-continuous-outdoor-streaming-seam-v1-2026-09-11.md)。
3. 处理 Squad／人物遭遇／残留位置时读 [222](222-cw-u1-u4-continuous-implementation-2026-09-13.md) 与 [ADR-0035](43-decisions/ADR-0035-unified-squads-and-encounter-scope.md)。
4. 处理行政控制／旗／WorldMap Overlay 时按 [223](223-cw-04-territory-claim-administrative-control-2026-09-14.md) → [229](229-cw-04-flag-placement-player-camp-retirement-2026-09-14.md) 顺序阅读。
5. 等制作人完成 CW-04 Play 验收；通过后再更新状态并决定是否授权 CW-05。
