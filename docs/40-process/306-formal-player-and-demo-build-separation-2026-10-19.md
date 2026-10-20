# 正式试玩包与历史 Demo 分离（2026-10-19）

状态：**Implemented / 制作人确认打包与运行、打包后基本存读档无问题 / 构建隔离其余分项待验 / Committed·Sealed（2026-10-20）**。2026-10-19 制作人先反馈“可以正常打包，可以正常运行”，随后反馈“验收了，打包后的存档感觉没有什么问题”。据此将实际出包、启动及打包后基本存读档记为该反馈范围内 Producer Accepted；Formal／Demo 各自的工程外路径、作弊工具和内容隔离、旧 Editor／Demo 槽位未变化仍无逐项结果，不从“感觉没问题”外推。民生业务另由制作人确认整套验收，见 267 唯一状态表。本轮经制作人“按建议做，Demo和正式版本拆开”授权，收口正式/历史Demo构建入口、正式内容随包加载、存档隔离与普通会话菜单；必要的Build Settings调整属于本轮授权范围，其他ProjectSettings/Packages保持。

正式试玩仍使用 `Assets/Scenes/LevelTester.unity`、现有 `scenario_ch01_reference` 与 `roster_level_tester`，不把参考开局冒称为已经完成的正式剧情/经济平衡。历史 `Demo_v0_1`、`DemoParityHost` 及其配置保留，通过独立构建入口出包。人物、美术、Core规则、Content身份/坐标均不变。

制作人已确认现有民生整套范围验收通过，状态见 267 唯一状态表；下方民生检查保留为原计划目录，有单一问题复现时按当前真实床位规则定向返修，不再次要求完整复验。Medic、装备/制造与跨Site物流不在本轮实现范围。普通任务不提交或推送。

## 两套独立构建

| 项目 | 正式试玩 Formal | 历史 Demo |
| --- | --- | --- |
| Unity菜单 | `XianXia/Build/Formal Windows x64` | `XianXia/Build/Historical Demo Windows x64` |
| 命令行executeMethod | `XianXia.Unity.EditorTools.PlayerBuildProfiles.BuildFormalWindows` | `XianXia.Unity.EditorTools.PlayerBuildProfiles.BuildDemoWindows` |
| 场景 | 仅LevelTester | 仅Demo_v0_1、DemoParityHost |
| 产品名 | PJCultiva | PJCultiva Demo |
| 目录/程序 | `Builds/Formal/<时间-唯一后缀>/PJCultiva.exe` | `Builds/Demo/<时间-唯一后缀>/PJCultivaDemo.exe` |
| 编译标志 | 不加Demo标志 | 仅本次Build使用PJCULTIVA_DEMO_BUILD |
| 内容 | `PJCultiva_Data/StreamingAssets/PJCultiva/Formal/BaseGame` | `PJCultivaDemo_Data/StreamingAssets/PJCultiva/Demo/BaseGame` |
| 存档 | `<persistentDataPath>/Saves/Formal/slot0.json` | `<persistentDataPath>/Saves/Demo/slot0.json`（Host存档入口） |
| 开发工具 | 构建场景副本去除LevelTester作弊面板/调试栏，运行时禁止补回或显示 | 保留其原有开发入口，不与正式菜单混用 |

本轮只支持Windows x64。Build Settings默认只列正式LevelTester；历史Demo用专用菜单明确选择，不修改或删除其场景/配置。构建菜单临时设置产品名并在finally恢复Editor原值；每次创建新输出目录，旧包不自动覆盖。构建遇到Play Mode、未保存场景、缺少平台支持、内容或Prefab引用不完整会明确失败，不自动保存当前场景。

构建回调校验场景归属，避免Demo混入Formal或反向混入。Formal构建的场景副本清空Editor内容路径、地图文件/文本覆盖，不写回原场景；保留preferredMapLayoutId并按正式Content定义加载同一地图（当前为Data/Maps/ch01_cave_map.json中的base:map_ch01_cave），避免携带仓库相对路径。直接Build Settings构建同样经过场景和内容回调，但建议始终使用上表专用菜单以获得固定产品名和独立输出目录。

## 内容与资源交付

Editor保持仓库Content/BaseGame默认路径及显式override；Player默认只从随包StreamingAssets加载当前profile的BaseGame。缺包时明确显示失败/重试/退出，不回退开发机路径或偷偷开空世界。

构建只复制manifest.json及其正式Data目录下的JSON，不复制Authoring、_backups、ArtSource、工具、Temp或存档。Resources中的美术和Prefab引用仍由Unity打包；构建前同时核对Editor Prefab与Player引用表，不能以AssetDatabase可用冒充Player引用齐全。

随exe输出build-identity.json，记录profile、Unity版本、Content版本、场景清单、UTC与每个JSON的SHA-256。该文件仅在内容复制完成后生成，不能以一份内容目录或该脚本存在冒充实际成功的exe。当前BaseGame版本仍为0.0.4-world-expand-04，Snapshot仍为v15；本轮不借出包更改Content业务数据或Snapshot格式。

## 正常会话菜单与存档兼容

正式Player从顶栏“游戏菜单”或无其它模态/文字输入时Esc打开；使用具名模态暂停与输入owner，不改手动暂停或时间倍率。提供继续、保存、读取、新开局、退出。读取/新开局/退出需确认放弃未保存进度，已有存档不自动删除。保存仍调用既有正式Capture，读取仍调用原restore/rehydration/presentation链，不增加第二套Snapshot业务。

保存先写同目录.pending，再首次Move或Replace正式槽位；被替换版本留作.previous。失败保留原槽位，旧schema/内容不兼容继续由既有显式错误处理，不自动重置。新开局重载当前正式场景但不覆盖存档。

Editor继续使用原persistentDataPath/vs04_slot0.json，避免本轮把既有开发存档藏到新目录；Player使用profile子目录和独立产品名。旧文件不自动搬迁、加载或删除；若需导入旧存档，应另行明确操作并接受现有兼容校验。Demo/正式/Editor不会互相自动读取。

## 当前配置与后续内容边界

正式试玩目前是当前完整玩法的交付基线，不代表正式剧情/平衡已经完成。仍用scenario_ch01_reference、roster_level_tester、3800×1700世界和当前经济/日程/倍率；开局钱包10000/5/2及测试任务均保留。后续20～30分钟体验小段只作下一阶段规划：用现有劳役/修炼/同伴/战斗与接管做有准备、有收益变化的闭环；需先完成本轮实际出包与民生验收，再单独确认内容目标，不在本轮擅改任务、奖励或修炼速度。

## 最短人工验收表

准备类操作在Editor LevelTester用已有工具完成；正式包不提供作弊夹具。270旧工作单只作操作参考，业务判据以当前267与下表真实床位规则为准。

| 检查 | 具体操作与通过标准 | 当前结果 |
| --- | --- | --- |
| 独立启动 | 各构建一次Formal/Demo；将整包放到工程外路径，Formal进入连续世界且无作弊工具，Demo只进入历史场景，日志内容路径不指向仓库 | 制作人反馈可正常打包与运行；各 profile、工程外路径及工具隔离仍待逐项记录 |
| 会话与隔离 | Formal新开局→保存→退出进程→重启→读取；核EntityId、位置、床绑定。核Editor旧槽与Demo文件未变化，Formal新开局不删槽位 | 制作人确认打包后基本存读档无问题；EntityId／床绑定及跨profile槽位隔离未逐项反馈，仍待核 |
| 食宿/职业 | 自动从合法储藏取粮→实际扣库存→劳动/收获入实际Site公库→回本人床；核职业切换、无粮/低精力与日程59/60、263/264边界 | 待人工 |
| 救援/床位 | 同势力弥留→唯一救援者与真实BedId预约→走到伤员→搬到床边→Occupant一致；跨旗帜与中断不重复人物/预约。医疗床稳定不算回血治疗 | 待人工 |
| 押送/照料/招降 | 正式捕获→真实Carrier→玩家控制Site中Prison房间/合法牢床→床边交接；床用途或占用冲突明确拒绝；再核食宿、Proper Care、Loyalty及招降权限 | 待人工 |
| 建设与存档 | 正式UI落点→取材只扣一次→搬运/施工→完成；取材、搬运、睡眠、救援、押送/关押及战后返场各在中途保存/读档，稳定ID/预约/资源账目不变 | 待人工 |
| 已封板关联回归 | 只做相关冒烟：参战议政厅/旗的出手与返场、门框/挂牌可见、地图点选、菜单叠开/关闭、八向/卧姿/工具与资源加载；无复现不重开已封板系统 | 待人工 |

每次人工结果记录版本、profile、场景、倍率、操作和实际结果；失败先给单一复现，再决定返修。不能把所有状态继续笼统标“已完成”，也不因某条未验自动重做全部系统。

## 实际验证与剩余限制

全8程序集离线编译通过，测试源码的两条既有warning保留；Formal及PJCULTIVA_DEMO_BUILD的非Editor Host分支分别编译通过。新Scene GUID、正式入口、BaseGame清单、元数据与未改动范围做最少静态核对；没有编写或运行自动测试。

**制作人已确认实际打包、运行、打包后基本存读档，以及现有 CIVILIAN-LIFE-01 整体民生验收；Formal／Demo 的工程外与槽位隔离、存档内 EntityId／床绑定尚未逐项记录。** 开发代理仍未自行启动 Unity、调用 BuildPipeline 或运行自动测试；上述人工结论来自制作人。后续具体平台、资源或视觉问题以人工操作与真实日志为准。

## 主要文件

HostPlayerPaths、HostGameSessionMenu、PlayableHostBootstrap、HostLevelTesterSnapshotOps、HostFormalHud及开发UI显示门禁；Editor/PlayerBuildProfiles和EditorBuildSettings；README、247、267、305、路线图、Devlog与本报告。Core、Data JSON、Demo/正式场景源、美术、Packages均无本轮修改，未Git提交或推送。
