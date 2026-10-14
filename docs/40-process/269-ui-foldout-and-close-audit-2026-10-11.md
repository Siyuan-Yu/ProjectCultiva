# 269 · UI 折叠菜单与关闭专项核查（2026-10-11）

> 状态：**UI 折叠菜单与关闭修复 Implementation Complete / Producer Acceptance Pending**  
> 核查性质：源码、调用链与资源结构静态检查；未启动 Unity／PlayMode／Runner，以下“人工验证”均为待执行步骤。  
> 范围：既有 UI 的关闭、返回、取消、完成入口；不改变 Gameplay 事务语义。

## 1. LevelTester 二级目录与关闭生命周期

LevelTesterToolCatalog 的 74 个 ToolId 和九个显示分组保持不变。每个目录项增加稳定 GroupKey；HostLevelTesterCheatPanel 用 HashSet<GroupKey> 保存用户展开偏好，不依赖列表下标。首次显示只展开当前功能所属组；大类按钮只切换本组，小功能仍调用 SelectTool。收起当前组保留右侧当前页和滚动状态。

搜索不修改展开集合。搜索期间临时绘制包含命中项的组和命中小功能；清空搜索后重新按原集合显示。目录内容高度由当前可见组数和小功能数计算。搜索、折叠和选页本身均不调用业务 service。

关闭按钮矩形保持完整 64×20；标题拖动区止于按钮左侧 8px，且 GUI.DragWindow 在其它控件之后执行。Hide 取消 Session Reset 与 Content section 待确认，释放 TextInputOwner，并把 persistent rect 退休到本帧。GUI.Window 返回后若已隐藏，会立即退出，不再登记矩形、读取焦点或重新获取 owner。外部调用 Hide 只记录延迟清理请求；合法 GUI 回调只清名称以 LevelTester. 开头的焦点，并仅在当前 hotControl 仍等于本窗口记录值时释放拖动捕获。最后选中功能、搜索文字和组偏好保留，危险确认不保留。

## 2. 逐窗关闭核查表

| 界面 | 按钮／键 | 实际 handler 与可见状态 | 暂停／输入／等待及清理 | 本轮结论 | 待制作人人工验证 |
|---|---|---|---|---|---|
| LevelTester | “关闭”、反引号、外部 Hide | Hide() → visible=false | 释放 TextInputOwner、退休 persistent rect、取消两类确认；GUI-safe 清本窗口焦点／拖动 | **已修**：DragWindow 重叠及关闭后重获 owner | 搜索框聚焦后点按钮中心／边缘；确认当帧不穿透、次帧可操作世界；重开确认偏好保留且无待确认 |
| Character Sheet | ×、Escape、主体失效 | 全部经 Close() → open=false | 释放 CharacterSheet modal pause 与本面板 legacy gate | **已修**：三条直接赋值旁路；× 后停止续绘 | 分别用 ×／Esc／失效主体关闭，确认世界恢复且人物选择不变 |
| Cultivation | 主页关闭、详情关闭、Esc／C、主体失效、仪式开始 | 整窗经 Close()；“返回”只关详情；确认取消只关子层 | Close 清详情和确认并释放 CultivationPanel pause | **已修**：主页、熟练详情和失效／切换路径旁路 | 主页、详情、突破确认各层逐次 Esc；关闭不取消已开始仪式 |
| Combat Arts | 主页／详情关闭、Esc、研读开始 | Close() 清详情和确认；返回只回列表 | 释放 CombatArtsPanel pause | **已修**：研读成功旁路与主页关闭后续绘 | 主页与详情关闭；启动研读后面板消失、仪式条继续且无双重暂停 |
| Inventory（背包／仓库） | B、两视图“关闭” | Toggle/按钮 → Close() | 释放 InventoryPanel pause | **已修**：两个按钮关闭后立即返回，避免同帧继续绘制 | 两视图分别关闭；关闭点击不能触发下方格子，库存不回滚 |
| Construction | Toggle／关闭；建造转 placement | 普通关闭经 Close；建造使用 CloseForPlacement | 两者释放 ConstructionPanel pause；工单不删除 | 原路径正确，保持 | 关闭菜单不删除已有工单；选择建造后 placement 仍可用 |
| Mortal Work | Toggle／关闭 | Close() → open=false | 释放 MortalWorkPanel pause | 原路径正确，保持 | 关闭后职业分配保留，世界恢复 |
| Quest Journal | J、右上入口、日志关闭 | Toggle() 统一分流 Open/Close；按钮关闭后返回 | 释放 QuestJournal pause，并按其它 Inventory／Construction 状态重算 legacy gate | **已修**：直接 toggle／赋值旁路 | 用 J、入口按钮、面板按钮各关一次；其它模态仍开时不得误解锁 |
| World Map | M、顶部关闭、无导航关闭 | CloseWithLocalMapTakeover 或 CloseInternal | 释放 WorldMapPlanningOverlay owner，清 pan／flyout；按既有规则恢复 Surface travel | 原路径正确，保持 | 自动旅行中开图再关图继续旅行；普通 Close 不改路线 |
| Strategic Character List | 关闭 | Close() → _open=false | 无独立 pause／owner，由 WorldMap 管理 | **已修**：关闭后停止本帧详情续绘 | 关闭 flyout 后地图保持，列表不再响应同次点击 |
| Faction Diplomacy Overview | 关闭 | Close() → _open=false | 无独立 pause／owner，由 WorldMap 管理 | **已修**：关闭后停止本帧详情续绘 | 关闭 flyout 后地图保持，人员／外交数据不变 |
| FormalHud 建筑／住房／恢复点／囚室检视 | × | DrawInspectShell → housingAreaSelection.Clear() | 清唯一 Inspect target；无 pause／named input | 原路径正确，保持 | 逐类点开后用 × 关闭；世界物体和业务状态不变 |
| World Activity 详情 | 关闭 | 清 selectedActivityId/status | 无 pause；不删除 board/history | 原路径正确，保持 | 关闭详情后活动仍在列表／历史 |
| World Activity 历史 | 关闭 | historyOpen=false 并立即返回 | 无 pause；不删除历史 | **已修**：关闭后不再进入 ScrollView | 点击关闭位置不应同时选中历史行 |
| Shop Trade | UGUI 关闭、Esc、距离／会话失效 | Close() 隐藏动态 root | 释放 ShopTrade pause/input owner和 shopkeeper wait；不撤销交易 | 原路径正确，保持；按钮由代码绑定 Close | 买卖一次后关闭，余额／库存保持；重新接近可再开 |
| Auction | UGUI 关闭、Esc、距离／会话失效 | Close() 隐藏动态 root | 释放 AuctionPanel pause/input owner和 provider wait；不改 listing/escrow/claim | 原路径正确，保持；按钮由代码绑定 Close | 寄拍／出价后关闭，托管和冻结资金保持 |
| Manual Learn Prompt | 取消、Esc；覆盖确认取消／成功转仪式 | 整窗经 Close；覆盖取消只清 pending learner | 释放 ManualLearnPrompt pause；成功不重复扣物品 | 原路径正确，保持 | 普通取消、覆盖取消、成功转仪式三条路径 |
| Combat Art Learn Prompt | 取消、Esc、成功转仪式 | Close() 清 item/status | 释放 CombatArtLearnPrompt pause | 原路径正确，保持 | 取消不学习；成功只启动一次仪式 |
| Cultivate Confirm | 取消／确认／主体失效 | Close() 后再下发正式命令 | 释放 CultivateConfirmPrompt pause；按钮消费当前事件 | 原路径正确，保持 | 取消零副作用；确认仅下发一次 |
| Breakthrough Ritual | Esc/F1／取消；结果“知道了” | CancelChannel 与 CloseResult 分离 | channel cancel 无惩罚；结果关闭释放 BreakthroughRitual pause，不回滚结算 | 原路径正确，保持 | 取消 channel 无结果弹窗；结果关闭不重复结算 |
| Skill Study Ritual | Esc/F1／取消；结果“知道了” | CancelChannel 与 CloseResult 分离 | 结果关闭释放 SkillStudyRitual pause；不回滚学习／突破 | 原路径正确，保持 | 取消与完成结果分别验证一次 |
| TicTacToe | 完成后的“结束” | CloseAndNotify 先关窗、清 callback、释放 TicTacToe pause，再单次回调 | onFinished 清空防重复通知 | 原路径正确，保持 | 完成后连续点击只应结算一次 |
| Content Interrupt | 事件选项；任务通知“知道了” | 正式事件必须选 choice；通知只清 activeQuestNotify | ContentInterrupt pause 由 HasBlockingInterrupt 同步；不提供跳过 ActiveEvent | 原路径正确，保持 | 通知关闭恢复；正式事件不能只隐藏后留下 ActiveEvent |
| Dialogue UGUI／Presenter／Controller | 可 dismiss fallback 的按钮／Esc；正式 choice | View 只转发；Presenter 调 DismissFallback 或 Controller choice；inactive 后 view Hide | 释放 Dialogue pause并按小游戏状态恢复 world input；正式剧情只经 choice 结束 | 原路径正确，保持；UGUI listener 运行时接线 | fallback 可关；正式对话无万能跳过；结束后 NPC wait 可释放 |
| NPC Context Menu | Esc、外点、各确认取消 | CloseAll 清 phase/targets/callback；Recruit 有专用 cancel | 分别释放 aggression／dismantle owner；outside click Use；NPC 按正式等待释放 | 原路径正确，保持 | 菜单外点不穿透；取消攻击／拆除不执行；等待正常释放 |
| Character Encounter 确认／战报 | “取消主动攻击”；战报“继续” | CancelPending()；Committed 战报必须经 CloseReport() 阶段校验 | 释放 CharacterEncounterUI pause/input lock并清 prepared field；报告关闭后再清 Presenter report | 原路径正确，保持 | Pending 可取消；restore 禁止非法取消；Committed 报告只关闭一次 |

## 3. 资源与层级结论

本表中的 IMGUI 面板没有 Scene/Prefab Button.onClick 绑定。Shop、Auction 与 Dialogue UGUI 均在脚本中创建 root、Canvas／Raycaster（或使用现有 view root）并显式注册 listener；本轮未发现需要改 Scene／Prefab 引用才能成立的关闭缺陷，因此没有修改资产或 .meta。运行时层级、遮罩和实际 Raycaster 命中仍须按下节在 Unity 人工确认，静态检查不冒充点击验证。

HostInputGate.Clear() 只清 legacy 两个 bool，不清 named owner。LevelTester、WorldMap、Shop、Auction 与 CharacterEncounter 的具名 owner 均只按自己的字符串 Acquire／Release；本轮没有调用 ResetSession、没有清空其它 owner、没有修改 ManualPaused 或 Time.timeScale。各 modal pause 继续使用自己的 owner；关闭商业／工作／建设／活动界面不会撤销已完成事务。

## 4. 人工验收顺序

1. 打开 LevelTester，确认默认只有当前组展开；任意展开三组、收起当前组，右侧页保持。搜索另一收起组中的功能并选中，清空搜索后原三组状态恢复。
2. 搜索框保持光标时依次点击关闭按钮中心、文字和边缘。每次都应关闭；标题其它空白仍可拖动。关闭当次点击不传到世界，下一帧世界恢复。
3. 打开 Session Reset 或任一 Content 危险确认后直接关闭。重开仍保留搜索、组偏好和最后页，但只显示初始危险按钮，不自动执行。
4. 依次用 ×／Esc／面板按钮关闭人物页、任务日志、修炼页；在修炼熟练详情先测“返回”，再测“关闭”。确认每个窗口只释放自己的暂停。
5. 分别关闭斗技、背包、仓库、战略角色、势力、活动历史，确认同一鼠标事件没有触发关闭按钮下方内容。
6. 对 Shop／Auction、学习提示、两类 Ritual、小游戏、Content 通知、Dialogue、NPC 菜单和 CharacterEncounter 按表中最后一列做最短 smoke；重点确认已完成事务不回滚、正式事件不被跳过、战报沿用 CloseReport。

## 5. 静态验证边界

已执行 Core／Data／Unity／Editor offline compile、ToolId／GroupKey 计数与唯一性、关闭调用链、owner 成对、滚轮隔离、.meta/GUID 和 git diff --check。未启动 Unity，未运行 PlayMode、Runner、Test Runner 或新增测试框架；所有实际点击、Canvas 排序和运行时暂停恢复仍标为 Producer Acceptance Pending。

