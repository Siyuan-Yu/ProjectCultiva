# SOCIAL-QUEST-01 — Secret-Realm Quest Social Topic + Temporary Quest Companion

> 状态：Producer Accepted / Sealed | 日期：2026-09-25

## 最终范围与产品语义

Temporary quest companion participates in party travel and combat, but is not a player-controllable character.
临时同行跟随受控队伍、随队进入 Separate Space、通过 NPC AI 参战并占用容量；可以选中查看，不能成为 ActiveCharacter、不能接受玩家手动战斗命令或普通 Stop Follow。可控性从永久 Character roster / 既有玩家势力管理 authority 派生，首先排除 QuestCompanion binding；UI、手动命令后端、自动 Active 候选和恢复共用判定。成员、空间和自动战斗 authority 保留。

仅 Active + secretRealm QuestInstance 派生 Priority0「关于【任务名】」。QuestDefinition/QuestSpec 的 questKind=general|secretRealm，缺省 general，未知值拒绝。最高 authored Priority>0 覆盖，0同列；复用现有 Dialogue，不新增 Event VM/UI。Topic 始终绑定真实 QuestInstanceId。

邀请校验真实实体、Alive/actionable、非敌对、战斗锁、同空间、未同行/未绑定、容量6，以及 RelationshipLedger 的 target NPC→actual actor Score>=独立 SecretRealmQuestInviteMinScore=20。拒绝/接受均中文回应。Dynamic Opportunity NPC 当前拒绝，不延长其生命周期。

## Authority、离队与 Save/Load

QuestCompanionBoard 独立保存 CompanionEntityId + QuestInstanceId + OriginalSquadId + Active/PendingDeparture。加入现有受控 Squad，保留原队剩余成员；不改 FactionMembership、EntityTag、永久 recruit/CharacterIds。

ReadyToClaim / Completed / Failed / Active→Inactive(Abandoned) 均锁定 PendingDeparture；重新接取不解锁旧绑定。普通 Surface、无战斗/独立空间/Dialogue/transition/modal且有合法永久 Active 时，经私有 TryDepartQuestCompanion 离队。公开 TryStopFollow 对临时绑定始终拒绝。二者共用原精确落点保留/工作清理；原 Squad 合法、可行动 leader、容量可用、无锁、同 Surface 同落点才恢复，否则 singleton，不 teleport。死亡/Removed 清绑定，失能保留；既有 handoff 不重写。

Snapshot 当前 v11。ContentProgress 持久保存绑定与 PendingDeparture；恢复校验实体、实例、状态、受控 Squad及 secretRealm定义，不重复 AddMember，不替换同模板 NPC。可控性派生，不增加冗余 bool。对话禁止保存，旧v1～v10严格拒绝。

## Producer Acceptance

制作人明确验收通过：秘境/普通话题区分、关系不足拒绝/达标接受、自动跟随、进入废弃洞府/Separate Space、一起战斗、Save/Load、所有任务结束状态 PendingDeparture/安全离队、FactionMembership不变、QuestInstance/NPC实例不串。

最终 P1 已通过：临时同行不可切 Active、不暴露玩家手动战斗控制、不显示/不允许普通停止跟随，同时自动同行/参战/任务安全离队保持正常。

## Acceptance Content / Authoring

base:quest_social01_cave（A，期限7天）、base:quest_social01_cave_b（B，9天）、base:quest_social01_general（普通对照）；manual且可放弃，探索既有 base:loc_cave_chamber 完成。固定 NPC 阿石为可重复验收对象；将老/主管的高优先 authored 话题正常覆盖 social。
LevelTester Content：接取A/B/普通、NPC→Active 19/20、反向30及只读实例/绑定信息。QuestEditor仅分类下拉，无新编辑器或图节点。

## 最终静态验证

- 精确暂存版本 Unity 单独编译通过（LevelTester 排除前轮诊断段）；其余生产 C# 与工作树一致。
- Core/Data/Unity/Unity.Editor：322/80/140/6 sources，ALL_OK；未启动 Unity/PlayMode/Runner，未新增或运行行为测试。
- BaseGame真实ContentPackageLoader通过，三份任务分类/IsSecretRealm正确；unknown questKind明确拒绝；Data→Core正式单点投影完整。
- JsonSnapshotSerializer纯DTO编码往返一致：schema11、uint64 EntityId=9007199254740993、QuestInstanceId、OriginalSquadId、PendingDeparture保持。非世界行为回放。
- Active手动/自动/恢复入口均使用统一helper；Selected不等于Controllable；手动战斗与StopFollow有后端guard，自动follow/combat/生命周期离队有独立内部入口。
- Quest四种结束路径、绑定clone/restore、Outcome rollback、travel/space/combat成员引用已静态核对。P1未改Snapshot shape或验收Content；共用离队位置/清理代码逐段一致。
- 新Core .cs/meta成对且GUID唯一；git diff --check通过。无Knowledge/Trading/Equipment/Crafting/Production/Logistics/NPC AI或实验功能。

## 提交范围与既有依赖

本轮从尚未提交的DELAYED v10运行基线继续到SOCIAL v11。为保留已验收的v11 wire/事务/对话上下文并保证提交可编译，本次纳入调度board、原上下文、snapshot、必要dispatcher及共用滚动面板运行依赖；不把DELAYED独立里程碑标为封板。
DELAYED专属验收JSON、EventEditor字段/表单/验证增量、process263与ADR0040不暂存。共享文档仅暂存本次SOCIAL最终内容；前轮其它文档修改保留。未更改当前分支、不rebase、不force push。

## Roadmap

SOCIAL-QUEST-01 → Producer Accepted / Sealed。
下一阶段：Full Trading（不自动开始）→ Equipment / Crafting → Production / Logistics → NPC AI / Strategic Autonomy last。
Knowledge / Rumor / Information Propagation：Future / Only if gameplay later requires it。

## 最终修改文件清单

- `Assets/Scripts/Core/Content/ContentEventBoard.cs`
- `Assets/Scripts/Core/Content/ContentEventService.cs`
- `Assets/Scripts/Core/Content/ContentEventSpec.cs`
- `Assets/Scripts/Core/Content/ContentOutcome.cs`
- `Assets/Scripts/Core/Content/ContentOutcomeApplier.cs`
- `Assets/Scripts/Core/Content/QuestCompanionService.cs`
- `Assets/Scripts/Core/Content/QuestCompanionService.cs.meta`
- `Assets/Scripts/Core/Content/QuestService.cs`
- `Assets/Scripts/Core/Content/QuestSpec.cs`
- `Assets/Scripts/Core/Content/ScheduledContentEventBoard.cs`
- `Assets/Scripts/Core/Content/ScheduledContentEventBoard.cs.meta`
- `Assets/Scripts/Core/Input/PlayerInputPort.cs`
- `Assets/Scripts/Core/Persistence/ContentProgressSnapshotHelper.cs`
- `Assets/Scripts/Core/Persistence/SnapshotService.cs`
- `Assets/Scripts/Core/Persistence/WorldSnapshot.cs`
- `Assets/Scripts/Core/Simulation/SimulationWorld.cs`
- `Assets/Scripts/Core/World/PlayerPartyRuntime.cs`
- `Assets/Scripts/Data/Bootstrap/ContentRuntimeBootstrap.cs`
- `Assets/Scripts/Data/Content/ContentPackageLoader.cs`
- `Assets/Scripts/Data/Content/ContentReferenceValidator.cs`
- `Assets/Scripts/Data/Content/DefinitionSchema.cs`
- `Assets/Scripts/Data/Content/QuestDefinition.cs`
- `Assets/Scripts/Data/Serialization/JsonSnapshotSerializer.cs`
- `Assets/Scripts/Unity/Host/ContinuousOutdoorSurfaceRuntime.cs`
- `Assets/Scripts/Unity/Host/HostActionMenu.cs`
- `Assets/Scripts/Unity/Host/HostCharacterEncounter.cs`
- `Assets/Scripts/Unity/Host/HostCommandBridge.cs`
- `Assets/Scripts/Unity/Host/HostContentInterruptPresenter.cs`
- `Assets/Scripts/Unity/Host/HostDialogueController.cs`
- `Assets/Scripts/Unity/Host/HostDialogueModel.cs`
- `Assets/Scripts/Unity/Host/HostFormalHud.cs`
- `Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs`
- `Assets/Scripts/Unity/Host/HostMoveController.cs`
- `Assets/Scripts/Unity/Host/HostNpcMeleeAssault.cs`
- `Assets/Scripts/Unity/Host/HostPlayerPartyController.cs`
- `Assets/Scripts/Unity/Host/HostSelectionController.cs`
- `Assets/Scripts/Unity/Host/LevelTesterCheatContentSection.cs`
- `Assets/Scripts/Unity/Host/PlayableHostSession.cs`
- `Content/BaseGame/Data/Quests/social_quest01_acceptance.json`
- `Content/BaseGame/Data/SCHEMA.md`
- `ExternalTools/ContentAuthoring/QuestEditor/MainWindow.xaml`
- `ExternalTools/ContentAuthoring/QuestEditor/MainWindow.xaml.cs`
- `ExternalTools/ContentAuthoring/Shared/PackageValidator.cs`
- `ExternalTools/ContentAuthoring/Shared/SchemaFields.cs`
- `docs/00-project/00-overview.md`
- `docs/00-project/03-glossary.md`
- `docs/20-systems/2E-events-and-world-state.md`
- `docs/20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md`
- `docs/20-systems/2M-character-social-relations-v1.md`
- `docs/30-tech/36-content-package-and-mod-architecture.md`
- `docs/40-process/247-project-handoff-current-state-2026-09-18.md`
- `docs/40-process/264-social-quest-01-secret-realm-social-topic-and-temporary-companion-2026-09-25.md`
- `docs/40-process/41-roadmap.md`
- `docs/40-process/42-devlog.md`
- `docs/40-process/43-decisions/ADR-0041-secret-realm-quest-companion-and-snapshot-v11.md`
- `docs/40-process/43-decisions/README.md`
