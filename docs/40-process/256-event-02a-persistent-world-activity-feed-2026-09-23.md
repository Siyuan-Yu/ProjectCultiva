# EVENT-02A — Persistent World Activity Feed

> 状态：**Producer Accepted / Sealed**
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

两项内容继续属于 `event02_acceptance.json` 的 acceptance/prototype chain；封板后作为 `worldVisible` 与 `publicNotice` 的正式 authoring/reference sample 保留，不升级为荒村正式剧情。等真实荒村内容同时覆盖静默发现、公开 Activity、定位、Save/Load 与 expiry→history 后，再单独评估整链删除。

## 5. 制作人验收结果

制作人已于 2026-09-23 实际验收 publicNotice Activity、详情、exact-location camera locate、Save/Load、restore 不重复通知，以及 expiry 后进入 History；受伤散修保持 worldVisible 且不自动进入 Activity。状态更新为 **Producer Accepted / Sealed**。

## 6. 验证边界

Seal 收口只运行指定 lightweight Continuous Surface neighborhood／startup preflight tests、全程序集 offline compile、静态引用检查与 `git diff --check`；不启动 Unity或大型测试。未实现 hidden、动态 WorldObject、通用通知中心、minimap marker、waypoint、auto travel、teleport、camera preload 或自动跨 Surface。
