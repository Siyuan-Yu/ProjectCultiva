# EVENT-EDITOR-V2 — Visual Event / Dialogue Flow Authoring

> 日期：2026-09-23  
> 状态：**Producer Accepted / Sealed（2026-09-23）**
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

### V2.1 Graph-first follow-through

本轮在同一 `EventFlowGraph`、`EditorSession` 与 JSON working copy 上完成制作面升级，没有引入第二套 Graph framework：

- Step 左侧增加明确 input port，Next/Choice 右侧 output port 放大并提供 hover、拖拽提示与未连接强调；节点正文和 Choice 文字改为 inline editor，Speaker 在节点头部直接选择。
- 直线替换为带箭头的 WPF Bezier `Path`。连接可 hover、单击选择、Delete 或右键断开；拖线使用同样的曲线 preview，合法 input 与当前 hover 目标高亮，释放到节点 body 也可命中，自连接不产生 mutation。
- 拖到空白先弹出 Speaker 菜单；Choice 来源推荐玩家，玩家来源推荐互动对象，互动对象来源推荐玩家。确认后才创建、连接、定位并聚焦新 Step；Esc 不改变 working copy。空白画布右键也可快捷创建 Step。
- 自动布局按估算正文行数与 Choice 数量增加纵向间距；工具栏“专注模式”折叠 Browser/Inspector，让 Graph 占据主体空间。
- `base:event_event_editor_v2_supervisor_chat` 继续作为正式 acceptance content：三 Step、两条连接及 layout 均已存在，因此 V2.1 不重复改写正式对话内容。

V2.1 静态回读确认该 acceptance event 为 3 Step／2 connection／3 layout node，speaker 顺序为 `@target → @actor → @target`。Shared 与 EventEditor 单独 build 为 0 warning／0 error；Build All 成功发布 10 个 Editor，正式 `Apps/EventEditor.exe` 更新时间为 2026-09-23 11:33:38，启动后窗口标题正确且进程 Responding。当前 computer-use 会话只暴露浏览器、不暴露 native app target，因此逐项点击/拖线仍保留为制作人人工手感验收，未伪报自动通过。

### V2.2 Dirty correctness 与 Dialogue classification

- `ShowEventInspector` 改为纯 UI load/show，不再隐式提交；从 Step/Choice 切回 Event 时由调用者在切换前显式 commit。`SetSession` 因此不会把旧 Inspector controls 写入新 Event。
- `EditorSession` 保存 Working JSON＋layout clean baseline；Mutation、Undo、Redo 后统一重算差异。现有 Event 加载后 clean，新建 Event 首次保存前强制 dirty，保存后以当前状态更新 baseline。
- fallback 标签只用于无条件、可重复、Priority 0 的 onTalk；V2.4 已进一步取消“◇ 状态对话”等推导类型。NPC 对话事件新建模板默认 Priority 10。
- 将老五条条件状态对话 Priority 从隐式 0 统一为 10，Conditions、Outcomes、Quest、Minigame 与 Step 内容未改；不新增将老 fallback，也不把单 Step 内容机械拆成多 Step。

V2.2 验证：Shared/EventEditor 单独 build 均为 0 warning／0 error；静态回读确认将老五条 Priority 10 且 Conditions 数量分别为 1/3/3/2/1，supervisor acceptance 仍为 Priority 0、repeatable、0 Conditions。Build All 成功发布，正式 exe 更新时间 2026-09-23 12:24:44；启动后进程 Responding，初始窗口标题无 dirty `*`。当前 computer-use 会话仍未暴露 native app target，无法可靠注入切 Event、编辑和 Undo，因此这些交互项明确保留为制作人人工 smoke，而非伪报通过。

### V2.3 Canonical working document

残余 false dirty 来自合法 sparse JSON 与 Inspector 显式默认值之间的 shape 差异。新增 `EventEditorDocumentNormalizer`，Existing Event 在 clone 后、New Event 在 raw 创建后、Legacy Convert 在转换末尾统一 canonicalize；clean baseline 始终建立在 canonical working representation 与已补齐的 layout 上。

Event 默认补齐 name、trigger、priority、topicText、once、onceScope、conditions；Step 补齐 speakerRef、text、outcomes、choices；Choice 补齐 text、conditions、outcomes、nextStepId、unavailableMode、requirementText。location/quest/NPC/object binding 不铺空字符串，而按 `JsonEdit.SetString` 的 sparse 语义清理空值。Graph refresh 同时取消 `Prune/GetOrCreate`，只读取 baseline 前已规范化的 layout。

V2.3 build：Shared/EventEditor 均为 0 warning／0 error，Build All 成功发布 10 个 Editor；正式 exe 更新时间 2026-09-23 12:40:59，启动后 Responding 且初始标题无 `*`。静态回读确认 `base:event_jiang_lao_ready_claim` 的磁盘源仍保持 sparse（缺 topicText/onceScope，Choice 缺 unavailableMode/requirementText），没有用批量改 Content 掩盖问题。当前 computer-use 不暴露 native app target，因此 CASE 1–7 的完整点击级 smoke 仍明确为制作人人工验收项。

### V2.4 Explicit fallback dialogue

Browser 不再显示“状态对话”等伪分类。唯一特殊 authoring 概念是保底：onTalk、具体 NPC、P0、repeatable、0 Event Conditions；其它 Event 只显示 Name/Topic 与 Priority、Conditions、repeat scope factual badges。

Event Inspector 增加保底 toggle。开启会清空 Event Conditions 并锁定 Trigger/NPC/Priority/Repeat/Conditions；取消会恢复普通编辑并设为 P10。同 NPC 第二条保底会在新建/勾选时提示跳转，并在保存 validation 中报告歧义。新建向导改名为“NPC 保底对话”与“NPC 对话事件”。

BaseGame 静态审计只有 `base:event_event_editor_v2_supervisor_chat` 符合保底，且无重复；将老五条保持 P10 与原 Conditions。EVENT-01 acceptance IDs/flags 仅存在于自身内容与 layout，未发现正式消费者，但因 EVENT-01 仍为 Producer Acceptance Pending，本轮保留，未创建 Acceptance 类型或 badge。

V2.4 build：EventEditor 0 warning／0 error，Build All 成功发布 10 个 Editor；正式 exe 更新时间 2026-09-23 13:41:14，启动后 Responding，标题无 dirty `*`。完整 toggle/冲突弹窗点击手感仍由制作人验收。

### V2.5 Human-readable Character Picker

Shared 新增 `CharacterDefinitionInfo` 与 `PackageStore.AllCharacterDefinitions`。目录直接从当前 `ContentPackage` 已加载的 `character` definitions 构建，包含中文名、稳定 Definition ID、相对 package root 的来源路径和包名；不读取 Authoring CSV，也不把来源写进 Event schema。

可复用 WPF `CharacterPicker` 在当前值中显示“中文名 — ID”与灰色来源；统一选择窗口支持按中文名、ID、来源路径搜索，并按实际来源文件筛选。Event Inspector 的 `npcDefinitionId`、NewEventDialog 的 NPC、Step Speaker“指定人物…”均已切换到该控件。按对象 Browser 保持中文名主标题、灰色 ID 和带来源的 Tooltip。

V2.5 静态回读确认将老、杂役主管、主角、试炼弱匪和青石验收人物均命中预期 source file。Shared/EventEditor 单独 build 为 0 warning／0 error，`git diff --check` 通过；Build All 成功发布 10 个 Editor（仅 MapEditor 既有 4 条 nullable warning），正式 exe 更新时间为 2026-09-23 14:31:28。启动后只保留一个正式窗口，进程 Responding，标题为 `XianXia · Event Editor`。当前 computer-use 会话未暴露 native app surface，因此搜索与来源筛选的点击手感保留为制作人人工验收。

### V2.6 Repeat clarity 与全局人物来源

Runtime 静态审计确认 `RepeatAllowed` 对 `once=false` 直接放行，Event Finish 也只有 `spec.Once` 才 `MarkFired`；candidate resolver 在 Trigger/Binding/Repeat/Conditions 之后只保留最高 Priority，并保留同优先级全部候选。因此 Repeatable completion 不会阻止下一次候选，P50 会压住 P0 fallback，同层 P50 会共同进入 Topic Selection，Global Once 完成后由 fired gate 排除。当前 acceptance 的“验收·问点私事”为 P50、`once=false`、0 Conditions；Runtime 无需修改。

UI 重复选项改为“满足条件时可重复／全局仅一次／每个目标仅一次／每角色×目标仅一次”，保底帮助明确其无条件、可重复、P0 且只在无更高合法对话时出现。顶部新增单一“人物来源”状态，动态收集实际 source paths；Browser、Event NPC、New Event NPC、指定 Speaker 全部消费该状态。Picker 内部来源下拉删除；当前 binding 被筛掉时保留值并显示非阻塞提示。

仓库 `AGENTS.md` 禁止新增或运行自动化测试，因此本轮未改/未跑 NUnit；以现有 Resolver、现有 Once/Priority 测试源码和 acceptance JSON 的静态回读作为证据。Shared/EventEditor build 均为 0 warning／0 error，Runtime 与测试文件 diff 为空，`git diff --check` 通过。Build All 成功发布 10 个 Editor（仅 MapEditor 既有 4 条 nullable warning），正式 exe 更新时间 2026-09-23 14:54:12；启动后单窗口 Responding，标题正常。computer-use 未暴露 native app surface，四入口同步筛选和切换不 dirty 的点击级手感仍待制作人验收。

### Final Patch：记忆人物来源与封板

EventEditor 新增 editor-local `EventEditorUserSettings`。最后一次主动选择的人物来源按规范化绝对 Package Root 写入 `%LOCALAPPDATA%\XianXia\EventEditor\settings.json`；打开 Package 时先扫描实际 character sources，仅当保存值仍存在才恢复，否则使用“全部来源”。设置读写失败为非阻塞状态栏 warning；设置不进入 Content、manifest、Graph layout、Runtime、Undo/Redo 或 dirty。

2026-09-23 制作人确认 EventEditor V2 producer acceptance passed。已验收并封板 Graph-first authoring、Step/Choice connection、inline authoring、可读 Speaker、Conditions/Outcomes、Priority/Topic/Repeat/Once、显式 NPC fallback、dirty/undo/redo、layout persistence、对象/事件 Browser、全局人物来源、可读 Picker、记忆上次来源，以及 NPC onTalk／WorldObject onInspect authoring。此前唯一剩余 UX 问题即重开后未恢复人物来源，已在本 final patch 关闭。状态更新为 **Producer Accepted / Sealed**，不继续拆 V2.7/V2.8。

Final Patch 验证：EventEditor build 0 warning／0 error，`git diff --check` 通过，Build All 成功发布 10 个 Editor（仅 MapEditor 既有 4 条 nullable warning）；正式 `Apps/EventEditor.exe` 更新时间 2026-09-23 15:22:04，单窗口启动后 Responding、标题正常。设置文件在制作人首次主动切换来源时创建，位于仓库外且未进入 Git。当前 computer-use 未暴露 native app surface，因此关闭／重开与失效 source 的点击级 smoke 未由代理伪报通过；恢复、fallback 与 no-dirty 路径已做静态接线核对，制作人可在当前已打开正式版中直接复核。

## 非目标与冻结边界

未实现 EVENT-02 Opportunity Director、随机对象、tag/archetype runtime binding、ContentIntent、剧情 Snapshot persistence、Cutscene、概率脚本或 Runtime Graph。Snapshot schema 未改变。EVENT-01 Runtime、HostDialogue、Priority/Topic/Step/Outcome transaction 保持原 authority。

## 验证与发布

- Shared / EventEditor Release build：0 warning / 0 error。
- BaseGame Event authoring validation：15 events，0 errors，0 legacy（审计基线 14，迁移 9，新增验收内容 1）。
- Runtime Content Loader / ReferenceValidator、全仓 offline compile、diff-check 与正式 Build All：见本轮最终报告。
- 正式交付仅为 `ExternalTools/ContentAuthoring/Apps/EventEditor.exe`；`.build` 不是交付产物。

正式 `Apps/EventEditor.exe` 可启动，Windows Accessibility 树已读到 Browser、Flow Graph、Event/Step/Choice Inspector、Priority、Topic、Repeat 与 onTalk/onInspect binding 控件。本机自动化层对该 WPF 窗口持续返回 `coordinate input geometry is unavailable`，重新启动后仍无法注入点击，因此自动 UI 输入/拖线和逐项回读未记为通过；数据与 layout 由静态回读和正式 validation 覆盖，制作人手感验收仍为 Pending。

制作人仍需按 [111](111-content-studio-event-editor-usage.md) 完成人工手感验收后再标记 Producer Accepted。
