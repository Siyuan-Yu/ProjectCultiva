# Vertical Slice 0.4 Plan v0.1 — Unity Playable Host

> 状态：**已验收完成**｜最后更新：2026-08-01  
> 类型：垂直切片实施计划｜验收见 [61](61-vertical-slice-0.4-acceptance-report.md)  
> 前置：[VS0.3 验收](58-vertical-slice-0.3-acceptance-report.md) **已通过**  
> 依据：`32` Demo→正式桥接、`33` v0.2、`35`、ADR-0009（正式 UI 另案）、ADR-0022（Demo 冻结）  
> **不修改 Core Freeze 正文**。Demo Runtime **继续冻结：不扩玩法、不迁逻辑当真源**。  
> 实现：V4-A～H 均已独立 commit（见 [61](61-vertical-slice-0.4-acceptance-report.md)／[62](62-project-status-2026-08-01.md)）。

---

## 0. 目标

把 VS0.1～0.3 **已有纯逻辑玩法闭环**接入一个**最小可操作的 Unity 场景**，使玩家（或试玩者）能用手完成「一天」体验：

```text
加载 BaseGame → 创建 World／三人
  → 场景表现三角色
  → RTS 点选／框选
  → 右键或调试键 → PlayerCommandRequest → 既有 PlayerInputPort
  → Labor／Rest／Observe／Cultivate
  → 最小 HUD 读 Day／Hour／Action／Schedule／Quota／Risk／Realm
  → DomainEvent 调试反馈
  → Snapshot 存／读
```

**成功判据：** 不依赖 EditMode 测试 API，在 Host 场景内用鼠标／快捷键跑完与 VS0.3 §3 同构的一日循环（可加速 Tick）。

**非目标：** 产品级 RTS／正式 UI 框架／美术替换／地图玩法／把 Demo 逻辑搬进 Core。

### 0.1 桥接原则（强制）

对齐 `32`：

1. Demo 是**语义与手感参考**，不是正式实现源。  
2. Host 只做 **Unity 适配层**：输入 → `PlayerCommandRequest`；只读查询／事件 → 表现。  
3. **禁止** UI／MonoBehaviour 直接改 Entity 组件或属性。  
4. **禁止**把 `Assets/Scripts/Runtime/**` 玩法代码迁移／复制进 Core／Data／新 Host 当逻辑真源。  
5. 新场景与新脚本落在 `XianXia.Unity`（及专用 Scene），**不改**既有 Demo 场景玩法。  
6. 过程纪律（VS0.3 §7）：**每 Phase 单独实现 → EditMode／PlayMode 门禁 → 单独 commit → 停等确认**。

---

## 1. 现有能力（Host 只接线，不重做）

| 层 | 已有 | Host 用法 |
|---|---|---|
| Data | `ContentPackageLoader`、`DefinitionRegistry`、`sites.json` 等 | 启动时加载 `Content/BaseGame` |
| Bootstrap | `ContentGameStart`／`GameStartBootstrap` | 扩展或并列「PlayableDayStart」装配 Schedule／Site／Risk／Quota 钩子（见 §2） |
| Core Input | `IPlayerInputPort`／`PlayerCommandRequest`／`PlayerCommandKind`（Labor／Rest／Observe／Cultivate） | 唯一下令入口 |
| Core Sim | `SimulationWorld`／`SimulationLoop`／`ScheduleDriver`／DayClock／Gate／Cultivate／QuotaConsequence | Host 每帧或定时 `Tick` |
| Core 读模型 | Entity 组件、Events、`SnapshotService` | HUD／存档 |
| Unity | `XianXia.Unity` asmdef＋`UnityHostMarker` 占位 | 本切片填实 |

---

## 2. Unity Host 如何加载 Content 与创建 World

### 2.1 启动管线

```text
PlayableHostBootstrap (Unity)
  → 解析 Content 根路径（StreamingAssets 拷贝或 Editor 下绝对／相对 Content/BaseGame）
  → ContentPackageLoader.Load
  → 将 Cultivation／OpportunitySite 等 Map 并 Register* 进 World（对齐 VS0.3 测试装配）
  → 生成三角色（复用 ContentGameStart 角色 ID）
  → 绑 ScheduleDefinition、DailyTask、PersonalConcealmentRisk、DayBoundary／QuotaConsequenceHandler
  → 持有 SimulationLoop + PlayerInputPort
  → 进入可玩状态（Paused 或 1x）
```

### 2.2 建议交付

| 类型 | 职责 |
|---|---|
| `PlayableHostSession` | 持有 World／Loop／Port／Registry；生命周期 Clear／Rebuild |
| `PlayableDayBootstrap`（Data 或 Unity 编排） | 「可玩日」一键装配；避免把装配逻辑散落在多个 MonoBehaviour |
| Content 路径策略 | Editor：仓库 `Content/BaseGame`；Player：约定 StreamingAssets 同步（本切片可先 Editor-only） |

### 2.3 Core／Data 是否改动

| 允许（薄） | 禁止 |
|---|---|
| Data：抽取／扩展「VS0.3 可玩日装配」API，供 Host 与测试共用 | 为 Host 在 Core 引入 `UnityEngine` |
| Core：仅当缺只读查询 API 时加**无行为**访问器（优先用现有组件） | 改 Freeze；为 UI 特化规则 |
| 缺路径工具时的 Editor 小工具 | 改 `ProjectSettings`／`Packages`（未经批） |

---

## 3. 场景中如何表现三个角色实体

### 3.1 原则

- **无地图系统／无寻路**：场景可为空白地面＋三枚胶囊／简单 Sprite。  
- 每个 Core `EntityId` ↔ 一个 `EntityView`（MonoBehaviour）。  
- 世界坐标仅为**表现槽位**（固定偏移或简单环形排布），不表示 LocalMap 格子真源。  
- View 每帧／每 Tick 后从 World **只读**同步：选中高亮、头顶简短状态（可选）。

### 3.2 最小表现

| 项 | VS0.4 |
|---|---|
| Prefab | `EntityView`：Collider（点选）＋可选 Text／颜色区分三角色 |
| 生成 | Bootstrap 后按 Entity 列表 Instantiate |
| Focus | 可用边框色标记 FocusCharacter（若已有）；否则「当前主选中」即可 |
| 移动动画 | **不做**寻路走动；下令后不播位移（或极简「忙碌」色变） |

---

## 4. RTS 点选／框选（最小）

### 4.1 语义（对齐 Demo 已验证、`32`）

- 左键点选单个可控角色。  
- 左键拖拽框选多个。  
- Shift＋点选：加选／减选（建议做；可砍到「框选覆盖」）。  
- 选中集合＝后续下令目标（可对多实体各发一条相同 `PlayerCommandKind`）。

### 4.2 实现边界

| 做 | 不做 |
|---|---|
| 屏幕空间矩形 vs Collider／屏幕投影点 | 导航网格、地面点击移动 |
| 选中 Outline／颜色 | 完整 RTS 多选指挥条 UI |
| 只选 Host 生成的可控三人 | Demo 村民群体、守卫点选 |

**只读参考 Demo：** `PartyCommandController`／`CameraController` 的输入手感与射线思路——**重写到 `XianXia.Unity`**，不引用 Runtime 程序集玩法类。

---

## 5. 右键／调试命令 → PlayerCommandRequest

### 5.1 管道（强制）

```text
Unity Input（右键菜单键／数字键／IMGUI 按钮）
  → HostCommandAdapter
  → new PlayerCommandRequest(entityId, kind, durationTicks)
  → IPlayerInputPort.Submit（或现有 Port API 名）
  → Order → Action（Core）
```

### 5.2 建议键位（可调）

| 输入 | 命令 |
|---|---|
| 1 或 IMGUI「劳动」 | `Labor` |
| 2／「休息」 | `Rest` |
| 3／「观察」 | `Observe` |
| 4／「修炼」 | `Cultivate` |
| 右键（可选） | 弹出极简命令列表（**非**正式 UI 框架；IMGUI／临时） |
| Space | 暂停／继续 Tick |
| + / - 或 [ ] | 倍速 1x／2x／5x（只调 Host 推进频率，不改 Tick 语义） |
| . 或 N | 单步 1 Tick（调试） |

`durationTicks`：用常量或按 Kind 表（与 EditMode 测试对齐）；不在 Host 算玩法公式。

### 5.3 多选下令

对选中集合 **逐个** `PlayerCommandRequest`；任一失败用事件／屏幕日志提示，不中断整批（策略写清）。

---

## 6. 支持的最小命令

| Kind | 已有 Core | Host 验收 |
|---|---|---|
| Labor | ✅ | 可打断 Schedule；推进 Quota |
| Rest | ✅ | 可下令休息 |
| Observe | ✅ | 可发现 Site（可调高发现率便于试玩） |
| Cultivate | ✅＋Gate | 无 Site 时拒绝有反馈；有 Site 后 Progress／Risk 变化 |

不做：Move、战斗、对话、建造。

---

## 7. 最小 HUD（调试级，非正式 UI）

技术：优先 **IMGUI** 或单 Canvas 文本（ADR-0009 正式 UI **不在本切片**）。

| 字段 | 数据源（只读） |
|---|---|
| Day／Hour | `DayClock` ← `WorldTick` |
| 当前 Action | ActiveAction 类型名／剩余 Clock |
| Schedule | 当前 `ScheduleBlock` Activity 或「无绑定」 |
| Labor Quota | `DailyTask` Required／Completed／Deviation |
| PersonalConcealmentRisk | 组件 0–100 |
| Realm | `CultivationComponent.Realm`（及可选 Progress） |

另建议只读：选中 Entity 名／Id、PendingReprimand、KnownSites 数量、暂停／倍速状态。

**禁止：** 用 HUD 按钮直接改 Risk／Realm／Quota 数值（存档调试除外且须走 Snapshot／Port）。

---

## 8. DomainEvent → 调试反馈

### 8.1 订阅方式

Host 在每 Tick 后（或 Loop 暴露的 drain）读取 `DomainEventQueue` 增量，映射为：

- 屏幕滚动日志（最近 N 条）  
- 可选：短暂飘字（纯表现，可砍）

### 8.2 优先展示的事件

| Event | 玩家可感 |
|---|---|
| `DayStarted`／`DayEnded` | 日界 |
| `ScheduleInterrupted`／`QuotaDeviationCreated` | Override 代价 |
| `ObservationResolved`／`OpportunitySiteDiscovered` | 观察／发现 |
| `OrderRejected`／`ActionFailed` | 下令失败（如无 Site 修炼） |
| `QuotaConsequenceApplied` | 日终后果 |
| `ActionCompleted`（Cultivate／Labor） | 行动结束 |
| `Breakthrough` | 若碰巧发生则显示；**不强制** |

不在 Host 内实现新玩法规则。

---

## 9. Snapshot 保存／读取

| 项 | VS0.4 |
|---|---|
| API | 既有 `SnapshotService`＋JSON 序列化 |
| UI | IMGUI「Save」「Load」或快捷键 F5／F9 |
| 路径 | `Application.persistentDataPath` 下固定文件名（如 `vs04_slot0.json`） |
| 验收 | 玩中途存盘 → 改状态或重进 → 读档后 Day／Action／Quota／Risk／KnownSites 一致 |
| 注意 | Load 后重建 EntityView 绑定；再挂 DayBoundary／Quota Handler（对齐 VS0.3 观察） |

---

## 10. Demo v0.1：可参考 vs 禁止迁移

### 10.1 只读参考（允许看、禁止 copy-paste 当真源）

| Demo 区域 | 参考什么 |
|---|---|
| `PartyCommandController` | 点选／框选／多选手感 |
| `CameraController` | 平移／缩放（Host 可极简复刻） |
| `DemoPrototypeHud` | IMGUI 信息密度（字段改读 Core） |
| `UnitActivityOverhead`／飘字 | 事件反馈呈现思路 |
| `ReplaceableSprite` | 「占位美术」约定（可选） |
| `32` 映射表 | 语义对应关系 |

### 10.2 禁止迁移／接入 Host 当逻辑

| Demo 区域 | 原因 |
|---|---|
| `GameClock`／`ScheduleService`／`HourlySchedule` | 已由 Core `WorldTick`／`ScheduleDriver` 取代 |
| `CharacterActionController`／`CharacterAction` | 已由 Order／Action 取代 |
| `WorkSystem`／`WorkSpot`／`WorkZone` | 无地图工位；Labor 为抽象 Action |
| `CultivationSystem`／`UnitCultivation` | 已由 CultivateAction／Gate／Risk 取代 |
| `DailyTaskSystem`（Runtime） | 已由 `DailyTaskComponent`＋QuotaConsequence 取代 |
| `SupervisorAngerSystem` | VS0.3 明确不做主管系统 |
| `SpiritSiteZone`＋寻路进入 | Site 为抽象 KnownSites，禁地图触发 |
| `VillageCrowdPresenter`／Ambient NPC 玩法 | 超出三人 Host |
| Demo 场景资源当正式真源 | 场景可开新 `PlayableHost` Scene，不改 Demo 关卡逻辑 |

### 10.3 程序集纪律

- Host **不** `asmdef` 引用 `Runtime`／Demo 玩法程序集（若 Runtime 无 asmdef，仍禁止 `using` 其命名空间）。  
- 允许引用：`XianXia.Core`、`XianXia.Data`、Unity 引擎模块。

---

## 11. 明确不做

- 正式美术替换与动画管线  
- 完整地图系统／LocalMap 玩法  
- 寻路／点击地面移动  
- 战斗  
- NPC 高级 AI  
- 完整剧情导演／章节脚本  
- 正式 UI 框架（UGUI 产品架构、Localization 产品壳）  
- 修改 Core Freeze／扩 Demo Runtime 玩法  
- 擅自改 `ProjectSettings`／`Packages`  

---

## 12. 分阶段实施与独立 Commit

> 纪律：每 Phase **单独**实现 → 测试门禁 → **单独 commit** → 停等确认。禁止合并 Phase 提交（VS0.3 反例）。

| Phase | 目标 | 主要交付 | 门禁 | 建议 commit message |
|---|---|---|---|---|
| **V4-A** | 会话启动 | `PlayableHostSession`／Bootstrap；Load Content；建 World；Loop 可在 Editor 推进 | EditMode 或 Host 烟测：三人 Entity 存在 | `feat(unity): vs0.4 phase a host bootstrap` |
| **V4-B** | Entity 表现 | `EntityView` 生成与 Id 绑定；基础相机 | PlayMode：场景可见三人 | `feat(unity): vs0.4 phase b entity views` |
| **V4-C** | RTS 选择 | 点选／框选／选中高亮 | PlayMode：可多选三人 | `feat(unity): vs0.4 phase c rts selection` |
| **V4-D** | 命令桥 | Adapter→`PlayerCommandRequest`；四命令 | 下令后 Core ActiveAction 变化；无直改组件 | `feat(unity): vs0.4 phase d command bridge` |
| **V4-E** | 时间＋HUD | 暂停／倍速／单步；HUD 六字段 | HUD 与 DayClock／组件一致 | `feat(unity): vs0.4 phase e hud and clock` |
| **V4-F** | 事件反馈 | 事件日志面板 | Observe／DayEnd／Reject 可见 | `feat(unity): vs0.4 phase f event feed` |
| **V4-G** | Snapshot | Save／Load 按钮或快捷键 | 存读后状态一致＋View 重建 | `feat(unity): vs0.4 phase g snapshot ui` |
| **V4-H** | 一日可玩验收 | 手操清单＋简短 PlayMode 测试（能自动化则自动化） | §13 清单勾完 | `test(unity): vs0.4 phase h playable day` |

可选：**V4-I** Content 拷贝到 StreamingAssets（仅当要打 Windows 包试玩）；不阻塞 Editor 验收。

### 12.1 Core／Data 变更策略

- 优先零 Core 规则变更。  
- 若装配代码从测试复制过多：允许 **Data 侧** `PlayableDayBootstrap` 一次性整理，**单独 Phase／commit**，并写清与 Host 的边界。  
- 任何玩法语义变化先 ACR／文档，禁止 Host「顺便改规则」。

---

## 13. 验收标准

- [ ] Editor 进入 Host 场景即可 Load BaseGame 并创建三角色 World  
- [ ] 三人在场景中可见且可点选／框选  
- [ ] Labor／Rest／Observe／Cultivate 均经 `PlayerCommandRequest`→Port  
- [ ] HUD 显示 Day／Hour、Action、Schedule、Quota、Risk、Realm 且与 Core 一致  
- [ ] DomainEvent 调试反馈至少覆盖发现／拒绝／日终后果  
- [ ] Snapshot 存读可用  
- [ ] 可手操完成：默认 Schedule → Observe 发现 → Cultivate → 跨日见 Quota 后果（允许加速）  
- [ ] 无地图／寻路／战斗／NPC AI／剧情导演／正式 UI 框架  
- [ ] 未修改 Freeze；未扩展 Demo Runtime 玩法；未污染 Demo 场景逻辑  
- [ ] 每 Phase 独立 commit；最终 EditMode 既有测试仍全绿  

---

## 14. 风险点

| 风险 | 缓解 |
|---|---|
| Host 滑成「第二个 Demo」 | 禁迁 Runtime 逻辑；清单外功能拒绝 |
| Content 路径在 Player 构建失败 | V4-A 先锁定 Editor 路径；打包另 Phase |
| Tick 与帧率耦合导致手感不稳 | Host 用累加器按倍速推进固定 Tick；可暂停 |
| 选中与 Core DirectControl 不一致 | 只允许对 Bootstrap 三人下令 |
| Load 后 Handler／View 丢失 | V4-G 验收强制重建清单 |
| 为 HUD 便利改 Core 规则 | 只加只读 API；规则变更走确认 |
| PlayMode 难自动化 | V4-H 允许手操清单＋关键 Adapter 的 EditMode 测 |

---

## 15. Cursor 任务模板（复制即用）

### Task V4-A
```text
只做 VS0.4 V4-A（docs/40-process/59-vertical-slice-0.4-unity-playable-host-plan-v0.1.md）。
PlayableHost 加载 Content/BaseGame 并创建可 Tick 的 World（三人）。
禁止：选中/命令/HUD、改 Demo Runtime、改 Freeze、进 V4-B。
完成后：门禁 + 单独 commit + 停止等待确认。
```

### Task V4-B
```text
只做 V4-B：EntityView 绑定三角色。禁止寻路/地图/命令桥。单独 commit，停等。
```

### Task V4-C
```text
只做 V4-C：点选/框选。可只读参考 Demo PartyCommandController，禁止引用 Runtime 命名空间。单独 commit，停等。
```

### Task V4-D
```text
只做 V4-D：输入→PlayerCommandRequest→Port；Labor/Rest/Observe/Cultivate。禁止直改组件。单独 commit，停等。
```

### Task V4-E
```text
只做 V4-E：暂停/倍速/单步 + 最小 HUD 六字段。禁止正式 UI 框架。单独 commit，停等。
```

### Task V4-F
```text
只做 V4-F：DomainEvent 调试日志。禁止新玩法规则。单独 commit，停等。
```

### Task V4-G
```text
只做 V4-G：Snapshot Save/Load UI。Load 后重建 View/Handler。单独 commit，停等。
```

### Task V4-H
```text
只做 V4-H：一日手操验收清单与必要测试。不扩展地图/战斗/剧情。单独 commit，停等最终验收。
```

---

## 16. 编码前确认

1. **范围：** 是否批准本 Host 切片（§0～§13）为 VS0.4 唯一编码范围？  
2. **Demo：** 是否确认 Runtime **只读参考、禁止迁移／引用**？  
3. **试玩平台：** 第一刀是否 **Editor PlayMode 即可验收**（不强制打 Windows 包）？  
   - 建议：是。  
4. **发现率：** Host 默认是否提高 Observe 发现率以便一天内可玩到 Cultivate？  
   - 建议：Editor／Host 配置可调，默认偏高。  
5. 批准后是否严格 V4-A→H 串行、每 Phase 独立 commit？

**确认前禁止编码。**
