# QUEST-INSTANCE-01 — 动态人物委托与真实互动上下文 V1

> 状态：**Implementation Complete / Producer Acceptance Pending**  
> 日期：2026-09-24  
> 范围：Quest Template／Instance、互动接取与交付、真实角色关系、生命周期、Journal/HUD、Snapshot v8、QuestEditor／EventEditor、确定验收入口

## 1. 最终模型

- `QuestSpec`／Quest JSON 是模板。`runtimeMode=fixed` 保留原单份任务语义；`runtimeMode=characterCommission` 可由不同真实人物生成多份 `QuestRuntime`。
- 固定任务的运行键仍为 Quest DefinitionId。人物委托由 `QuestBoard.NextInstanceSequence` 分配 `quest-instance:<sequence>`，模板 ID 不能当作实例唯一身份。
- 人物委托实例保存发布者 EntityId、来源 Opportunity InstanceId、接取时发布者名称、接取角色、状态、进度、接取日、原始期限、交付事实和失败原因。
- 同一发布者＋同一模板最多保留一份实例。Inactive 放弃实例可在原期限内重接；Completed／Failed 不重开。玩家任务属于共享队伍，不按队员复制。

## 2. 接取、交付与人物上下文

- `acceptanceMode=interaction` 的人物委托只能由 `acceptQuestFromTarget` 接取；缺少 Actor／Target、目标已消失或模板类型不符都会失败。任务日志不会展示未接取模板，`TryStart(templateId)` 不能绕过真实发布者。
- Event 条件支持当前 Target 的可接、Active、已交付、ReadyToClaim、Completed、Failed，以及按模板需求判断物品是否可交付。
- `deliveryRequirements[]` 是交付需求唯一真源。通用 `deliverQuestToTarget` 验证发布者、状态、期限和库存，扣除全部需求后记录交付并转为 ReadyToClaim；空完成条件不会自动完成。
- `@actor`、`@target`、`@issuer` 按真实 EntityId 解析。固定 DefinitionId 若对应多个运行人物会明确失败，不选择“第一个”。`affectionAtLeast` 明确 from → to；EVENT-02 成功交付配置为当前发布者对当前交谈者 Affection +1。

## 3. 库存、事务与生命周期

- resource 交付复用 `PlayerStrategicResourceService`：位于己方可访问战略库存网络时使用背包＋合格公库，离开网络时仅背包；非 resource 保持 bag-only。
- `OutcomeTransaction` 同时捕获 Quest 实例字典与下一实例序列、背包、公库、关系 Ledger、DomainEvent 和 Event Step。扣物品后任一后续结果失败会整体恢复。
- 只有未交付 Active 委托会因发布者死亡／Removed 或来源 Opportunity 到期而 Failed。Chunk 卸载、View 缺失、离开 Surface、临时受伤／战斗不构成永久消失。ReadyToClaim／Completed 不因发布者消失回退。
- 放弃只允许未交付 Active；保留原始期限。放弃后到期会转 Failed，不能用反复放弃刷新倒计时。

## 4. Snapshot v8

`contentProgress` 新增 `nextQuestInstanceSequence`，每条 Quest runtime 保存 `questInstanceId`、模板、发布者、Opportunity 来源、发布者历史名称、接取者、状态／进度／期限、交付事实与失败原因。恢复校验实例 ID、同发布者同模板唯一性、序列单调、模板存在、状态／进度／期限、Active 来源一致性。Completed／Failed／ReadyToClaim 的历史来源可已消失。

v1～v7 不包含可可信恢复的发布者实例身份，明确 `SnapshotVersionMismatch` 并要求新开局；不从旧 flag 或附近同模板 NPC 猜测。

## 5. 制作工具与游戏 UI

- QuestEditor 中文表单可选择“固定剧情任务／人物委托”“任务日志／真实人物互动”，配置交付物品、数量、期限、放弃、条件、奖励和失败结果。
- EventEditor 继续复用 Shared 条件／结果控件，可选当前对象委托状态、接受／交付当前对象委托、`@actor/@target/@issuer` 关系结果和明确方向好感条件；无需手写实例 ID 或 EntityId。
- Quest Journal／HUD 以实例键追踪、领奖、放弃；同模板两份委托分别显示发布者、接取日和实例编号。ReadyToClaim 只能领奖，不能放弃。

## 6. 确定人工验收入口

1. New Game 后打开 LevelTester 开发工具（反引号），进入“内容”。
2. 点击“QUEST-INSTANCE-01：生成两名同模板临时行商”。按钮只补足到两名，不改变正式 Director 默认密度；状态行显示两条 Opportunity InstanceId、EntityId 与精确坐标。
3. 左侧活动动态出现两条临时行商，可分别定位。两者使用相同 `base:character_event02_temporary_merchant`、相同 Opportunity／Event／Quest 模板，但是真实 Entity 不同。
4. 分别接取甲、乙，任务日志应显示两条“临时行商·代采灵药”，发布者与实例编号不同。
5. 准备两份任意来源灵药；只向甲交付，甲为 ReadyToClaim、甲对当前交谈者 Affection +1，乙仍 Active。再向乙交付，乙独立 ReadyToClaim、关系独立变化。
6. 在接取、单份交付、双份交付与领奖阶段分别 Save／Load，确认实例、发布者、期限、交付、关系与状态不串线；重复点击不能重复扣物品、加关系或领奖。
7. 任务日志分别领取，确认两份 Completed。受伤散修保留为第二模板验证，不要求随机寻找。

## 7. 本轮边界与验证

固定剧情任务、EVENT-01/02、Activity、Continuous Surface 5×5 streaming、SAVE-01 与 STRATEGIC-STOCK-01 既有能力保持兼容。未实现 `ContentIntent`、延迟反应、知识传播、势力继承、完整交易／装备／制作／物流或 NPC 战略自主行为；小游戏中途保存仍为独立待办。

本轮执行离线全程序集编译、相关轻量测试、BaseGame Content Loader／ReferenceValidator、QuestEditor／EventEditor build、正式 Build All 与 `git diff --check`；未启动 Unity。正式状态在制作人人工验收前保持 **Implementation Complete / Producer Acceptance Pending**。
