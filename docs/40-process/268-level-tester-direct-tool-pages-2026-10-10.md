# 268 · LevelTester 独立小功能页重组（2026-10-10）

> 状态：**Implementation Complete / Producer Acceptance Pending**  
> 范围：LevelTester 开发工具信息结构；不修改 Gameplay 业务规则  
> 取代：2026-10-10 早先的“五大页签聚合”方案

> 2026-10-13 精简：删除 `civilian.prepare-food`、`civilian.prepare-bed`、`civilian.prepare-emergency-sleep`、`civilian.prepare-no-food`、`civilian.prepare-rescue`、`civilian.prepare-prison`、`civilian.prepare-construction`、`civilian.restore-supervisor` 及其专用 handler；当前目录为 66 个 ToolId。以下迁移表只列仍存在的入口。

## 1. 最终结构

`HostLevelTesterCheatPanel` 保持一个开发工具窗口。顶部固定显示搜索、唯一选中人物、ActiveCharacter、当前 Site、场景、Content 包、Snapshot schema 和最近操作结果。左侧是九个稳定 `GroupKey` 的二级折叠菜单，右侧只绘制当前 `ToolId` 对应的一个功能页。

默认展开当前 `ToolId` 所属组，其余组收起；各组可独立展开，收起当前组不改变右侧选择。搜索直接过滤真实 `LevelTesterToolCatalog.Entry`，并临时显示命中组与命中功能；点击结果只改变稳定 `ToolId`，不执行工具。搜索不修改用户折叠集合，清空后恢复搜索前状态。目录高度按本帧实际可见组和子项计算。不存在“当前验收／世界与内容”等聚合页，也没有“旧版全部工具”入口。

每个功能页包含用途、实际目标或前置、该功能的参数／预设、执行按钮及最近结果。危险准备和 Reset 保留二次确认；切换 `ToolId` 立即取消尚未完成的确认。右侧滚动位置按 `ToolId` 保存，左侧目录使用独立滚动状态。

## 2. 旧入口到新功能的完整迁移表

| 旧文件／旧区域／按钮 | 新功能 | 稳定 ToolId | 执行入口 | 对象与影响范围 |
|---|---|---|---|---|
| Content/Civilian：饱食 80、10 | 设置饱食 | `civilian.satiety` | `DrawCivilianNeedTool` | 唯一选中的既有 MortalCivilianState |
| Content/Civilian：精力 80、30、5 | 设置精力 | `civilian.energy` | `DrawCivilianNeedTool` | 唯一选中的既有 MortalCivilianState |
| Content/Civilian：Detained 忠诚 10 | 设置囚犯忠诚 | `civilian.detained-loyalty` | `DrawDetainedLoyaltyTool` | 唯一选中的 Detained 凡人；权限未扩大 |
| Battle：选中角色进入弥留 | 让选中人物进入弥留 | `combat.incapacitate-selected` | `DrawSelectedIncapacitateTool` | 唯一选中 Character |
| Battle：我方全员攻击 +10 | 增加我方全员攻击 | `combat.party-attack` | `DrawPartyAttackTool` | `PlayerParty.Members` |
| Battle：最大生命 +50 并回满 | 增加我方全员最大生命 | `combat.party-max-hp` | `DrawPartyMaxHpTool` | `PlayerParty.Members` |
| Battle：生命／灵力回满 | 恢复我方生命与灵力 | `combat.party-refill` | `DrawPartyRefillTool` | `PlayerParty.Members` |
| Content/Civilian：公共库存加入粗粮 | 添加公库粗粮 | `civilian.grain-add` | `DrawGrainTool` | 荒村 Site PublicStock |
| Content/Civilian：公共库存清空粗粮 | 清空公库粗粮 | `civilian.grain-clear` | `DrawGrainTool` | 荒村 Site PublicStock |
| Content/Civilian：全局 Reset | 重置凡人验收场景 | `civilian.reset` | `ExecuteCivilianReset` | CIVILIAN 全局验收前置；二次确认 |
| Content/Civilian：定位荒村 | 定位荒村 | `focus.huangcun` | `DrawFocusHuangcun` | CameraRig；不传送、不改状态 |
| Content/Civilian：定位粮田 | 定位粮田 | `focus.grain-field` | `FocusWorkArea` | `base:workarea_labor_yard` |
| Content/Civilian：定位药田 | 定位药田 | `focus.herb-field` | `FocusWorkArea` | `base:workarea_herb_field` |
| Content/Civilian：定位伐木区 | 定位伐木区 | `focus.woodcut` | `FocusWorkArea` | `base:workarea_forest_woodcut` |
| Content/Civilian：定位医疗点 | 定位医疗点 | `focus.medical` | `FocusWorkArea` | `base:workarea_huangcun_medical` |
| Content/Civilian：定位囚室 | 定位囚室 | `focus.prison` | `FocusWorkArea` | `base:workarea_huangcun_prison` |
| Content/Civilian：定位所选伤员 | 定位选中伤员 | `focus.injured` | `FocusInjured` | 唯一选中伤员 EntityId |
| Content/Shop：定位商店 | 定位验收商店 | `focus.shop` | `DrawFixedFocusTool` | 青石坊市指定商店 |
| Content/Auction：定位拍卖行 | 定位验收拍卖行 | `focus.auction` | `DrawFixedFocusTool` | 青石坊市指定拍卖行 |
| Content/Shop：三个玩家钱包预设 | 设置玩家钱包 | `commerce.player-wallet` | `DrawPlayerWalletTool` | PlayerParty 共用钱包 |
| Content/Shop：商店标准／不足资金 | 设置商店资金 | `commerce.shop-wallet` | `DrawShopWalletTool` | `base:shop_qingshi_general` 钱包；库存不变 |
| Content/Shop：Reset Shop | 重置验收商店 | `commerce.shop-reset` | `Commerce.ResetShopForDebug` | 指定商店库存与资金 |
| Content/Auction：发放寄拍物品 | 发放寄拍验收物品 | `commerce.auction-items` | `DrawAuctionItemsTool` | PartyInventory；受容量约束 |
| Content/Auction：Auction 标准钱包 | 设置拍卖验收钱包 | `commerce.auction-wallet` | `DrawActionTool` | PlayerParty 共用钱包 |
| Content/Auction：Reset Auction | 重置验收拍卖行 | `commerce.auction-reset` | `AuctionHouseService.ResetForDebug` | 指定 AuctionHouse |
| Content/Social：秘境任务 A | 接取秘境验收任务 A | `quest.secret-a` | `DrawQuestTool` | 当前 ActiveCharacter |
| Content/Social：秘境任务 B | 接取秘境验收任务 B | `quest.secret-b` | `DrawQuestTool` | 当前 ActiveCharacter |
| Content/Social：普通对照任务 | 接取普通对照任务 | `quest.general` | `DrawQuestTool` | 当前 ActiveCharacter |
| Content/Social：19／20／反向 30 | 设置社交好感及方向对照 | `social.relation` | `DrawRelationshipTool` | 唯一选中 NPC 与 Active；方向显式 |
| Content：Flag 设置／清除 | 设置或清除剧情 Flag | `content.flag` | `DrawFlagTool` | 唯一选中 Entity；无选中时全世界 |
| Content：强制事件 | 强制呈现事件 | `content.force-event` | `DrawForceEventTool` | 同上 |
| Content：双同模板行商 | 生成两名同模板行商 | `content.merchants` | `SpawnMerchants` | WorldOpportunity runtime |
| Content/Dynamic：Hidden 石碑 | 生成隐藏石碑 | `content.hidden-stele` | `SpawnDynamic` | WorldOpportunity runtime |
| Content/Dynamic：PublicNotice 包裹 | 生成通知包裹 | `content.notice-package` | `SpawnDynamic` | WorldOpportunity runtime |
| Content/Dynamic：WorldVisible 灵草 | 生成可见灵草 | `content.visible-herb` | `SpawnDynamic` | WorldOpportunity runtime |
| Content/Dynamic：清理三类物体 | 清理动态发现验收物体 | `content.clear-dynamic` | `ClearDynamic` | 仅三类 acceptance instances |
| Background：前往世界地点 | 后台人物前往据点 | `world.background-travel` | `LevelTesterCheatBackgroundSection.DrawTool` | 显式人物＋Site |
| Background：取消 | 取消后台人物旅行 | `world.background-cancel` | 同上 | 显式人物 |
| Battle：所选 NPC 小队移动 | 所选 NPC 小队移动 | `world.npc-squad-move` | `DrawNpcSquadMoveTool` | 唯一选中 NPC 所属 Squad |
| Diplomacy：宣战 | 宣战 | `diplomacy.war` | `LevelTesterCheatDiplomacySection.DrawTool` | 势力 A/B |
| Diplomacy：结盟 | 结盟 | `diplomacy.alliance` | 同上 | 势力 A/B |
| Diplomacy：建立附庸 | 建立附庸 | `diplomacy.vassal` | 同上 | 宗主／附庸 |
| Battle：准备 A/B 继承候选 | 准备继承候选 | `succession.prepare` | `DrawSuccessionPrepareTool` | SUCCESSION fixture |
| Battle：当前 Party 正式全灭 | 使当前 Party 全灭 | `succession.kill-party` | `DrawSuccessionKillTool` | 当前 PlayerParty |
| Battle：当前 Party 全员弥留 | 使当前 Party 全员弥留 | `control.incapacitate-party` | `DrawControlHandoffTool` | 当前 PlayerParty |
| Battle：关系合格则介入 | 遭遇候选介入 | `encounter.intervene` | `DrawEncounterInterventionTool` | 当前 CharacterEncounter 候选 |
| Time：1x／2x／5x／20x | 调整运行倍速 | `time.speed` | `LevelTesterCheatTimeSection.DrawTool` | Host 正常帧循环 |
| Time：步进／批量 Tick | Core Tick 步进与批量推进 | `time.core-ticks` | 同上 | Core 数据；上限 500 Tick |
| Time：次日／第 3／第 7 日 | 推进至日界线 | `time.midnight` | 同上 | Core 数据 |
| Snapshot：Save | 保存测试存档 | `session.save` | `DrawSnapshotSaveTool` | LevelTester Snapshot slot |
| Snapshot：Load | 读取测试存档 | `session.load` | `DrawSnapshotLoadTool` | 整个 session；恢复后清旧上下文 |
| Snapshot：Reset Session | 重置整个会话 | `session.reset` | `DrawSessionResetTool` | 整个 session；二次确认 |
| Content/Civilian 综合技术诊断：移动部分 | 人物移动诊断 | `diagnostic.movement` | `DrawMovementDiagnostic` | 唯一选中人物，只读 |
| Content/Civilian 综合技术诊断：生命／势力部分 | 人物生命与势力诊断 | `diagnostic.character` | `DrawCharacterDiagnostic` | 唯一选中人物，只读 |
| Content/Civilian 综合技术诊断：Needs／Food／Sleep／Flee 部分 | 民生食宿诊断 | `diagnostic.needs` | `DrawNeedsDiagnostic` | 唯一选中凡人，只读 |
| Content/Civilian 综合技术诊断：Rescue 部分 | 救援搬运诊断 | `diagnostic.rescue` | `DrawRescueDiagnostic` | 活动救援，只读 |
| Content/Civilian 综合技术诊断：Prison 部分 | 囚犯押送与关押诊断 | `diagnostic.prison` | `DrawPrisonDiagnostic` | CapturedEscorted／Detained，只读 |
| Content/Civilian 综合技术诊断：建设工单部分 | 凡人建设诊断 | `diagnostic.construction` | `DrawConstructionDiagnostic` | CivilianConstructionJobs，只读 |
| Content/Civilian 综合技术诊断：Site Encounter 部分 | 据点遭遇诊断 | `diagnostic.encounter` | `DrawEncounterDiagnostic` | 当前 CharacterEncounter，只读 |
| Content/Shop runtime 摘要 | 商店运行态 | `diagnostic.shop` | `DrawShopDiagnostic` | 指定 Shop 钱包／库存，只读 |
| Content/Auction runtime 摘要 | 拍卖运行态 | `diagnostic.auction` | `DrawAuctionDiagnostic` | 指定 AuctionHouse listings／claims，只读 |
| Content/Social Quest runtime 摘要 | 任务与临时同行诊断 | `diagnostic.quest` | `DrawQuestDiagnostic` | 任务实例／同行绑定，只读 |
| Diplomacy：势力／战争只读摘要 | 外交状态诊断 | `diagnostic.diplomacy` | `DrawDiplomacyDiagnostic` | 战争／联盟／附庸，只读 |
| Content：Scheduled Content Events | 延迟事件队列 | `diagnostic.delayed-events` | `DrawDelayedEventDiagnostic` | ScheduledContentEvents，只读 |
| Content：刷新 Dump | 内容状态 | `diagnostic.content` | `DrawContentDiagnostic` | 当前明确目标，只读 |
| Diagnostics：系统诊断 | 性能与系统诊断 | `diagnostic.system` | `DrawDiagnosticsTab` | 当前 session/Host，只读 |

## 3. 生命周期与输入约束

- `ToolId` 是选择身份；搜索过滤不改变身份，也不使用列表下标。
- 左右两个 `ScrollView` 各有状态。右侧滚动由 `Dictionary<ToolId, Vector2>` 保存。
- 浏览目录、切换功能、输入搜索和展开页面不会调用业务 service。只有当前页明确按钮会写状态。
- 切换工具会取消 Content 危险确认和 Session Reset 确认。执行时各原 handler 重新读取当前 World、Entity、Site 和权限。
- World／Scene 变化继续调用 `ClearSessionContext`，清除旧实体引用、结果、Dump、确认及各 section runtime cache；保留当前 `ToolId` 和界面偏好，不自动执行。
- `HostUiHitTest` persistent overlay、`HostInputGate` 文本焦点保护、CameraRig／WorldMap／Selection／世界命令门禁和正式 Modal 优先级保持不变。两个滚动区均在窗口持久遮挡矩形内，滚至边界也不会把滚轮传给世界。
- 关闭按钮先于拖动处理；标题拖动矩形排除关闭按钮及 8px 间距，`DragWindow` 位于其余窗口控件之后。`Hide` 当帧退休窗口矩形，并取消 Reset／Content 待确认；`GUI.Window` 返回后若已隐藏，立即停止重新登记、焦点读取和输入 owner 获取。
- 外部 `Hide` 只设置延迟清理标记，不在非法 GUI 上下文调用 GUI API。下一次合法 GUI 回调只清除名称属于 `LevelTester.*` 的焦点，并仅在 hotControl 仍等于本窗口记录值时释放本窗口拖动捕获。

现有 UI 的逐窗关闭核查和本轮最小修复见 [269](269-ui-foldout-and-close-audit-2026-10-11.md)。

## 4. 实现边界

删除主窗口对 `CheatTab`、手写 `SearchCatalog`、验收卡片和 `DrawActiveTab` 聚合调用的依赖。`LevelTesterCheatContentSection`、`LevelTesterCheatBackgroundSection`、`LevelTesterCheatDiplomacySection`、`LevelTesterCheatTimeSection` 改为按明确 `ToolId` 绘制；旧的全量 `Draw()` 方法已移除。保留入口继续复用原有 service；2026-10-13 删除的 8 项不保留隐藏分派或专用准备方法。没有引入反射发现、命令总线、插件系统、第三方 UI 或新 Debug 框架。
