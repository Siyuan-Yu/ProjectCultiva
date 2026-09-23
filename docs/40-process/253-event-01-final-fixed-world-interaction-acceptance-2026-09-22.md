# EVENT-01 FINAL — 固定世界互动事件与制作人验收面

> 状态：**Implementation Complete / Producer Acceptance Pending**｜优先级：P0｜最后更新：2026-09-22
> 前置：[252 EVENT-01A](252-event-01a-interaction-event-and-multistep-dialogue-v1-2026-09-22.md)。本页记录 Final 增量与可直接制作的验收路线；未获制作人人工签收前不得写 Accepted／Sealed。

## 最终范围

EVENT-01 只负责固定世界中已存在 NPC／WorldObject 的主动互动：onTalk 与 onInspect 共用 Interaction Context、候选解析、party-aware Conditions、Priority／Topic、Steps、Outcomes 与 repeat gate。EVENT-02 才负责随机 World Opportunity 的生成、权重、期限和过期；本轮也不含 ContentIntent、跟随演出、自动玩家移动、镜头脚本或剧情 runtime 持久化。

`ContentInteractionContext` 保存 Actor、可选 NPC EntityId、TargetKind、稳定 TargetKey、目标定义／实例 ID 与显示名。NPC TargetKey 保持原 EntityId 数字字符串；对象为 `<worldObjectKind>:<stable-id>`。PerTarget／PerActorTarget 均使用该 TargetKey。

WorldObject Host 只对存在 eligible onInspect 候选的对象增加“调查”。点击后先关闭菜单并保持世界运行，以既有移动器前往 approach point；抵达时按 kind＋stable ID 重新找对象、比较身份并重新解析候选，随后才打开 Topic／Event 和 Modal Pause。查看详情、恢复、工作、攻击、拆毁等原菜单入口仍在。

## Load definitions-only

`ContentRuntimeBootstrap.RehydrateContentDefinitions` 是 New Game 与 Snapshot Load 共用的 QuestDefinition／ContentEventDefinition mapper。`RuntimeContentShellBootstrap.Rehydrate` 调用它，并调用 `ChapterRuntimeBootstrap.ApplyDefinitions`。该路径不会调用 OpeningInventory、OpeningScenario side effects、Opening reward、chapter activation、opening flag 或 new-game event。

Quest／ContentEvent／Chapter 的 definitions 在 Load 后可解析；Quest runtime、Event fired／active context、Chapter active／beat、Story Flags、ContentCounters／Daily 等 narrative runtime state 仍未进入磁盘 Snapshot。schema 保持 6。

## 真实验收对象

仓库没有与正式内容隔离的 EVENT-01 acceptance event 文件，因此不写入 BaseGame 剧情 JSON。用 EventEditor 在测试包副本中按下表配置：

| 角色／对象 | 实际 ID | 依据 |
|---|---|---|
| 荒村杂役主管 | `base:character_ch01_ref_supervisor` | opening scenario 与 continuous Surface opening anchor 均包含 |
| 荒村议政厅 ControlCore | `base:workarea_supervisor_mansion` | `base:loc_ref_road_hub` 的固定 ControlCore WorkArea；Host 稳定身份使用 WorkAreaId |
| 对应 Surface placement（只供定位核对） | `base:site_huangcun:block_supervisor_mansion` | 不填写到 `worldObjectId` |

### NPC 三条事件

| 字段 | 高优先级事件 | 低优先级话题一 | 低优先级话题二 |
|---|---|---|---|
| id | `base:event_accept_event01_supervisor_100` | `base:event_accept_event01_supervisor_50_a` | `base:event_accept_event01_supervisor_50_b` |
| trigger／npcDefinitionId | `onTalk`／`base:character_ch01_ref_supervisor` | 同左 | 同左 |
| priority／topicText | `100`／`先处理急事` | `50`／`询问荒村近况` | `50`／`询问议政厅` |
| once／onceScope | `true`／`global` | `true`／`perTarget` | `true`／`perActorTarget` |

高优先级事件：单一步骤 `urgent`，`@target` 正文“先把眼前这件急事处理完。”，无选项，Step outcome 为 `setFlag`／`accept:event01_high_done`，结束。

话题一按以下步骤制作：

1. `ask`：`@target`，“你想问荒村的什么事？”；三个 Choice。
2. `history`：`@actor`，“我想听听荒村近来的变化。”；Next=`reply`。
3. `reply`：`@target`，“路与人都在变，账册却要一笔笔记清。”；无 Next，结束。

`ask` Choices：

| id | text | Conditions | unavailableMode／RequirementText | Next |
|---|---|---|---|---|
| `ask_history` | `询问近况` | 空 | disabled | `history` |
| `party_qi` | `请教炼气之事` | `realmAtLeast / QiRefining` | disabled／`队伍中须有一名存活成员达到炼气境` | 空，结束 |
| `hidden_foundation` | `追问筑基秘闻` | `realmAtLeast / Foundation` | hidden | 空，结束 |

话题二可用单一步骤：`@target`，“议政厅管的是荒村眼下能管住的事。”；一个无条件 Choice `leave`，结束。

### WorldObject 两条事件

两条都配置 `trigger=onInspect`、`worldObjectKind=controlCore`、`worldObjectId=base:workarea_supervisor_mansion`、`priority=75`，TopicText 分别为“查看门前刻痕”和“查看议事告示”。

- `base:event_accept_event01_core_marks`：`once=true`、`onceScope=perTarget`。入口 `inspect` 为旁白“石阶边缘留着反复搬运重物的磨痕。”；Choice `note` 文本“记下磨痕”，outcome=`setFlag / accept:event01_core_marks_seen`，结束。
- `base:event_accept_event01_core_notice`：`once=false`。入口 `inspect` 可用 `@target`，“议政厅外贴着新换的议事告示。”；Choice `leave`，结束。

## CASE A～E 人工验收路线

### CASE A — NPC Priority／Topic／Steps

1. 新开局进入荒村，以实际 ActiveControlledCharacter 右键 `base:character_ch01_ref_supervisor`，点“对话”；角色应先走近，到达后只直接播放 Priority 100。
2. 正常结束，世界恢复；再次交谈应出现两个 Priority 50 Topic，顺序按事件 ID 稳定。
3. 选“询问荒村近况”：验证 `@target → Choice → @actor → @target → End`。正式 Event 中 Esc 不应硬退出；结束后回世界，不自动播放另一话题。
4. 再谈仍可看到尚未消费的话题二；已消费话题按各自 once scope 隐藏。

### CASE B — Party-aware

1. 使用正常存档／现有角色成长手段，让非当前发起者的一名 living party member 达到 QiRefining，而当前 Actor 未达到；话题一的 `party_qi` 应可选。
2. 换到全队均未达 QiRefining 的新档或验收副本；该项应灰显并显示 RequirementText。`hidden_foundation` 在全队无人筑基时不出现。
3. 若给两名队友分别制造只能各满足一半的两条件 Choice，该 Choice 必须仍失败；runtime 定向测试覆盖“不跨队友拼条件”。

### CASE C — WorldObject Inspect

1. 在离议政厅较远处右键荒村议政厅；原“查看详情”仍存在，并新增“调查”。
2. 点“调查”后不得立刻暂停或弹窗；角色应走到建筑接近点。到达后对象仍存在且身份仍为 `controlCore:base:workarea_supervisor_mansion` 时，出现两个同 Priority Topic。
3. 选择刻痕事件，读旁白、点 Choice，确认 flag outcome 后结束；阅读期间世界暂停，结束恢复。再次打开菜单，查看详情及原攻击条件行为仍可用。

### CASE D — Once Scope

1. Priority 100 验证 Global Once：完成后同 session 不再出现。
2. `core_marks` 验证 PerTarget，或 NPC 话题二验证 PerActorTarget；切换 Actor／目标时只按配置的稳定 TargetKey 组合放行。
3. 定向纯 C# 用例同时覆盖 NPC 原 EntityId key 与对象 `<kind>:<stable-id>` key。

### CASE E — Load definitions-only

1. 新开局确认主管可对话、议政厅可调查；保存后 Load。
2. Load 后再次打开同 NPC／对象菜单，Resolver 应仍识别相同 definitions。
3. 本轮不以 fired、Quest progress、active dialogue、Chapter progress 或 Story Flag 跨存档为验收项；它们可能重置，这是已声明边界，不得记录为 persistence 已完成。

## 验证与交付状态

已执行：`tools/offline-compile.ps1` 全程序集 `ALL_OK`；Shared 与 EventEditor build 0 warning／0 error；现有轻量 `ContentEventOnTalkTests` 定向运行 9/9；BaseGame `ContentPackageTests` 9/9；`git diff --check` 通过。不启动 Unity／PlayMode／batchmode，不新增大型测试框架。SPACE-01-P1 的 `strategic.separateSpace` JSON 读写保留，`WorldSnapshot.CurrentSchemaVersion == 6`。所有修改保持未 stage／未 commit／未 push。
