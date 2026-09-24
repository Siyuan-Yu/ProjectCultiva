# 111 · 事件编辑器用法（EventEditor V2）

> 状态：**Producer Accepted / Sealed（含 DYNAMIC-DISCOVERY-01 binding）**｜最后更新：2026-09-25
> 工程：`ExternalTools/ContentAuthoring/EventEditor/`；正式产物：`ExternalTools/ContentAuthoring/Apps/EventEditor.exe`。
> 实施记录：[254 EVENT-EDITOR-V2](254-event-editor-v2-visual-flow-authoring-2026-09-23.md)。

## 1. 产品模型

Event 始终是独立 `contentEvent` Template。左侧“按对象”只依据 `npcDefinitionId` 或 `worldObjectKind/worldObjectId` 建立浏览投影，不改变 ownership；“按事件”列出同一批 canonical templates。未来 Opportunity 可增加新的 binding 方式，不需要重做 Graph。

Graph 是现有 `entryStepId + steps[]` 的可视化：画布节点只有 Step，普通 Next port 写入 `step.nextStepId`，Choice row 的 port 写入 `choice.nextStepId`，未连接表示事件结束。Runtime 没有新增 Graph、Branch、Dialogue VM 或第二套 schema。

## 2. 打开与界面

日常启动正式 `Apps/EventEditor.exe`。源码改动后统一运行 `ExternalTools/ContentAuthoring/编译-所有编辑器.cmd`，不得把 `.build` 或 `bin/Release` 当交付产物。

主界面分三栏：

- 左：内容浏览器，默认按对象分为人物、世界物体和其他事件；也可切换按事件。搜索覆盖 name、topic、event id、NPC、对象与 trigger。名称/Topic 为主，技术 ID 为灰色次要信息。
- 中：自由 Flow Graph。支持中键平移、滚轮缩放、节点拖动、Ctrl 多选、框选、Delete、Ctrl+C/V、自动整理、缩放到全部。
- 右：上下文 Inspector。未选节点时编辑 Event；选 Step 编辑说话人与正文；点击 Choice row 编辑条件、禁用/隐藏、RequirementText 与结果。

顶部“专注模式”会暂时折叠内容浏览与 Inspector，让 Graph 占满工作区；再次点击恢复。它不改变数据或布局坐标。

顶部标题带 `*` 表示 working copy 未保存；Ctrl+S 保存，Ctrl+Z/Y 撤销/重做。切事件、切包或关窗口时，dirty document 必须选择保存、不保存或取消。

`*` 基于 Working JSON 与 Graph layout 相对最近保存状态的真实差异：只浏览、切 Inspector、平移或缩放不会出现 `*`；正文、条件或节点位置改变会出现；手工改回原值或 Undo 回最近保存状态后会自动消失。新建草稿在第一次保存前始终带 `*`。

旧包和 BaseGame 可以合法省略 Runtime 默认字段。打开时编辑器会在内存 working copy 内统一补齐 Inspector 所使用的默认值，然后才建立 clean baseline；该过程不会自动保存磁盘。这样只查看 Event/Step/Choice 时，Inspector 写回同一默认值是真正 no-op，不会制造 Undo 项或 dirty。

## 3. 新建普通对话

1. 点“＋新建”并选择“NPC 普通对话”。
2. 选择人物、填写事件名称；此时只创建内存草稿，不写磁盘。
3. 默认生成 `onTalk`、Priority 0、可重复、`@target` 入口 Step；按对象浏览时显示“★ 普通 / 保底对话”。
4. 直接在入口节点头部选择 Speaker、在正文框写台词；点节点底部“＋ 添加玩家选择”并在 Choice 行内填写玩家选择。
5. 从 Choice 的大号右侧 output port 拖到空白处，在轻量菜单选择下一句 Speaker；编辑器随后生成稳定 Step ID、创建连接、选中新节点并聚焦正文。Choice 来源默认推荐“当前玩家角色”，Enter 可接受推荐，Esc 取消且不改连接。
6. 从玩家 Step 的 Next port 再拖出下一句；说话人可改为当前互动对象。
7. 点保存后才写入 `Content/BaseGame/Data/Events/`，并同步保存 Graph layout metadata。

NPC 特殊对话仍选择人物，但由 Event Inspector 配置 Priority、Conditions 与 Repeat。世界物体互动向导选择 kind/stable id 并自动使用 `onInspect`。其他事件可编辑 manual、arrive、explore、quest completed/failed 等 trigger；工具不是 NPC Dialogue 专用编辑器。

“NPC 保底对话”模板默认 onTalk、Priority 0、可重复、无 Event Conditions；“NPC 对话事件”模板默认 Priority 10，用于条件、任务和剧情内容，但不再推导“状态/特殊/普通”等类型。

Event Inspector 的“作为该人物的保底对话”开启时会清空 Event Conditions，并锁定 Trigger、NPC、Priority、Repeat 与 Conditions。取消后恢复普通编辑并设为 Priority 10。同一 NPC 只能有一条保底；冲突时可跳转现有保底，保存验证也会阻止歧义。

Browser 中保底显示为“★ 保底 · 名称”；其它 Event 直接显示 Name/Topic，并追加 `[Pxx]`、`[条件n]`、`[一次]`、`[每目标一次]` 等事实 badge。

人物绑定统一使用 Character Picker：当前值显示“中文名 — Definition ID”，下方灰字显示定义来源。点“选择…”后可按中文名或 ID 搜索；Event Inspector、新建事件和 Step 的“指定人物…”共用同一份由已加载 `character` definitions 生成的目录。事件磁盘内容仍只保存 Definition ID，来源路径不会写入 Runtime JSON。左侧按对象 Browser 以中文名为主、ID 为灰色次要信息，来源放在 Tooltip。

V2.6 起来源筛选不再位于每个 Picker 内，而统一放在顶部“人物来源”。选择某个实际 source path 后，按对象人物 Browser、Event NPC 选择、New Event NPC 与“指定人物…”同步只列该来源；Picker 搜索保留中文名与 Definition ID，列表仍显示来源 metadata。“全部来源”为默认值。切来源不会写 Event、不会 dirty；若当前 Event 绑定的人物来自其它来源，Inspector 继续显示原 binding，并给出“当前绑定人物不属于当前人物来源筛选”的非阻塞提示。

最后一次主动选择的人物来源会按规范化 Package Root 记录在 `%LOCALAPPDATA%\XianXia\EventEditor\settings.json`。再次打开同一 Package 时，若该来源仍存在会自动恢复；来源已删除、改名或个人设置不可读时无声回退“全部来源”。这是编辑器个人设置，不进入仓库、Content、layout、Undo/Redo 或 dirty 判定。

重复规则文案为“满足条件时可重复／全局仅一次／每个目标仅一次／每角色×目标仅一次”。“满足条件时可重复”不等于保底：它仍参与正常 Priority arbitration。保底固定为 onTalk、P0、可重复、0 Event Conditions；存在任意更高 Priority 合法候选时不会和高层 Topic 一起显示。

## 4. Graph 与 Inspector

- Entry 以绿色“入口”badge 标记；右键节点或 Inspector 可设为入口，正常模式不手填 EntryStepId。
- Step/Choice 技术 ID 自动生成为 `step_001`、`choice_001` 等。移动节点、改正文不改 ID；复制生成新 ID；技术 ID 只在高级折叠中只读显示。
- Speaker 显示“旁白／当前玩家角色／当前互动对象（绑定人物）／指定人物”，磁盘仍保存空、`@actor`、`@target` 或 DefinitionId。
- Node 头部可直接切换旁白、当前玩家、当前互动对象或指定人物；正文是多行编辑框，Enter 换行、Ctrl+Enter 提交。Choice 文字也可在节点内直接编辑，单击 Choice 行会让 Inspector 显示其高级属性。
- 每个节点左侧有 input `○`，Next/Choice 右侧有大号 output `●`。从 output 拖到已有节点正文或 input 创建/重连；拖动时合法 input 高亮，自连接被拒绝并在状态栏提示。
- 连接是带方向箭头的 Bezier 曲线；hover 高亮，单击后可按 Delete，或右键“断开连接”。port 自身的断开菜单仍保留；没有连接即 Event End。
- output 拖到空白处会先选择新节点 Speaker，再真正修改 working copy。画布空白右键也能直接创建玩家、互动对象、旁白或指定人物 Step，并自动进入正文编辑。
- 自动整理按无环 Step DAG 把入口置左、分支向右并纵向展开，并根据正文与 Choice 数量估算节点间距；手动位置仍可继续调整。
- 单节点并不表示迁移不完整：一次 NPC 发言、若干玩家 Choice、然后结束，本来就应是一个 Step。不要按正文标点拆节点；只有流程继续或分支需要进入下一阶段时才创建下一 Step。

## 5. 条件、结果与验证

Inspector 复用 Shared 的结构化编辑器，并以中文表达当前 Runtime 已支持的 kind，例如“境界 ≥”“剧情标记已存在”“任务已完成”“设置剧情标记”“开始任务”“给予资源”“关系变化”。少见的 key 放在对应行字段中，不要求普通路径手写 JSON。

保存前仍以 `EventAuthoringValidator` 与 Runtime Content validation 为 authority。错误面板把能定位的消息映射回 Step/Choice；双击错误会选中节点、定位画布并打开相应 Inspector，错误节点显示红框。

### 动态机会物体（DYNAMIC-DISCOVERY-01）

`Trigger = onInspect` 时可把“世界物体类型”选为“动态机会物体”，随后从 World Opportunity 模板选择器中选择 `spawnKind=worldObject` 的定义。编辑器保存 `worldObjectKind=opportunityObject` 与模板 `worldOpportunityId`，并保持 `worldObjectId` 为空；运行时实例 ID 由 Opportunity Board 分配，作者不手写。

Step／Choice Outcomes 可选“解决当前世界机会”（`resolveCurrentOpportunity`）。该 Outcome 不需要 ID 或 Amount；普通 Inspect 不会自动结束机会，只有明确配置这个 Outcome 或等待 expiry 才进入 terminal。

该动态模板绑定与显式解决 authoring 已随 DYNAMIC-DISCOVERY-01 于 2026-09-24 完成制作人验收并封板。

## 6. Working copy、保存与 layout

现有事件打开后先深拷贝为 working document；新事件也是纯内存草稿。保存前校验，成功后复用 `PackageStore.SaveDefinition` 的临时文件安全写入；保存失败不覆盖源定义。

画布坐标不进入 Runtime Step schema。版本化 metadata 位于：

`Content/BaseGame/Authoring/EventEditor/layouts.v1.json`

格式为 `schemaVersion + events[{eventId,nodes[{stepId,x,y,collapsed}]}]`。Package Loader 只扫描 `Data/**/*.json`，因此 Runtime 完全不读取该文件。保存会清理不存在的 event/step layout。

## 7. Legacy 边界

BaseGame 当前 15 条 `contentEvent` 已全部使用 Steps，legacy-authoring 数量为 0。其中审计基线为 14 条（既有 Steps 5 条、由 legacy 等价迁移 9 条），另有本轮用于实际保存与回读验收的主管普通对话 1 条。Runtime/Data Loader 可继续读取外部旧包的 `body/choices`；EventEditor 打开旧定义时只显示“旧版事件格式”和“转换为当前格式”，不再提供长期双轨编辑页。转换规则为单入口 Step：onTalk 默认 `@target`，其他 trigger 默认旁白，旧 Choice 条件/结果保持、Next 为空。

EVENT-02 已为 onTalk 增加可选 `npcTags[]` 与 `worldOpportunityId`：Event Settings 可编辑标签并从 Name＋ID 下拉选择 Opportunity；无固定人物的 Event 在按对象 Browser 中进入“通用人物模板”。随机动态 WorldObject、hidden、通用剧情 Snapshot persistence、Cutscene 与脚本语言仍未实现。
