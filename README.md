# 修仙游戏项目（暂定名 XianXia）

> **2026-10-28 当前参数与性能交付：** 主管／巡卫观察半径 **50格**，主管压力上限 **15**；普通日程违规实际被发现每WorldTick加1，否则减1至0，倍速同比、暂停停止，HUD /15；满15追赶近身开战，已触发惩戒不因压力下降自动取消。完整现行开局清单见[425](docs/40-process/425-huangcun-opening-completion-2026-10-27.md)，性能证据和本轮优化见[426](docs/40-process/426-opening-performance-regression-2026-10-28.md)。52.83FPS／400ms尖峰是修前记录；去重复全建筑扫描、增加私人资产缓存、优化空间查询和范围圈已实施，修后接近200FPS仍待人工不限帧复测。下方旧参数按日期保留，现行以本条和425为准。

> **2026-10-28 荒村开局基础补全：** 当前完整功能、规则参数、存档边界与人工验收统一见[425](docs/40-process/425-huangcun-opening-completion-2026-10-27.md)。补有限采矿／矿工、基础丹器加工及药物使用、同伴分工与自动私粮、实际地点配给、全队违规和墙体视线；私人旗帜24格范围自由建造、分房存货，发现秘密修炼／基地后先战人再拆建筑，无告密。NpcAutonomyState白名单补线。内容0.0.48-opening-production-camps／Snapshot v21；Implemented，待人工验收。
> **2026-10-27 室外观察范围：** [423](docs/40-process/423-outdoor-awake-watch-eligibility-2026-10-27.md)按制作人最新要求取消Patrol／Inspect课表门槛：主管／巡卫室外清醒、未打坐时45格观察生效，娱乐、自由活动、前往工作和返床途中均可发现；实际睡眠／修炼或进入房间后停用。实际发现、牢房观察和范围圈共用资格；巡视路线仍按巡视时段走。每秒1压力与解放后停用原压力不变。基础编译通过、现场待验，v21不变。

> **2026-10-27 压力每秒与药田视觉：** [422](docs/40-process/422-secondly-pressure-and-spirit-herb-visual-2026-10-27.md)把当班主管／巡卫45格内发现课表违规改为每WorldTick加1点（1倍速约每秒1点，倍速加快、暂停停止，重叠不叠加）；补农作回执组件白名单与停止清理。药田正式贴图也使用蓝绿冷调，生长／成熟有稀疏静态青蓝灵光，普通农田与成熟周期不变。四程序集编译通过、现场待验，内容0.0.47／v21不变。

> **2026-10-27 主管驻军控制权返修：** [421](docs/40-process/421-supervisor-garrison-motion-authority-fix-2026-10-27.md)确认主管另属单人huangcun_labor_garrison，旧Bootstrap统一创建SquadWorldMotion，导致所有个人日程被跳过。新开局本城个人职责小队保留Squad／守城身份但不建行军站位；现有状态包含主管的闲置驻村编队在下一WorldTick交还个人日程。去主管课表伪执行头标，补实际绑定／控制权诊断。主管作息与选点可专用，底层寻路／碰撞共用。基础编译通过，实际移动待人工验收，内容0.0.47／v21不变。

> **2026-10-27 可编辑巡视点：** [420](docs/40-process/420-editable-patrol-waypoints-2026-10-27.md)／[ADR-0071](docs/40-process/43-decisions/ADR-0071-settlement-patrol-waypoints.md)将巡视改为真实路线：荒村墙内四角与四边中点共8点，巡卫顺序循环、主管随机选点；到点后按点数自动短停。占领后从工作→巡卫排班→巡视点编辑在地图添加、调序、删除或全部清空，编号／连线预览，保存后生效。使用既有v21进度保存路线和人物进度，不改地图／内容0.0.47。基础编译通过、现场待验，未提交推送。

> **2026-10-27 巡视／主管作息与田块返修：** [419](docs/40-process/419-patrol-arrival-supervisor-routine-and-fields-2026-10-27.md)修复驻村双巡卫仍被世界移动小队控制、仅匹配地点ID就认定到岗以及换班残留床位路线；主管每日随机排巡视5时／修炼5时／睡眠8时／娱乐6时，正常睡眠返自己的床，娱乐接真实下棋与休闲。248块农田统一15×12，荒村住宿区下方独立药田移除，保留4块药田；内容0.0.47-patrol-routines-fields／Snapshot v21。需新开局看地图，Implemented / 待人工验收。

> **通用城池巡卫：** 已接“工作 → 巡卫排班”，我方已占领城池可安排本城常驻人员白班00–12／晚班12–24、换班或撤下，支持多人同班，日程随存档保留；荒村新开局两人复用通用日程。原荒村日程仍兼容，旧课表时段记录以[418](docs/40-process/418-general-settlement-patrol-shifts-2026-10-27.md)为准。Implemented / 待人工验收。

> **两巡卫当前排班：** 甲 00:00–12:00 巡视，乙 12:00–24:00 巡视，其余作息错十二小时；正式开局与名册保持两份独立日程绑定。此前 06／18 交班为历史配置，巡视半径仍为 45 格。修改日程数据后需重新载入内容，建议新开局查看。

> **巡视范围最新调整：** 主管与巡卫巡视半径当前试为 45 格，发现、村界值守距离和范围圈共用参数同步；其他压力计时和惩戒规则不变，现有存档可继续。见 ADR-0065／399；20 格与 60 格为此前记录。

> **当前完整交接与 GitHub 上传（2026-10-27）：** [417 荒村牢房、战斗与寻路完整阶段快照](docs/40-process/417-prison-combat-navigation-checkpoint-and-github-handoff-2026-10-27.md)汇总 401～416 与 ADR-0066～0070，覆盖探视照料、破墙营救、监管招募、简化伤势、求饶俘虏、房间面板、寻路追击及战斗工作禁用。当前内容 `0.0.46-huangcun-discipline-unconscious`／Snapshot v21；Implemented / Producer Acceptance Pending，最新追击与寻路仍待现场验收。本次明确授权提交并推送现有 `dev_openworld`，实际结果以 Git 和远端记录为准。下方 400、旧版本与“未提交推送”均为历史阶段说明，当前统一以 417 为准。

> **2026-10-27 追击与战斗工作禁用：** [416](docs/40-process/416-encounter-pursuit-and-work-lock-2026-10-27.md)补战场接管时旧工作／休息／日程移动清理，追击独立于攻击冷却、按实时节奏重寻，导航不依赖身体渲染。耕田、砍树、资源破坏、通用劳动与修炼覆盖菜单、指令和最终执行门禁，拒绝工作前保留当前战斗指令；战斗破墙保留。四程序集离线编译通过，现场追击待验，未启动 Unity／测试、未提交推送。

> **2026-10-27 寻路返修：** [415](docs/40-process/415-character-navigation-recovery-2026-10-27.md)已接有效追击路线复用、倍率无关的碰撞短步、局部绕人、占用落点重选和中途失效重规划。抱持载荷不接独立路线，真正堵路停止并提示，不误触到达交互。房间门口／墙边／农田／抱持通行待现场验收；四程序集离线编译通过，未启动 Unity／测试、未提交推送，v21 不变。

> **2026-10-27 放你一马语义修订：** [414](docs/40-process/414-mercy-without-permanent-withdrawal-2026-10-27.md)／ADR-0070按制作人最新要求覆盖旧Spared规则：放你一马同暂不处置，仅关闭对话，不退出战斗、不锁右键、不显示已获放过；醒来正常续战。索财只限制同次战败再索财，不妨碍后续俘虏／拒绝求饶。未结算旧Spared清理，已索财记录保留、已提交战果不重开。四程序集离线编译通过、现场待验，未启动Unity／测试、未提交推送，v21不变。

> **2026-10-27 抱持与救护返修：** [413](docs/40-process/413-carried-view-alignment-and-custody-rescue-guard-2026-10-27.md)统一抱持显示偏移、独立战场校正／检查与脚底坐标，防止合法搬运误报Aligned=False或视觉偏移写回Tactical。已俘虏／拘押和战场所有权中的人物排除普通救护，旧任务取消不再挪走俘虏，真实床位提交复验；敌方凡人不能借救护夺人放床。四程序集离线编译通过，运行待人工验收，未启动Unity／测试、未提交推送，v21不变。

> **2026-10-27 战场续战、俘虏与房间面板：** [412](docs/40-process/412-battle-recovery-capture-and-room-panel-2026-10-27.md)／ADR-0069已补待结算苏醒续战与结束按钮／提交复验；昏迷敌人右键可近身俘虏，真实一对一抱持，抱持者不能攻击但可移动与原有飞行，其他可控人物可继续战斗。支持放下、失能掉落及真实返场押送；房间本体右键用途菜单退役，左键选中在底部面板按管理权改用途。四程序集离线编译通过，现场待人工验收，未启动Unity／测试、未提交推送，v21不变。

> **2026-10-27 对话布局返修：** [411](docs/40-process/411-dialogue-choice-layout-and-wake-clarification-2026-10-27.md)已将求饶主页面收为五项，索物／索钱进入可返回子页；UGUI和IMGUI按选项与正文自适应高度，长列表宽屏双列，超高才滚动。保留真实HP严格超过50%才苏醒，不恢复100秒计时；截图25/93继续倒地符合现行规则。四程序集离线编译通过，实际显示／点击待人工验收，未启动Unity、未提交推送。

> **2026-10-27 最新战斗操作与战败处置：** [410](docs/40-process/410-rts-attack-and-defeated-mercy-2026-10-27.md)／ADR-0068按制作人明确追加取消独立战斗30%低血自动撤离／投降。清醒敌人红光标右键直攻，实际受击到约1%HP昏迷；倒地菜单继续攻击进入求饶，直接放过／索真实携带物／索真实钱包灵石后放过，或拒绝转弥留，弥留再显式击杀成尸体。放过不治伤、不改势力，清掉未执行捕获请求；同次战败不能重复索取。四程序集离线编译通过，待人工验收，v21不变，未提交推送。通用NPC背包、复杂Separate Space求饶及移接未实施，边界见410。

> **2026-10-27 战场种地返修：** [409](docs/40-process/409-encounter-farm-ownership-and-withdrawal-feedback-2026-10-27.md)补实际参战者农作Begin／持续执行／最终提交和工作动画门禁，覆盖Active至ReadyToEnd；旧农作不能接管战场人物。人物持续显示投降／退出交战／待结算，结束栏列出原因。低血劣势退出是既有317规则，是否取消另待制作人选择，尚未擅改。四程序集离线编译通过，待人工验收。

> **2026-10-27 最新伤势实现：** [408](docs/40-process/408-simplified-injuries-and-remains-grafting-2026-10-27.md)／ADR-0067已接简化割伤、重伤、缺肢、经脉断裂与真实治疗恢复；不做疼痛／出血／止血。固定100秒苏醒退役，改实际HP超过50%；普通药不修复残肢／经脉。金丹遗骸移接仅设计、未实施。Snapshot现v21，支持旧v15～v20 JSON读取迁移；内容仍0.0.46。四程序集离线编译通过，运行待人工验收，未启动Unity／测试、未提交推送。

> **2026-10-27 身体流程接线复核：** [407](docs/40-process/407-unconscious-combat-lifecycle-wiring-audit-2026-10-27.md) 修复失能时旧床休息／恢复指令残留、结算后旧锁敌自动补刀、战场显式单次补刀接近被拒绝、途中身体状态变化误升级处决、苏醒站位覆盖战斗权威及旧精灵倒地躺姿。既有一 HP 昏迷、倍速苏醒、弥留和战后身体保留规则不变；四程序集离线编译通过，待人工验收；不改内容版本／Snapshot v20，未提交推送。

> **当前统一交接（2026-10-27）：** [400 荒村当前完整阶段快照与 GitHub 交接](docs/40-process/400-huangcun-current-checkpoint-and-github-handoff-2026-10-27.md) 汇总 394 生存首版与 395～399 的全部比例、布局、田块周期、树矿、移动、巡查惩戒、昏迷救护、范围圈和待验事项。当前 `0.0.46-huangcun-discipline-unconscious` / Snapshot v20，Implemented / Producer Acceptance Pending。制作人明确授权本轮文档化、commit＋push；结果以 Git 记录为准，不等同体验封板或自动开启下一轮。下方旧版本、范围和“未提交”均为历史阶段记录，当前以 400 为准。

> **巡查链路复核与范围圈：** [399](docs/40-process/399-huangcun-discipline-chain-audit-and-watch-rings.md) 确认主管压力随倍速和暂停，补追赶被普通活动覆盖、停在旧目标点和床上目标寻路的接线问题；在岗人员显示二十格金圈，追赶红圈，休班／失能隐藏。沿用 `0.0.46` / Snapshot v20，可继续现有存档；四程序集基础编译通过，实际完整流程待人工验收，未启动 Unity／自动测试，未提交推送。

> **当前荒村巡查与身体流程：** [398](docs/40-process/398-huangcun-discipline-and-unconscious-combat.md) / [ADR-0065](docs/40-process/43-decisions/ADR-0065-huangcun-discipline-and-unconscious-combat.md)。巡卫昼夜交班、二十格观察违规五秒增一点压力、满值真实接近并发起正常惩戒战斗；首败一 HP 昏迷，默认一百秒可苏醒，再受攻击转弥留，弥留需右键显式杀死，战后保留真实状态。昏迷／弥留均接医疗和住宅床搬运。`0.0.46-huangcun-discipline-unconscious` / Snapshot v20，须新开局；四程序集基础编译通过，未启动 Unity 或自动测试，待人工验收，未提交推送。本轮未改地图布局，不表示其他旧待办或全部体验封板。

> **荒村村内疏树追加：** [397](docs/40-process/397-thin-room-walls-resources-and-movement.md) 已从城外移回八棵现有中树，分散在村内空地，保留两倍大小并避开房门、中央道路及田地；其他城市、建筑与资源总数不变。当前 `0.0.45-huangcun-sparse-trees`，须新开局，发布及验收状态见 397。下方 0.0.44 的“城内资源全移城外”在荒村由此覆写。

> **当前房墙、资源和移动：** [397](docs/40-process/397-thin-room-walls-resources-and-movement.md) / [ADR-0064](docs/40-process/43-decisions/ADR-0064-thin-room-walls-larger-resources-and-movement.md)。房间外框仍两倍，房墙恢复与城墙一格等厚；1944 树／250 矿按 0.0.43 长宽各乘二并疏排至城外，保留资源数量与城市建筑位置。步行当前 1.5 倍、飞行为新步行两倍，参数在 world_spatial_rules.json。内容 `0.0.44-thin-walls-resources-movement` / Snapshot v19，须新开局；基础编译通过，发布状态见 397，待人工验收，未提交推送。下方 396 的资源大小与房墙厚度由本轮覆盖，粮草周期仍保留。

> **当前尺寸与作物周期：** [396](docs/40-process/396-world-building-scale-and-crop-cycles.md) / [ADR-0063](docs/40-process/43-decisions/ADR-0063-world-building-scale-and-basic-crop-cycles.md)。所有 66 城建筑、房间及家具实际长宽统一为最初两倍；248 块农田为 12×8、181 块药田为 15×12，普通粮食五日、基础药草十日成熟，照料不直接催熟。荒村保留 12 间普通住房、邻田与主管巡卫设施权限。版本 `0.0.43-world-scale-and-crop-cycles` / Snapshot v19，须新开局；基础编译完成，正式发布状态见 396，待制作人人工验收，未提交推送。下方 395 的 1.5 倍及仅荒村范围为上一轮记录。

> **2026-10-26 荒村比例与布局最新轮次：** [395](docs/40-process/395-huangcun-furniture-scale-and-layout-2026-10-26.md) / [ADR-0062](docs/40-process/43-decisions/ADR-0062-huangcun-physical-furniture-scale.md)。人物显示两倍，荒村房间／床／家具实际长宽 1.5 倍；普通住房按 10 人＋2 间空房保留，扩大农田药田并紧邻住宿，三处修行设施集中到主管巡卫区且占领前限其使用、解放后开放。其他城市既有布局不改。`0.0.42-huangcun-neighborhood` / Snapshot v19，须新开局；正式发布与基础编译完成，待制作人人工验收，未提交。下方 394 是本轮之前的生存阶段快照。

> **当前统一交接（2026-10-26）：** [394 荒村生存首版阶段快照与剩余工作](docs/40-process/394-huangcun-survival-checkpoint-and-backlog-2026-10-26.md)。当前 `0.0.40-huangcun-survival` / Snapshot v19，需新开局。可选帮助、两座炉具、生存／营地／同伴营救首版已实现；完整实玩、节奏与存读档验收尚未完成，不是体验封板。本次按制作人明确要求整理文档并提交上传；Git 结果以提交和远端记录为准。下方版本、授权与“未提交”是各轮发生时的历史记录，当前状态以 394 为准。

> **2026-10-26 荒村生存首版已接线：** [393](docs/40-process/393-huangcun-survival-and-hidden-camp-design-2026-10-26.md) / [ADR-0061](docs/40-process/43-decisions/ADR-0061-huangcun-survival-camp-and-captive-companion.md)。新开局两人同行、第三人被囚；维生配给、额外短工／采集、私人营地、送粮送药与三种营救接入现有时间／伤势／存档。内容 `0.0.40-huangcun-survival`，Snapshot v19，须新开局；左下角“荒村生活 / 同伴营救”进入。Implemented / 待人工验收，无强制教程；复杂追捕、自由营地扩建和完整制作暂不做，不能把基础编译当作体验验收。未提交推送。

> **2026-10-26 荒村炉具与循环复核：** [392](docs/40-process/392-huangcun-furnaces-and-loop-audit-2026-10-26.md) 补丹室炼丹炉、炼器室炼器炉，制作玩法仍按要求暂缓；同时逐项对照当前可玩循环与原预期。自由成长／真实夺村主干已有，劳动压力内容反馈、秘密修炼后果、资源用途及自然成长／恢复体验仍有缺口或待人工验收，不因补提示而宣称全部完成。

> **2026-10-26 荒村可选帮助：** [391](docs/40-process/391-huangcun-practical-opening-guidance-2026-10-26.md) 已补修炼、生活和夺村的只读状态指引，默认收起；所有教学可忽略，不新增强制教程或玩法门槛。内容 `0.0.38-optional-huangcun-guidance`，基础编译通过，正常开局节奏及恢复体验待人工验收，未提交推送。

> **当前整轮交接（2026-10-26）：** [390 世界地图汇总与荒村下一轮](docs/40-process/390-world-checkpoint-and-huangcun-next-round-2026-10-26.md)。内容 `0.0.37-two-compact-sects`，66 城／14 无主，地图、河网、势力与 UI 修订已实现；运行验收仍待制作人进行。本次按明确要求整理并提交上传；下一轮只聚焦“占领荒村议政厅之前”的新手闭环，野外扩张暂缓。下方逐轮记录为历史，不用旧城数或旧盆地尺寸覆盖当前汇总。

> **2026-10-26 两宗增补与地图阶段收尾：** [388](docs/40-process/388-two-compact-sects-and-next-step-2026-10-26.md) 新增天麓宗、听雨宗，各占相邻 4 城，主城分别为镇魔北关／雷南关。全图仍 66 城，无主降至 14，其他势力不变；内容 `0.0.37-two-compact-sects` 须新开局。已发布并静态核对，待人工验收，未封板。下一步建议先验收地图／性能，再打磨荒村周边可玩流程，不自动开展新阶段。

> **2026-10-26 四势力归属收拢：** [387](docs/40-process/387-compact-faction-ownership-2026-10-26.md) 镇岳集中高原及北侧、朔风集中雪原、青霄集中盆地、合欢集中东西阵盆地与西北邻城；数量仍为 7／7／5／5，合欢主城改西阵镇。只调整 14 城归属，地形／城址／导航不变。`0.0.36-compact-faction-ownership` 须新开局，待人工验收；下方 386 的合欢主城与分配为旧版。

> **2026-10-26 河网与势力图例交互：** [386](docs/40-process/386-river-network-and-faction-navigation-2026-10-26.md) 已发布组合河网及东部大河北延（8 段河、13 桥），图例悬停高亮所属城镇、双击聚焦主城。镇岳 7 城、合欢 5 城、无主 22 城，总数 66 不变。内容 `0.0.35-river-network` 须新开局；四程序集基础编译通过，待制作人人工验收。

> **2026-10-26 关口接缝与性能返修：** [384](docs/40-process/384-gate-seams-and-runtime-cost-2026-10-26.md) 修补全部 20 座关城的山墙接缝，取消城外宽切口并处理转角门道；城市位置、尺寸、扩建框及飞行规则不变。内容 `0.0.34-gate-seams` 须新开局；互动点空间索引、睡眠查询和 HUD Layout 开销已优化，四程序集基础编译通过，通行与实际 FPS 收益待制作人人工验收。

> **2026-10-26 风暴／雷暴最新覆写：** [383](docs/40-process/383-storm-thunder-gates-2026-10-26.md) 将风暴城迁至东侧红框位置，扩展飓风／雷暴区域并消除中间平原缺口；新增雷西关、雷南关，雷暴山圈只留两处穿城入口。共 66 城，其他 63 个既有城镇位置不变，尺寸与飞行速度不改。内容 `0.0.33-storm-thunder-gates` 须新开局，发布与验收状态见 383。

> **2026-10-26 局部地理最新覆写：** [382](docs/40-process/382-basin-seals-and-tongxi-relocation-2026-10-26.md) 封闭西阵／东阵与南赢两处盆地的山口；南赢镇及盆地整体南移 360 格，桐溪镇迁到魔兽山脉与极寒雪原之间。其余 62 城、城镇尺寸和飞行速度不变，仍共 64 城。内容版本 `0.0.32-basin-seals-town-relocation`，须新开局；发布与验收状态见 382。下文两城旧坐标及旧盆地入口为历史记录。

> **2026-10-26 最新地图覆写：** [381](docs/40-process/381-round-enclave-and-beast-gates-2026-10-26.md) 按制作人确认，将荒村改为约 **960 格直径自然圆形盆地**；魔兽山脉外围连续山带、东／西／南／北四座新增中立关城及西侧接桥，合计 **64 城**。全图仍为 18450×8700，原 60 城、建筑和五级扩建尺寸不动，飞行速度不改。内容版本 `0.0.31-round-basin-beast-gates`，需新开局，发布结果与人工验收状态见 381。下方 720 方形、60 城及旧版本记录均为此前阶段。

> **2026-10-26 荒村盆地收紧：** [380](docs/40-process/380-world-compaction-and-presentation-fixes-2026-10-26.md) 最新覆写为 **720×720 格可探索范围**，山带外置，完整保留五级 540×540 及每边 90 格余量。城心／全图／其他城镇不动，洞府和资源原位保留，仅迁移两端灵阵。内容包 `0.0.30-huangcun-basin-720` 已发布，须新开局，Unity 通行及视觉待人工验收；下文 0.0.29 为历史版本。

> **2026-10-26 空地压缩与表现返修：** [380](docs/40-process/380-world-compaction-and-presentation-fixes-2026-10-26.md) 已将主世界压至 **18450×8700 格**（面积减少约 21.8%），60 城、城镇实体及五级扩建尺寸不变，最小城框间隙仍为 48 格；小盆地及关口邻域保护，飞行／旅行速度未改。北部角色深度排序修复、床位／床边出生修复保留，并优化睡眠表现查询。内容包 `0.0.29-compact-world` 须新开局；运行三件套已发布，基础编译完成，通行／视觉及性能收益待制作人复测。下文 21600×9500 与严格统一等比为此前阶段。

> **2026-10-26 当前正式世界地理：** [378 定稿实施](docs/40-process/378-final-world-geography-gate-handoff-2026-10-26.md)与 [ADR-0060](docs/40-process/43-decisions/ADR-0060-final-world-geography-and-site-retirement.md) 已按定稿图实际 **60 城**统一等比复原城址、地貌与关口；八个人类宗门不变，总数包含朔风堡与无主城。旧 108 个 Site 中 56 保留、52 退役，新增 4 城。主世界 21600×9500 格，内容包 `0.0.28-world-map-exact` 须新开局；基础编译完成，Unity 人工验收待制作人进行。下文 108 城／16 宗属此前阶段。

> 当前实施以 [357 金丹精力与城镇实体范围](docs/40-process/357-golden-core-energy-and-town-footprints-2026-10-25.md)为准：等级 1～5 的控制范围长宽为 180／270／360／450／540 格；108 座固定城镇实体和外墙已按当前等级调整，荒村为 180×180 格。金丹及以上日常与打坐不再扣精力。内容包 `0.0.23-town-footprints-080` 需新开局；**Implemented / Producer Acceptance Pending**，世界图偶发旅行取消仍待现场追踪。下文为此前阶段记录。

> 最新：[350 城镇墙体与石屋／Chunk 接缝返修](docs/40-process/350-town-walls-stone-room-art-and-chunk-seams-2026-10-25.md)。青石与其余 79 处原无墙据点补石墙并留门，108 处固定 Site 均有墙；石屋改用独立石墙贴图，草地 Chunk 边缘重新铺齐。内容包 `0.0.21-town-walls-and-art` 需新开局；基础编译通过，Unity 视觉／通行／帧时待制作人人工验收，未提交推送。

> 最新：[348 自然错落的城镇与青石街区](docs/40-process/348-natural-town-layout-and-qingshi-urban-quarter-2026-10-25.md)。108 座城镇在 15200×6800 世界内改为不等距错列，五级范围最近仍隔 96 格；青石镇改为 240×210 格的正交街区，补石制公共建筑、沿街铺居和边缘田圃；其余 106 处补 258 间沿街房。内容包 `0.0.20-natural-towns`，旧档需新开局；Unity 人工验收待制作人进行，未提交推送。

> 最新：[347 五级城镇分离与世界扩图](docs/40-process/347-five-level-town-separation-implementation-2026-10-25.md)。同宗及异宗的 108 处据点已在 15200×6800 格世界重排，全部升到五级后最近两座行政方框仍隔 175 格；道路、蓝图和锚点同步迁移。内容版本 `0.0.19-separated-world` 需新开局。基础编译／静态检查后仍待 Unity 人工验收；未提交推送。

> 最新：[345 固定议政厅据点实施](docs/40-process/345-fixed-sites-and-town-levels-architecture-change-2026-10-25.md)。102 旧旗据点改为固定可占领议政厅，荒村一级、青石与 102 据点三级，五级范围／升级费用已接线；青石街区扩展并铺正交道路，内容版本 `0.0.18-fixed-council-sites` 需新开局。基础编译与静态完整性通过，Unity 人工验收待制作人进行；未提交推送。

> 最新：[342 世界聚落与公开传承](docs/40-process/342-world-settlements-and-public-techniques-2026-10-24.md)。108处现有据点已有街区，含16宗门主城商业NPC；新增16功法／16斗技／32书册，石木功能房与树木外观变体。版本 `0.0.17-world-settlements` 需新开局；运行待人工验收，未提交推送。

> 最新：[341 荒村四区调整](docs/40-process/341-huangcun-quadrants-resources-and-workrooms-2026-10-24.md)。田地紧排、住房分区、室外棋盘、两倍散生树、铁铜银灵散矿与低级木制功能房已配置。版本 `0.0.16-huangcun-quadrants` 需新开局；基础编译／静态引用通过，运行待人工验收，未提交推送。

> 最新：[340 荒村生活与聚落内容](docs/40-process/340-huangcun-daily-life-and-settlement-content-2026-10-24.md)。开局引导释义、巡卫／主管规律作息、近田药圃、树林／修炼院／医疗床、青石生活设施与8委托已实施；版本 `0.0.15-huangcun-daily-life` 需新开局。四程序集基础编译和内容静态校验通过，运行待人工验收，未提交推送。制作人确认此前卡顿基本解决，200 FPS仍未实测确认。

> 2026-10-24主控与性能返修见[339](docs/40-process/339-pointer-facing-and-movement-performance-2026-10-24.md)：制作人最新13:46 F9报告平均133.40 FPS，全程未暂停，120不是限帧值。继续向200 FPS预算优化覆盖／床边重复查询、临时分配与隐藏HUD点击区；新增Ctrl+F9临时不限帧对照，关闭F9恢复原设置。基础编译通过，追加收益与200 FPS待新采样，完整交互待验；未提交推送。当前宗门密度暂保留，位置精调后续另做。

> 最新领地：[338 十六宗门正式势力范围](docs/40-process/338-sixteen-sect-territories-2026-10-24.md)。16宗领地已配置，新增90旗，世界与荒村尺寸不变；内容0.0.14，需要新开局。城内设施／驻军仍待补，运行待验收，未提交推送。

> 最新：[337 金丹化翼实施与验收](docs/40-process/337-golden-core-flight-design-2026-10-24.md)。制作人已批准框架扩展；金丹突破、F3飞行／轻量翅膀、空地75%闪避及Snapshot v18已接线，内容0.0.13-golden-core-flight。基础编译通过，运行待人工验收，未提交／推送；F2仍为独立远程模式。

> 最新操作见[336](docs/40-process/336-directional-ranged-control-and-polish-2026-10-24.md)：筑基F2纱衣后，WASD移动、鼠标瞄准、长按左键定向普攻，命中才伤害；骨架瞄准与UI互斥已接线，基础编译通过，待人工验收，未提交推送。

> 宗门打法最新见[335](docs/40-process/335-sixteen-sect-combat-identities-2026-10-24.md)：16套功法／斗技补齐攻防血量、回血、连击冷却与修炼难度取舍。版本0.0.12-sect-combat-identities；已配置，待人工验收；普攻加速及元素特殊机制待框架批准。未提交推送。

> 2026-10-24最新布局见[334](docs/40-process/334-compact-faction-territories-and-single-core-huangcun-2026-10-24.md)：同宗据点按约280格邻接收拢，荒村重排为220×200，全部村内物件均在自身议政厅225×225范围内；原补范围旗移至独立的衡河西岸驿。同步林间实物、出生床位、道路与宗门候选。版本`0.0.11-compact-territories`，需新开局；框架未改，运行待验，未提交推送。

> 2026-10-24最新内容见[333](docs/40-process/333-small-village-and-sixteen-sects-content-2026-10-24.md)与[十六宗门目录](docs/30-content/16-sect-world-content-catalog.md)：正式名册20人分布为荒村13／青石镇7，新增16套入门功法与斗技、32书册、青石书院购书与4项交付换书。正式内容现22任务／67生活事件／6机会模板，版本`0.0.10-small-village-sect-books`，需新开局。16宗城与附属节点为作者候选，高阶人物和专属机制仅设计；未扩框架，待人工验收，未提交推送。

> 2026-10-23最新追加见[332](docs/40-process/332-huangcun-confined-opening-and-liberation-2026-10-23.md)：正式荒村开局在真实占领前限制离村、暂停外界自主模拟，占领后永久解锁外界、势力入口与建造。正式内容现20人／18任务／54生活事件／6机会模板，复用原资源和同屏上限；版本0.0.9-huangcun-liberation，需新开局。当前已实现、待人工验收，未提交推送。

单人开发的修仙题材游戏。Unity + Cursor。本仓库同时存放**策划文档**与**游戏工程**，用 Git 实现跨设备开发与开发史留存。

> **2026-10-27 牢房当前入口：** [402](docs/40-process/402-prisoner-person-interaction-and-trespass-2026-10-27.md) 已改为进入牢房后右键具体囚犯，房间／门／牢床不再打开人物对白。占领前被在岗巡视实际发现即追赶开战；占领后可继续找未释放同伴照料或下令释放。清醒囚犯只在所属牢房内活动，床位保留容量；401的任务、送饭送药和逐渐恢复保留，修炼丹药延期。沿用内容 0.0.46／Snapshot v20；基础编译与运行验收记录见402，未提交推送。

## 当前阶段

> **2026-10-27 牢房整链复核返修：** [406](docs/40-process/406-prison-flow-wiring-audit-2026-10-27.md) 已核对探视／照料、宣战破墙、占领后监管与归附；修复易主时拘押被改写、失去牢房后仍显示释放、出口移动被日程覆盖、监管坏目标阻塞、实际受击跨日漏记，以及获释伤员床与恢复记录不一致。四程序集基础编译通过，仍待人工实玩与存读档验收，未提交推送。

> **2026-10-27 占领后监管与招募：** [405](docs/40-process/405-prison-warden-care-and-recruitment-2026-10-27.md)／ADR-0066 已接工作／人事中的监管职业，实际从储藏室取送公粮、灵草；囚犯不再抽象吃公粮或每天掉原势力忠诚，改按实际照料逐日降低抵抗。进入牢房右键本人对话查看照料、身体并劝说归附，成功解除拘押／加入玩家势力但不入队、不移动、不治疗。同伴乙直接释放，无招募门槛。四程序集基础编译通过，运行与存读档待人工验收，未提交推送。

> **2026-10-27 监狱破墙营救：** [404](docs/40-process/404-prison-wall-breach-rescue-2026-10-27.md) 已移除撬锁和双巡卫失能点击强救，改为正常宣战、独立战场内攻击真实监狱墙体，导航确认可通行缺口后就地解除拘押。兼容攻击主管／议政厅时同场破墙；保留位置、伤势、药量和昏迷，不自动入队或占领。占领后对人下令释放及赎人保留。四程序集基础编译通过，待人工验收；未提交推送。

> **2026-10-27 违规分级与床休息返修：** [403](docs/40-process/403-offense-tiers-prison-buffer-and-bed-rest-fix-2026-10-27.md) 保留普通日程违规累计压力，将牢房及外沿三格内被在岗巡视看到接为直接追赶；修复床边开始休息后被到达收尾Stop取消，以及1.5表现距离误当Core使用格的问题。其他直接开战情形仅设计候选。四程序集基础编译通过，运行待人工验收，未提交推送；仍为0.0.46／v20。

2026-10-23 换电脑续做入口：[328 暂停检查点与完整续做清单](docs/40-process/328-paused-work-cross-computer-handoff-2026-10-23.md)。本次备份包含扩图／整数格／木墙、编辑器保存修复、运行性能与 NPC 修复，以及荒村首段内容首稿；当前版本 `0.0.8-huangcun-living-opening`。正式绑定已切换，但旧验收内容隔离、将老分支修正、最终引用检查与统一编译尚未完成。检查点提交不代表全项目审查结束或制作人人工验收；新电脑需从源码重新发布外置编辑器。

2026-10-23 最新荒村布局见 [322](docs/40-process/322-huangcun-compact-housing-and-cell-wood-walls-2026-10-23.md)：住宿横向隔 1 格、排间隔 3 格，房床及住户锚点同步；240×200 村界新增 844 段 1×1 木墙，四面各留 8 格通路。新增轻量木墙 prefab，木墙建造模板也为 1×1；当前内容版本 `0.0.7-huangcun-wood-walls` 覆盖下文版本。基础编译通过，需新开局验收通行／破坏／帧率，未提交。

2026-10-23 最新占地规则见 [321](docs/40-process/321-integer-grid-footprints-and-use-cells-2026-10-23.md)：物件整数格放置，蒲团／棋盘 1×1，人物 1×1 连续移动且美术尺寸独立，棋盘两侧显示真实使用格脚印。当前版本 `0.0.6-grid-footprints`，覆盖下文扩图版本及“保留旧物件宽高”表述；世界、控制范围及道路河流宽度不变。已实现，需新开局人工验收，未提交。

2026-10-23 世界与荒村扩展见 [320](docs/40-process/320-world-expand-05-7600x3400-2026-10-23.md)：世界 **7600×3400**、荒村 **240×200**，一级议政厅／势力旗控制范围 **225×225**；道路河流与建筑家具保持原尺寸。已实现，待新开局人工验收；内容版本 `0.0.5-world-expand-05`，旧档保留、不自动迁移。未提交。

2026-10-21 当前工作区获制作人明确封板上传授权：[312医疗](docs/40-process/312-medic-bedside-treatment-2026-10-20.md)、[313医疗收尾与地面物品](docs/40-process/313-medical-closure-and-ground-loot-2026-10-20.md)、苏醒返工及洞府地面排序/spaceKind文件加载修复统一提交上传。8程序集离线编译通过；未逐项确认的Unity视觉与边界验收保持原状态，下文“未提交”为此前实施时点记录。

2026-10-20 医疗收尾与地面物品显示见[313](docs/40-process/313-medical-closure-and-ground-loot-2026-10-20.md)：卧床恢复精力、旧档零血囚犯补齐、携药转职归还/死亡落地，以及洞府功法/斗技按物品显示已接线，待现场验收。当前Snapshot为v17，正常读取v15/v16补齐，不覆盖旧档；下面v16记录为医疗初次实施时点。未提交。

2026-10-20 医者床边治疗已接线、待制作人现场验收，见[312](docs/40-process/312-medic-bedside-treatment-2026-10-20.md)与[ADR-0048](docs/40-process/43-decisions/ADR-0048-medical-recovery-and-carried-medicine.md)。公库/附近Party供药、持续药效、中级医疗床减缓弥留、HP超过50%苏醒及Snapshot v16已实施；新开局阿禾、阿杏、阿枝为医者。此前“Medic延期/医疗床永久稳定”以本条与267现行覆写为准，未提交。

2026-10-20 世界物件选中反馈见 [307](docs/40-process/307-world-object-selection-feedback-2026-10-20.md)：房间蓝色 Gizmos 调试圈已替换为正式选中细框，床、门、木墙等按各自范围显示；离线编译通过，待制作人现场验收，未提交。

2026-10-19 当前推进：[306 正式试玩与历史Demo构建分离](docs/40-process/306-formal-player-and-demo-build-separation-2026-10-19.md)。正式Windows入口为 `XianXia/Build/Formal Windows x64`（仅LevelTester），历史Demo使用独立菜单/产品名/输出/存档；内容随包加载与普通会话菜单已接线。制作人确认可打包、运行且打包后基本存读档未见问题；Formal／Demo／Editor 槽位隔离未逐项记录。[CIVILIAN-LIFE-01](docs/40-process/267-civilian-life-01-design-freeze-2026-09-27.md#当前功能与验收状态表唯一维护点2026-10-19-制作人反馈覆写)已获制作人整体人工验收，尚未封板；Medic 等延期功能不在范围。本轮没有改变参考开局经济或开始新玩法。以下历史段落以247最新覆写、267唯一状态表和306为准。

**Architecture Freeze v0.2＋ADR-0038 最终冻结。** Continuous Outdoor、统一 Squad／CharacterEncounter、Actual Administrative Control、MAP-01～04、SPACE-01、LEGACY-FINAL-A／B／C，以及 Hex／Army 正式运行依赖退役与 021915 统一收尾均已完成制作人验收并封板。封板提交为 `9b32fe0`。正常 Outdoor authority、合法 compatibility 边界和新会话唯一恢复摘要见 [247 交接](docs/40-process/247-project-handoff-current-state-2026-09-18.md)。
Unity 版本锁定 **2022.3.6f1 Built-in**（ADR-0001）。

2026-10-19 当前工作区剩余改动获制作人明确“全部封板提交并上传”授权。势力旗反击、WorldMap 图标缩放及窗口输入隔离按制作人已确认范围记 Producer Accepted；提交和推送不代表 Medic、其他系统残项或未现场验收的美术已完成。

系统残项与现有美术接线最新静态核对见 [305](docs/40-process/305-system-and-art-closure-audit-2026-10-19.md)；已补齐玩家／渔村两种现成旗帜 View，其他待验与真缺口分开记录，不代表全项目系统或美术已封板。

2026-10-19 势力旗验收覆写：制作人已确认独立战场反击功能验收通过；本轮状态为 Implemented / Producer Accepted / 未封板，尚未提交或推送。实施时的待验收描述以本条和 [ADR-0046](docs/40-process/43-decisions/ADR-0046-faction-flag-encounter-defense.md) 当前状态为准。

2026-10-19 势力旗后续扩展：已按制作人确认接入与议政厅同规则的独立战场远程攻击，无守军旗单独受攻也进入独立战场；当前为 Implemented / Producer Acceptance Pending，尚未提交，未进行 Unity 现场验收。见 [ADR-0046](docs/40-process/43-decisions/ADR-0046-faction-flag-encounter-defense.md)。

2026-10-19 最新覆写：议政厅独立战斗攻击及弹道、巡卫战后按入战位置返场、人物骨架与床层的两处 Sprite 顶点越界修复均已由制作人在 LevelTester 现场验收；本批获明确封板推送授权。议政厅正式规则见 [战斗 §12.2.1](docs/20-systems/23-combat.md)、[ADR-0045](docs/40-process/43-decisions/ADR-0045-council-hall-encounter-defense-authority.md)，本批状态与范围见 [247 最新交接](docs/40-process/247-project-handoff-current-state-2026-09-18.md) 及 [开发日志](docs/40-process/42-devlog.md)。下方“议政厅尚未实施／待验”相关段落属于当时阶段，不代表当前状态。

2026-10-18 人物美术与移动的最新实施/待验状态见 [297 当前交接](docs/40-process/297-art-rig-and-source-cleanup-current-handoff-2026-10-18.md)；12 套 Unity 共用骨架 Skin、八向与侧臂、卧姿和右键连续改路已接线，美术源文件清理有独立台账。版本封板不等于制作人人工视觉验收。
制作人复看人物动画后同意推进；当前黄村住宅—道路—田地同屏表现样板的实施与待验入口见 [298](docs/40-process/298-huangcun-environment-visual-slice-2026-10-18.md)。
住宅墙面现场反馈及实木墙、麦田／药田阶段图返修见 [299](docs/40-process/299-solid-wood-wall-and-farm-stage-presentation-2026-10-18.md)。
田地逐格缩小感与议政厅圆盘外观的返修接线见 [300](docs/40-process/300-field-scale-and-council-hall-exterior-2026-10-18.md)，仍待人工验收。
议政厅屋顶符阵已作静态美术接线；自动攻击的规则边界与待定数值见 [301 ACR](docs/40-process/301-acr-council-hall-spirit-defense-2026-10-18.md)，玩法尚未实施。
现有筑基纱衣远程普攻的通用可见弹道返修见 [302](docs/40-process/302-unified-visible-ranged-projectile-presentation-2026-10-19.md)；议政厅待正式参战伤害权威接入后复用。
同伴窄路慢速蹭行的 Host 移动返修及议政厅攻击现状核查见 [303](docs/40-process/303-party-corridor-transit-and-council-hall-attack-status-2026-10-19.md)。
制作人复验后要求人物行走暂时互相穿过；最新共用移动改动见 [304](docs/40-process/304-unblocked-character-transit-2026-10-19.md)，待现场验收。303 的在途避让现由 304 替代；议政厅攻击仍未实施。

远端：https://github.com/Siyuan-Yu/ProjectCultiva

## AI 多会话协作

三类长期工作流（架构／Unity 开发／剧情文案）的职责、同步与变更流程见：

**[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)**

（编号说明：`46` 已用于 Demo 美术资源表，本规范为 **52**。）

## 从哪开始读

新接手这个项目（包括几个月后的自己、别人、或新的 AI 会话），按这个顺序读：

1. `AGENTS.md` — AI 硬约束与当前阶段  
2. `docs/40-process/52-ai-collaboration-protocol.md` — 多会话职责与真源规则  
3. `docs/00-project/00-overview.md` — 最高层大纲与系统索引（**入口**）  
4. `docs/00-project/03-glossary.md` — 术语表  
5. `docs/40-process/247-project-handoff-current-state-2026-09-18.md` — 新会话恢复摘要、系统现状总表与当前 Proposal
6. `docs/30-tech/33-architecture-core-rules-freeze-v0.2.md` — 架构主契约
7. `docs/00-project/04-reading-guide.md` — 按下一议题选择少数正文
8. `docs/40-process/42-devlog.md` — 最近决策与历史理由（无需从头通读）
9. （Demo 参考）`45`～`49` — 原型范围与美术；正式语义以 Freeze／`32` 为准  
10. `docs/10-benchmark/14-borrow-and-differentiate.md` — 借鉴与不做

## 文档结构

```
docs/
├── 00-project/          项目定位与约束
│   ├── 01-vision.md             愿景、目标玩家、成功标准、待确认问题
│   ├── 02-scope-and-constraints.md  现实约束与范围控制原则
│   └── 03-glossary.md           术语表（命名唯一真源）
├── 10-benchmark/        竞品拆解
│   ├── 11-tale-of-immortal.md          鬼谷八荒
│   ├── 12-cultivation-simulator.md     了不起的修仙模拟器
│   ├── 13-comparison.md                横向对照
│   └── 14-borrow-and-differentiate.md  借鉴/差异化/不做
├── 20-systems/          系统设计（一系统一文档）
├── 30-tech/             技术架构
├── 40-process/          研发管理
│   ├── 41-roadmap.md            里程碑与完成标准
│   ├── 42-devlog.md             开发日志（倒序）
│   ├── 43-decisions/            ADR 架构决策记录
│   ├── 51-…-implementation-plan  Core M1 实施计划
│   └── 52-ai-collaboration-protocol.md  多 AI 会话协作规范
└── 90-templates/        模板
```

## 维护规范

这套规范存在的唯一目的：让**三个月后的你**和**接手的人**能在半小时内恢复完整上下文。

1. **文档先于代码**：系统设计文档没定稿，不写该系统的代码。
2. **devlog 必须写**：每次有实质进展就在 `42-devlog.md` 顶部追加一条，重点写"为什么"。
3. **贵的决定写 ADR**：以后改起来会痛的决定（版本、格式、架构边界、引入插件）必须留记录。
4. **命名走术语表**：新概念先登记 `03-glossary.md`，再写代码。
5. **文档头部三件套**：状态、优先级、最后更新日期，改内容就改日期。
6. **提交信息带前缀**：`feat/fix/docs/refactor/data/chore`。
7. **多 AI 会话**：遵守 `52-ai-collaboration-protocol.md`；重要结论必须落文档，禁止只在聊天里定案。
8. **项目文档默认使用中文**：设计、开发流程、架构、验收与 Devlog 正文默认使用中文；类名、方法名、字段名、JSON key、文件路径、DefinitionId 等技术标识保持原文。

## 跨设备开发

```bash
# 首次
git clone <远端地址> && cd XianXia

# 每次开工前
git pull

# 每次收工（只选择本轮相关路径；不要混入其他工作区改动）
git add -- <明确路径>
git diff --cached --check
git commit -m "docs: 更新xxx"
# 仅在制作人明确要求时 push
```

远端仓库：`https://github.com/Siyuan-Yu/ProjectCultiva`
Unity 版本：`2022.3.6f1（Built-in，见 ADR-0001）`
