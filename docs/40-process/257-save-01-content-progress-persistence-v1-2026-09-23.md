# SAVE-01 — Content Progress Persistence V1

> 日期：2026-09-23
> 状态：**Producer Accepted / Sealed（2026-09-24）**
> 范围：Snapshot v7 Content Progress authority；EVENT-02 受伤散修／临时行商 prototype Quest wiring

> **后续替代说明（2026-09-24）：** 本页保留 SAVE-01 封板时的历史实现与验收事实。当前两条 EVENT-02 动态任务已由 [QUEST-INSTANCE-01](258-quest-instance-01-dynamic-character-commissions-v1-2026-09-24.md) 改为结构化人物委托，Snapshot 当前为 v8；下文 accepted／handed-in／done flags 不再是现行 Content。

## 1. 结果

SAVE-01 将“玩家做到哪里”纳入 Snapshot，把静态 Content definitions 与运行进度分离。`WorldSnapshot.CurrentSchemaVersion` 已升为 7；`contentProgress` 必须存在且 `hasAuthority=true`。v1～v6 缺少足够事实，统一返回 `SnapshotVersionMismatch`，不把缺失字段猜成空进度。

EVENT-02 现有两条 World Opportunity → Event → Quest 链。临时行商带 publicNotice 与精确定位，作为制作人主验收入口；受伤散修链继续作为第二条示例保留。两者都由初次对话 `startQuest`，随后接受任意合法来源的灵药，并通过 Active Event 的 `stockAtLeast`、`removeStock` 与 hand-in flag 完成交付；任务进入 ReadyToClaim 后仍由 Quest Journal 领奖。

## 2. Snapshot v7 authority

`contentProgress` 保存：

- `flags[]` 与保序 `flagHistory[]`
- 全部 `quests[]`：QuestId、Status、ProgressCount／Max、AcceptedAtDayIndex、DeadlineDayIndexExclusive
- `firedEventKeys[]`：global／perTarget／perActorTarget 的现有稳定字符串
- `chapter`：ActiveChapterId、ChapterStartDayIndex、AppliedBeatKeys
- `counters[]` 与保存实际日期索引的 `dailyMarks[]`
- `laborTicks[]` 与 `laborHarvests[]`；内部 composite key 作为 opaque stable key round-trip

恢复时各 Board 做 authoritative replace。Quest 不重新 Start，Chapter 不调用会清 applied beats 的 Activate，Event restore 只恢复 fired keys 并清空 active interaction。definitions-only rehydrate 完成后，校验所有恢复 Quest、active Chapter、applied beat 与 fired Event 引用。

Active dialogue 不进入 Snapshot。`world.ContentEvents.HasActive` 时 `SnapshotService.CaptureJson` 明确拒绝，并提示“请先完成当前对话/事件后再保存。”现有 CharacterEncounter Preparing／Committed gate 保持。

实体、空间、Party／Squad、CharacterEncounter、Separate Space、Inventory、Relationship、WorldOpportunity、WorldActivity、Outdoor state 与随机流等既有 authority 保持原字段和职责，没有复制进 ContentProgress。

## 3. Playable loop 统一

`PlayableSimulationLoopFactory.Create` 是 New Game 与 Snapshot Load 的共享入口，并各注册一次：

1. `QuotaConsequenceHandler`
2. `ChapterDayHandler`
3. `QuestDeadlineDayHandler`
4. `SupervisorPressureHandler`

`SimulationLoop` 内建的 `WorldOpportunityDriver` 保持不变。Load 后 deadline failure、Chapter day beat 与 Opportunity tick 都继续生效，不在 Host rehydrate 重复追加 handler。

## 4. EVENT-02 prototype Quest

任务定义位于 `Content/BaseGame/Data/Quests/event02_opportunity_quests.json`。

临时行商主验收任务：

- ID：`base:quest_event02_temporary_merchant_herb`
- 目标：`hasFlag quest:event02_temporary_merchant_herb_handed_in`
- 期限：2 天
- 奖励：修为进度 +2，并设置 `quest:event02_temporary_merchant_herb_done`
- `autoOffer=false`，`abandonable=true`
- 初次 P50 Event 要求缺少 `story:event02_temporary_merchant_herb_accepted`；接受 Choice 在同一 transaction 设置 flag 并 `startQuest`，拒绝没有 Outcome，也不扣除玩家已经持有的灵药。Active 时由 P60 交付 Event 接管：持有 `base:resource_spirit_herb ×1` 才可选择交付，同一 transaction 先 `removeStock`，再设置 hand-in flag；Event 结束时现有 Quest Evaluate 将任务转为 ReadyToClaim。临时行商 Opportunity 生命周期为 3 天。

受伤散修第二条示例：

- ID：`base:quest_event02_wounded_cultivator_herb`
- 目标：`hasFlag quest:event02_wounded_cultivator_herb_handed_in`
- 期限：2 天
- 奖励：修为进度 +3，并设置 `quest:event02_wounded_cultivator_herb_done`
- `autoOffer=false`，`abandonable=true`

初次 Event 顶层要求缺少 `story:event02_wounded_cultivator_herb_accepted`。接受 Choice 在同一 Outcome transaction 内先设置 accepted flag，再 `startQuest`；拒绝不写 flag，之后仍可重问。Quest Active 时 P60 Event 提供灵药交付：库存不足时选项灰显并显示“需要灵药 ×1”，交付成功才设置 hand-in flag。Opportunity 生命周期为 3 天，Director 其它行为不变。

两条任务都不检查地点、采收历史、劳动记录、获得来源或采集角色。任务接取后，即使队伍背包已有灵药，单纯 Evaluate 仍保持 Active；只有匹配 Opportunity template 的 NPC 交付 Event 才会扣除 1 份灵药并满足目标。系统不保存 issuer NPC instance，也不实现 NPC 当面发奖；目标达成后仍通过现有 Quest Journal `TryClaimRewards` 领取。

通用 `removeStock` 与 `addStock` 使用相同的 `id`／`amount` 约定，`amount <= 0` 默认 1。非 resource 仍只从 PartyInventory 扣除；resource 改由 `PlayerStrategicResourceService.TryConsume` 使用当前 Player Accessible Stock。处于可访问己方战略物资网络时，该数量为 PartyInventory＋eligible WorldSitePublicStock；离开网络时只剩 PartyInventory。Outcome transaction 同时保存并恢复 PartyInventory 与 WorldSitePublicStock board，后续 Outcome 失败时，扣除的资源、事件队列和其它早期 mutation 一并回滚。EventEditor 的共享 Outcome 编辑器以“移除物品 / 交出物品”显示该类型，并提供物品与数量字段。

## 5. 验证记录

- `tools/offline-compile.ps1`：`ALL_OK`
- SAVE-01 定向 headless tests：覆盖任务五种状态、奖励不重发、Flags／History、三种 once scope、Counter／Daily／Labor、Chapter、对话存档门、读档 day handlers、v6 与损坏 v7 拒绝，以及 `removeStock` 成功／不足／事务回滚、任意来源库存交付与 hand-in ReadyToClaim round-trip
- BaseGame Content loader：PASS
- BaseGame Content reference validation：PASS，0 errors
- SAVE-01＋Quest／Event／Content 定向组合：30/30
- ContentAuthoring Shared tests：9/9；EventEditor source build：0 warnings / 0 errors
- STRATEGIC-STOCK-01＋SAVE-01／Quest／Event 定向组合：30/30；覆盖网络内外、非 resource bag-only、战略扣除、混合来源、SitePublicStock 回滚、仓库取出／满包和 Quest／Journal 一致性
- `git diff --check`：见本轮最终报告
- 未启动 Unity；未 stage／commit／push

## 6. 制作人人工验收

1. New Game，等待左侧“动态”出现“临时行商”，点击“定位”并自行走到行商处。
2. 与临时行商交谈；Choice 显示时尝试 Save，应提示先完成当前对话／事件。
3. 选择“可以，我帮你留意。”，确认 Journal 出现“临时行商·代采灵药”，状态 Active，期限 2 天；接取过程不扣除背包中已有灵药。
4. 完成对话后 Save / Load，确认 Quest 仍 Active、deadline 不变、accepted flag 存在，初次接取对白不再出现。
5. 通过任意合法现有方式让 PlayerParty 共享背包获得 `base:resource_spirit_herb ×1`。不要求药田 Harvest、LocationLabor 或来源记录。
6. 确认仅拥有灵药时 Quest 仍为 Active。再次与临时行商交谈，应看到“交给他一份灵药。”；没有灵药时该选项灰显并显示“需要灵药 ×1”。
7. 选择交付，确认背包灵药 -1、`quest:event02_temporary_merchant_herb_handed_in` 已设置、对话结束后 Quest 转为 ReadyToClaim。
8. Save / Load，确认灵药仍已扣除、hand-in flag 与 ReadyToClaim 保持，Active 交付入口不再出现，奖励尚未发放。
9. 在 Quest Journal 领取，确认 Completed、修为进度 +2、flag `quest:event02_temporary_merchant_herb_done` 已设置。
10. 再次 Save / Load，确认仍为 Completed，奖励不能重复领取。
11. 受伤散修的“受伤散修·寻药”按相同逻辑作为第二条验证链，但不再是本轮必须随机找到的主验收入口。

两条 prototype 不再以 LocationLabor 为目标，但 Snapshot v7 仍完整保存 `LocationLaborProgressBoard` 的 labor／harvest facts，供现有及未来的 `uniqueLaborAtLocation`／`uniqueHarvestAtLocation` 内容使用。

### STRATEGIC-STOCK-01 补充验收

1. 玩家在荒村己方实际控制范围，PartyInventory 灵药为 0、势力公库灵药至少为 1；临时行商“交给他一份灵药”应可选。交付后公库 -1、背包仍为 0、hand-in flag 存在，Quest 进入 ReadyToClaim。
2. 在己方控制范围打开背包，切换“势力仓库”，确认仅列出当前可访问网络的战略资源 totals。选择灵药并“取出 1”，确认公库 -1、PartyInventory +1；“取出尽量多”受背包剩余容量限制。
3. 离开己方控制范围再次打开背包，“势力仓库”入口保留但灰显并提示仅在己方实际控制范围可访问；已经取到 PartyInventory 的灵药仍存在，并可在领地外完成 NPC 交付。
4. Snapshot schema 保持 v7；公库与背包转移复用既有两类持久化 authority，不新增字段。

制作人实际完成以上验收前，本专项不得标记 Accepted / Sealed。

## 7. Producer Acceptance / Final Seal（2026-09-24）

制作人已确认当前内容验收通过。SAVE-01 的 Snapshot v7 Content Progress、EVENT-02 两条 hand-in Quest、战略资源直接交付、势力仓库取出、ReadyToClaim／Claim／Completed 与各阶段 Save/Load 已按当前范围封板。

- 状态：**Producer Accepted / Sealed**
- Snapshot schema 保持 v7；本次封板不新增 v8 字段。
- `WorldSitePublicStockBoard + WorldSiteStorageRoomBoard + PlayerStrategicResourceService + PartyInventory` 继续作为唯一库存 authority，不建立第二套 FactionInventory。
- 当前势力仓库只覆盖战略 resource 与取出操作；通用 Item／装备仓库、容量、物流、Quest issuer 与 NPC 当面领奖仍不在封板范围。
