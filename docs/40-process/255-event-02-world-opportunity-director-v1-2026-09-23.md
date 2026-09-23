# EVENT-02 — World Opportunity Director V1

> 状态：**Implementation Complete / Producer Acceptance Pending**  
> 日期：2026-09-23  
> 范围：Continuous Surface 上的动态 NPC Opportunity；EVENT-02A 持久活动动态见 [256](256-event-02a-persistent-world-activity-feed-2026-09-23.md)；不包含 hidden 与动态 WorldObject。

## 1. 已实现链路

`worldOpportunityDirector` 按当前 PlayerParty Surface 管理目标密度；`WorldOpportunityDriver` 只在该 Surface 当日首次 Tick 时补充机会。模板按 `weight` 抽取，并受 `maxActive`、`conditions` 与 Surface 约束。NPC 人物池直接复用 `SpawnTableDefinition`，SpawnZone 与 Opportunity 共同使用 Core `WeightedRandomPicker` 的权重算法；单实例选一个 Character definition，再复用 `ContentGameStart.BuildSpawnFromDefinition` 与 `GameStartBootstrap.SpawnIntoWorld`。

落点以玩家精确 Surface 坐标为中心，在 `[minPlayerDistanceWorld, maxPlayerDistanceWorld]` 内最多尝试 64 次极坐标候选；候选必须位于同一 `SurfaceGroundNavigation` coverage、可行走、满足距离、按配置避开 WorldSite，并与其它 active Opportunity 保持最小间距。失败不猜测 Site arrival、不放到非法点，当日不继续 roll。

成功后实体写入 `EntityLocationComponent` 与精确 `WorldPresence.SetAtWorldPosition`。Host 沿用现有 `ContinuousOutdoorSurfaceRuntime` 通用 materialization 与 fingerprint/reconcile，不创建 Opportunity 专用 View spawner。EVENT-02A 后，`publicNotice` 创建持久 WorldActivity 并额外发布一次 `WorldOpportunityNotice` Toast；`worldVisible` 无 UI 提示或 Activity。

## 2. 生命周期与存档

`SimulationWorld.WorldOpportunities` 保存已注册 Director/Spec、active instances、Entity→Instance 映射、下一实例序号与每 Surface 上次刷新日。实例使用稳定 `opportunity:<sequence>` identity，并记录模板、Surface、实体、创建日、排他到期日与发现方式。

到期时只允许执行 `setFlag`／`clearFlag`／`addCounter`／`setCounter`，随后清 Background travel、WorldPresence 与 EntityLocation，并把实体生命周期置为 Removed；已经死亡或 Removed 的 NPC 只做幂等 mapping cleanup。

`WorldSnapshot.CurrentSchemaVersion` 仍为 6。新增 optional `worldOpportunityRuntime` authority，保存实例、next sequence 与 Surface refresh states；旧 v6 缺字段视为空 board。存在 authority 时，非法或可能撞号的实例序号、重复 instance/entity binding、实体不存在、非法 definition id、非法 expiry 或重复 Surface refresh state 均返回 `SnapshotInvalid`。规则定义不进入 Snapshot，由 definitions-only rehydrate 从当前 Content 重新注册，不重放 Opening。

## 3. Event 绑定

`ContentEventDefinition`／`ContentEventSpec` 的 onTalk 新增 `npcTags[]` 与 `worldOpportunityId`。所有非空 binding 条件为 AND：指定人物按 DefinitionId exact match；tags 从当前 target entity 的 `PersonalityProfileComponent` 检查 ALL；Opportunity 通过当前 TargetEntityId 查询 active instance 并匹配模板。三者都为空时仍是 global onTalk contextual event。旧的仅 `npcDefinitionId` Event 行为不变。

EventEditor 的 Event Settings 可选择指定人物、输入必须标签，并从可读的 Name＋ID 下拉选择 Opportunity；按对象 Browser 将无固定人物的绑定投影到“通用人物模板”。Graph 与 Runtime dialogue flow 未改变。

## 4. OpportunityEditor

正式 WPF 工具位于 `ExternalTools/ContentAuthoring/OpportunityEditor/`，发布为 `Apps/OpportunityEditor.exe`。左侧分别列出 Surface Director 与 Opportunity，右侧表单编辑密度、Surface、权重、并发数、生命周期、玩家距离、WorldSite 许可、发现方式、结构化 Conditions 与受限 expire Outcomes。

人物池继续使用 `spawnTable`。工具可选择并编辑已有池，或在明确的 `Data/WorldOpportunities/world_opportunity_authoring.json` 中新建独立池；添加人物复用 Shared Character Picker。OpportunityEditor 决定“世界生成什么”，EventEditor 决定“互动后发生什么”。

## 5. 制作人验收内容

- Director：`base:world_opportunity_director_main_wilderness`，Main Wilderness 目标 2/2。
- Opportunity：`base:world_opportunity_event02_wounded_cultivator`、`base:world_opportunity_event02_temporary_merchant`，各 `maxActive=1`。
- 人物池：`base:spawn_event02_wounded_cultivator_pool`、`base:spawn_event02_merchant_pool`。
- 人物：`base:character_event02_wounded_cultivator_a`、`base:character_event02_wounded_cultivator_b`、`base:character_event02_temporary_merchant`。
- Event：`base:event_event02_wounded_cultivator_talk`、`base:event_event02_temporary_merchant_talk`。

验收内容经 EVENT-02A 调整为 2～3 world units；临时行商公开准确位置，受伤散修保持 worldVisible。两个模板各限 1 个，首次成功 refill 最终应各出现 1 个。

## 6. 验证与未实现

- 已完成 offline compile、Runtime BaseGame Content validation、Shared/EventEditor/OpportunityEditor build、Build All publish 与 `git diff --check`。
- 仓库规则禁止代理新增或运行自动测试，因此任务书列出的定向自动测试未执行；运行行为留给制作人人工验收。
- V1 只生成真实 NPC；`hidden` 明确 validation failure。动态 WorldObject、程序化人物生成、NPC 自动找玩家、Opportunity Chain、步行随机弹剧情与通用 narrative runtime persistence 均未实现。
- 本轮未 stage、commit、push、reset 或启动 Unity。
