# 111 · 事件编辑器用法（EventEditor V2）

> 状态：**Implementation Complete / Producer Acceptance Pending**｜最后更新：2026-09-23
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

顶部标题带 `*` 表示 working copy 未保存；Ctrl+S 保存，Ctrl+Z/Y 撤销/重做。切事件、切包或关窗口时，dirty document 必须选择保存、不保存或取消。

## 3. 新建普通对话

1. 点“＋新建”并选择“NPC 普通对话”。
2. 选择人物、填写事件名称；此时只创建内存草稿，不写磁盘。
3. 默认生成 `onTalk`、Priority 0、可重复、`@target` 入口 Step；按对象浏览时显示“★ 普通 / 保底对话”。
4. 在入口 Step Inspector 填主管台词；点“＋添加 Choice”填写玩家选择。
5. 从 Choice port 拖到空白处，编辑器自动生成稳定 Step ID、创建连接、选中新节点并聚焦正文。Choice 来源的新节点默认建议“当前玩家角色”。
6. 从玩家 Step 的 Next port 再拖出下一句；说话人可改为当前互动对象。
7. 点保存后才写入 `Content/BaseGame/Data/Events/`，并同步保存 Graph layout metadata。

NPC 特殊对话仍选择人物，但由 Event Inspector 配置 Priority、Conditions 与 Repeat。世界物体互动向导选择 kind/stable id 并自动使用 `onInspect`。其他事件可编辑 manual、arrive、explore、quest completed/failed 等 trigger；工具不是 NPC Dialogue 专用编辑器。

## 4. Graph 与 Inspector

- Entry 以绿色“入口”badge 标记；右键节点或 Inspector 可设为入口，正常模式不手填 EntryStepId。
- Step/Choice 技术 ID 自动生成为 `step_001`、`choice_001` 等。移动节点、改正文不改 ID；复制生成新 ID；技术 ID 只在高级折叠中只读显示。
- Speaker 显示“旁白／当前玩家角色／当前互动对象（绑定人物）／指定人物”，磁盘仍保存空、`@actor`、`@target` 或 DefinitionId。
- Node 只显示正文摘要、Choice rows、条件/结果/Entry/Error badges；完整正文始终在 Inspector 编辑。
- 从 port 拖到已有节点创建连接；拖到空白自动创建下一句；port 右键“断开连接”。没有连接即 Event End。
- 自动整理按无环 Step DAG 把入口置左、分支向右并纵向展开；手动位置仍可继续调整。

## 5. 条件、结果与验证

Inspector 复用 Shared 的结构化编辑器，并以中文表达当前 Runtime 已支持的 kind，例如“境界 ≥”“剧情标记已存在”“任务已完成”“设置剧情标记”“开始任务”“给予资源”“关系变化”。少见的 key 放在对应行字段中，不要求普通路径手写 JSON。

保存前仍以 `EventAuthoringValidator` 与 Runtime Content validation 为 authority。错误面板把能定位的消息映射回 Step/Choice；双击错误会选中节点、定位画布并打开相应 Inspector，错误节点显示红框。

## 6. Working copy、保存与 layout

现有事件打开后先深拷贝为 working document；新事件也是纯内存草稿。保存前校验，成功后复用 `PackageStore.SaveDefinition` 的临时文件安全写入；保存失败不覆盖源定义。

画布坐标不进入 Runtime Step schema。版本化 metadata 位于：

`Content/BaseGame/Authoring/EventEditor/layouts.v1.json`

格式为 `schemaVersion + events[{eventId,nodes[{stepId,x,y,collapsed}]}]`。Package Loader 只扫描 `Data/**/*.json`，因此 Runtime 完全不读取该文件。保存会清理不存在的 event/step layout。

## 7. Legacy 边界

BaseGame 当前 15 条 `contentEvent` 已全部使用 Steps，legacy-authoring 数量为 0。其中审计基线为 14 条（既有 Steps 5 条、由 legacy 等价迁移 9 条），另有本轮用于实际保存与回读验收的主管普通对话 1 条。Runtime/Data Loader 可继续读取外部旧包的 `body/choices`；EventEditor 打开旧定义时只显示“旧版事件格式”和“转换为当前格式”，不再提供长期双轨编辑页。转换规则为单入口 Step：onTalk 默认 `@target`，其他 trigger 默认旁白，旧 Choice 条件/结果保持、Next 为空。

EVENT-02 Opportunity Director、随机 NPC/Object、tag/archetype runtime binding、剧情 Snapshot persistence、Cutscene 与脚本语言均未实现。
