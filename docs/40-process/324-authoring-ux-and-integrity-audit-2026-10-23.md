# 作者工具易用性与内容保存完整性收口（2026-10-23）

> 恢复后追加：任务日志显示条件及编辑器控件接通，任务保存不再清除对话发放条件；旧事件生成器保护现有分步对话及人物委托，关联扫描识别acceptQuestFromTarget。完整结果见329。

> 状态：Design Confirmed（制作人授权本轮全项目审查与合理优化）／Implemented／Producer Acceptance Pending。本页负责 ExternalTools/ContentAuthoring 作者入口，不改变玩法、正式 Content、Surface 发布几何、美术资源精细度、Freeze、Packages 或 ProjectSettings。当前工作区已有地图扩展与 FineEditor 修改，全部保留。

## 当前证据与本轮决定

316 已修的体魄、多项任务交付、独立正式名册、通用 Blueprint 发布与 EXE 更新不重复列为缺陷。本轮只处理以下静态可证实的问题：

| 问题 | 审查时点代码证据 | 决定 |
|---|---|---|
| 坏 JSON 被静默忽略，包总览可能显示“校验通过” | `Shared/PackageStore.cs:111–129` 捕获解析异常后直接 continue；`Shared/PackageValidator.cs:9` 只检查已加载定义 | 保留带相对路径、JSON 行列的加载错误；包总览立即显示，校验包含加载错误；内容不完整时拒绝保存并提示修复后重新加载。 |
| 多个工具打开同包后，旧内存可覆盖别人刚保存的同文件；临时文件名固定且最后使用非原子的覆盖复制 | `Shared/PackageStore.cs:160–188` | 为已加载文件记录磁盘版本，保存前发现外部修改即拒绝覆盖；用同目录唯一临时文件与原子替换提交。明确这是文件级冲突保护，不自动合并作者修改，不承诺跨文件事务。 |
| 新定义按文件名子串寻找目标，可能写入错误的任务／事件文件 | `Shared/PackageStore.cs:291–305` 的 Contains 与 fallback | 保持现有 ID 路由规则，按最终完整目标路径精确匹配；不存在就新建该路径。避免失败后把尚未落盘的新定义留在包索引中。 |
| 地点表保存会丢失未展示的合法字段 | `LocalPlaceEditor/MainWindow.xaml.cs:222–279` 从字段白名单重建地点，未保留 `enterConditions` | 从原地点对象克隆后更新表单管理字段，保留条件等其余合法字段；清空可编辑字段仍正常删除；保存前提交表格编辑并检查重复地点 ID／起点。 |
| 随机机会编辑器会把共享人物生成池数量改回 1 | `OpportunityEditor/MainWindow.xaml.cs:211、414` 只读权重，保存时硬写 countMin=countMax=1 | 显示与保存原 countMin/countMax，数量允许既有非负值且 max≥min，权重为正数，保留池条目未展示字段；不改变机会生成规则。 |
| 独立打开含多条定义的地图文件会只登记一张地图，后续保存可能截断其他定义 | `Shared/PackageStore.cs` 的 `RegisterStandaloneMap` 只建立单条 ContentFile | 独立入口明确拒绝多定义文件并引导通过“打开包”编辑；单地图登记同样记录磁盘版本。 |

## 实施与检查边界

- 先完成上述共享与界面接线，再执行既有基础编译／发布 11 个编辑器；发布只更新忽略的 Apps 产物。
- 不编写或运行自动测试、工具测试、Unity Test Runner、PlayMode 或 batchmode，不启动 Unity。
- 运行体验由制作人人工确认：坏 JSON 能定位；两个工具编辑同文件时不会静默覆盖；地点门槛与生成池数量保存后保留。只用独立候选副本做破坏性输入，不污染正式 Data。
- 未实现的泛化内容重命名／跨文件事务／所有窗口统一撤销框架不在本轮范围，不将其列为开始第一章制作的前置条件。

## 本轮结果

以上六项均已实施，当前接线位置：

- `Shared/PackageStore.cs:104` 加载错误保存到 `LoadIssues`；`Shared/PackageValidator.cs:9` 合并这些错误；`PackageBrowser/MainWindow.xaml.cs:39、74` 在加载时和校验时显示文件。包总览的通过文案说明这只是作者检查，正式引用仍由 Unity 内容校验完成。
- `Shared/PackageStore.cs:180` 保存使用唯一临时文件、同目录原子替换、同文件进程间互斥和已加载文本的 SHA-256 版本检查。检测到其他编辑器保存时给出保留当前修改、重新加载的提示，不静默覆盖。`SaveDefinition` 还拒绝空／重复 ID；新建文件用不覆盖的移动提交。克隆定义使用 `DeepClone`，去掉原先一次额外 JSON 序列化和重解析。
- `Shared/PackageStore.cs:346` 按最终完整路径追加定义，失败不把新定义加入包索引；`Shared/EditorPrompts.cs:8` 与各新建／保存按钮把 IO 或校验失败显示为明确错误，不因未捕获异常退出窗口。功法、斗技、地点表的新建也统一走该共享入口。
- `LocalPlaceEditor/MainWindow.xaml.cs:148、225、247` 保存前提交单元格，检查重复地点和起点，克隆原地点再更新表单字段，保留 `enterConditions` 等未展示数据。
- `OpportunityEditor/MainWindow.xaml.cs:215、436` 与 `MainWindow.xaml:56` 读取、显示、校验并保存人物池的数量范围；共享池的原始条目其他字段一并保留。
- `Shared/PackageStore.cs:742` 独立地图打开不会再把多定义文件降为单定义；通过包入口编辑仍可保留整个文件。

2026-10-23 19:42:10—19:42:56（Asia/Shanghai），执行既有 `publish.ps1 -LogPath build-all.log`，11 个 Release 编辑器均成功发布，Apps 整体切换成功，退出码 0。WorldComposer／FineEditor 一并包含开始本轮前已有的地图工具修改；本轮没有改动那些源码。MapEditor 原有 4 条 nullable 警告仍在，无新增编译警告。`git diff --check` 无空白错误。未运行任何自动测试、工具测试或 Unity；未提交或推送。

### 窄范围人工确认

1. 在候选包副本制造一个缺逗号的 JSON，PackageBrowser 应显示相对路径与行／字节列，不能显示全部通过；修复后重新加载。不要在正式 Data 制造错误。
2. 两个编辑器先打开同一个候选包，同文件先由 A 保存，再由 B 保存；B 应提示磁盘已有外部修改，A 的内容保留。单文件保护不等于多文件批量保存事务。
3. 在带 `enterConditions` 的地点上只改名称，以及在含 0—3 数量范围的生成池上只改权重；保存后其余字段保留。最后一次正在输入的表格单元格也应写入，非法数字应被拒绝。

这些是本次新增编辑器行为的人工确认，不要求重做既有游戏系统验收。当前只记录 Implemented 与真实编译／发布结果，不补造 Producer Accepted。
