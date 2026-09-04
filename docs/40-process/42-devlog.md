# 开发日志

> **倒序追加：最新的记录写在最上面。**
> 这是项目的历史记录，用于跨设备/跨时间恢复上下文，以及交接给他人时说明"为什么代码长这样"。
>
> 每次有实质进展就追加一条。宁可短，不可漏。

---

## 2026-09-05 — WorldGraphEditor 势力管理与开局战略内容编辑（待人工验收）

- 保留既有 Territory Brush 与独立「管理势力」窗口；补齐已有势力 ID 永久只读、新建势力 ID／颜色／排序校验、普通排序跳过 999 等特殊排序，以及保存后对 Territory Brush 的即时刷新。
- 删除势力前的共享引用扫描扩展为递归检查整个 `Content/BaseGame/Data`：角色默认势力、场景／名册出场势力、FormalArmy、WorldSite／Territory 与 `strategicOpening` 的玩家／附庸／联盟／战争字段均会阻止删除。
- WorldGraphEditor 新增「势力管理」「开局战略」入口；开局战略窗口按 PackageStore 加载全部 `openingScenario`，以中文势力名、ID 和颜色选择玩家势力、附庸、联盟、开局战争。保存只替换所选场景 Raw JSON 的 `strategicOpening` 节点，绝不重建或同步其它 Scenario，也不触碰 Unity Runtime／SaveGame。
- 新增 Shared `OpeningStrategicAuthoring`，保存前按正式语义拒绝未知势力、自附庸、重复／套娃附庸、联盟冲突、重复／反向战争及联盟与战争同对。

**验证**：WorldGraphEditor Release 构建 0 warning／0 error；Shared.Tests 50/50 通过；`git diff --check` 无空白错误。WPF 交互与内容 roundtrip 留待用户人工验收。

---

## 2026-09-04 — 人物编辑器首次设势力与底部摘要 HUD 修正（待 Unity 人工验收）

- 修复原本无势力人物首次选择势力会因空身份立即清掉 `defaultFactionId`、再被 UI 重载回无势力的问题。现在选择非空势力自动设为「成员」；只有势力本身为空才清空字段与禁用身份下拉。刷新场景出场继承显示不再重载正在编辑的人物下拉框。
- 底部人物 HUD 恢复为单一紧凑摘要，移除「况／属／灵／修／性／事／系」内部切换入口。高度从 210 调整为 228；地点与势力身份使用统一 Presentation Resolver，在固定的两行区域绘制，避免与心境条重叠。完整人物资料继续由右侧「人物」打开的 `HostCharacterSheetPanel` 负责。

**验证**：CharacterNpcEditor Release 构建 0 warning／0 error；`git diff --check` 无空白错误。Unity Editor 编译与人工 UI 验收待执行。

---

## 2026-09-04 — 人物展示与默认势力开局链收口（待 Unity 人工验收）

**势力链**：人物定义的 `defaultFactionId/defaultFactionRole` 仅作为新会话初始化种子；Spawn 的 `factionMode` 可选 `CharacterDefault`、`Override`、`Unaffiliated`。开局解析后，当前归属唯一读取 `FactionMembershipComponent`，存档恢复继续优先保存的 Runtime membership，不会被人物默认值回写覆盖。新增开局单次 `[OpeningFaction]` 开发追踪，并以 Level Tester Roster 的巡卫甲／乙／丙验证人物默认势力继承。

**人物展示**：底部人物栏恢复接通既有「总／属／灵／修／性／事／系」七栏；势力显示改用正式中文势力名与身份，Presence 调试英文改为中文位置。新增只读 `HostCharacterPresentation` 汇总 Runtime 当前状态和人物定义的静态标签分类；详情页按身份、属性、灵根、性格、履历、天赋、活动倾向展示。人物详情打开时停止绘制世界头顶血条，避免覆盖 Modal；开发构建为打开人物详情记录实际 focus／subject。

**编辑器提示**：保存人物状态栏会显示实际文件、默认势力及「下一次新建游戏会话生效，当前运行中游戏不会热重载」；顶部说明区明确保存人物、加入场景出场、导出 Level Tester 名册分别影响何处。

**验证**：CharacterNpcEditor Release 构建 0 warning／0 error；Unity Editor／EditMode 尚待执行。

---

## 2026-09-04 — WorldMap 历史验收追踪日志清理（仅日志）

**范围**：删除已完成验收阶段遗留、会在正常交互中刷屏的 WorldMap 右键旅行／GatewayConfirm／Territory Border 追踪日志。

- 移除 `[GatewayB1Trace]`、`[HEX-RIGHTCLICK]`、`[ENEMY-RESIDUAL-RIGHTCLICK]`、`[GatewayConfirmUI]` 与 `[TerritoryBorder]` 的 `Debug.Log` 路径及其仅服务于该日志的辅助状态。
- 保留开发构建下用于报告 FormalArmy authority 损坏和 Hex 投影异常的 `Debug.LogWarning`；未修改任何命令分派、Gateway、Territory 或渲染算法。

**验证**：`git diff --check` 通过；Unity Host 无独立命令行工程，本轮未运行 Unity Editor。

---

## 2026-09-04 — WorldMap「情报」面板中文化（仅表现层）

**范围**：`HostWorldMapPanel` 的玩家可见情报文本改为中文；未改变 WorldMap 点击、Hex／WorldSite／Territory 数据、出口连接或任何运行时权威。

- WorldSite 详情统一显示「地点／地点 ID／锚点格／地点占地／可用出口／占地范围／本地地图 ID／领地区域 ID／控制势力／领地范围／所属势力 ID／当前格」。
- 普通 Hex／Territory 详情统一显示「地图格／地形／道路：是或否／可通行：是或否／控制势力／领地区域」；空值显示「无」，不再显示 `None`、`True`、`False`。
- 势力显示优先为「DisplayName（ID）」；没有 DisplayName 时只显示 ID。WorldSite 类型展示层映射为中文，Terrain 继续复用现有中文 `HexTerrainPresentation`。

**验证**：`git diff --check` 通过；当前仓库未提供 Unity Host 独立 `.csproj`，未运行 Unity Editor。本次 Shared.Tests Release 构建 0 warning／0 error（不覆盖 Unity Host 编译）。

---

## 2026-09-04 — Strategic Faction 开局成员资格数据链收口（待 Unity 验收）

BaseGame 的 Scenario 与 Level Tester Roster 已移除全部 `assignOpeningFaction` 和 `openingFactionId`；此前依赖场景默认势力的 13 名主管／巡卫／劳工 Spawn 在两处内容中均显式写为 `base:sect_huangcun_labor`。正式规则改为 Spawn/Roster `factionId + factionRole` 初始化 `FactionMembershipComponent`，无 `factionId` 即无势力。旧字段仍由 Loader/Applier 兼容读取，且只在显式 `factionId` 缺失时回退，并输出 `[ContentLegacy]` 开发期警告。CharacterNpcEditor 新增来自共享 faction 目录的“所属势力”下拉（包含无势力与山匪），保存时不再写 legacy 开关；本轮未发现 FormalArmy Authoring UI，未新建。FormalArmy 保持 Army 级 faction，成员初始化为 `Member`。

---

## 2026-09-04 — WorldGraphEditor 无势力／无主地笔刷（待人工验收）

WorldGraphEditor 的「势力范围」笔刷列表最上方新增固定编辑器工具项「□ 无势力 / 无主地」。它不是 `FactionDefinition`，不写入 `factions.json`，也不会出现在「管理势力…」窗口。选择后左键／左拖与右键／右拖均走既有擦除路径：普通 standalone Hex 移除独立控制记录；WorldSite 的 Footprint 或默认外围辖区会整块清空 `OwnerFactionId` 与 `ControlFactionId`，但保留 `TerritoryRegionId`、`PrimaryWorldSiteId` 和辖区几何。无主笔刷的 hover 使用低透明灰色预览，明确表示即将清除控制权；同一 stroke 的 Undo 仍复用原子撤销逻辑。完整实现、数据契约、验证和人工验收项见 [193](193-strategic-faction-content-and-worldgraph-territory-authoring-2026-09-04.md)。

## 2026-09-04 — Territory Authoring 从 MapEditor 纠正迁移至 WorldGraphEditor（待人工验收）

**纠正**：`MapEditor` 的职责严格恢复为 LocalMap `mapLayout` 矩形编辑；上一轮误加的 HexWorld Territory 页、画布、专用状态和事件已精确撤销。`WorldGraphEditor` 才是唯一 Hex 战略大地图 authoring 工具，Territory 现在只在其现有 `HexMapViewHost` 上编辑，没有第二套画布或 Runtime 引用。

**Shared 正式内容支持**
- `HexWorldDefinitionDto.TerritoryRegions`、`HexWorldSiteDto.TerritoryRegionId` 与 `HexWorldTerritoryRegionDto` 纳入 Shared DTO/JSON round-trip；保存稳定排序（RegionId、每 Region R→Q）。
- `HexWorldContentValidator` 新增 Region identity、Primary Site、双向绑定、Owner=Controller、bounds、跨 Region overlap、Site footprint 归属校验；有 error 时 WorldGraphEditor 禁止保存。
- `HexWorldEditorDocument` 成为 Territory mutation authority：O(1) Hex lookup、assign/reassign、erase footprint 保护、补齐 footprint、Odd-R 外围一圈、创建 Region、删除 Site 一并删 Region、SiteId rename 同步 PrimaryWorldSiteId；Undo/Redo 使用完整 DTO snapshot。
- 修复创建 Region 时 `Region.Hexes` 与 `Site.Footprint` 列表别名的风险，二者现在独立复制。

**WorldGraphEditor**：左侧增加「地图编辑／势力范围」页签，中心继续复用 `_mapView`。Territory Tab 以 WorldSite/Region 选择为目标，左键/拖拽原子 assign/reassign，右键/拖拽擦除普通 Hex；同一 stroke 一次 Undo。`HexMapViewHost` 增加独立 Territory DrawingVisual，处在 Terrain 与 footprint/hover/site overlay 之间；hover 不重绘 Territory。

**验证**：MapEditor Release 0 error（4 个既有 nullable warning）；WorldGraphEditor Release 0 error / 0 warning；`Shared.Tests` 24/24 通过（包括 Territory assign/reassign、footprint 保护、Undo、JSON round-trip、Odd-R 外围一圈）。

---

## 2026-09-03 — WorldMap Territory Overlay 图层开关（显示势力范围 toggle，纯 presentation）

**规则**：Territory 是永久 World State；Territory Overlay 是可选 WorldMap Presentation Layer，两者解耦。默认 **OFF** —— WorldMap 视觉与 Territory 实现前一致。

**实现（纯 Unity Host 层，未触碰 Core/Data authority）**
- `HostHexWorldRenderer`：
  - 新增 `ShowTerritoryOverlay` 静态开关 + `SetTerritoryOverlayVisible(bool)`，默认 false。
  - terrain 批恢复纯 terrain 色（`ResolveTerrainColor`），**不再 bake** territory tint → ON/OFF 不需重建 terrain cache（cache 本就只存几何元数据）。
  - 新增独立 `DrawTerritoryOverlay` 批：遍历 `TerritoryRegions.Regions[].Hexes`（稀疏，非全图扫描）→ `cell.ControlFactionId` 非空 → `StrategicFactionCatalog.MapTint` 半透明叠加（alpha=TerritoryTintStrength 0.22，等价旧 Lerp 视觉）；None 不画。绘制顺序 = Terrain → Territory overlay → footprint selection → hover/select outline → WorldSite/armies/icons；overlay 独立 flush，不污染后续 selection 批。
  - `ResolveTerritoryTint`（bake 版本）删除。
- `HostWorldMapPanel`：
  - `_showTerritoryOverlay` 字段（panel 实例级 → panel hide/show 不重置；纯 UI preference，**不写 SaveGame**）。
  - 标题行右侧、关闭按钮左侧加 `GUI.Toggle`「显示势力范围」；点击即时 `SetTerritoryOverlayVisible`；每帧 DrawGraph 前防御性同步。
  - Inspector 的 ControlFactionId/TerritoryRegion/PrimaryWorldSite 显示不受 toggle 影响（Territory 数据常驻）。

**验证**：Unity 程序集 Roslyn（dotnet csc @unitycheck.rsp -target:library）0 error（仅既有 warning）；git diff --check 干净。GL 视觉需 Unity 人工验收：OFF=原地图；ON=各 Site Territory 淡色 tint；再 OFF=颜色消失且 terrain/WorldSite/army/player/selection 不受影响；再 ON=颜色重现（Region 数据未重算）。

---

## 2026-09-03 — Phase 2J TerritoryRegion V1 硬化（Capture 一次易主事务 + ch01 形状修复 + overlap STOP）（待验收，192）

**范围**：在 191 基础层之上补齐 2J 指令缺口；不重写 191。

**Domain 硬化**
- `TerritoryRegionBoard`：新增 `_regionIdByHex` + `TryGetAtHex`（O(1)）；`Register` 对跨 Region overlap **throw InvalidOperationException**（含 hex/两 Region），同 RegionId 覆盖幂等；不自动裁决。
- `TerritoryControlService`：补 `TryGetRegionAtHex` / `TryGetRegionForSite`。
- `TerritoryInvariantValidator`：新增 13.6（region 每 hex ControlFactionId == Region.Controller）+ Bandit（`base:faction_bandits`）不能控 Territory。
- `HexWorldContentLoader`：Register try/catch → Result（不击穿 Apply）。

**Capture 一致性（§36-39）**
- 新增 `WorldSiteTerritoryTransferService.Transfer`：Site Owner + Region Controller + 每 Hex 一次易主（无 Region legacy/dynamic fallback 只改 Owner；双向绑定不一致 = failure）。
- `CaptureObjectiveService.TryCompleteWorldSiteCapture` 由裸 `SetOwner` 改为调 Transfer —— 不再有「Owner 改、Region 未改」中间态。

**Content 修复（决定性证据）**
- 验证工具（镜像 HexMath Odd-R）发现上轮 191 固化 Region hexes ≠ footprint+1-ring：ch01 30 Region 中 17 错、mvp 8 中 7 错。
- **ch01 权威重生成**（footprint ∪ ring ∩ bounds）：190 → 285 hexes；Regions=30 / Controlled=15 / Neutral=15 / Overlap=0 / InvariantErrors=0（ALL PASS）；diff 仅 territoryRegions 段 +212/−117。
- **travel_mvp overlap STOP**：footprint+ring 重生成会使 `base:region_huangcun` 与 `test:region_player_camp` 重叠 5 hex `(3,6)(4,6)(4,7)(3,7)(5,6)` → 按 2J §6.12 不自动裁决，**mvp 未写盘**，等设计调整。

**Host / 工具 / Snapshot / 文档**
- WorldMap tint 强度 0.26→0.22；Hex inspect 用 Board TryGetAtHex O(1)；Exporter hex 排序 R,Q；loader territoryRegions RejectUnknownFields；Snapshot Restore 后 Development 校验 Owner==Region==每 Hex（Debug.Fail 不静默修）；2J 加 Implementation Status + §6.12 SUPERSEDED banner。

**验证**
- Host 全链编译 0 error（2 既有无关 warning）；ch01 verify ALL PASS；`git diff --check` 通过；未跑 Unity tests。人工验收见 192 Part E（Case A–I + mvp overlap 裁决）。

---

## 2026-09-03 — Hex Territory + TerritoryRegion V1 基础层（领土真源 + 内容加载 + WorldMap 表现）（已封板，191）

**范围**：只建领土 authority 链；无 Capture/Siege/AI/Supply/Economy（下一轮才做 TransferWorldSiteAndTerritory 事务）。真源：2J。

**Domain（Core）**
- `TerritoryRegion.cs`：RegionId/PrimaryWorldSiteId/ControlFactionId/Hexes[]（固化）；Region identity 与 Controller 分离（无主 Site 也有 Region）。
- `TerritoryRegionBoard.cs`：Register/TryGet/TryGetByPrimaryWorldSite/Clear；禁止扫全图猜归属。
- `WorldSite.TerritoryRegionId`（与 Region.Hexes 严格分离：footprint ≠ 辖区）。
- `StrategicBoard.TerritoryRegions` 挂 Board。
- `TerritoryControlService`：GetController(world,hex)/GetRegionForSite/SetRegionController（region.ControlFactionId + 全部 hex 同步；**不改** Site Owner —— 避免与 Capture 循环依赖）。
- `TerritoryInvariantValidator`：Site↔Region 双向绑定、Owner==Controller、Region hex 在界内/无重复/无跨 Region overlap、footprint ⊆ 自身 Region。

**Content（Data）**
- `HexWorldContentDefinition` + `TerritoryRegionContentDefinition`；Site 定义加 territoryRegionId；root 加 territoryRegions。
- `HexWorldContentLoader.Apply`：cells → sites → territoryRegions（territory 最后写 cell.ControlFactionId = region.controller）；加载后跑 invariant，error → Result.Failure（不静默猜谁覆盖谁）。
- `HexWorldContentExporter` 补 regions 导出；`ContentPackageLoader`/`DefinitionSchema` 支持 JSON 字段。
- `SCHEMA.md` 补 hexWorld/territoryRegions 章节。

**Content（生成器一次性 + 固化）**：`TestResults/territory_generate.py`（footprint 距离最近竞争 + SiteId ordinal tie-break、footprint 保护、有主 Site radius：footprint≥4 → 2 否则 1、无主 Site = footprint-only）。写入：travel_mvp（8 region：huangcun 27 hex/压迫宗门、zhuangyuan 30/南堰、lingdi 7、player_camp 3，无主 town 仅 footprint）、ch01（30 region：15 有主，huangcun/zhuangyuan r2 30 hex，其余 7）。涂色 ≈ 15% 地图，85% 保持无主荒野。

**Persistence**：`TerritoryRegionControllerSnapshotDto{RegionId,ControlFactionId}`；StrategicSnapshotHelper Capture 全 region、Restore 用 SetRegionController；JsonSnapshotSerializer 读写 territoryRegionControllers；Site Owner 先 restore（既有）后 region controller restore，二者一致性由下一轮 transfer transaction 统一。

**WorldMap 表现**：`HostHexWorldRenderer` 淡色 Territory tint（ControlFactionId 非空 → StrategicFactionCatalog.MapTint Lerp 0.26，None 不加；正式 MapColor 非 hash）；`HostWorldMapPanel` Hex/WorldSite inspector 显示 ControlFactionId/TerritoryRegion/PrimaryWorldSite/Controller。

**验证（headless）**：TerritoryContentCheck 16 项 PASS（8 regions、invariant 干净、region hex 全部涂 controller 色、67/450 涂色、chengzhen 无主 region 空 controller=footprint-only、footprint⊆region、改 controller→Capture→JSON→Restore 后 region+hex 恢复、SetRegionController 不动 site owner）；回归 WeakHex/Bootstrap/LocalCombatHandoff/PreciseResidual/PendingEngagement/HostSim3 全 PASS；Core/Data/Unity 0 error。

**真源**：2J（规则）/ 本轮 devlog（实现）。已封板归档（191，与 Phase 5S persistence 收口同批）；Capture transfer transaction 留待下一轮。

---

## 2026-09-03 — Phase 5S persistence 收口：PendingEngagement / BattleOffer JSON Save-Load 完整持久化（已封板，191）

**目标**：BattleOffer 弹出时 Save → Load 后恢复完全相同的 frozen engagement；不重新 Gather、不重算支援范围、不因读档改变 Manual / Auto / Retreat / Local-origin declaration 语义。

**背景**：`PendingEngagementSnapshotRestore.Capture/Restore` 与 `StrategicSnapshotHelper` 已接线，缺口在 `JsonSnapshotSerializer` 未序列化 pendingEngagement；且现有 DTO 漏了若干 gameplay authority 字段（尤其 `BattleParticipantSnapshot.LocalMapResolutionKind`，读档缺失会默认 ExplicitEncounterMap，对 WORLD_COMBAT Auto 危险）。

**修复**
- DTO 扩展（WorldSnapshot.cs）：engagement 级补 `PrimaryEnemyFactionId / PlayerInclusionReason / RequiresPlayerDecision / PendingBattleTriggerReason / InitiatorCommittedHexQ·R / DefenderCommittedHexQ·R / RetreatHasValue`（committed hex 默认 int.MinValue=InvalidHexComponent）；participant 级补 `ParticipantEncounterLocalMapId / ParticipantLocalMapResolutionKind / HasParticipantLocalMapResolutionKind`；record 级补 `IncludedReason + HasPreBattle/PreBattleMode/SiteId/HexQ·R/FollowStackId/CombatPursuitStackId`（不存半份 snapshot）。
- Capture 补全部字段（含 `HasParticipantLocalMapResolutionKind=true`、retreat 仅非空写值）；Restore：`RequiresPlayerDecision` 用持久化值不再由 InvolvesPlayerSide 推导；PlayerInclusionReason 以持久化为准、仅旧 snapshot 才从 PlayerParty initiator 推导 DirectInitiator；frozen participant resolution：有 flag 直接还原，无 flag 用 `BattleLocalMapResolver.ResolvePendingEngagement` fallback（不盲信 0=WorldSite）；record 还原 IncludedReason + PreBattle（PreBattleHexQ/R 缺省 int.MinValue）；restore 尾部 `offer.SetPlayerParty(participants.CollectSelectedFriendly())` —— Participants.Selected 是 frozen selection authority，不另存重复 roster（用户勾选 Optional 后存档回来仍一致）。
- JsonSnapshotSerializer：新增独立 `SerializePendingEngagement/ReadPendingEngagement`（不塞进巨型 inline）+ hex pair `[{q,r}]` helpers；`SerializeStrategic` 仅在 PendingEngagement.EngagementId 非空时写 `pendingEngagement`（无 active BattleOffer 不写）；`ReadStrategic` 读到才设 dto（缺省 null = 无 pending battle）；EntityId 继续走 U/ReadUValue（不 JSON double 强转）；retreat 用 `hasValue` flag 防把 null retreat 恢复成默认对象；旧 JSON 缺 requiresPlayerDecision 时 Read 层 fallback involvesPlayerSide（等价历史推导，Restore 不重推）。未升 SchemaVersion（v6 optional 字段，旧档按无 pending battle 加载）。
- Development dump：`PendingEngagementSnapshotRestore` 在 Capture 与 Restore 后 #if 输出 `[PendingEngagementSnapshot]`（EngagementId/Initiator/DecisionSubject/BattleLocation/BattleArea·SupportArea count/BattleSiteId/Attacker·Defender/Locked 集合/BattleAnchor/Participant count+ResolutionKind+Map/OfferOrigin/RequiresWarDeclaration/Retreat）便于 Save 前 Load 后直接比较。

**验证（headless）**
- `PendingEngagementRoundTripCheck` 52 项 PASS：A 组 Capture（含 retreat/preBattle/resolution kind/Local-origin offer metadata）；B 组 JSON round-trip（frozen support area hexes 逐对一致、Optional unselected 保留、PreBattle+IncludedReason 保留）；C 组 Restore 语义（frozen support area 不重新 resolve、offer.SetPlayerParty 从 Selected 重建=仅 Mandatory、resolution kind=Wilderness）；D 组旧格式 fallback（无 hasLocalMapResolutionKind 时 resolve 出 Wilderness 而非 ExplicitEncounterMap；无 inclusion reason 时 PlayerParty initiator 旧 fallback DirectInitiator）；E 组 no-pending（无 active offer 不写 pendingEngagement、Load 后 null）。
- 回归：LocalCombatHandoffCheck / PreciseResidualCheck 全 PASS；Core/Data/Unity 三程序集 0 error；git diff --check 干净。
- ⚠️ 既有失败（与本轮无关，baseline 已复现）：RosterParityCheck / ContentLoadCheck 的 `localLoc=` 空 + location mismatch —— 疑似 f123cb0 的「NPC 初始 LocalMap 坐标归属 Spawn Instance」重构后 harness 断言过时，待处理。

**待人工验收（Unity）**：Case A PlayerParty Attack Army Offer → Save/Load → Manual/Auto（Auto 须按 WorldSite/Wilderness 结算，不得走 ExplicitEncounterMap）；Case B FormalArmy 发起同验；Case C LocalMap 主动攻击 Neutral（Origin=LocalMapHostileAction、Auto 不可选、RequiresWarDeclaration=true、点击 Manual 先宣战再入战）；Case D Optional friendly 勾选差异存档恢复一致；No-pending regression（Save/Load 后 PendingEngagement.IsActive=false、时钟不冻结）。

---


**症状**：上一轮修复后弥留 Follower 离开/重进能重新出现，但出现在 Hex 中心 + EntityId formation offset 的"重新计算位置"，不是原本倒下的精确位置；且 WorldSite 的 residual hex 上一版按主控 WorldPosition 派生（把 Follower 的 hex 按主控位置决定）。

**root cause**：`LocalCombatCasualtyHandoffService` 只保存 ResidualHex；`LoadedStrategicPopulationMaterializer.TryResolveResidualLocalPlacement` 重进时 ResidualHex → Hex center → WorldToLocal → ApplyFormationOffset，任何位置都丢。Save/Load 侧 `JsonSnapshotSerializer.SerializeStrategic` 根本没写 characterWorldPresences 的 worldX/worldY，读档同样丢。

**修复（ResidualHex 只答"属于哪个战略格"；精确落点另存 continuous world position，重进反向映射回原位）**
- `WorldPresenceBoard`：`WorldAgentPresence.SetAtResidualWorldPosition(residualHex, precise)` + board wrapper —— Mode 保持 AtHex（UsesHexPresence 语义不变），同时携带 HasContinuousWorldPosition/WorldPosX/Y。`SetAtHex` 维持"仅 Hex"（旧数据 / Auto Battle）。
- `StrategicResidualPresenceService.PlaceCharacterAtResidualWorldPosition`：IsResidualLifeCandidate 后 SetAtResidualWorldPosition；原 PlaceCharacterAtResidualHex 保留（战略战斗 fallback 不动）。
- `LocalCombatCasualtyHandoffService`：新增带 local point + bounds 的重载 —— Host 倒下瞬间从 EntityView 捕获真实 local（不读可能 stale 的 PresentationOverride）；Wilderness → context.WildernessHex + TryProjectLocalToWorld；WorldSite → 用<b>角色自己的</b> localX/localZ 经 TryLocalToWorldSurface → WorldToHex + OccupiesHex（边界歧义近邻），不再用主控位置。无 view/bounds → hex-only fallback（WorldSite 下明确失败，不猜主控 hex）。
- `PlayableHostBootstrap`：`TryGetCurrentLocalPresentation`（从 entityViewSpawner.Registry 读真实 transform → HostPresentationSpace）+ 立即 SetPresentationOverride 对齐 Domain presentation；CombatantDefeated 时 ResolveLoadedStrategicBounds 复用缓存 bounds 传入 handoff。
- `LoadedStrategicPopulationMaterializer.TryResolveResidualLocalPlacement`：presence.HasContinuousWorldPosition → precise WorldToLocal 回放（<b>不加 formation offset</b>，仅轻 safety clamp）；无 precise → legacy hex center + formation offset fallback 保留。
- `WorldAgentMapPositionResolver`：UsesHexPresence 且有 precise → 用 precise（LocalMap/WorldMap 同一 physical truth）。
- Save/Load：`CharacterWorldPresenceSnapshotDto` 加 `HasWorldPosition`（不靠 WorldX==0 判断，(0,0) 合法）；StrategicSnapshotHelper.Capture AtHex/AtWorldPosition 带字段、Restore AtHex+HasWorldPosition → SetAtResidualWorldPosition、AtHex 无 → SetAtHex；`JsonSnapshotSerializer` SerializeStrategic/ReadStrategic 补 hasWorldPosition/worldX/worldY（旧存档无字段 → false → legacy fallback）；Restore 记录 restoredCharacterWorldPresenceIds，旧 ResidualCharacterPresences 只作 legacy（已恢复者跳过，防 SetAtHex 清掉 precise）。

**验证（headless）**：PreciseResidualCheck 10 项全 PASS（SetAtResidualWorldPosition 行为 / Capture 保留 HasWorldPosition+WorldX/Y / Restore 还原 precise / 旧 Residual DTO 不覆盖新 authority / legacy 无 HasWorldPosition 走 hex-only）；上一轮 LocalCombatHandoffCheck 23 项回归全 PASS。Core/Data/Unity 三程序集 0 error；git diff --check 干净。

**真源**：本轮 devlog。

---

## 2026-09-03 — FIX：Local Combat 弥留者 residual handoff（PlayerParty/普通角色不随队消失）（已封板，190）

**症状**：主控 LocalMap 中普通 Local Combat（非战略 Encounter）把 PlayerParty Follower 打成弥留 → 当下正常显示，但离开再返回该 LocalMap 后该弥留者消失（战略战斗的双方弥留者仍存在）。

**root cause**：`PlayableHostBootstrap.DispatchDrainedEvents` 的 CombatantDefeated fallback 只完整覆盖 FormalArmy casualty（detach + Army residual）；PlayerParty/Follower/普通 LocalCharacter 无第三层 handoff → presence 未钉 hex，重进时：① `PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap` 遍历 party.Members 全集（含弥留 follower）把它当活人重新生成/随队；② `LoadedStrategicPopulationMaterializer` residual loop 无条件排除所有 party member，弥留者也到不了 StrategicResidual 路径。

**修复（统一 ownership 规则：任何角色 Incapacitated / visible Corpse 即停止跟随原移动 owner，钉到倒下真实 hex；逻辑 membership 保留、physical traveling 分离）**
- 新增 `Core/World/Strategic/LocalCombatCasualtyHandoffService`：非 Encounter / 非 FormalArmy 的 defeated residual 角色 → 解析当前 Loaded LocalMap 真实物理 hex（Wilderness=context.WildernessHex；WorldSite=PlayerParty canonical WorldPosition 派生 hex 且 site.OccupiesHex 校验，边界歧义近邻寻格，绝不 AnchorHex 瞎猜）→ `StrategicResidualPresenceService.PlaceCharacterAtResidualHex`（复用唯一 authority，无第二套 residual）。FormalArmy member 明确拒绝（防双 owner）。DEV 诊断输出完整字段。
- `PlayableHostBootstrap.DispatchDrainedEvents`：fallback 链改为互斥三层 Strategic Encounter → FormalArmy casualty → LocalCombatCasualtyHandoffService；新增 `LogLocalCombatDefeatDiagnostics`（EntityId/LifeState/IsPlayerPartyMember/FormalArmyId/WorldPresence/Traveling）。
- `PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty` 加生命 gate：`CombatLifeStateService.CanFight`（非 Alive 不随队）—— 自动辐射 capture/reconcile/materialize 过滤。绝不 TryRemoveMember。
- `PlayerPartyHexTravelService`（6 处）/`PlayerPartyWorldLocationQuery`/`PreEngagementLegalLocation` 的 `CaptureTravelingMembers(party.Members)` 直传改为 `CaptureTravelingMembersForPartyTransition`；`ManualBattleWorldCommitService` 保留（battle 进入时刻 living participant 快照语义，注释说明）；`HexStrategicSessionBootstrap` 保留（New Game 全 living）。
- `PlayerPartyLocalMapMaterializationService`：materialize 循环跳过非 Alive 成员（含 Active 弥留），末尾 `CaptureTravelingMembers(materializedIds)` 只含实际生成者。
- `LoadedStrategicPopulationMaterializer` residual loop：排除条件从"无条件排除所有 party member"改为仅排除 transitionable member → 弥留/尸体 party member（AtHex residual）允许走 StrategicResidual 重生成；不 double（MaterializeResidual 查 occupant）。
- `HostPlayerPartyController`：`TickFollowers` + `OrderFollowerTowardActive` 加 CanFight gate（Incapacitated/Dead 绝不发 follow movement）。

**验证（headless）**：LocalCombatHandoffCheck 23 项全 PASS（living 跟随正常；follower 弥留→AtHex(5,11)→traveling 排除→reconcile 不拖走→返回 rematerialize→party materialize 不生成弥留者；FormalArmy member 拒绝非 army handoff、FormalArmyCasualtyService 正常 detach+AtHex）。Core/Data/Unity 三程序集 0 error；git diff --check 干净。

**真源**：本轮 devlog。

---

## 2026-09-03 — Territory Border：双势力内侧 ribbon 表现

- Territory Border 不再使用 centered line；每条 exposed shared edge 以真实拓扑边为外侧基准，向当前 owning Hex 内部铺设 halo 与 faction 主色双层 ribbon。
- 删除异 faction 共享边的 canonical-owner suppression：A/B 接壤时双方各在自己的 Hex 内侧绘制，不重叠，保留中间原始 Hex seam；同 faction（即使 Region 不同）仍不画政治边界。
- ribbon 宽度按 owning Hex 中心到 shared edge 的距离限制在 30% 内，避免 zoom out 时吞没整个 Hex；未改 Hex 拓扑、SurfaceExit、Territory Domain 或 shared-edge tests。

---

## 2026-09-03 — Territory Border：共享边 corner authority 修复

- 修复 `HostHexWorldRenderer` 将 Neighbor direction 映射到错误 Hex 边的几何问题；边界无法封闭的根因不是 perimeter 算法，而是 Renderer 自行维护了错误公式。
- `HexMath.GetSharedEdgeCornerIndices` 成为 direction→真实 shared edge corner 的唯一 authority；Territory Border 与 `SurfaceExitZoneCalculator` 统一复用，避免两处复制方向映射。
- 增加 EditMode 几何回归：任意方向的邻格 opposite edge 端点吻合、单 Hex 6 边、相邻同 faction 两格 10 边、凹形三格 perimeter 无 dangling vertex。未运行 Test Runner。
- Development 下 hover/选中受控 Hex 变化时输出一次 `[TerritoryBorder]`，含邻格 faction、exposed 判定与 corner pair，便于人工核对 NE/NW/SW/SE 物理边。

---

## 2026-09-03 — Territory V1：WorldMap 政治外边界表现

- `显示势力范围` 不再给受控 Hex 叠加 faction fill；开关关闭与开启时 terrain 颜色完全一致，开启只增加独立的 Territory border pass。
- 边界唯一 authority 为 `HexCell.ControlFactionId`：同 faction 相邻 Hex 不画内部线，即使 `TerritoryRegionId` 不同；有主→无主/地图外画外边界；异 faction 共享边按 faction id 稳定顺序只画一次。
- 边界几何直接复用真实 Hex corners 与 `HexMath.Neighbor`，每条边使用浅色 halo + faction 主线双层 quad；不创建 GameObject，不写入 terrain cache，不改 Territory Domain、Capture 或存档。

---

## 2026-09-03 — Phase 5S CLOSED：真实世界战略战斗与世界／近景连续性 V1（待 checkpoint）

- **最终模型**：WorldMap 是战略总览，LocalMap 是当前真实 surface 的 RPG 近景；普通 `WORLD_COMBAT` 在真实 WorldSite/Wilderness LocalMap 发生并在同一世界现场结束，不再进入独立 EncounterMap。
- **已收口的连续性**：PlayerParty LocalVisible/AutoTravel 不再自取消，Site/Wilderness transition 采用 prepare→commit 与 exact ingress；可见 Surface Exit 即保证右键、WASD、LocalVisible AutoTravel 均可 traversable。
- **已收口的战斗 authority**：WorldSite 整个 footprint 决定 BattleArea/SupportArea；冻结 `BattleSiteId` 决定真实战场 LocalMap；PlayerParty 直接发起、Local hostile Character/Army 分类、Neutral 延迟 DeclareWar、精确 BattleHex commit、Auto player commit、WorldSite Army population 与 Manual participant materialization 均已接入。
- **原地战斗与残留**：Local-origin 同 physical surface 原地增量 assembly，保留当前 tactical placement；FormalArmy 战略伤亡在 Resolve detach，普通 Local Combat 伤亡即时 Army→StrategicResidual handoff，弥留/尸体保持真实 LocalMap 现场。
- **内容与验收 fixture**：FormalArmy 与青石 acceptance NPC 均为 Content-driven；authored NPC 使用 `localPosition`，保留青石镇 V1 regression fixture，不恢复 C# prototype bootstrap。
- **边界**：Lingering 仅保留旧 Auto/Explicit compatibility；PendingEngagement JSON 存档、authored instance identity、Surface Exit 性能缓存与大规模 population authoring 进入非阻塞 backlog。本阶段不新增玩法，建立单个 checkpoint。

---

## 2026-09-03 — P0 修复：非 Encounter FormalArmy 伤亡转独立残留（未提交）

- 普通 Local Combat 的 `CombatantDefeated` 原先只尝试战略 Encounter 处理；上一场战斗结束后 participant 已清空，因此倒下的 FormalArmy 成员仍挂在 Army，却又不属于 living Army population 或 StrategicResidual，视图会被裁剪。
- 新增 `FormalArmyCasualtyService`：仅当战略层明确未接管、成员处于 residual 生命周期且仍属于 FormalArmy 时，复用 `ArmyService.DetachNonLivingMemberAtCurrentArmyLocation` 解除编制，并由既有 `FormalArmyMemberPresenceSync` 写入 Army 当前 Hex 的残留 Presence。
- `ArmyService` 的批量战后 detach 与普通 Local Combat 单成员 detach 现共用同一逻辑，包含 Leader 刷新及最后一人 Army 解散；不会清 LocalMap occupant 或 `PresentationOverride`。
- `DispatchDrainedEvents` 以 `StrategicEncounterSpawner.OnCombatantDefeated` 的 handled 结果分流：战略手动战斗仍延迟到 Resolve；非 Encounter 军团伤亡完成交接后仅做一次当前战略人口 reconcile 与视图刷新。
- 新增开发期 `[NonEncounterArmyCasualty]` 诊断，记录 Army、生命状态、战场/当前 surface、表现覆盖、detach 与残留结果。未改 Participant gathering、战斗 Resolve、SupportArea、Travel 或 Exit。

---

## 2026-09-03 — P0 修复：友方 FormalArmy 参战快照与倒地残留生命周期（未提交）

- 移除 `BattleParticipantGatheringService.AddFormalArmiesAsMandatory` 对 `EntityTag.Npc` 的错误排除：由 `FormalArmyContentBootstrap` 创建的正式军团士兵虽带 `Npc` 标签，仍须以 `MandatoryFriendly`、正确 `FormalArmyId`、`Selected=true` 进入冻结参战快照；PlayerParty 原有的 NPC 排除不变。
- `BattleParticipantSpatialGuard` 增加开发期快照完整性审计：每个 `LockedPlayerFormalArmyIds` 中仍可进行宏观命令的成员都必须在快照中以匹配军团编号的 `MandatoryFriendly` 出现，并输出成员、标签、势力与缺失原因。
- `StrategicEncounterSpawner.OnCombatantDefeated` 现区分冻结友军与敌军：友军倒地只检查是否进入战后阶段，不执行敌军清场、追踪刷怪清理或 ArmyStack 数量同步；战斗结束后仍按既有 `ArmyPostBattleSyncService` 时机处理军团成员脱离与残留锚定。
- Local-origin 原地战斗保留已加载友军的 `PresentationOverride`，跨 surface 装配仍覆盖为战斗队形；技能栏不再在击败瞬间强制销毁目标视图，尸体/倒地者交由既有可见性与生命状态规则管理。
- 新增开发期 `[BattleFriendlySnapshot]`、`[BattleCasualty]`、`[BattleResidual]` 诊断；未改 Life 规则、SupportArea、Exit、Travel 或战斗结算策略。非 Unity 编译与差异检查结果见本轮交接。

---

## 2026-09-03 — P0 回归修复：WorldSite 跨面准入与可见出口契约（未提交）

- **根因**：上一轮 Surface Exit 的 transition PREPARE 误用了 `CanEnterWorldSiteLocalMap(..., "")`。该 API 的真实语义是“Party 已经在该地点后，是否能打开 LocalMap”，会检查 `HasPartyMemberAtSite`；从邻 Hex 正要进入目标地点时该条件必然为假，导致所有合法 Wilderness→WorldSite 透明出口被拒绝。
- **Access 拆分**：保留旧的 already-present scene access（新增明确名 `CanOpenWorldSiteLocalMapFromPresence`，旧名兼容保留）；新增 `CanTransitionPlayerPartyIntoWorldSite`，只检查 world、modal lock、目标 Site 与 LocalMap，不要求目标地点已有 Party presence。Wilderness→Site、手动 Site→Site、LocalVisible Site→Site 均改用 transition admission。
- **统一结构预检**：新增 `SurfaceExitTraversalService.TryPrepareTraversal`，零副作用验证当前 Context、目标格、DestinationKind/SiteId、Transition Admission、精确 ingress 或 wilderness map fallback。Presenter 仅显示“结构预检成功且本地可达”的出口；带连接的 WASD／右键执行入口在开启 Edge Gate 前复用该预检。
- **原子 Scene 提交**：`EnterWorldSiteAsParty` 先完成 transition admission 与 LocalMap 解析，随后才提交 AtSite／成员 presence；`WorldTravelService.ActivatePreparedWorldSiteScene` 只写确定性的 PartyWorld/LocalMap，不再重新执行 presence gate。
- **审计补全**：开发期输出当前 surface 的 Strategic、StructuralReady、LocallyReachable、VisibleUsable、ExactDuplicate 计数；结构失败的出口不会显示。不同 canonical edge 指向同一 WorldSite 仍是合法 `MULTIPLE_EXITS_TO_SAME_SITE`。
- **验证**：Core、Data、Runtime Host 非 Unity 编译 0 error；2 个既有 warning（`HostWorldMapPanel.cs:725` CS0162，`HostPlayerPartyController.cs:49` CS0414）。未运行 Unity Test Runner／PlayMode；待 LevelTester 验收 `(2,7)→(3,7)/(3,8)` 的右键、WASD 与 AutoTravel。

---

## 2026-09-03 — P0 修复：出口边身份、地点间事务与手动使用出口（未提交）

- **精确入口**：`WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection` 不再从按 `DestinationHex` 聚合的本地离场出口中匹配 `RepresentativeSource`，而是验证目标足迹格与来源外部格确实相邻，并用该唯一共享边直接建立入口。营地 `(4,6)` 进入荒村 `(3,7)` 与 `(4,7)` 两条边现在分别保留各自的边界接触点；离场出口按目标格聚合的表现规则保持不变。
- **先准备、后提交**：LocalVisible 自动离场和空闲手动离场均先验证来源地点、目标格、目标类型、地点访问资格、目标地图与精确入口；准备失败不再提前修改 Canonical、队员 Presence、`PartyWorld`、`LocalMap` 或路线段。正常业务失败全部前移到提交前。
- **明确的手动出口命令**：右键点中可用出口后不再只是普通移动，而是取消当前 AutoTravel、以精确目标寻路到所选出口，抵达回调再显式提交所选 `SurfaceExitConnection`；失败只更新状态与原因，不切图。
- **统一可用出口**：`HostSurfaceExitZonePresenter` 暴露当前战略有效且与 Active 同连通分量的出口集合；Presenter、右键、WASD、普通边缘检测、自动旅行与改道共同验证该集合。
- **物化失败保护**：`PlayableHostBootstrap` 的 `playerPartyMaterialized` 改为真实读取 `materializeResult.IsSuccess`；失败会记录错误并停止依赖新落点的组装、人口协调、视图重建、相机和恢复流程，不把失败伪装成已物化，也不消费失败前保留的入口上下文。
- **开发期拓扑审计**：所有 WorldSite 输出名称、足迹格数、战略出口数、唯一目标格/地点数、逐出口来源/目标/地形/可通行性/共享边数、同一目标地点多出口分组，以及当前已加载地点的本地不可达数。正式数据静态核对：主角营地 6 个战略出口、0 个无效出口，其中 `(3,7)` 与 `(4,7)` 两条均进入青石荒村；庄园右侧两个可通行道路目标为 `(18,4)` 与 `(18,5)`。
- **验证**：使用 Unity 2022.3.6f1 随附 Roslyn 与现有响应文件完成 Core、Data、Runtime Host 非 Unity 编译，0 错误；既有警告为 `HostWorldMapPanel.cs:725` 不可达代码与 `HostPlayerPartyController.cs:49` 未使用字段。`git diff --check` 通过；未运行 Unity Test Runner、PlayMode 或 EditMode，运行时案例留给 LevelTester 人工验收。

---

## 2026-09-03 — FIX：LocalVisible Surface Exit 的完成语义与可达性（未提交）

- `HostMoveController` 将路径完成策略拆为 `HoldStandby` 与 `PreserveCurrentCommand`：内部 LocalVisible 出口移动、终点移动与 follower 跟随不再在抵达时产生 Stop/Wait，从而不应由 Host 自己取消 PlayerParty AutoTravel。
- 新增 `SurfaceExitWalkGridReachability`：只在 `SlotRect ∩ WalkGrid` 的可走 cell 内选点，并以零 goal-snap A* 验证；Presenter 与 LocalVisible 出口执行共用该查询。不可达出口不显示；当前计划出口不可达则明确取消旅行并保留当前位置，避免无限重试。
- 本条尚未完成 Unity 人工验收；未改 SurfaceExit connection 几何、World topology、Battle、FormalArmy 或 NPC placement。

---

## 2026-09-02 — REFACTOR：NPC 初始 LocalMap 坐标归属 Spawn Instance（未提交）

- **问题**：青石镇五名验收 NPC 原先各自占用一个 LocalPlace，只是为了保存呈现坐标；这些伪地点被地图正确显示为地点名称，暴露了人口实例坐标与语义地点混淆。
- **调整**：`OpeningSpawnEntry` 新增可选 `localPosition { x, z }`；`openingScenario.spawns` 与 `characterRoster.entries` 共用同一解析/校验 schema。`WorldRegionBootstrap` 将其写入既有 `EntityLocationComponent.SetPresentationOverride`，并允许与 `localLocationId` 同时存在。
- **内容迁移**：青石散人甲/乙、青石挑衅者、朔风镇民甲/乙在 scenario 与 Level Tester roster 中均保留 `worldSiteId=base:site_chengzhen`、迁为精确 `localPosition`；删除五个非语义 LocalPlace，保留青石镇·入口等真实地点。
- **边界**：未改 CharacterDefinition、WorldPresence、HostMapGraybox、EntityViewSpawner、LocalMapVisibility、战斗或 FormalArmy 逻辑。`GameStartLookup` 仍以 definitionId 映射单一 EntityId（同 definition 多实例时后者覆盖），后续应单列 `AuthoredCharacterInstance / SpawnInstanceId` 改造。

---

## 2026-09-02 — FIX 试炼弱匪位置避让朔风外援队 (9,7)（未提交）

- 症状：LevelTester travel_mvp 中试炼弱匪（base:formal_army_bandit_weak）与青石验收朔风外援队（initialHex (9,7)）同格重叠。
- 原因：weak 首选 hex 旧算法 = 荒村 anchor + (6,0)；travel_mvp 荒村 anchor=(3,7) → (9,7)，正撞外援队 initialHex。
- 修复：`Ch01HexPrototypeMapBuilder.ResolvePrototypeTestBanditHexesBelowHuangcun` weak 首选改为 (Q+7, R-1) → travel_mvp (10,6)（青石镇北侧横路，passable 且不属于任何 Site footprint）；fallback 同步。ch01 大图荒村 (80,52) → weak (87,51) 不受影响。
- 验证：WeakHexCheck harness（PlayableDayBootstrap + ch01_reference + roster_level_tester）WEAK_HEX_CHECK_PASS：weak=(10,6)、strong=(5,11)、casualty=(0,5)、reinforcement=(9,7) 不重叠。

---

## 2026-09-02 — Local Hostility→BattleOffer V1 + Qingshi Acceptance Fixtures + 系列 FIX（【暂未验收】）

**背景**：在已验收的 PlayerParty Battle Initiator V1（3824178）之上，继续收口 LocalMap 主动攻击产品规则、补齐青石镇可验收 Content，并修复 Unity 人工验收暴露的 4 类问题。本组改动均未提交 commit，待人工验收后统一合并。

### 1) CORRECTION — Local Hostile Action → BattleOffer V1（未验收）
- **产品规则**：LocalMap Active 主动攻击 Character——① LocalCharacter（非 FormalArmy member）：一次确认"是否攻击【角色名】？"→ 直接进入 Local Combat，不 DeclareWar/BattleOffer/Gather Army；② living FormalArmy member（StrategicMilitary）：创建与 WorldMap Attack Army 共用核心的 BattleOffer/PendingEngagement/Participant Snapshot（含 PlayerParty/Mandatory Friendly/Friendly Support/Primary Enemy/Enemy Reinforcement/战力/位置），但 **Local-origin Offer 的 decision 只有 [手动战斗][撤退]，绝无 [自动战斗]**；③ Neutral 军事攻击不单独弹 DeclareWar 窗——Offer 内展示"确认手动战斗将向【Faction】宣战"，[撤退] 不宣战不造成伤害，[手动战斗]=DeclareWar commitment point（先 ValidateManualEntry → 若 RequiresWarDeclaration defensive re-validate → StrategicMilitaryAggressionService.TryEscalateToWar → 进入既有 Manual WORLD_COMBAT）；④ 已 War member：同样建 Offer，RequiresWarDeclaration=false，无宣战 warning；⑤ 已处于 active WORLD_COMBAT：guard 保持，直接继续 Tactical Combat。
- **实现**：删除 HostNpcContextMenu.Phase.MilitaryWarConfirm + 双确认（AttackConfirm1/2）、TryRouteMilitaryAttack()/TryRoutePlayerHostileAction()、HostStrategicInterruptPresenter.TryEnterCurrentOfferAsManual、PlayerPartyStrategicCombatCommandService.TryBeginLocalPlayerPartyMilitaryAttack；保留 HostileActionClassificationService（ArmyService.TryGetArmyForCharacter=StrategicMilitary 唯一 authority）/LocalHostileActionRoutingService/RequiresWarDeclaration。StrategicBoard 新增 `BattleOfferOrigin { StrategicCommand=0, LocalMapHostileAction=1 }` + Offer 4 个 metadata 字段；BattleDecisionPolicy 对 Local-origin 强制 Auto=false（按钮真源）；BattleOfferService 抽 `TryBuildOfferForPlayerPartyAttackCore(..., requireExistingWar, origin, ...)`，新增 Local-origin 变体（requireExistingWar=false）；PlayerPartyStrategicCombatCommandService 改为 `TryPrepareLocalPlayerPartyMilitaryAttackOffer`（prepare gate，9 项检查，不要求已 War）；WorldSnapshot/PendingEngagementSnapshotRestore DTO 同步 4 字段。HostNpcContextMenu 统一入口 `TryHandlePlayerHostileAction(actor,target,onConfirmed)`（active combat→直接执行/LocalCharacter→一次确认后执行/StrategicMilitary→建 Offer/Reject→consume）+ OnNpcArriveAttack re-classify race safety；HostCombatSkillBar 抽 `ExecuteResolvedHostileCast`（确认后执行、Cancel 不扣 cooldown、技能打 StrategicMilitary→建 Offer 不释放）；HostStrategicInterruptPresenter.DrawBattleOffer 重构（Auto 文案条件化、宣战 warning、按实际 option 数布局、Manual commit 三阶段流程）。Retreat 全程复用 BattleRetreatService.ExecuteRetreat + FinishOfferResolution，绝不调用 StrategicMilitaryAggressionService。WorldMap Attack 无 regression（保留 requireExistingWar=true + Origin=StrategicCommand + Auto）。

### 2) Qingshi Local Hostility Acceptance Content Fixtures（未验收）
- **验收对象**：默认 PlayableHost 用 `base:scenario_ch01_reference` + `base:hex_world_travel_mvp_30x15`，青石镇 Site `base:site_chengzhen`/LocalMap `base:map_site_chengzhen`/Places `base:places_site_chengzhen`（footprint (10,7)(11,7)(10,8)(11,8)，邻接增援初始 Hex (9,7)：passable、Road、必然属 SupportRing）。
- **9 个 Character**：3 无势力普通 NPC（青石散人甲/乙、青石挑衅者含 personalityTags "hostile"）+ 2 朔风镇民（Faction=shuofeng、Member、FormalArmy=none）+ 2 朔风驻镇卫（AtSite chengzhen，LoadedStrategicPopulation materialize）+ 2 朔风外援卫（InitialHex (9,7)）。新 `Content/BaseGame/Data/Characters/qingshi_hostility_acceptance_characters.json`（6 定义复用 guard 角色，普通凡人数值，不参与军事分类）。
- **通用数据能力（无 qingshi 特判）**：OpeningScenario.spawn 支持可选 `worldSiteId`/`localLocationId`（OpeningSpawnFields + LoadOpeningScenario + ContentReferenceValidator：LocalLocationId 须存在、WorldSiteId 须在 OpeningHexWorldId 的 HexWorldContentDefinition.Sites、无 OpeningHexWorldId 却声明 WorldSiteId → error，不 runtime fallback）；FormalArmyDefinition 支持可选 `initialHex`（q/r，`InitialHex != null` 为 presence authority，JSON (0,0) 合法；FormalArmyContentBootstrap 泛化 tail：validate cell passable → ArmyHexTravelService.InitializeArmyAtHex → ArmyStackAdapter.SyncStackTravelFromFormalArmy，**必须走 FormalArmy.WorldMotion**）；WorldRegionBootstrap/HexStrategicSessionBootstrap.ApplyOpening 按 spawn.WorldSiteId 对 authored entity（含 npc）SetAtSite；ContentReferenceValidator.ValidateScenarios 对 initialFormalArmyIds 做 scenario-aware q/r 校验。
- **青石 LocalPlace**：`ch01_site_chengzhen_places.json` 新增 5 个正式 LocalLocation（unaffiliated_a/b/hostile、shuofeng_civilian_a/b，kind Settlement，tags [acceptance,qingshi,civilian]），不改原 start place；`scenarios.json` 只在 ch01_reference 增加 spawn 条目 + initialFormalArmyIds 追加 2 支 Army（驻镇队 `army:qingshi_acceptance_shuofeng_garrison` 无 initialHex；外援队 `army:qingshi_acceptance_shuofeng_reinforcement` initialHex q9r7）。`SCHEMA.md` 记录两处扩展。不做 C# ID 特判、不建临时军队、不改 HexWorld terrain/owner、不预设 Player↔ShuoFeng War。

### 3) FIX — Authored WorldSite Resident NPC Presentation（未验收）
- **symptom**：青石 LocalMap 只看到 5 个 "XX·位"（HostMapGraybox WorldLocation label，来自 LocalPlaceSet），5 个 non-Army authored resident 无 EntityView；驻镇卫（FormalArmy）正常。
- **root cause**：`LocalMapVisibility.IsEntityVisible` 的 AtSite presence 分支用 `PartyWorld.SiteId` 锁 focusSite；玩家以 **AtHex/AtWorldPosition（Wilderness 邻接/呈现）** 停在 site footprint hex 上时 SiteId 为空 → focusSite=null → presence.SiteId(chengzhen) != null → 不可见；garrison 走 LoadedStrategicPopulation materialize 分支（硬门禁前 return true，不依赖 PartyWorld.SiteId）所以正常。
- **修复**（只动 `Unity/Host/LocalMapVisibility.cs`）：新增 `TryResolveVisibilityFocusSite`（优先 PartyWorld.SiteId，失败按玩家物理 hex——PlayerPartyTravel.CurrentHex 或 AtWorldPosition→HexMath.WorldToHex→Sites.TryGetAtHex 反查所属 WorldSite，与 wilderness reconcile 同源）；**只改 AtSite presence 分支**的 focusSite 解析，硬门禁 gate 原样保留。Domain 层 harness 全绿（Entity/Npc tag/AtSite@chengzhen/EntityLocation/ViewableEntityIds/IsEntityVisible 6 点），AtHex 场景修复前后对比验证，荒村 farmer 零泄漏。未碰 LoadedStrategicPopulation*/presence/army。

### 4) FIX — Qingshi NPC Missing Under Level Tester CharacterRoster（未验收）
- **root cause**：Unity Level Tester 默认 `characterRosterId="base:roster_level_tester"`，PlayableDayBootstrap/ContentGameStart 在 roster 非空时用 `roster.Entries` **替代** scenario.Spawns → ch01_reference.spawns 的 5 个青石 NPC 从未 SpawnIntoWorld（FormalArmy 走 initialFormalArmyIds 不受影响）。
- **修复**：① `ContentPackageLoader.LoadCharacterRoster` 补读 `worldSiteId`/`localLocationId` 两字段（与 LoadOpeningScenario 完全一致——之前 JSON 有值但 runtime 空）；② `level_tester_roster.json` 追加 5 个正式 entry（3 无势力 assignOpeningFaction=false + 2 镇民 assignOpeningFaction=true/factionId=shuofeng/Member，均带 worldSiteId/localLocationId，**不含 4 个 FormalArmy guard** 防 duplicate）；③ ContentReferenceValidator 新增通用 `ValidateCharacterRosters`（definitionId→character/jobId→job/localLocationId→已存在地点表；worldSiteId 依赖配对 scenario 的 HexWorld context 不在 roster 层猜）；④ SCHEMA.md 注明 roster entries[] 同形支持两字段。**不改 roster override semantics**（不 merge scenario.spawns+roster.entries），scenario.spawns 的 5 人保留（两种正式配置都存在）。新增 headless `RosterParityCheck`（OpeningScenarioId=ch01_reference + CharacterRosterId=roster_level_tester，与 Unity 完全一致）验证 5 NPC Entity/Npc tag/AtSite@chengzhen/EntityLocation/faction 全部 PASS；上一轮 BootstrapCheck 用 roster=null 所以误判。

### 5) FIX — LocalMap-origin BattleOffer 按钮重叠（未验收）
- **root cause**：`HostStrategicInterruptPresenter.DrawBattleOffer` 的 `btnIndex++` 写在 GUI.Button **点击条件体内**——IMGUI 只在点击帧返回 true，未点击帧 btnIndex 恒 0 → Local-origin（Auto=false）时 Manual 与 Retreat 落同一槽位，视觉重叠 + 点击命中歧义。
- **修复**：改为**动作列表驱动**——新增 `ButtonSpec{Label,Action}`，按 BattleDecisionPolicy 结果 append [自动战斗][手动战斗][撤退]，统一循环 `x = left + slotW*i`（x 与点击状态无关）；顺序保持 Auto→Manual→Retreat（Local-origin 为 [手动战斗][撤退]，无空槽）；每个按钮执行原业务动作零改动。warning 行（box底-78）与按钮行（box底-44）间距恒 12px 已核对不需调。

### 6) FIX — PlayerParty WorldSite Ingress Robustness + Location Authority Consistency（未验收）
- **症状**：PlayerParty 带 Followers 在 LocalVisible 下跨 LocalMap（尤其 WorldSite→WorldSite）易卡边缘/透明 Surface Exit、Active+Followers 挤一点、WASD 走不出、roster 位置 "?"。
- **position authority 不变式**：PlayerPartyWorldMotion（LocationKind/SiteId/CurrentHex）= strategic authority；WorldPosition = continuous physical authority；EntityLocationComponent.LocationId 只是 LocalMap named place（空不代表 unknown）；Followers 无独立 strategic position。
- **修复**：① `PlayerPartyLocalMapMaterializationService`：WorldSite fresh placement 的 follower 也用泛化 `ApplyPartyFormationOffset`（原 ApplyFollowerPresentationOffset，snapshot 分支不加 offset），candidate 经新纯函数 `ClampFormationCandidateToSafeInterior`（NearEdgeMargin 内缩，wilderness/site 通用）收敛 SafeInterior，防 offset 推回 exit band；materialize assert 改用实际 map bounds + SafeInterior + PresentationOverride。② `HostPlayerPartyController.RebindAllFollowers` 按 Party.Members 稳定顺序 `OrderFollowerTowardActive(id, followerIndex)`，goal=Active+FollowerOffset(index)（与 TickFollowers 同一 formation convention），不再 goal=Active exact position。③ `HostMoveController`：`SnapOntoWalkableIfNeeded` OOB 分支不再直接 return，新增 nearest-safe-walkable resolver（raw cell floor 保留→clamp 到 grid→ring/BFS→cell center 在 SafeInterior 且不在 SurfaceExit slot→返回 world center）；WASD tick 尾部对当前 cell OOB/blocked 兜底。④ Active repair 同步 presentation + canonical：SetPresentationOverride + transform 即刻更新，AtWorldSite 时立即 PlayerPartyWorldSiteLocalVisibleSync（不等 LateUpdate），保持 LocationKind/SiteId 不 SetAtWorldPosition。⑤ Followers 以 Active safe+slot offset 为 preferred 独立解析，同 cell 占用自动找下一候选。⑥ `PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation` 不再 silently invent spawn point，只用调用方实际落点完成 Gate（Disarmed），unsafe → diagnostics + Host repair 接管；re-arm 仍只走 IsInSafeInterior 的 TickRearm，绝不硬开 EdgeArmed。⑦ `PlayerPartySurfaceEdgeGate.ConsumeIngressContext()` 新增（仅清 5 个 ingress 字段），materialize return 前消费，防旧 ingress direction 泄漏（重复 Site→Site）。⑧ Site→Site 直连（TryExitWorldSiteByConnection + LocalVisibleAutoTravel destination branch）：external 属另一 site 时用 WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection 建立 destination ingress + SetIngressContext + BoundaryContactWorld canonical + committed ingress hex=external，无正式 ingress 明确 failure 不清入。⑨ boundary CurrentHex 三处从 WorldToHex 猜改正式 topology Hex（exit→external；wilderness→site→destinationHex；LocalVisible→destinationHex）；长期 AtWorldSite footprint hex 仍由 WorldSiteSpatialMapping 即时派生。⑩ `PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion`（motion→member 单向 repair，仅 ShouldMemberTransitionWithParty，FormalArmy 排除；AtWorldSite→SetAtSite/AtWorldPosition→SetAtHex(CurrentHex)，实际 repair 打一次 diagnostics），调用点：EnterWorldSiteAsParty 成功尾 + materialize success 后。⑪ roster UI：`HostStrategicRosterQueries` row 新增 LocationLabel，PlayerParty member（非 grouped）走 PlayerPartyWorldLocationQuery——AtWorldSite→SiteId+LocationLabel、AtWorldPosition（Wilderness 正常态）→SiteId=""+DescribeHexLabel；`HostStrategicCharacterListPanel` 显示 LocationLabel，双击 focus 仅 SiteId 非空时触发，杜绝 "?" 与 fake SiteId。⑫ 不做每帧 WorldToHex(transform)→Set Site/Hex 反推（保持正式 Surface Transition 决定 Context、Context 内连续 WorldPosition 的架构）。

**验证（非 Unity）**
- Core/Data/Unity 三程序集编译 0 error（Roslyn + Unity 2022.3.6f1 官方引用）；`git diff --check` 干净；headless 回归：BOOTSTRAP_CHECK_PASS / ROSTER_PARITY_PASS / CONTENT_CHECK_PASS / HostSim AtSite+AtHex 场景 PASS。人工验收清单：① LocalMap 攻击 LocalCharacter/StrategicMilitary 各 CASE（含 Neutral 宣战 commitment）；② 青石 7 个 EntityView（5 resident + 驻镇卫甲乙）+ 外援不提前出现；③ BattleOffer 按钮 CASE A–D；④ PlayerParty Ingress CASE A–H。

**状态**
- 【暂未验收】：以上 6 组改动（含 Content fixtures）尚未在 Unity 人工验收，也未 commit。

---

## 2026-09-02 — PlayerParty Battle Initiator V1 + Remote Attack / Pursuit Parity（已验收，3824178）

**做了什么**
- **Initiator V1**：PlayerParty 不需要组 FormalArmy，即可在 WorldMap 主动攻击 living Enemy FormalArmy。`CanTriggerPlayerPartyEngagement`（抽共享 `CanTriggerFromCommittedHex`，SupportArea 冻结集合 Contains）；`TryBeginPlayerPartyEngagement`（与 FormalArmy 共用 `CommitEngagement` 核心）；`TryGatherDirectPlayerPartyInitiator`（Active 必须成功加入否则整体拒绝；Followers 同为 DirectInitiator 不 Optional）；`TryBuildOfferForPlayerPartyAttack`（复用 Offer 创建尾巴，不复制 PlayerBattleOfferService）。字段：`InitiatorKind=PlayerParty / InitiatorFormalArmyId="" / AttackerArmyId="" / DecisionSubjectKind=PlayerParty / DirectInitiator`。
- **`AttackerArmyId=""` 下游审计**：SyncAttackerArmyAfterBattle 遇含 PlayerParty 的 mandatory party → no-op（绝不把附近 friendly FormalArmy 写成 AttackerArmyId）；ClearAttackOrdersAfterBattle 空值跳过；CommitArmyAtExactBattleHex 跳过 PlayerParty 记录。WorldMap UI 按 selection authority 分流（FormalArmy → ExecuteAttackStack；PlayerParty → command service）。
- **Pursuit Parity**：命令资格拆 `CanIssueAttackOrder`（菜单 gate，**不检查距离**）/ `CanEngageArmyNow`（+ SupportArea）；`AttackArmy` 立即接战 or 追击由 Core 决定。新增 `PlayerPartyHexPursuitService`（薄 movement adapter）：target 真源 = `PlayerPartyWorldMotion.AttackOrderTargetArmyId`（strategic order metadata 非 position authority），每 tick Host StepTick 驱动 `AfterTravelTick`（条件校验 → 先查 contact → 未接触 retarget，target 移动自动改道）；进入 SupportArea 即停 + 建 Offer，不要求走到 target exact Hex。普通 Move / Gateway 前 `CancelPursuit`；先 validate 后覆盖。Save→Load 后 Movement 恢复 Idle、pursuit 清空（与普通 travel 同契约）。
- 未动：FormalArmy travel / ArmyHexPursuitService / StrategicEncounterSpawner / ResolveService / LoadedStrategicPopulation* / Content JSON。

**验证**
- Host 全链编译 0 error（2 个既有无关 warning）；`git diff --check` 通过。人工验收 Case A–I 全部通过（commit「玩家主控大地图发起战斗没问题」）。

**真源**
- 本轮 devlog + `docs/40-process/187-playerparty-battle-initiator-v1-and-remote-attack-pursuit-parity-2026-09-02.md`

---

## 2026-09-01 — Auto WORLD_COMBAT physical authority 修复 + Phase 5S Final Closure（已验收，1886c02）

**做了什么**
- Auto 修复：`ActivateOffer` 立刻冻结 `LocalMapResolutionKind`（不再默认 ExplicitEncounterMap 误入 legacy lifecycle）；`ResolveAuto` 在 casualty 前复用 `ManualBattleWorldCommitService.CommitWorldCombatParticipants`（同一套 commit，零复制）；`HasActualPlayerPartyParticipant` 按 snapshot records 判断（不依赖 `AttackerArmyId != ""`）；`BindEncounterAfterAutoResolve` realWorldCombat 分支禁 `RestoreParticipantsAfterBattle` / 禁写 LingeringBattlefieldRegistry（residual 走 StrategicResidualPresence + LoadedStrategicPopulation）；Confirm auto settlement 后 Auto 走 `ApplyPartyWorldSitePresentation(closeWorldMap:false)` 切 BattleHex LocalMap，Manual 保持 `RefreshLoadedStrategicPopulation()`。第三方 Army vs Army Auto 不移动 PlayerParty。
- 修既有宏不匹配：`AssertFinalResidualAuthority` 调用包进 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`（生产编译与 Unity 行为不变）。
- **Final Closure**：新增 `docs/40-process/186-phase-5s-final-architecture-closure-2026-09-01.md`（19 条 final invariants + Character Content Authoring Convention）；6 份冲突旧文档顶部加 SUPERSEDED banner（23-combat §2、147/149/150/153/159 的 lingering 再入部分），历史内容保留不改。

**验证**
- Host 全链编译 0 error；`git diff --check` 通过；人工验收 Auto Case A–F 通过（commit「自动战斗也没问题」）。

---

## 2026-08-31 — Prototype Bandit FormalArmy → Content JSON 迁移（【暂未验收】）

**做了什么**
- 3 支 Prototype Bandit FormalArmy（荒村山匪 4 / 试炼弱匪 1 / 试炼强匪 3）从 C# 硬生成迁移到 Content JSON 驱动；runtime IDs（`army:formal_bandit_patrol_1` / `army:formal_bandit_patrol_weak` / `army:formal_bandit_casualty_test`）保持完全不变
- Data 层新增 `FormalArmyDefinition` / `FormalArmyMemberDefinition`；`DefinitionRegistry`、`DefinitionSchema`、`ContentPackageLoader` 增加 `formalArmy` 类型与严格字段校验；`ContentReferenceValidator` 校验成员 character 引用、leader 唯一、runtime id 全局唯一、scenario 引用存在
- `OpeningScenarioDefinition.InitialFormalArmyIds` 决定实际出生的军队；`FormalArmyContentBootstrap` 按 `initialFormalArmyIds` 复用 `ContentGameStart.BuildSpawnFromDefinition` → `GameStartBootstrap.SpawnIntoWorld` → Faction 指派 → `SetAtSite(assemblySiteId)` → `ArmyService.CreateAuthoredArmy` → `ArmyStackAdapter.EnsureLinkedStackView`；bootstrap 内禁设属性/Realm（只来自 CharacterDefinition）
- Core 新增 `ArmyService.CreateAuthoredArmy`（Content/seeding 专用，验证全部 Domain invariants，不走玩家组建策略）；`ArmyStackAdapter.SyncBanditStackView` 泛化为 `EnsureLinkedStackView`
- `Ch01ScenarioStrategicSetup` 删除 `SeedPrototypeBanditArmies`（含 `Armies.Clear()`）；`ArmyStackAdapter` 删除 5 个 EnsureBandit* 方法；**`TestStrategicBootstrap.cs` 整文件删除**；BanditScout 生成路径一并删除
- 新 Content：`Characters/strategic_bandits.json`（4 个 character，弱匪 凡人 3/1/12/6、强匪 筑基 36/24/150/16 原样迁移）、`Armies/ch01_test_armies.json`；`scenarios.json` 加 `initialFormalArmyIds`；`SCHEMA.md` 记录新类型
- 刻意保留：`IsTrivialTestEnemyStack` / `IsCasualtyTestEnemyStack` 及 runtime ID 常量（AutoBattle/diagnostics 仍用）；`PositionPrototypeTestBanditArmies` placement policy（仅 TryGet 已存在 army → 放置，不创建）
- EditMode 测试迁到新 `TestArmyFixtures` helper（纯测试夹具）；

**验证（非 Unity）**
- Core/Data/Unity/EditMode 四程序集编译 0 error（Unity 官方 rsp 引用）；Content 实载校验 PASS；端到端 `PlayableDayBootstrap.Start(ch01_reference)` 验证 3 支 Army（4/1/3）、runtime IDs、弱匪 Realm=Mortal+Attack3、强匪 Realm=Foundation+Attack36 全部正确
- 静态 production-reference check：`Assets/Scripts` 中 `EnsureBandit*` / `SeedPrototypeBanditArmies` / `TestStrategicBootstrap` / `test:bandit_` 0 命中；三个 runtime IDs 的实例创建唯一来源为 formalArmy Content bootstrap

**真源**
- 本轮 devlog（未单独归档 phase 文档）

**状态**
- 【暂未验收】：Unity 内 10 项验收（开局 3 支 Army／runtime IDs／弱强匪属性／Travel／LocalMap 可见／WORLD_COMBAT／residual marker／Save-Load smoke）待人工

---

## 2026-08-31 — WORLD_COMBAT 战后 residual ownership 修复（【暂未验收】）

**做了什么**
- 根因：`FormalArmyMemberPresenceSync.DetachMemberAtArmyLocation` 对 downed 成员统一走 `SetAtSite/SetAtWorldPosition`，覆盖了 `EnsureEnemyDownedWorldPresence` 已建立的 AtHex Residual authority，导致 WorldMap 无统一 Downed marker、离开再回来不 rematerialize
- `DetachMemberAtArmyLocation` 改为 residual-safe：Incapacitated / VisibleCorpse → `StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, memberId, army.WorldMotion.CurrentHex)` 后 return；living 保持旧行为；无 position 不拿 (0,0) 覆盖
- `ResolveAndEnd` realWorldCombat 分支重排：Release scope → 三个 Army sync（Attacker/Enemy/Participant）→ **FINAL RESIDUAL AUTHORITY**（EnsureFriendly/EnemyDownedWorldPresence + `AssertFinalResidualAuthority`）→ FinishOfferResolution；assert 仅 DEV/EDITOR 下 Debug.Assert，无 runtime log
- 无 prototype-specific special case（BanditPatrol/WeakBandit 无特判）；detach 结果与 battle role / sync 顺序无关

**真源**
- 本轮 devlog

**状态**
- 【暂未验收】：Case A 荒村山匪 4 弥留 marker=4；Case B 离开再回来 materialize；Case C 弱匪无 regression；Case D 敌军发起 Engagement 全灭；Case E Reinforcement 全灭

---

## 2026-08-31 — Phase 5S WORLD_COMBAT Battle Lifecycle Authority Cleanup（【暂未验收】）

**做了什么**
- WORLD_COMBAT 只拥有 participant / hostility / combat state / presentation / freeze-postbattle；世界实体存在只属于 PlayerPartyWorldMotion / FormalArmy.WorldMotion / StrategicResidualPresence / LoadedStrategicPopulation
- `StrategicEncounterSpawner` 新增薄 `PlanFreshWorldCombatManualEncounter`：清 Active Encounter transient（不清 Lingering Registry / Residual / 历史 Hex residual），不走旧 Lingering reuse 分支；`ApplyPending` 增 `freshWorldCombat` 判定（LocalMapResolutionKind WorldSite/Wilderness）
- `TryPrepareSnapshotEnemyParticipants` / `TryPrepareFormalArmyEncounterEntities` 增 `trackInEncounterScope` 参数：真实 WORLD_COMBAT 传 false（真实 FormalArmy 不再进入 BattlefieldSpawnScope 的 owned-entity 生命周期）；legacy Explicit/stored lingering/AutoBattle 保持 true
- `OnCombatantDefeated` / `TryMarkFieldCleared` 改为双通道：frozen `BattleParticipantSnapshot`（真实 enemy，`HasCombatCapableEnemyParticipant`）+ tracked spawn（synthetic fallback）；snapshot 增纯 membership helper `IsEnemyParticipant` / `IsSelectedFriendlyParticipant`
- `StrategicEncounterResolveService.ResolveAndEnd`：realWorldCombat 分支优先（不 Park Lingering）；`RestoreParticipantsAfterBattle` 仅 legacy 走；`ReleaseWorldCombatScopeWithoutRemovingEntities` 改调新增 `ClearCompletedWorldCombatSession`（清 ActiveBattlefieldId/tracked/EngagedPartyIds/SpawnOnNextMapLoad/FieldCleared/ArmyStackId/EncounterLinkId/LingeringLocalMapId/PendingLingeringEnterBattlefieldId；不清 Registry/Residual/Pursuit/BattlefieldLingering）
- `BattleOfferService.FinishOfferResolution`：`!realWorldCombat` 才写 LingeringBattlefieldRegistry（WORLD_COMBAT residual 走 StrategicResidualPresence）
- `HostStrategicInterruptPresenter.EnterManualEncounter`：worldCombat → fresh path；legacy 保留 `TryPrepareLingeringLocalMapSession + PlanManualEncounter`；PostBattle 文案去掉“残留战场/再派人进入”旧语义
- `StrategicEncounterHostilityService` 未改（snapshot+tracked+engaged 已正确）；AutoBattle / ExplicitEncounterMap 本轮保持

**真源**
- 本轮 devlog

**状态**
- 【暂未验收】：6 个 Case（无 residual／有 residual／同 Hex 旧 residual+新 living Army／living Army 历史伤亡／我方全倒／ExplicitEncounter smoke）待人工

---

## 2026-08-31 — Lingering Battlefield 特殊入口退役（【暂未验收】）

**做了什么**
- 产品模型：弥留/尸体 = 真实 Character 的 strategic residual presence（WorldMap marker 纯信息展示，非 Encounter gateway）；residual-only Hex 一律普通移动，不再产生 BattleOffer
- `HexResidualContextQuery.BuildMenuActionKinds` 只因 ActiveEnemyArmy 产生 `AttackArmy`；`HexRightClickResolver` 重写为 Active living Enemy Army → WorldSite → Move（删除“敌方残留存在但 Runtime 不可用→禁移动”旧规则；`HasEnemyResidualPresentation` 仅信息性）
- `StrategicTravelDriver.AfterTravelTick` 移除 `ArmyHexLingeringArrivalService.AfterTravelTick` 与 `TryResolvePendingLingeringVisit`（旧 PendingLingering intent 不再自动弹 BattleOffer）；`ArmyHexCommandService.AttackLingeringBattlefield` 标 `[Obsolete]`
- `HostWorldMapPanel`：Hex/Stack/Avatar 菜单删“攻击/进入残留战场”“追击/再攻”production gateway；residual marker 左键改纯 Inspect（“移动至该格可在 LocalMap 查看现场”）；`DrawArmyStacks` 不再注册 remnant 隐形 hit rect；`TryOpenStackAttackMenu` residual-only stack 直接 return false；`TryOpenPendingLingeringVisitAfterArrival` 等无调用者方法删除；`HostStrategicInterruptPresenter`“去查看”不再自动进残留
- `HexActiveEnemyArmyQuery`：living FormalArmy 不再因 `HasDownedRemnant` 被排除；raw abstract stack 仍排除 remnant；`BattleParticipantGatheringService` / `BattleInterruptQueue` 的 living member 不再被历史 casualty 过滤
- 刻意保留：StrategicResidualPresenceService / LoadedStrategicPopulationMaterializer / LingeringBattlefieldRegistry / BattleOfferService lingering 方法 / ArmyHexLingeringArrivalService 等底层兼容（只关产品入口，不删旧 Domain）

**真源**
- 本轮 devlog

**状态**
- 【暂未验收】：6 个人工 Case（Enemy residual-only / PlayerParty / Friendly residual / living+casualty / Army 到 residual Hex / End Battle regression）待人工

---

**做了什么**
- B2-2.1 已人工确认真实 WorldSite LocalMap 与 Active 主控角色正确；敌方 Participants、我方 FormalArmy Participants 仍未见。
- 静态确认同图复用仍进入 `PlayableHostBootstrap.ApplyPartyWorldSitePresentation` 内唯一的 `StrategicEncounterSpawner.ApplyPending(world)` 调用点；`SpawnOnNextMapLoad` 是既有一次性消费标记。
- 在该真实调用点加入一次性 `[WorldCombatAssembly]` 前后日志，读取 Snapshot、追踪、表现落点、可见性、地图解析与同图复用状态，供 LevelTester 直接区分实体准备、表现与视图层断点。
- 未增加平行 assembly，不重载 LocalMap；未改 `LocalMapVisibility`、Participant Gathering、Battle completion、Travel、Pause 或 Camera。

**真源**
- [184](184-phase-5s-b2-2-2-same-map-manual-encounter-assembly-diagnosis-2026-08-30.md)

**状态**
- Phase 5S-B2-2.2：诊断已入仓，待最小 LevelTester 复验；尚未依据运行时结果修复参与者缺失。

---

## 2026-08-30 — Phase 5R-B7A WorldSite Surface Passability（实现完成／待人工验收）

**做了什么**
- 静态 A/B 证明截图绕线不由 non-target Site blocking 导致：`(3,7)→(6,9)` blocked/unblocked 同 path、同 cost=4.4，MandatoryTransit=false
- 精确根因是 Preview 绘制 Site departure 的内部 hex-center 拼接前缀，而 World／LocalVisible executor 实际从 Canonical 直走正式 BoundaryContact
- PlayerParty 普通 Surface route 删除 non-target Site blocked 与 MandatoryTransit fallback；Site 身份不再改变 passability
- 保留 target Site whole-footprint goal-set；补齐非目标 Site 的同 Order `Wilderness→Site→Wilderness` Context continuation
- Preview 在 departure 时从正式 outside exit 绘制，与 executor 共用 egress authority
- 非 Unity Core harness：B7A_01～10 **10 PASS / 0 FAIL**；Unity／LevelTester 待人工

**真源**
- [175](175-phase-5r-b7a-worldsite-surface-passability-2026-08-30.md)

**状态**
- Phase 5R-B7A **实现完成，待人工验收；未提交**

---

## 2026-08-28 — Phase 3 + Phase 4 文档封板收口

**做了什么**
- **Phase 3 = Accepted / Sealed**（用户正式确认；**非** Cursor 独立 Unity 人工验收）
  - 核心目标：FormalArmy 军事层；PlayerParty 独立旅行；Continuous WorldPosition／Travel／Presence／Save-Load／Authority 边界
  - 验证：LevelTester 持续使用 + Phase 4 开发／人工验收实际依赖
  - 原计划 166 F11 TEST 1–10／167 验收 1–12 逐条签字表未单独归档 → 不再阻塞
  - 真源：[166](166-phase-3-formal-army-continuous-world-2026-08-27.md)／[167](167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md)
- **Phase 4 = Accepted / Sealed**（LevelTester 人工验收通过）— 见同日较早条目
- 同步 [41-roadmap](41-roadmap.md)、[163](163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)

**状态**
- Phase 0–4 **Accepted / Sealed** · Phase 5 **Not Started**

---

## 2026-08-28 — Phase 4 Accepted / Sealed（仅文档／Roadmap 收口）

**做了什么**
- 正式标记 **Phase 4 = Accepted / Sealed**；**未**扩功能、**未**启动 Phase 5
- 最终 Battle Authority 写入 [171](171-phase-4-battle-authority-2026-08-28.md) §1 为正式真源：
  - Trigger＝Initiator/Defender **共边相邻**（禁 WorldPosition 距离）
  - BattleArea＝Defender 当前 Hex（多 Hex Site＝全 Footprint）
  - SupportArea＝BattleArea ∪ 共边邻格；Participants／Manual 按 SupportArea + 交战方
- 记录 Hex topology Authority 修复（Odd-R↔axial；含 **CollectHexLine 已修**）；归类为 Hex 真源修复，非 Phase 4 特补丁
- **Deferred / Future Regression：** 敌军主动攻击 Retreat 人工验收；AI vs AI 主动接战人工验收（缺战略 AI，不阻塞封板）— 见 171 §8
- 附带体验：WorldMap 列表滚动收紧、Zoom In 扩大、Cheat Tools 与 F10 解耦（171 §6）
- 同步 [41-roadmap](41-roadmap.md)、[163](163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)
- 唯一人工验收 Scene：`Assets/Scenes/LevelTester.unity`

**状态**
- Phase 4 **封板完成** · Phase 5 **Not Started**

---

## 2026-08-28 — Phase 4 Participant 来源追踪：PlayerHex Authority + 删除 Snapshot 旁路

**做了什么**
- **根因确认（LevelTester 实测）：** `BattleAreaHexes` / `SupportAreaHexes` **计算正确**；Player 误加入因 **PlayerHex Authority 与 WorldMap Marker 分裂**（Gathering 旧读 `WorldPresence`，Marker 读 `PlayerPartyTravel`）
- **Battle Trigger（A）：** 新增 `BattleEngagementTriggerService`；Initiator **已提交 Hex** ∈ Defender SupportArea；禁止 ContinuousWorldPosition 派生格提前接战
- **Player Hex Authority：** `BattleEngagementSpatialQuery` 优先 `PlayerPartyTravel`；idle 用 `WorldToHex(WorldPosition)` 与 Marker 对齐
- **Participant 旁路删除：** 移除 `ApplyLockedParticipantsToSnapshot` 内 **`seedMandatoryAttackers` 循环**（曾可在 `PlayerPartyIncluded` 时绕过 SupportArea 写 Snapshot）
- **逐成员 Gathering：** 每个 Party 成员独立 `MemberHex ∈ SupportArea`；Snapshot 写入二次校验
- **IncludedReason + 硬断言：** `BattleParticipantInclusionReason` / `BattleParticipantSpatialGuard`；Cheat Panel 输出判定链（Before/After Gathering/Snapshot）
- **WorldMap Debug：** `BattleEngagementWorldMapDebug` 橙框 BattleArea、蓝框 SupportArea
- **Tests：** Trigger 回归、Player 距 Defender 2 格、stale WorldPresence、`OfferPath_PlayerTwoHexFromDefender` 全路径集成测试
- 文档：[171](171-phase-4-battle-authority-2026-08-28.md) §4A–4B 根因与 IncludedReason
- **未**同步飞书；**未**人工验收；EditMode **待 Unity 跑通**（BatchMode 因 Editor 占用未执行）

**状态**
- Phase 4 Trigger + Gathering + Participant 追踪入仓 · push GitHub

---

## 2026-08-28 — Phase 4 SupportAreaHexes 集合规则（supersede 中心距离 ≤1）

**做了什么**
- **规则变更：** Participant Gathering 改用 **`BattleAreaHexes` + `SupportAreaHexes`** 冻结集合；资格判断为 `SupportAreaHexes.Contains(UnitHex)`
- **BattleArea：** 野外 = Defender 接战 Hex；多 Hex Site = 全 Footprint（非 Anchor 代替）
- **SupportArea：** BattleArea + 与其中任一 Hex 直接共边相邻的全部 Hex
- **Domain：** 新增 `BattleEngagementSupportArea`；重写 `BattleParticipantGatheringService` / `BattleEngagementAuthorityService` / Debug / Snapshot 持久化 SupportArea 列表
- **Bugfix：** `ApplyLockedParticipantsToSnapshot` 中 `seedMandatoryAttackers` 不再绕过 Gathering 空间规则
- **Tests：** T1–T10、LevelTester 回归、SeedMandatoryAttackers 快照测试
- 文档：[171](171-phase-4-battle-authority-2026-08-28.md) 更新；「BattleLocationHex 距离 ≤1」标记 superseded
- **未**同步飞书；**未**人工验收

**状态**
- Phase 4 SupportArea 规则入仓 · 待 EditMode 跑通

---

## 2026-08-28 — Phase 4 BattleLocationHex 规则修正（supersede Initiator 扫描）

**做了什么**
- **规则变更：** Participant Gathering 唯一空间 Authority 改回 **`BattleLocationHex`**（纯 Hex Graph Distance ≤1）
- **Initiator + Defender** 无条件加入；其他 Army 须同交战方 Faction + 距 BattleLocationHex ≤1 + 无 Battle Lock
- **PlayerParty** 强制加入条件改为距 BattleLocationHex ≤1（非 Initiator）
- **`InitiatorEngagementLocation`** 降为 Debug-only，不参与资格判断
- **Domain：** 重写 `BattleParticipantGatheringService`、`BattleEngagementHexDistance`（`HexDistanceToBattleLocation` / `ResolveBattleLocationHex`）
- **Tests：** `BattleAuthorityTests` T1–T9 按新规则重写（含 T6 防 Initiator 回归、T7 第三方、T9 不重新扫描）
- **Debug：** Cheat Panel 战斗页输出 BattleLocationHex / PlayerPartyHex / ManualEligible
- 文档：[171](171-phase-4-battle-authority-2026-08-28.md) 更新；旧 Initiator-centered 规则标记 superseded
- **未**同步飞书；**未**人工验收

**状态**
- Phase 4 规则修正入仓 · 待 EditMode 跑通

---

## 2026-08-28 — Phase 4 Battle Authority 入仓 + Initiator 扫描中心修正

**做了什么**
- **Phase 4 Domain：** `PendingEngagementRuntime`、`BattleEngagementAuthorityService`、`BattleParticipantGatheringService`、`BattleDecisionPolicy`、`BattleManualEntryPolicy`、`BattleRetreatService`
- **语义分离：** `BattleInitiator` vs `PlayerDecisionSubject`；Manual 仅 `PlayerPartyIncluded`；拒绝远程 FormalArmy Manual
- **Initiator 扫描中心修正：** 原 `GatherAndLock` 误用 `BattleLocation`（Defender Hex 优先）；改为 `InitiatorEngagementLocation` 唯一中心；`BattleLocation` 仅 Presentation / LocalMap 锚点
- **接线：** `BattleOfferService.ActivateOffer` → Authority；`HostStrategicInterruptPresenter` 按钮由 Policy 驱动；Snapshot Pending Engagement + Initiator 字段
- **EditMode：** `BattleAuthorityTests` T1–T9（含 T8 关键 Initiator≠接触点场景）
- **编译修复：** `CreateParty` 适配 `TryAddMember` 新签名
- 文档：[171](171-phase-4-battle-authority-2026-08-28.md)
- **未**同步飞书；**未**人工验收；**未**跑全量 Unity Tests

**状态**
- Phase 4 **实现入仓 · 待 EditMode 跑通** — 产品指令：**暂停扩展与人工验收**
- **未做：** Legacy 战斗入口删除、Initiator=PlayerParty 路径、LevelTester 手操封板

---

## 2026-08-28 — WorldMap 选中真源、Attack Order Snapshot 与 Strategic UI 输入优先级

**做了什么**
- **Army Marker Load：** 玩家 FormalArmy 不再误走 ArmyStack Presentation；`ArmyWorldMapPresentation.ShouldDrawArmyStackMarker`；`ARMY_VIS07`
- **选中真源：** 新增 `HostWorldMapSelectionAuthority`；Marker / Army List / 右键 Dispatcher 统一读取；移除空 Id fallback PlayerParty
- **Attack Order Snapshot：** `AttackFormalArmy` + `OrderTargetArmyId` 持久化；`RestoreAttackOrderIfNeeded`；`FormalArmyOrderSnapshotTests`
- **Strategic UI Input Priority：** 修复 `HandleMapInput` 提前导致 Panel Block 未注册 + `e.Use()` 吞 Checkbox；`HostUiHitTest` 双通道；OnGUI 顺序收口
- 文档：[170](170-worldmap-selection-strategic-ui-input-2026-08-28.md)
- LevelTester CASE 1–6 **人工验收通过**

**状态**
- 已 commit + push；**未**同步飞书

---

## 2026-08-28 — Snapshot Faction / Test Entity 生命周期审计

**做了什么**
- 审计 LevelTester Save→Load：**主角团变 None** + **测试山贼消失** 为同一 Snapshot 链路类问题，非 Faction Registry 丢失
- 主角团：`base:faction_player` 为 Catalog ID（无 Faction JSON）；根因 `JsonSnapshotSerializer` 漏写 entity `factionId`
- 山贼：Snapshot **含** entity 19–26；根因 Restore 顺序（Membership 晚于 FormalArmy Apply）+ **ArmyStack 未从 FormalArmy 重建**
- 统一修法：`StrategicSnapshotHelper.FinalizeRuntimeLinks` + `ArmyStackAdapter.EnsurePresentationStacksFromFormalArmies` + Rehydration 后二次 Finalize
- 文档：[169](169-snapshot-faction-test-entity-lifecycle-audit-2026-08-28.md)
- **未** commit 代码改动（文档先行）；**未**人工验收封板

**状态**
- 待 LevelTester TEST 1–2 重验 + 新 Save 含 entity `factionId`

---

## 2026-08-27 — Phase 3 收口：A2 Authority 重验、PP-Follower、试炼三军与伤亡夹具

**做了什么**
- **A2 Authority 第二轮：** 禁止 FormalArmy 静默 `RemoveFromPlayerParty`；PlayerParty 成员须先 Leave Party 再组军；`ArmyAuthorityRules.TryValidateNotPlayerPartyMember`；`FormalArmyPhase3AuthorityTests` 扩充
- **G16–G18：** Moving Army `SyncNonLivingMembers`；F11 Debug Incap/Sync Casualties/Presence 显示
- **PP-Follower：** `PlayerPartyTransitionMembership` — Follower 跨 LocalMap/Hex 与 Active 同步；`PlayerPartyFollowerLocalMapTransitionTests`
- **主角营地：** 独立 `base:map_player_camp` + `player_camp_places.json`；不再复用荒村 LocalMap
- **试炼三军：** 荒村山匪 / 试炼弱匪（必胜）/ 试炼强匪（自动伤亡）；travel_mvp 小图西北放置出界修复
- **伤亡夹具：** 试炼强匪自动战必胜 + **必定 1 人弥留或阵亡**；`AutoBattleCasualtyFixtureTests`
- 文档：[167](167-phase-3-closure-playerparty-and-casualty-fixtures-2026-08-27.md)；166/roadmap 更新
- **未**人工验收封板；**未**跑全量 Unity Tests

**状态**
- Phase 3 **收口入仓** — 待 LevelTester 167 验收清单 + 166 F11 TEST 1–10
- **未做：** Phase 4 Battle Authority、Legacy 战斗入口删除

---

## 2026-08-27 — Phase 3 FormalArmy Continuous World 实现入仓（待验收）

**做了什么**
- **3A Authority：** `ArmyAuthorityRules`；Active 禁入军；Follower 入军原子退出 Party；Background Travel Cancel
- **3B–C World Motion + Travel：** `FormalArmyWorldMotion`／`FormalArmyContinuousTravelService`；Site Departure；Footprint → `AtWorldSite` canonicalize；`ArmyHexTravelService` 委托连续推进
- **3D Presence：** `FormalArmyMemberPresenceSync` 成员 Presence 从 Army 派生
- **3E–G ArmyService：** 仅 friendly Site 组军；Wilderness 禁 Disband；Snapshot Phase 3 字段 + `FormalArmySnapshotRestore`
- **PresenceHex 收口：** `EnsurePresenceHexValid()` 强制 `PresenceHex == AnchorHex`；Loader／Editor／Validation；30×15 测试 Content 调整
- **Host：** F11 `HostFormalArmyDebugPanel`（`PlayableHostBootstrap` 挂载）
- **EditMode：** `FormalArmyPhase3AuthorityTests`
- **编译修复：** Snapshot `armyMotion` 重名；`TryGetActiveSegmentWorld` 签名；DebugPanel `using`
- 文档：[166](166-phase-3-formal-army-continuous-world-2026-08-27.md)；roadmap／163 更新
- **未**同步飞书；**未**人工验收封板

**状态**
- Phase 3 **未封板** — 待 LevelTester F11 人工 TEST 1–10 + EditMode 全绿
- **未做：** Battle Authority（Phase 4）、FormalArmy WorldMap Marker、Autonomous AI

---

## 2026-08-27 — Phase 2D Background Character World Travel 人工验收封板

**做了什么**
- **2D-A～D**（延续 720f585）：Background Simulation Scheduler、Travel Core、Save/Load、F12 Debug
- **2D-E Loaded LocalMap Materialization**：`LoadedDestinationArrivalMaterializer`（Initial Load + Runtime Arrival 共用）；Wilderness Hex Notify；Site Ingress `BackgroundTravelArrivalContext`；Belonging Query + Explain
- **2D-F Site Departure 语义**：禁止 BeginTravel 同步 instant arrival；`BeginSiteDepartureTravel` 保持 `AtWorldSite` 直至跨过 Boundary；真实 FootprintCenter→BoundaryEntry Travel
- **2D-G Destination Canonicalization**：Travel To Hex 命中 Footprint → `AtWorldSite(siteId)`；Travel To Site 真源 `WorldSiteId`；AnchorHex 仅 Presentation；Character 投影 PresenceHex
- **Bootstrap 修复**：同伴开局 `AtWorldSite(荒村)` + PresenceHex；`CaptureTravelingMembers` 仅主控
- **Development Trace**：`BGTRAVEL TRACE #N`（Core TraceSink + Host 注入）
- EditMode：`BackgroundCharacterWorldTravelPhase2DTests`／`BackgroundLoadedDestinationArrivalTests`／`BackgroundWildernessLocalMapMaterializationTests`
- 文档：[165](165-phase-2d-background-character-world-travel-2026-08-26.md) 封板更新；roadmap 2D 标记完成
- **未**同步飞书

**状态**
- Phase 2D **封板**；Deferred：Autonomous AI Travel、Background Combat UX、FormalArmy Continuous Marker

---

## 2026-08-26 — Phase 2C Continuous Player World Movement 人工验收封板

**做了什么**
- Continuous Player World Movement：WorldPosition 真源、LocalMap↔HexWorld 双向投影、Edge Transition
- **Ordinary Hex Actual Connections**：合法 Neighbor 数 = Exit Connection 数；真实世界方向 → LocalMap 周界投影；Overlap Resolution
- **WorldSite Full-Footprint Boundary Connections**：全 Footprint 外围唯一 Outside Hex；非固定 6 出口；FootprintWorldCenter 方向投影；ExitZone→Connection→DestinationHex Transition
- Edge Ping-Pong Guard、Canonical Exit Trigger Geometry、Host Presentation／Debug
- EditMode：`SurfaceExitConnectionTests`／`SurfaceExitZoneOverlapTests`／`WorldSiteFootprintExitConnectionTests`／`PlayerPartyContinuousWorldPhase2CTests`
- 文档：164／roadmap／2K §5.8.7 状态更新
- **未**同步飞书；**未 push**

**状态**
- Phase 2C **封板**；下一目标 Background Travel／Directional Site Entry Spawn 等 Deferred 项

---

## 2026-08-26 — Phase 2C Surface Exit Zone／Edge Transition 入仓

**做了什么**
- Surface LocalMap Edge Transition（Site Exit／Wilderness 跨 Hex；不 snap 邻格中心）
- Ping-Pong Guard：`PlayerPartySurfaceEdgeGate`（TransitionInProgress／Disarm／Rearm）；不改 Zone Geometry
- **Canonical Exit Trigger Geometry**：`ExitTriggerDepth` + PlayableBounds；Geometry∥Availability；Detection 与 Presentation 共用
- MapLayout 字段 `exitTriggerDepth`；Host `HostSurfaceExitZonePresenter` 半透明精确覆盖
- 文档：2K §5.8.7 Exit Trigger；[164](164-phase-2c-surface-exit-zone-and-edge-transition-2026-08-26.md)；glossary／roadmap／163
- **未**同步飞书

**状态**
- Phase 2C 竖切入仓；已由后续封板 commit 取代

---

## 2026-08-26 — Phase 2B 人工验收封板

**做了什么**
- 制作人确认 Phase 2B 手操通过：PlayerParty Travel／Wilderness／Materialize／Background 留下
- EditMode 回归 PASS（含 Travel／Presence／Party／MultiHex／Snapshot）；补测具 `HexWorld.MapId` 与 MH05 Anchor 断言
- 代码真源：`c895d3d`
- 本条封板 commit：**不 push**；不混入 Phase 2C

**状态**
- Phase 2B **封板**；进入 Phase 2C（Continuous World Movement／双向投影／Edge Transition）

---

## 2026-08-25 — Phase 2B PlayerParty World Travel MVP

**做了什么**
- **独立测试世界** ase:hex_world_travel_mvp_30x15（30×15／6 Site）；正式 ase:hex_world_ch01 保留；开局 scenario 指向测试世界
- **PlayerParty Hex Travel**（非 Fake Army）：PlayerPartyWorldMotion／PlayerPartyHexTravelService；共用 Hex A*（HexTravelMode.Ground）与 per-Hex tick；可取消停当前格
- **WorldMap**：Active 头像 Marker；无军团时右键 Party Travel；路径预览；顶栏停止旅行
- **Wilderness Fallback**：Terrain→复用 LocalMap 模板（Plain→map_site_a／Forest→map_site_linjian／Mountain→map_site_kuangshan）
- **Materialize 闭环**：PlayerPartyLocalMapMaterializationService + ExpandLocalMapForCurrentPartyWorld；修复 AtHex 在非遭遇 LocalMap 被隐藏导致「进近景无 Party」
- **UX 注记**：「进入近景(调试)」仅为 Phase 2B Prototype；正式方向为关 WorldMap 自动 Expand
- **测试**：PlayerPartyWorldTravelPhase2BTests（含 Materialize／Background 不跟随）
- **未做**：Continuous LocalMap 边跨 Hex、Close→自动 Expand、Background Travel／Combat、正式 Wilderness 生成

**状态**
- Phase 2B 代码落地并 push；手操继续验证 Travel／Wilderness Materialize

---

## 2026-08-25 — Phase 2A 人工验收封板

**做了什么**
- 制作人确认 Phase 2A 手操通过：PresenceHex／Character World Presence／青石荒村 Multi-Hex／Background 不画 WorldMap 头像
- 代码真源：`18600af`（feat）／`8d49bf4`（docs note）
- 本条仅文档封板；**不 push**；不混入 Phase 2B

**状态**
- Phase 2A **封板**；下一阶段 Phase 2B（PlayerParty World Travel MVP + 30×15 测试世界 + Wilderness Fallback）

---

## 2026-08-25 — Phase 2A PresenceHex + Character World Presence（代码落地）

**做了什么**
- **PresenceHex**：Content `presenceQ/R`、Loader 缺省→Anchor 确定性迁移、Runtime `WorldSite.PresenceHex`、Validator／WorldGraphEditor 查看与设置（≠ Anchor 允许）
- **Character World Presence 查询**：`CharacterWorldPresenceQuery`（AtSite→Site.PresenceHex；Army 成员→Army.CurrentHex；AtHex 预留）
- **真源纪律**：AtSite 存 SiteId，不另存可漂移 Hex；Stop Follow 保留 Site Presence；Background 不画 WorldMap 个人头像
- **Snapshot**：可选 `characterWorldPresences`（v6 向后兼容）
- **验收 Content**：青石荒村 `base:site_huangcun` Footprint 扩为 4 格；Anchor `(80,52)`；Presence `(81,52)`
- **Camera 最终规则**（同日）：仅 WASD Hard Follow；RTS 不控镜头 — 见上条／[2K §1.1](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)
- **未做：** Background Travel／Combat／Continuous Exit／Wilderness／Party WorldMap Avatar／FormalArmy 重构
- **已知缺口：** Prototype **无** WorldSite LocalMap→卸载近景的正式 Exit；开「地图」仅叠 UI

**状态**
- 代码待制作人手操 Presence／Camera；本轮按指示 push GitHub；**不同步飞书**

---

## 2026-08-25 — LocalMap Camera 最终规则（仅 WASD Follow）

**做了什么**
- **Supersede**「RTS 默认 Follow／中键取消 Follow」
- 最终：仅 WASD → Snap＋Hard Follow；RTS／右键寻路完全不控镜头；切换 Active 一次性 Snap
- 真源写入 [2K §1.1](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、ADR-0026 补钉；Host 实现已对齐

**状态**
- 规则已定；待制作人 CAMERA-A～F 手操签收

---

## 2026-08-25 — Phase 1 PlayerParty 封板

**做了什么**
- 人工验收确认：Single Active／Party≤6／Follow／Stop Follow／Active Switch／Followers Move+Combat／Group Work／View≠Command／非 Active 右键不 fallback／Route Preview 仅 Active／**Camera：仅 WASD Hard Follow（RTS 完全不控镜头）**／Dying·Dead 不可 Active／FormalArmy 与 Party 互斥
- 代码已在 `aa1ebb9`／`e683aab`／`8770fb0`；本条仅文档封板，不 push

**状态**
- Phase 1 **封板**；进入 Phase 2A（PresenceHex + Character World Presence 真源）

---

## 2026-08-25 — RPG-First 架构真源（仅文档）

**做了什么**
- **新真源 [2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)**：ActiveControlledCharacter / PlayerParty(≤6) / PresenceHex / 连续 HexWorld / FormalArmy 军事层 / Character Policy V1 / Succession
- **[ADR-0026](43-decisions/ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)**：从「多人 RTS + 移动必 Army」迁到 RPG-first
- **[163](163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md)**：代码冲突审计 + Phase 0–8 迁移计划（**未实现代码**）
- Supersede：2A 铁则 4、ADR-0024 部分、Glossary／Roadmap／Overview／AGENTS；RTS 过程文档加 Legacy 注记
- **禁止本轮：** 任何 C#／Prefab／Content／Snapshot／Editor 改动

**状态**
- 文档 Phase 0 **完成**；等待制作人审核后再开 Phase 1
- 飞书 **NOT SYNCED**

---

## 2026-08-24 — Hex WorldSite 准入/人口、Ch01 全 Site LocalMap、Manual Battle 可见性

**做了什么**
- **Hex 敌军 Pure 真源：** `HexActiveEnemyArmyQuery` 禁止 Node fallback；修复山匪 hex 误判荒村
- **WorldSite 进入：** `StrategicWorldSiteAccessService`、`WorldTravelService.EnterWorldSiteScene`、`HostWorldMapPanel` 右键菜单
- **Site 人口：** `StrategicWorldSitePopulationService`；`LocalMapVisibility` / `PlayableHostBootstrap` 不再用 FocusArmy 作白名单
- **Ch01 内容：** 28 Site 独立 map/places JSON；`ch01_hex_world.json` 全量 `localMapId`
- **Manual Battle：** `MarkPartyInEncounter` 主路径补全；遭遇图 `IsEngaged` 可见；Battle 前 `ClearSiteFocus`
- EditMode：SITE-ENTER、Population、Ch01 Mapping、ENCOUNTER_ASSEMBLY_03
- 收束文档：[160-hex-worldsite-localmap-and-manual-battle-fix-2026-08-24.md](160-hex-worldsite-localmap-and-manual-battle-fix-2026-08-24.md)（**未同步飞书**）

**验证**
- 制作人手操：Site 进入、人口、Manual Battle Members — **本轮口头 OK（几乎没有问题）**
- Unity EditMode 全套：**需关 Editor 后 batch**

**状态**
- 战略 WorldSite / Content / Manual Battle **COMMITTED + PUSHED**
- 飞书 **NOT SYNCED**

---

## 2026-08-24 — Encounter 作用域 Lingering + WeakBandit 参战名单 + 大地图路线预览

**做了什么**
- **多战场隔离：** `LingeringBattlefieldRegistry`、`BattlefieldSpawnScope`、冻结 Participant 的 Lingering 再进；MULTI-ENCOUNTER / LINGERING_PARTICIPANT EditMode 测试
- **WeakBandit 参战名单：** `EncounterAssemblyTrace`、Snapshot 统一刷怪、Hex 支援 1 格；`LocalMapVisibility` 遭遇图 Participant 过滤 + 荒村误判修复
- **大地图 UX：** `HostWorldMapPanel` 路线预览仅绑定当前选中我方 Moving Army（Presentation only）
- **Hex 右键 / 残留查询：** `HexRightClickResolver`、`StrategicResidualPresentationQuery` 等；多组 EditMode 测试
- **内容与 Editor：** `ch01_hex_world.json`、WorldGraphEditor / Shared.Tests 增量
- 收束文档：[159-encounter-scoped-lingering-and-worldmap-path-preview-2026-08-24.md](159-encounter-scoped-lingering-and-worldmap-path-preview-2026-08-24.md)（**未同步飞书**）

**验证**
- 制作人手操：荒村启动、路线预览、取消选择、Residual/Lingering — **本轮口头 OK**
- Unity EditMode 全套：**需关 Editor 后 batch**

**状态**
- 战略 Encounter / Lingering / WorldMap Path Preview **COMMITTED（本地）**
- 飞书 **NOT SYNCED**

---

## 2026-08-23 — Hex Battlefield Residual 聚合重构（PURE DERIVED）

**做了什么**
- 废弃 WorldMap 每 Character 散落头像 / 匿名 ArmyStack remnant 正式表现
- Residual 位置收口：`WorldAgentPresence.AtHex` + `HexCoord`；`StrategicResidualPresenceService`
- Presentation：`StrategicResidualPresentationQuery`（Hex × DynamicRelation × DEAD/DOWNED）
- Host：`DrawResidualMarkers`、左键详情、右键穿透；Active Army 优先
- Snapshot schema **v3 → v4**：`ResidualCharacterPresenceDto`（仅 CharacterId+Hex）
- EditMode：`ResidualGroupingPresentationTests`（ARMY/GROUP/REL/LIFE/SAVE）

**验证**
- EditMode Residual / Downed Vis：**需 Unity EditMode Runner**
- Manual Acceptance A–F：**NOT RUN**（制作人手操）

**状态**
- Domain + Host + Snapshot **已实现** · Manual Acceptance **PENDING**

---

## 2026-08-23 — WorldGraphEditor Performance Pass（200×100）

**做了什么**
- 审计：旧实现每次 Refresh 清空 Canvas + 最多 2 万 `Polygon`（per-Hex immediate）
- 改为 **16×16 Chunk DrawingVisual** 世界坐标缓存 + viewport `MatrixTransform`；visible chunk culling；dirty chunk rebuild
- Pan/Zoom/Hover **不** rebuild terrain geometry；Brush Stroke 单次 Undo；Validate 不再跟绘制绑定
- 状态栏 perf 指标；`Shared.Tests` WGE-PERF-01~08
- 文档：更新 [158](158-hex-world-content-authoring-pipeline-2026-08-23.md) Editor Performance Architecture

**验证**
- `dotnet test Shared.Tests`：11 passed
- WorldGraphEditor 手操 Idle/Pan/Zoom/Brush：**NOT RUN**（需制作人本地打开）

**状态**
- Editor Performance Pass **已实现 · MANUAL ACCEPTANCE PENDING**

---

## 2026-08-23 — 158 Hex World Content Pipeline + WorldGraphEditor WYSIWYG

**做了什么**
- **HexWorld JSON Pipeline：** `Content/BaseGame/Data/Worlds/ch01_hex_world.json`；`HexWorldContentLoader`；Scenario `openingHexWorldId`
- **WorldGraphEditor 彻底 Hex 化：** 删除 Node/Route 编辑；Terrain/Road/Site WYSIWYG + Save JSON
- **WYSIWYG Layout 修复：** `HexWorldLayoutShared` 对齐 Runtime `HexWorldLayout`（Odd-R parity bug）
- **校验：** Road 连通性；Content schema `openingHexWorldId` / `hexWorld` reference
- 文档：[158-hex-world-content-authoring-pipeline-2026-08-23.md](158-hex-world-content-authoring-pipeline-2026-08-23.md)；更新 ADR-0025 / 155 / roadmap

**验证**
- EditMode：`HexWorldWysiwygLayoutTests`（5000 roundtrip）、`HexWorldContentPipelineTests`
- Unity PlayMode + Editor 手操：**延期**

**状态**
- Hex World Content Authoring **已实现 · UNITY MANUAL VERIFICATION 延期**

---

## 2026-08-23 — 154 Formal Army RTS 收束文档 + 追击暂缓

**做了什么**
- 收束文档：[154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md](154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md)
- 记录 `f6eb844` 已交付：Formal Army 移动/攻击、青色路径预览、残留战场不双倍、角色名单弥留/尸体
- **追击（尤其追移动敌军）仍有问题** — 制作人决定暂缓，§3 记录静态分析与下轮修复方向
- 更新 [141](141-pursuit-stick-and-multi-melee-2026-08-18.md) 已知问题、[62](62-project-status-2026-08-01.md)、[41-roadmap](41-roadmap.md)

**验证**
- 无新代码提交；文档收束 only

**状态**
- Formal Army RTS **部分实现 ACCEPTANCE**（移动/攻击/预览/残留/名单 OK）
- 追击追移动敌 **KNOWN ISSUES · 延期**

---

## 2026-08-22 — Strategic Host 双入口（角色／军队 · 移除 Node 组军）

**做了什么**
- 大地图工具栏并列 **「角色」** / **「军队」** 全局一级入口（不依赖 Node）
- **正式删除** Node 菜单「军团管理／节点组军」及 `OpenForNode` 玩家路径
- `HostArmyFormPanel` 仅作为军队列表 embedded 的 Detail / Creation UI
- 地图 Army portrait：单击 → 打开军队列表 + Detail；双击 → 镜头定位
- 角色列表：左侧多选未编组 + 底部「组建军队」；军队列表 empty state + 「组建军队」
- 验收清单修订：[153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md](153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md)

**验证**
- EditMode：`HostStrategicRosterQueriesTests` — **STATIC REVIEW PASSED · UNITY VERIFICATION 延期**

**状态**
- Host 双入口 **已实现 · STATIC REVIEW PASSED · UNITY VERIFICATION 延期**

---

## 2026-08-22 — Strategic Host 双入口（角色／军队列表 · 全战式）

**做了什么**
- 大地图工具栏并列 **「角色」** / **「军队」** 按钮；无军队时军队按钮 **不灰掉**，列表显示 empty state
- `HostStrategicCharacterListPanel`：全局角色列表；单击详情；双击跳 Army／Node；多选「组建军队 [ACCEPTANCE]」
- `HostStrategicArmyListPanel`：全局军队列表；单击详情；双击镜头定位；「新建军队」embedded 创建
- `HostStrategicRosterQueries`：只读角色／军队列表数据 + 战力摘要
- `HostArmyFormPanel`：`OpenGlobalDetail` / `OpenGlobalCreate` embedded 模式；仍只走 `ArmyUiCommands`
- 节点菜单「军团管理」→ **「节点组军（次级）」**；失败原因改 status 提示，不再整按钮灰掉
- 验收清单：[153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md](153-strategic-layer-runtime-acceptance-checklist-2026-08-22.md)

**验证**
- EditMode：`HostStrategicRosterQueriesTests` — **STATIC REVIEW PASSED · UNITY VERIFICATION 延期**
- Host 手操 153 勾选表 — **延期**

**状态**
- A–K **已实现 · FINAL STATIC CLOSURE PASSED · MANUAL ACCEPTANCE UI 已实现**
- Host 双入口 **已实现 · STATIC REVIEW PASSED · UNITY VERIFICATION 延期**

---

## 2026-08-22 — Strategic Manual Acceptance UI（Unity 验证 延期）

**做了什么**
- 统一 Host 开发验收面板：`HostStrategicAcceptancePanel`（F8／大地图「战略验收」）；标注 DEVELOPMENT / ACCEPTANCE UI
- War / Alliance / Vassalage / Tribute hook / Node Owner / Retreating / Landless / Snapshot v2 最小可见与手操
- `HostArmyFormPanel` 补 AddMember / RemoveMember / ChangeLeader
- 战后 Aftermath 面板（Captured / Escaped / RetreatingArmy）；`ResolveLifeStateLabel` 显示「被俘」
- Core：`StrategicAcceptanceCommands` + `StrategicAcceptanceInspector`（薄 wrapper，不写 Board）

**验证**
- EditMode：`StrategicAcceptanceTests` — **PENDING — UNITY VERIFICATION 延期**

**状态**
- A–K **已实现 · FINAL STATIC CLOSURE PASSED · MANUAL ACCEPTANCE UI 已实现 · UNITY VERIFICATION 延期**

---

## 2026-08-22 — Strategic Layer Final Closure（Unity 验证 延期）

**做了什么**
- Legacy anonymous ArmyStack：`StrategicDayHandler` → `EnsureBanditScoutArmy`（FormalArmy + 4 真实 Scout Character）
- 玩家 Character 战略入口：`WorldTravelPathService` + Host 全面拦截；仅 Formal Army 移动/追击
- Ch01 外交污染隔离：`Ch01ScenarioStrategicSetup` / `Ch01ScenarioProgressionHooks`；Generic Bootstrap 不再决定剧情 War
- Snapshot：**v1 explicit reject**；v2 required + strategic state mandatory
- 文档：2A §44 Ch01 Scenario 边界；152 §12 Final Closure

**验证**
- EditMode：`StrategicFinalClosureTests` — **PENDING — UNITY VERIFICATION 延期**

**状态**
- A–K **已实现 · FINAL STATIC CLOSURE PASSED · UNITY VERIFICATION 延期**

---

## 2026-08-22 — Phase E–K 战略层 E–K（Unity 验证 延期）

**做了什么**
- **E** BattleOffer AttackerArmyId/DefenderArmyId；Army vs Army 追击 Adapter；BattleParticipantSnapshot 成员 ID  
- **F** AutoBattle 真实 Character 伤亡；ArmyStackAdapter 派生 downed 统计  
- **G** WarBoard/WarGateService DeclareWar/IsAtWar/CanAttack；Host/BattleOffer 军事门槛  
- **H** CaptureObjective + Node Owner 易主；ControlCore 接入；ArmyFormationNodePolicy 移除 presence 通用路径 → Ch01ScenarioArmyFormationPolicy  
- **I** Alliance/Vassalage/Tribute 占位  
- **J** Captured/Escaped/RetreatingArmy/Landless hook  
- **K** WorldSnapshot Schema v2 + StrategicSnapshotHelper + JsonSnapshotSerializer 战略字段  

**验证**
- EditMode：ArmyPhaseE–KTests 已编写 — **PENDING — UNITY VERIFICATION 延期**  

**下一步**
- Unity Test Runner 全量回归（含 StrategicPhaseTests + 153 链）  

---

## 2026-08-22 — Phase B 最小组军 UI + WorldMap Army 投影（Unity 验证 延期）

**做了什么**
- HostArmyFormPanel + 节点菜单「军团管理」；ArmyUiCommands 薄层  
- ArmyWorldMapPresentation：FormalArmy @ NodeId + Leader 派生头像；AtNode 角色不重复正式显示  
- ArmyFormationNodePolicy：Ch01 无 Owner 时 presence-based 己方 Node  
- ArmyService：AddMember / RemoveMember / ChangeLeader / CollectResidentsAtNode  
- ArmyPhaseBTests（8 条）+ 152/roadmap 状态更新  

**验证**
- Unity Test Runner / Host：**延期**（制作人暂缓）  

**下一步**
- 恢复 Unity 后补跑 ArmyDomainTests + ArmyPhaseBTests + StrategicPhaseTests + Host 手操  
- 等待 **Phase C** 批准  

---

## 2026-08-22 — Phase A Formal Army Domain（Unity 验证 延期）

**做了什么**
- FormalArmy / ArmyService / ArmyMembership / ArmyDomainTests（11 条）  
- StrategicBootstrap Owner 保护；静态复核修复（单真源 / ForceDisband / AtNode-only）  

**验证**
- Unity：**延期**  

---

## 2026-08-22 — 152 审核后小修（仅文档，未编码）

**做了什么**
- 152 rev.2：War(G) 先于 Capture(H)；Legacy Character Travel B–D 过渡 + Phase D 正式退出；Phase A 收紧（自动化为主、Host 仅回归、禁 Army Debug UI）  
- roadmap／overview 状态同步：**Phase A 编码仍未批准**

**未做**
- 任何代码；Phase A 未开始

**下一步**
- 等待制作人 **「正式批准 Phase A 开工」**

---

## 2026-08-22 — 152 战略 Faction / Formal Army / Capture 实现分期计划（仅文档）

**做了什么**
- 只读代码审计结论 + 制作人拍板迁移方向 → 正式实现分期 [152](152-strategic-faction-army-capture-implementation-plan-2026-08-22.md)  
- 路线：Formal Army Domain + Compatibility Adapter（非 WorldPresence 大爆炸）；A–K 可停点；双真源退出表；第一刀推荐 **Phase A**（Domain + Membership，无移动／无战斗改动）  
- 最小更新 roadmap／overview 索引

**未做**
- 任何代码／Scene／测试／Snapshot 改动；**Phase A 未开始**

**下一步**
- 制作人审核 152 → 明确批准 **Phase A** 后方可编码

---

## 2026-08-22 — 153 弥留残留收束 + 自动战宏观头像 + 接战／追击修复（已编码）

**做了什么**
- 自动战胜后 `EnsureMacroRemnantSpawns`：宏观立刻刷弥留／尸体个体 + `WorldPresence`；隐藏聚合 `ArmyStack` 标记  
- 战损语义：`IncapacitatedMemberCount`／`CorpseMemberCount`；处决留尸体不 `Armies.Remove`  
- 接战强制名单：`CollectViewParty(mandatoryLiving)` 仅行动决定人 + 半径内弥留／尸体；探望记录派出名单  
- 接战窗撤退：`ClearPursuitForAgents`；自动战胜 `ClearPursuitForEngagedKeepEnRoute`  
- 再进 LocalMap 倒计时 preservation；`EstimateAutoWinPercent` 调整；EditMode 回归测例  
- 过程文档 [153](153-lingering-remnant-macro-presentation-2026-08-22.md)；GitHub／飞书同步

**未做**
- 2A 正式 Faction／Diplomacy／War／Capture 代码（仍为 Prototype `ArmyStack`）  
- 手操签收 153 清单

**下一步**
- 手操验 153 → 制作人批准 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) 实现分期  
- 见 153 §6 与 devlog 外交条目

---

## 2026-08-22 — 战略势力层规则第二轮补充（仅文档，未编码）

**做了什么**
- 制作人拍板 9 条补充规则，写入 [2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md) §1.4／§6／§10／§19.1／§20／§37
- 同步 [ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)、glossary（FactionId／ArmyMembership／GarrisonedArmy／RetreatingArmy 等）、`24`／`26`／`28` 注记

**关键规则**
- Army 编组仅能在己方 Node；禁止跨 Faction 混编；驻扎不自动解散
- 全系统统一 FactionId；无战后系统保护期；独立 Faction 最多 1 个 Alliance + 成员战争绑定
- Capture 全部完成后可「结束战斗」；残余守军 Captured／Escaped → RetreatingArmy

**未做**
- 任何代码改动；概率公式；RetreatingArmy AI

**飞书**
- 已 provision + 同步本轮战略文档（2A、ADR-0024、glossary、24/26/28、roadmap、devlog 等）；全量同步 28 篇历史文档因飞书权限 forBidden 失败（与本轮无关，需手动分享应用编辑权）

---

## 2026-08-22 — 战略势力层设计文档同步（仅文档，未编码）

**做了什么**
- 制作人与设计讨论完成战略 Faction / Army / Diplomacy / Vassalage / Alliance / War / Capture 基础框架
- 新增 [2A 势力、军队、外交与战略占领](../20-systems/2A-factions-armies-diplomacy-and-capture.md) 系统设计真源
- 新增 [ADR-0024](43-decisions/ADR-0024-real-cultivators-and-army-strategic-model.md)：修士 = 持久 Character + Army 载体；部分 supersede ADR-0008
- 修订 `27`／`34`／`26`／`24`／`28`／`33` 注记；`113`／`138`／`139`／`140` 增加 Prototype vs target-model 区分
- 更新 glossary／roadmap／reading-guide／overview

**未做**
- 任何代码、JSON、Scene、测试改动
- 外交 UI、占点、Army 编组、AI、Snapshot 实现

**下一步**
- 制作人审核文档 → 明确「可以开工」后再进入实现

---

**做了什么**
- `base:map_world_node_stub` 扩为 150×80 空场；去掉歇脚树装饰  
- [151](151-encounter-stub-map-150x80-2026-08-21.md)

---

## 2026-08-21 — 150 批 3：残留再进走接战 Offer

**做了什么**
- `TryBuildOfferForLingeringBattlefield`；弥留菜单／残留栈再攻统一弹 Offer  
- 残留 Offer 使用 `LingeringLocalMapId`；补 `LingeringBattlefieldPartyService.cs.meta`  
- EditMode 测例；[150](150-lingering-battlefield-batch3-offer-2026-08-21.md)  
- 手操验收跳过（Unity 占用／环境未就绪）  
- 飞书／GitHub 同步

---

## 2026-08-21 — 149 批 2：Core 下沉 + 探望到站

**做了什么**
- 新增 `LingeringBattlefieldPartyService`；`EnterLingeringBattlefield` Core 校验  
- `PendingLingeringVisitIncapId` + 到站自动开进入菜单  
- [149](149-lingering-battlefield-batch2-2026-08-21.md)

---

## 2026-08-21 — 148 收束：批 1 + 接战一致性 + 删 JoinOngoing

**做了什么**
- `HostWorldMapPanel` 批 1；`ExecuteAttackStack` 仅 Pursuit 到站弹接战  
- `BattleOfferService.TryPromoteNextQueuedOffer` 人未到只追击  
- 删除 `JoinEngagedMembers`／JoinOngoing UI；测例更新  
- 收束 [148](148-worldmap-linger-incap-ux-2026-08-21.md)；飞书同步；提交（手操跳过）  
- 下一步：[149 批 2](149-lingering-battlefield-batch2-2026-08-21.md)

---

## 2026-08-21 — 148 大地图弥留交互与点击修补（待手操验）

**做了什么**
- `HostWorldMapPanel`：弥留左／右键分工、`CollectLingeringViewParty`、敌军吸附与命中优先级  
- 新建 [148](148-worldmap-linger-incap-ux-2026-08-21.md)；飞书 provision＋同步  

---

## 2026-08-21 — 收束 147＋飞书／GitHub（接战点／弥留残留）

**做了什么**
- 扩写 [147](147-battlefield-linger-no-teleport-2026-08-21.md) 收束全文（对齐 `eece220`）  
- 更新总览／通读／62／路线图／feishu-map；飞书 provision＋同步；推 GitHub  

---

## 2026-08-21 — 接战点无瞬移＋弥留残留战场

**做了什么**
- 战后参战者一律落 BattleAnchor，禁止瞬移回家  
- 有弥留则保留 Encounter；大地图可再攻击／查看进入  
- 未处决自动战＝全员弥留；再进刷弥留怪；修换路瞬移／再进跳荒村  
- 大地图支援半径滑块（默认 0.25）；ADR-0023 补丁：结束战斗≠销毁战场  
- 提交推送 `eece220`

---

## 2026-08-21 — ADR-0023／146 手操签收

**做了什么**
- 制作人手操确认 145／146 清单通过；文档状态改为已签收  
- 下一刀待选：占点／外交，或 Snapshot 纳入 Strategic／冻结态

---

## 2026-08-21 — 收束 146＋飞书／GitHub（ADR-0023 Host 打磨）

**做了什么**
- 新建 [146](146-adr0023-host-ux-polish-2026-08-21.md)：支援世界坐标半径、手动非强制结束、自动结算弹窗、山匪可见性、出队修复、CS0128
- 修订 [145](145-adr0023-phases-af-acceptance-2026-08-21.md)；更新总览／通读／62／glossary／devlog
- 飞书 provision＋同步；提交推送 `main`

---

## 2026-08-21 — ADR-0023 Phase A～F 连续落地

**做了什么**
- Phase A：ClockFreeze／Modal／禁 Tick  
- Phase B：ParticipantSnapshot＋ReinforcementRange  
- Phase C：Offer 可选支援勾选 UI  
- Phase D：PostBattle＋PreBattle 还原（防瞬移）  
- Phase E：BattleInterruptQueue 串行  
- Phase F：`Adr0023BattlePhasesTests`＋验收文档 [145](145-adr0023-phases-af-acceptance-2026-08-21.md)  
- 战中 JoinOngoing 改为排队（旧测已改期望）

---

## 2026-08-21 — ADR-0023：Manual Encounter 冻结战略 WorldTick

**做了什么**
- 新增 [ADR-0023](43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)；影响审计与分期 [144](144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)
- 修订 `21`／`23`／`33` 补丁／138§3.1／139／140；[143](143-localmap-worldmap-interaction-behavior-spec-2026-08-20.md) 标 superseded（废回战场／战斗中切图）
- 更新 glossary／总览／62／通读／roadmap／ADR 索引／0018 补充
- **Phase A：** `StrategicClockFreeze`；Offer／Manual／PostBattle；`SimulationLoop`／Host 禁 Tick；Modal 禁战略出行／进其他场景；薄「结束战斗」；EditMode 冻结断言

**废弃默认：** 战斗期间战略世界继续跑；FieldCleared 后挂起 InEncounter 再「回战场」

---

## 2026-08-20 — 文档 143：LocalMap／大地图进出交互行为方案

**做了什么**
- 新建 [143](143-localmap-worldmap-interaction-behavior-spec-2026-08-20.md)：按 **1A＋2A** 写进出状态机、操作目录、决策表、§7.1「一人进村再回战场」泳道；**待确认后再改代码**
- 更新总览／62／通读／139／140 交叉引用；飞书 provision＋同步
- 说明：不做 Figma；可视化用行为树／Mermaid／ASCII

---

## 2026-08-20 — 收束文档 142＋飞书／GitHub

**做了什么**
- 收束 [142](142-auto-battle-incap-corpse-2026-08-20.md)（自动战结算＋弥留／尸体＋状态 UI 角标＋大地图菜单修复）；更新 23／62／总览；飞书 provision＋同步；提交推送
- **未手操验收**，仅 EditMode 测试

---

## 2026-08-20 — 自动战结算＋弥留／尸体

**做了什么**
- `CombatLifeStateService`：0 血→弥留；补刀→死亡+尸体；按修为 2／3／5 游戏日 decay
- `AutoBattleCasualtyService`：自动战胜／败伤亡；处决 vs 击溃；接战 checkbox
- LocalMap／Host 表现：弥留停战、补刀、角标；`HostFormalHud` 底栏左上角
- 大地图左键节点菜单锚定节点框（去掉镜头居中抽搐）

---

## 2026-08-18 — 收束文档 141＋飞书／GitHub

**做了什么**
- 收束 [141](141-pursuit-stick-and-multi-melee-2026-08-18.md)（追击贴敌＋LocalMap 多选近战）；更新 140／139／62／总览／通读；飞书 provision＋同步；提交推送

---

## 2026-08-18 — 大地图攻击持续贴敌军

**做了什么**
- 追击每 tick `SyncPursuersToStack`：敌军挪位仍改道贴当前宏观位置，重合再弹接战
- `StartTravelToStackAnchor` 对道路行军中的栈也按显示进度追，不只追 Dest 节点

---

## 2026-08-18 — LocalMap 多选一起攻击

**原因**：`HostNpcMeleeAssault` 只记一名攻方；后到的 `Begin` 顶掉先前的人

**修复**：多名己方可同时攻击同一目标；右键攻击对当前选中全员下指令

---

## 2026-08-18 — 清大世界旧实现＋138 飞书换链

**做了什么**
- 删大地图「外交」面板／敌军「交谈」占位；删 `CaptureNodeForPlayer`、`TryResolveEncounterVictory`、未用 `CollectAtNodeParty`、`DepartingLocalMap` 运行时分支
- 旧 138 飞书无写权限 → 新建文档并换链：https://my.feishu.cn/docx/FSOcd9I2oosbBWx82CXcKMkZnod

---

## 2026-08-18 — 收束文档 140＋飞书／GitHub

**做了什么**
- 收束文档 [140](140-world-map-rts-battle-return-rollup-2026-08-18.md)；更新 139／62／总览／通读；飞书 provision＋同步；提交推送

---

## 2026-08-18 — 打完回不了青石荒村

**原因**：清场后仍 `InEncounter`，可达性用「较近端」当 BFS 起点；半路打完较近端常是荒村，点回荒村被当成「已在」→「无法沿宏观道路到达」。与外交无关。

**修复**
- 路中／InEncounter：当前道路两端可直达
- `ResolveAnchorNodeId` 覆盖 InEncounter
- 暂清节点 Owner、大地图不再按势力染色

---

## 2026-08-18 — 后到增援只弹加入战斗

**原因**：先到点「手动战斗」时 `ClearPursuit` 清掉全员（含路上第二人）标记 → 后到误弹到站查看

**修复**
- `ClearPursuitForEngagedKeepEnRoute`：只清进场者，按身上标记重建追击名单
- `BeginPursuit` 同栈合并名单
- 大地图点选头像时状态栏提示颜色含义（橘=接战／蓝=路上／灰绿=驻留）

---

## 2026-08-18 — 追击绝不弹到站 + 去掉战略敌对门槛

**做了什么**
- 角色挂 `CombatPursuitStackId`：攻击上路必标记；到站跳过「是否查看」，只弹接战
- `BeginPursuitToStackAnchor` 强制 `BeginPursuit`，到位立即尝试 BattleOffer
- 战略敌对不再挡进场／攻击确认；外交默认中立

---

**做了什么**
- 进场景：有我方在场即可进（含敌占荒村），不再被外交敌对挡住
- 攻击／追击：不弹到站「是否查看」；接战弹窗优先盖过到站提示

---

**原因**
- 进 Encounter 时清掉 TravelTicks，路进度变成 0 → 无法回荒村（误判已在出发端）
- 删敌军栈前未快照路点；路锚去非端点时误走瞬移逻辑

**修复**
- 删栈前 `SnapshotEngagedRouteFromStack`；`PreserveRouteProgressForEncounter`
- 释放进度丢失时用 0.5；路锚先走到端点再续走；短途至少 8 tick

---

**产品（1C）**
- 中途可开大地图查看、可派人增援；参战中不可宏观离开
- 敌清空：无结算弹窗、不自动开大地图；画面留战场 LocalMap；`FieldCleared` 后可大地图下令移动

---

## 2026-08-17 — 大地图选栈吸附 + 加入战斗真进场

**做了什么**
- 敌军栈点选：扩大吸附半径；已选己方时左键点栈保留选中并弹出攻击菜单
- `JoinEngagedMembers`／`RelocatePartyOnEncounterMap`：增援一律 `InEncounter`（修复到站后仍 AtNode／路锚导致看不见）

---

**做了什么**
- 遇敌：沿用 `BattleOffer`（自动／手动／撤退）；追击抵达仍由 `StrategicPursuitService` 弹出
- 到站：`ArrivalNotice` — 最终目的地才弹；文案「XXX 抵达「YY」」；**去查看**→开大地图并选中到站者；**暂不查看**关掉
- 接战优先于到站，不叠弹

---

## 2026-08-17 — 清残留 + 全员上路视线留在 LocalMap

**做了什么**
- 再删：`MarkDepartingLocalMap`／`CommitTravelAfterLocalExit`／`EnsureSpawned`／`TryUnloadActiveLocalMapIfNoFriendlyParty`／`NotifyPlayerOverride` stub／「未出行」头顶字
- 出行后**不再卸图、不 FrameCamera**：只 Despawn 上路者，画面留在当前 LocalMap
- `ApplyPartyWorldNodePresentation`：目标图暂无我方、或焦点图空但 ActiveMap 仍在 → return，禁止误卸把视线带走

---

## 2026-08-17 — 大地图重切：纯 RTS（删边缘离场）

**产品**
- 放弃「走到地图边缘再上路」；问题过多，整段 Host 离场链删除
- 大地图：选人下令 → **立刻** LocalMap 消失 → 宏观移动；路上再点别处 = 改目标打断
- 遇敌进 LocalMap／后到加入：之后再加细，本刀不动接战弹窗主路径

**做了什么**
- 重写 `HostWorldTravelDeparture`（约删光边缘／Force／Prepare）
- 删 `OrderEntityToWorldPointForDeparture`
- 确认窗不再传 `useLocalMapExit`
- 更新 139

---

## 2026-08-17 — 「有的能出行有的不能」：Departing 被滤掉 + 失败卡死

**原因**
1. `CanReceiveTravelOrder` 不含 `DepartingLocalMap` → 正在走边缘的人在大地图里**点不动／下不了新令**
2. 边缘失败后硬禁止上路 → 部分人既不走边缘也不上路，像「不能出行」

**修复**
- 可下令模式加入 `DepartingLocalMap`；改令先 `CancelDeparture`
- 边缘彻底失败时再宏观保底上路；状态栏区分「已出发／未能出行」人数

---

## 2026-08-17 — 根因：在场者离场失败会静默「直接上路」

**为什么会出现「有人走边缘、有人直接出发」**
- 旧循环：`TryStartEdgeExit` 一失败（无 View／Mark 被路权挡住／寻路失败）→ **立刻** `StartAgentTravel`+Hide
- 主角常成功，队友常失败 → 看起来像随机分裂

**这次硬规则**
- `MustEdgeExit`（在 ActiveMap 节点或场景里已有 View）→ **禁止**回退宏观上路
- 失败走 `TryEmergencyEdgeExit`（直线／贴边瞬移再回调）
- `MarkDepartingLocalMap` 不再因路权失败而挡离场标记

---

## 2026-08-17 — 离场链收束：RTS 大地图 + 保留边缘离场

**产品确认**
- 大地图 = RTS（随时下令／打断）；遇敌才进战斗 LocalMap；后到可加入
- 人在节点场景时：**仍要走到边缘再上路**（选项 2）

**做了什么**
- 重写 `HostWorldTravelDeparture`：删掉 Force／双路径／IssueOne Stop 旁路，只留 `TryStartEdgeExit`
- 在场全员：补刷坐标 + EnsureSpawn → 走边缘（寻路失败则直线）→ 上路
- 不在场：直接宏观移动；无人留下才卸图

---

## 2026-08-17 — 多人离场：禁止「寻路失败就直接上路」

**根因**
- 在 Active LocalMap 上的队友若无 View／坐标在墙外／寻路失败，旧逻辑会**静默回退** `StartAgentTravel`+Hide，看起来像瞬移上路；主角常有合法 View 所以仍走边缘

**做了什么**
- 在场者：`ShouldAttemptLocalMapExit` → **必须**走边缘；失败则直线离场，不再宏观跳过
- `OrderEntityToWorldPointForDeparture`：不发 Stop 打断同批；寻路失败改直线
- 离场前强制重刷表现坐标并贴格；整批 `_suppressOverride`

---

## 2026-08-17 — 多人离场：全员走边缘，不再只有主角

**做了什么**
- 焦点节点上 AtNode／Departing 的我方一律可见（不再因 LocationId 过期隐身）
- 离场前 `PrepareAgentsForLocalMapExit`：补表现坐标 + `EnsureSpawned`
- 多人离场边缘点加队形错开，降低寻路挤死回退成「直接上路」

**判断与理由**
- 旧逻辑 `NeedsLocalMapExit` 要求已有 EntityView；队友常无 View → 直接宏观移动

---

## 2026-08-17 — 卸图：仅当该 LocalMap 无我方角色

**做了什么**
- 离场只 Despawn 离开者；**当前 Active LocalMap 上仍有我方**时保持装载
- **无人留下**才 `UnloadActiveLocalMapPresentation`（清空 ActiveMap／视图／WalkGrid；空遭遇顺带清刷怪与 Engaged）
- `ApplyPartyWorldNodePresentation`：目标图上无我方且非即将刷遭遇 → 直接卸图，禁止空图挂载

**判断与理由**
- 击杀已不卸图；多人分遣时一人出门不应把还在村里的人连图一起卸掉

---

## 2026-08-17 — 遭遇战击杀不再自动胜利／弹大地图

**做了什么**
- 打死遭遇敌军只同步栈人数；不自动胜利结算、不卸图、不 `Open` 大地图
- 离场／回大地图改由玩家自行操作

---

## 2026-08-17 — 修复「出不了门」：离场失败必须回退宏观移动

**做了什么**
- `MarkDepartingLocalMap` 不再要求目标与当前节点**直达相邻**（多段路径可离场）
- 走边缘失败／标记失败时**回退** `StartAgentTravelToTarget`／追击上路，不再整单取消
- 边缘朝向用 BFS 下一跳，避免指错方向

**判断与理由**
- 强制 `useLocalMapExit` 后，非相邻目标或边缘寻路失败会 `continue` 且无上路 → 表现为完全出不去

---

## 2026-08-17 — 大地图攻击对齐普通派遣离场

**做了什么**
- 右键他方栈：敌对＝攻击／详情；非敌对＝攻击／交谈／详情（去掉跟随）
- 攻击：登记追击后直接 `BeginPursuitToStackAnchor`（场景走边缘离场→大地图追击），不再二次确认、不关大地图
- 普通节点／道路出行确认也强制 `useLocalMapExit: true`
- 先到 BattleOffer 手动战；后到「加入战斗」流程沿用

---

## 2026-08-17 — 重做路上遇敌（删同路自动接战）

**做了什么**
- 删除 `CheckBattleCollisions`／`CheckRouteCollisions`（同路即「行军遭遇」）
- 新增 `StrategicEngageRules`：须节点／道路进度真正重合才可接战
- 接战弹窗仅：**主动攻击**或**追击抵达**；普通赶路过敌不弹
- 更新 [138](138-world-strategic-battle-offer-plan-2026-08-17.md)／[139](139-world-map-rts-orders-2026-08-17.md)；补 EditMode 回归

**判断与理由**
- 旧逻辑「同 Route 非锚点栈即弹」+「行军中同路即 CanEngage」导致过路遇敌；与可自由移动、无暗雷产品定冲突

---

## 2026-08-17 — 删除 Route danger 暗雷

**做了什么**
- 删除 `RouteEncounterService`、`RouteEncounterPending`、路遇弹窗 UI 与相关 EditMode 测试
- 接战仅保留 ArmyStack + `BattleOffer`（同路碰撞／追击抵达）
- 更新 [113](113-world-graph-local-map-architecture-revision-v0.1.md)、[138](138-world-strategic-battle-offer-plan-2026-08-17.md)、SCHEMA；飞书同步

**判断与理由**
- 产品已定：战略遭遇必须对应大地图可见敌军，随机「路遇险情」与全战式接战语义冲突

## 2026-08-17 — 138 战略层 Phase 0～4 落地

**做了什么**
- Core：`StrategicBoard`、`BattleOffer`、外交四档、`ArmyStack`、日界 AI 派兵；`SimulationLoop` Travel 后接 `StrategicTravelDriver`
- Host：`HostStrategicInterruptPresenter`（接战弹窗）；`HostWorldMapPanel` 开 M 暂停、删 `DriveTravelWhilePaused`、Space／倍速、归属色、外交侧栏、Army 图标
- EditMode：`StrategicPhaseTests` 通过

**判断与理由**
- 按 138 分期验收；统一世界时间（§3.1）在 Host 层落地，避免战略层与 LocalMap 双时钟

**注：** 初版 Phase 0 曾含 Route danger roll（暗雷），已于同日后续删除。

## 2026-08-17 — 138 增补：统一世界时间＋无文明式复杂度

**做了什么**
- [138](138-world-strategic-battle-offer-plan-2026-08-17.md) §3.1：开大地图自动暂停；Space 全局继续；倍速与 LocalMap 一致；接战强制暂停
- §9 明确无科技树／兵种克制；飞书同步

**判断与理由**
- 大地图要看见派兵移动，时间须能流；开图先停给决策时间，与即时制不矛盾

## 2026-08-17 — 大地图战略层＋接战弹窗设计

**做了什么**
- 新建 [138 战略接战计划](138-world-strategic-battle-offer-plan-2026-08-17.md)：帮派／外交／占点分期、全战式 BattleOffer（战力对比／自动胜率／自动或手动 LocalMap）
- 更新 [113](113-world-graph-local-map-architecture-revision-v0.1.md) Phase G、[62 现状](62-project-status-2026-08-01.md)、总览；飞书同步

**判断与理由**
- WorldGraph 底座已有，下一刀应是「战略遭遇怎么打」而非再扩 LocalMap 战斗细节；接战弹窗统一自动／手动入口

## 2026-08-17 — 远程弹道文档＋飞书／GitHub

**做了什么**
- 补全 [134](134-spirit-veil-ranged-normal-attack-2026-08-16.md) 统一弹道专节；更新 [137](137-skill-mastery-farm-veil-chop-rollup-2026-08-17.md)／`23`
- 飞书同步相关页；推 GitHub（弹道代码＋文档）

---

## 2026-08-17 — 统一远程攻击弹道特效

**做了什么**
- `HostMeleeStrikeVfx.PlayRangedBetween`：青色光核沿攻→守飞行，抵达后爆闪＋受击闪白（纱衣普攻及日后远程共用）
- 程序化 `RangedProjectileSprite`；近战挥砍不变

---

## 2026-08-17 — 收束文档 137＋飞书／GitHub

**做了什么**
- 新建 [137](137-skill-mastery-farm-veil-chop-rollup-2026-08-17.md)（熟练／纱衣／田区／砍树＋冲击确认验收）
- 更新 [131](131-skill-mastery-study-ritual-2026-08-16.md)／[135](135-world-object-inspect-and-tree-chop-2026-08-16.md)／[136](136-farm-field-zone-labor-2026-08-16.md)／总览／通读／62
- 飞书 map 增补 130～137；provision＋同步；推 GitHub

---

## 2026-08-17 — 熟练冲击确认窗（成功率只在询问时显示）

**做了什么**
- 斗技／功法「冲击下一档」先确认：是否突破＋成功率＋材料；结果弹窗不再写成功率
- `EvaluateMasteryBreakthroughChance`＋`HostSkillMasteryPanelUi.DrawBreakthroughConfirm`

---

## 2026-08-17 — 砍树掉木修复＋中树10／大树40

**做了什么**
- 产量：中树 10、大树 40（小树 3）；伐倒先入包再销毁，避免读已毁对象
- 树不再注册 Work 热点（右键砍伐，不再误成林区劳动）
- 背包满时提示粗木未入包

---

## 2026-08-17 — 斗技／功法两级熟练度 UI（列表＋详情）

**做了什么**
- 斗技：一级竖列表（圆标＋名称摘要）；点进二级熟练度页（各档效果／冲击材料／进度）
- 功法：境界面板点当前功法卡片，进同款二级熟练度页（无多功法列表）
- 共用排布：`HostSkillMasteryPanelUi`

---

## 2026-08-17 — 斗技学习黄条对齐境界突破＋明确学习成功率

**做了什么**
- 删掉屏幕中上独立参悟框；学习／熟练冲击改用底栏状态板上方黄条（同突破）
- 文案与预估统一写成「学习成功率」；战斗释放不掷此骰
- 文档：[131](131-skill-mastery-study-ritual-2026-08-16.md)

---

## 2026-08-17 — 删掉绿草上的幽灵农田／药田工区

**做了什么**
- 去掉 `ResolveWorkBand` 旧大片农田／药田色带；Legacy 麦垄／药畦热点一并删
- 左键检视：farm／herb／grain 工区只点在耕种格上才出「工区·农田／药田」
- 地点圆心改为多块田的平均中心（`MapLayoutPresentationSync` + places JSON）
- 文档：[136](136-farm-field-zone-labor-2026-08-16.md)

---

## 2026-08-16 — WorldGraphEditor 可视化节点拖动

**做了什么**
- 去掉纯表格编辑；画布拖节点／连线；右侧属性
- 外形与 `HostWorldMapPanel` 对齐（128×44 标签、棕线、Y 向上）
- 文档：[128](128-world-graph-editor-usage.md)

---

## 2026-08-16 — 农田只画一遍：物件页保留，分区去掉药田／农田区

**做了什么**
- MapEditor 分区板去掉 `zoneHerb`／`zoneGrain`；物件 `herbField`／`grainField` 即整片可耕作区
- 参考图删掉重复区标；Host 目录仍兼容旧图 overlay
- 文档：[112](112-map-editor-usage.md)

---

## 2026-08-16 — NPC 日程 Labor 接入田区走格农作

**做了什么**
- `HostFarmFieldLabor.SyncNpcScheduleFarmers`：`WorkAction`（Labor）＋ farm／herb／grain 工区＋有田格 → 自动走格
- NPC 收获进据点库存；玩家仍进背包；离开 Labor 自动停
- 文档：[136](136-farm-field-zone-labor-2026-08-16.md)

---

## 2026-08-16 — 田区自动农作（整片区＋格上作物）

**做了什么**
- 药田／农田按 location 成区；交互后自动选格播种／照料／收获／清理
- 格状态着色；自然缓慢生长；停止／移动中断
- 文档：[136](136-farm-field-zone-labor-2026-08-16.md)

---

## 2026-08-16 — 非玩家筑基交战自动开纱衣

**做了什么**
- `TryAutoActivateForNonPlayer`：Npc＋筑基＋灵力够 → 交战 `Begin` 自动开纱衣
- 杂役主管改为筑基、灵力 180（内容数据）
- 玩家仍仅 F2 手动；农田区域劳作下一刀

---

## 2026-08-16 — 世界物只读况栏＋砍树掉木

**做了什么**
- 左键点空统一只读物况栏（主管府／住房／工区／耕种格／树墙）；无指令球
- 树／墙带耐久；砍完树掉粗木并销毁；右键／F8 可砍拆
- 文档：[135](135-world-object-inspect-and-tree-chop-2026-08-16.md)

---

## 2026-08-16 — 斗气纱衣改 F2，并入人物面板指令横排

**做了什么**
- 去掉 R；选中筑基后底栏「战斗」旁出现 `F2 纱衣`
- 事件流水显隐改 Ctrl+F2，避免抢键

---

## 2026-08-16 — 斗气纱衣：筑基远程普攻姿态

**做了什么**
- Core：`SpiritVeil*`——筑基固定灵力召唤；开则普攻射程 7，伤害／攻速不变；空灵力／交战结束卸下
- Host：`R`／底栏纱衣；追击按姿态射程；远程青色外放特效
- 文档：[134](134-spirit-veil-ranged-normal-attack-2026-08-16.md)；更新 `22`／`23`

---

## 2026-08-16 — 功法／斗技轻量编辑器＋清理非正式斗技样例

**做了什么**
- 新增 `ManualArtEditor`（`启动-ManualArtEditor.cmd`）；Shared 认 `combatArt`／`mastery`
- 删无入口的 `art_spirit_strike`；内置斗技注册缩成 JSON 缺失时的薄保底
- 文档：[133](133-manual-art-editor-and-cleanup-2026-08-16.md)

---

## 2026-08-16 — 熟练度配置化：修 bug＋校验

**做了什么**
- 修：`ProgressRequiredToNext` 误删；熟练进度按配置表同步；斗技直授写入 profile；`teachesArtId`→`combatArt` 引用校验
- 增：`SkillMasteryAbsoluteTierTests`；JSON／引用静态检查通过

---

## 2026-08-16 — 熟练度配置化：每档绝对值（不连乘）

**做了什么**
- `mastery.tiers`／`breakthroughs` 进功法 JSON；新增 `combatArt`＋`combat_arts.json`
- Core 按档读绝对值（修为速度／伤害倍率）；去掉 EffectMult 连乘
- SCHEMA／[132](132-skill-mastery-config-absolute-tiers-2026-08-16.md)

---

## 2026-08-16 — 功法／斗技：蓄势研读＋熟练度（入门→小成）

**做了什么**
- Core：`SkillMastery*`；点学改蓄势掷骰入门；打坐／释放／灌注涨熟练；材料突破小成；效果倍率
- Host：`HostSkillStudyRitual`；境界面板／斗技面板灌注与冲击；Snapshot 读写
- 文档：[131](131-skill-mastery-study-ritual-2026-08-16.md)

---

## 2026-08-16 — WorldGraph Host 出行／进场景关大地图／场景隔离

**做了什么**
- 进场景：`Close()` 大地图 + `ApplyPartyWorldNodePresentation(closeWorldMap: true)`
- 场景隔离：Legacy 药畦／色带／灰盒回落仅限荒村；Preferred 缺失不刷荒村砖
- 收束文档 [129](129-world-graph-host-travel-scene-isolation-2026-08-16.md)；飞书 provision＋同步；推 GitHub

---

## 2026-08-16 — 宏观出行：确认／走到边缘／途中不可进场景

**做了什么**
- 右键目标节点 → 确认弹窗（打断当前行为）
- 确认后：人在当前 LocalMap 则走到地图边缘再消失，再上大地图慢移
- 途中 LocalMap 不现身；头像菜单可「查看信息」，到站后可「进入场景」

---

## 2026-08-16 — 大地图 30 节点＋组队移动（无通行令）

**做了什么**
- 去掉通行令／permit UI 与旅行门槛
- Ch01 WorldGraph 扩至 30 节点；仅荒村有 LocalMap
- `WorldPresenceBoard`：每人宏观位置；大地图勾选组队点相邻节点出发

---

## 2026-08-16 — WorldGraph D／F＋localPlaceSet

**做了什么**
- 换 Node：`ApplyPartyWorldNodePresentation` 卸／装 LocalMap；占位节点清空实体图
- Ch01 地点表迁 `localPlaceSet`（删 `ch01_reference_region`）；Scenario 改 `openingLocalPlaceSetId`
- WorldGraphEditor（节点／道路表）；`启动-WorldGraphEditor.cmd`

**下一步**
- 手操验：地图旅行换图；编辑器改点边保存

---

## 2026-08-16 — WorldGraph A～C：旅行＋地图按钮

**做了什么**
- Core：`WorldGraphBoard`／`PartyWorldPresence`／`WorldTravelService`（关隘 traversalRequirements）
- Bootstrap 灌图；Ch01 含青石关；`permit:guanai` 才能过关去矿山
- Host 顶栏「地图」＋M；废默认 Y 宏观 Travel；region 标明仅村内地点表
- EditMode：`WorldTravelPhaseBTests`

**下一步**
- D：换 Node 卸／装 LocalMap；F：WorldGraph 编辑器

---

## 2026-08-16 — WorldGraph 阶段 A（数据＋Loader）

**做了什么**
- `type=worldGraph`：SCHEMA／Definition／Loader／引用校验；`WorldGraphs/ch01_world_graph.json`（荒村绑现有 map，矿山／林间／渡口占位）
- EditMode `WorldGraphPhaseATests`；旧 `worldRegion` 并行不改 Demo Host

**判断与理由**
- 战略层（含未来宗门运营）需要节点图底座；A 只立数据，旅行／大地图 UI／卸图另开 B～D

**下一步**
- 阶段 B：`StartTravel`／时间推进／到站 EnterNode

---

## 2026-08-16 — 收束文档 127＋飞书／GitHub

**做了什么**
- 新建 [127](127-defeat-teleport-cave-camera-spawn-table-gui-2026-08-16.md)；更新总览／通读／62／126；飞书 provision＋同步；推 GitHub

---

## 2026-08-16 — 击败瞬移／进出洞相机＋刷怪表 GUI

**做了什么**
- 击败后只 Despawn 尸体，不再 Rebuild 整图（根因：Rebuild 把交战坐标重置成地点中心）
- 走动写 PresentationOverride；进出洞换地点清 override；镜头优先对准己方
- MapEditor「编辑／新建刷怪表…」GUI；重发 MapEditor

**判断与理由**
- 生命／背包等 Core 状态进出洞本就同一世界，不靠存档；缺的是表现坐标继承

---

## 2026-08-16 — 收束文档 126＋飞书／GitHub

**做了什么**
- 新建 [126](126-control-core-chase-spawn-zone-rollup-2026-08-16.md)：府近战／追击／刷怪区表
- 更新总览／通读／62／feishu-map；飞书 provision＋同步；推 GitHub

---

## 2026-08-16 — MapEditor 刷怪区＋刷怪表（无敌人编辑器／无钉点）

**做了什么**
- `type=spawnTable`＋`placement.kind=spawnZone`；开局 `SpawnZoneApplier` 引用角色定义刷人
- 洞府残影改走刷怪区；名册／residentNpc 拿掉；MapEditor 分区页可画刷怪区
- 近战追击：到位待命不再误脱战（并行）

**判断与理由**
- 敌人＝角色数据；工具只编「哪里出／出哪张表」，避免双 schema

---

## 2026-08-16 — 近战追击不再因到位待命而脱战

**做了什么**
- 根因：出距追击到位后 `HoldStandby`→`Stop`→`Disengage`，追一下就停战
- 交战攻方到位跳过待命；追击重寻路更快并保持定住目标

**判断与理由**
- 近战应对移动目标持续贴身；玩家显式 Stop／右键地面仍脱离

---

## 2026-08-16 — 主管府突击对齐正式近战

**做了什么**
- 删除 `TestMeleeDamagePerHit`／固定每秒伤；`ControlCoreService.ApplyStrikeFromAttacker`＝攻−防/2
- `HostControlCoreAssault`：近战间隔＋挥砍特效；破门站占不变
- 主管 NPC 仍走 `HostNpcMeleeAssault`（右键攻击）；文案／[121]／[125] 同步

**判断与理由**
- 地图互砍已正式化后，府建筑不应再留临时常量伤

---

## 2026-08-16 — 彻底去掉机缘点静默学功法

**做了什么**
- `CultivationAttemptGate`：入定不再 Learn（已在 `f71063c`）
- `OpeningScenarioApplier`：修士 NPC 开局也不再 `offeredManualId`→LearnManual（仅 Discover 地点）

**来历**
- 该机制来自 **2026-08-01 VS0.3**（`8893906`）：Gate「已知可修炼 Site → 顺带学点上功法」，当时验收文档写明「青云诀经机会获得」
- 不是本轮偷偷加的，是旧竖切保底；与后来「秘籍显式学习」冲突，故清掉

---

## 2026-08-16 — 战斗／斗技／体魄验收收束＋功法不再保底

**做了什么**
- 未学功法统一「还没有学功法」；`CultivationAttemptGate` 去掉入定静默学青云诀
- 收束文档 [125](125-combat-arts-physique-acceptance-rollup-2026-08-16.md)；更新总览／通读／62；飞书 map＋同步；推 GitHub

**判断与理由**
- 「青云诀残篇」来自入定顺带 Learn，不是单纯 UI 占位；与秘籍显式学习冲突

---

## 2026-08-16 — 选中面板右侧斗技栏（1–6 可点放）

**做了什么**
- 左键展开角色底栏：主面板右侧竖列显示装备斗技，角标 1–6，标题「斗技·已学N」
- 己方左键点击主动格＝释放（与快捷键共用 `HostCombatSkillBar`）；底栏横向技能条去掉以免叠层

**判断与理由**
- 对齐修仙模拟器「状态板右侧技能位」；键位与技能名同格可见

---

## 2026-08-15 — 体魄≠血条：拆开 Physique／生命

**做了什么**
- 新增 `AttributeId.Physique`（体魄）；`MaxHp` 显示一律改「生命」
- 概况／人物／境界／突破／属性列表统一走 `HostAttributeLabels`
- 角色 JSON 补 Physique；突破阶梯附带少量体魄；SCHEMA／术语表对齐

**判断与理由**
- 术语表本就写体魄＝肉身属性；此前把 MaxHp 标成体魄是误用

---

## 2026-08-15 — 修互砍：战斗组件未进 Entity 白名单

**做了什么**
- `Entity` 白名单补上 `CombatVitals`／`CombatArts`／`EncounterLink`／`CharacterBio`
- 根因：`AddComponent` 失败被静默忽略 → 无生命池 → `ApplyStrike` 失败 → **无掉血、无攻击特效**

**判断与理由**
- 特效绑在命中成功路径上；组件挂不上时两边一起哑火

---

## 2026-08-15 — 1–6 专供斗技；卸掉其它数字快捷键

**做了什么**
- `HostCommandBridge`：劳动／休息／观察／修炼／社交／分工等不再绑 0–9（默认 None；场景序列化一并清零）
- `DemoPrototypeHud`：去掉 1／2／5 调倍速（改顶栏按钮）
- 斗技释放仍由 `HostCombatSkillBar` 独占数字键 1–6

**判断与理由**
- 数字键与技能栏抢键；菜单／V／顶栏已够用

---

## 2026-08-15 — 主动斗技：裂爪击／开山拳＋1–6 技能栏

**做了什么**
- 装备栏 6 格；选中角色按 1–6 释放主动斗技
- 洞府掉落「裂爪击」（200%×3）；将老任务奖励「开山拳」（500%×1）＋原功法秘籍

**判断与理由**
- 先做可手操的主动技竖切，半自动／技能树另开

## 2026-08-15 — 敌对姿态：免确认／可见标识

**做了什么**
- 明确：`hostile` 标签＝敌对姿态（洞府威胁）；无标＝中立／未宣战（主管等）攻击仍双确认
- 敌对：头顶名「·敌对」、偏红、菜单「（敌对）」且一点即打；主动寻仇留后

**判断与理由**
- 与宣战前中立单位分流，符合「洞府里就是敌人」

## 2026-08-15 — 斗技学习／装配面板

**做了什么**
- 脚下状态板右侧新增「斗技」入口（与人物／境界／关系并列）
- `HostCombatArtsPanel`：已学列表、装到键 1–6、从背包秘本学习（不消耗）

**判断与理由**
- 斗技装配与人物面板同一入口区，符合「点人再开详情」手操

## 2026-08-15 — 洞府残影不应出现在地表

**做了什么**
- 洞内地点（`interior`／`localMapId`）地表强制不可见
- 洞府怪无驻点时不落到开局地表点；Level Tester 默认剧本改 `scenario_ch01_reference`

**判断与理由**
- 残影出现在外面＝驻点／可见性过滤断了，不是「怪该刷在村口」

## 2026-08-15 — 战斗 A–C：近战体感／指令层／血条

**做了什么**
- A：挥砍特效＋交战中标签；敌人交战被 Hold，不乱跑
- B：换目标；右键地面脱离；S／Stop／行动菜单「脱离战斗」；顶栏交战提示
- C：头顶体魄／灵力护盾条；底栏「况」显示当前 HP／护盾（非仅上限）

**判断与理由**
- 按约定一次做完 A→C，再进入 D 斗技加深／E 差异化

## 2026-08-15 — 通用近战挥砍特效＋战斗路线拆分

**做了什么**
- `HostMeleeStrikeVfx`：程序化挥砍弧＋受击闪白；普攻互砍／主动斗技共用
- 回复中给出相对完整战斗的分阶段拆分（近战体感 → 指令层 → 斗技／姿态 → 境界差异）

**判断与理由**
- 设计文档（23）要 RTS 当场打；先把「看得见打」做稳，再扩系统面

## 2026-08-15 — 洞府残影战斗修复

**做了什么**
- 名册开局时 `WorldRegionBootstrap` 用 roster entries 挂驻点（残影终于在 `loc_cave_chamber`）
- 敌对 NPC：菜单只显示攻击、跳过双重确认；近距直接开打
- 追击不再每帧 `issueStop`；灵力护盾不再被 `EnsureVitals` 回满
- 应用 `initialRealmPlaceholder`；敌对单位偏红着色

**判断与理由**
- 「斗技对、战斗不对」主因是残影没挂上洞内地点，外加互砍手操脆弱

## 2026-08-15 — 战斗 Alpha／洞内残影／斗技 v0／秘籍不消耗

**做了什么**
- 秘籍／斗技秘本**不消耗**；换功法覆盖确认；旧功法修饰卸除
- 地图内 `HostNpcMeleeAssault` 自动互砍；击倒 NPC 写 `encounter:*`
- 洞府残影 `character_cave_shade`；洞内掉落斗技秘本；斗技装 1 门进普攻

**判断与理由**
- 遭遇依赖战斗；斗技挂伤害，功法仍一人一本

## 2026-08-15 — 将老／秘籍／洞府进出收束（文档＋推送）

**做了什么**
- 收束文档 [124](124-jiang-lao-cave-manual-rollup-2026-08-15.md)；更新总览／通读／62／devlog；飞书 map＋同步；推 GitHub
- 功能已含：将老井字棋、秘籍道具、勘查（神识×2／多圈）、进洞选人、出口离开、洞府秘诀拾取（攻+6%）

**判断与理由**
- 炼气后功法来源先竖切可玩，再开洞内遭遇／多功法并存

## 2026-08-15 — 洞窟 LocalMap 进出竖切

**做了什么**
- 地点字段 `localMapId`／`enterLocalMapId`／`enterSpawnLocationId`；Core `EnterLocalMap`／`LeaveLocalMap`
- 后续迭代：勘查显形、右键进／出、选人弹窗、洞内秘籍拾取（详见 [124](124-jiang-lao-cave-manual-rollup-2026-08-15.md)）

**判断与理由**
- 对齐 [113] 最小「发现→进另一张图」，不做完整 WorldGraph

## 2026-08-15 — 学功法：秘籍道具＋选人研读

**做了什么**
- 功法字段 `grade`／`effectSummary`；打坐修为改跟 `cultivationSpeed`
- 道具 `teachesManualId`；背包使用 → 选炼气队员学习并消耗；将老任务奖励改发秘籍

**判断与理由**
- 传承≠立刻学会；功法按角色学、需炼气；品阶／效果先做展示闭环

## 2026-08-15 — 将老对弈竖切（井字棋＋三胜传承）

**做了什么**
- NPC `character_jiang_lao`（泉边踱步）、任务 `quest_jiang_lao_chess`、功法 `cultivation_jiang_lao_legacy`、对话链＋`startMinigame`
- Host `HostTicTacToePanel`：真井字棋；每日一局（`daily:jiang_lao_chess`）；累计胜 3 次领残谱

**判断与理由**
- 对弈先于洞窟；日限＝可来才下，非每日必到；传承走既有 `learnManual` 领奖

## 2026-08-15 — 功法任务条件／奖励接口（内容另填）

**做了什么**
- 条件：`counterAtLeast`／`missingDailyFlag`／`hasDailyFlag`／`encounterCleared`
- 奖励：`addCounter`／`setCounter`／`setDailyFlag`／`clearDailyFlag`／`learnManual`／`setEncounterCleared`
- SCHEMA＋QuestEditor 字段；测 `ContentQuestApiSliceTests`；说明 [123](123-quest-manual-api-interfaces-2026-08-15.md)

**判断与理由**
- 将老对弈／洞窟探索先定契约，避免内容 JSON 绑死假库存代币；日访与计数进会话板、不升 Snapshot

## 2026-08-15 — 境界阶梯／打坐／突破仪式 Host 闭环

**做了什么**
- 人物／境界／关系暂停窗（底栏右侧入口）；F6 就地打坐；修为每 5 游戏分 +5
- `realmLadder` 内容阶梯：感应→炼气→筑基；手动突破；感应境不需功法
- 突破约 10s 黄条蓄势；可取消；移动／受伤／其他指令打断失败；暂停结果弹窗（境界＋属性差分）
- 炼气起灵力护盾；IMGUI 悬停墨色锁定
- 新建 [122](122-cultivation-breakthrough-host-ritual-2026-08-15.md)；更新总览／通读／62／SCHEMA；飞书同步；推 GitHub

**判断与理由**
- 突破不能是瞬升按钮；蓄势＋结果窗才能形成可读反馈；感应期不卡功法避免开局假门槛

## 2026-08-15 — 住房分配／主管府占领／Import 清旧

**做了什么**
- 住房区 vs 主管府拆分；`zoneHousing`／`controlCore`；三住房样例＋`homeWorkAreaId`
- 左键住房／府面板；右键府攻击；靠近近战 20／秒；破门站满 occupyHoldSeconds 占领
- `SettlementAuthority`（manageHousing／manageSchedules）；课表可改
- Level Tester Import 清空 mapRoot 子物体（修叠旧建筑）
- 新建 [121](121-housing-assignment-and-control-core-2026-08-15.md)；更新总览／通读／62／SCHEMA／114；飞书同步；推 GitHub

**判断与理由**
- 府不是住房；权限走内容 `grantsPrivileges` 而非硬编码建筑类型；攻击入口对齐 NPC 右键菜单

## 2026-08-15 — 人物／工区编辑器、名册刷人、倍速、对话发任务可见性

**做了什么**
- 废弃职业式 Job；WorkAreaEditor／CharacterNpcEditor；characterRoster 试玩刷人
- 人物编辑器保存出场／导出名册：深拷贝修复崩溃＋另存为默认当前路径
- Host 倍速：`PresentationDeltaTime` 驱动移动；顶栏统一 `SetSpeedMultiplier`（工作／休息／吃饭靠 Tick 已随倍率）
- 事件编辑器补 `npcDefinitionId`；人物页只读关联 onTalk；说明对话发任务非硬编码
- 新建 [120](120-character-roster-editors-and-timescale-rollup-2026-08-15.md)；更新 111／118／119／总览／通读／62；飞书同步；推 GitHub

**判断与理由**
- 制作人必须能在编辑器里看见「谁能对话发任务」；倍速必须覆盖表现层移动，否则时钟与行为脱节

## 2026-08-14 — 对话／任务 UX polish＋主管 startQuest 样例

**做了什么**
- `stockAtLeast` 任务进度显示 x/目标（右栏／J）
- 对话 `startQuest` 后自动追踪 + 顶部 toast
- 主管不语 → 惩罚任务 + 再对话切催促事件；EditMode 测
- 更新 [117](117-npc-dialogue-host-ux-rollup-2026-08-14.md)；飞书同步；推 GitHub

**判断与理由**
- 对话接任务需要即时反馈与可读进度，否则制作人难以验证 content 管线

## 2026-08-14 — NPC 对话框＋任务失败＋时间流速＋文档收束

**做了什么**
- NPC 右键对话／攻击；走近停下；onTalk → UGUI 居中对话框（打字机／立绘占位／可换皮）
- 非 onTalk 仍走中央打断；对话时隐藏 ACS 底栏
- 任务 `deadlineDays` 失败、`failResults`、`relationDelta` 多目标／`@party`；日志「已失败」
- 时间：1x＝1 现实秒 5 游戏分；5x＝25 游戏分／秒
- 新建 [117 收束](117-npc-dialogue-host-ux-rollup-2026-08-14.md)；更新总览／通读指南／62；飞书同步；推 GitHub

**判断与理由**
- 对话要先定 Host 框架再换美术；失败惩罚与流速是手操可感的基础规则

## 2026-08-14 — Ch01 三环手操＋小队背包＋文档收束

**做了什么**
- Ch01 裁成三任务（手动接取）：uniqueHarvest／背包 stockAtLeast／三人集合
- 劳动约 10s/份＠1x、自动续采；`LocationLaborProgressBoard` 计 harvest
- 小队共用 50 槽背包（B／顶栏）；任务库存读背包
- 任务日志 J、ReadyToClaim 领奖；QuestEditor 补劳动类条件
- 新建 [116 收束](116-recent-updates-rollup-2026-08-14.md)；更新 SCHEMA／110／总览／通读指南；飞书同步；推 GitHub

**判断与理由**
- 制作人要可手操的劳役→凑物资→集合闭环，且物资必须进可见背包而非隐性聚落库存

## 2026-08-10 — 重写「编辑器工具」文档（106）

**做了什么**
- [106](106-content-authoring-editors-plan-v0.1.md) 重写：点名 4 个一期编辑器＋二期 3 个；每个写清界面／操作／实现；工程放 ExternalTools、读写 Data JSON、Phase 验收

**判断与理由**
- 旧稿只停在原则层，制作人看不出「要做哪些、屏幕长什么样、怎么保存」

## 2026-08-10 — 文档补录＋编辑器工具计划确认

**做了什么**
- 新建 [106 编辑器工具](106-content-authoring-editors-plan-v0.1.md)：ExternalTools、A～D 模块、读写 Data JSON、加载路径说明
- 新建 [107 近期里程碑收束](107-recent-milestones-rollup-2026-08-10.md)：导航／NPC／Demo0.1／WorkAction 热修
- 更新总览／[62 现状](62-project-status-2026-08-01.md)／通读指南；飞书映射与同步

**判断与理由**
- 制作人明确要用编辑器做关卡；计划先落盘再开工，避免范围漂移
- 总览／现状仍停在 RTS 页时，后续三块底座对制作人不可见，故补收束页

**下一步：** 确认后开工 ExternalTools 第一期（校验台＋地点＋任务＋事件）。

## 2026-08-10 — 热修 WorkAction OrderId 编译

**做了什么**
- `WorkAction.cs` 补回 `using XianXia.Core.Orders`（commit `65f39a5`）

**判断与理由**
- 清理 unused using 时误删，导致 Unity CS0246／CS0738

## 2026-08-03 — Demo 0.1 Production · Chapter 01 Playable Arc

**做了什么**
- 计划 [103]；验收 [104]；制作人手操 [105]
- 新任务「调度·三人分派」；砍柴老人与矿工老倔拆角；矿洞氛围事件
- 开局／任务／功法／洞府文案对齐 30 分钟可玩弧；HUD 提示含分派三人

**判断与理由**
- 不扩系统：弧骨架已在 [97]，Production 补「可签收体验」缺口（分派节点＋机缘一致性＋手操脚本）
- 矿工与老人必须拆角，否则 NPC Job 样例会毁掉主线机缘

**下一步：** 制作人按 [105] 手操签收；正式长文案可按 [94] 换皮。

## 2026-08-03 — NPC Simulation Foundation Milestone

**做了什么**
- Location：`tags`／`allowedActivities`；WorkArea／Job 内容包；样例药农／矿工／巡卫／管事
- Core：`ActivityResolver`＋`NpcActivityDriver`；`MoveAction`／`WorkAction`（Schedule 不直接产 Labor）
- Host：`MovementIntent` 寻路，去掉硬编码地点启发式
- 计划 [101](101-npc-simulation-foundation-milestone-plan-v0.1.md)；验收 [102](102-npc-simulation-foundation-acceptance-report.md)

**判断与理由**
- 用配置 Job×WorkArea 而不是行为树：满足「策划定义谁／做什么／在哪／每天怎么动」，避免假 AI
- Move／Work 拆段才能接统一导航，且不把坐标写进 C#

**下一步：** 手操签收四类样例走动；无 Job NPC 可逐步迁到 Job。

## 2026-08-03 — Navigation Foundation Milestone

**做了什么**
- Core：`WalkGrid`／A*／`Ch01ReferenceWalkGrid`（可行走＋障碍）
- Host：移动沿航点；`HostNpcScheduleMover` 按课表走地点；多单位软分离
- 计划 [99](99-navigation-foundation-milestone-plan-v0.1.md)；验收 [100](100-navigation-foundation-acceptance-report.md)

**下一步：** 手操签收绕障／NPC 走动；障碍烘焙可后补。

## 2026-08-03 — RTS 手动控制＋HUD／多交互点

**做了什么**
- 己方不跟课表自动行动（`ScheduleDriver` 跳过 Character）；指令改为移动／停止／交互／战斗占位／修炼点选
- HUD：时钟显示分钟；暴露／主管压顶栏全局条＋条内数值；角色面板交差／功法
- 各地多交互点＋地面标记；走到具体点再劳动／修炼
- 交付记录 [98](98-rts-manual-control-and-hud-pass-2026-08-03.md)；飞书同步

**下一步：** NPC／守卫表现巡逻；守卫数据修正；手操签收。

## 2026-08-02 — 第一章可玩弧＋打断＋RTS 引导（交付总结）

**做了什么**
- 内容打断 CIF：`HostContentInterruptPresenter`（事件选项＋任务提醒、强制暂停）；计划／验收 [95]／[96]
- 样例关 Data 拉齐 2G 觉醒弧至炼气→隐藏→权力伏笔（无战斗）；制作对照 [94]
- RTS：首次入区自动勘察；开局／操作条引导；F11 调试／G 敛息；焦点一人下令
- 场景：`DemoParityHost`＝样例关；`PlayableHost`＝框架调试
- 交付总结 [97](97-ch01-playable-arc-and-ux-delivery-2026-08-02.md)；EditMode **194/194**
- 文档收束＋飞书 map 增补并同步

**下一步：** 制作人手操签收 DemoParityHost；正式第一章文案；战斗／夺权另开。

## 2026-08-02 — Demo 手感对齐关验收

**做了什么**
- [91] 缺口审计→按文档补齐；HostDemoTileMap（Demo 布局 Prefab 铺砖）；全 NPC 可见；验收 [93]
- EditMode **182/182**

**下一步：** 制作人手操签收；stride=1 可选加密；日课三资源任务数值再对齐。

## 2026-08-02 — Demo 手感对齐（正式 Host）进行中

**做了什么**
- 缺口审计 [91]；PlayableHost 改 Sprite＋XY；Stop／W／C／X／G；工区产资源；暴露／愤怒；课表／头顶字／村民标签／守卫商人
- 进度 [92](92-demo-parity-progress-2026-08-02.md)

**下一步：** 见上条验收。

## 2026-08-02 — Chapter 01 Reference Level 验收

**做了什么**
- 模板关：8 区灰盒地图／RTS 移动＋行动菜单／FormalHud／三类 AI／参考 Scenario＋Data
- 制作流程 [88](88-chapter-01-reference-level-production-guide.md)；验收 [89](89-chapter-01-reference-level-acceptance-report.md)；EditMode **175/175**

**下一步：** 制作人手操参考关；按 88 生产真实章节内容。

## 2026-08-01 — 文档收束＋飞书同步（VS0.7～1.0）

**做了什么**
- 新增交付总结 [75](75-vs0.7-to-1.0-delivery-summary-2026-08-01.md)；更新总览／现状／SCHEMA
- feishu-map 增补 VS0.7～1.0 计划／验收／SCHEMA；`--provision`＋全量同步

**下一步：** 制作人手操 Demo；Snapshot 入档前硬停。

## 2026-08-01 — VS1.0 Demo 0.1 Vertical Slice 验收

**做了什么**
- Demo 闭环验收测：探索→学法→突破→据点日产→关系
- Host Demo 路径提示；EditMode **169/169**；验收 [74](74-vertical-slice-1.0-acceptance-report.md)
- **VS0.7～1.0 长期路线自动化收束**

**下一步：** 产品定 Snapshot 入档／正式 UI／内容扩量；开发可停。

## 2026-08-01 — VS0.9 World Interaction Layer 验收

**做了什么**
- WorldRegion／Travel／Explore；四地点内容；Host 俯视布局＋T／Y
- EditMode **168/168**；地点不入 Snapshot；验收 [72](72-vertical-slice-0.9-acceptance-report.md)

**下一步：** VS1.0 Demo Vertical Slice。

## 2026-08-01 — VS0.8 Cultivation & Settlement Simulation 验收

**做了什么**
- Resource／Facility／Settlement 内容化；SettlementBoard＋日终生产；WorkRole／AssignWork
- 开局青石洞府＋三人分工；Host HUD／键 8–0
- EditMode **165/165**；据点不入 Snapshot；验收 [70](70-vertical-slice-0.8-acceptance-report.md)

**下一步：** VS0.9 世界互动层。

## 2026-08-01 — VS0.7 Character & Content Foundation 验收

**做了什么**
- Character：`personalityTags`／`backgroundTags`／`talentTags`；Scenario 驱动开局 spawn／NPC／势力／关系
- 移除 PlayableDay 软编码 CreateNpc「村内可招者」
- 数据样例：`character_village_recruit`／`character_herb_gatherer`／`cultivation_wood_whisper`／`scenarios.json`
- EditMode **161/161**；验收 [68](68-vertical-slice-0.7-acceptance-report.md)；**未**改 Freeze／Snapshot schema

**下一步：** VS0.8 据点／资源／经营循环（另开计划）。

## 2026-08-01 — 文档收束＋飞书同步（VS0.6）

**做了什么**
- 固化 VS0.6 计划／验收／制作人试玩清单（64／65／66）
- 更新现状 62、路线图、总览；飞书 map 增补并全量同步
- 开发停止；状态＝制作人人工验收

**下一步：** 按 66 试玩签收；再定 Snapshot 入档或下一切片。

## 2026-08-01 — VS0.6 Phase E：Playable Social Host 验收

**做了什么**
- SocialHostAcceptancePhaseETests 闭环
- 验收报告 [65](65-vertical-slice-0.6-acceptance-report.md)
- EditMode 157 全绿；Snapshot 未升版

**下一步：** 停止编码；待产品定 Snapshot 入档或下一切片。

## 2026-08-01 — VS0.6 Phase B～D：社会 HUD／命令／事件

**做了什么**
- HUD：Personality／Relation／Faction／FocusKind
- Port：Help／Slight／Recruit → 既有 Social／RecruitService（非 Order）
- Bridge：键 5／6／7；Actor＝首个 Character，Target＝首个非 Actor
- EventFeed 优先 RelationshipChanged／FactionMembershipChanged
- **未**升 Snapshot schema

**下一步：** V6-E 整合验收报告。

## 2026-08-01 — VS0.6 Phase A：Recruitable NPC 表现

**做了什么**
- ViewableEntityIds：三角色 + 可招 Npc
- EntityViewSpawner 生成／绑定 Npc 槽位（可点选）
- 计划 [64](64-vertical-slice-0.6-playable-social-host-plan-v0.1.md)

**下一步：** V6-B 社会 HUD 薄信息。

## 2026-08-01 — 文档收束＋飞书同步（VS0.5 完成后）

**做了什么**
- 更新 [62 现状](62-project-status-2026-08-01.md)：测试门禁 151、下一步、验收入口
- 飞书 map 增加 VS0.5 验收报告 [63](63-vertical-slice-0.5-alpha-acceptance.md)；provision／全量同步

**下一步：** 人工验收 VS0.4 Host 手操 + VS0.5 Core 社会闭环；关系入档前硬停。

## 2026-08-01 — VS0.5 Phase G：Alpha 整合验收

**做了什么**
- `SocialAlphaAcceptancePhaseGTests`：人格→开局关系→Help→招募→日程偏置→社会漂移→Player Override；断言 Snapshot schema=v1
- 验收报告 [63](63-vertical-slice-0.5-alpha-acceptance.md)；更新计划／现状／路线图

**下一步：** 关系入档前硬停确认 schema；否则另开切片。

## 2026-08-01 — VS0.5 Phase F：社会 Tick

**做了什么**
- `SocialTickDriver`：每 N tick 对 Character／Npc 抽一对，低频 Help／Slight → Ledger（人格加权）
- `SimulationLoop` 可选启用（默认关）；`PlayableDayBootstrap` 开启漂移
- 固定 seed 可复现；**未**改 Snapshot／无地图邻近
- EditMode 150 全绿

**下一步：** V5-G Alpha 整合验收。

## 2026-08-01 — VS0.5 Phase E：人格日程偏置

**做了什么**
- `PersonalityScheduleBias`：bold／cautious／curious 微调 Schedule 活动与时长；bold 可将 Rest 块转为 Labor；bold+cautious 互相抵消
- `ScheduleDriver` 注入偏置；Player Override 仍优先；可招 Npc 挂 Schedule（无 quota）
- EditMode 147 全绿；**未**改 Snapshot／Freeze

**下一步：** V5-F 社会 Tick 漂移。

## 2026-08-01 — VS0.5 Phase D：薄招募

**做了什么**
- `FactionMembershipComponent`／`RecruitService`（门槛＝target→recruiter Score ≥ RecruitMinScore）；离开清隶属、保留 Ledger
- `EntityTag.Npc` + `CreateNpc`：可招者为 Npc，不进 DirectControl／CharacterIds
- PlayableDay：三人劳役隶属 `base:sect_huangcun_labor`；额外 1 名无势力「村内可招者」
- `EventType.FactionMembershipChanged`；**未**改 Snapshot schema
- **Content Authoring Tool 需求（记录）：** 可招 NPC／势力角色若继续手写 spawn，应改为 Content 表＋编辑器；当前 Alpha 仅 1 个软编码实例
- EditMode 143 全绿

**下一步：** V5-E 人格日程偏置。

## 2026-08-01 — VS0.5 Phase C：开局关系

**做了什么**
- `OpeningRelationsSeeder`：三人互惠 `opening_companion` 分
- `SocialInteractionService`：Help／Slight → RelationshipService
- `PlayableDayBootstrap` 启动时播种；EditMode 覆盖种子／互动／可玩日

**下一步：** V5-D 薄招募／FactionMembership。

## 2026-08-01 — VS0.5 Phase B：RelationshipLedger

**做了什么**
- `RelationshipEvent`／`RelationshipLedger`／`RelationshipComponent`／`RelationshipService`
- `SimulationWorld.Relationships`；`EventType.RelationshipChanged`
- 唯一写路径：Service → Ledger Append → 缓存刷新 → DomainEvent；**未**改 Snapshot schema
- `SocialAlphaConstants` 预置试玩常量（供 C～D）

**下一步：** V5-C 开局关系种子 + Help／Slight。

## 2026-08-01 — 文档现状总表＋飞书同步

**做了什么**
- 新增 [62 项目现状](62-project-status-2026-08-01.md)：VS0.1～0.5 进度、V4 交付／commit、V5-A 状态、硬停与下一步
- 更新总览／路线图／VS0.4·0.5 计划头状态／`AGENTS.md`；飞书 map 补 VS 验收与现状文档并全量同步

## 2026-08-01 — VS0.5 Phase A：人格档案

**做了什么**
- `PersonalityProfileComponent`；`CreateCharacter` 默认挂载；`GameStartBootstrap` 写入 Spawn tags
- Content 三人差异标签可查询；34／术语表登记；**未**改 Snapshot

**下一步：** V5-B RelationshipLedger。

## 2026-08-01 — VS0.4 自主推进：先完成 D～H，再进 VS0.5

**决定**
- 不搁置 V4-D～H；按 Phase 独立实现／测试／commit 做完 Host 可玩切片后，再开 VS0.5 社会 Alpha。

## 2026-08-01 — VS0.4 Phase D：命令桥

**做了什么**
- `HostCommandBridge`：选中集合 → `PlayerCommandRequest` → `IPlayerInputPort.Submit`
- 键位 1–4／调试按钮：Labor／Rest／Observe／Cultivate；多选逐个下令、失败不中断
- EditMode／PlayMode：ActiveAction、Schedule Override、Observe→Cultivate

**下一步：** V4-E 时间控制＋最小 HUD。

## 2026-08-01 — VS0.4 Phase E：HUD 与时钟

**做了什么**
- `HostHudSnapshot`／`HostDebugHud`：Day／Hour／Action／Schedule／Quota／Risk／Realm 只读
- 键位：Space 暂停、`.`／N 单步、`[`／`]` 倍速 1→2→5；自动 Tick 按倍速推进
- EditMode：HUD 与 DayClock／组件一致

**下一步：** V4-F DomainEvent 调试反馈。

## 2026-08-01 — VS0.4 Phase F：事件反馈

**做了什么**
- `HostEventFeed`：Tick／Init 后 Drain DomainEvent 到环形缓冲；IMGUI 调试列表
- 优先标记 Day／Override／Observe／Reject／Quota／Action 等事件
- EditMode：发现／ScheduleInterrupted 可见

**下一步：** V4-G Snapshot 存读 UI。

## 2026-08-01 — VS0.4 Phase G：Snapshot UI

**做了什么**
- `PlayableHostSession.CaptureSnapshotJson`／`RestoreSnapshotJson`（既有 SnapshotService，不改 schema）
- `HostSnapshotPanel`：F5／F9＋按钮；Load 后 `RebuildPresentationAfterLoad`
- EditMode：存读后 Tick／Quota／KnownSites／Views 一致

**下一步：** V4-H 一日可玩验收。

## 2026-08-01 — VS0.4 Phase H：一日可玩验收

**做了什么**
- `HostPlayableDayPhaseHTests`：选中→Schedule→Observe→Cultivate→日界→Snapshot
- 验收报告 `61-vertical-slice-0.4-acceptance-report.md`
- VS0.4 Host 切片闭环完成

**下一步：** 开 VS0.5 社会／人格／关系 Alpha（Core）。

## 2026-08-01 — VS0.4 Phase C：RTS Selection

**做了什么**
- `HostSelectionState`／`HostSelectionController`：点选替换、Shift 点选 Toggle、框选覆盖
- `EntityView.SetHighlight` 驱动；Rebuild 清空选择
- EditMode／PlayMode 选择烟测；**未做** PlayerCommand／Port 命令／HUD／事件日志

**下一步：** 等确认后 V4-D 命令桥。

## 2026-08-01 — VS0.4 Phase B：EntityView 表现绑定

**做了什么**
- `EntityView`／`EntityViewRegistry`／`EntityViewSpawner`：三人胶囊槽位＋Collider＋只读同步
- `PlayableHostCameraRig`：固定观察＋WASD／滚轮极简
- Host 初始化／Rebuild 时生成与清理 View；EditMode＋PlayMode 烟测
- **未做：** 点选／框选／命令／HUD／事件日志／存档／地图／Demo 迁移

**下一步：** 等 V4-B 验收后再进 V4-C。

## 2026-08-01 — VS0.4 Phase A：Playable Host Bootstrap

**做了什么**
- Data：`PlayableDayBootstrap`（Load BaseGame → 三人 → Schedule／Site／Manual／DailyTask／Risk；Loop＋Port；默认 QuotaConsequence）
- Unity：`PlayableHostSession`／`PlayableHostBootstrap`；场景 `Assets/Scenes/PlayableHost.unity`
- Editor：Space 单步 Tick、P 暂停／继续；Content 路径失败清晰报错
- EditMode：`PlayableDayBootstrapPhaseATests`；全量 **107/107**
- **未做：** EntityView／点选／命令／HUD／事件日志／存档 UI／Demo 迁移／V4-B+

**下一步：** 等 V4-A 验收后再进 V4-B。

## 2026-08-01 — Vertical Slice 0.4 Unity Host 规划（不编码）

新增 [`59-vertical-slice-0.4-unity-playable-host-plan-v0.1.md`](59-vertical-slice-0.4-unity-playable-host-plan-v0.1.md)：VS0.3 规则闭环 → 最小 Unity 可玩场景；Content／World、EntityView、RTS 选择、PlayerCommandRequest、HUD、事件、Snapshot；Demo 只读禁迁；V4-A～H 独立 commit。确认前不编码。

## 2026-08-01 — VS0.3 最终验收报告（不编码）

新增 [`58-vertical-slice-0.3-acceptance-report.md`](58-vertical-slice-0.3-acceptance-report.md)：汇总 A–D、完整玩家循环、可玩边界、架构观察／ADR 挂账、下一阶段仅规划；**特别记录**后续每 Phase 必须单独测／单独 commit。不自动开工下一阶段。

## 2026-08-01 — VS0.3 Phase B–D：Observe／偷修／日终 Quota

**做了什么**
- B：`ObserveAction`、`OpportunitySite`／`KnownSites`、`sites.json`（NameKey＋短描述）
- C：`CultivationAttemptGate`（发现 Site 后学青云诀再 Cultivate）；`PersonalConcealmentRisk` 0–100
- D：`QuotaConsequenceHandler` 挂 `DayEnded`；`PendingReprimand`＋日切重置
- 整合测：`Vs03PhaseBcdIntegrationTests`（命令序列，非章节脚本）
- **未做：** 主管视线／潜行／目击者、地图、战斗、产品 UI、第一章导演

**下一步：** 等 VS0.3 验收。

## 2026-08-01 — VS0.3 Phase A：DayClock／日循环

**做了什么**
- `DayClock`：由 `WorldTick` 派生 dayIndex／tickInDay／hourOfDay（不另存）
- 跨日：`DayEnded` 先于 `DayStarted`；`IDayBoundaryHandler` 空钩子供 D 接入
- EditMode：`DayClockPhaseATests`（派生、95→96 顺序、同日不重复、Snapshot）
- **未做：** Quota 结算／Reset／Risk／Observe／Site／Narrative／Schedule 改动

**下一步：** 等确认后 Phase B。

## 2026-08-01 — VS0.3 计划设计确认修订（不编码）

完善 [`57`](57-vertical-slice-0.3-plan-v0.1.md)：非剧情／非章节脚本／RTS／Schedule 默认／Override 优先；范围 A–D；Core／Data／Narrative 分层；禁止战斗地图寻路／NPC AI／主管 Boss／完整关系；阶段 V3-A～E。与 `2I` 阶段叙事口径对齐。确认前不编码。

## 2026-08-01 — 叙事：荒村杂役阶段框架重定位（不编码）

**做了什么**
- 删除线性稿 `2I-chapter-1-huangcun-labor-v0.1.md`
- 新建 [`2I-huangcun-labor-phase-narrative-v0.1.md`](../20-systems/2I-huangcun-labor-phase-narrative-v0.1.md)：定位为**阶段叙事**（状态／触发／反馈／可重复事件），非固定章节脚本
- 废除：固定第几天偷修、固定结束点、写死现实／理想伙伴、姓名性别锁定
- 主管改为长期压迫源；倒台声明为玩家筑基后（与 `20` 冲突已记入 2I §11）
- 更新 `20-systems/README`、`00-overview`、`2G` 链与语感说明

**为什么**
- 游戏非章节制／非线性剧情 RPG；原 v0.1 易被读成强制主线日程

**仍待确认**
- 主管倒台门槛（筑基 vs 炼气掀桌／外交分流）；告密是否允许；开局关系权重；`2G` 是否改名；默认灵地包装

## 2026-08-01 — 叙事：第一章《荒村杂役篇》v0.1（不编码）

> 已被上条重定位取代；原文件已删除。保留本条仅作历史痕迹。

## 2026-08-01 — Vertical Slice 0.3 规划草案（不编码）

新增 [`57-vertical-slice-0.3-plan-v0.1.md`](57-vertical-slice-0.3-plan-v0.1.md)：第一天完整体验闭环——Day/Hr/Tick、Observe、OpportunitySite、偷修接 Cultivate、QuotaDeviation 日终薄后果、Exposure 建议一并、V3-A～H 与 Cursor 任务。VS0.2 已验收；确认前不编码。

## 2026-08-01 — VS0.2 Phase C：Player Override + Quota 偏差

**做了什么**
- PlayerOrder 打断进行中的 Schedule Action，原因 `OverrideByPlayer`；事件 `ScheduleInterrupted`
- `DailyTask`：RequiredAmount／CompletedAmount／Deviation；未完成 Schedule Labor → `QuotaDeviationCreated`
- Snapshot 恢复 Deviation／ActiveOrderSource；EditMode `PlayerOverridePhaseCTests`
- **未做：** 主管／惩罚／关系／Exposure／UI／地图／移动／战斗

**下一步：** 等 Phase C 验收。

## 2026-08-01 — VS0.2 Phase B：ScheduleDefinition + ScheduleDriver

**做了什么**
- `ScheduleDefinition`／`ScheduleBlock`／`ScheduleActivity`（Labor／Rest）
- `ScheduleComponent` 绑定；`ScheduleDriver` 空闲且无 Player Order 时注入 `OrderSource.Schedule`
- `OrderQueue`：Player 入队优先于 Schedule
- Snapshot 含 schedules + 实体绑定；EditMode **84/84**
- **未做：** Override 中断、Quota 惩罚、Observe、Exposure、AI／UI／地图

**下一步：** 等确认后 Phase C。

## 2026-08-01 — VS0.2 Phase A：PlayerInput → Order → Labor

**批准：** `56` 已审；RTS／Schedule 语义／报告 B 范围冻结。

**做了什么**
- `IPlayerInputPort`／`PlayerInputPort`／`PlayerOrderFactory`／`PlayerCommandRequest`
- `LaborAction`／`RestAction`／`DailyTaskComponent`；`OrderType.Labor|Rest`
- EditMode：`PlayerInputPhaseATests`；全量 **78/78**
- Commit：`548a095` `feat(core): vs0.2 phase a player input bridge`
- **未做：** Schedule、Override、Observe Action、Exposure、UI

**下一步：** 等确认后 Phase B（ScheduleDefinition／Driver）。

## 2026-08-01 — VS0.2 开工前确认报告（不编码）

新增 [`56`](56-vertical-slice-0.2-pre-dev-confirmation.md)：RTS 控制冻结；目标改为 PlayerOrder+Schedule+Override+规则惩罚；第一阶段仅 Port／Factory／Schedule／Labor·Rest·Observe／最小 Override；Exposure／OpportunitySite 后置；列出 ADR D1–D6。等确认。

## 2026-08-01 — VS0.2 计划范围收紧修订（不编码）

按补充要求重写 [`55`](55-vertical-slice-0.2-plan-v0.1.md)：明确≠第一章；六条核心体验；PlayerInput→Order→Action；Schedule=计划非 AI；Override 三维代价；偷修仅 CultivationAttempt 接口；ExposureRisk 0–100 进入；延期表与风险点；阶段 V2-A～H。

## 2026-08-01 — 文档同步（VS 0.1 完成链）

将已完成的 Data Pipeline M1／Bootstrap／Cultivation／验收状态写回过程文档：`53` 完成标准勾选与命名落地说明、`AGENTS`／通读指南／`34` 组件实现注记；入库未跟踪的 `55` VS0.2 计划草案；补本条之前缺失的专条（见下）。**不编码。**

## 2026-08-01 — Vertical Slice 0.2 规划草案（不编码）

新增 [`55-vertical-slice-0.2-plan-v0.1.md`](55-vertical-slice-0.2-plan-v0.1.md)：杂役第一天闭环——输入→Order、最小 Schedule、Player Override、三角色、日流程、偷修接入、暴露是否进切片、不做清单、V2-A～G 与 Cursor 任务模板。

## 2026-08-01 — Vertical Slice 0.1 验收

**判断：** Core／Data／Bootstrap／Cultivation 闭环已足以作为下一「可感杂役日」切片的底座；产品可玩性仍缺输入／工作／日程／地图。

**做了什么**
- 验收报告 [`54-vertical-slice-0.1-acceptance-report.md`](54-vertical-slice-0.1-acceptance-report.md)
- 架构观察：Cultivation vs Manual 命名、WorldLayout 存档、境界／Progress 语义待 ADR
- EditMode 门禁（Cultivation 完成后）：**73/73**

**下一步：** VS 0.2 规划确认前不编码。

## 2026-08-01 — Cultivation Slice 0.1：凡人→炼气闭环

**判断：** 修炼必须走 Order→Action→ActionClock；突破事件可观测；Snapshot 须恢复 Progress／Realm。

**做了什么**
- `CultivationComponent`／`CultivationService`／`CultivateAction`／`EventType.Breakthrough`
- `RealmStage`：仅 Mortal／QiRefining
- 青云诀 `base:cultivation_qingyun_manual`（Speed／BreakthroughProgress／Modifiers）
- 学法 → 修炼 → 突破 → Snapshot 中途／完成后一致
- Commit：`64cb3ab` `feat(core): cultivation vertical slice`

**不做：** 多境界、天劫、丹药、洞府、战斗。

## 2026-08-01 — Vertical Slice 0.1 Bootstrap

**判断：** 开局只需数据结构 + 三角色 Entity + 初始化事件；不是地图／工作玩法。

**做了什么**
- `WorldInitData`（Region／LocalMap／Settlement 占位）
- `GameStartBootstrap`／`ContentGameStart`
- 角色 Definition：protagonist／companion_a／companion_b（性格 Tag、灵根／境界占位）
- Commit：`6897807` `feat(core): prepare vertical slice 0.1 bootstrap`

## 2026-08-01 — Data Pipeline M1-B：CSV→JSON 校验导入

**判断：** CSV 只作 Authoring；失败必须阻断写盘并给出 ValidationReport。

**做了什么**
- `CsvDefinitionImporter`／`SimpleCsv`（无 Excel 库）
- 校验：重复 ID、非法 ID、缺必填、`requiredRealm` 形如 DefinitionId 时引用必须存在
- Authoring：`Content/BaseGame/Authoring/Csv/*.csv`
- Commit：`90f89ea` `feat(data): complete data pipeline m1-b import validation`

## 2026-08-01 — Data Pipeline M1-A：三类 Definition 加载

**目标：** BaseGame JSON → ContentPackageLoader → Registry 可查询（无玩法结算）。

**做了什么**
- `CharacterDefinition`／`CultivationDefinition`／`ItemDefinition` + Registry 扩展  
- 严格未知字段／重复 ID／非法 DefinitionId  
- `characters.json`／`cultivation.json`／`items.json` + SCHEMA.md  
- EditMode：`ContentPackageTests` 覆盖加载与阻断路径  
- Commit：`3ee16e1` `feat(data): complete data pipeline m1-a definitions`

**说明：** 实现文件名为 `cultivation.json`／`CultivationDefinition`（与计划草案中的 Manual／manuals 用词并存；以代码与 SCHEMA 为准，待 ADR 统一术语）。

## 2026-08-01 — Data Pipeline M1 计划批准（v0.2）

**确认：** JSON 运行时为主、CSV 辅助、Excel 仅编辑源；严格未知字段／重复 ID／非法引用；不实现完整 Localization（预留 NameKey）；Modifier 规则与计算在 Core，Data 只提供配置。

**后续：** 编码已完成（见上 M1-A／M1-B 专条）；计划正文完成标准已勾选。

## 2026-08-01 — Core Milestone 1 验收完成（收尾）

**判断：** 统一 Core 骨架已证明可承载后续系统；下一优先是真实数据接入，而非继续扩模拟功能。

**做了什么（Phase 1～10）**
- 程序集：`XianXia.Core`／`Data`／`Unity`／`Tests`（Core／Data 无 UnityEngine）
- Id／WorldTick／ActionClock／Result／IRandomSource（完整 PRNG 状态）
- ContentPackage 基础（显式 BaseGame）／Entity／AttributeModifier（`2C` 公式）
- DomainEvent／Order→Action／SimulationLoop／Snapshot JSON
- EditMode **54/54**；`CoreM1IntegrationTests` **PASS**（单 Region：加载定义→实体→Wait→存档续跑）

**Git 范围**
- `1688187` `feat: scaffold Core M1 phase 1…`
- … 阶段 2～10 与整合测 …
- `e8340da` `chore: add JsonSnapshotSerializer meta`
- 共 12 个提交（`1688187`‥`e8340da`）

**关键架构成果**
- 双时间职责可测；Modifier 可溯源；业务失败走 Result；存档可恢复 Tick／Action／PRNG／Final
- Demo Runtime 未迁入、未扩展

**下一步（当时）**
- Data Pipeline M1 → 其后 VS Bootstrap／Cultivation（均已完成，见上方专条）

## 2026-08-01 — Core M1 阶段 10 Snapshot＋整合烟测完成

**判断：** JSON 往返必须恢复 WorldTick／ActionClock／PRNG／Modifier Final；内容版本不匹配走 Result。

**做了什么**
- `WorldSnapshot`／`SnapshotService`／`JsonSnapshotSerializer`
- 黄金：Wait 中途存档 → 新 World 续跑同 Tick 完成；Modifier／PRNG 一致；版本不匹配失败
- `CoreM1IntegrationTests` 单 Region 闭环

**下一步**
- Core M1 编码完成，待人工总验收

## 2026-08-01 — Core M1 阶段 9 Order／Action／SimulationLoop 完成

**判断：** WorldTick++ 只能由 SimulationLoop 拥有；Wait(4) 在第 4 次推进完成。

**做了什么**
- Order／OrderQueue／DefaultOrderTranslator；WaitAction／ApplyModifierAction
- `SimulationWorld`＋`SimulationLoop`；CanStart 失败 → OrderRejected／ActionFailed 事件

**下一步**
- 阶段 10 Snapshot JSON 往返

## 2026-08-01 — Core M1 阶段 8 DomainEvent 完成

**判断：** 失败路径也必须留下事实事件，避免用异常代替世界记录。

**做了什么**
- `DomainEvent`／`DomainEventQueue`／`EventType`（EntityCreated／Modifier*／Action*／OrderRejected）
- Peek／Drain；稳定顺序；cursor 供 Snapshot

**下一步**
- 阶段 9 Order／Action／SimulationLoop

## 2026-08-01 — Core M1 阶段 7 AttributeModifier 完成

**判断：** Final 只能经管道计算；黄金例锁定 (100+10)×(1+0.5)=165。

**做了什么**
- `AttributeId` 小枚举；`AttributeModifier`／`ModifierOperation`／`AttributePipe`／`ModifierIdFactory`
- `AttributesComponent` 正式挂载 Add／RemoveBySource／GetFinal／Explain；无公开 SetFinal

**下一步**
- 阶段 8 DomainEvent

## 2026-08-01 — Core M1 阶段 6 Entity 基础完成

**判断：** 组合组件 + 白名单，避免配置反射创建任意组件；Dead／Removed／Incapacitated 语义先锁死。

**做了什么**
- `Entity`／`EntityStore`／`EntityIdFactory`／`EntityQuery`
- 四核心组件：Identity／Attributes（入口）／Lifecycle／ActionState
- Character 标签；白名单拒收未知组件

**下一步**
- 阶段 7 AttributeModifier 管道

## 2026-08-01 — Core M1 阶段 5 ContentPackage 基础完成

**判断：** 官方内容也必须走 ContentPackage，且 M1 只显式加载、不扫 Mods/。

**做了什么**
- `ContentManifest`／`ContentPackageLoader`／`DefinitionRegistry`／`CharacterDefinition`
- `AssetId`／`DataVersion`；`SimpleJson`（Data 层，零 UnityEngine）
- BaseGame `Data/characters.json` 样本；重复／非法 ID／缺字段校验

**下一步**
- 阶段 6 Entity 基础

## 2026-08-01 — Core M1 阶段 4 随机系统完成

**判断：** 存档一致性要求完整 PRNG 状态，而非仅 seed＋计数。

**做了什么**
- `IRandomSource`／`DeterministicRandom`（XorShift128+）／`RandomState`／`RandomStreamId`
- 单测：同 seed 序列一致；Capture／Restore 后续抽取一致

**下一步**
- 阶段 5 ContentPackage 基础

## 2026-08-01 — Core M1 阶段 3 Result／Validation 完成

**判断：** 业务失败必须与异常路径分离，否则 Tick 模拟会被异常控制流污染。

**做了什么**
- `ErrorCode`／`GameError`／`Result`／`Result<T>`／`ValidationReport`／`IValidator`
- `DefinitionId.Parse` → `Result<DefinitionId>`；保留 `TryParse` 兼容
- EditMode 增补 Result 测试

**下一步**
- 阶段 4 随机系统

## 2026-08-01 — Core M1 阶段 2 基础类型完成（待确认）

**判断：** 跨系统共享原语必须先于 Result／Entity／Modifier 落地；DefinitionId 与 EntityId 在类型层隔离，避免后期存档污染。

**做了什么**
- 清理检查：删除探测残留 `Logs/phase1-offline-boundary-check.txt`；无探测脚本／临时工程改动进正式程序集
- 实现 `EntityId`／`DefinitionId`／`SourceRef`＋`SourceKind`／`ModifierId`（最小句柄）／`ActionId`／`EventId`／`SnapshotId`／`RegionId` 占位
- 实现 `WorldTick`（1 tick=15 分，96／日；加减 checked 溢出抛 `OverflowException`）与 `ActionClock`（Duration 剩余，钳制≥0，不改 WorldTick）
- DefinitionId 非法解析过渡：`TryParse` 返回 bool（不做 Phase 3 Result）
- EditMode 全量 **24/24** 通过（含原 5 个边界测试）

**下一步**
- 人工确认后进入阶段 3 Result／Validation

## 2026-08-01 — Core M1 阶段 1 工程结构完成（待确认）

**判断：** 正式分层必须在编译期不可破坏；Demo Runtime 保持不动。

**做了什么**
- 确认／补齐 `XianXia.Core`／`Data`／`Unity`／`Tests` 四 asmdef（Core／Data `noEngineReferences`）
- 补 `Unity/Presentation/`；Tests 按计划落在 `Assets/Tests/EditMode/`
- `DataAssemblyMarker` 用 `typeof(CoreAssemblyMarker)` 保留真实程序集引用
- 修正 Tests asmdef 重复 TestRunner 引用
- batchmode 编译成功；EditMode 边界测试 5/5 通过

**下一步**
- 人工确认阶段 1 后进入阶段 2 基础类型

## 2026-08-01 — Core M1 补充执行规则

写入 Plan v0.2 §0.2／AGENTS／ADR-0022：Demo 冻结；阶段门禁与独立提交；禁擅自改 ProjectSettings／Packages／冻结 ADR／Demo；设计冲突停码。

## 2026-08-01 — Core M1 计划批准并进入编码（阶段 1）

**判断：** Implementation Plan 人工确认 5 项（Domain 不拆 asmdef、Snapshot＝JSON、Random＝完整状态、AttributeId＝小枚举、EditMode 为完成标准）。

**做了什么**
- 发布 Plan **v0.2**；修订 ADR-0022 实施确认节  
- 开始阶段 1：正式 asmdef 工程结构（不扩 Demo）

**下一步**
- 阶段 1 完成并确认后进入阶段 2 基础类型

## 2026-08-01 — AI 多会话协作规范

新增 [`52-ai-collaboration-protocol.md`](52-ai-collaboration-protocol.md)（`46` 号已被 Demo 美术占用）。同步更新 `README.md`、`AGENTS.md` 入口。不改架构／不写代码。

## 2026-08-01 — Core M1 实施计划草案（不编码）

新增 [`51-core-milestone-1-implementation-plan-v0.1.md`](51-core-milestone-1-implementation-plan-v0.1.md)：十阶段工程／类型／Result／Random／ContentPackage／Entity／Modifier／Event／Order-Action／Snapshot；附风险与人工确认项。**确认前不写 Core 代码。**

## 2026-08-01 — 文档可读性整理与飞书一一对应

1. 新增 [通读指南](../00-project/04-reading-guide.md)、[ADR 决策索引](43-decisions/README.md)  
2. 总览入口改为可点链接；飞书 map 补齐 `34`／`35`／`36`／`2E`／审计／全部 ADR  
3. 同步脚本导航分组改为 00／10／20／30／43／40  
4. 原则：**本地 MD 真源 ↔ 飞书阅读层结构与链接一致**；不重写已冻结规则正文  

## 2026-07-31 — Architecture Freeze v0.2 修补

根据审计报告与人工确认，写入 v0.2（仍不编码）：

1. **RelationshipLedger** 唯一真源；Component 只缓存（ADR-0017）  
2. **WorldTick** 唯一世界时间轴；**ActionClock** = Action Duration（ADR-0018）  
3. **Dead ≠ Removed**；Incapacitated 非死；Recovered→Alive（ADR-0019）  
4. **FocusCharacterUnavailable**；DirectControl≠Focus≠Leader≠Identity（ADR-0020）  
5. 开局三人隶属压迫宗门劳役；主管同宗管理者（`2G`／`34`）  
6. 地图 **World／Region／LocalMap**；修订 `24`（ADR-0021）  
7. **Core M1** 范围冻结（ADR-0022）  
主契约文件：`33-architecture-core-rules-freeze-v0.2.md`；v0.1 改为指向 stub。

---

## 2026-07-31 — 架构契约一致性审计报告 v0.1

完成只读审计，产出 `50-architecture-freeze-review-report-v0.1.md`。主轴数据流一致；主要冲突：`24` 地图正文过时、隐匿字段异名、Relationship 双写未定权威、FocusCharacter 失能规则缺失。建议小修补后再开 Core 第一阶段。不编码。

---

## 2026-07-31 — 架构冻结增量：死亡／控制权／Mod Ready

在上一轮文档包之上补三类不可后补的契约（仍不编码、不扩 Demo）。

**为什么补：**

1. **永久死亡默认 + TemporaryProtection**：若默认“剧情重要=不死”，选择与因果差异化会被架空；保护必须显式、阶段性。  
2. **Membership／Role／Relationship／ControlAuthority + PlayerAgency**：单一 IsPlayer／FactionId 无法表达失势领袖、客卿、离开后再敌对；势力领导权必须随职位动态得失，旧势力转 AI 继续。  
3. **Mod Ready／ContentPackage**：把“暂不承诺 Mod”改为正式长期目标但分阶段；官方必须与社区同管线，否则日后拆硬编码极贵。当前只冻结构，不写加载器。

**本轮：** `33` §19～21、`34` 生命周期与势力控制、`36` ContentPackage、`2E` 存档／事件扩展、`27`／`28` 对齐、术语表、路线图阶段 A～E、ADR-0010～0016；飞书说明改号 `37`。

**故意后置：** FocusCharacter 死亡接管细则、TemporaryProtection 事件模板库、阶段 B 加载器、Workshop。

---

## 2026-07-31 — 架构冻结文档包收口（不编码）

**为什么现在冻结这些边界：** Demo 已验证「下令→行动→劳动／偷修→世界在动」的手感；若直接开写 Core，会在 ECS vs 组合、事件溯源 vs 快照、Intent 层、多乘区、私有倒计时等分叉上反复返工。先把可实现、可测试、可维护的契约写死，正式开发才有唯一依据。

**本轮写入：**

- 主契约扩写 `33`：总架构、双层时间、实体、Order/Action、事件账本、地图四类、多队离屏、战斗、AI、军队边界、永久保存、校验
- 新／完善：`34` 实体与组件、`35` Order/Action、`2C` Modifier 公式、`2E` 事件与 WorldLedger
- 桥接 `32`：Demo 类映射；公开概念去掉 Intent
- ADR-0002～0008；总览 v0.6、术语表、路线图 M2.5「架构冻结文档包」、AGENTS 必读
- 原 `35-feishu-sync` 改号为 `36`，把 `35` 留给 Order 系统

**故意后置：** 第一次突破事件细则、炼气术法清单、`24` 正文全面改写、AttributeId 全表、Knowledge 传播公式、正式 UI（ADR-0009）、任何 Core／Demo 代码。

**为什么不采用：**

- **Unity ECS：** 高精度实体规模在 30～50 + 群体层，组合模型更利单人+AI 与序列化；见 ADR-0002
- **完整事件溯源／回放：** 成本高且拖垮开发期规则迭代；快照+ScheduledEvent+必要账本足够「世界记得」；见 ADR-0005
- **完整 GOAP／每类巨型行为树：** 用时间表+效用+统一 Action 更可解释、可维护

**下一步：** 人工审核本包 → 通过后再写突破规格 → 再开 Core asmdef。

---

## 2026-07-31 — Milestone 3.5：统一角色行动与交互框架

建立 `CharacterAction` / `CharacterActionController`，工作与修炼复用同一套「移动→交互→进度→产出／中断」。

- 右键地面=移动；右键工位=采集木材／草药／耕作；右键灵地=开始修炼
- 自动走近交互距离后进入 Working／Cultivating；暂停与倍速影响进度
- 新命令中断旧行动；超时无法到达则回 Idle 并提示原因
- 选中栏显示行动／目标／进度；头顶短状态文字；产出飘字
- 数值来自 `WorkZone` / `ActionSettings`，不写死在移动脚本

---

## 2026-07-31 — 选中单位路线预览

选中角色有移动／赴工／追击目标时，画预览线到落点：绿=移动、黄=赴工、红=追击；入定中不画。

---

## 2026-07-31 — 修炼语义：停下入定，非选目标

明确修炼与工作／攻击的交互差异：工作／攻击是 RTS 选目标；修炼是收敛打坐。

- 按 C：先停掉当前工作／移动／交战，再尝试入定
- 仅在灵地内才真正入定涨修为；站在外会提示「已停下·需在灵地入定」
- 入定有压扁打坐姿势 +「入定」飘字；移动／离开灵地／X → 出定
- 按钮文案改为「入定／出定」

---

## 2026-07-31 — 补基础 RTS 体验交互

补齐此前缺失、但很影响试玩可读性的交互：

- **攻击(A)**：红指针选 NPC → 追击进入交战（闪红、头顶标）；离开／移动则停（伤害待战斗系统）
- **移动落点 X**、**工作产出飘字**、**头顶活动图标**（移／工／修／攻）
- **双击己方单位** = 全选三人

---

## 2026-07-31 — 工作指令改成 RTS 选目标

试玩反馈：应是「点工作 → 指针变样式 → 再点工位寻路开工」，不是到了工位旁再点工作直接开工。

- `工作(W)` 一律进入选目标模式（黄圈指针 + 工位高亮）
- 左键点工位／工作区：寻路过去并开工
- 右键／Esc：取消选目标；平常右键移动会停止当前工作
- 离开工位且未在赶往工位：自动清工作状态

---

## 2026-07-31 — 交互验收迭代汇总（可玩性包）

试玩驱动的一揽子 RTS／经营可读性改动，方便验收压迫感与分工偷修循环。

**操控**：框选／点查；中键拖地图；底部常驻状态栏；点 UI 不丢选中；S 停止  
**查看**：可控三人数值栏；NPC 只读身份栏；主管红三角／守卫黄三角  
**工作**：黄色多工位；`工作(W)`→点工位才开工；右键只移动  
**劳役表**：全村一张村规（右侧竖栏）；村民按表走动；被发现才涨愤怒  
**氛围**：主管／守卫巡逻；村民按表干活  

明细见本文件下方各条与 `44-session-handoff-2026-07-31.md`「本轮改动汇总」。

---

## 2026-07-31 — 显式工作指令 + 多工位

试玩反馈：到区自动开工不合理；大片农田应有多处可操作点。

- 工作区生成多个黄色工位圈（农田5／森林4／草药3）
- 选人 → **工作(W)** → 再点工位才开工；若已在工位旁则直接开工
- 右键工位／空地只移动，到达不自动工作
- Esc 取消「选工位」模式

---

## 2026-07-31 — 全村劳役表 + 被发现才涨愤怒

按设计语义重构课表：一张主管规定的全村表，不再 PA/PB/PC 分别排班。

- `ScheduleService` 改为单一 `villageSchedule`；村民 NPC 与愤怒判定共用
- 课表 UI 改为一行「村规」；文案说明不直接指挥三人
- 愤怒：工时内未工作 **且** 处于主管/守卫检测范围内才累计
- 威胁头顶色标（上一条）保留

---

## 2026-07-31 — 威胁头顶色标

- 主管头顶红色三角；守卫头顶黄色三角（村民／商人无）
- 运行时按 `ThreatLevel` 自动挂载，未重建场景也可显示

---

## 2026-07-31 — NPC 只读状态栏 + 移除左侧「状态」面板

试玩反馈：需区分主管／村民；NPC 也应底部查看；左侧「状态」与底部重复。

- 点 NPC 显示底部只读状态栏（身份／威胁／当前活动），不可操作
- 可控三人仍用底部操作栏；点 NPC 会取消选中
- 删除左侧「状态」面板（三人汇总与底部重复）
- 左侧「详情」保留建筑／工作区／灵地；人物信息改看底部栏

---

## 2026-07-31 — 角色状态栏常驻 + 氛围 NPC

试玩反馈：底部应显示修为／灵气等数值；点操作不应取消选中；世界太死板。

- 单人选中底部状态栏：境界、修为条、暴露条、灵气环境／吸收、课表与指令、操作按钮
- 点状态栏／侧栏／顶栏不再触发世界取消选中；仅点空白或其他单位／建筑时切换
- 主管、守卫、商人简易巡逻；生成 4 名氛围劳工按课表去农田／森林／吃饭／睡觉
- 未重建场景时 `AmbientWorldBootstrap` 运行时自动补挂

---

## 2026-07-31 — 单位操作条 + 停止指令 + 区域／灵地标记

试玩反馈：进农田后无法取消工作；找不到灵地；缺少修仙模拟器式「选中后给操作」。

- 选中单位时底部显示操作条：停止(S) / 修炼(C) / 停修(X) / 敛息(G)，并显示当前指令
- `S` 或「停止」取消移动、工作与修炼
- 工作区在地图上显示名称边框；东南灵地有青色菱形标记与屏幕标签
- 未重建场景时运行时自动补挂标记组件

---

## 2026-07-31 — 中键拖拽移动地图

试玩反馈：80×50 地图无法平移，验收受阻。补齐 RTS 基础镜头操控。

- `CameraController`：鼠标中键按住拖拽平移（抓取地图）；仍受 `WorldBounds` 限制
- 滚轮缩放逻辑不变；HUD 帮助文案补充中键说明

---

## 2026-07-31 — 基础 RTS 交互：框选 + 点选查看

试玩反馈：没有框选、不能点建筑／农田看状态，会严重干扰对压迫／分工循环的验收。先补最小操控，不做夺府指令、不做正式 UGUI。

- 左键拖拽框选可控制角色；单击点选；Shift 追加／取消
- 左键点民宅／仓库／主管府／工作区／灵地 → 左侧「详情」面板（只读）
- 工作区详情显示产出、区内人数、正在工作人数；灵地显示可修炼与修炼人数
- 建筑点选只查看，不下指令；右键工作／移动逻辑不变
- 场景生成器写入 `StructureInspectable`；未重建场景时运行时按对象名补挂

---

## 2026-07-31 — 补齐跨设备交接文档

此前有 `42-devlog` 与 GitHub 提交，但最新 `44-session-handoff` 仍停在 7/30 策划日，换设备无法 30 秒恢复原型进度。

- 新增 `44-session-handoff-2026-07-31.md`：30 秒摘要、开工 5 步、操作、入口路径、约束、建议下一步
- `README` 补远端链接与交接入口；`41-roadmap` 勾选已落地的 M3／M3.5／M4／课表项
- 旧交接 `44-…-07-30.md` 顶部标明以 7/31 与 devlog 为准

---

## 2026-07-31 — Milestone 3.5：基础工作交互

补齐 RTS 式工作指派缺口：选中角色后右键工作区下达持续工作，右键空地自由移动。

- 角色状态：`Idle`／`Moving`／`Working`
- 仅 `Working`（已指派且位于对应工作区）产出木材／草药／粮食
- HUD「状态」面板显示正在工作／移动中／空闲与目标工作区
- 不再因“人站在区内”自动打工

---

## 2026-07-31 — 时间表网格（测试可改）+ 地块悬停灵气

- 时间表改为环世界式 **24 小时 × 三角色** 网格；点击格子循环 睡→起→工→饭→闲
- 正式设计仍应锁定；当前单机测试 `allowEditForTesting=true`，可「重置」回默认劳工课表
- 遵守检测改为按**每角色当前小时**活动判断是否该工作
- 新增地图格环境数据（属性能量／灵气／是否浓郁），确定性随机，灵地偏高；鼠标悬停浮层显示

---

## 2026-07-31 — 进入架构冻结阶段

Demo 功能开发结束。新增并冻结：

- `33-architecture-core-rules-freeze-v0.1.md`：Modifier 管道、Tick、四层模拟、炼气能力、突破事件、隐匿三层
- `32-prototype-to-product-bridge.md`：Demo 已验证语义 → 正式接口；重构换实现不改语义
- `2C-attributes-and-modifier-pipeline.md`：系统展开入口

同步更新：总览阶段、`AGENTS.md`、`31` 架构、`20-systems` 索引、`27`／`2F`／`21`、术语表、路线图 M2.5、handoff／`49`。

**本阶段不编码**；等待下一轮规则确认（2C 数据、2E、第一次突破细则）。

---

## 2026-07-31 — 文档：Demo v0.1 原型现状快照

新增 `49-demo-v0.1-prototype-status.md` 作为当前可玩版本单一入口；同步更新 `45-demo-v0.1` 实现进度表、`41-roadmap` M3～M5 勾选、`44-session-handoff` 代码索引；飞书全量同步。

---

## 2026-07-31 — Milestone 5：NPC 基础日程系统

让荒村按日程自主运转，为后续偷修／潜行提供基础。不做战斗、发现玩家、追捕、潜行判定。

- 新增可配置 `NpcScheduleConfig`（24 小时相位：工作／休息／巡逻）
- 守卫：巡逻点列表 + 路线 + 速度；状态 `Patrol`／`Rest`（夜间回休息点）
- 主管：白天巡视检查，晚上返回住所
- 村民：不逐人全模拟；`VillageCrowdPresenter` 按日程显示群体「工作中／休息中」
- NPC 头顶显示当前状态（巡视中／工作中／休息中）

验收入口：Play 后观察守卫沿路线巡逻，倍速到夜晚应回休息点；主管夜间归府；住宅旁可见村民群体状态。

---

## 2026-07-31 — Milestone 4 接入统一行动（暴露风险补齐）

在 M3.5 行动框架之上，补完秘密修炼验收环，仍不进入战斗／突破／功法／灵根／占领。

- 灵地增加可交互占用态：进入飘字「可修炼」，地图标签显示交互状态
- 行动制修炼同步 `Cultivating`；修为进度 0～1000，数值走 `CultivationConfig`
- **修复**：行动制修炼时补回暴露风险（夜低／昼高／主管附近额外）；只显示不惩罚
- 敛息草（初始 3，G 键）降低暴露；未入定时仍可在灵地缓慢采集
- HUD：修为／暴露／修炼状态／当前暴露增速；支持一人修炼、其他人继续工作

验收入口：Play 后右键灵地令角色 A 修炼，B／C 派往农田或森林；切白天看暴露上升，按 G 用敛息草。

---

## 2026-07-31 — Milestone 4：秘密修炼系统

验证核心循环：白天劳动 → 夜晚偷修 → 修为增长 → 隐藏身份。不进入战斗／突破／功法／灵根／占领。

- 隐藏灵地新增 `SpiritSiteZone`：进入后提示可修炼；未修炼时缓慢采集敛息草
- 角色新增 `UnitCultivation`：`Cultivating` 状态与修为 `0～1000`
- `CultivationSystem` 支持选中角色开始／停止修炼；移动会中断修炼；其他角色可继续工作移动
- 暴露风险：夜晚低、白天高、靠近主管额外增加；本阶段只显示不惩罚
- 新增资源 `ConcealGrass`（初始 3），使用后降低暴露风险
- HUD 新增修为、修炼状态、暴露风险与修炼操作按钮（C／X／G）

验收入口：打开 `Assets/Scenes/Demo_v0_1.unity`，将一名角色移到东南隐藏灵地按 C 修炼，其余角色继续派往工作区。

---

## 2026-07-31 — Milestone 3：基础荒村生活循环

只实现任务、工作、资源与主管愤怒，不进入战斗／修炼／突破／占领。

- 新增独立 `DailyTaskConfig`：每天 06:00 发布“木材 20、草药 5”，次日 06:00 结算；进度只统计发布后的新增资源
- 新增木材、粮食、草药数值库存与 HUD 资源面板
- 新增森林／草药区／农田三类工作区；角色进入后按游戏时间自动产出对应资源
- 新增 `SupervisorAngerSystem`（0～100）：任务未完成增加、工作时段每游戏小时检查未工作角色、任务完成减少；本阶段不施加处罚
- HUD 新增任务进度／剩余时间、资源和主管愤怒面板
- 场景生成器新增任务与愤怒配置资产、草药区占位地块和三个可配置工作区

验收入口：打开 `Assets/Scenes/Demo_v0_1.unity`，进入 Play Mode 后将三名角色分别派往森林、草药区和农田。

---

## 2026-07-31 — 灰盒尺度修正 + 基础时间表验证

按 Demo v0.1 下一里程碑落地，不进入战斗／修炼／突破／占领。

- 地图由 28×18 扩到 **80×50**，分区为住宅、主管府／仓库、农田工作区、森林、隐藏灵地，并拉开移动距离
- 角色根节点逻辑尺寸不变；`Visual` 独立缩放默认 **0.6**；碰撞体与选中圈独立
- 移动速度改为 **1.5**，使横向穿越约 **53 秒**（落在 45～60 秒目标）
- 新增滚轮镜头缩放，并用 `WorldBounds` 限制不超出地图
- 新增独立 `GameClock`：暂停／1x／2x／5x；默认 1 游戏日＝现实 8 分钟，可配 5～10
- 新增只读时间表配置 `DaySchedule_Laborer` 与面板；玩家命令优先，时间表不自动控制角色
- 工作时段检测是否在 `WorkZone`；违反仅调试显示，并预留 `ISupervisorAngerSink`（暂不处罚）

验收入口：打开 `Assets/Scenes/Demo_v0_1.unity` 进入 Play Mode。

---

## 2026-07-31 — Demo v0.1 原型正式开工

用户明确结束“只做策划”的等待阶段，要求不等待最终美术，先以可替换 Sprite 架构建立 Unity 原型。

- 锁定 Unity 2022.3.6f1 + Built-in，并更新 ADR-0001
- 建立 `Assets/`、`Packages/`、`ProjectSettings/` 工程结构
- 新增 `ReplaceableSprite`：角色、NPC、地块、建筑、UI 均通过独立 Sprite 引用
- 新增三人选择与移动：左键单选、Shift 多选、右键编队移动
- 新增荒村场景生成器：28×18 地块、住宅／主管府／仓库、主管／商人／守卫与玩家三人
- 新增 15 项占位 PNG 规格与 Prefab 生成规则；已有同名 PNG 不会被生成器覆盖
- 新增 `48-demo-v0.1-minimum-art-integration.md`

Unity 2022.3.6f1 个人版许可证随后已激活。批处理导入与生成验证通过，实际产出 15 个 Prefab、`Demo_v0_1.unity`，并将场景加入 Build Settings；二次无修改打开验证退出码为 0。

---

## 2026-07-31 — 新建 Demo v0.1 AI 美术生成批次计划

新增 `docs/40-process/47-demo-v0.1-ai-art-batches.md`，把美术需求拆成可执行的 AI 生成批次，**不立即生成全部素材**。

- 第一批 ≤10：只验风格（草地／路／灵地／树／敛息草／民宅／主管府／玩家A／妖兽／灵力特效）
- 第二批：服务探索→修炼→战斗→击败主管→占领
- 第三批：增强体验；城市／宗门／大地图／高阶技能／8方向明确排除
- 每项含顺序、数量、用途、Prompt、Unity处理、角色一致性
- 明确 AI 出静态／方向参考，行走逐帧人工二次制作

---

## 2026-07-31 — 新建 Demo v0.1 美术资源需求表

新增 `docs/40-process/46-demo-v0.1-art-assets.md`，将 Demo 范围转成可供 AI 生成、人工整理与后续 Unity 导入参考的资源规划。

- 统一分为 A 必须拥有／B 可占位／C 正式版替换
- Prototype 推荐 2D 3/4 俯视、32px Tile、64px 角色帧、64px 图标
- 第一批只要求 4 方向最小动画；8 方向完整动画延后
- 列出玩家三人、主管、商人、守卫、村民、人才候选、妖兽的资源与动画需求
- 列出地形、环境物件、建筑、战斗特效、UI、图标和目录命名规范
- 提供统一 AI Prompt、负面词与生成验收清单
- 明确 AI 不适合直接稳定交付多方向逐帧 Sprite Sheet，需人工统一

仍处纯策划阶段，不开始制作素材或搭建 Unity 工程。

---

## 2026-07-31 — 新建 Demo v0.1 设计文档

新增 `docs/40-process/45-demo-v0.1.md`，冻结第一个可验证闭环，明确不做完整游戏。

- 目标：验证玩家是否喜欢「凡人 → 修士 → 势力建立」
- 地图：一张荒村（住宅、主管府、工作区、农田、森林、隐藏灵地、妖兽区）
- 六阶段：入村 → 发现机缘 → 一人突破／两人护法 → 隐藏修炼与敛息 → 夺控制核心／击败主管 → 管理（时间表／人口／人才）
- 最小系统：三人分控、暂停／2x／5x、感应境／炼气／灵力、RTS＋一技能＋一妖兽、夺权后管理
- 完成标准：能完整体验 凡人→修炼→突破→隐藏→反抗→占领→管理

仍处纯策划：先补突破与夺府体验脚本／区域草图，不进入 Unity。

---

## 2026-07-31 — 前期节奏压缩、敛息、夺取控制权与学校人才

在不覆盖既有设计的前提下，整理近期讨论进对应章节。

**前期节奏（`20`／`2G`／`2F`）**

- 凡人阶段不是长期玩法；约 **40 分钟～1 小时**内从感应境进入炼气
- 主题改为：从被压迫的凡人，成为隐藏的修士
- 两段循环：凡人觉醒 → 隐藏修士（炼气后不能立刻公开）
- 离开荒村高风险而非禁止
- 新增敛息资源（如敛息草）：短时隐藏修为，需持续采集
- 三人分工：修炼／采隐藏资源／探情报

**据点夺取与管理（`26`／`21`）**

- 占领=夺取控制权，不是杀光；控制核心建筑（主管府／城主府）
- 三种方式：斩首夺权／逐步瓦解／外交接管
- 占领后第一项重要权限：修改时间表（影响生产、幸福度、人才）

**人才与成长（`27`／`26`）**

- 学校约每 2～3 月刷新人才候选
- 可收为弟子（需修炼潜力）或任命管事（不需修炼天赋）
- 成长循环：治理→人才→弟子→增强→扩地→再治理

下一步：第一次突破事件，或细化隐藏修士／势力发展。

---

## 2026-07-31 — 突破系统通用规则定型

在 `25-cultivation-and-breakthrough.md` 中补齐突破规则层，并同步第一章草案、术语、总览和交接文档。

- 修为达标只获得**突破资格**，不会自动升级；所有突破由玩家主动开始
- 小境界突破流程轻、风险低；大境界突破改变生命层次，需要环境、身体、心境、资源与护法
- 突破过程进入事件，可出现灵气不足、心魔、妖兽、敌袭与机缘
- 结果统一为普通／完美／瑕疵／失败，不再使用“优秀突破”旧称
- 突破会产生随境界增强的异象，并可能引发敌对或友好势力介入
- 同伴、宗门与地点可提供护法；加入势力的保护与资源成为实际价值
- 渡劫不覆盖所有突破，高境界逐渐出现；起始境界与因果影响待定
- 保留十境界顺序；“筑基→金丹／金丹→元婴”仅为机制举例，不删除结晶与具灵

下一步：设计第一次突破事件（感应境 → 炼气），或继续势力发展。

---

## 2026-07-31 — 世界三级结构、四层模拟、时间尺度与领地建设

在保留既有设计前提下，补充世界／人口／领地／修炼节奏，并复核战斗框架（不覆盖已定规则）。

**世界与据点（`24` 重写）**

- 结构定为：大陆 → 城市区域 → 格子地图
- 城市区域是连续地图（约 10 屏），不是单独城池；大陆约 100 屏
- 区域出口由地图数据决定
- 小格子：角色约 1 格，房屋／酒馆等约 4×5；未来地图编辑器编辑
- 地图数据方向：地形、资源、灵气（含属性）、建筑、人口群体、关键 NPC
- 废止早前“村庄 1.5 屏／城市 2–3 屏／短道路连接”的尺度描述

**人口（`27`）**

- 四层模拟：修士高精度／关键 NPC 实体／势力战略／普通凡人群体统计
- 凡人不逐人模拟；地图用代表性群体单位；关键凡人实体化

**领地（`26`）**

- 据点是可发展区域；可建洞府、生产、灵气、防御建筑
- 灵气来源：地形天然 + 建筑 + 灵物 + 多据点汇聚

**修炼时间尺度（`25`）**

- 开局→元婴约 60–100 小时游玩目标
- 正常资质（灵根 15–20／30）+ 黄阶高级：炼气→筑基约 100 游戏天
- 玄阶约 70 天、地阶约 20 天（示意）；低境界差距小、高境界差距扩大

**战斗（`23`）**

- 本次内容与既有小队框架一致，仅强化击杀进入因果／道德体系的表述

**后续讨论方向：** 突破系统、境界突破事件、势力发展。

---

## 2026-07-30 — 境界差异补充、小队战斗框架与灵气汇聚

在不覆盖既有世界观、时间、主管、属性、灵根、功法、江湖关系设计的前提下，补充三块规则。

**境界能力差异（`22`）**

- 明确每个大境界应改变移动、战斗、世界交互三类体验
- 成长闭环：境界改变能力 → 能力改变玩法 → 势力改变资源 → 资源推动修炼 → 修炼推动更高境界
- 筑基：控制外物与灵气（法器／附武／驭器／远程）
- 金丹：力量外放；内丹风格示例改为灼烧／恢复／治疗／麻痹／反伤／护盾；真正飞行方向倾向金丹，仍待最终确认
- 元婴：生命形式改变；补神魂战斗、神识压制、出窍风险
- 化神：补神识领域方向；保留原分神／化身讨论
- 新增**踏空**与普通飞行分层
- 空间能力：悟道原有虫洞讨论保留；羽化本次出现空间通道方向，归属冲突标待确定，不擅自迁移

**战斗框架（`23` 重写扩写）**

- 核心小队级修士战斗；明确不做数千人实时微操
- 大战用算法／战报表现
- 操作：左键选择，右键移动／攻击／互动，1–6 放技能
- 可学很多技能，战斗最多装备 6 个
- 手动／半自动／自动三种释放模式；AI 细则以后设计
- 普通攻击随境界改变方式
- 生命归零＝重伤非即死；默认不攻击重伤；可求饶／威胁／交易；放过产生后续关系
- 战利品按敌人实际携带结算
- 神识影响控制与压制；元婴后可神魂战斗

**领地灵气（`26`）**

- 占领地盘是为了修炼优势，不是扩图
- 每据点有灵气；多据点可把灵气汇聚到主洞府
- 不做复杂摆放；汇聚损耗／容量／可争夺性待确定

同步修订 `25`、`2D`、术语表、总览、索引、交接文档。

---

## 2026-07-30 — 功法系统规则层独立成章

新增 `2H-manual-system-rules.md`，只确定功法底层规则，不设计具体功法与数值。

- 功法是修仙成长核心，但不是职业限制；角色原则上可学习任何功法
- 灵根不构成禁学，只影响学习、掌握、修炼与发挥效率
- 功法可有境界、身体、前置知识、传承等客观条件；“可学”不等于低境界可完整发挥
- 黄／玄／地／天代表完整程度与成长潜力，影响修为获取、最大灵力、灵力恢复、灵力质量、续航与成长上限
- 高阶功法前期优势可以有限，随境界提高和修为需求增加而逐渐放大
- 修为与灵力继续严格分离：修为用于境界成长，灵力用于技能、护体与法宝
- 大部分功法不可升级品阶，角色通过寻找和更换功法成长；少数特殊机缘功法可成长
- 掌握程度提升不等于功法品阶提升
- 功法决定“如何修炼”，技能／斗技决定“如何战斗”
- 新增策划规则字段清单，但明确未进入数据库或技术实现

同步修订 `2D`、`2B`、`25`、总览、系统索引、术语表与交接文档。

新增未决问题：更换功法的转修成本、主修／辅修关系、灵力质量是否独立显示、功法成长上限的具体含义。

---

## 2026-07-30 — 统一属性体系，感应境不再等于特殊视觉

本次确定底层规则，不做数值与具体功法内容。

**统一属性结构（`2B` 重写为《角色属性与修仙成长体系》）**

- 玩家、NPC、凡人、修士、敌人**共用同一套属性**，区别只是开发程度
- 分四类：肉身；灵魂（神识／悟性）；修仙（灵根／灵力／修为／境界）；社会（性格、背景、家乡、关系、声望、目标、欲望）；另加心境
- 修为是成长资源、灵力是战斗资源，两套系统不混用
- 神识：社交洞察、探索感知、可控制的法宝／法术数量与上限、精神抗性；**明确不替代关系系统**
- 悟性：学得更快、更易领悟，**不直接提升技能威力**

**感应境重新定义（影响 `22`、`20`、`2G`、`25`、`28`、`27`、`23`）**

- 感应境**只是修炼境界，不是特殊视觉能力**
- 能力仅三项：感受天地灵气、基础吸收、进入正式修炼准备阶段
- 玩家能看见隐藏灵物、妖气、隐藏修士，来源改为**神识、天赋与经历**
- 因此一个神识出众的普通凡人也可能察觉异常，为"不起眼的 NPC 知道很多"提供了自然解释
- 已从上述文档中替换掉「感应境能力」作为感知手段的旧表述

**灵根与掌握程度**

- 灵根与「属性亲和」**合并为同一概念**，术语表原待确定项关闭
- 灵根以数值表示（示例：火灵根 15/30）；可跨属性学习但速度、熟练与效果下降
- 属性名单改为：火、金、土、木、雷、风、冰、毒；仍不做传统五行相克
- 技能与功法有**独立掌握程度**：初学→入门→小成→大成→圆满→化境（命名待定）
- 战力排序中的「属性天赋」改为「角色属性」，涵盖神识、悟性、心境

**新增未决问题**

- 新名单不含「水」，但 `22` 内丹风格表、`25` 与 `2G` 的属性环境表仍有水条目。已就地标注待确定，未擅自删除，需用户定案水是并入冰还是保留。
- 神识的社交优势上限如何设才不架空关系系统。
- 掌握程度六档的最终命名与各档实际差异。

---

## 2026-07-30 — 时间表、控制优先级与主管愤怒

补充时间与劳役系统的社会自由主题，不改动世界观、感应境、修炼与因果既定方向。

**时间系统（`21`）**

- 采用类似《环世界》的时间表：时间代表修仙社会中的自由程度
- 核心理念：低阶没有自己的时间，高阶与势力掌控者拥有更多时间自由
- 不同身份不同时间表；前期只可查看，后期可制定；Demo 可临时开放修改做验证
- 流速改为暂停／2x／5x；暂定现实约 5～10 分钟 = 游戏一天，待 Demo 调
- 角色控制：手动 + 自动跟随／按时间表；无命令默认待机；第一阶段无复杂自主 AI
- 行为优先级：玩家命令 > 紧急事件 > 时间表 > 待机；战斗中不强制切回时间表

**劳役与主管（`2F`）**

- 明确主题：玩家不是一开始没有力量，而是没有自由
- 新增主管愤怒值与低／中／高／极高阶段后果方向；数值待定
- 愤怒来源含违反时间表、未完成任务、私离区域、偷练／藏物被发现
- 主管系统服务于"逐渐获得自由"：前期守规则 → 中期用关系与能力降低风险 → 后期变成管理者
- 怀疑度与愤怒是否合并为一条风险反馈：倾向合并展示，标为待确定
- 待设计项按用户清单原样登记，不自行定稿

---

## 2026-07-30 — 术语统一：境界名一律写作「炼气」

此前文档里「炼气」与「练气」两种写法混用（正文多为"练气"，第一章标题与口头讨论多为"炼气"），违反术语表「禁止同义词混用」这条硬规则。

已全库统一为**炼气**，覆盖 17 份文档共 90 处，包括竞品拆解里对《鬼谷八荒》境界名的引用。术语表新增一行 `炼气 / QiRefining` 并注明不要写回「练气」，避免后续会话反复。

选择「炼气」而非「练气」的理由：这是讨论时的自然用法；趁尚未进入实现、代码与配置表里还没出现标识符时统一，成本最低。

---

## 2026-07-30 — 第一章体验流程草案：从凡人到炼气

新增 `2G-first-chapter-flow.md`。这是一份体验流程草案，不锁定人物、地点、名称与具体事件。

节奏骨架：劳役入场 → 感应探索 → NPC 机缘 → 多路线取得功法 → 秘密准备 → 引气入体进入炼气 → 低级妖兽教学战 → 管理者与制度矛盾伏笔。

关键补充：

- 初始地点暂称“青云宗外围资源点”；宗门保护地方但底层资源体系压迫劳役者
- 第一次炼气需要感应积累、功法、合适环境与不被打断的时间
- 结果方向为普通成功、优秀突破、失败反噬，具体判定待确定
- 炼气后获得灵气池、恢复与基础防御；战斗先耗灵气，耗尽后才直接损伤生命
- 突破后的低级妖兽战是首场修仙教学战，不等于第一次 Boss 战

---

## 2026-07-30 — 修炼／属性／功法体系定方向；世界观合并整理

**第一部分：世界观**

`29` 按《世界观哲学：天道、因果与修仙社会结构》合并整理，补强灵气分布与阶层表述、对抗邪修不计严重业障、元婴／化神滥用力量的示例。不重复开新平行章节。

**第二部分：修炼相关**

- `22`：炼气定为"正式进入修仙体系"而非巨大跃迁；感应境明确不能真正运转灵力、不能使用修士能力
- `25`：扩写第一份功法多路线、自然环境灵气／属性环境、灵物不叠放、阵法宗门期
- 新建 `2B-attributes-and-affinity.md`：七属性亲和；**明确不做传统五行相克**
- 新建 `2D-manuals-arts-and-equipment.md`：黄玄地天功法／斗技；战力排序境界>功法>斗技>属性>装备；低境界可获高阶功法但受容量限制

术语表同步：属性亲和、品阶、灵物、阵法；生克标为明确不做。本阶段仍无数值定稿。

---

## 2026-07-30 — 世界观哲学：天道因果与修仙伦理

把 `29-karma-and-consequence.md` 从简短的力量约束草稿扩成世界观／伦理专章。

核心方向：

- 世界不是正道善良、魔道邪恶的二分，而是围绕资源、寿命、修炼机会与境界突破运转的复杂社会
- 力量越强，因果限制越多；影响道心、气运、突破、渡劫与修行道路
- 杀戮按情境判定，不做"杀人 = 罪恶值"
- 凡人与修士存在阶层与利益交换；宗门压迫常是结构性结果，宗门不做脸谱化反派
- 玩家长期主题：从被压迫者成长为体系一环后，是改变体系，还是成为新的维护者

本阶段只记设计方向，不进数值与平衡。总览新增第 6 条设计支柱；术语表补功德、心魔、气运、天道等词。

---

## 2026-07-30 — 修正感应境：核心角色开局即具备

感应境不再是游戏过程中触发解锁的阶段。三名核心角色开局默认已经处于感应境，代表拥有修仙潜质，能够隐约感知并极低效吸收天地灵气；世界中的大多数普通人仍是真正凡人，不能感知灵气。

感应境主要用于感知灵气浓度、隐藏资源、灵物、异常区域、部分隐藏修士、妖气与特殊环境。它不能学习或运转正式功法，也不算真正修士。

玩家需通过探索、NPC 互动或机缘获得第一份功法；首次成功按功法正式运转灵气后进入炼气。设计目标也随之明确：玩家从开局就在逐渐发现一个隐藏的修仙世界，而不是通过事件突然获知自己可以修仙。

已从开局、境界、修炼、总览、术语表和交接文档中删除「感应境如何触发」的旧描述。

---

## 2026-07-30 — 开局章节展开：感应境、半固定背景与 NPC 隐藏经历

**做了什么**

围绕"开局体验／第一章流程"补了三块新设计，并同步到相关系统文档。

**1. 凡人与炼气之间插入感应境**

> **后续修正（同日）：** 感应境不是后续触发的过渡阶段；三名核心角色开局默认已经处于感应境。以下记录保留为设计演变历史，以最新一条为准。

这是本次最有结构性影响的一条。玩家不再直接从凡人进炼气，中间加一个过渡阶段：

- 感应境只给**认知**不给力量：能感知灵气、发现灵物与妖气痕迹，世界的样子变了
- **感应境不能学习正式功法**，必须进炼气才能修习并运行修炼体系
- 因此"获得功法"与"突破炼气"被拆成两个独立目标，可以互相牵引

判断：这一层解决了一个此前含糊的问题——玩家凭什么找得到秘密灵地、凭什么看出某个砍柴人不普通。原来只能靠事件推送，现在有了角色自身的能力来源。它也让开局多出一级可感知的进展，不必等到炼气才有正反馈。

**2. 三名初始角色改为半固定背景 + 玩家自定义**

要素：家乡、身世标签、性格标签、天赋倾向、外貌。固定部分负责产出剧情（回故乡、遇旧人、家族关系），自定义部分负责代入感。

约束写进文档：背景要素必须真的对应地点、NPC 或事件，不能只是角色卡上的文字；玩家改了背景，被引用的剧情也要跟着改。

**3. NPC 拥有可挖掘的隐藏经历**

NPC 不只是任务发布器。样板案例：砍柴人曾是低资质修士，修炼失败落回凡人，想把功法传出去。玩家通过聊天、观察、感应境能力三条途径挖掘。

这条让感应境第一次作用于**社交层面**而不只是探索：同一个 NPC，凡人眼中和感应境眼中不是同一个人。

**4. 开局玩法循环成文**

接受劳役 → 完成任务 → 获得生存资源 → 与 NPC 交流 → 获取信息和机会 → 偷偷探索 → 寻找修炼资源 → 提升实力 → 获得功法 → 进入炼气。

关键点写清楚了：劳役不是终点，而是换取信息与自由时间的成本。玩家在生存、任务、修炼三者间分配时间。

**改了哪些文档**

`20-opening-experience.md` 大幅扩写（新增初始角色、感应境、玩法循环、NPC 隐藏经历、待设计清单五节）；`22` 加入感应境并重排章节；`25` 新增"入门的两级门槛"；`28` 新增 NPC 隐藏经历一节；`27` 补初始角色与凡人的秘密；术语表补 7 个词。

**待解决（本次确认要做但未展开的五项）**

- 宗门劳役制度
- 第一次战斗流程
- 第一次突破炼气流程
- 第一处秘密修炼地点
- 三个初始角色之间的关系

另外新增的待确定项：感应境的感知范围、精度与极低效率吸收如何表达；感应境的"感知灵气"与炼气候选能力里的"灵气感知"是同一能力的两档还是两个东西。

---

## 2026-07-30 — 策划方向统一整理，多项结论回退为「待确定」

**背景**

明确当前处于**纯策划阶段**：不进入 Unity、不做技术架构、不写代码。本次按讨论内容统一整理策划案，并把此前由 AI 提前拍板、但实际尚未确认的设计**回退为「待确定」**。

**做了什么**

- `AGENTS.md` 与总览新增「当前阶段：纯策划」，规定遇到未定设计标注待确定，不得自行定稿
- 定位改写为：以修仙成长为核心的实时暂停式战略 RPG，融合个人修仙 RPG、RTS 式世界与战斗、领地经营、宗门经营、人物关系与江湖系统
- 开局补全：约 3 名角色、被迫进入宗门体系底层劳役、白天完成管理者任务、可同时给不同角色下达 RTS 式实时任务、**每天没有命令次数上限**、基础饭食只维持生存
- 前期核心体验写实：白天服从、晚上偷偷寻找修仙机会，形成风险／收益选择
- **战斗形态修正**：统一为"直接在世界地图中进行，不切换独立副本"。此前写的"据点战役进入独立战斗场景"已删除，大规模战争的承载方式改列为待确定
- 世界地图写入规模目标：3 块大陆、约 30 座城市，村庄约 1.5 屏、城市约 2–3 屏，自由缩放与连续无缝视觉切换
- **凡人系统回退**：不再断言"凡人按人口组管理、不逐个模拟"。改为策划目标"凡人真实存在于世界中"，分层模拟深度列为待确定；重要凡人可有姓名、关系、性格与故事；凡人军队要能参战但不必生成上千独立单位
- 正魔大战与宗门使者写入世界与关系文档，作为"江湖关系影响宏观势力发展"的载体

**回退的具体条目**

| 此前写法 | 现状 |
|---|---|
| 炼气解锁周天调息／灵气锻体／灵气感知（已定） | 炼气能力**暂定**，候选：灵气感知、灵力攻击、自然恢复增强、灵气护体 |
| 筑基使用「法器」，法宝留到金丹 | 按讨论改为筑基即使用**法宝**并学习**斗技**；是否分两级待确定 |
| 金丹解锁自主飞行（已定） | 飞行解锁于**金丹还是元婴，待确定** |
| 飞行不给通用闪避／增伤加成 | 恢复为"更高闪避、更高伤害、无视大量地形"的设计方向，**具体数值待确定** |
| 战斗分两档，战役进独立场景 | 统一在世界地图内进行 |
| 凡人不逐个模拟（明确不做） | 放宽为**待确定**，策划目标是凡人真实存在 |

**判断与理由**

- 之前几轮为了推进速度，把我的建议直接写成了已定稿，这会让后续设计建立在未经确认的前提上。本次统一改回讨论中／待确定，代价是文档"看起来没那么确定"，但避免了错误前提被继承
- 术语表移除了未确认的提案词（周天调息、灵气锻体、灵力灌注、丹相），改为在待定名词区登记，等能力定稿后再统一命名

**待解决**

- 炼气最终能力清单
- 飞行归属金丹还是元婴
- 凡人分层模拟的深度
- 凡人军队的参战表现方式
- 大规模战争在不切副本前提下如何承载

---

## 2026-07-30 — 十境界权限骨架与悟道空间网络

> **后续修正（同日）：** 本条中的炼气三能力、筑基法器、金丹丹相与自主飞行，均属当时的提案，未经确认。已在上一条中回退为「待确定」。境界名称与"每境界必须带来机制新能力"这两条仍然成立。

**做了什么**
- 境界名称定为：炼气、筑基、结晶、金丹、具灵、元婴、化神、悟道、羽化、登仙
- 修正炼气与筑基边界：炼气只负责体内循环（调息、锻体、感知）；灵力离体、法器、术法、御器与短距离御器飞行移到筑基
- 金丹定为丹相、属性本源、灵力外放、自主飞行与法宝；元婴定为出窍和死亡逃生；同时操纵第二躯体延后到化神
- 悟道加入空间规则：战术挪移／空间锁，以及可连接远方据点的空间虫洞网络
- 空间虫洞定为双端空间锚点基础设施，有建造与维护成本、通行容量，可被封锁、破坏与反向入侵

**判断与理由**
- 如果炼气已经御器，筑基相对炼气的规则优势不够明显；移动后，筑基管事弟子的第一卡点更可信
- 金丹解锁自主飞行比等到元婴合适：中期就能改变地图规则，元婴则专注第二生命
- 空间虫洞若能随手连接任意地点，会抹掉物流、地形和战争前线；把它做成可争夺的基础设施，反而能生成后期战略玩法

**下一步**
- 用户提到后期希望加入“两种”能力，目前只描述了空间力量，需要确认第二种
- 继续定义结晶、具灵、化神、羽化、登仙各自不可替代的新操作

---

## 2026-07-30 — 炼气能力、RTS 操作与宗门审查定型

> 后续修正：御器与灵力灌注已移至筑基；见上一条“十境界权限骨架”。

**做了什么**
- 参考《凡人修仙传》与《斗破苍穹》的能力边界，把凡人 → 炼气定为“首次获得灵力资源与操作权限”
- 炼气基础能力定为一套灵力系统的三种用法：周天调息、灵力灌注、御器；通过炼气小阶段逐步展开，防止一次塞三个按钮
- 明确炼气恢复不能治疗重伤，御器受距离／重量／灵力限制，持续御剑飞行留给筑基
- 战斗定为 RTS 操作语法 + 连续即时暂停：框选、编队、右键指令、Shift 队列、技能指定目标
- 明确筑基管事弟子不能被刚入炼气的三人正面数值碾过，必须先破坏飞行法器、消耗灵力、布置陷阱或争取内应
- 新增宗门使者审查：正魔大战提供暂缓处理窗口，使者报告综合据点产出、地方稳定、旧管事证据与私人关系；投其所好的礼物可以推动有利报告

**判断与理由**
- 单纯回血不够表达境界质变；“凡人只能卧床，炼气可以主动调息，但要付出时间与暴露风险”才是操作规则改变
- 炼气即可基础御器符合类型认知，但如果同时获得持续飞行，会提前透支筑基的辨识度
- 使者不能只因一件礼物无条件洗掉杀死宗门弟子的事实；礼物负责改变解释意愿，产出、稳定与旧管事证据负责让解释站得住
- RTS 指的是选取与下令语法，不是高 APM；暂停、指令队列和自动基础攻击继续负责降低操作负担

**下一步**
- 排前 5 天具体配额与教学节奏
- 设计宗门使者的身份、偏好、礼物来源与审查倒计时

---

## 2026-07-30 — 确认外门劳役身份与第一卡点，并约束心智负担

**做了什么**
- 确认身份：三人家贫被迫接受宗门外门劳役，本是凡人但有天赋
- 确认第一卡点：宗门派驻的管事弟子（约筑基初期），手下有凡人监工
- 在开局与义务文档中写入「按天数分层解锁」与「同时只盯一条怀疑度」的防过载约束
- 术语表区分管事弟子（StewardDisciple）与监工（Overseer）

**判断与理由**
- 外门劳役比「被强征的苦力」更贴合后续宗门关系：掀桌后要面对的是宗门态度，不是单纯地方豪强
- 筑基初期作为第一卡点合理：凡人阶段够压迫，炼气后有挑战但不至于无解
- 心智过载的真正解法不是砍掉明暗双轨，而是**不要第一天全开**；玩家每天主动决策约 5 次封顶

**下一步**
- 设计凡人 → 炼气能力矩阵（须服务战斗／跑图／隐匿）
- 排前 5 天具体配额与教学节奏表

---

## 2026-07-30 — 前期主玩法定型：明面配额与暗面积累

**做了什么**
- 新建 `2F-obligation-and-concealment.md`：把前期定为劳役处境，玩家白天完成监工配额换口粮，夜间偷偷修炼、狩猎、采药、炼制
- 定义怀疑度（Suspicion）作为暗面行动的统一代价，分五档后果，最高档是剧情分支而非失败结算
- 定义掩护、藏匿点、贿赂三个机制，让三人分头行动产生真实的策略取舍
- 完善 `21-core-loop-and-time.md`：确定指令模型（下达意图、角色自动执行、三人独立队列）、1 Tick = 15 游戏分钟、每 Tick 六步结算顺序、自动暂停规则、战斗与世界时间的两档处理
- 用一整天的时段表做了纸面推演，确认一天约 5–8 次重要决定
- 改写 `20-opening-experience.md`，把阶段一二从抽象的"求生与机缘"落成可玩的配额日常与偷练日常
- 术语表新增两组：时间与指令（Tick、Order、OrderQueue、AutoPause 等）、义务与隐匿（DailyQuota、Suspicion、Contraband、Stash、Cover 等）

**判断与理由**
- 前期一直缺一个持续的压力来源。配额加口粮解决了这个问题：官粮能活命但对修炼无益，"老实干活"这个消极解法被直接堵死，玩家必须冒险
- 明面与暗面的对立同时制造了时间张力（白天被占用）、风险张力（偷练会暴露）和资源张力（私藏会被没收），三者叠加让"挤出来的每一刻钟都有分量"，这正是"修炼要挑时间挑地点"能成立的前提
- 明确不做实时潜行（视野锥、噪音、蹲伏躲避）。它是另一个游戏类型，成本高且只服务前期一小段；抽象化的怀疑度则能平滑演化成后期的势力猜忌、情报与外交
- 指令模型定为环世界式的"指派 + 自动执行"，不是全战式直控。既然过程自动完成，玩家的乐趣就必须来自取舍，这一条决定了前期所有系统的设计取向
- 修正了此前"1 Tick = 一个时辰"的设想。那个粒度无法表达移动、采集和遭遇战，改为 15 分钟一 Tick、一日 96 Tick，表现层仍用时辰与时段
- 战斗与世界时间分两档：小队遭遇战不切场景、世界时间照常流逝，保留"A 在打架时 B 还在采药"的同时性；据点争夺这类战役才进独立场景并冻结世界
- 整套结构不是一次性的开场关卡。配额演化为宗门贡献与岁贡，藏匿点演化为暗桩，贿赂守卫演化为收买敌方要员，玩家开局学会的心智模型后期可直接复用

**风险与约束**
- 最大风险是这段变成"打工模拟器"。已写入节奏约束：劳役阶段控制在游戏内 15–30 天、实际游玩 1–1.5 小时，配额内容必须随天数变化，结束由玩家推动而非熬够天数

**待解决**
- 怀疑度按角色算还是按小队算
- 修炼气息外泄如何表达，是否需要"掩息"作为第一个功法
- 睡眠与体力如何建模（夜间修炼必然挤占睡眠，代价要在数值上体现）
- 1 Tick = 15 分钟的手感需要原型验证

**下一步**
- 设计"凡人 → 炼气"的能力矩阵，要求在战斗、跑图、隐匿三个场景都有用
- 把前 5 天的配额内容与秘密灵地的发现节点排成具体关卡表

---

## 2026-07-30 — 拆分文档结构：总览只留大纲

**做了什么**
- 把总览页的细节全部拆出，新建 10 份系统文档（20 开局体验、21 核心循环与时间、22 境界与机制能力、23 战斗、24 世界与据点、25 修炼与突破、26 领地经营、27 角色与人口、28 江湖关系、29 因果与业力）
- `00-overview.md` 精简为最高层大纲：定位、身份五阶段、五支柱、三层玩法、文档索引、依赖顺序、范围控制、跨系统未决问题
- 更新 `20-systems/README.md` 清单与依赖图，补齐尚未开始的基础层系统（五行、属性管道、功法词条、事件与世界状态）
- 更新 `AGENTS.md`：入口改为总览，并新增"总览只放大纲"的硬性规则
- `feishu-map.json` 增加 10 个系统文档条目，待用户建好飞书空文档后填 ID

**判断与理由**
- 单页塞满细节会导致两个问题：阅读时找不到重点，修改时容易冲突。分层后总览负责导航，系统文档负责深度
- 每份系统文档末尾都保留自己的未决问题清单，跨系统的问题才留在总览，避免同一个问题写在多处
- 21 核心循环与时间被标为下一份要完善的，因为其他系统都挂在它定义的时间轴上

**待解决（阻塞后续）**
- 21 的时间模型未定：暂停时能下什么命令、同一 Tick 内各系统如何排序结算、何时切手动战斗
- 三个跨系统分叉未定：战斗是连续即时暂停还是短回合、领地是自由布局还是槽位面板、境界命名体系

**下一步**
- 完善 `21-core-loop-and-time.md`，纸面推演"一天怎么过"

---

## 2026-07-30 — 核心游戏概念第一次收敛

**做了什么**
- 将用户关于“境界机制质变、个人到势力、领地扩张、江湖关系和真实修炼”的口述去重并整理为正式策划框架
- 确认玩家身份是从凡人小队逐步成长为修仙势力，不是纯个人 RPG 或开局宗门模拟
- 确认当前战斗方向为 2D 暂停即时小队战术，战力悬殊时允许自动结算
- 明确凡人人口群体化管理，核心修士逐个养成，长期目标上限约 30–50 人

**判断与理由**
- 项目真正的核心差异化是“大境界解锁实际机制能力”，而非普通数值成长
- 个人修炼与领地扩张必须互相供养，否则角色 RPG 与经营系统会成为两个割裂的游戏
- 完整概念已接近中型策略游戏，必须先验证“三人、一个村庄、两个据点、一次机制质变”的最小闭环
- 自由建造、完整天下关系网和后期大规模战争暂不进入第一阶段

**待解决（阻塞后续）**
- 暂停即时战斗的具体时间与行动模型尚未确定
- 领地采用自由布局还是据点槽位尚未确定
- 境界名称、能力矩阵和最终胜利条件尚未确定

**下一步**
- 编写《核心循环与统一时间系统》，先纸面推演角色行动、修炼、领地产出和战斗切换

---

## 2026-07-30 — 飞书云文档同步打通

**做了什么**
- 复用 ChatCCC 已有飞书应用凭据，完成 Markdown → 飞书 docx 的单向同步脚本
- 修复清空接口方法（DELETE）、表格只读字段剥离、按 children 切批等坑
- 首次成功将 `01-vision.md` 同步到飞书文档《攻城略地 修仙 策划案》

**判断与理由**
- 本地 Markdown 仍是唯一真源；飞书只作阅读与分享层，避免双向编辑冲突
- 不新建应用，复用现有机器人凭据，减少运维面

**待解决（阻塞后续）**
- 其余策划页的飞书文档 ID 尚未创建/填入 `tools/feishu-map.json`
- `01-vision.md` 的 Q1–Q5 仍未回答，方向尚未收敛

**下一步**
- 用户可继续新建飞书空文档并填入映射；或先回答愿景分叉问题

---

## 2026-07-29 — 项目启动，建立文档体系

**做了什么**
- 建立文档仓库 `D:\UnityProjects\XianXia`，初始化 Git
- 完成两款竞品的系统拆解：鬼谷八荒、了不起的修仙模拟器
- 产出横向对照，明确两者本质区别是"玩家身份"（修仙者 vs 宗门天意）
- 起草借鉴项 / 差异化假设 / 明确不做清单
- 确定架构原则：逻辑与表现分离、数据驱动（文本为真源）、数值可溯源、随机可复现

**判断与理由**
- 竞品的成本大头是内容量而非系统复杂度，单人项目只能走"系统做深、内容做窄、美术做省"
- 即时动作战斗列入"明确不做"，动画与手感是个人开发者投入产出比最差的部分
- 配置表用 CSV/JSON 而非 ScriptableObject，理由是可 diff、可 Git 合并、可被 AI 批量修改

**待解决（阻塞后续）**
- `01-vision.md` 的 Q1–Q5 未回答，尤其 Q1（玩家身份）不定则所有系统设计无法收敛
- Unity 版本与 UI 方案未定（ADR-0001、ADR-0002）

**下一步**
- 回答 Q1–Q5 → 定稿差异化 → 进入 M1 纸上原型

## 2026-08-30 — Phase 5R-B7B authority audit

- B7A 已由 `d551ea0` 封板（该提交已在 `origin/dev`；并非本轮执行）。
- 完成 WorldSite transit / destination authority 静态审计：目标 Site 才允许正式 ingress 与 `CompleteMove`，非目标 Site 保持最终目的地并继续 egress。
- 本轮未运行 Unity、未修改 Travel／Exit／Camera／Repath，也未提交 B7B。

## 2026-08-30 — Phase 5R-B7C legacy state cleanup

- 删除 PlayerParty `MandatoryWaypointSiteId` 及 Host 对应旧状态显示；确认无存档兼容影响。
- Host `GatewayConfirm` scaffold 因共享依赖尚未完全证明而延期，FormalArmy 逻辑未动。
- 完成静态 consumer audit；未运行 Unity／PlayMode／Test Runner，也未重新执行非 Unity compile；B7C 保持未提交。

## 2026-08-30 — Phase 5R Final Closure

- B7C 已封板于 `5da552a`（未 push）。
- 新增 5R 最终 WorldSite／Wilderness authority map、Pause 规则、KEEP／REMOVE／DEFER 与 Known Issues 总结。
- 未发现当前 PlayerParty 旅行范围内的已知阻塞问题；未修改 Runtime。

## 2026-08-30 — Phase 5S-A battle spatial authority

- 新增纯 Core `WorldSiteBattleSpatialPolicy`：OccupiedHexes 为 BattleArea，六邻接 union-minus-area 为 SupportRing1。
- 保留地形不可通行格的空间支援含义；未接入战斗结算、单位或旅行逻辑。
- 新增几何 EditMode 测试；未运行 Unity，未提交。

## 2026-08-30 — Phase 5S-A.1 existing battle audit

- 发现现有 `BattleEngagementSupportArea` 已提供 FormalArmy 接战的 footprint/support 构造，但 SupportArea 当前包含 BattleArea 且不做 world-boundary 过滤。
- PlayerParty→WorldSite 正式攻击入口未发现；Character Combat 与 FormalArmy 接战能力为 partial/implemented 混合状态。
- `WorldSiteBattleSpatialPolicy` 暂作为 provisional candidate，未接线、未修改 Runtime。
