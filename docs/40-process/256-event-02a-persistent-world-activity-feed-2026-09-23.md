# EVENT-02A — Persistent World Activity Feed

> 状态：**Implementation Complete / Producer Acceptance Pending**  
> 日期：2026-09-23  
> 范围：仅接入 WorldOpportunity `publicNotice`；不是 Quest、ContentEvent 弹窗或通用通知中心。

## 1. Runtime 与生命周期

`SimulationWorld.WorldActivities` 持有稳定 `activity:<sequence>`、SourceKind/SourceId、标题、正文、创建日、Active/History、read state 与可选结束日。Opportunity publicNotice 成功生成后，以稳定 Opportunity InstanceId 作为 SourceId 创建 unread Activity；同一时刻仍发布一次非阻塞 Toast。worldVisible 不创建 Activity、Toast 或 History。

Opportunity 到期，或 NPC death/Removed 导致实例清理时，关联 Activity 自动从 Active 转入 History并记录结束日。History 保留原标题、正文和创建日，最多 100 条，超出时删除最旧记录。玩家只能 Mark Read，不能删除仍有效 Activity。

## 2. Snapshot 与恢复

`WorldSnapshot.CurrentSchemaVersion` 保持 6，新增 optional `worldActivityRuntime` authority，保存 next sequence 与全部 Active/History entries。旧 v6 缺字段视为空。新 authority 严格验证稳定 ID、唯一 source、状态/结束日、100 条上限；Active worldOpportunity Activity 必须指向同一 Snapshot 已恢复的 active Opportunity Instance，否则 `SnapshotInvalid`。

Restore 直接写 board，不发布 `WorldOpportunityNotice`，所以 Load 后不会重复 Toast。规则定义继续由 Content rehydrate；位置公开资格来自 Opportunity spec。

## 3. Host UI 与定位

`HostWorldActivityPanel` 位于左侧，Active 默认只显示 unread 标记与标题。点击后 Mark Read 并打开轻量详情，显示正文、出现日、状态和预计剩余天数；历史入口按结束时间倒序展示，可再次打开原详情。

仅 `publicNoticeRevealExactLocation=true` 且 source instance、entity、exact WorldPresence 均有效时显示“定位”。定位先核对 PlayerParty 与目标 Surface；同 Surface 使用现有 Continuous mapper 与 `PlayableHostCameraRig.FrameWorldPoint`，只移动镜头。其它 Surface 仅提示，不切 Surface、不移动玩家、不导航、不暂停。

## 4. Acceptance Content

- 受伤散修：worldVisible，距离 2～3 world units，不进 Activity。
- 临时行商：publicNotice，标题“临时行商”，正文“听说荒村附近来了一名临时行商。”，公开准确位置，距离 2～3 world units。

## 5. 制作人人工验收

1. New Game：左侧出现 unread“临时行商”并短暂 Toast；受伤散修不入栏。
2. 点击临时行商：显示正文、出现日、有效状态与剩余时间；unread 强调消失。
3. 点击定位：镜头对准行商，玩家角色不移动、不产生路线、不暂停。
4. Save → Load：Activity、read state 与同一 NPC 定位仍在，不重新 Toast。
5. 推进一天：旧行商到期后从 Active 消失并进入 History；Director 新生成的行商拥有新 Activity 与新 Toast。
6. 受伤散修始终只通过世界实际发现，Activity/History 不自动记录。

## 6. 验证边界

仓库规则禁止代理新增或运行自动测试，因此任务书定向 tests 未执行。交付只进行全程序集 offline compile、BaseGame Content validation、OpportunityEditor/Build All、静态引用检查与 `git diff --check`；Unity 运行行为等待制作人人工验收。未实现 hidden、动态 WorldObject、通用通知中心、minimap marker、waypoint、auto travel、teleport 或自动跨 Surface。
