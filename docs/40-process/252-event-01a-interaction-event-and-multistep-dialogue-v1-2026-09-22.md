# EVENT-01A — 互动事件仲裁与多步骤对话 V1

> 状态：**Implementation Complete / Producer Acceptance Pending**｜优先级：P0｜最后更新：2026-09-22
> 范围与规则真源：[2E §0.1](../20-systems/2E-events-and-world-state.md#01-event-01-final-已批准实施契约2026-09-22)。本轮为制作人明确授权的新扩展；95／96 当年的“多段对话树不做”只是历史切片边界，不是当时遗漏的 bug。
> **后续：** 本页保留 EVENT-01A 实施切片；WorldObject onInspect 与 Load definitions-only 已由 [253 EVENT-01 Final](253-event-01-final-fixed-world-interaction-acceptance-2026-09-22.md) 接续完成。本页关于“留给 EVENT-01B”的文字只描述当时边界。

## 交付与边界

沿用 ContentEvent、ContentEventBoard、ContentEventService、ContentCondition／Outcome 与既有 Outcome transaction；沿用 HostDialogue Controller／Model／UGUI／IMGUI、Content Interrupt 和 WPF EventEditor。没有平行 Dialogue System、Graph Editor、Opportunity Director、事件队列或剧情 persistence。正式 Host 接线仅到已有 NPC 靠近／Hold／Talk arrival 链，World Object Inspect 留给 EVENT-01B。

## JSON 形状

下面是一条 definition，制作时放在内容文件的 `definitions` 数组中。它只示范格式，本轮没有改写正式 BaseGame JSON。

```json
{
  "id": "base:event_01a_example",
  "type": "contentEvent",
  "name": "后山之事",
  "trigger": "onTalk",
  "npcDefinitionId": "base:character_ch01_ref_supervisor",
  "priority": 10,
  "topicText": "谈谈昨晚的事",
  "once": true,
  "onceScope": "perTarget",
  "conditions": [],
  "entryStepId": "ask",
  "steps": [
    {
      "id": "ask",
      "speakerRef": "@target",
      "text": "昨晚你去了哪里？",
      "outcomes": [],
      "choices": [
        { "id": "truth", "text": "说实话", "conditions": [], "outcomes": [], "nextStepId": "player_truth" },
        {
          "id": "cultivation", "text": "谈谈修炼",
          "conditions": [{ "kind": "realmAtLeast", "realm": "QiRefining" }],
          "unavailableMode": "disabled", "requirementText": "队伍中须有人达到炼气境",
          "outcomes": [], "nextStepId": ""
        },
        { "id": "leave", "text": "以后再说", "conditions": [], "outcomes": [], "nextStepId": "" }
      ]
    },
    { "id": "player_truth", "speakerRef": "@actor", "text": "我昨晚去了后山。", "outcomes": [], "choices": [], "nextStepId": "reply" },
    { "id": "reply", "speakerRef": "@target", "text": "下次记得早些回来。", "outcomes": [], "choices": [], "nextStepId": "" }
  ]
}
```

- `priority` 缺省 0；`topicText` 空时依次回退 Name／短 Id。
- `once=false` 可重复；`once=true` 且未写 `onceScope` 为 global。`perTarget` 对实际 Target EntityId；`perActorTarget` 对实际 Actor×Target。没有实例 Target 的旧 API／非互动 trigger 不猜目标，不能触发 target-scoped once。
- `speakerRef` 空／null 为旁白，`@actor`／`@target` 使用 Begin 时的绑定实例；显式 character DefinitionId 由 ReferenceValidator 检查，runtime 缺失／多实例歧义明确显示诊断，不回退到其他人物。
- Choice 的 `unavailableMode` 缺省 `disabled`，显示 RequirementText／“条件未满足”；`hidden` 完全不进入 UI。Choice.Text 只是选择意图；玩家说话通过后续 `@actor` Step 表达。
- 不写 Steps 的旧 body／choices 映射成 `$legacy` 单步：onTalk 使用 @target，其余旁白；旧 Choice 无 next 即完成。Loader 不修改 JSON；EventEditor 保留旧编辑区，不自动迁移。
- Loader／ReferenceValidator 拒绝 mixed format、无效 enum、重复 Step／Choice Id、缺入口／Next、环、无可达 terminal、有 Choices 又有 Step.Next，以及非 terminal minigame choice。步骤 Outcomes 不接受 startMinigame，它必须位于 terminal Choice。显式 `steps: []` 也拒绝。

## 仲裁、条件与运行态

`ResolveInteractionCandidates` 收集 trigger／NPC DefinitionId／repeat／conditions 合格项，只保留最高 Priority，再按 Event.Id ordinal 排序。0 个保持 fallback，1 个直接 Begin，多于 1 个展示 Topic Selection；Topic 点击时重新解析候选，防止使用过期资格。取消话题不会 Begin／MarkFired。

`InteractionConditionsPass` 独立于全局 `ContentConditionEvaluator.AllPass`：只有实际 Actor 属于 PlayerPartyContext 时，才允许任意一个有效存活成员独自满足整组条件；不能跨成员拼条件。Event、Choice 显示和点击最终复核共用这个规则。Actor 不在队伍时回退 actor-only。Outcome subject 仍是实际 Actor。

ContentEventBoard 的 session runtime：ActiveEventId、ActiveStepId、ActiveActorId、ActiveTargetEntityId、ActiveInteraction。Begin 一次绑定；Host 不在每一步重选玩家／NPC。Global fired key 仍为 Event.Id，保持 HasFired(eventId)；目标范围使用 `#target:<id长度>:<eventId>:<target>`，角色×目标范围使用 `#pair:<id长度>:<eventId>:<actor>:<target>`。这些 key 与完整 active context 一起 Capture／Restore。

无 Choices 的“继续”与 Choice 点击都调用 ResolveChoice：先复核资格，合并 Step.Outcomes＋Choice.Outcomes 到原有单个 transaction，再前进或结束。失败保留 active step／context，并恢复 flags／counters／quests／fired／事件队列及既有事务覆盖状态。只有 Finish 才写 fired、发布一次 ContentEventResolved 并清 active。完成时 Quest Evaluate 在当前 active 占用期间进行，避免自动接播另一个事件。

## Host 与 EventEditor

- onTalk 使用原底栏：Topic Selection 可 Esc／“离开”；正式 Event 只能通过 Continue／正常终止 Choice 结束，Esc 不清 active。原打字机、具名 Modal Pause 与 BlockWorldInteraction 保持。两个 View 的选项支持滚动；UGUI 按 Event＋Step 重置打字机，避免相同正文跳步时漏刷新。
- 非 onTalk 保留 first-match trigger 仲裁及原 Interrupt 容器，但复用 Controller 构建当前 Step／Speaker／Continue／Choice 和小游戏处理。没有第二套 Step 执行逻辑。
- terminal startMinigame 在完整结算成功并 Finish 后打开既有姜老棋局；失败不启动，游戏结果回调仍走原逻辑。没有小游戏后返回 Step 的 continuation。
- EventEditor 左侧事件列表保留，顶层增加优先级／话题／四种重复规则。新建默认 Step；入口、步骤列表、说话人、正文、Step Outcomes、Choice 条件／不可用模式／说明／Outcomes／Next 都是中文表单。条件与结果复用 Shared.JsonArrayEditor。
- 步骤支持增删、上下移；重命名更新本事件入口和 Next 引用；删除后悬空引用由保存校验拒绝。下一步选择“结束事件”即空 Next。有 Choices 时禁用 Step.Next。旧事件显式显示“旧单页事件（兼容格式）”。
- Shared 的字段表、PackageBrowser 校验和任务关联查询同步识别 Steps；不会把 Step 中的 startQuest 漏掉。
- 打开本轮构建：`dotnet run --project ExternalTools/ContentAuthoring/EventEditor/EventEditor.csproj --no-build`，或运行 `.build/EventEditor/bin/Debug/net8.0-windows/EventEditor.exe`。本轮只 build，没有批量 publish；既有 Apps 发布副本不会自动更新。

## 修改文件清单

代码目录均相对仓库根：

| 范围 | 文件 |
|---|---|
| Core/Content | ContentEventSpec.cs、ContentEventBoard.cs、ContentEventService.cs、ContentOutcomeApplier.cs |
| Data/Content | ContentEventDefinition.cs、ContentPackageLoader.cs、DefinitionSchema.cs、ContentReferenceValidator.cs；新增 ContentEventStructureValidator.cs 与 .meta |
| Data/Bootstrap | ContentRuntimeBootstrap.cs |
| Unity/Host | HostNpcContextMenu.cs、HostDialogueController.cs、HostDialogueModel.cs、HostDialoguePresenter.cs、HostDialogueUguiView.cs、HostDialogueImguiView.cs、HostContentInterruptPresenter.cs |
| 现有测试文件 | Assets/Tests/EditMode/ContentEventOnTalkTests.cs（仅补定向用例，不执行） |
| EventEditor | MainWindow.xaml、MainWindow.xaml.cs |
| Shared | SchemaFields.cs、PackageValidator.cs、PackageStore.cs；新增 EventAuthoringValidator.cs |
| 文档 | 03-glossary、2E、111、117 增补、41-roadmap、42-devlog、247 handoff、本记录 |

Core／Data／Unity 文件位于 `Assets/Scripts/`；EventEditor／Shared 位于 `ExternalTools/ContentAuthoring/`。工作区原有未跟踪 `ExternalTools/ContentAuthoring/EventEditor.zip` 保留。

## 验证与人工验收

- `tools/offline-compile.ps1`：全仓程序集 `ALL_OK`。既有 `OpeningFactionMembershipTests.cs` 重复 using 与 `WorldTickTests.cs` 同变量比较两个警告，未改动它们。
- `dotnet build ExternalTools/ContentAuthoring/EventEditor/EventEditor.csproj --nologo`：成功，0 警告、0 错误。
- `git diff --check`：通过。新增 C# meta 有合法唯一 GUID。
- 附件特别要求存在轻量测试时补少量定向用例，因此扩充现有 ContentEventOnTalkTests：高优先级与同级候选、整组队友条件、实例 once 范围、legacy 隐式步骤、三步推进与绑定 Actor、失败回滚、非法 Next／cycle。只编译，不运行测试；hidden／disabled 与 Host 行为等待下列人工验收。
- 静态读取 BaseGame 现有 11 条事件，均为 legacy；选项 Id／NPC 引用／默认 once scope 未发现兼容冲突，未修改这些 JSON。
- 全局 ContentConditionEvaluator 未改；Snapshot serializer／DTO／CurrentSchemaVersion 未改，仍为 **6**，SPACE-01-P1 serializer patch 保留。ProjectSettings／Packages／Freeze／Demo 未改。
- 未启动 Unity，未运行 PlayMode、Test Runner、batchmode、headless 或行为矩阵。编译／静态核对不代表人工验收。
- 未 stage、commit、push；状态保持 **Implementation Complete / Producer Acceptance Pending**。

制作人人工验收（在制作包副本中配内容，避免污染正式剧情）：

1. EventEditor 新建 Step 事件，保存重开；旧事件打开保存后仍是 body／choices。配置无效 Next／环／重复 Id 应拒绝保存。
2. 同 NPC 配 1 个低优先级和 2 个同高优先级事件：只出现高层话题；Esc 可退出且不消费 once。只剩 1 个合格最高层时直接进入。
3. Choice → @actor → @target → 结束：每步台词／说话人正确，正式事件 Esc 不退出，结束回世界、不接播其他话题。多于屏幕容量的选项可滚动访问。
4. 炼气条件仅队友满足时可用；两个条件分别由不同成员满足时不可用；disabled 有说明、hidden 不显示。配置可失败 Outcome 时失败停留原 Step、先前 Step／Choice 本次结果不残留。
5. 同 DefinitionId 的不同 NPC 实例验证 perTarget，切换 Actor 验证 perActorTarget；中间步骤不消费 once，Finish 才消费。旧 once 缺省仍全局一次。
6. 用现有 non-onTalk trigger 触发新 Step，验证旁白／指定人物／继续／分支与结束；检查旧姜老棋局仍在选择成功后打开，新 terminal minigame choice 同样可用。

结构上可达 terminal 不保证每个时刻都存在满足条件的 Choice；作者应给正式事件保留一个无条件终止选项，避免全部条件选项不可用时无路可走。
