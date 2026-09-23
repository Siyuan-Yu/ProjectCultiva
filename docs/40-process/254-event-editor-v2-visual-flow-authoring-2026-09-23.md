# EVENT-EDITOR-V2 — Visual Event / Dialogue Flow Authoring

> 日期：2026-09-23  
> 状态：**Implementation Complete / Producer Acceptance Pending**  
> 范围：EventEditor authoring UX、editor-only layout metadata、BaseGame legacy ContentEvent 等价迁移；Runtime schema/行为不重构。

## 交付结果

EventEditor 从“ID 列表＋超长表单＋Step/Choice ListBox”改为三栏制作界面：按对象/按事件 Browser、自由 Flow Graph、上下文 Inspector。Event 仍是 canonical Template；按对象只是 binding projection。Graph 只可视化现有 Step/Choice NextStepId，不产生第二套 Runtime Graph。

主要实现拆为：

- `EditorSession`：working copy、dirty、undo/redo、保存接受状态。
- `EventFlowGraph`：Step Node、连接、拖线创建节点、平移缩放、框选多选、复制粘贴、删除、自动 DAG layout 与错误高亮。
- `GraphLayoutStore`：读写 `Content/BaseGame/Authoring/EventEditor/layouts.v1.json`，Runtime 不消费。
- `NewEventDialog`：NPC 普通/特殊对话、世界物体互动、其他事件的内存草稿向导。
- `MainWindow`：三栏编排、Browser projection、上下文 Inspector、保存保护与验证定位。

## 数据与兼容

BaseGame 审计共 14 条 ContentEvent：原有 Steps 5 条，legacy 9 条。9 条仍有当前用途，全部等价迁移为单入口 Steps；本轮未删除仍被引用的 Harness/CH01/将老/药坡内容。此前确认的旧主管 talk/hurry 与 orphan herb penalty 已在上一轮退休，本轮开始时即不存在。

审计迁移完成后新增 `base:event_event_editor_v2_supervisor_chat` 作为本轮普通对话保存/回读验收内容，因此最终 BaseGame 为 15 条 ContentEvent。该事件包含主管、玩家、主管三句 Step 链，两项 Choice，其中炼气选项使用 `realmAtLeast/QiRefining + disabled + RequirementText`，末句使用合法 `setFlag` Outcome；对应 layout 位于 editor-only metadata。

迁移保留 trigger、once、binding、conditions、location/quest 与 Choice conditions/outcomes；onTalk speaker 为 `@target`，其他为旁白，Choice Next 为空。将老小游戏仍是 terminal Choice outcome。迁移后 BaseGame legacy-authoring 为 0。Runtime/Data legacy reader 保留给外部旧包。

## 制作体验

Step/Choice ID 自动生成且不随正文/坐标变化；Entry、Next 不在正常模式手填。端口拖到空白会新建节点并连接。Inspector 用自然语言显示 speaker、重复规则、条件和结果；Event technical ID 与 binding key 收到高级区。Priority 0、once=false 的 onTalk 在按对象 Browser 标为“★ 普通 / 保底对话”。

新建事件在首次保存前不进入 PackageStore 或磁盘。切换事件、切包和关窗统一处理保存/放弃/取消。保存前由 `EventAuthoringValidator` 校验，错误映射回节点；成功后安全写回 definition 与 layout。

## 非目标与冻结边界

未实现 EVENT-02 Opportunity Director、随机对象、tag/archetype runtime binding、ContentIntent、剧情 Snapshot persistence、Cutscene、概率脚本或 Runtime Graph。Snapshot schema 未改变。EVENT-01 Runtime、HostDialogue、Priority/Topic/Step/Outcome transaction 保持原 authority。

## 验证与发布

- Shared / EventEditor Release build：0 warning / 0 error。
- BaseGame Event authoring validation：15 events，0 errors，0 legacy（审计基线 14，迁移 9，新增验收内容 1）。
- Runtime Content Loader / ReferenceValidator、全仓 offline compile、diff-check 与正式 Build All：见本轮最终报告。
- 正式交付仅为 `ExternalTools/ContentAuthoring/Apps/EventEditor.exe`；`.build` 不是交付产物。

正式 `Apps/EventEditor.exe` 可启动，Windows Accessibility 树已读到 Browser、Flow Graph、Event/Step/Choice Inspector、Priority、Topic、Repeat 与 onTalk/onInspect binding 控件。本机自动化层对该 WPF 窗口持续返回 `coordinate input geometry is unavailable`，重新启动后仍无法注入点击，因此自动 UI 输入/拖线和逐项回读未记为通过；数据与 layout 由静态回读和正式 validation 覆盖，制作人手感验收仍为 Pending。

制作人仍需按 [111](111-content-studio-event-editor-usage.md) 完成人工手感验收后再标记 Producer Accepted。
