# DELAYED-EVENT-01 — 延迟 ContentEvent 基础

> 状态：Implementation Complete / Producer Acceptance Pending | 优先级：P0 | 最后更新：2026-09-25

制作人本轮明确授权新增延迟事件及 Snapshot v10；不包含封板或提交授权。

## Scope 与 Explicit Non-Goals

现有 Step/Choice Outcome 可用 `scheduleEvent` 指定现有 Event Id 与正整数游戏日。复用 ContentEvent、Outcome transaction、Host presenter、Shared Outcome Editor；不增加 Graph Node 或剧情 VM。
不实现 Knowledge/Rumor/Information Propagation、ContentIntent、NPC 主动寻人、同行 AI、Quest 社交、交易、装备、制作与物流。

## 已确认实现契约

- ScheduledContentEventBoard 是 pending authority，每条独立递增 scheduled-event:N；不按 EventId 去重。
- deadline = 当前 WorldTick + Amount × WorldTick.TicksPerDay，checked arithmetic；不以日界或读档时刻重新起算。
- 保存原 Actor/Target/Issuer EntityId、TargetKind/Key/DefinitionId/DisplayName 及由 runtime 推导的 OpportunityInstanceId。
- 到期按 ExecuteTick、sequence 排序；目标定义或原实体/机会失效、Conditions 不成立则取消并记录原因；禁止模板替换。
- Host 单一安全入口判定战斗、正式对话、Modal、restore/transition；不安全保持 pending；Manual Pause 不等同 Modal。
- 成功进入 Active 后消费；scheduled 标记隔离普通 global once，原触发规则不变。事务回滚同时还原队列和 sequence。
- Snapshot v10 将 NextScheduledEventSequence 与 ScheduledEvents 纳入 ContentProgress；v1～v9 明确拒绝；按绝对 Tick 原样恢复。
- EventEditor 仅增量加入普通 Outcome 的事件选择与整数天数；验收内容复用真实临时行商，独立文件，不改原委托。

## 当前产品路线

当前 DELAYED-EVENT-01；下一 SOCIAL-QUEST-01（Quest Social Topic + Invite NPC To Join / Participate）；之后 Full Trading → Equipment / Crafting → Production / Logistics → NPC AI / Strategic Autonomy last。
Knowledge / Rumor / Information Propagation 为 Future / Only if gameplay later proves it necessary，不是近期任务或本轮依赖。

## 验证与验收

仅编译、Content JSON/引用与 Snapshot 接线静态检查、meta/GUID 检查；不启动 Unity、不运行测试。编译和静态验证不等于运行验收；本轮未 git add/commit/push。

## Runtime 与 Context identity 实际接线

`SimulationWorld.ScheduledContentEvents` 持有 `ScheduledContentEventBoard`；实例与 dispatcher 放在同一新增 Core 文件，Host 无权写 pending。
`ContentOutcomeApplier` 创建 schedule，事务 memento 保存列表与 NextInstanceSequence；任意后续 Outcome 或 finalize 失败时一起回滚。
`ContentEventBoard.ActiveScheduledInstanceId` 标记当前来源；成功 SetActive 后消费，完成不写普通 fired key。同 EventId 的两条实例可各执行一次。scheduled Event 即使 definition.trigger=onTalk 也走既有非交互 ContentInterrupt presenter，不重选话题。

每条持久化字段：InstanceId、EventId、ScheduledAtTick、ExecuteTick、ActorEntityId、TargetEntityId、TargetKind、TargetKey、TargetDefinitionId、TargetDisplayName、IssuerEntityId、OpportunityInstanceId。
Actor 不换成当前控制人物。NPC TargetKey 是原 EntityId 数字字符串；动态物体是 `opportunityObject:worldObject:opportunity:N`。Opportunity 由当前真实 Target 的 authority 索引推导，作者 JSON 不填写 runtime id。

## scheduleEvent schema

```json
{"kind":"scheduleEvent","id":"base:event_delayed_event01_merchant_followup","amount":1}
```

Id 必须存在，Amount 只接受 1～Int32.MaxValue 整数日。到期计算使用 checked；实际 `WorldTick.TicksPerDay=288`，不复制历史文档中的旧常量。分配序号耗尽时明确失败，不溢出复用 ID。

## Safe presentation 与取消

`HostContentInterruptPresenter.CanPresentScheduledEvent` 是唯一 gate：检查 InitialBootstrap、PendingRestoredStrategicSnapshot、Active Event、正式 Dialogue/Topic、Modal ownership、HostInputGate、CharacterEncounter、StrategicClockFreeze、ContinuousManualCombat、Separate Space 使用的 HostNpcMeleeAssault、Continuous Surface streaming transition。Manual Pause 不被等同于 Modal；暂停时可呈现已经真实到期的事件，不推进 Tick。

unsafe 时 dispatcher 不检查/消费 due 条目；恢复安全后才按 ExecuteTick/sequence 验证并呈现最多一条。目标定义不存在、原角色不存在/不再 Alive、原 Opportunity 到期/解决/移除或身份错配、目标 Conditions 不成立均取消，记录 LastDiagnostic，不每日重试。
固定世界物体复用可用 authority：旗被移除、破坏物已毁、runtime 建造物被移除均取消；无全局失效证据的 authored 物体保留稳定身份，不因 View 或 chunk 卸载误取消。没有新增物体注册表。

## Snapshot v10

ContentProgress 新增 NextScheduledEventSequence/ScheduledEvents，UInt64 wire 沿用字符串避免精度损失。Capture → JSON Serialize → Parse → Restore → definitions-only validation 已接通。
检查字段类型、canonical 唯一实例 ID、next 不复用、Actor 非 None、上下文形状、ScheduledAtTick≤WorldTick、正整数日 deadline 差与数值范围。rehydrate 后要求 Event 定义存在，现存 Target/Opportunity 身份错配拒绝。

自然失效来源的 future pending 可以合法保存；恢复保持原 ID，到期取消，而不是把整个旧来源替换成新 NPC。Restore 不调用 scheduleEvent、不改 deadline、不恢复已消费条目。Active Event/Dialogue 的既有禁存边界未改变，pending 不禁存。

## EventEditor integration

Shared `ContentFieldCatalog`/`SchemaFields`/`JsonArrayEditor` 增加“延迟事件 / Schedule Event”。事件选择显示 Name + Id，支持搜索当前 package 的 ContentEvent；搜索及刷新不触发 Changed，实际选值变更才写回。整数天数默认 1，保存验证拒绝空值、小数与非正数。
保留现有 Working Copy/Undo/Redo/dirty/保存重开通路，没有新增节点。EventAuthoringValidator 对 Step 与 Choice 同时验证，Runtime loader 拒绝非整数，ContentReferenceValidator 校验目标定义。
WorldOpportunity expireOutcomes 四种轻量结果的专用白名单保持其原有范围，不把失效回调扩成新的剧情入口。
已运行仓库 `publish.ps1` 的统一 staging/switch 流程，11 个默认编辑器已发布到既有 Apps；现有“启动-EventEditor.cmd”可直接使用新版本。

## Acceptance content 入口

New Game → LevelTester 反引号 → 内容 → 复用“QUEST-INSTANCE-01：生成两名同模板临时行商” → 实际与行商交谈 → “约定之后再谈（DELAYED 验收）”。
内容文件 `Content/BaseGame/Data/Events/delayed_event01_acceptance.json` 包含两个独立 Event；原任务文件未改。验收话题 Priority=50，与原 offer 同级；已有任务进行中 Priority=60 仍优先，先完成/处理原话题即可。

- 明日再谈：+1 日。
- 六日后再谈：+6 日，原商人五日期限结束后应取消。
- 明日再谈两次：同一 Choice 创建两个独立实例，后续 definition 故意保留 once=true 验证不被 global once 吞掉。
- “让后续条件不成立／恢复后续条件”：设置/清除 `acceptance:delayed_event_blocked`，明确影响所有待履约约定；也可用现有 Debug flag 输入。
- 每次 follow-up 正常完成后 `acceptance:delayed_event_presented` 加 1，LevelTester 内容区显示履约计数、所有 pending、deadline/remaining、原身份与最近 presented/cancelled reason；列表滚动高度随条目增长。

推进后关闭调试/其它模态面板，再观察安全世界弹出。

复用现有 Advance Days：它跳到后续日界，日中预约后点“1天”可能尚未经过完整288 Tick。请以显示的 ExecuteTick/remaining 为准，再自然推进；本轮不改变旧时间工具。

## Static verification

- `tools/offline-compile.ps1 -Only XianXia.Core,XianXia.Data,XianXia.Unity,XianXia.Unity.Editor`：四个程序集 ALL_OK。
- EventEditor `dotnet build --no-restore`：0 warning / 0 error；统一 11-editor publish exit 0、Apps switch succeeded。Legacy MapEditor 既有 nullable warnings 保留，不属于本轮修改。
- 直接调用既有 ContentPackageLoader.Load + ContentReferenceValidator.Validate：BaseGame 通过，25 个 ContentEvent；包括新增验收 JSON 与目标引用。
- 直接调用既有 JsonSnapshotSerializer 对纯 DTO 做 Serialize/Deserialize/re-encode 静态往返：schema=10，12 个字段原样保留，示例 WorldTick=200 / ExecuteTick=344，remaining=144；JSON 重编码一致。未做世界行为回放或存档游戏模拟。
- 静态核对 scheduleEvent 的 Runtime switch、loader 数量解析、reference scanner、Shared kind/form/validator；Snapshot capture/serializer/parser/restore/rehydrate 全链；active 标记与事务回滚；普通 once/repeat 路径保留。
- 新增 Core .cs 与 .meta 成对，GUID 唯一；Content 在仓库原有 Content/BaseGame 数据目录，不是新 Unity asset。
- 未启动 Unity、PlayMode、Runner；未新增/运行自动行为测试、测试脚本或框架。未改 ProjectSettings/Packages/Freeze/Demo Runtime，未实现 Knowledge/ContentIntent/NPC AI。

## Producer manual acceptance checklist

- [ ] 1. New Game 后能正常遇到现有“临时行商”，原 QUEST-INSTANCE 流程没有被新的验收 Event 永久遮挡。

- [ ] 2. 与行商进入 DELAYED 验收话题，选择“明日再谈”；当前 Event 结束后 follow-up 不立即出现。

- [ ] 3. LevelTester 能看到 1 条 pending scheduled instance，包含唯一 InstanceId、原 TargetEntityId 和原 OpportunityInstanceId。

- [ ] 4. 在到期前保存；退出/Load；scheduled instance 仍存在，Target / Opportunity identity 不变，剩余 Tick 正确，不从 1 天重新开始。

- [ ] 5. 世界时间到 ExecuteTick 后，在普通安全世界状态自动出现 follow-up ContentEvent。

- [ ] 6. Follow-up 的 @target 解析为原来的真实行商，而不是同模板的另一个行商。

- [ ] 7. Follow-up 完成后该 scheduled instance 被消费，不会第二次出现。

- [ ] 8. 对同一个 follow-up Event 连续建立两条 schedule；两条 InstanceId 不同，不能因为 EventId 相同而覆盖。到期后两条分别执行。

- [ ] 9. 如果场上同时存在两个同模板“临时行商”，分别从 A / B 建立 schedule；后续上下文分别绑定 A / B，不串实例。

- [ ] 10. 选择“六日后再谈”，让原行商 Opportunity 在到期前 expiry / remove；即使后来还有另一个同模板行商，原 schedule 也必须取消，不能找替身。

- [ ] 11. LevelTester / diagnostic 能看到该 schedule 的 cancellation reason，且 pending 中不再存在。

- [ ] 12. 在战斗 / 正式对话等不安全状态附近到期时，不强行插入 Event；恢复普通世界安全状态后再呈现。世界 Tick 本身被冻结的状态不得伪造“已经到期”。

- [ ] 13. 目标 Event Conditions 在到期时不满足，则该 schedule 取消，不无限期重试。

- [ ] 14. EventEditor 可以添加 scheduleEvent，选择后续 Event 和延迟天数；保存、关闭、重开值不丢；Undo/Redo 与 dirty 正常。

- [ ] 15. EventEditor Graph、现有 Conditions/Outcomes、onTalk/onInspect、旧 Event 均无回归。

- [ ] 16. Snapshot 已升级到 v10；v10 Save/Load 正常；旧 schema 按当前严格策略明确拒绝，不 silent migrate。

- [ ] 17. 对话进行中依旧禁止保存；没有扩大其它禁存范围。

- [ ] 18. 原 QUEST-INSTANCE、DYNAMIC-DISCOVERY、Activity、WorldMap、控制恢复等已封板功能没有被改造成新架构或产生明显回归。

## 实际修改文件清单

- `Assets/Scripts/Core/Content/ContentEventBoard.cs`
- `Assets/Scripts/Core/Content/ContentEventService.cs`
- `Assets/Scripts/Core/Content/ContentEventSpec.cs`
- `Assets/Scripts/Core/Content/ContentOutcome.cs`
- `Assets/Scripts/Core/Content/ContentOutcomeApplier.cs`
- `Assets/Scripts/Core/Content/ScheduledContentEventBoard.cs`
- `Assets/Scripts/Core/Content/ScheduledContentEventBoard.cs.meta`
- `Assets/Scripts/Core/Persistence/ContentProgressSnapshotHelper.cs`
- `Assets/Scripts/Core/Persistence/SnapshotService.cs`
- `Assets/Scripts/Core/Persistence/WorldSnapshot.cs`
- `Assets/Scripts/Core/Simulation/SimulationWorld.cs`
- `Assets/Scripts/Data/Content/ContentPackageLoader.cs`
- `Assets/Scripts/Data/Content/ContentReferenceValidator.cs`
- `Assets/Scripts/Data/Serialization/JsonSnapshotSerializer.cs`
- `Assets/Scripts/Unity/Host/ContinuousOutdoorSurfaceRuntime.cs`
- `Assets/Scripts/Unity/Host/HostContentInterruptPresenter.cs`
- `Assets/Scripts/Unity/Host/HostDialogueController.cs`
- `Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs`
- `Assets/Scripts/Unity/Host/LevelTesterCheatContentSection.cs`
- `Content/BaseGame/Data/Events/delayed_event01_acceptance.json`
- `ExternalTools/ContentAuthoring/Shared/ContentFieldCatalog.cs`
- `ExternalTools/ContentAuthoring/Shared/EventAuthoringValidator.cs`
- `ExternalTools/ContentAuthoring/Shared/JsonArrayEditor.xaml.cs`
- `ExternalTools/ContentAuthoring/Shared/SchemaFields.cs`
- `docs/00-project/00-overview.md`
- `docs/00-project/03-glossary.md`
- `docs/20-systems/2E-events-and-world-state.md`
- `docs/30-tech/36-content-package-and-mod-architecture.md`
- `docs/40-process/247-project-handoff-current-state-2026-09-18.md`
- `docs/40-process/263-delayed-event-01-content-event-scheduling-2026-09-25.md`
- `docs/40-process/41-roadmap.md`
- `docs/40-process/42-devlog.md`
- `docs/40-process/43-decisions/ADR-0040-delayed-content-event-authority-and-snapshot-v10.md`
- `docs/40-process/43-decisions/README.md`

## 当前工作树与状态

起始工作树为空；本轮仅上述范围改动，未 stage/commit/push。Apps 与编译缓存属于既有忽略目录，发布不产生已跟踪二进制变更。

**Implementation Complete / Producer Acceptance Pending**
