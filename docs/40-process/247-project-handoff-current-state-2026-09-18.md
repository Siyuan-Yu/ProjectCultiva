# Project Handoff — Continuous World Current State

> **2026-10-29 最新完整交付：** [441](441-farming-and-bed-interaction-github-handoff-2026-10-29.md)承接9a7caa8a，收录438～440全部改动：按站位持锄左键农作、床F直接睡觉、每日照料100%锁定及存读档。明确授权中文文档化并提交上传origin/dev_openworld；地图自动备份两份保留本地，其余本轮正式改动完整提交。基础编译与静态检查通过，现场待人工验收；历史“未提交”由本次交付更新，结果以Git记录为准。

> **2026-10-29 当前农作返修：** [438](438-position-based-manual-farming-2026-10-29.md)覆写435的鼠标选格要求：实际装备锄头、站在真实农田／药田内即可按住左键劳动，无需鼠标对准田格；田外不可隔空作业，不自动走田，提示跟随人物。三个程序集基础编译通过，未运行Unity或自动测试，待人工验收；未提交推送。

> **2026-10-29 最新中文交付汇总：** [437](437-manual-labor-weapons-animation-and-github-handoff-2026-10-29.md)承接已提交774a520f，汇总435／436全部劳动、装备、配方、存读档、统一动作及文案改动。制作人明确授权中文文档化、commit与push至origin/dev_openworld；两份地图备份保留且不上传。内容0.0.49-melee-tools／存档v21，四程序集基础编译通过、运行待人工验收；此前“未提交”为历史状态，本次结果以实际Git记录为准。

> **2026-10-29 当前表现返修：** [436](436-shared-overhead-tool-and-weapon-animation-2026-10-29.md)承接435，将主控／同伴／NPC的工具劳动和持械近战统一为明显举起后向下挥击；补低幅挥锄、空手农作及艺术绑定回退。只改Host表现，四程序集编译通过，未启动Unity／测试，待人工验收，未提交推送。

> **2026-10-28 当前实施：** [435](435-manual-field-work-and-melee-weapons-2026-10-28.md)承接已提交434，主控装备锄头、近田指向田格长按左键劳动，松手／移动停止，原NPC／同伴／RTS自动农作保留。新增七种工具／近战武器、B背包装备卸下、伤害和工具效果、锻造与实际手持；单槽沿原Equipment modifier保存，旧档不重复发工具。内容0.0.49-melee-tools／v21；四程序集基础编译与必要静态检查通过，未启动Unity／测试，未提交推送，两份地图备份保留。现场验收及项目进度／空感评估和后续建议在435，未自动实施后续叙事方向。

> **2026-10-28 最新整轮交付：** [434](434-rpg-controls-supervision-and-github-handoff-2026-10-28.md)为427～433与ADR-0073／0074的完整状态／范围／人工验收汇总，承接425与此前提交0165ed5d。制作人明确授权文档化、commit＋push至origin/dev_openworld，两份本地地图自动备份原样保留且排除。下方各轮“未提交”是实施时历史，最新Git结果以本次交付为准；Implemented／Committed不等于Producer Accepted／Sealed。

> **2026-10-28 最新F交互：** [432](432-rpg-single-target-interaction-hint-2026-10-28.md)取消431目标选择列表，当前有效人物／实际可用物体上方显示具名F提示，F直接原菜单。管理对象、重复住宅工区热点、普通跟随队友及清醒交战敌人不抢占日常提示；身体／照料／战败／原政治权限保留。左键房间检查和核心／旗显式宣战仍可用，未更改地图赶路及RTS开关。Implemented／待人工验收，未提交推送。

> **2026-10-28 最新操作模式：** [431](431-rpg-controls-and-independent-map-travel-2026-10-28.md)已接场景RTS关／地图点击赶路开两个独立本机偏好，顶栏操作设置与地图页开关；主控WASD、战斗左键定向／长按普攻、F近处目标菜单，近处右键和非战斗左键查看保留。旧主控自动追击攻击门禁、近远程冷却交接、墙／核心／旗手动出手及远程交互接近拦截同轮完成；同伴敌人AI、原政治／身体／处置不改，武器延期。Implemented／待人工验收，未启动Unity／自动测试／提交推送，0.0.48／v21不变。

> **2026-10-28 最新暴露退役：** [429](429-retire-personal-exposure-mechanic-2026-10-28.md)／ADR-0074覆盖428仅藏HUD的旧边界，正式运行组件、风险规则、修炼累计与G降值入口已删。旧Snapshot暴露字段仅兼容读、不恢复、不再输出；UseConcealGrass=14为保留退役号，在任何副作用前失败。旧敛息草物品不删除，新开局停旧专用配给。50格／压力15／督工和前轮427／428修改保留，0.0.48／v21不变。四程序集编译通过，人工读档与交互待验，未提交推送。

> **2026-10-28 最新违规督工：** [428](428-violation-notice-and-supervision-follow-2026-10-28.md)／ADR-0073已接HUD明确违规和当前要求、低压发现者跟随督工、实际开始规定活动解除跟随。Core复用追赶人物引用和ContentCounters模式，旧档无标记仍为原惩戒；普通督工不构成惩戒战。50格、压力15及直接执法保留。HUD旧暴露条撤下，Component／存档不删。四程序集编译通过，现场／存读档待人工验收，0.0.48／v21不变，未提交推送。

> **2026-10-28 最新移动观察接线：** [427](427-moving-supervisor-observation-position-2026-10-28.md)定位非凡人个人日程移动缺少中途精确位置提交：圈随 View 移动，Core 观察可能仍读旧位置。已在共享移动脚底捕获中补普通个人位置提交，沿原 World／空间所有权门禁，更新实际发现和室内资格；不新增全实体扫描。50格、压力15和WorldTick频率保留，0.0.48／v21不变。基础编译通过，运行待人工验收，未提交推送。

> **2026-10-28 本次统一交付：** 最新参数为50格观察、压力上限15；被发现普通违规每拍+1，否则-1至0，达到15追赶近身开战，追击已触发后不因压力减少自动撤销。HUD /15，暂停／倍速一致。当前全部开局功能及人工验收入口[425](425-huangcun-opening-completion-2026-10-27.md)；性能回退证据、资产缓存／空间查询／范围圈优化和复测方法见[426](426-opening-performance-regression-2026-10-28.md)。四程序集基础编译通过，性能目标及运行体验待制作人人工验收。制作人已明确授权本轮文档、性能及参数修改提交推送；内容0.0.48／Snapshot v21保持。

> **2026-10-28 最新交接：** [425 荒村开局补全与统一清单](425-huangcun-opening-completion-2026-10-27.md)承接418～424以及本轮私人营地追加。有限矿业与基础加工、同伴分工、近场配给、遮挡及同伴违规、私人旗帜建造与真实拆毁已接入；全部当前开局功能及人工验收以425为入口。内容0.0.48／v21；Implemented / Producer Acceptance Pending。未启动Unity、未运行自动测试。
> **2026-10-27 观察资格最新：** [423](423-outdoor-awake-watch-eligibility-2026-10-27.md)把观察与巡视日程解耦，主管／巡卫室外清醒未修炼即可生效，娱乐／返床途中也有效；实际睡眠、打坐、房内停用。Core共用CanObserveOutdoors供发现／牢房观察及圈显示，旧“只在巡视时观察”覆盖。每秒1点、45格、倍速暂停不改。四程序集编译通过、实际表现待验，未提交推送。

> **2026-10-27 压力／药田：** [422](422-secondly-pressure-and-spirit-herb-visual-2026-10-27.md)当前压力为每个观察到违规的WorldTick加1点，原五Tick累计已覆写，仍45格／随倍速暂停／多人观察不叠加。实际农作Work阶段回执补入组件白名单并在停止时清理；药田正式贴图接蓝绿冷调与稀疏静态灵光。四程序集基础编译通过、实际计数与画面待人工验收，未提交推送。

> **2026-10-27 主管卡住根因：** [421](421-supervisor-garrison-motion-authority-fix-2026-10-27.md)定位正式开局单人主管驻军一直持有世界移动权，419只释放巡卫遗漏主管，420也未解决。已改新开局个人职责小队不创建行军站位，旧状态释放包含主管的本城闲置编队，保留Squad／DefendedSiteId；实际行动优先，课表不伪称已执行。新增SupervisorDutyBinding日志及初始化名册／场景信息。四程序集编译通过，需现场验主管离开议政厅及回床，未提交推送。

> **2026-10-27 巡视点最新：** [420](420-editable-patrol-waypoints-2026-10-27.md)／ADR-0071已接本城路线地图编辑、编号连线、保存与清空；巡卫按序循环，主管随机走点，到位后自动短停。荒村墙内初始8点，清空不自动补回。使用既有v21 ContentCounters保存地点、版本及人物进度，替代旧工区兜底；内容仍0.0.47，原地图无需重新发布。四程序集编译通过、现场待验，未启动Unity／自动测试、未提交推送。

> **2026-10-27 巡卫与田块返修：** 最新[419](419-patrol-arrival-supervisor-routine-and-fields-2026-10-27.md)：驻村巡卫解除无行军任务的世界编队站位控制、保留Squad关系，真实到岗与换班清旧路线；主管每天随机5时巡视／5时修炼／8时睡眠／6时娱乐，抽取随既有进度保存。农田统一15×12、荒村下方独立药田取消（余4块）。内容0.0.47／v21，需新开局，基础编译通过、现场待验，未提交推送。

> **2026-10-27 通用巡卫：** [418](418-general-settlement-patrol-shifts-2026-10-27.md)已接各已占领城池“工作 → 巡卫排班”，真实常驻凡人／修士可安排白班00–12、晚班12–24或撤下；沿用日程存档与本城路线，荒村复用，巡视圈45格。未增加战斗／治安规则，不改Snapshot v21。基础编译通过，现场换班待验收，未提交推送。

> **当前完整交接与提交上传（2026-10-27）：** [417](417-prison-combat-navigation-checkpoint-and-github-handoff-2026-10-27.md)接替 400 为当前总交接，汇总 401～416 与 ADR-0066～0070。内容 0.0.46／Snapshot v21；牢房、身体、求饶俘虏、房间面板及寻路／追击／工作互斥已实现，最新现场问题仍待人工验收。制作人本次明确授权完整文档化、commit＋push 到 origin/dev_openworld；此前各轮“未提交”是实施时历史，不构成当前交付状态。两份本地地图自动备份按既有约定排除并原样保留。提交与上传不等同 Producer Accepted／Sealed。

> **2026-10-27 最新追击／工作门禁：** [416](416-encounter-pursuit-and-work-lock-2026-10-27.md)已补入战与旧现场恢复时取消普通行动、睡眠及日程移动接管；追击每 0.4 实时秒独立更新，保留有效路线、拒绝已退出目标，并添加按失败原因去重的 EncounterPursuitBlocked 诊断。战斗耕田／伐木／普通资源破坏／劳动／修炼入口与执行禁用，工作循环退出；战斗墙体破坏保留。四程序集离线编译通过，具体截图现场待验，未启动 Unity／测试、未提交推送，0.0.46／v21 不变。

> **2026-10-27 最新寻路返修：** [415](415-character-navigation-recovery-2026-10-27.md)针对门口／墙边／农田／抱持卡住，补追击路线复用、有界绕人、动态落点重选、保留原命令的路线修复与无进度停止反馈；短步碰撞不随快进扩大，暂停不计堵路时长。只改 Host 导航表现，不改空间真源、身体／俘虏规则或 v21。四程序集编译通过、现场待人工验收，未启动 Unity／测试、未提交推送。

> **2026-10-27 最新求饶交互：** [414](414-mercy-without-permanent-withdrawal-2026-10-27.md)／ADR-0070明确放你一马与暂不处置完全相同，仅关闭对话；撤Spared退出／锁定／头标，之后仍可俘虏或继续求饶，醒来按412正常续战。索钱／索物只保留同次已索财防重复，不锁其它处置。未结算旧Spared清理、旧索财保留，已提交与正式撤退不重开。编译通过、待人工验收，未启动Unity／测试、未提交推送，内容0.0.46／v21不变。

> **2026-10-27 最新抱持现场返修：** [413](413-carried-view-alignment-and-custody-rescue-guard-2026-10-27.md)定位encounter:52阿石错位恰为旧(-0.28,-0.16)表现偏移；统一CarryFacing×0.38显示与健康检查，Tactical／WorldPresence／override保持脚底，capture不再回灌视觉偏移。自动救护候选／继续／放床排除拘押和独立战场人物，俘虏建立、Tick、运输加载校验清失权旧任务，取消不挪人、不解除有效拘押床位。四程序集编译通过、现场待验，未启动Unity／测试、未提交推送，内容0.0.46／v21不变。

> **2026-10-27 最新战场／房间：** [412](412-battle-recovery-capture-and-room-panel-2026-10-27.md)／ADR-0069已补ReadyToEnd苏醒续战、结束前复验与目标已达成但仍交战的有效存档组合；右键昏迷敌人近身俘虏，占一名抱持者、禁止攻击但可移动／原有飞行，保留其他人物控制。真实一对一押送支持放下、抱持者失能掉落、返场与原床位安置，修士不因此成为凡人工作者。房间本体右键用途入口移除，左键高亮后在底部面板改用途与安置，权限与二次确认保留。四程序集离线编译通过、待人工验收，未启动Unity／测试、未提交推送，内容0.0.46／v21不变。

> **2026-10-27 最新UI返修：** [411](411-dialogue-choice-layout-and-wake-clarification-2026-10-27.md)已实现求饶五项主菜单、索物／索钱带返回子页、UGUI／IMGUI自适应高度和长列表双列，超过屏幕空间才滚动。制作人澄清后保留严格超过半血苏醒，不恢复100秒倒计时，未改Core身体与v21存档。四程序集离线编译及diff检查通过，现场待人工验收，未启动Unity或测试、未提交推送。

> **2026-10-27 最新：** [410](410-rts-attack-and-defeated-mercy-2026-10-27.md)／ADR-0068覆盖409待确认项及317的NPC自动退出：已明确取消30%低血劣势自动撤离／投降，保留玩家主动撤退。战中清醒敌人红色光标右键直攻；约1%HP昏迷后右键继续攻击到求饶处置，放过／索实物／索实钱后放过或拒绝使其弥留，再右键显式击杀成尸体。当前荒村独立战斗链已接、后端不能绕过求饶补击，事实随v21既有Counter保存，旧未提交低血Withdrawn战场可清理重续，真战后苏醒不重开；农作门禁保留。通用个人包、复杂空间求饶、移接不在实施结果；编译通过、待人工验收，未提交推送。

> **2026-10-27 最新截图返修：** [409](409-encounter-farm-ownership-and-withdrawal-feedback-2026-10-27.md)针对阿兰29/93生命停战且种地，确认既有30%低血劣势Withdrawn可提前结算，不等于昏迷；已修农作Host旁路与动画缺少战场门禁、持续退出状态／结束原因缺失。保留低血撤离／投降，是否取消等制作人选择。Implemented / Producer Acceptance Pending，离线编译通过，不代表现场已验收；v21不变，未提交推送。

> **2026-10-27 最新简化伤势：** [408](408-simplified-injuries-and-remains-grafting-2026-10-27.md)／ADR-0067按制作人最后补充实施：低于50%可能割伤、低于25%可能重伤，打昏可能缺肢／断经脉，弥留95%断经脉；无疼痛、出血或止血。真实恢复超过50%HP才醒，固定100秒退役；普通伤渐愈，残肢与经脉伤保留。属性减益、缺肢显示、医者／监管／探视药物及身体面板、战后和v21存档接线完成；旧v15～v20 JSON可迁移，不补掷残伤。金丹遗骸移接、专门经脉修复及完整残疾生活仍未实施；编译通过、待人工验收，内容0.0.46，未提交推送。

> **2026-10-27 最新身体流程复核：** [407](407-unconscious-combat-lifecycle-wiring-audit-2026-10-27.md) 已补床休息／恢复处失能取消、倒地躺姿、战场内显式补刀近身入口及不可升级的单次操作意图，结算后原持续攻击不会自动将首次昏迷者打成弥留；苏醒不覆盖参战空间，非参战囚犯按真实床边站起。首次一 HP 昏迷、随倍速苏醒、弥留显式死亡和返回保留身体规则不变。四程序集离线编译与diff检查通过，仍待制作人人工验收，未启动Unity／自动测试、未提交推送；内容0.0.46／v20。

> **2026-10-27 最新牢房整链复核：** [406](406-prison-flow-wiring-audit-2026-10-27.md) 已静态核对401～405入口到结算／保存恢复并同轮修复：据点易主跳过已拘押／押送身份；同伴下令释放检查当前实际管理权；解除拘押统一处理床预约／睡眠／恢复放置，真实昏迷或睡眠床占用保留并支持Prison恢复记录读取；出口精确到达及普通日程前优先；监管濒危补药优先、坏目标有界退避；实际伤害补记每日受攻击。招募仍不移动、不治疗、不入队，同伴乙免招募。四程序集离线编译通过，未运行Unity或测试，人工验收待完成；未提交推送。

> **2026-10-27 最新占领后牢房管理：** [405](405-prison-warden-care-and-recruitment-2026-10-27.md)／ADR-0066 已接监管岗位、真实取送公粮灵草、独立抵抗及对话招募。工作面板选择己方可管理凡人任职监管；实际到合法仓库取一份携带物，到牢内本人附近交付，读取既有食物／医疗规则，濒危时可补足稳定药量。携带字段可选持久化，旧档无货；取消／转职／死亡沿既有返库落地。抵抗与每日事实复用持久化 ContentCounter/Daily，不改原势力忠诚或关系账本；完整照料日-5，受忽视+5，受攻击+10，不重复刷。拘押招募统一近场／管理权／清醒／抵抗及关系校验；成功不移动、不治疗、不自动入队。同伴乙免招募；既有破墙释放保留。监管离屏取送和自身需求通过既有冷导航实际移动，不瞬间交付。四程序集编译通过；未运行测试或Unity，人工验收待完成，未提交推送，仍沿现有版本与v20。

> **2026-10-27 最新监狱营救：** [404](404-prison-wall-breach-rescue-2026-10-27.md) 已用正常宣战、CharacterEncounter 战场内破坏监狱墙体替代撬锁／双巡卫失能强救。导航更新后验证真实内外可通行缺口，囚犯就地解除拘押，清醒者正常寻路离开，昏迷者仍需搬运；不移动释放瞬间位置、不治疗、不自动入队、不占领核心。无守军结构战保存可选 prisonAssaultRoomId（既有格式号不变）；旧档缺省空，旧撬锁执行失败且不扣料。墙缺口和获救复用原持久化；返场／加载导航后继续核对物理缺口，避免提前离场漏放人。四程序集基础编译通过，运行待人工验收，不启动 Unity／测试，未提交推送。

> **2026-10-27 最新违规分级与床休息：** [403](403-offense-tiers-prison-buffer-and-bed-rest-fix-2026-10-27.md) 将402的房内发现扩为牢房矩形及外沿三格禁区，被在岗实际看到立即追赶；日程不合规仍累计压力，满值规则保留。修复右键床到达后新BedRest被Stop取消，改为先收尾旧接近再执行回调，并按真实可达床边／Core使用格接线。其他严重行为只列候选，不自动实施。四程序集基础编译通过，实际睡姿、寻路和禁区范围待人工验收，未提交推送；内容0.0.46／v20。

> **2026-10-27 牢房最新入口与拘押：** [402](402-prisoner-person-interaction-and-trespass-2026-10-27.md) 覆盖401的入口和发现条件：进入牢房、靠近并右键具体囚犯；房屋菜单不代替人物。占领前原劳役玩家成员在牢内被巡视实际看见即追赶开战，无需压力满值；出门或切主控不清除追赶，占领后停用发现。清醒囚犯仅在所属牢房内活动，真实牢床在活动时保留预约容量；沿现有v20字段兼容床上旧档与房内活动。401的照料、任务和恢复保留，占领后可在对白内释放同伴。Implemented / Producer Acceptance Pending；未启动 Unity／自动测试、未提交推送，基础编译与人工路线见402；内容仍0.0.46／v20。

> **上一轮统一恢复入口（2026-10-27）：** [400 荒村当前完整阶段快照与 GitHub 交接](400-huangcun-current-checkpoint-and-github-handoff-2026-10-27.md) 汇总此前能力、持久化、人工验收和未实现边界。内容 0.0.46 / Snapshot v20；四程序集基础编译完成，未启动 Unity／自动测试，仍待人工实玩验收。400 当轮获授权的文档整理、commit 和 GitHub push 不延伸到 401；两份本地 previous 备份保留。394～399 的旧版本和未提交结论为当时历史。

> **巡查链路与范围圈追加：** [399](399-huangcun-discipline-chain-audit-and-watch-rings.md) 已确认压力五秒一点随倍速、暂停不涨；补追赶临时任务占用、旧 v20 残留行动清理、到旧点后继续追踪和床上人物近身站位。增加共用 Core 二十格资格／坐标的巡视圈，金色在岗、红色追赶，非在岗隐藏。整链静态调用已核对，四程序集基础编译通过，未运行测试或 Unity、未人工实玩验收。内容与格式仍 0.0.46／v20，不改地图，可继续当前存档，未提交推送。

> **当前巡查与身体流程交接：** [398](398-huangcun-discipline-and-unconscious-combat.md) / ADR-0065 已接昼夜巡卫、二十格观察、五秒一点主管压力、真实追赶与正常惩戒战斗。首败保留一 HP 昏迷，默认一百秒、随暂停／倍速推进；再攻击转弥留，普通攻击弥留不致死，右键显式杀死；战后身体状态原样保留，昏迷／弥留均可真实搬运到医疗／住宅床。生命周期既有枚举值不变，v20 保存计时与惩戒身份。内容 `0.0.46-huangcun-discipline-unconscious`，须新开局；四程序集基础编译通过，未运行自动测试或 Unity，未人工验收、未提交推送，非荒村全部体验封板。地图仍沿用 0.0.45。

> **荒村疏树追加：** 制作人要求村内留少量树。现从城外移回八棵中树至西南、东北及东侧空地，保留两倍尺寸、稳定 ID 与资源总数，避开门前、主路与田地；其他城市和建筑不动。版本 `0.0.45-huangcun-sparse-trees`，须新开局，发布与人工验收状态见 [397](397-thin-room-walls-resources-and-movement.md)。本次仅内容和文档变化，不运行自动测试、不启动 Unity、未提交推送。

> **当前房墙、资源和移动交接：** [397](397-thin-room-walls-resources-and-movement.md) / ADR-0064 已实施一格等厚房墙，显示与冷热通行共用薄墙矩形；房屋整体大小及家具位置保留。全图 1944 树／250 矿按 0.0.43 长宽各两倍并疏排至城外，扩作者画布而不移城址／城墙／建筑或行政范围。worldSpatialRules 的 walkSpeedMultiplier=1.5、flightSpeedMultiplier=2 可继续调参；WASD、点击、NPC 和冷热移动接线一致。内容 `0.0.44-thin-walls-resources-movement` / Snapshot v19，须新开局；基础编译通过，发布结果及人工查看项见 397，未人工验收、未提交推送。396 的作物周期、住房与设施权限保留。

> **当前实施交接：** [396](396-world-building-scale-and-crop-cycles.md) / ADR-0063 已实施全 66 城的建筑、房间与家具实际长宽两倍，以及全部粮田 12×8、药田 15×12，播种后五／十游戏日成熟，照料不直接加成长。真实占地、室内出生点、床位绑定、动态建设、私人营地及作者发布同步接线；荒村住房数量／邻田／占领前后设施权限保留。内容 `0.0.43-world-scale-and-crop-cycles` / Snapshot v19，须新开局；基础编译完成，正式发布结果见 396，Producer Acceptance Pending，未提交推送。下方 395 的 1.5 倍和仅荒村范围被本轮覆盖；历史提交授权不适用于本轮。

> **2026-10-26 最新比例与布局：** [395](395-huangcun-furniture-scale-and-layout-2026-10-26.md) / ADR-0062 承接制作人本轮授权：人物显示两倍，房间／床／家具实际长宽 1.5 倍；普通住房 10 人＋2 间备用，田地扩大并邻近住宿，练功室／功法阁／斗技坊在主管巡卫区，占领前仅主管巡卫可用、解放后开放。只重排荒村，其他城市既有实体保留。`0.0.42-huangcun-neighborhood` / Snapshot v19，须新开局；正式发布、基础编译和必要静态核对完成，未人工验收、未提交推送。下方 394 的提交上传授权只属于历史轮次，不授权本轮自动提交。

> **最新统一交接：** [394 荒村生存首版阶段快照与剩余工作](394-huangcun-survival-checkpoint-and-backlog-2026-10-26.md) 汇总 391～393 的实际交付、玩法入口、人工验收、未实现内容与下一轮建议。当前 `0.0.40-huangcun-survival` / Snapshot v19，须新开局；Implemented / Producer Acceptance Pending。制作人已明确授权本次文档整理、commit 和 push；不是体验封板，也不自动开展下一轮。下方“未提交”、旧版本和等待授权均保留为发生时的历史，不覆盖 394 当前状态。

> **2026-10-26 荒村生存首版：** 制作人明确批准实施，393 的原营地 ACR 由 [ADR-0061](43-decisions/ADR-0061-huangcun-survival-camp-and-captive-companion.md)承接。[393](393-huangcun-survival-and-hidden-camp-design-2026-10-26.md)已补实际数值、入口、持久化及人工验收项。`0.0.40-huangcun-survival` / Snapshot v19，须新开局；两人同行、同伴乙真实监牢囚禁；维生配给、短工／采集、两个营地选址、私人储物、城外修炼、探视与三路救援已接线，无强制教程。四程序集基础编译通过，未启动 Unity／自动测试，未人工验收、未提交推送。复杂搜查与剧情表现仍待打磨，不宣称体验封板。

> **2026-10-26 炉具与荒村循环：** [392](392-huangcun-furnaces-and-loop-audit-2026-10-26.md) 为本轮实施／复核入口。两炉只做预置实体、阻挡、美术和说明，不做配方产出或扩展建造入口；当前循环有自由成长到真实夺村的主干，压力／隐匿后果及资源用途密度仍偏薄。不要把旧稿强制筑基、复杂追捕或自动交权当作已批准待办。

> **2026-10-26 荒村可选帮助：** [391](391-huangcun-practical-opening-guidance-2026-10-26.md) 已实施默认收起的修炼／生活／夺村只读帮助、首次实际吐纳记录、核心状态与医疗净恢复提示、委托与巡卫文案对齐。制作人明确禁止强制教程：不锁操作、不强制弹窗、不增加进程门槛。内容 `0.0.38-optional-huangcun-guidance`，四程序集编译通过；正常开局节奏与恢复体验待人工验收，未提交推送。

> **当前统一入口（2026-10-26）：** [390](390-world-checkpoint-and-huangcun-next-round-2026-10-26.md) 汇总本轮地图／河网／势力／UI／修复、作者发布路径与验收边界，并记录荒村占领议政厅前的已有内容和缺口。本轮制作人授权文档整理、提交与推送，未授权自动开始下一轮。当前为 66 城／14 无主、`0.0.37-two-compact-sects`；运行仍待人工验收，不因提交宣称封板。下方按时间累积的旧结论均需结合 390 当前状态阅读。

> **2026-10-26 城镇详情展示：** [389](389-map-city-inspection-and-wilderness-discussion-2026-10-26.md) 已将右上角选城信息改为中文归属／色标、等级、主城标识和坐标，底部提示改城名；基础编译通过，无需新开局，待 UI 人工验收。野外玩法仍为 Proposed，未开始铺设内容。

> **2026-10-26 最新增补：** [388](388-two-compact-sects-and-next-step-2026-10-26.md) 已发布天麓宗、听雨宗，各 4 个邻近城镇，主城为镇魔北关和雷南关。`0.0.37-two-compact-sects`：66 城／14 无主，其他势力不改；仅 8 城归属变化，地形／导航／建筑原样保留。须新开局人工验收，未提交封板；下一阶段建议仅为 Proposed。下方无主 22 是历史数量。

> **2026-10-26 最新归属覆写：** [387](387-compact-faction-ownership-2026-10-26.md) 已将镇岳、朔风、合欢、青霄收拢到相邻城镇，城数 7／7／5／5 不变，合欢主城改西阵镇。内容 `0.0.36-compact-faction-ownership`；仅所有权和缓存来源 hash 更新，地形／导航／建筑／城址不变。须新开局人工验收，下方旧分配不再是当前初始归属。

> **2026-10-26 最新覆写：** [386](386-river-network-and-faction-navigation-2026-10-26.md) 组合河网与东部大河北延已发布为 `0.0.35-river-network`，8 段河、13 桥；图例支持悬停高亮和双击主城。镇岳 7、合欢 5、无主 22，总数 66。城址、等级、扩建预留和飞行不变，导航／大地图 hash 一致，四程序集基础编译通过，待新开局人工验收。下方“河流仍为候选”是历史状态。

> **2026-10-26 图例与河流方案：** [385](385-world-map-legend-and-river-options-2026-10-26.md) 已实施大地图底部可折叠势力色标／实时占城数，基础编译通过，待人工验收。新增两河仍为三个候选图，未修改或重发运行地图；不得将草案当成河道定稿。

> **2026-10-26 关口接缝与性能返修：** [384](384-gate-seams-and-runtime-cost-2026-10-26.md) 为本轮实施记录：20 座关城接缝精修、城外宽切口取消、转角门道两侧封闭；不移动城市、不缩城镇或五级框、不改飞行。内容 `0.0.34-gate-seams` 需新开局。互动点空间索引、床位查询与固定布局 HUD 已优化，基础编译通过；Producer Acceptance Pending，不能用旧性能报告宣称帧率回退已解决。

> **2026-10-26 风暴／雷暴最新覆写：** [383](383-storm-thunder-gates-2026-10-26.md) 为当前东北局部地理依据：风暴城东迁，飓风／雷暴区域共边填空，新增中立三级雷西关／雷南关及雷暴外围阻挡；66 城，其余 63 个旧城不动。内容 `0.0.33-storm-thunder-gates` 须新开局，发布／验收状态见 383。

> **2026-10-26 局部地理最新覆写：** [382](382-basin-seals-and-tongxi-relocation-2026-10-26.md) 取消西阵／东阵、南赢盆地的穿山缺口；南赢镇和盆地南移 360 格，桐溪镇迁到魔兽山脉与极寒雪原之间。其他 62 城、世界尺寸、城镇尺度与速度不改。内容 `0.0.32-basin-seals-town-relocation` 须新开局，发布／验收状态见 382；下文为此前记录。

> **2026-10-26 最新地图覆写：** [381](381-round-enclave-and-beast-gates-2026-10-26.md) 为当前局部地理依据：荒村约 960 格直径自然圆形；魔兽山脉外围闭合山带、四座新增中立关城、西关接桥，合计 64 城。原 60 城、18450×8700 世界、建筑和五级扩建尺度不变，飞行速度不改。内容 `0.0.31-round-basin-beast-gates` 须新开局；发布结果和待人工验收项见 381。下文 720 方形／60 城是历史口径。

> **2026-10-26 荒村盆地最新覆写：** 制作人要求恢复 720×720 可探索范围，已按 [380](380-world-compaction-and-presentation-fixes-2026-10-26.md) 发布 `0.0.30-huangcun-basin-720`。城心／全世界 18450×8700／60 城位置及尺寸不变，五级框每边保留 90 格，山带外置；原洞府、建筑、资源与出生点不动，仅迁移内外灵阵。静态核对洞府／灵阵和全部 NPC 小队初始落点合法，未启动 Unity、未运行自动测试、未提交推送，需新开局人工验收。

> **2026-10-26 空地压缩最新覆写：** [380](380-world-compaction-and-presentation-fixes-2026-10-26.md) 与 ADR-0060 追加授权为当前布局依据。主世界 **18450×8700**，内容 **0.0.29-compact-world**，需新开局；60 城、建筑及 540 格五级框不缩，最小框间隙 48 格，全部扩建框完整位于世界边界内。保留原定稿坐标，加独立单调空地压缩配置，同源发布三件套；小盆地／关口保护、速度未改。北部角色排到地面以下的显示问题已修复，床位精度与床边出生修复保留，睡眠查询与临时分配已优化并补 F9 标记。四程序集基础编译完成，Unity 人工验收及 FPS 收益待制作人复测；不得宣称性能回退已完全解决。未提交／推送。

> **2026-10-26 地图精确复原：** [378 地理定稿与实施](378-final-world-geography-gate-handoff-2026-10-26.md)及 [ADR-0060](43-decisions/ADR-0060-final-world-geography-and-site-retirement.md) 现为新开局世界布局权威：按图实际 **60 城**（旧 56 保留／52 退役／4 新增）、八个人类宗门；21600×9500 Surface 统一等比城心与地貌，荒村使用闭合多边形和实际两端灵阵，Mountain 导航阻挡与世界图 Y 方向已修正。内容包 `0.0.28-world-map-exact` 需新开局，四程序集基础编译完成，Unity 视觉／关口／飞行人工验收待进行。本页下方 57／108 城、旧版本与 16 宗为此前阶段记录。

> **2026-10-25 当前待验收改动：** [357](357-golden-core-energy-and-town-footprints-2026-10-25.md)已覆写 [356](356-world-map-flight-travel-and-level-one-size-2026-10-25.md) 的 0.75：等级 1～5 理论长宽为 180／270／360／450／540 cells，108 处固定城镇实体蓝图按现等级重做，围墙沿控制边缘；荒村现为 180×180 格。金丹及以上不再因日常或打坐扣精力，已耗尽者恢复行动阈值。作者源已重发三份正式运行世界数据，运行包 `0.0.23-town-footprints-080` 需新开局。此前世界图中断预检与开发调用栈保留；其他取消来源仍待现场复现，基础编译／静态检查已完成，未运行自动测试、未提交推送。

> **2026-10-25 最新世界图旅行返修：** [351](351-world-map-long-route-and-player-marker-2026-10-25.md)将远距离受阻寻路移出地图主线程，限制长路径简化成本，局部前沿须产生实际位移；村界／无路拒绝显式提示，主控小队使用固定屏幕像素标志。Core／Data 与 Unity Core／Host 基础编译 0 错误；Unity 现场帧时、远路可达性仍待制作人验收。未提交推送。

> **2026-10-25 最新城镇视觉返修：** [350](350-town-walls-stone-room-art-and-chunk-seams-2026-10-25.md)为青石及其余 79 处无墙据点补石墙，在道路口留门；108 处固定 Site 现在均有墙数据。石屋墙体换独立石砌图，Chunk 草地网格按瓦片实际跨度铺齐。运行包 `0.0.21-town-walls-and-art`，需新开局。仅基础编译／静态引用完成，Unity 视觉、通行、帧时待人工验收；未提交推送。

> **2026-10-25 最新城镇布局：** [348 实施／验收](348-natural-town-layout-and-qingshi-urban-quarter-2026-10-25.md)把 108 个固定 Site 从等距方阵改为不等距错列，五级范围最小间隔 96 格，主世界仍为 15200×6800；蓝图、地点／人物、同城与城际道路随之迁移。青石镇 240×210 格正交街区补石制公共房和街面内容，其余 106 处补 258 间沿街房。运行包 `0.0.20-natural-towns`，需新开局；Unity 人工验收待制作人进行，未提交推送。下条 175 格与 `0.0.19` 是此前等距布局的历史状态。

> **2026-10-25 最新世界布局：** [ADR-0056](43-decisions/ADR-0056-five-level-town-separation-and-world-expansion.md) 与[347 实施／验收](347-five-level-town-separation-implementation-2026-10-25.md)将主 Surface 扩至 15200×6800 格并重排 108 个固定 Site；哪怕全部五级，行政方框最小仍隔 175 格，蓝图、锚点、道路和地形同步迁移。运行包 `0.0.19-separated-world`，需新开局；基础编译／静态检查通过，Unity 人工验收待进行，未提交推送。下文 7600×3400 和旧内容版本均是各自时点历史状态。

> **2026-10-25 最新运行实施：** [345 固定议政厅实施记录](345-fixed-sites-and-town-levels-architecture-change-2026-10-25.md)已将 ADR-0055 接入作者源和正式运行包 `0.0.18-fixed-council-sites`：108 个固定 Site／核心、103 个三级范围、五级升级入口、青石正交街区与五处边缘重排。基础编译和静态完整性通过，Unity 人工验收待制作人进行；未提交推送。下两段保留为此前检查点与设计阶段的历史记录。

> **2026-10-25 工作区备份入口：** 制作人要求先将当前所有非忽略工作区改动连同文档提交并上传 GitHub；范围与状态见[346检查点](346-2026-10-25-workspace-github-checkpoint.md)。这是 Git 备份，不改变各功能的 Producer Acceptance，也不把下述固定据点新设计当作已实施。实际提交及推送结果以 Git 为准。

> **2026-10-25 最新设计，不是运行现状：** 制作人确认[ADR-0055](43-decisions/ADR-0055-fixed-council-hall-sites-and-five-level-control-range.md)与[345变更单](345-fixed-sites-and-town-levels-architecture-change-2026-10-25.md)：荒村一级，青石与 102 个旧旗据点暂三级，其他四个固定地点保留当前等级；议政厅 1～5 级长宽倍率 1／1.5／2／2.5／3；102 个历史旗 Site 保留身份迁为固定可占领核心，玩家不再建旗造新 Site，可升级现有 Site。允许重排中心与道路，青石改为横平竖直、少分叉街路，旧[344](344-qingshi-urban-upgrade-proposal-2026-10-24.md)已据此修订。旧档采用现有 package-version 拒绝／保留路径，不暗迁；[108行身份清单](345-site-identity-inventory.tsv)已导出。状态 Design Confirmed / 未实施，地理落点与升级成本待内容制作；下方 344 曲折街巷说明只保留历史时点含义。

> 最新：[344青石镇城镇化修订提案](344-qingshi-urban-upgrade-proposal-2026-10-24.md)。制作人否定先前四区／十字路／四门方案；已据同类游戏重画[沿街城镇概念图](344-qingshi-urban-concept.png)并列出青石应有的书肆、坊市、拍卖、医馆、委托、功法阁、丹室、炼器坊、客栈、驿站、巡守所等功能层次。现成系统与待设计的高级玩法分开列明；设计状态Proposed，青石正式蓝图与运行地图未改。下文若提到344旧四区图，以此条覆盖。

> 最新：[343有效WASD复采与返修](343-world-streaming-and-tick-frame-budget-2026-10-24.md)。23:22报告已采到约18秒20倍速WASD，移动常见159～239 FPS，Surface跨界峰值2.27ms；仍有30～45ms慢帧，F9定位尸体／医疗合段稳定5.2ms/Tick与凡人阶段部分9～10ms/Tick。已跳过满血、无病程/草药的普通角色每秒全图查床，并为凡人阶段补F9只读细分；四程序集基础编译通过，修后1倍速/20倍速移动待复采。青石城市骨架与[概念图](344-qingshi-urban-concept.png)为[344 Proposed](344-qingshi-urban-upgrade-proposal-2026-10-24.md)，未改地图。未运行自动测试、未提交推送。下条23:09“无WASD”只指前一份报告。

> 最新：[343修后F9](343-world-streaming-and-tick-frame-budget-2026-10-24.md)及[344青石城市化提案](344-qingshi-urban-upgrade-proposal-2026-10-24.md)。制作人23:09新报告仅含1倍速静止窗口，平均约290 FPS、WorkTarget约0.09ms/帧；WASD与跨Chunk尚无修后数据，最高30.6ms偶发帧需用新F9细分农田/凡人/个人生活/旅行等Tick服务后定位。四程序集基础编译通过。青石现行140×160、14房/28树，比荒村稀疏；344仅Proposed，建议先城市街区灰盒、再建筑/真实NPC/美术逐批推进，当前地图未改。Unity画面采集重试超时，用户授权操作不等于已取得现场操控；未运行自动测试、未提交推送。

> 最新：[343制作人复测后的悬停与核心Tick返修](343-world-streaming-and-tick-frame-budget-2026-10-24.md)。22:46人工F9报告均为20倍速：跨Chunk的Surface.Update峰值约2.4ms，稳定约5.2ms／帧的鼠标悬停路径与Core Tick峰值是当前重点。已把田格命中改为局部分桶、工区田格只查一次，工作光标跳过不会消费的抽象工区全图遍历，议政厅占地改用派生索引，并在F9加入悬停和Tick分段。四程序集基础编译通过；修后1x／20x帧率、GC与最慢帧等待制作人新开局现场采样。未运行自动测试、未提交推送；下方343首轮“尚无修后采样”只指该当时状态。

> 最新：[343 世界扩容性能与Chunk顿挫](343-world-streaming-and-tick-frame-budget-2026-10-24.md)。22:06现有F9报告在20倍速、无WASD时记录世界Tick显著挤压主线程；按制作人要求已为自动Tick设单帧预算与有界积欠，以派生空间／床位索引取代局部导航、床边与分块建造物全图扫描，并将普通跨Chunk建筑树木生成拆为小批。洞口显形与中途改向清理已接线，F9补分项。四程序集编译通过；本轮后FPS／跨界峰值未现场采样，需制作人新开局1x与20x、荒村及宗门街区行走验收。未启动Unity、未运行自动测试、未提交推送；342内容与已封板迁移不变。

> 最新：[342 世界聚落与公开进阶传承](342-world-settlements-and-public-techniques-2026-10-24.md)。按制作人授权将102个旗帜据点与4固定据点正式配置街区，连同荒村／青石共108处；16主城使用石制住宅与公建，新增36真实服务NPC，总人口56。新增16功法／16斗技／32书册和21建筑定义；木石表现、旗帜据点NPC／地点加载与Site→Chunk索引已接线。版本0.0.17需新开局，四程序集编译和静态内容检查通过；运行未验、未提交推送。丹室炼器配方、高阶宗主与全城人口仍未实现；本条覆盖旧候选不发布状态，不重开任何封板迁移。

> 最新：[341荒村四区与散生资源](341-huangcun-quadrants-resources-and-workrooms-2026-10-24.md)。内容0.0.16需新开局：一格田埂、右上药圃、右下主管／巡卫房床和锚点、删除荒村歇脚屋、5室外棋盘、210棵两倍长宽树、铁铜银灵各4散矿。新增低级木制8×12功法阁／丹室／炼器室及建造定义，阁内2蒲团；丹室／炼器配方未实现。核心范围和青石布局保留，旧采树区抽象发木取消，作者／运行／候选同步。四程序集基础编译与静态内容校验通过，运行待验，未提交推送；340对应旧布局以本条覆写。

> 最新：[340荒村生活与聚落补充](340-huangcun-daily-life-and-settlement-content-2026-10-24.md)。内容0.0.15需新开局；开局三选项释义／将老补接、巡卫主管规律日程和真实修炼、近田药圃、荒村72新树／公共修炼院／歇脚屋／2医疗床、青石生产生活设施／原居民职业作息、8委托与33对话已接线并发布。荒村989对象、青石64；每Site仍仅一公库，原人口／核心范围保留。四程序集基础编译及内容静态引用通过，运行待人工验收，未提交推送；统一验收与未完系统见340。制作人本轮确认339卡顿基本解决，未将200 FPS／全场景性能判为通过，本轮没有启动Unity。

> 最新：[339现场133 FPS与200 FPS预算优化](339-pointer-facing-and-movement-performance-2026-10-24.md)：制作人13:46 F9报告平均133.40 FPS、完整按WASD窗口132.56，全程未暂停；1帧>33.3ms、最慢33.8ms。120仅预算，现场VSync=1／targetFrameRate=-1／170Hz。已继续实施既有SurfaceSpatial索引复用、床边等价短路、隐藏选择圈／标签与队伍临时分配优化，并修复隐藏HUD点击区持续累积；Ctrl+F9仅在F9期间临时不限帧，关闭／禁用恢复原设置，报告保留全部脚本项。四程序集基础编译及静态检查通过，追加收益／200 FPS与完整交互待新采样，未提交。备份Temp/339-200fps-before；制作人采样授权仍有效、自动测试禁令不变。下方339“未达成／待报告”为更早阶段记录。

> 最新：[339采样后的定向返修](339-pointer-facing-and-movement-performance-2026-10-24.md)：制作人报告约23～26 FPS，Update均值34.03ms为主要开销。已让远处势力旗／NPC先由已加载Chunk集合排除，避免逐帧扫描10336块；旗帜／光标不变时停止重复写表现。F9补具体脚本热点，Unity／Unity.Editor基础编译通过；修改后FPS与运行待人工确认，120 FPS未达成，未提交。旧采样保存在Temp/339-hotspot-before。

> 339最新现场：制作人截图22.6 FPS／44.3ms，正式HUD隐藏仍持续低帧；i7-12700F／RTX 3070／约32GB／170Hz已只读核对，目标约120 FPS（尚未达成）。F9已补CPU分项与关闭时保存最近采样到Temp/339-performance-latest.txt，Unity／Unity.Editor基础编译通过；需制作人人工1x行走约10秒并关F9取得分项证据，主要耗时尚未定位。未启动Unity／自动测试，未提交。

> 最新：[339 主控朝向与移动性能](339-pointer-facing-and-movement-performance-2026-10-24.md)。制作人要求普通主控站立也跟随鼠标，并查WASD卡顿／木墙抖动。持续朝向、普通Chunk网格分帧准备、环境mipmap、日志／文本重复开销及音效空对象判定已修；四程序集基础编译通过。持续低帧反馈补充：HUD固定样式复用、F9帧耗时读数已接线，Unity／Unity.Editor编译通过；下一次人工行走可读取平均FPS／最慢帧，并F10对照HUD。未运行Unity或自动测试，未测量现场FPS，仍待人工验收、未提交。当前宗门密度暂留、位置后续微调，不据此宣称宗门完整内容已验收。

> 最新：[338 十六宗门正式势力范围](338-sixteen-sect-territories-2026-10-24.md)。16宗全部接入正式领地，新增90旗（总102旗及原6处Site），世界／荒村尺寸不变；0.0.14需新开局。地图补宗门名及派生边界缓存。四程序集基础编译及静态引用检查通过；运行待验，未提交推送。下方候选城镇状态仅指城内设施和驻军，领地已发布。

> 最新：[337 金丹化翼实施与验收](337-golden-core-flight-design-2026-10-24.md)。制作人已批准框架扩展；金丹突破、F3飞行／轻量翅膀、空地75%闪避及Snapshot v18已接线，内容0.0.13-golden-core-flight。基础编译通过，运行待人工验收，未提交／推送；F2仍为独立远程模式。

> 2026-10-24：制作人批准[筑基定向射击](43-decisions/ADR-0053-directional-ranged-basic-attack.md)，F2展开后主控WASD移动、鼠标瞄准、长按左键发射。状态与验收见336；不会绕过原独立战斗确认。

> 宗门打法最新见[335](335-sixteen-sect-combat-identities-2026-10-24.md)：16套功法／斗技补齐攻防血量、回血、连击冷却与修炼难度取舍。版本0.0.12-sect-combat-identities；已配置，待人工验收；普攻加速及元素特殊机制待框架批准。未提交推送。

> 2026-10-24最新布局：[334](334-compact-faction-territories-and-single-core-huangcun-2026-10-24.md)。荒村220×200、804段围墙、单议政厅225×225完整覆盖；13人／床位同步，原补范围旗变为村外衡河西岸驿。同宗据点280格邻接，正式世界与16宗门候选同步，Content`0.0.11-compact-territories`需新开局。框架／宏观地形／河流不变，运行待验，未提交推送。

> 2026-10-24最新续做：[333 小荒村与十六宗门内容](333-small-village-and-sixteen-sects-content-2026-10-24.md)。荒村13／青石镇7，正式名册20；新增16功法、16斗技、32书册及公开书院4委托，现22任务／67生活事件／6机会模板。版本`0.0.10-small-village-sect-books`需新开局。制作人选择高阶人物先设计，16宗城与附属节点只在作者候选，无发布绑定；未改框架、待人工验收、未提交推送。此条覆盖下方旧人口与版本计数。

> 2026-10-23最新开局规则及续制见[332](332-huangcun-confined-opening-and-liberation-2026-10-23.md)与[ADR-0052](43-decisions/ADR-0052-huangcun-confined-opening-and-world-unlock.md)：新增Core村界准入与外界暂停，真实取得荒村后永久解锁；正式内容现20人／18任务／54生活事件／6机会模板，版本0.0.9，需新开局。已有329～331成果完整保留；此条覆写下方不受限自由出村与旧数量，未提交推送，待人工验收。

> 2026-10-23 最新内容／小物件美术续制见[331](331-huangcun-content-and-small-props-production-2026-10-23.md)：正式自由首段现20人、11任务、38生活事件、3机会；新增条件回访与可选实际成长记录。粮／药／石3张透明静态图已接背包和地面显示，导入上限128、不增加实体或角色精细度。56项登记／XLSX同步、静态引用0错误；未运行Unity，未提交推送。主管交权等未定规则继续保留。

> 2026-10-23后续运行复查见[330](330-runtime-lifecycle-and-npc-access-review-2026-10-23.md)：动态机会／地面物品会话生命周期与登记、独立棋座可达性、洞府手动视线、任务HUD摘要一致性及UI临时分配已修，基础编译通过，待窄范围人工复看。329内容与未定规则边界保留，未提交。

> **2026-10-23恢复覆写：** 制作人授权继续，已完成328所列正式内容隔离、任务日志、将老稍后分支、正式／LevelTester同源入口、基础编译缺口及独立战斗视线／战后显式远程补刀接线；首段现20人／9任务／30个生活事件／3个机会。见[329](329-project-audit-resume-and-opening-completion-2026-10-23.md)。下方暂停状态为历史检查点；当前Implemented / Producer Acceptance Pending，未提交或推送。

> **2026-10-23 暂停／跨电脑恢复覆写：** 先读 [328](328-paused-work-cross-computer-handoff-2026-10-23.md)，再读 [323](323-project-wide-audit-and-production-pass-2026-10-23.md)～[327](327-huangcun-opening-content-production-2026-10-23.md)。编辑器 11/11 发布已完成；部分性能／NPC 增量尚未统一编译；荒村首段 6 份 JSON 已落盘并改为正式绑定，但旧验收内容隔离、任务日志显示及将老稍后分支仍未完成。当前版本为 `0.0.8-huangcun-living-opening`，覆盖下文版本号。制作人要求把当前全部源码／内容／文档备份上传 GitHub，属于暂停检查点，不是完工验收或封板；后续从 328 的断点续做。

> **2026-10-23 最新布局覆写：** [322](322-huangcun-compact-housing-and-cell-wood-walls-2026-10-23.md) 已收紧 22 间房及 27 床，19 间普通住宅横向隔 1 格／排间隔 3 格，20 名住户锚点同步；新增 844 段 1×1 木围墙、四面各留 8 格开口。独立木墙 prefab 单渲染层、无物理组件，按既有 Chunk 加载；木墙建造模板改 1×1、石墙不变。当前版本 `0.0.7-huangcun-wood-walls`，覆盖下文版本与 99 对象统计；村内现 943 对象、正式 Surface 948 对象。Implemented / Producer Acceptance Pending / 未提交；须新开局检查通行、破坏和同屏／加载性能，不以编译代替人工验收。

> **2026-10-23 整数格占地覆写：** [321](321-integer-grid-footprints-and-use-cells-2026-10-23.md) 已修正旧导入物件的小数投影占地、旗落点及动态物件，人物 1×1 连续移动，棋盘两侧使用格脚印与 Core 目标共源；作者与加载校验已接入。当前版本以 322 为准；世界、荒村、行政范围及道路河流宽度不变。Implemented / Producer Acceptance Pending / 未提交，需新开局验收。

> **2026-10-23 当前地图：** [320 WORLD-EXPAND-05](320-world-expand-05-7600x3400-2026-10-23.md) 已按制作人确认将世界扩至 **7600×3400**、荒村扩至 **240×200**，议政厅／势力旗一级控制范围改为 **225×225**；道路／河流／桥梁与建筑家具宽高保留，房床、人物锚点及小队部署同步。作者源经现有发布器生成三份运行数据，地图显示纹理按设备上限采样；内容版本为 `0.0.5-world-expand-05`，须新开局查看，不迁移或删除旧档。当前 Implemented / Producer Acceptance Pending / 未提交，具体检查与人工验收见 320。

> 2026-10-21 最新下棋规则：左右两个独立座位，一人到位即娱乐；个人心境、进度与退出互不依赖，取消等棋友／双人开局。棋盘与蒲团补齐左键检视和选框，突破结果置于普通 HUD 上层。见 [319](../40-process/319-two-npc-chess-recreation-2026-10-21.md)。

> 2026-10-22 棋盘娱乐与脚点避让返修见 [319](319-two-npc-chess-recreation-2026-10-21.md)：荒村四蒲团左侧增加 1×1 棋盘；左右两个独立座位，一人到位即开始自身娱乐，不等棋友。棋盘／蒲团占位、人物短步避让与设施空闲脚点已接线；身份一致的坐姿／落子、中断与棋局存档保留。原将老井字棋保留。美术登记新增 ART-ROOM-011 后共 56 项。实施范围获制作人封板授权，视觉与运行行为仍待现场验收；Git 结果以仓库为准。

> 2026-10-21 NPC AI 闭环与多轮停战谈判见 [318](318-npc-ai-closure-and-peace-negotiation-2026-10-21.md)、[ADR-0051](43-decisions/ADR-0051-party-exhaustion-and-negotiated-peace.md)。离屏返床／接敌／撤离、遮挡、六槽冷却与个人需求 JSON wire、全队主动休息／力竭留地及外交谈判已接线，待制作人 LevelTester 逐项验收；该实施时点尚未提交，现以 2026-10-22 封板记录及 Git 为准。谈判只释放实际俘虏，不交易自由人物；支持多轮还价、附庸及战争级停战。

> NPC 自主生活／战斗入口：[317](317-npc-autonomy-design-confirmation-2026-10-21.md)、[318](318-npc-ai-closure-and-peace-negotiation-2026-10-21.md)、[ADR-0049](43-decisions/ADR-0049-npc-personal-life-and-cold-conflict-authority.md)。三档倾向、合法取食、蒲团到位修炼、疲劳返床、洞府近场和 NPC 离屏交战均已接线；318 后续补齐队伍主动全员休息、现场保守遮挡、非独立现场斗技冷却保存。自主投降后的实际捕获押送、NPC-NPC 专门战报／掠取／增援及完整现场验收仍未闭环。静态编译与 Git 封板不等于制作人逐项验收，当前 **Partial Implementation / Producer Acceptance Pending**。本页下方历史状态须按最新 318／319 与 devlog 解释。

> 2026-10-21 内容生产前修复入口：[316](316-content-production-readiness-repair-2026-10-21.md)。正式开局已用独立参考名册，作者工具保存与非荒村蓝图导出接缝已修，外观映射可配置 Rig 且构建校验资源；代码已实现、人工画面验收待做。后续制作人授权与 NPC AI 返修一并提交上传；实际 Git 状态以仓库历史为准。本页下方早期“未提交”文字保留历史时点，以最新 devlog 为准。

> 2026-10-20 后续医疗收尾与地面显示：[313](313-medical-closure-and-ground-loot-2026-10-20.md)已接线，待人工验收、未提交。实际卧床恢复精力、旧v15零HP囚犯兼容、转职归还/死亡落地及洞府功法/斗技显形均已实施；当前Snapshot v17支持读取v15/v16。下方初次医疗v16记录保留历史时点含义。

> 2026-10-20 医疗覆写：[312](312-medic-bedside-treatment-2026-10-20.md)与[ADR-0048](43-decisions/ADR-0048-medical-recovery-and-carried-medicine.md)已按制作人授权实施。Medic不再延期；公库/附近Party取药、床边施药、弥留及超过50%HP苏醒、Snapshot v16（v15读取时补齐）待现场验收。新开局阿禾、阿杏、阿枝为医者；既有房屋与床位不变。未提交；不继承旧民生整体验收状态。

> 2026-10-20 直线弹道验收覆写：制作人确认远程普通攻击现为发射后固定方向的直线弹道、不要追踪，并明确表示“现在这样挺好的”。[310](310-visible-ranged-hit-damage-timing-2026-10-20.md)／[ADR-0047](43-decisions/ADR-0047-visible-ranged-impact-damage-timing.md) 状态更新为 **Implemented / Producer Accepted / 直线修正待提交封板**；下方 310 待验收仅保留历史时点记录。

> 2026-10-20 最新封板：制作人要求将当前工作区所有内容统一中文提交并上传 GitHub。此次覆盖 [306 正式／Demo 构建分离](306-formal-player-and-demo-build-separation-2026-10-19.md)、[307 物件选中与蓝点返修](307-world-object-selection-feedback-2026-10-20.md)、[308 床／搬运／卧姿返修](308-bed-entry-and-rescue-pickup-2026-10-20.md)、[310 可见远程命中时扣血](310-visible-ranged-hit-damage-timing-2026-10-20.md)及同期民生状态、静态准备度文档。CIVILIAN-LIFE-01 整体与正式包打包／运行／基本存档沿用制作人已给的验收反馈；307、308、310 的具体运行观感仍为 Producer Acceptance Pending。下方未提交／未封板仅保留历史时点记录，以本条和各最新文档为准。

> 2026-10-19 当前新实施：[306 正式试玩与Demo分离](306-formal-player-and-demo-build-separation-2026-10-19.md)。制作人授权后，正式LevelTester与历史Demo使用独立Windows构建入口、产品名、内容路径和Player存档；普通会话菜单已接线，Editor旧存档保留。制作人确认可打包、运行且打包后基本存读档未见问题；Formal／Demo／Editor 分项隔离尚未逐项记录，未提交/推送。制作人已明确确认现有 CIVILIAN-LIFE-01 整体民生验收通过，详见 267 唯一状态表；Medic 和新内容不在该范围。路线图/267/305已增量修正旧口径；下方封板记录只对应先前962542c及其范围。

> 2026-10-19 最新封板授权：制作人明确要求将当前工作区剩余改动全部提交并上传。势力旗战斗反击、WorldMap 图标缩放、窗口输入隔离及绿／蓝旗外观仅按已反馈的现场范围记 Producer Accepted；封板不扩大 Medic、其他系统和美术残项的验收范围。下方“未提交／未封板”为各实施当时记录。

> 2026-10-19 系统／美术静态审查入口：[305](305-system-and-art-closure-audit-2026-10-19.md)。本轮只补现成绿／蓝势力旗运行图并校正 `267` 过时状态；未将 Medic、新物流或有质量问题的旧图伪装成已完成。

> 2026-10-19 势力旗验收覆写：制作人已明确确认本轮势力旗战斗内反击验收通过，状态 **Implemented / Producer Accepted / 未封板**。下文“待验收”仅为实施时记录；本轮尚未提交或推送。

> 2026-10-19 势力旗后续扩展：按制作人确认，势力旗复用议政厅的独立战场远程攻击规则；无守军旗单独受攻也进入以旗为唯一敌方核心的独立遭遇。Core／Data／Host 已接线，状态 **Implemented / Producer Acceptance Pending / 未提交**；规则与范围见 [ADR-0046](43-decisions/ADR-0046-faction-flag-encounter-defense.md) 和 [战斗 §12.2.1](../20-systems/23-combat.md)。议政厅上一轮的人工验收不能代替本轮势力旗验收。

> 2026-10-19 最新验收与封板授权：制作人明确确认议政厅攻击及弹道、巡卫战后返场、两处 Sprite `Invalid vertex array` 修复在 LevelTester 现场验收通过，并要求将本批改动封板推送。状态为 **Implemented / Producer Accepted / 封板授权**；开发侧仍只做离线编译与静态核对，不声称亲自运行 Unity。下文旧的 Proposed／待验叙述为历史阶段，以本条为准。

> 2026-10-19 最新覆写：议政厅攻击已按制作人确认写入 [战斗 §12.2.1](../20-systems/23-combat.md) 与 [ADR-0045](43-decisions/ADR-0045-council-hall-encounter-defense-authority.md)，并完成 Core／Data／Host 接线，基础离线编译通过。制作人随后明确回复“验收好了”，本功能状态 **Implemented / Producer Accepted / 未提交封板**。下文关于 301 仍 Proposed、符阵不发弹道的陈述只记录当时阶段，已由本条覆写。开发侧未启动 Unity 或运行自动测试；验收结论来自制作人，本轮未提交或推送。

## 2026-10-18 人物表现与美术源文件最新交接

当前人物骨架、八向移动、睡眠层级、工具/武器及右键改路的代码状态和美术文件清理边界，统一转读 [297 最新交接](297-art-rig-and-source-cleanup-current-handoff-2026-10-18.md)。它们已实施并获制作人版本封板授权，但侧臂、床上表现及连续改路等现场观感仍待人工验收；不能由 Git 提交或离线编译推定为 Producer Accepted。以下旧段落保留其各阶段历史上下文，不作为 2026-10-18 人物美术现状的唯一入口。

制作人后续复看动画认为当前观感可继续推进。本阶段新增 [298 黄村环境同屏样板](298-huangcun-environment-visual-slice-2026-10-18.md)：住宅木地板/围墙及道路仅在 Host 表现接线，田地沿用现有状态图，实施完成待制作人现场验收；没有改变 Continuous Surface、建筑或农田 authority。

298 木框墙已被制作人现场截图判定像栅栏；[299 返修](299-solid-wood-wall-and-farm-stage-presentation-2026-10-18.md) 改为封闭实木墙，并让麦田／药田图随现有真实阶段切换。仍为 Host 表现 Implementation Complete / Producer Acceptance Pending；不把 298 的旧墙描述当作当前视觉状态。

制作人新截图指出田地像逐格缩小的方块、固定议政厅仍是圆形占点。按 [300 返修](300-field-scale-and-council-hall-exterior-2026-10-18.md)，田地取四阶段图的内部垄沟/植株区域以去掉每格独立土边；预设 `controlCore` 换为封闭屋顶的议政厅，并按等比例靠南端落地。只属 Host 表现 Implemented / Producer Acceptance Pending；议政厅对敌自动攻击仍为 Proposed，未改战斗规则或 SiteCore authority。

制作人进一步提出议政厅屋顶仙术设施、近距自动单体远程攻击，暂以自身尺寸约 5 倍射程、20 伤害、3 秒间隔为意向。Host 运行图已加静态屋脊符阵；规则、伤害和发射特效**均未实现**。涉及建筑作为攻击者、独立 Encounter 与伤害归责的 [301 ACR](301-acr-council-hall-spirit-defense-2026-10-18.md) 待 Architecture/System Design 定案；不得将美术接线视为功能完成。

制作人已澄清：议政厅**仅在正式加入的战斗场景中**攻击本场敌方；平时不开火，不自行触发战斗。该触发范围已写回 301；建筑参战身份、场内伤害/冷却/存档接口仍待架构定案。

2026-10-19 [302 通用可见弹道](302-unified-visible-ranged-projectile-presentation-2026-10-19.md) 已把现有筑基纱衣远程普攻在独立 Encounter 中接到原通用弹道，并放缓飞行、增加短尾迹；Core 命中时点未改。当前炼气不具备同一远程**普攻**，议政厅静态符阵仍不发射；这些状态不可混写。

制作人再次现场确认议政厅在战场不发弹道，同时截图指出主角右键被同伴卡成慢步。按 [303](303-party-corridor-transit-and-council-hall-attack-status-2026-10-19.md) 查明议政厅并无建筑攻击事件，不能将静态屋脊符阵当功能；移动器的 30% fallback 位于累计穿行分支之前，致可行小步长期吞掉穿行。Host 已调整同队短暂穿行和通用 fallback 顺序，基础编译完成，实际走位待人工验收；301 ACR 仍需 Architecture 定案后才能实施议政厅攻击。

制作人复验 303 后仍被人物卡住，明确要求暂时全部人物互相穿行。[304 最新移动范围](304-unblocked-character-transit-2026-10-19.md) 已移除共用 Host 路径上的人物避让、慢步与终点占位阻塞，只保留 WalkGrid 静态障碍；普通右键下令时仍尽量找空位，任务与战斗本身的站位/合法性不变。状态 Implemented / Producer Acceptance Pending，303 移动策略已被替代；议政厅攻击继续 Proposed / Gameplay Not Implemented。

## LOCAL-CONTINUITY-FINAL（2026-10-16，待制作人验收）

同势力凡人的本地救援已从“同 Site”改为“同一 Continuous Surface + 精确同 Faction + 有界本地范围 + 真实路线／设施权限”。最紧急伤员先取得唯一救援者与稳定 BedId；医疗床、本人住宅床、空闲公共住宅床依次选择，进行中跨旗帜边界不重选，临时路径未准备有界退避，最终仍必须搬到床边原子安置。职业工区、分配住宅床、附近合法储藏室及指定牢床的发起资格不再错误依赖人物当前 Site；库存仍从锁定的实际来源 Site 扣除，囚犯交接仍以床所属玩家控制 Site 和最终床边到达为准。

Continuous Surface Host 现在以 `IsWorldPositionLoaded` 统一约束具体位置的蓝点、地点文字、世界物／洞口／控制核心拾取、菜单和检视外框。退场 chunk 的 presenter owner 先清理，再在同一 transition 尾部重建 overlay；“Site 部分加载”不再注册其未加载坐标的地点。该门槛只属于本地表现与现场交互，Core 地点、人物、床位、任务和医疗稳定数据不随镜头卸载。通用开局床位初始化已移除固定 20 人限制，继续校验 SpawnKey、住宅用途、重复人物和既有几何。当前实际包为 `Content/BaseGame`，LevelTester 使用 `base:scenario_playable_day` 与 `base:roster_level_tester`，主要运行地图为 `Data/Worlds/main_wilderness_surface_v1.json`，作者源为 `Authoring/ContinuousSurface/Worlds/main_world.worldcomposition.json` 及对应 Blueprint；本轮未修改 Content。

离线全程序集编译通过；按仓库规则未新增或运行自动测试，未启动 Unity／PlayMode／Runner。Snapshot 保持当前 v15。状态：**CIVILIAN-LIFE-01 / LOCAL-CONTINUITY-FINAL Implementation Complete / Producer Acceptance Pending**。

## 当前阶段人工验收工作单（2026-10-11）

完整操作表、填写栏和结果驱动的下一步建议见 [270](270-current-stage-producer-acceptance-plan-2026-10-11.md)。本次只读复核确认 CIVILIAN 囚犯食宿／两套日程缺口仍存在，并补充记录文本输入快捷键旁路与 legacy 输入标志覆盖；未实施修复。UI、CIVILIAN、DELAYED 的现有 Pending 状态不因验收表或静态核查自动改变。

## UI 折叠菜单与关闭修复（2026-10-11）

LevelTester 保留 74 个稳定 ToolId 与单功能页，左侧九组现以稳定 GroupKey 独立展开／收起；搜索临时展开命中项，清空恢复原折叠偏好。关闭按钮与标题拖动不再重叠，Hide 后不会从残留搜索焦点重新获取输入 owner，危险确认也会取消。人物页、任务日志、修炼页以及审计发现的斗技／背包／地图子面板当前帧续绘路径已收束到各自 Close 或安全返回。完整逐窗静态核查见 [269](269-ui-foldout-and-close-audit-2026-10-11.md)。状态：**UI 折叠菜单与关闭修复 Implementation Complete / Producer Acceptance Pending**；未启动 Unity，运行行为待制作人人工验收。

## LevelTester 工具整理与输入隔离（2026-10-10）

早先五大聚合页签方案已被制作人纠正并标记 Superseded。当前 LevelTester 使用左侧 74 个具体功能入口、右侧单一功能页和真实可点击搜索结果；稳定 ToolId、完整旧入口迁移与 handler／对象范围见 [268](268-level-tester-direct-tool-pages-2026-10-10.md)。owner-scoped 浮窗遮挡、Camera/WorldMap/Selection/世界命令共用命中、文本焦点门禁、固定目标/结果、Core 时间标识、正式 CameraRig 定位和 Reset 二次确认继续保留。状态：**LevelTester 独立小功能页重组 Implementation Complete / Producer Acceptance Pending**。CIVILIAN-LIFE-01 仍为 **Implementation Complete / Producer Acceptance Pending**。

## CIVILIAN-LIFE-01 P1 医疗搬运点与囚犯实际送达（2026-09-30）

荒村已增加 `base:workarea_huangcun_medical` 医疗点与容量 4、固定 PrisonerOnly 的 `base:workarea_huangcun_prison` 专用囚室。医疗点只接收所有正常可工作 PlayerFaction 凡人实际搬来的同势力弥留 Character；送达后不自动治疗，不存在 MedicalWard、床位、收治、出院或医疗 Snapshot。CapturedEscorted 由发起捕获的玩家主控真实押送，carrier 与 prisoner 实际进入囚室范围后 Core 才允许 Detain；两类运输中断均在 carrier 最后合法位置安全放下。Snapshot 保持 v14；正式 HUD 与 LevelTester 已接线。状态：**Implementation Complete / Producer Acceptance Pending**。未启动 Unity／PlayMode／Runner，未运行自动测试；静态编译不等同制作人验收。

## CIVILIAN-LIFE-01 当前交接（2026-09-27）

**Implementation Complete / Producer Accepted（整体范围）/ 未封板。** 已按 [267](267-civilian-life-01-design-freeze-2026-09-27.md)／[ADR-0044](43-decisions/ADR-0044-individual-mortal-faction-work-needs-and-captivity-direction.md) 接入真实个体凡人、Needs、Work/OffDuty、职业、人事、占领与俘虏招募；最终缺口已以真实 Character 搬运、Site 内资源托运及正式建筑／势力旗拆除工单接通。制作人已确认整套民生验收；历史逐项清单不再要求重复执行。

P1 忠诚修正：Runtime Loyalty 已从 MortalCivilianState 移至通用 `CharacterFactionLoyaltyLedger`，凡当前有 FactionMembership 的 Character 均按 CharacterId + 当前 FactionId 持有值；无势力显示“—”。人物概况、人事页、LevelTester、占领／拘留／招募共用该 authority。Snapshot 仍为未封板 v14，新增独立 loyalty entries，缺少新字段的 interim v14 拒绝；制作人尚未完成整轮验收。

P1 物理取食／正式工作 UI：普通自由凡人现在到当前合法 Site 的启用 StorageRoom 取 food，到达时才原子扣 SitePublicStock，再进入 Eat；无设施／无粮／不可达留下诊断。Detained 与 PlayerParty 原供给不变。正式 HUD“工作”面板按当前查看 Site 列人，活动显示统一读取 MortalCivilianState，职业命令仍由 SetProfession 权限守卫；专业工区仅允许当前 Site、同 owner/faction、tag 匹配目标。Snapshot v14 不变，取食任务读档重评估。

MovementIntent Host bridge P1：managed mortal 离开 FarmerWork／HerbFarmerWork 时，同帧释放旧 NPC-schedule farm worker 后由 HostNpcScheduleMover 消费新 intent；专用释放只取消农田表现 path，不清 Core intent，且 managed mortal 不再回退残留 WorkAction/Schedule。Core、Host mover 与 squad presenter 共用 `MortalCivilianMovementAuthority`：静态 singleton resident squad 让位于 civilian local movement，真实战略路线、多成员 FollowLeader follower、PlayerParty、Encounter 与 SeparateSpace 继续优先。StorageRoom runtime 从 authored／constructed footprint 解析最近外围 access point，再经小范围 walk-grid snap；Core 以 HostArrived 完成 pickup。Schedule 只决定 Needs 满足后的工作阶段，不是 movement owner；Snapshot v14 不变。

P1 食宿最终闭环：FetchFood 现有明确等待寻路／行走／失败／到达／进食阶段，来源、权限、设施或无粮失败保留原因并有界重试；Energy 极低时优先就地睡眠。返家与真实睡眠分离，行走期间不恢复 Energy，只有同一住所的 HostArrived 才开始床上睡眠。MovementIntent revision 会使 Host 取消被 Core 替换的旧 civilian path；暂停仍冻结世界，人物菜单只屏蔽玩家输入。civilian local 实际移动持续提交 Character WorldPresence，不再由静态 resident squad 覆盖。荒村 guard housing 门洞 source/baked 同步缩短一格，离线 walk-grid 复算确认 guard、储藏室、外部入口与住房连通。LevelTester 增加四个“准备食宿 A/B/C/D”入口与 path/tick/pause 全链诊断；Snapshot 保持 v14，仅补足睡眠阶段持久化，Food Fetch 行程仍读档重评估。

P1 显式驻军／失所者交接：Site assault 只读取 `SquadState.DefendedSiteId`，不再按同阵营与现场位置推断。荒村主管单人 Squad 显式绑定荒村；巡卫甲／乙／丙改为无绑定的独立 patrol squad，普通攻击仍会按三人 roster 进入 CharacterEncounter。Displaced 清除旧 Site 工作／居住／取食和瞬时行动；materialized 时留在现场 idle／基础 Needs，离屏后以 bucket 批量推进 Needs，并复用 BackgroundCharacterTravel 前往同 Surface 最近可达的己方 active Site。无目标时保留原位，到达后仍为 Displaced。Snapshot v14 保存 defense binding、refuge target、needs 结算 tick 及既有 background route；LevelTester 可直接查看两条 authority。

P1 恢复点／囚犯交接：Rescue 只向同 Site／Surface 的空闲 `recoverySpot` 送达，医疗区内两点优先、区外第三点仍有效；点位预订与单点单伤员 authority 防止竞争，送达不治疗。精确 RecoverySpot movement intent 携带 stable destination key 与 Core radius，Host 使用同一换算半径和 exact A*，换点会取消旧 revision/path。v14 顶层持久化恢复点送达关系。`base:site_huangcun:prison_dropoff` 位于囚室南侧内部入口；右键专门显示安置／查看囚室，carrier 实际寻路后才可通过共用 `CanDetain/Detain` 安置。

### Current Resume Order

§89 第 8／9 项的实现入口已补齐：LevelTester 可准备阿石搬运阿青、阿土治疗，以及当前 Site 木材／工人；建设仍由正式 UI 落点。当前没有正式 Repair 玩法；普通建筑拆除也未开放。不得扩张为泛化 NPC AI framework，也不得顺带启动 Generic Knowledge、Equipment/Crafting、Production/Logistics 或 Strategic Autonomy。

实现复用现有 NPC movement/work、LocationLabor、Site PublicStock、injury/recovery、CharacterEncounter、housing、FactionMembership、玩家永久 roster 与 PlayerParty 边界，以及正式 Site transfer。当前 Snapshot schema v14；住房保持现有可见表现，不引入 private presence／房屋黑箱。

状态链：SHOP-TRADE-01 与 AUCTION-01 均为 **Producer Accepted / Sealed**；CIVILIAN-LIFE-01 为 **Implementation Complete / Producer Accepted / Committed·Sealed（2026-10-20）**。

## AUCTION-01 最终交接（2026-09-27）

当前 Snapshot v14，v1～v13 严格拒绝。AUCTION-01 的独立 AuctionProvider/House、Listing 与双托管、抽象 Market review、结算/5% fee、Claims、正式 UGUI、验收内容和 Save/Load 已通过制作人人工验收，状态 **Producer Accepted / Sealed**。黄村拍卖行执事在 `(5.30,11.10)`；LevelTester 内容页可定位、给钱包/物品、Reset 并查看 runtime。人工结果见 [266](266-auction-01-auction-house-consignment-and-bidding-v1-2026-09-25.md)。Auction-02 与 Sect Contribution Exchange 未自动授权。

## SHOP-TRADE-01 当前交接（2026-09-25）

Shop/Market 与独立 Low/Mid/High 钱包已通过制作人人工验收并封板。按 [265](265-shop-trade-01-designated-shop-and-wallet-v1-2026-09-25.md) 新开局，黄村 `(5.14,11.10)` 找专用杂货铺掌柜；其状态与 AuctionHouse 分离。

## SOCIAL-QUEST-01 最终封板（2026-09-25）

**Producer Accepted / Sealed**。制作人已验收主流程和最终不可控、不可手动停止跟随 P1。

Temporary quest companion participates in party travel and combat, but is not a player-controllable character.
临时同行跟随受控队伍、随队进入 Separate Space、通过 NPC AI 参战并占用容量；可以选中查看，不能成为 ActiveCharacter、不能接受玩家手动战斗命令或普通 Stop Follow。可控性从永久 Character roster / 既有玩家势力管理 authority 派生，首先排除 QuestCompanion binding；UI、手动命令后端、自动 Active 候选和恢复共用判定。成员、空间和自动战斗 authority 保留。

当前 SOCIAL-QUEST-01：Producer Accepted / Sealed。下一阶段 Full Trading（本轮不启动）；之后 Equipment / Crafting → Production / Logistics → NPC AI / Strategic Autonomy last。Knowledge / Rumor / Information Propagation：Future / Only if gameplay later requires it。


> 2026-09-25 SOCIAL-QUEST-01 最终 P1：制作人主流程人工验收通过。临时同行仅参与移动/空间/战斗，不授予 Active 或玩家手动战斗控制；统一 authority、UI/backend guard、restore reconcile 已实施。Snapshot v11 不变；最终封板授权已由制作人给出。后续方向不自动开始。

## 2026-09-25 — SOCIAL-QUEST-01

**Producer Accepted / Sealed**。在未提交 DELAYED-EVENT-01 基线上完成 questKind、Active 秘境实例话题、NPC→Actor 门槛20、临时同行绑定、Party/Squad 跟随与安全离队、Snapshot v11、QuestEditor 与 LevelTester 验收入口。保留前轮改动；无 stage/commit/push，无 Unity/行为测试。详见 [264](264-social-quest-01-secret-realm-social-topic-and-temporary-companion-2026-09-25.md)。

当前路线：SOCIAL-QUEST-01 → Full Trading → Equipment / Crafting → Production / Logistics → NPC AI / Strategic Autonomy last。Knowledge / Rumor：Future / Only if gameplay later requires it。后续不自动启动。


## 2026-09-25 当前接续：DELAYED-EVENT-01

[263](263-delayed-event-01-content-event-scheduling-2026-09-25.md) 已完成独立延迟队列、原实例上下文、Outcome 事务回滚、安全串行呈现、Snapshot v10、EventEditor 增量与临时行商验收内容。状态 **Implementation Complete / Producer Acceptance Pending**；未提交、未封板。Snapshot 当前 v10，v1～v9 严格拒绝。

当前路线：DELAYED-EVENT-01 → SOCIAL-QUEST-01（Quest Social Topic + Invite NPC To Join / Participate）→ Full Trading → Equipment / Crafting → Production / Logistics → NPC AI / Strategic Autonomy last。Knowledge / Rumor / Information Propagation 为 Future / Only if gameplay later proves it necessary。下面旧 KNOWLEDGE-DELAY-01／Knowledge + delayed 的路线表述均已 superseded，不是当前授权。

## 2026-09-25 最新接续摘要：DYNAMIC-DISCOVERY-01＋MAP-COORD-01 FINAL SEAL

[DYNAMIC-DISCOVERY-01](261-dynamic-discovery-01-dynamic-worldobject-foundation-2026-09-24.md) 与 [MAP-COORD-01](262-map-coord-01-worldmap-world-coordinate-readout-2026-09-25.md) 已于 2026-09-24 完成制作人人工验收，状态均为 **Producer Accepted / Sealed**。`WorldOpportunity` 现同时支持既有 NPC 与不创建 Character Entity 的动态 WorldObject；动态物体以 Opportunity Instance 派生稳定 `WorldObjectInstanceId`，直接保存 `SurfaceId + exact WorldPosition`，由独立 Host transient registry 呈现与拾取，不进入导航阻挡。

发现模式固定为 `worldVisible`、`publicNotice`、`hiddenUntilDiscovered`。隐藏物体在 Domain 中先存在，任一存活 PlayerParty 成员进入 authored radius 后永久揭示；发现前不创建 Presentation、Interaction、Activity、Toast 或定位入口。onInspect 通过 `worldObjectKind=opportunityObject + worldOpportunityId` 绑定模板，`resolveCurrentOpportunity` 显式终结当前实例并与 Activity 变更共同参与 Outcome transaction；Inspect 本身不等于解决。

Snapshot 当前为 **v9**，持久保存 SpawnKind、稳定物体身份、精确坐标与发现状态；v8 及更早版本明确不兼容，不猜测迁移。OpportunityEditor、EventEditor 与 LevelTester 三条稳定验收物体已接通。制作人确认 Hidden 石碑的未发现隐藏、距离发现、调查保留、显式解决与 Activity 主链通过。WorldMap 另以同一 Surface 投影显示 pointer／player exact coordinate 与自适应 major ticks，不泄漏隐藏 Opportunity。当时 KNOWLEDGE-DELAY-01 方向已由顶部 DELAYED-EVENT-01 / SOCIAL-QUEST-01 顺序替代。

## 2026-09-24 已封板基线：PLAYER CONTROL CONTINUITY / SUCCESSION FINAL SEAL

制作人已完成人工验收并确认“这一轮很完美”。SUCCESSION-01、CONTROL-HANDOFF-01 与 CONTROL-HANDOFF-01-P1 均为 **Producer Accepted / Sealed**。现行控制连续性分三级：当前 Party 内仍有合法成员时按固定顺序接替；无可控成员但仍有生者时走 Emergency External Handoff，旧生者留在原地 idle Recovery Squad；全员 Dead／Removed 时走 True-Death Succession，旧成员保持 corpse／singleton authority。外部候选共用统一 policy，以 CombatPower 最高、稳定 EntityId tie-break，并在 membership mutation 前捕获真实 `SurfaceId + exact WorldPosition`。

接管者若来自 NPC Squad，只拆本人并保留其余 roster／motion。跨 Surface 或远出旧 loaded 5×5 时以接管者 chunk hard re-anchor；目的地 Surface、邻域 Site／Squad／Population 与 successor EntityView 同步就绪后才切 Camera／Selection。CharacterEncounter `Preparing / Active / ReadyToEnd` 持有 spatial authority；`Committed` 仅保留战报，不阻断 ordinary world，`CloseReport` 不承担世界重载。Separate Space handoff 不执行正常 Leave，旧伤员／尸体保持洞内持久位置。该封板时点使用 Snapshot v8；当前全局 schema 已由 DYNAMIC-DISCOVERY-01 提升为 v9，控制连续性字段 shape 未变。

旧“只有全员真正死亡才允许切到势力其它人物；全员弥留保持 TemporarilyUnavailable”的规则已标记 **Superseded on 2026-09-24**，现行真源为 [ADR-0039](43-decisions/ADR-0039-external-faction-control-handoff.md)、[2K §4](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[259](259-succession-01-player-faction-succession-2026-09-24.md) 与 [260](260-control-handoff-01-emergency-faction-control-transfer-2026-09-24.md)。DYNAMIC-DISCOVERY-01 的当前状态见本页顶部；NPC AI 继续最后。

## 2026-09-24 实施时点历史：CONTROL-HANDOFF-01（状态已由顶部 Final Seal 取代）

制作人修订 SUCCESSION-01 最终产品规则：[ADR-0039](43-decisions/ADR-0039-external-faction-control-handoff.md) 将外部势力控制转移统一为 Emergency Takeover 与 Succession。当前 Party 无任何可控成员但仍有生者时，如果玩家势力存在外部合法人物，也会在战斗正式提交后接管；旧生者保留在原地 idle Recovery Squad，Dead／Removed 继续 singleton/corpse retirement。无候选时不拆旧 Party，等待恢复。

实现记录见 [260](260-control-handoff-01-emergency-faction-control-transfer-2026-09-24.md)。候选筛选、CombatPower/EntityId 排序、精确位置、Surface re-anchor 与 Separate Space presentation release 只有一套；Emergency 使用独立 DomainEvent/Toast。Snapshot 仍为 v8。LevelTester “战斗”新增“CONTROL-HANDOFF-01：使当前 Party 全员弥留”。**历史实施时点状态**为 Implementation Complete / Producer Acceptance Pending；当前状态已由本页顶部 Final Seal 更新为 Producer Accepted / Sealed。

P1 已修正战后即时目的地物化：CharacterEncounter `Committed` 仅保留战报，不再阻断普通 Continuous Surface；独立战场退出在等待外部接管时跳过旧 Party Surface 重建，并由唯一 Host 呈现门槛同步完成 C 的 Surface、邻域人口与 EntityView 后再切 Camera／Selection。真正死亡旧成员 singleton 注册保持每人一次，双死亡回归已覆盖。无需关闭战报或点击 HUD 头像触发刷新。两项状态仍为 **Implementation Complete / Producer Acceptance Pending**。

## 2026-09-24 实施时点历史：SEAL-20260924 / SUCCESSION Final Audit（状态已由顶部 Final Seal 取代）

QUEST-INSTANCE-01、VASSAL-WORK-01 与 VASSAL-WORK-01-P1 已由制作人人工验收并正式 **Producer Accepted / Sealed**。P1 的最终 opening context-menu 问题属于 Host Active/View/Selection 初始化时机，并非 Direct Vassal Work Authorization 失败。

SUCCESSION-01 主体审计通过，未重建系统。仅修正来源 NPC squad 在 successor 同时为 command target 时的悬挂目标；无候选扫描从逐帧改为 0.75 秒 throttle，首次进入／显式刷新／Snapshot restore 仍立即；LevelTester 护卫限定玩家势力。候选、精确位置、远距无捕获 Surface re-anchor、物化后 Camera/Selection、Separate Space wipe 与 Snapshot v8 行为保持。**历史实施时点状态**为 Implementation Complete / Producer Acceptance Pending；当前状态已由本页顶部 Final Seal 更新。

## 2026-09-24 最新接续摘要：VASSAL-WORK-01-P1

制作人实际验收证明 Direct Vassal Labor Authorization 已正确生效；残余故障是 Host opening selection 时机。Continuous New Game 曾在 Active EntityView materialize 前选中，Registry gate 令其失败，最终物化后未补选；Context Gate 接受空 selection，但农田旧入口没有 Worker。

P1 已在最终 opening population Refresh/Prune/Spawn 后仅为空 selection 补选已物化 Active，并把 party-work actor 语义集中到 `HostPlayerMoveCommandGate`：合法非空选择使用所选 Party members，空选择隐式使用可行动 Active，非法非空选择不回退。农田开工为 0 时会显示明确反馈。Snapshot restore 既有 rebind 未改，Vassal Work Authorization／仓储／建造／收获归属均未改，Snapshot 仍为 v8。状态 **Implementation Complete / Producer Acceptance Pending**；离线编译 `ALL_OK`，未运行自动测试或 Unity，未 stage／commit／push。

## 2026-09-24 最新接续摘要：VASSAL-WORK-01

[VASSAL-WORK-01](260-vassal-work-01-direct-vassal-labor-access-2026-09-24.md) 已完成实施，状态 **Implementation Complete / Producer Acceptance Pending**。严格 `WorldAdministrativeAssetAuthorizationService` 未放宽；新增 Work Authorization 只允许实际管理势力自己或其直接附庸劳作，并明确区分 `AllowedAsManager`／`AllowedAsVassalWorker`。玩家开工、逐格复核、Host Hover、NPC WorkArea 与 NPC schedule 收获均已统一接线。

Ch01 真实 opening 已有 `base:faction_player → base:sect_huangcun_labor` 直接附庸，故现有荒村宗主粮田／药田会自然开放劳动，不含 ID hardcode。玩家手控产物仍进背包，NPC schedule 产物仍进宗主实际 Site 公库；宗主仓储、建造、拆除、管理、住房、时间表和 ControlCore 继续隔离。解除附庸或 War 后权限即时失效。Snapshot 仍为 v8；离线编译 `ALL_OK`，未运行自动测试或 Unity，未 stage／commit／push。完成本项人工验收后继续既有 SUCCESSION-01 验收。

## 2026-09-24 实施时点历史：SUCCESSION-01（状态已由顶部 Final Seal 取代）

QUEST-INSTANCE-01 已由制作人人工验收通过并封板，正式状态为 **Producer Accepted / Sealed**。旧文档中的 Pending 是实施时点历史，不回写。当前最新实现是 [SUCCESSION-01](259-succession-01-player-faction-succession-2026-09-24.md)：只有旧 PlayerParty 全员真正 Dead/Removed 才从玩家势力其它真实人物中选择继承者；战力最高优先、同战力较小 EntityId 优先，普通队内 Active 接替仍保持固定 Party 顺序。

继承者在改变 Squad 前捕获精确 Surface/WorldPosition/Site/来源 Squad；旧死亡 Party 退为 singleton，新 `squad:player` 仅含继承者。NPC squad 只分离继承者。Host 对跨 Surface 或超出 loaded 5×5 的位置执行无旧位置捕获的 hard re-anchor，并在 EntityView 就绪后镜头/选中。Separate Space 全灭只释放玩家表现 ownership，不把尸体送回洞口。无候选继续保持 AwaitingSuccession，可存档并在 world shell 恢复后重试。Snapshot schema 仍为 v8。

LevelTester 反引号开发工具 →“战斗”提供 SUCCESSION-01 A/B 候选准备与正式全灭按钮。该段记录的**历史实施时点状态**为 Implementation Complete / Producer Acceptance Pending；当前状态已由本页顶部 Final Seal 更新。后续顺序为 Dynamic WorldObject / Discovery → Knowledge + delayed event foundation → Full trading → Equipment/crafting → Production/logistics → NPC AI last。

## 新会话恢复摘要（2026-09-25；最新状态以顶部 Final Seal 摘要为准）

### 项目与阶段

PJCultiva／XianXia 是一款以**具体角色的修仙成长**为核心，结合同行小队、连续探索、人物关系、实时暂停战斗和领地经营的单机 2D RPG。SHOP-TRADE-01 与 AUCTION-01 均为 **Producer Accepted / Sealed**。当前 Snapshot schema 为 v14；CIVILIAN-LIFE-01 为 **Implementation Complete / Producer Acceptance Pending**，人工验收入口见本页顶部。

### DYNAMIC-DISCOVERY-01 当前实现（2026-09-24）

[261](261-dynamic-discovery-01-dynamic-worldobject-foundation-2026-09-24.md) 增加动态 Opportunity WorldObject、三种发现模式、稳定模板 onInspect、显式 transaction-safe resolve、Activity 生命周期和 Snapshot v9。隐藏物体发现前不泄漏任何 Host 表现或 Activity，发现后永久可见并可定位；worldVisible 不入 Activity，publicNotice 保持即时公开。OpportunityEditor 与 EventEditor 已覆盖全部新字段，LevelTester 提供三条稳定验收实例与清理入口。状态 **Producer Accepted / Sealed**。

### QUEST-INSTANCE-01 当前实现（2026-09-24）

[258](258-quest-instance-01-dynamic-character-commissions-v1-2026-09-24.md) 将人物委托从“模板 ID＋全局 flag”升级为稳定 `QuestInstanceId`。同模板 NPC A／B 可分别接取、交付、领奖、放弃、失效和存读档；`@actor`／`@target`／`@issuer` 精确指向真实 Entity，关系继续写入 `RelationshipLedger`。当前 Snapshot schema 为 v8，v1～v7 要求新开局。固定任务保留既有模板单份语义。LevelTester 内容页可确定生成两名同模板临时行商。制作人已于 2026-09-24 完成双实例完整路线人工验收，状态 **Producer Accepted / Sealed**。

### SAVE-01＋STRATEGIC-STOCK-01 当前封板（2026-09-24）

[257](257-save-01-content-progress-persistence-v1-2026-09-23.md) 的 Content Progress 与战略库存封板继续有效；QUEST-INSTANCE-01 已把当时两条 prototype flag 流替换为结构化人物委托。当前 Snapshot 为 v9，v8 及更早版本明确拒绝；ContentProgress 与资源访问 shape 未变，仍保持 PartyInventory＋可访问战略库存的既有语义。

### Continuous Surface Streaming Radius 2（2026-09-23）

Core `ContinuousSurfaceStreamingPolicy` 是唯一 active-neighborhood policy：radius 2、diameter 5。`ContinuousOutdoorSurfaceRuntime.InitializeNeighborhood`、`RequestNeighborhoodTransition` 与 `ContinuousOutdoorStartupPlanner.TryPreflightNeighborhood` 共用该值；完整邻域 25 chunks，authored edge 可少于 25。相邻 crossing 仍 staged 且每 Update 最多 Build 1，典型新增 5 chunks；initial activation／Snapshot presentation rebuild 最多同步 Build 25。Chunk metric、player-centered authority 与 GridPathfinder 算法未改变；当前 Snapshot schema 为 9。

### EVENT-02A 当前封板（2026-09-23）

[256](256-event-02a-persistent-world-activity-feed-2026-09-23.md) 将 `publicNotice` 从 Toast-only 改为持久 `WorldActivityBoard` authority：Active／History、unread、最近 100 条与严格 Opportunity source 一致性。其原字段 shape 在当前 Snapshot v9 中保持；DYNAMIC-DISCOVERY-01 另使已发现 `hiddenUntilDiscovered` 进入 Activity，并让动态物体定位使用实例精确坐标。worldVisible 仍不入栏，Toast 仍只在 Activity 首次创建时播放一次。EVENT-02A 原范围已 **Producer Accepted / Sealed**。

### EVENT-02 当前封板（2026-09-23）

[255](255-event-02-world-opportunity-director-v1-2026-09-23.md) 建立独立于旧 `OpportunitySite` 的动态 NPC 机会链：当前 Surface 每日低频 refill、SpawnTable 单抽、合法可走精确坐标、真实 NPC/WorldPresence、通用 Continuous materialization、EVENT-01 onTalk 与到期 Removed。该 V1 范围已 **Producer Accepted / Sealed**；“不支持 hidden 或动态 WorldObject”是当时范围，现已由 DYNAMIC-DISCOVERY-01 的 additive 扩展取代。当前 Snapshot v9 同时保存 NPC 与动态物体实例。

### EVENT-EDITOR-V2 当前增补（2026-09-23）

[254](254-event-editor-v2-visual-flow-authoring-2026-09-23.md) 将 EventEditor 改为 Browser＋Visual Steps Graph＋Inspector。Event 仍是独立 Template；按对象只是 binding projection。Working copy、dirty、undo/redo、自动布局、错误定位与 `Authoring/EventEditor/layouts.v1.json` 已接通。EVENT-02 后续增加 tag／Opportunity onTalk binding 与通用人物模板投影；Graph authority 未变。当前 narrative runtime 已进入 Snapshot v8。

制作人已完成 Graph、Step/Choice 连线、inline authoring、Priority/Topic/Repeat、保底与 Once、dirty、全局人物来源、可读 Character Picker、NPC／WorldObject authoring 的实际验收。最后剩余的“重启后不恢复人物来源”已由 final patch 以 editor-local、按 Package Root 的 `%LOCALAPPDATA%\XianXia\EventEditor\settings.json` 关闭；当前状态为 **Producer Accepted / Sealed**，不继续拆 V2.7/V2.8。

V2.1 在原 Graph 上补齐 input/output port、Bezier 箭头连接、连接选择/删除、拖空白选择 Speaker 后创建、节点内 Speaker/正文/Choice 编辑、按内容估算间距与专注模式。仍只写同一 Steps working copy 和 editor-only layout；Runtime、ContentEvent schema、Priority/repeat/Host dialogue 均未改。状态仍为 **Implementation Complete / Producer Acceptance Pending**。

V2.2 修复打开/切换 Event 时旧 Inspector 控件误写新 Session 的 dirty 根因，并把 dirty 改为 Working JSON＋layout 相对 clean baseline 的差异判断；Undo/Redo 或手工改回 baseline 会恢复 clean，新建草稿首次保存前保持 dirty。Browser fallback 分类收紧为无条件、可重复、Priority 0 的 onTalk；五条将老条件状态对话统一 Priority 10。未改 Runtime resolution 或 Snapshot。

V2.3 进一步确认 sparse JSON 默认字段才是残余 false dirty 根因：新增 editor-only document normalizer，在 Existing/New/Legacy Convert 的 Session baseline 之前 canonicalize Event/Step/Choice 默认字段；optional binding 仍保持 sparse。Graph refresh 改为只读 layout，不再在 render 阶段补节点或 prune。Runtime Loader、schema 与磁盘源内容的自动写回规则未改。

V2.4 删除“状态对话/特殊对话/普通对话”等伪类型，只保留由现有字段组合表达的显式保底：onTalk、具体 NPC、P0、repeatable、无 Event Conditions。Inspector toggle 负责约束字段，同 NPC 唯一性在创建/切换与保存验证两层检查；Browser 改为“★ 保底”加 Priority/Conditions/Repeat factual badges。EVENT-01 acceptance 内容经引用审计仍自包含，但专项 Pending，故未删除。

### EVENT-01 Final 当前增补（2026-09-22）

[253](253-event-01-final-fixed-world-interaction-acceptance-2026-09-22.md) 是固定世界 NPC onTalk＋WorldObject onInspect、统一稳定 TargetKey、对象接近后复核、EventEditor 绑定及 Load definitions-only 的实施记录与人工验收入口。状态 **Implementation Complete / Producer Acceptance Pending**。沿用既有 ContentEvent／Outcome transaction／HostDialogue；旧 body／choices 兼容；Priority／Topic、整组 party-aware 条件、Step 与 once scope 均为 session runtime。EVENT-02 已在其上增加动态 NPC binding；ContentIntent 与通用剧情 runtime persistence 仍未实现。

### 基线、验收与 Git

- 当前对话审查基线：`Scripts(20260922-042034).zip`。制作人明确说明：042034 与 040154 解包后的文件内容一致，**不是**又实施一轮代码修改。
- 021915 完成主要 Hex／Army 正式运行依赖删除；040154 完成旅行契约、Host 物化刷新、序列化字段映射、当前数据分阶段恢复和孤立链删除等统一收尾；042034 复核未发现需要重开专项清理的实质问题。
- 状态分层：设计边界 **Design Confirmed**；上述代码 **Implemented**；制作人人工验收明确通过，故为 **Producer Accepted**；封板提交为 `9b32fe0f06d9e838e1e0be6ea40f5be50b239e01`（`chore: seal Hex/Army runtime retirement and cleanup`），故专项 **Committed / Sealed**。
- 当前 branch：`dev_openworld`。本轮文档收口开始前，封板提交后工作区只保留两份无关未提交改动：`Assets/Scripts/Unity/Host/HostBreakthroughRitual.cs`、`Assets/Scripts/Unity/Host/HostSkillStudyRitual.cs`。当前工作区还包含本轮文档／协作规范更新；两份 C# 不属于本轮，不得回滚、覆盖或顺带提交。
- 此前 82/82、离线编译与 converter 5/5 是实施阶段的历史执行记录，不是本次文档收口重跑，也不能代替制作人人工验收。

### 已封板的运行边界

- 普通 Outdoor 位置与旅行 authority：`SurfaceId + exact WorldPosition + Continuous Surface`；当前 movement budget scale 来自 opening `outdoorSurface.movementScale`，不读取 `LegacyHexWorld`，也不等同于 `cellSize`。
- 正常活动人物组织：`Squad`；玩家投影：`PlayerParty + ActiveControlledCharacter`；NPC group 运动：`SquadWorldMotion`。singleton Squad 身份不自动等于物理移动 ownership。
- 现代战斗：`CharacterEncounter` + 真实 Character participants + 独立战术坐标 + source／return world anchors。世界时间与战斗时间按已冻结规则分离。
- 据点与行政：`WorldSite + SiteCore + TerritoryClaim + Actual Administrative Control`；Surface 范围查询不以 Hex footprint 兜底。
- 真正独立空间：`LocalMapSession`／Separate Space（当前样板为洞府）；普通城市／户外 Site 不因此切换 LocalMap。
- 旧 Hex 世界、旧执行器和旧 Army runtime migration 不再承载正式 Gameplay。残留的旧格式识别／拒绝、稳定 JSON key／ID／数值、`FormerlySerializedAs`、独立转换工具和历史文档不等于旧运行系统仍在。

### 不应重新开启

没有“继续找 Legacy／Hex／Army”的默认任务。不得因历史文档、协议键、保留枚举值、旧序列化字段名或离线转换器仍含历史词语而重开扫描删除；不得恢复旧 Hex／Army runtime adapter。下方 Historical Resume Snapshot 与旧里程碑只记录当时事实。

### 当前限制与未核实事项

- 本封板不表示全项目完成、所有历史存档均可直接加载、所有历史字符串消失或项目无缺陷。
- 系统现状以本文下方“当前系统现状总表”为准；表内 `Proposed / Not Implemented` 不构成开发授权。
- SAVE-01 已选择“Active Event 期间禁止保存”，并实现通用任务／剧情状态磁盘持久化；当前只等待制作人人工验收，不是未授权 Proposal。
- Snapshot restore 的 Quest／ContentEvent／Chapter definitions shell 不重放 opening；SAVE-01 在 definitions rehydrate 后校验恢复引用。Separate Space Session JSON wire 继续沿用既有 authority。WorldMap 的 Site／Flag marker 随 world projection 缩放，而 Player／NPC Squad marker 仍固定像素，仍是未授权差异。

### 新会话短阅读顺序

`AGENTS.md` → [00 overview](../00-project/00-overview.md) → 本节与[当前系统现状总表](#当前系统现状总表2026-09-22) → [31 architecture](../30-tech/31-architecture.md) → 下一议题对应的少数 `20-systems` 正文与源码入口。无需顺序重读全部历史 devlog。

### 执行与验收规则

遵守 `AGENTS.md` 与 [52 协作规范](52-ai-collaboration-protocol.md)：一轮一份明确指令；不自行运行测试或启动 Unity；代码只做基础编译与最少静态检查；编译、静态核对、人工验收和提交状态分开；未经制作人确认不自动编码；普通实施不自动提交，“封板”才授权选择性 commit，默认不 push／tag／amend／reset／clean。

> **当前边界：** Runtime Loader 只接受当前 `outdoorSurface`／`npcSquad`；旧 `formalArmy`／`hexWorld` 明确拒绝。`LegacyRuntimeConverter` 只读输入并写不同且尚不存在的独立副本，但只无损转换 FormalArmy 与 current authority 完整的 hybrid Snapshot；`hexWorld`／`openingHexWorldId` 只检测并拒绝，须走现有 WorldComposer／SurfaceAuthoring Legacy migration 路径且无样例时不猜。旧 Content 的 `squad:migrated:`／`squad:legacy:` 与旧 Snapshot 的 `squad:army:` 三规则保持分离；稳定 wire key、数值空洞与 ID 只用于检测／离线转换。

## Current implementation and verification — 2026-09-22

- **021915 统一处置表：**

| ID | 当前消费者／数据 | 决定 | 验证证据 |
|---|---|---|---|
| PP-PLAN | Surface Host 路径缓存、取消／到达／恢复 | 恢复单次逻辑操作一次 `TravelPlanVersion` 失效；逐帧更新不增；重放置清旧 Site context；取消保留当前位置；到达保留目的 Site；零 consumer 的全清 `Clear` 删除 | `PlayerPartyWorldMotionCurrentContractTests` 5/5 |
| HOST-SCOPE | chunk／到达／成员／生命状态／读档／Encounter／Separate Space | `None`／`RefreshViewsOnly`／`ReconcileAndRefresh` 三态合并；generation 已完成 Runtime reconcile 时只同步 Host Views；dirty 的先后顺序有独立 generation 证据 | `OutdoorEntityReconcileGateTests` 6/6；静止 tick 回归 |
| SNAP-CURRENT | exact presence、PlayerParty、Squad、Background、Flag／Site／Claim、Encounter、Separate Space | Continuous Outdoor 必须有精确 Surface；合法非物理 Site 可无坐标；idle background 空 Surface 只可由现成 exact personal authority 归一；Interior／Encounter 不强行户外化 | current Snapshot／opening／Site／Encounter 定向矩阵；本机 `vs04_slot0.json` 完成 Core + content-dependent phase-two restore |
| DEAD-CHAIN | placement trace、旧 Site access／constants／grid wrapper、playable bounds、旧 map stub／catalog／visibility尾部 | 全仓零正式 root 后连 `.meta` 删除；洞府 placement、当前可见性、独立战与 `LogSnapshot` 保留 | Core／Data／Unity／Tests／Player assemblies 全量离线编译；符号与 GUID 扫描 |
| API-NAME | 空 residual stop、无效 out／参数、CoPresence enum、诊断文字 | 删除空壳和参数；`LocalMapOccupants = 2` 保持数值；稳定 wire／ID／DTO 不变 | 编译 + 直接调用扫描 |
| CONTENT-ASSET | BaseGame Surface／NpcSquad、movement scale、旧转换、SerializeField | `movementScale 1.0` 与 `cellSize 0.028` 分离；opening Surface 唯一注入；converter fail-closed；镜头字段以 `FormerlySerializedAs` 保值 | Content tests、预算公式测试、converter 5/5、Scene／Prefab／GUID 静态检查 |

- **核心入口：** `Assets/Scripts/Core/Simulation/SimulationWorld.cs` 仅持有 `ContinuousWorldMovementScale`；`Assets/Scripts/Core/World/ContinuousWorldMovementScale.cs` 只读该字段。`Assets/Scripts/Data/Bootstrap/ContentRuntimeBootstrap.cs` 从当前 opening Surface 的 `movementScale` 唯一注入。
- **Content 边界：** `Assets/Scripts/Data/Content/ContentPackageLoader.cs` 拒绝 `hexWorld`／`formalArmy`；`Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json` 显式 `movementScale: 1.0`。该值不是同文件的 `cellSize: 0.028`。
- **离线入口：** `ExternalTools/ContentAuthoring/LegacyRuntimeConverter/` 只转换 FormalArmy Content 与 current authority 完整的 hybrid Snapshot；不会覆盖输入，也不生成同路径输出。`hexWorld`／`openingHexWorldId` 在写文件前拒绝并转介 WorldComposer／SurfaceAuthoring Legacy migration 路径。
- **已删除链：** Core Hex 目录、旧执行器、Outdoor LocalMap／footprint／SurfaceExit／Host 接线，以及 Site／Flag／Encounter／SeparateSpace current 链的 Hex 参数／缓存均已退出。
- **此前实施阶段实际执行记录（不是本次封板重跑）：** Core／Data／Unity／Unity.Editor／Tests／PlayModeTests／Assembly-CSharp／Assembly-CSharp-Editor 离线编译 `ALL_OK`；021915 当前行为矩阵 82/82；ContentAuthoring solution 0 warning／0 error，converter 5/5。本机 `vs04_slot0.json`（36 entities、28 Squads、6 motions、20 exact presences、11 flags、11 runtime sites、17 claims）完成 serializer、Core restore、Content shell、Surface Site、PlayerParty／Background route、政治状态与 Squad motion 二阶段恢复。
- **封板：** 制作人已确认本版人工验收通过。本次封板只做规范与状态文档更新及选择性 Git 提交；不重跑编译或任何测试，不启动 Unity。本专项 **Producer Accepted / Sealed**。不宣称全项目无缺陷，也不宣称历史 Hex／Army 字符串已清零。

<a id="当前系统现状总表2026-09-22"></a>
## 当前系统现状总表（2026-09-22）

本表只写当前可用能力与明确边界。状态列中的 `Accepted / Sealed` 仅引用已有人工验收和 Git 记录；未列明者不得自动提升为已验收。源码路径用于恢复上下文，不表示需要重新审计全仓。

### A. 世界、位置与地图

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| Continuous Surface 与 Runtime Chunk | `OutdoorSurfaceSpatialAuthority`、`SurfaceGroundNavigation` 与 exact `WorldPosition` 承载户外地形、通行、道路／桥／森林输入和 chunk materialization；`SimulationWorld.ContinuousWorldMovementScale` 由 opening `outdoorSurface.movementScale` 注入 | `Assets/Scripts/Core/World/Surface/`；`Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json`；动态状态进 `WorldSnapshot`，静态地理留在 Content | Implemented / Accepted / Sealed。自动水文、道路 A*、detail scatter、完整大陆内容覆盖与 Runtime Chunk profiling 仍是 Future |
| WorldMap | `HostWorldMapPanel` 查看同源 Surface、exact 选点、缩放／平移、Site／Flag／Party／NPC Squad／控制范围；Site／Flag／Party／NPC Squad 图标按同一 world projection 缩放，Header／Flyout 固定屏幕空间；打开时只规划，关闭后恢复 Surface travel | `Assets/Scripts/Unity/Host/HostWorldMapPanel.cs`；不保存第二套地图位置，只提交 `PlayerPartyWorldMotion` 旅行计划 | MAP-02/03 Accepted / Sealed；2026-10-19 Player／NPC Squad 固定像素图标差异修正获制作人现场验收，状态 Producer Accepted、尚未封板提交 |
| PlayerParty 移动与同行 | 单 `ActiveControlledCharacter`；PlayerParty 上限 6；Followers 为 AI；`PlayerPartySurfaceTravelService`／`PlayerPartyWorldMotion` 以 Surface route 移动；取消保留位置，重放置清旧 Site，到达保留目的 Site | `Assets/Scripts/Core/World/Strategic/PlayerPartyWorldMotion.cs`、`PlayerPartySurfaceTravelService.cs`；Snapshot 保存位置、目的地、计划语义并在 Surface shell 后恢复 route | Implemented / Accepted / Sealed。飞舟、跨大陆新能力与完整继承终局未实现 |
| NPC Squad 与独立人物后台移动 | 正常 group = `Squad + SquadWorldMotion`；独立人物可由 background travel authority 移动；loaded/unloaded 只改变表现与 LOD，不改身份／位置 authority | `SquadState.cs`、`SquadWorldMotion.cs`、`BackgroundCharacterTravelService.cs`；Snapshot + Content-dependent phase-two restore | Hex／Army retirement seal 已覆盖。完整战略 AI／NPC 主动战争仍 Future |
| 表现物化与刷新 | Host 只根据 presentation scope materialize；`OutdoorEntityReconcileGate` 合并为 `None`／`RefreshViewsOnly`／`ReconcileAndRefresh`，避免静止 tick 重扫并保留真实 scope 变化 | `Assets/Scripts/Unity/Host/OutdoorEntityReconcileGate.cs`、`PlayableHostBootstrap.cs`；运行 generation 不作为磁盘 authority | Implemented；021915 统一收尾 Accepted / Sealed。性能 profiling 未核实，不作为当前 bug |
| 世界／Interior／Encounter 位置恢复顺序 | `EntityLocationSnapshotAuthorityComponent`、character presence、PlayerParty／Squad motion、Separate Space／CharacterEncounter 分层恢复；Host presentation 优先级为 Separate Space → Encounter → Outdoor | `WorldSnapshot`、`SnapshotService`、`StrategicSnapshotHelper`、`HostSnapshotSessionRehydration` | Outdoor／Encounter spatial 主线已封板；Separate Space session JSON wire 缺口见 D 表。不保证任意旧档直接兼容 |

### B. 小队、战斗与人物状态

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| Squad／PlayerParty／主控 | 每个正常活动人物属于唯一 Squad，单人也是 singleton Squad；PlayerParty 是玩家 Squad + Active 的控制投影；切换 Active 不改 Faction／HomeSite／关系 | `SquadBoard`、`PlayerPartyState`、`PlayerPartyTransitionMembership`；Snapshot 保存 membership 与 active identity | Unified Squad 主线 Accepted / Sealed。singleton Squad 不自动拥有某个共用物理位置 |
| CharacterEncounter | 第一击前统一确认；真实初始双方、有限候选、临时独立战术坐标、世界停表／战术时间、ReadyToStart／Active／ReadyToEnd／结算／返回；只恢复各自战前 world anchor，保留战果 | `CharacterEncounter.cs`、`HostCharacterEncounter.cs`；`WorldSnapshot.CharacterEncounter` + participant origin／return／tactical fields | Current 主线 Accepted / Sealed。NPC 对 NPC 大型战争、无限远援与大军实时战不在当前范围 |
| 独立战场与战报 | 使用接战地点同源地形／关键建筑 materialize；正式参与者决定控制、胜负与战报；最终结算唯一 | `ContinuousOutdoorEncounterField`、CharacterEncounter settlement/report services；Snapshot 在可保存 phase 保存，Preparing 明确禁止存档 | 已实现当前范围。完整俘虏／赎金、全面战争为 Future |
| 弥留、死亡与遗体 | `Incapacitated`、`Dead`、`Removed` 分离；Dead ≠ Removed；失能主控优先按 Party 顺序接替；residual／corpse 保留人物真实 Surface／空间责任 | `CombatLifeStateService`、entity lifecycle、world presence／residual services；Snapshot 保存 lifecycle、位置与相关 Encounter 状态 | 当前链已实现并纳入封板；玩家势力无人终局、完整继承／丧葬等未实现 |
| 私人冲突、关系介入与 War | 攻击人物不自动宣战；关系只决定有限介入资格；攻击势力有效拥有的建筑才走对应战争授权；永久关系 authority 是 `RelationshipLedger` | `CharacterEncounterHostilityService`、relationship services、Faction／Claim boards；关系事件已进 Snapshot | 设计与当前主线已确认。更完整外交、战争升级和 NPC 自动战争仍 Future |

### C. 据点、建设与生活

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| WorldSite／SiteCore／控制 | Permanent Site 使用固定 Council Hall core；FactionFlag 可创建可拆／可毁 runtime Site；Owner、TerritoryClaim 与 Actual Administrative Control 分离；范围改变不移动人物 | `WorldSite`、`WorldSiteBoard`、`FactionFlagService`、`WorldSitePhysicalRegionQuery`；Snapshot 保存 runtime sites、flags、claims | Implemented / Accepted / Sealed。SiteCore、Owner、Actual Control、建设权限与资产状态不得混写 |
| 建设与范围显示 | `ConstructionCatalog` + `ConstructionService` 事务性消费 PartyInventory，当前支持势力旗、FarmField、RecoverySpot、StorageRoom 等真实 outdoor constructed assets；footprint 必须落在发起势力 Actual Control；可主动拆除返料 | `Assets/Scripts/Core/Construction/`、`HostConstructionPanel` 与各 placement presenter；constructed assets／farm plots／Inventory 进入 Snapshot | 2L Implemented / Producer Accepted / Sealed。当前不含完整建筑产权、Core upgrade 产品 UI、通用建筑战争或通用 house／workshop 建造 |
| 农田、劳动与 Site 经济 | WorkArea／Job／Schedule、农作逐 tick 消费当前行政授权，收获进入 Site inventory／treasury 相关 current board | `Assets/Scripts/Core/Labor/`、Strategic economy services；Content 位于 `WorkAreas/`、`Jobs/`、`Schedules/`、`SiteEconomies/` | 当前样例链已接通；更完整仓储物流、税赋、跨 Site 运输、离屏生产未实现 |
| 接管与资产保持 | 接管改变政治／行政 authority，不传送人物、不自动删除建筑／库存／日程；拆可移除旗不等于清空 Site 资产 | Site warfare／capture services、FactionFlag／Claim restore | Current 主线 Accepted / Sealed。产权、继承、战争升级和普通建筑战争扩展仍需另议 |

### D. 探索与独立空间

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| 洞府发现与显形 | 当前 cave 样板支持靠近提示、神识 Survey、`KnownSites` reveal；Survey 只更新知识和表现，不提前加载 Interior 或移动 Party | `ExplorationService`、`OpportunityEntranceRules`、`KnownSitesComponent`；KnownSites 作为实体组件随当前 entity Snapshot 保存 | SPACE-01 cave 流程 Accepted / Sealed。其他秘境／遗迹不因设计存在而视为已有正式内容 |
| 进入、同行与内部状态 | `SeparateSpaceTransitionService.Enter` 验证入口并捕获 exact outdoor return；当前真正随队成员全部 transition；Interior 只 materialize occupants／当地居民；内部战斗原地进行 | `LocalMapSession`／SeparateSpace session、`PlayerPartyTransitionMembership`、`SeparateSpaceCombatPolicy` | Implemented / Accepted / Sealed。普通 Outdoor Site 不使用这条链 |
| 离开、回程与存读档 | authored exit edge 由 Active 触发；Leave 恢复 exact `ReturnSurfaceId + WorldPosition`；DTO／helper capture／restore SeparateSpace session，load 不重新调用 Enter；`JsonSnapshotSerializer` 已读写 `strategic.separateSpace` | `SeparateSpaceExitEdgeTrigger`、`SeparateSpaceSessionSnapshotRestore`、`HostSeparateSpaceExitTrigger` | SPACE-01 历史主线 Accepted / Sealed；SPACE-01-P1 JSON wire 修复状态不变，当前总 Snapshot schema 为 7 |
| 拾取物保持 | `WorldLootPickupService` 记录稳定 loot spot id；`HasTakenWorldLootSnapshotAuthority` + `TakenWorldLootSpotIds` 保存已取状态 | `WorldSnapshot`／`SnapshotService` | 当前专用链已接线；它不是通用 Story Flag／Quest 持久化 |

### E. 人物、成长与内容

| 范围 | 当前能做什么／authority | 持久化与主要入口 | 状态／不能做什么／待讨论 |
|---|---|---|---|
| 属性、境界与修炼 | AttributeModifier 管道、Realm／Cultivation、学习／熟练度／突破 ritual 与 WorldTick／ActionClock 基础链已存在；当前两份 Ritual Host UI 改动仍是无关未提交工作区内容 | `Core/Attributes`、`Core/Cultivation`、`HostBreakthroughRitual`、`HostSkillStudyRitual`；Snapshot 已保存 Cultivation、已学功法、功法／斗技 mastery 和 manual specs | 当前 slice 已实现；更完整境界内容、突破事件与 AI 日程不是因底层类型存在就视为完成。两份 Ritual 改动尚未确认／提交 |
| 行动、Order 与日程 | `ActiveActions`、Order queues、Schedule definitions／entity binding 与 `DailyTaskComponent` 已用于当前劳动、修炼、恢复和日程链 | Snapshot 已保存 active actions、orders、schedules、entity schedule id 与 daily task fields；Host ritual channel 自身不是 Snapshot authority | 当前基础链已实现；不得从可序列化目标推断所有 Action／Host UI 中间态均可无损继续 |
| 关系、Bond、态度与档案 | `SocialBondBoard` 保存客观 Bond；五维单向态度由 `RelationshipLedger` 事件聚合；人物档案供 UI／内容查询 | `RelationshipService`、`SocialBondBoard`、profile components；Snapshot 已保存 Social Bonds、RelationshipLedger 与 PersonalityProfile tags | 2M 主线 Accepted / Sealed。关系 ≠ 私人冲突 ≠ Faction War |
| 背包、装备、资源与掉落 | 非 resource 只使用 PartyInventory；resource 在可访问己方战略物资网络时统一使用 PartyInventory＋eligible WorldSitePublicStock，离开网络退回 bag-only。`stockAtLeast`、Quest progress／Journal 与 `removeStock` 共用该语义 | `PlayerStrategicResourceService` 是访问与稳定扣除顺序 authority；`HostInventoryPanel` 同窗提供小队背包／势力仓库，只允许战略资源从公库取到背包；PartyInventory、WorldSitePublicStock 与 taken loot 均沿用既有 Snapshot 字段 | STRATEGIC-STOCK-01 **Producer Accepted / Sealed**；势力仓库不是通用 Item／装备仓库，不含容量与物流 |
| Quest | 固定任务与人物委托共用 `QuestBoard`／`QuestService`；Journal/HUD 按实例操作 | 当前 Snapshot v9 保存实例、发布者、来源 Opportunity、交付、期限与序列；Quest shape 自 v8 后未变，definitions 仍来自 Content | QUEST-INSTANCE-01 **Producer Accepted / Sealed** |
| Flags／ContentEvents／Chapters | `WorldFlagBoard`／`StoryFlagService`、`ContentEventBoard`、`ChapterBoard`／day handler 支持条件、触发与推进；固定与动态 WorldObject interaction event 已接通 | 当前 v9 保存 Flags／History、fired keys 与 Chapter runtime；restore shell definitions-only 重注册并校验，不重放 opening；Active dialogue 不保存 | SAVE-01／DYNAMIC-DISCOVERY-01 **Producer Accepted / Sealed** |
| WorldOpportunity／WorldActivity | NPC 与动态 WorldObject 共用 Opportunity lifecycle；动态物体有稳定 object identity、三种发现模式、模板 onInspect 与显式 resolve | v9 保存 SpawnKind、NPC／物体互斥身份、精确坐标、发现状态及 Activity；hidden 发现前不泄漏 | DYNAMIC-DISCOVERY-01 **Producer Accepted / Sealed** |
| ContentCounters／ContentDaily／LocationLabor | runtime board 支持计数、每日限制与采收／劳动 Quest facts；角色 `DailyTaskComponent` 是另一条实体日程链 | 当前 v9 保存全部 Counter、marked day index 及 opaque labor tick／harvest key；`DailyTaskComponent` 仍按实体字段保存 | SAVE-01 **Producer Accepted / Sealed**；两类 Daily authority 不混用 |

### F. 工具链与工程

| 范围 | 当前能做什么／authority | 入口与输出 | 状态／边界 |
|---|---|---|---|
| ContentAuthoring | `EditorManifest.json` 是编辑器清单 authority；`WorldComposer`／`FineEditor` 维护 Continuous Surface；OpportunityEditor 已覆盖 NPC／动态 WorldObject、发现规则，EventEditor 已覆盖 Opportunity Object onInspect 与显式 resolve | `ContentAuthoring.sln`、`编译-所有编辑器.cmd`、`启动-*.cmd`；发布到 generated `Apps/*.exe` | MAP-01／DYNAMIC-DISCOVERY-01 编辑器能力均已封板 |
| Authoring → Runtime | Authoring Source 只由 baker 消费；Preview／Bake 共用 `CompositionEngine`；候选导出不改 Runtime，`CompatibilityPublisher` 可 staging／backup／rollback 原子替换 Surface、Geography、WorldMap cache 三文件 | `Content/BaseGame/Authoring/ContinuousSurface/` → candidate 或 `Content/BaseGame/Data/Worlds/` | Authoring Source ≠ Runtime Content；不得让编辑源成为 runtime DefinitionRegistry authority。发布后 Content Validation 入口本轮未重新核实 |
| Runtime / converter / prototype 边界 | Core／Data／Unity 正式程序集不反向依赖工具；`LegacyRuntimeConverter` 只做有证据的无损 FormalArmy／hybrid Snapshot 转换，hexWorld fail-closed；Demo Runtime 只读参考 | `ExternalTools/ContentAuthoring/LegacyRuntimeConverter/`；输入只读，输出必须是不同且不存在的文件 | Converter 自测记录仅为历史；本轮不重跑。不得把工具接回正常 Loader |
| Content／Snapshot 版本策略 | JSON ContentPackage + namespaced DefinitionId；Loader 严格校验；当前 Snapshot v9 由版本化 DTO／serializer 恢复；v8 及更早 authority 明确报错，不静默猜 | `Content/BaseGame/manifest.json`、`ContentPackageLoader`、`WorldSnapshot`、`JsonSnapshotSerializer` | 当前正式 BaseGame 可用；完整外部 Mods 产品能力、复杂 migration 平台未实现 |
| 启动、构建与诊断 | Unity 版本 2022.3.6f1 Built-in；LevelTester 是主要逻辑试玩入口但不在 Build Settings；Build Settings 当前启用 DemoParityHost／Demo_v0_1；工具 Build All 日志为 `build-all.log`，WorldComposer／FineEditor crash log 在 `Apps/` | 根 `README.md`、`docs/40-process/114-level-tester.md`、`ExternalTools/ContentAuthoring/README.md`、`tools/offline-compile.ps1` | 本轮未编译、未启动 Unity。generated `Apps/` 静态观察仍有已移出 manifest 的旧 exe，须以后 Build All 刷新后再确认；不影响 manifest 作为工具清单真源 |

## 已确认且不随换会话重议的设计边界

1. 游戏首先是具体角色的修仙 RPG；同行小队、连续探索、关系、战斗与领地经营服务角色体验，不把玩家本体替换为 RTS／4X 组织。
2. `SurfaceCell` 是 1×1 逻辑地理单位；`RuntimeChunk` 是流式技术分区；`WorldEditorCell` 是宏观 authoring 单位；`WorldSiteBlueprint` 与 `DetailPatch` 是 bake 输入。它们不能互相替代；1×1 逻辑格也不等于可任意改写 movement scale 的 Unity world unit。
3. 普通城市／村镇／户外 Site 均属于同一 Continuous Surface；Cave／Interior／Dungeon 与临时 Encounter 才是真正 Separate Space。
4. 玩家与 NPC 使用统一 Squad 语义；每名 Character 保有真实位置，singleton Squad 不自动产生共享物理移动 ownership。
5. 战斗必须在第一击前经过统一确认；只以真实参与者结算；主世界停表而本场战术时间按自身 owner 推进；战后回各自 pre-encounter anchor，战果不回滚。
6. SiteCore、政治 Owner／Claim、Actual Administrative Control、建设许可与资产状态分离。控制改变不自动移动人物、删除资产或重置日程。
7. WorldMap 是同一 Surface 的 planning view；选择目标后关闭地图再恢复旅行。图标／标签按 world-space 投影随 zoom 缩放，不恢复固定屏幕像素方案。
8. RelationshipLedger 是态度真源；Social Bond 是客观关系事实；人物冲突、关系介入与 Faction War 是不同状态，不得互相隐式升级。
9. Separate Space transition 带当前 PlayerParty occupants、保存内部状态与 exact outdoor return；普通 Outdoor 不借此恢复一 Site 一 LocalMap。
10. 编辑器生产 authoring source，baker 生产 runtime output，游戏 Loader 只消费 current runtime Content。工具输出、历史原型和正常 gameplay authority 不得混层。

依据：[00 overview](../00-project/00-overview.md)、[03 glossary](../00-project/03-glossary.md)、[2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)、[23](../20-systems/23-combat.md)、[24](../20-systems/24-world-and-settlements.md)、[26](../20-systems/26-territory-management.md)、[28](../20-systems/28-jianghu-relations.md)、[ADR-0032～0038](43-decisions/README.md)。

<a id="save01通用内容状态磁盘持久化"></a>
## SAVE-01：通用内容状态磁盘持久化

- 当前 Snapshot v9 的 `ContentProgress` 是 Quest／Story／Event／Chapter 内容进度磁盘 authority；其 shape 自 v8 后未变。`hasAuthority=true` 用于区分合法空状态与缺失／损坏数据，Quest 实例身份与序列同属 authority。
- Flags／History、全部 Quest runtime、fired event keys、Chapter runtime、Counters／Daily、LocationLabor ticks／harvests 完整 round-trip。Quest／Event／Chapter definitions 仍来自当前 Content Package。
- Active Event 期间禁止保存，不持久化 pending dialogue。v1～v6 无法可靠推导上述状态，统一拒绝。
- New Game／Load 使用同一 `PlayableSimulationLoopFactory`，避免 Quest deadline 与 Chapter day beat 在读档后丢失或重复注册。
- 原 Proposal 已由制作人授权并实现；当前状态 **Implementation Complete / Producer Acceptance Pending**。完整范围、验证和人工验收见 [257](257-save-01-content-progress-persistence-v1-2026-09-23.md)。

## Historical Resume Snapshot — 2026-09-18～21（已归档）
> **2026-09-21 FINAL LEGACY CLOSEOUT：** Continuous World / Legacy Runtime Migration 已完成最终 Scripts-only 实现清理，状态为 **Implementation Complete / Producer Acceptance Pending**。PlayerParty authority 为 `SurfaceId + WorldPosition + SurfaceVisible / ContinuousSurfaceRoute`；NPC group 为 `Squad + SquadWorldMotion`；角色空间为 `AtWorldPosition / AtSite(background-resident) / InSeparateSpace / InEncounter`；领土为 `WorldSite + TerritoryClaim + Actual Administrative Control`；战斗为 `CharacterEncounter`；独立空间为 Interior LocalMap / `EntityLocation`。旧 Hex／FormalArmy／TerritoryRegion／Outdoor LocalMap 只保留 old input、migration、derived metadata、显式 compatibility 与 Demo/test contract。以后不得因搜索到 Legacy 名字自动开启 cleanup；仅在它重新成为现代 authority，或产品明确终止旧兼容时再处理。
>
> **本轮最终 smoke：** 复验 New Game、WorldMap／PlayerParty movement、行进 Save/Load、洞府进入离开、CharacterEncounter／战报／Return、NPC Squad marker，以及旧档 Outdoor LocalMap／Hex compatibility fixture。通过前不 git seal。
>
> **2026-09-21 Skill Mastery acceptance fix：** 功法／斗技灌注统一以真实 `mastery.breakthroughs` 路径计算资格与有效门槛，并在确认 `actualGain > 0` 后原子扣修为／写熟练。制作人随后要求当前全部功法／斗技显式配置到化境：四段门槛依次 20/30/40/50，灵草与粗木分别各需 1/2/3/4。显式 profile 缺失当前 tier 路径仍不回落成默认路径。
>
> **本补丁人工 smoke：** 分别查看洞府秘诀与一门斗技的小成／大成／圆满；核对灌注、冲击按钮、10 修为固定扣费、四档材料、近上限实际增量，以及关闭详情页后选中角色面板上方的仪式读条。通过前保持 Pending。
>
> **2026-09-21 FINAL-SEAL Non-World follow-through：** 非世界修复与清理已达到 **Implementation Complete / Producer Acceptance Pending**。四类学习／熟练判定统一到可保存 `world.Random`；失能自主行动即时且逐 tick fail-closed、释放本人预约；`TryAddAll` 与整组 outcome／领奖／事件 choice 形成原子提交；Normal Outdoor 公库改用 exact position 的 Actual Control。已删除旧即时学习 wrapper、空 UI、无入口关系种子／派生 helper 和 runtime 假 LocalMap／Settlement 布局；Prototype Demo 因 Build Scene、Prefab 与 GUID 契约整体保留。特殊空间公库政策与通用剧情 Boards 持久化仍待独立定义，不得宣称全游戏状态均已完整 Save/Load。全程序集 offline compile `ALL_OK`；集中纯 C# sanity（含上一轮回归）14/14；Current BaseGame Content reference validation 通过；`git diff --check` 通过且 staged 为空。
>
> **本轮制作人集中验收：** 1) 用背包现有功法秘籍／斗技秘本开始研读，中途令固定 subject 弥留，确认不授予且恢复后不会自动续上；2) 让角色执行采集／劳动或打坐，运行中令其弥留，确认进度停止、工位释放、下一条旧自主命令不启动；3) 满包时领取含多项奖励的现有任务／事件，确认无前项奖励、flag、关系或计数残留，腾出空间后仅领取一次；4) 在己方核心 Actual Control 内外移动并观察公库可用量／消费，陈旧地点上下文不得授权；5) 底栏人物入口直接打开统一人物档案的人际页；6) New Game 核对灵根、初始境界、RegionId 与 authored opening relations。通过前保持 Pending。
>
> **2026-09-21 FINAL-SEAL consolidated repair：** 已完成 correctness repair 与 dead-dependency closure，状态为 **Implementation Complete / Producer Acceptance Pending**。组织 membership 与实际移动／命令 ownership 已分离；casualty handoff 冻结首次精确位置；精确位置查询、显式 Surface 路由及后台 Snapshot 两阶段恢复已收口；零 caller battle/arrival/旧 runtime snapshot/stack/Host residue 已删除，旧 DTO/readers 与真实工具／测试 caller 保留。架构冻结详见 [ADR-0038](43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。本轮未打开 Unity、未暂存、未提交、未推送。
>
> **制作人人工 smoke（仍待完成）：** 1) 单人 Squad 成员个人旅行／到达／Save-Load；2) 多人 Squad 成员只随 Squad、不可同时个人旅行；3) A 点失能、Squad 移到 B、再死亡仍留 A；4) WorldMap 精确位置与 `(0,0)` 聚焦；5) 重叠 Surface 上 PlayerParty／Squad／后台个人均留在显式 Surface，跨 Surface 目的地拒绝；6) moving/idle 后台旅行 Save-Load，idle 不重启；7) Encounter 与 Separate Space 的弥留／死亡／返回不被户外 handoff 污染；8) Host roster 显示组织归属但只对真实 group 聚焦 Squad；9) New Game、旧档 migration、WorldSite/economy 与基础内容 smoke。全部通过前不得改为 Accepted / Sealed。

> **2026-09-21 LEGACY-FINAL-SEAL implementation：** Compatibility quarantine、zero-caller API cleanup、runtime naming cleanup、Development boundary guards 与最终 Architecture Freeze 已达到 **Implementation Complete / Producer Acceptance Pending**。冻结矩阵见 [ADR-0038](43-decisions/ADR-0038-continuous-world-legacy-migration-final-seal.md)。LEGACY-FINAL-C 已 Sealed；最终 smoke 通过前暂不宣称 Migration Complete。下一阶段为 Gameplay / Content development，具体玩法尚未指定。

> **2026-09-21 LEGACY-FINAL-C Seal：** Final Strategic Hex / Territory / Residual / Legacy Battle Runtime Retirement 已由制作人完成人工验收并正式 **Producer Accepted / Sealed**。正常 runtime authority 已收口为 Continuous Surface、Squad、WorldSite／Claim、CharacterEncounter 与 Separate Space；当前只进行 **LEGACY-FINAL-SEAL — Compatibility Quarantine / Dead API Cleanup / Architecture Freeze**，完成最终 smoke 前暂不宣称 Migration Complete。

> **2026-09-21 LEGACY-FINAL-C implementation：** Final Strategic Hex / Territory / Residual / Legacy Battle Runtime Retirement 已达到 **Implementation Complete / Producer Acceptance Pending**。TerritoryRegion、StrategicEncounter、RetreatingArmy 与 LingeringBattlefield runtime boards 已退出；现代 residual 使用 `AtWorldPosition + SurfaceId + exact WorldPosition`；旧 Hex／Outdoor LocalMap 与旧 DTO 仅留明确 compatibility／one-way migration 边界。正式记录见 [250](250-legacy-final-c-final-strategic-runtime-retirement-2026-09-21.md)。C 尚未获制作人验收，不得宣称 Legacy Finalization Complete。

> **2026-09-21 LEGACY-FINAL-B Seal：** PlayerParty Continuous Surface travel authority cutover 已由制作人完成人工验收并正式 **Producer Accepted / Sealed**。正常玩家户外位置与旅行使用 `SurfaceId + exact WorldPosition + SurfaceVisible`；Hex／Outdoor LocalMap executor 仅留兼容边界。当前主线切换为 **LEGACY-FINAL-C — Final Strategic Hex / Territory / Residual / Legacy Battle Runtime Retirement**。C 完成并验收前不得宣称 Legacy Finalization Complete。

> **2026-09-21 LEGACY-FINAL-B implementation：** 已完成 PlayerParty Continuous Surface travel authority cutover，状态为 **Implementation Complete / Producer Acceptance Pending**。正常 New Game／WorldMap／WASD 链使用 `SurfaceId + exact WorldPosition + SurfaceVisible`；正常 WorldSite 保持 AtWorldPosition，Hex／LocalVisible／Wilderness transition 仅留旧档、旧内容与旧 Outdoor LocalMap 兼容。正式记录见 [249](249-legacy-final-b-playerparty-continuous-surface-travel-authority-cutover-2026-09-21.md)。LEGACY-FINAL-C 为 Next；B 未经制作人验收且 C 未完成前不得宣称 Legacy Finalization Complete。

> **2026-09-21 current handoff：** LEGACY-FINAL-A 已由制作人完整人工验收并正式 **Accepted / Sealed**；已验收实现 checkpoint 为 `e97f53f`。当前主线为 **LEGACY-FINAL-B — PlayerParty Continuous Surface Travel Authority Cutover**，目标是把正常玩家旅行收口为 `SurfaceId + exact WorldPosition + SurfaceVisible`，并把 Hex／Outdoor LocalMap executor 限定为兼容路径。LEGACY-FINAL-C 为下一阶段；B/C 完成前不得宣称 Legacy Finalization Complete。

> **2026-09-20 Seal / Legacy Finalization superseding state：** MAP-04、CW-10、CW-10.5 与 Cave Loot persistence 已由制作人人工验收并正式 **Accepted / Sealed**。当前主线切换为 **LEGACY-FINAL-A — Unified NPC Squad World Motion / FormalArmy Runtime Retirement**；随后依次为 LEGACY-FINAL-B、LEGACY-FINAL-C。A/B/C 全部完成前不得宣称 Legacy migration complete。本文后续关于 MAP-04／CW-10／CW-10.5 pending 的段落均为历史恢复快照。

> **2026-09-20 LEGACY-FINAL-A implementation state：** A 已达到 **Implementation Complete / Producer Acceptance Pending**，正式记录见 [248 LEGACY-FINAL-A](248-legacy-final-a-unified-npc-squad-world-motion-2026-09-20.md)。正常 New Game 的 NPC group authority 已收口为 `Squad + SquadWorldMotion + exact Surface position`；Current BaseGame 不再创建 FormalArmy/ArmyStack，旧 content/save 仅作单向 migration input。待制作人验收 A 后才进入 B，TerritoryRegion 等 C 边界仍未完成。

> **2026-09-21 LEGACY-FINAL-A2 runtime zero-state（已封板）：** FormalArmyBoard、ArmyStackBoard、ArmyMembershipComponent 与旧 Army runtime/services 已物理退出 Simulation；旧 Content/Snapshot DTO 只在兼容边界直接单向迁移到 Squad + SquadWorldMotion，旧 active battle identity 迁到 SquadId / CharacterEncounter。制作人已完成人工验收，A 状态为 **Accepted / Sealed**。

> **2026-09-20 Current State supersedes the older snapshot below:** SPACE-01 已由制作人验收并以 `49f8650`（`Seal SPACE-01 separate-space ownership and persistence`）提交、推送，状态为 **Accepted / Sealed**。MAP-04 已恢复并完成 final physical cleanup 实施：删除 `ContinuousWildernessPair` runtime、清理 W1C acceptance logs / force-leave cheat、Normal Outdoor Surface sync 不再回退 Hex terrain legality、正常 `PartyWorld` summary 不再写 `AtHex`。MAP-04 当前为 **Implementation Complete / Producer Acceptance Pending**；本轮 MAP-04 文件全部保持未暂存、未提交、未推送。后文 2026-09-18 的 WIP、暂停与旧 consumer 数量是历史恢复快照，不再代表当前状态。

> **2026-09-20 Final Seal Preparation：** 最终 caller audit 发现并修复三处 modern AtHex producer：Continuous movement member presence、Snapshot active Continuous focus、Continuous AutoResolve battle commit。W1B primary-context dead APIs 已删除。现存 AtHex 只服务 old-save restore、旧 Outdoor LocalMap／Hex travel、non-continuous battle 与 legacy residual；WorldRegion 只保留 Data schema、旧包验证和 migration/import。Separate Space LocalMap 是正式独立空间 authority，不属于 Legacy。完整 Residual Matrix 见 [245 MAP-04](245-map-04-physical-legacy-cleanup-2026-09-17.md#final-seal-preparation--legacy-residual-matrix2026-09-20)。状态仍为 **Implementation Complete / Producer Acceptance Pending**，不得提前 Sealed。

> **2026-09-20 Cave Loot acceptance patch：** MAP-04 其余人工验收项已由制作人确认正常；最后阻断是洞府 loot 的 `loot:*` runtime flag 未进入 Snapshot，导致 Load 后重新物化。现增加窄范围 taken-loot Snapshot authority，并将 LocalMap loot identity 统一为 `MapLayoutId + PlacementId`；PartyInventory 沿用既有 authority，未扩展通用 StoryFlag persistence。纯 C# round-trip 覆盖已取、空 authority、跨 MapLayout 同名 placement 和背包满失败。MAP-04 仍待制作人复验后封板。CW-10／CW-10.5 current-code readiness audit 未发现 blocker，二者仍为 **Implementation Completed / Producer Acceptance Pending**。

> **本文是未来新会话（Codex / 新 ChatGPT / 新 Cursor 会话）接手本项目的正式入口。**
>
> - 轮次性质：**Documentation / Recovery Snapshot**（本轮禁止任何实现修改）
> - 请求日期：2026-09-18
> - 实际生成时间：**2026-09-19 00:33～00:45（本地）**，即制作人 09-18 夜场跨过午夜之后
> - 生成时 HEAD：`c05a3d26651693d0346db09e1b86220c3965d757`（branch `dev_openworld`）
> - 生成时工作树：**clean**（SPACE-01 的全部改动已由制作人在 2026-09-19 00:32:36 提交为 `c05a3d2`）
> - 关联：[246 SPACE-01](246-space-01-separate-space-interior-transition-v1-2026-09-18.md)｜[245 MAP-04](245-map-04-physical-legacy-cleanup-2026-09-17.md)｜[244 MAP-03](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)｜[243 MAP-02](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)｜[242 MAP-01](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)｜[240 Editor 工具链](240-editor-toolchain-cleanup-2026-09-15.md)｜[2N 系统真源](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)｜[ADR-0036](43-decisions/ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)｜[ADR-0037](43-decisions/ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)
>
> 本文件**不是**新的架构真源；它是对当前仓库真实状态的登记与索引。系统正文仍在 `docs/20-systems/**`，决策仍在 `docs/40-process/43-decisions/**`。

---

# Archived Recovery Snapshot（2026-09-18～19，不可执行）

以下 §1～22 保留当时事实与审计过程；所有“当前／下一步／恢复顺序”只描述当时状态。

# 1. Executive Summary（历史）

本项目是 **Unity 2D top-down 仙侠 RPG**（修仙世界模拟 + 角色扮演 + 战略层）。

当前阶段的真实结论：

- **Continuous Surface（连续地表世界）已经成为正常的 Outdoor 世界。** 一个大陆一张 Final Continuous Surface，正常户外 gameplay 使用 exact `WorldPosition` + `SurfaceId` + `SurfaceGroundNavigation`，**没有** Outdoor LocalMap 切换。
- **Hex / Outdoor LocalMap 正在退役。** Hex 字段与旧 LocalMap 代码仍大量存在于仓库中，但已被降级为 **Legacy / Derived Compatibility**，不再是正常产品的 authority。
- **WorldComposer / FineEditor 已经建立**（MAP-01，Producer Accepted / Sealed）。
- **Surface WorldMap 已经建立**（MAP-02，Producer Accepted / Sealed）。
- **Normal Gameplay Surface authority 已完成**（MAP-03，2026-09-17 Producer Accepted / Sealed；封板提交 `b04920b`）。
- **MAP-04（物理删除 Legacy）尚未结束**：已完成第一批大清理（外层 Editor、旧 Content 地图集、旧 WorldRegion 管线），剩余 consumer 仍多。
- **Separate Space / Cave 系统正在 SPACE-01 WIP**：主体实现已完成并已提交（`c05a3d2`），但存在明确的 deferred hardening，**尚未 Producer Accepted / Sealed**。

**必须明确：当前不是一个“坏掉的中间版本”。**

- 绝大部分 Continuous Outdoor 正常玩法（New Game 开局、精确地表旅行、旅行存读档、WorldSite 连续归属、FactionFlag 世界坐标放置与存读、NPC/FormalArmy 连续移动、Outdoor CharacterEncounter → 独立战场 → 战后精确回归、农田/储藏室/恢复处等 Continuous asset）已经可以正常运行，并已由制作人在 MAP-03 人工验收。
- 本轮离线验证（2026-09-19）：`XianXia.Core`（470 sources）、`XianXia.Data`（80）、`XianXia.Unity`（147）离线 C# 编译 **ALL_OK，0 error**（4 条既有 warning）。

**当前未完成主要集中在三处：**

1. **MAP-04 最终 Legacy cleanup**（Hex / WorldRegion / Outdoor LocalMap / 历史测试 fixture）；
2. **SPACE-01 的边缘 ownership / persistence hardening**（transition membership、逃离所有权、失能/尸体/落单、`PartyWorld` 语义、洞内 NPC 落点持久化）；
3. **Cave / Separate Space 的最终封板**（制作人人工验收 + 正式 seal）。

---

# 2. 本轮 Snapshot Audit 依据

本轮**不是**根据旧聊天或旧文档推断，而是同时对照：当前 git 状态 + 当前未提交/已提交代码 + 当前正式 Content + 当前 process docs。

实际读取/核对：

| 类别 | 实际核对对象 |
|---|---|
| Git | `git status --short`、`git diff --stat`、`git diff`、`git log -10 --oneline`、`git branch --show-current`、`git remote -v`、`git reflog`、`git show --stat`（`c05a3d2` / `54141d1` / `596d9c9`） |
| 正式文档 | [2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)、[41-roadmap](41-roadmap.md)、[42-devlog](42-devlog.md)、[240](240-editor-toolchain-cleanup-2026-09-15.md)、[242](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[243](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)、[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)、[245](245-map-04-physical-legacy-cleanup-2026-09-17.md)、[246](246-space-01-separate-space-interior-transition-v1-2026-09-18.md) |
| ADR | ADR-0036（地图方向）、ADR-0037（Editor 工具链／旧 Content 迁移）、ADR-0031／0032／0033／0035／0026／0023（Continuous World / SiteCore / Encounter / Squad / PlayerParty / Battle 时间） |
| 代码 | `SeparateSpaceTransitionService`、`SeparateSpaceResolver`、`SeparateSpaceExitEdgeTrigger`、`SeparateSpaceSessionSnapshotRestore`、`SeparateSpaceCombatPolicy`、`LocalMapSession`、`LoadedLocalMapPlacementSnapshotRestore`、`HostSeparateSpaceExitTrigger`、`HostCommandBridge`、`HostNpcContextMenu`、`PlayableHostBootstrap`、`HostSnapshotSessionRehydration`、`LocalMapVisibility`、`SnapshotActiveControlledLocalMapResolver`、`WorldSnapshot` |
| Content | `Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json`（解析：6 SiteRegion / 11 FactionFlag / 76 SitePlacement（含 6 controlCore）/ 21 SitePlace / 646 chunk / 24 openingEntityAnchor；cellSize 0.028、origin (-1.4,-1.4)、1900×850 Surface Cells）、`Sites/sites.json`、`Maps/ch01_cave_map.json`、`Maps/strategic_encounter_arena.json`、`LocalPlaces/ch01_cave_places.json`、`LocalPlaces/strategic_encounter_arena_places.json` |
| 工具链 | `ExternalTools/ContentAuthoring/EditorManifest.json`（当前 10 项）、`Apps/*.exe` 时间戳、`build-all.log` 末次记录 |
| 检索证据 | `findstr` 全仓库统计（见 §16）：Hex 字段名消费者 113 文件；`WorldRegion` 15 文件；LocalMap compat token 35 文件；Hex 命名文件 36 个 |
| 离线验证 | `tools/offline-compile.ps1`（Core/Data/Unity，2026-09-19，ALL_OK） |

### ⚠ 本轮审计期间发生的并发提交（重要）

审计开始时工作树有 46 项未提交改动（SPACE-01）。**审计进行中，制作人在 2026-09-19 00:32:36 从外部（非本会话）提交了 `c05a3d2`「先传一版洞府也好了」**，把当时全部 SPACE-01 代码 + 文档 + `tools/space-01-sanity.ps1` 提交并推送到 `origin/dev_openworld`。因此：

- 审计开始时的工作树快照仍完整保留在本文件 §22；
- 审计结束时工作树为 clean，HEAD = `c05a3d2`；
- **本会话没有执行任何 `git add` / `commit` / `push`**，也没有修改任何 SPACE-01 代码。

---

# 3. Milestone Status Table

| # | Milestone | Status | Producer Acceptance | Git checkpoint | Current meaning | Remaining work |
|---|---|---|---|---|---|---|
| 0 | **Editor Toolchain Cleanup** | **Accepted / Sealed** | 已验收（[240](240-editor-toolchain-cleanup-2026-09-15.md)） | 已提交 | 唯一 manifest（`EditorManifest.json`）为 Editor metadata 真源；Build All 为 staging all-or-nothing；正式输出平铺在 `Apps/<Editor>.exe` | 最后一次成功 Build All 产物**陈旧**（见 §17 问题 6）：`Apps/` 仍含已删除的 `RegionEditor.exe`／`WorldGraphEditor.exe` |
| 1 | **MAP-01 — WorldComposer / FineEditor Production V1** | **Accepted / Sealed（2026-09-16）** | 已验收（[242](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)） | 已提交 | 大陆级 macro authoring + 逐 Surface Cell 精修 + CompositionEngine/Bake + Legacy Migration Bridge + 荒村（青石荒村）迁移 | 自动水文、道路 A*、detail scatter、minor POI、terrain compatibility matrix、Runtime Chunk profiling 仍属后续范围 |
| 2 | **MAP-02 — Continuous Surface WorldMap** | **Accepted / Sealed（2026-09-16）** | 已验收（[243](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)） | 已提交 | WorldMap 由 exact `WorldPosition` 投影/反投影；Surface terrain/forest cache；固定 Header + 地图视口 + 按需 Flyout；Site/Flag marker 世界空间缩放 | 无（该 milestone 范围已封板）；不表示“完全去 Hex” |
| 3 | **MAP-03 — Normal Gameplay Surface Authority Cutover** | **Accepted / Sealed（2026-09-17）** | 已验收（[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)） | `b04920b`（`docs: seal MAP-03 continuous surface cutover`） | 正常玩法 authority = exact WorldPosition / SurfaceGroundNavigation / Continuous SiteCore / Actual Control；Hex 与 Outdoor LocalMap 降级为 Legacy / Derived Compatibility | MAP-04 负责物理清理；MAP-03 本身不删除兼容实现 |
| 4 | **MAP-04 — Physical Legacy Cleanup** | **In Progress / Paused / Not Accepted** | **未验收**（[245](245-map-04-physical-legacy-cleanup-2026-09-17.md)） | 部分已提交：`596d9c9`（第一批大清理）+ `54141d1`（第二批 + 回归修复）；**无 seal 提交** | 逐 consumer 删除 Hex / WorldRegion / Outdoor LocalMap / 旧 Editor / 旧 Content | 见 §15「Still remaining」；另有未通过的 Completion Gate（Build All 切换失败、Legacy Hex gameplay 源码面仍广、WorldMap 视觉未 Unity 验收） |
| 5 | **SPACE-01 — Separate Space / Interior Transition V1** | **Implementation Complete / Producer Acceptance Pending**（**不是 Accepted**） | **未验收 / 未封板** | `c05a3d2`（2026-09-19 00:32:36，已 push） | 统一 Separate Space 模型（Cave／Interior／Dungeon／SeparateMap）；Cave 为第一份样板；Enter/Leave Domain 收口到 `SeparateSpaceTransitionService`；in-place combat；physical exit；additive snapshot | **Deferred Final Hardening A–F**（§14）+ 制作人人工验收 checklist；封板前必须先做 hardening |

**说明：** `c05a3d2` 的提交信息「先传一版洞府也好了」表明制作人自测认为 Cave 基本好用；但**本仓库没有任何 Producer Acceptance / Seal 记录**，245 与 246 都明确写着未验收。**不要把它写成 Accepted。**

编辑工具链当前状态（`EditorManifest.json`）：Active 8 项 = `PackageBrowser`、`CharacterNpcEditor`、`ManualArtEditor`、`QuestEditor`、`EventEditor`、`WorkAreaEditor`、`WorldComposer`、`FineEditor`；Legacy Compatibility 2 项 = `LocalPlaceEditor`（仅室内/洞穴/独立地图）、`MapEditor`（仅室内/洞穴/独立战斗地图）。`WorldGraphEditor` 与 `RegionEditor` **已不存在**（项目、启动器、manifest 项均删除）。

---

# 4. 当前 Authoring 架构（MAP-01 已建立，固定术语）

## 4.1 比例与术语（**冻结，不得重新争论**）

| 术语 | 比例 / 尺寸 | 产品职责 | 非职责 |
|---|---|---|---|
| **Surface Cell** | 1×1 | **最小 gameplay terrain unit**：terrain / walkability / footprint / water / road / farm / fine edit | 不是运行块 |
| **World Editor Cell** | 10×10 Surface Cells | WorldComposer macro terrain authoring | runtime 不读取 |
| **Runtime Chunk** | 50×50 Surface Cells | **只用于 streaming / materialization / navigation cache** | **不是 authoring authority**、不是 Site 尺寸、不是行政范围、不是编辑格 |
| **WorldSite Blueprint** | 任意整数 Surface Cell 尺寸 | 固定 Site 的精修布局与 bake 输入 | 不是 runtime scene / map piece |
| **Detail Patch** | 任意 Surface Cell 尺寸 | 局部覆盖与手工修饰 | 不是 chunk |
| **WorldComposer** | — | **whole-continent authoring**（大陆级 macro） | 不做逐格精修 |
| **FineEditor** | — | **1×1 Surface Cell local authoring**（Blueprint / DetailPatch） | 不绘制整片大陆 |
| **Final Continuous Surface** | 一大陆一张 | runtime 户外地理唯一真源 | 不保留 source authoring pieces |

当前主 Surface 实测：`base:surface_main_wilderness_v1`，cellSize `0.028`，origin `(-1.4, -1.4)`，**1900×850 Surface Cells**，**646 个 50×50 chunk**（38×17）。Composer 视角为 190×85 World Editor Cells。

## 4.2 Content 分层

**Base Terrain（基础地形，仅三种）：** `Plain`、`Mountain`、`Water`
**Overlay（覆盖层，仅一种）：** `Forest`
**Path（曲线 overlay）：** `River`、`Road`
**World Object（显式世界物件）：** 树、桥、岩石等（`WorldObjectPlacement`）

- **Forest 是 overlay，不是 base terrain；Forest 可通行。**
- Road、River 是带控制点、宽度/等级的 Surface 坐标曲线 overlay，**不是单独地图**。
- Authoring schema v3：基础地形只有 `Plain / Mountain / Water`，覆盖层只有 `None / Forest`；旧 v1/v2 制作源加载时迁移；未设置的 Fine Cell = `Inherit`。

**MAP-01 V1 固定合成顺序（不得随意重排）：**
`Macro Base Terrain → Forest Coverage → River（最终基础地形为 Water）→ Road → Detail Patch terrain override → WorldSite Blueprint terrain override → Composition World Object / Blueprint object overlay → Preview / Final Continuous Surface Bake`

Preview 与 Bake 必须消费**同一个** `CompositionEngine`；确定性只依赖 world seed、全局 Surface 坐标与 source intent，**不依赖随机调用顺序**。

## 4.3 Authoring Source 与 Runtime Publish 分离（硬规则）

| 类别 | 谁编辑 | 谁消费 | 内容 |
|---|---|---|---|
| **Authoring Source** | WorldComposer / FineEditor | 只有 baker | `WorldCompositionDocument`、`WorldSiteBlueprintDocument`、`DetailPatchDocument` |
| **Runtime Generated Content** | Bake 产生 | runtime loader（只读） | Final Continuous Surface、Runtime Chunk、final terrain/geography、object placements、WorldSite 位置与 content、navigation input、WorldMap LOD/cache input |

- Authoring Source **不得**继续作为正常 runtime Data 直接塞进 `Content/BaseGame/Data` 被 runtime loader 当正式 gameplay definition 加载。
- 运行时**不需要知道**某个位置来自哪个 Blueprint / Detail Patch / 旧 LocalMap。
- 当前 `base:surface_main_wilderness_v1` 仍是混合容器（chunk source + old Site bake + LocalMap bridge + fallback）；其未来等价物是**纯 bake runtime output**。

## 4.4 编辑器 UI 语言

**WorldComposer / FineEditor 的 user-facing UI 目前要求中文。**

---

# 5. WorldComposer UX 决策（**以后不得重新争论**）

- WorldComposer coarse authoring 使用 **Area Brush**（1/2/4/8/16 编辑格、连续 stroke、Fill、Eraser、框选、pan/zoom、grid）。
- **River / Road 使用 path / spline**（Catmull-Rom 控制点；河流保存宽度；道路保存 `Trail / SmallRoad / Road / MajorRoad` 等级；交叉口可 `Unresolved` 或标记 `Bridge / Ford`）。
- **Forest 是 overlay，不是 base terrain；Forest 可通行。**
- **Road / Bridge / tree 可作为 authored world assets**（Blueprint / Composition 显式对象 placement）。
- **Runtime Chunk 只作为 Debug overlay** 显示（Runtime Chunk ≠ authoring unit，不进任何 gameplay authority）。
- **Terrain Expansion deterministic**（边缘展开由 world seed + 全局 Surface 坐标 + 周边 source intent 决定）。
- **Authoring Source 与 Runtime Publish 分离**（见 §4.3）。
- Blueprint 支持 quarter-turn rotation 与通用对象 placement（稳定 id、kind、Content/Asset 引用、局部 Surface 坐标、footprint、rotation、metadata）；缺失/损坏链接、越界、重复 id、重叠、未解决道路河流交叉口进入 Problems。
- 地图缩放：**Editor 用 `Alt + mouse wheel` zoom**。
- **WorldMap strategic marker（Council Hall / FactionFlag / Site label）属于 world-space presentation：**
  - 地图放大 → 图标和文字**一起变大**；
  - 地图缩小 → 图标和文字**一起变小**；
  - **不是** fixed-pixel HUD marker。
- **Header / Flyout 等 UI chrome 才是 fixed screen-space。**

---

# 6. Current Continuous World Runtime 架构

## 6.1 Normal Outdoor authority（MAP-03 之后）

- **SurfaceId** + **exact WorldPosition**（Canonical WorldPosition）
- **SurfaceGroundNavigation**（换路、寻路、walkability、river 不可通行 / bridge 可通行）
- **WorldSite CoreWorldPosition**（fixed Council Hall 精确世界坐标）
- **Actual Administrative Control**（与实际领土范围分离）
- **Continuous placements**（农田、储藏室、恢复处、工作区等 Continuous asset）
- **Runtime Chunk 3×3 streaming 仍保留**（只影响表现、缓存与近场 runtime，不是行政/建筑身份/编辑边界）

## 6.2 Hex 字段的当前地位（**已降级**）

`CurrentHex`、`DestinationHex`、`FinalDestinationHex`、`HexPath`、`AnchorHex`、`PresenceHex`、`OccupiedHexes`、`StrategicAnchor`、`BattleAnchorHex`、旧 Outdoor `LocalMapId`、旧 `mapLayout`

→ MAP-03 之后全部为 **Legacy / Derived Compatibility**。它们仍存在于代码、Save 与 Content 中，但**不再是 Main Continuous Surface 正常 Gameplay 的 authority**。

> “代码里还能 grep 到 Hex”**不是**判定未完成的依据；判定依据是它是否仍决定正常 gameplay 的位置/路线/合法性。MAP-04 的剩余项见 §15。

## 6.3 独立空间（Separate Space）架构（SPACE-01）

| | Continuous Outdoor Surface | Separate Space |
|---|---|---|
| 例 | `base:surface_main_wilderness_v1` | `base:map_ch01_cave` |
| 位置权威 | Continuous `WorldPosition` + SurfaceId | `ActiveMapLayoutId` + Interior local position |
| Streaming | Runtime Chunk | 独立 MapLayout / WalkGrid / LocalPlaceSet |
| 进入后 | — | 暂时离开 Outdoor playable presentation（Domain 保留） |
| 离开后 | — | 回到进入前 **exact Surface return** |

- Domain 真源：`SeparateSpaceTransitionService`（`ExplorationService.EnterLocalMap / LeaveLocalMap` 是薄封装）。
- Session 真源：`LocalMapSession` **已收窄为 Separate Space Session**（类型名保留兼容，语义不再是“所有局部世界”）。
- 正式可见性入口：`LocalMapVisibility.IsEntityVisibleInCurrentPlayableSpace`（SeparateSpace-first）。
- `SpaceKind`：`Cave / Interior / Dungeon / SeparateMap`，由 `MapLayoutDefinition.spaceKind` + `SeparateSpaceResolver` 解析（**业务代码禁止写死 map id**）。

---

# 7. WorldSite / Territory 当前最终规则

## 7.1 Site Core

| | Permanent Site | Runtime FactionFlag Site |
|---|---|---|
| Core | fixed Council Hall（议政厅） | 可拆卸 FactionFlag |
| `CoreIsRemovable` | `false` | `true` |
| WorldMap marker | house / council-hall symbol | flag symbol |
| anchor | exact `CoreWorldPosition` | exact flag world position |

## 7.2 控制与范围

- **Actual Control 与 theoretical range 分离**：实际行政资产 / 建造 / 公库存取消费 **Actual Administrative Control**。
- **同势力范围：WorldMap union presentation**（`WorldSiteActualControlOverlayBuilder.BuildFactionUnion`）。
- **FactionFlag 被敌人打爆 → 直接摧毁**；**议政厅不可直接永久删除**，只能走既有 Site warfare / occupation 规则。
- 150×150 Surface Cells 只是**一级 SiteCore 的理论行政控制范围**，不是 Chunk / Blueprint / World Editor Cell / authoring 最小尺寸。

## 7.3 当前永久 Site（Permanent Sites）实测

来自 `Content/BaseGame/Data/Worlds/main_wilderness_surface_v1.json` → `siteRegions`（**6 个**）：

| SiteId | DisplayName | siteType | arrival (WorldX, WorldY) | ownerFactionId |
|---|---|---|---|---|
| `base:site_chengzhen` | 青石镇 | Town | (18.61955, 11.25) | — |
| `base:site_guanai` | 青石关 | Pass | (21.65064, 16.5) | `base:faction_xijin` |
| `base:site_huangcun` | 青石荒村 | Village | (6.49519, 11.25) | `base:sect_huangcun_labor` |
| `base:site_lingdi` | 灵地 | SpiritLand | (38.10512, 9.0) | `base:faction_shuofeng` |
| `base:site_linjian` | 林间 | Forest | (12.99038, 7.5) | `base:sect_huangcun_labor` |
| `base:site_zhuangyuan` | 庄园 | Village | (29.01185, 6.75) | `base:faction_nan_yan` |

- 六处均已拥有 **fixed Continuous `controlCore`**（`sitePlacements` 中 `kind=controlCore` 共 6 个）；青石镇、青石关、灵地、林间、庄院的五棵树原有连续世界中心被用作固定议政厅 Core 中心（见 [244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)）。
- 另有 `Content/BaseGame/Data/Sites/sites.json` 中的 `base:site_abandoned_cave`（废弃洞府）——它是 **Separate Space 入口 Site（洞穴）**，不是永久 controlCore Site。
- `factionFlags` 实测 **11 面**（`base:flag_player_origin`（主角据点）＋ 荒村劳工 4 ＋ 渔村 3 ＋ 西晋 3），全部 `createsWorldSite: true`、`coreLevel: 1`。
- `openingEntityAnchors` 实测 **24 条**（New Game 断言 24 anchors / 24 spawned / 24 presence）。

---

# 8. MAP-02 WorldMap 正式产品规则（已封板）

- **Continuous Surface only** 是正常产品路径；**不显示 gameplay Hex grid**。
- Surface bounds、**exact WorldPosition click**、Surface travel 目标、Actual Control、Site Core marker、FactionFlag marker、NPC Squad、角色/势力面板、坐标网格。
- **UI 结构：** `Fixed Header` + `Full Map Viewport` + `Optional Inspect Flyout`（底部支援控制条仍固定）。
- **地图 content 不允许覆盖 Header**；地图渲染统一在 `BeginGroup(mapRect)` 内使用局部 `Rect(0,0,w,h)` 投影，组结束后一次性把 marker 命中区域转屏幕坐标（不再有永久右栏与 `GUI.matrix` 平移抵消）。
- **坐标网格：** 稀疏正交线，**无 X/Y 数字标签**（按缩放取 1/2/5×10^N Surface Cell 间隔，不进存档/寻路/拾取）。
- **Site marker：** Permanent Site → house / council-hall symbol；Runtime Flag Site → flag symbol；anchor = exact `CoreWorldPosition`。
- **图标/名字：** world-space scaling with map zoom；名称从图标右侧 8 Surface Cells 起排，字号按投影后标签高度缩放。
- **WorldMap 打开时是独立 planning overlay：** 打开时关闭背包/建造/任务 UI、冻结 LocalVisible 旅行、顶层输入阻断；**WorldMap open 不移动 Player**；**closing WorldMap 才继续 AutoTravel**。
- 无 Surface 时显示「当前世界没有可用的连续世界地图。」并走 Content error，不伪造 Hex 地图。

---

# 9. MAP-03 已验收的 Normal Gameplay Authority

制作人于 2026-09-17 人工验收通过（[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md)）：

- **PlayerParty Surface travel**：以精确当前位置与目的地查询 `SurfaceGroundNavigation`，不以 HexPath 作为启动条件；最终按物理距离结算；成员 Presence = `SurfaceId + WorldPosition`。
- **exact destination**：WorldMap 点选与路线目标共用 exact WorldPosition。
- **travel Save/Load**：保存精确目的地、到达半径与 Site 意图；Content shell 注册导航后按保存起点重新求路。
- **New Game opening exact anchor**：`openingSurfaceId` + 已发布 Surface Site/FactionFlag + `openingEntityAnchors`；不调用 Hex strategic bootstrap，不加载 HexWorld JSON，不激活 Outdoor WorldRegion。
- **WorldSite continuous membership**：正常 WorldSite 上下文由 continuous 行政/物理查询判定，不回落 Hex footprint。
- **FactionFlag world-space authority**：先以 `SurfaceId + WorldPosition` 验证地表与 Actual Control；`StrategicAnchor` 只做派生兼容；读档不要求保存的 AnchorQ/R 落在 Hex grid 内。
- **NPC Squad / Background Character continuous movement**：后台 NPC 到连续 Site 走地表寻路 + authored Site arrival；`SquadWorldMotion` 与个人后台旅行以精确起点、Surface route 和 authored arrival 求路。
- **Battle world-space anchor**：`BattleParticipantSnapshot` 增加 `HasBattleAnchorWorldPosition` / `BattleAnchorWorldX/Y` / `BattleAnchorSurfaceId`，offer 冻结时从实际接触点记录。
- **Residual world-space authority**：弥留/尸体的个人 `SurfaceId + WorldPosition` 可独立成为稳定空间 authority。
- **Outdoor Site 不切旧 Outdoor LocalMap**：正常 WorldSite 上下文不再装载 Outdoor LocalMap。
- **Interior / Cave 仍允许 separate space**（SPACE-01 承接）。
- **CharacterEncounter 正式战斗规则：**
  `Continuous Outdoor → BattleOffer → independent battle field`（这是当前 Outdoor 正式战斗规则）。

---

# 10. Battle 当前封板规则（**不要重新设计**）

**Outdoor CharacterEncounter（ADR-0023 / ADR-0033 / Phase 4 已封板）：**

- unified `BattleOffer`
- participant roster（参战名单）
- manual / auto battle
- **independent battlefield**（同源独立遭遇空间）
- **main world freeze**（具名 CharacterEncounter pause owner；见 ADR-0023／0033）
- battle participant only
- **post-battle exact return**（返回战前精确世界位置）
- report（战报）
- active-character takeover（战斗中控制权接管）
- lingering / downed / residual persistence（弥留、倒地、残留）

**Squad / PlayerParty spatial authority 已支持组内归属与每名真实角色位置**；战后分别按各自战前 world anchor 回写，不建立共同 Army 坐标。

---

# 11. SPACE-01 完整状态

## 11.1 目的

SPACE-01 建立 **Continuous Outdoor ↔ Separate Space** 的统一切换机制，而不是继续给 Cave 打零散补丁。

**Separate Space 将来用于：** Cave、Interior（室内）、Dungeon、Secret Realm（独立秘境）、其他 isolated playable spaces。
**当前 Cave 是第一份样板**（`base:map_ch01_cave`，`spaceKind: cave`）。

## 11.2 已实现的架构（`c05a3d2`）

- **Domain 真源收口：** `SeparateSpaceTransitionService.Enter / Leave`；Host 只负责 presentation rebuild / input / registry place activation。
- **Enter 流程：** resolve entrance/map/LocalPlaceSet/spawn → validate → **capture Outdoor exact return（SurfaceId + WorldX/Y）→ 建立 SeparateSpaceSession → 移动 PlayerParty membership + deterministic formation → Host 切换 presentation**。任意中途失败：Host 回滚 PlaceSet，**不得半进洞**。
- **Leave 流程：** validate session → clear Interior places/session → **restore exact Surface return** → Host 重新激活 Surface streaming/neighborhood/camera。**禁止**返回 Hex / Site center / fake entrance center。
- **Return authority：** 只有 `ReturnSurfaceId` + `ReturnWorldX/Y` + `ReturnLocationId`（不依赖 Hex / AnchorHex / PresenceHex / Overworld MapLayout）。
- **Occupant / visibility：** Separate Space 内只认 PlayerParty occupants + Active MapLayout/LocalPlaceSet 居民；Outdoor WorldSite / Surface chunk / Background NPC 不参与当前 playable presentation（Domain 保留）。
- **Combat policy：** `SeparateSpaceCombatPolicy`。Separate Space 内为 **direct in-place combat**（当前 MapLayout）；`CharacterEncounterService.RequiresEntry` 在双方均属 active Separate Space 时返回 `false`；`HostNpcMeleeAssault` / `HostNpcContextMenu` / `HostCharacterEncounter.Request` guard 均走 in-place；`HostPlayerPartyController.TickCombatFollow` 对 occupants 自动协战。
- **Entry Party Rule：** 进入 Separate Space = 当前真正随队成员**全部自动**transition（authority：`PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty`）；**已删除 `HostLocalMapEnterPrompt` 队员选择弹窗**；Host API = `IssueEnterSeparateSpace(leader, entranceId)`，旧 `IssueEnterLocalMapWithParty` 仅 legacy wrapper（忽略 party 数组）。
- **Physical Exit Trigger：** `HostSeparateSpaceExitTrigger` + Core `SeparateSpaceExitEdgeTrigger`。只有 **Active Controlled Character** 的 View 进入 authored Exit geometry 才触发；edge-trigger 防 spawn/Load 立刻离开；无确认窗；战斗中/失能/modal 不触发。**已删除**：右键「离开」、`TryPickInteriorExitAtMouse` pick 路径、Action Menu「离开洞窟」。
- **Snapshot（additive）：** `StrategicSnapshotDto.SeparateSpace`（`SeparateSpaceSessionSnapshotDto`：IsInSeparateSpace / SpaceKind / ActiveMapLayoutId / ActiveLocalPlaceSetId / Entry / Return / ReturnSurface + WorldX/Y / OccupantIds / ActiveCharacterId / EntryReason）。
- **Presentation restore 优先级（严格）：** 1) **Active Separate Space** → `RebuildSeparateSpacePresentationAfterLoad`（禁止 Outdoor Surface 抢先） 2) Independent CharacterEncounter → Encounter restore 3) Continuous Outdoor → `ContinuousOutdoorSurfaceRuntime.RebuildAfterWorldRestore`。Separate Space restore **不重新调用 `Enter()`**。
- **旧档迁移：** 无新 DTO 但 placements/推断 mapId 指向 Cave/Dungeon/Interior/Arena 时，用 PlayerPartyTravel exact position 重建 return authority；信息不足 → 明确 `SnapshotInvalid`，**不默默送回荒村**。
- **Content 边界：** `base:loc_ref_cave`（Outdoor entrance only）／`base:loc_cave_chamber`（Interior only）／`base:map_ch01_cave`（`spaceKind: cave`）。

## 11.3 Cave 当前已经基本工作的流程（依据代码 + 制作人自测记录）

| 阶段 | 当前真实行为 |
|---|---|
| **Discovery** | Hidden Cave entrance → 附近 strange-presence hint → Survey（神识）→ reveal entrance。Reveal authority = **PlayerParty knowledge**（`KnownSites`），**不是**全球 NPC knowledge。Survey 只做 `KnownSites.Discover` + reveal visual + 轻量 toast；**不**激活 Interior places、不改 ActiveMapLayoutId、不建 Session、不 deactivate Surface、不移动 Party。 |
| **Approach** | Revealed + Far → 右键 = 只走向 entrance；Revealed + Near → 出现 Enter action。 |
| **Entry** | 当前 PlayerParty traveling members **自动一起进入**（不再有 participant checkbox 选择）。 |
| **Cave Combat** | Separate Space 内 **direct in-place combat**：不进 BattleOffer、不建 CharacterEncounter、不开第二个独立战场；current Party followers 自动 assist。 |
| **Save/Load** | Cave 内 Save/Load 已基本验证可用（Load 后留在洞内、不默默回 Outdoor）；`[SeparateSpaceRestore]` / `[SeparateSpaceRestoreInvariantFailure]` 诊断日志已就位。 |
| **Exit** | **Physical Exit Trigger**：Active Controlled Character 实际进入 Exit geometry 才触发；不是右键菜单；返回 exact Outdoor position。 |

---

# 12. SPACE-01 Final Hardening（本轮已实现 · 待人工验收）

> 主体实现 checkpoint：`c05a3d2`。Final Hardening 代码在工作树**未提交**。
> 状态仍为 **Implementation Complete / Producer Acceptance Pending**。详见 [246](246-space-01-separate-space-interior-transition-v1-2026-09-18.md)。

| 项 | 状态 |
|---|---|
| A. Enter membership fallback 删除 | **Done** — 空成员明确失败 |
| B. Leave 严格 `ShouldMemberTransitionWithParty` | **Done** |
| C. stranded／downed／corpse 不错误 teleport | **Done**（代码）；待 Unity 人工验 |
| D. `PartyWorldPresenceMode.InSeparateSpace` | **Done**（additive=5；0–4 不变） |
| E. active-map 全部 Character local placement | **Done**；restore 不污染 membership |
| F. Producer Accepted／Sealed | **Pending** |

MAP-04 继续 **Paused / Producer Acceptance Pending**。

---

# 13. MAP-04 当前真实做到哪里

> **MAP-04 仍然 In Progress（Paused）。不要写完成，不要写 Accepted。**

## 13.1 Already migrated / removed（已提交，证据为 `596d9c9` / `54141d1` / `c05a3d2` 与当前仓库实测）

**A. Runtime / Bootstrap**

- `openingSurfaceId` 成为 Scenario 唯一 opening authority；`WorldRegionBootstrap` **已删除**（独立地图改用 `InteriorLocalPlaceBootstrap`）。
- `HexStrategicSessionBootstrap`、`HexStrategicMapBootstrap`、`HexTestWorldBootstrap`、`HexWorldStressMapBuilder`、`Ch01HexPrototypeMapBuilder`、`Ch01ScenarioStrategicSetup`、`StrategicBootstrap`、`HexWorldContentLoader`（+legacy adapter）**已移出 Runtime**，只在 `Assets/Tests/EditMode/LegacyHexFixtures/` 保留（8 个 fixture 类）。
- `HostFormalArmyContinuousPresenter`、`WorldSitePresentationLayer`、`FactionFlagWorldMapPresentation`、`BattleEngagementWorldMapDebug`、`HexMapMousePick`、`HexMapViewportProjection`、`HostHexGridDrawing`、`HostHexWorldRenderer`、`HexMapEditorService`、`WorldRegionBoard`（→ `LocalPlaceBoard`）**已删除**。
- `WorldVec2` 从 `Core.World.Hex` 移入中性 `Core.World`。
- `PlayerPartyContinuousFormationResolver`、`FormalArmyContinuousFormationResolver`、`CharacterEncounterSpatialAuthorityResolver`、`ContinuousCharacterSpatialAuthorityResolver`、`ContinuousSurfaceSessionBootstrap`、`ContinuousOpeningSpawnPresenceResolver` 等 Surface 侧 authority 已就位。

**B. Content（`Content/BaseGame/Data`）**

- **已删除**：`Worlds/ch01_hex_world.json`、`Worlds/travel_mvp_hex_world_30x15.json`、`Worlds/w1c_wilderness_acceptance_surface.json`、`Regions/world_regions.json`、**35 个 Outdoor MapLayout JSON**（含 `huangcun_01`、`player_camp_map`、各 `ch01_site_*_map`、wilderness fallback maps）、**34 个 Outdoor LocalPlaceSet JSON**、`world_node_stub_*`。
- **现存 `Maps/` 只有 2 个**：`ch01_cave_map.json`（`spaceKind: cave`）、`strategic_encounter_arena.json`。
- **现存 `LocalPlaces/` 只有 2 个**：`ch01_cave_places.json`、`strategic_encounter_arena_places.json`。
- `Regions/` 目录**已不存在**。
- 遭遇战术图由 `world_node_stub` 更名为独立 arena。
- 旧内容作为 EditMode 历史 fixture 保留在 `Assets/Tests/Fixtures/LegacyWorld/`（`ch01_hex_world.json`、`ch01_reference_map.json`），**不被当前 ContentPackage 加载**。

**C. 编辑器 / 工具链（ExternalTools）**

- `WorldGraphEditor`、`RegionEditor` **已删除**（项目、solution 项、manifest 项、启动器 `.cmd`）；`Shared/HexWorld/*`（14 个文件）与 `Shared.Tests` 的 8 个 Hex/Territory 测试**已删除**。
- `MapEditor` / `LocalPlaceEditor` 收窄为 Interior / Cave / 独立地图；`WorldComposer` 当前发布 UI 去掉“兼容模式”。
- `EditorManifest.json` 成为唯一 metadata 真源；`publish.ps1` 为 staging all-or-nothing。

**D. Host / WorldMap**

- `HostWorldMapPanel` 收敛为 Surface 地图（大幅删除 Hex renderer / grid drawing / picker / projection）；Surface-only WorldMap product 已建立。
- Outdoor opening authority、Site/FactionFlag 连续 anchor、Actual Control overlay 均已迁到 Surface。
- **部分** Hex / WorldRegion / Outdoor LocalMap fallback 已移除（Surface route 失败不再回退 Hex）。

**E. SPACE-01 附带的 legacy 删除**

- 删除 `HostLocalMapEnterPrompt`（478 行）；右键离开、Action Menu「离开洞窟」、`TryPickInteriorExitAtMouse` pick 路径均删除。

## 13.2 Still remaining（**2026-09-19 重新 grep 当前仓库的结果，不是旧聊天假设**）

| 类别 | 实测残留 | 数量 | 说明 |
|---|---|---|---|
| Hex 拓扑/表现源码 | `Assets/Scripts/Core/World/Hex/*`（HexWorld、HexCoord、HexMath、HexMetrics、HexPathfinder、HexTerrainCatalog/Presentation/Type/VisualInset、HexTravelMode、HexWorldLayout/MapRenderBounds/Scale、HexCell） | 14 文件 | 大量仍被 derived compat 消费者引用；不能一次性删 |
| Hex strategic 服务 | `ArmyHexBattleAnchorService`、`ArmyHexCommandService`、`ArmyHexLingeringArrivalService`、`ArmyHexMigrationHelper`、`ArmyHexPosition`、`ArmyHexPursuitService`、`ArmyHexTravelService`、`BattleEngagementHexDistance`、`FormalArmyHexWorldPositionResolver`、`HexActiveEnemyArmyQuery`、`HexFootprintSpatialGeometry`、`HexFootprintSpatialMapping`、`HexResidualContextQuery`、`HexRightClickResolver`、`HexStrategicLegacyGuard`、`HexStrategicRuntime`、`PlayerPartyHexPursuitService`、`PlayerPartyHexTravelService` | 19 文件（Hex 命名文件共 36 个） | `PlayerPartyHexTravelService` 的 HexPath/Footprint 旅行、Site `PresenceHex`、Flag strategic anchor 仍在 |
| Hex 字段消费者 | 命中 `CurrentHex/DestinationHex/FinalDestinationHex/AnchorHex/PresenceHex/OccupiedHexes/BattleAnchorHex/StrategicAnchor` 的文件 | **113 文件** | 多数为 derived/compat + Snapshot DTO；需逐 consumer 判定 |
| WorldRegion | 含 `WorldRegion` 的文件（`WorldRegionDefinition`、`DefinitionSchema`、`ContentPackageLoader`、`ContentReferenceValidator`、`MapLayoutDefinition/JsonLoader`、`OpeningScenarioDefinition`、`SpawnZoneApplier`、`LocalPlaceBoard`、`ContinuousMaterializePlacementSync`、`ContinuousOutdoorSurfaceRuntime`、`HostSnapshotLocalPlacementCaptureSync`、`PlayableHostBootstrap` 等） | 15 文件 | definition parser 只用于旧包输入；Content 侧 `world_regions.json` 已删 |
| Outdoor LocalMap compat | `WildernessLocalMapFallback`、`PlayerPartyLocalMapMaterializationService`、`BackgroundCharacterWildernessLocalMapMaterialization`、`BattleLocalMapResolver`、`LoadedLocalMapBelongingQuery/Explain`、`LocalMapHexDirectionProjection`、`BackgroundLoadedLocalMapArrivalDebug`、`PlayerPartyWildernessTransitionService`、`LoadedLocalMapPlacementSnapshotRestore` | 35 文件命中 compat token | 旧 Outdoor LocalMap snapshot ID 在 Host restore 时转 Surface；旧遭遇图 ID 映射到 arena |
| 旧 Content exporter | `Assets/Scripts/Data/Content/HexWorldContentExporter.cs`、`HexWorldContentDefinition.cs` | 2 文件 | 仍编入 Runtime |
| 历史测试 fixture | `Assets/Tests/EditMode/LegacyHexFixtures/`（8 类）、`Assets/Tests/Fixtures/LegacyWorld/`（2 JSON） | — | 按“不跑 Unity Test”约束未执行 |
| 旧档兼容 | `WorldSnapshot`、`StrategicSnapshotHelper`、`FormalArmySnapshotRestore`、`HostSnapshotSessionRehydration` 的 Hex 坐标/Presence/旧 LocalMapId 读取 | — | Intentional Legacy Compatibility，需先审计再删 |

**MAP-04 未通过的 Completion Gate（[245](245-map-04-physical-legacy-cleanup-2026-09-17.md)）：**

1. **Build All Apps 切换失败**：`publish.ps1` 候选 `Apps` 移入正式目录时 Windows `Access to the path ... denied`，脚本回滚成功 → 现有 `Apps/` 仍为旧 exe（见 §17 问题 6）。
2. **Legacy Hex gameplay 源码面仍广**：当前产品**不能**声称“完整 Hex Gameplay stack 已物理删除”。
3. **WorldMap 功能回归风险**：Surface-only 组件只经离线编译，未做 Unity 视觉/交互验收。
4. **旧战略可视化编辑入口**：`WorldGraphEditor` 的 `FactionManagerWindow` / `OpeningStrategicEditorWindow` 已删；当前 WorldComposer 可发布 Surface faction flag，但**尚无等价的独立可视化外交/开局战略编辑窗口**。
5. **历史 EditMode 测试**：旧 fixture 已搬迁，但按约束未运行；其语义覆盖旧 Hex，不是当前产品验收。

---

# 14. MAP-04 过程中已经发生的主要 Regression 与修复

> 记录这些是为了让未来开发者理解“为什么代码里存在某些 guard”。简短根因，不贴长代码。真源见 [245](245-map-04-physical-legacy-cleanup-2026-09-17.md) 与 [42-devlog](42-devlog.md)。

| # | 现象 | 根因 | 修复方向 |
|---|---|---|---|
| 1 | **New Game NPC 全消失** | 删除 `WorldRegionBootstrap.ApplyOpening` 后，15 名既无 `worldSiteId` 又无 `localLocationId` 的 Ch01 NPC 失去旧 `EntityLocation`；旧 place→Site 推断失效 | 改为 **anchor-first presence**：Normal Surface 开局直接以 `openingEntityAnchors`（稳定 SpawnKey/DefinitionId/SiteId/WorldX/Y）建立 presence + 独立 anchor census 断言 24/24/24 |
| 2 | **Snapshot load 后远处 NPC 泄漏/抽搐** | presentation 与 Domain spatial authority 冲突：先有 View 再回写 Domain，且 SiteArrival fallback 覆盖精确位置 | **snapshot restore 必须优先于 opening anchor**；首帧只允许 Domain→View；loaded chunk 是硬门；不一致 → `SnapshotInvalid`；加 `[SnapshotMaterializationLeak]` 诊断 |
| 3 | **FormalArmy authored deployment 三支部队堆在一起** | `assemblySite` personal presence 与 authored deployment 竞争 authority；idle `FormalArmyMemberPresenceSync` 保留旧个人位置；`SyncLegacyFromWorldMotion()` 每次把 `UsesHexStrategicPosition` 写回 `HasPosition` 覆盖 Surface 语义 | 新增 `initialSurfaceDeployment = {surfaceId, anchorSiteId, offsetCellsX/Y}`；authored Surface 部署直接建立首次 Army anchor；普通 idle tick 只保留锚点附近 near-field 个人位置；队形不写回 canonical position；间距 3 Surface Cells |
| 4 | **CharacterEncounter `LegacyUnqualifiedPosition`** | Personal space 被当成通用要求；但 FormalArmy / PlayerParty 是 **group authority** | 新增 `CharacterEncounterSpatialAuthorityResolver` / `ContinuousCharacterSpatialAuthorityResolver`；个人空间不再是通用前置 |
| 5 | **Runtime FactionFlag Site 启动 invariant 失败** | 动态 runtime Site ≠ authored Opening SiteRegion（authored 列表不含运行时新建 Site） | 启动 invariant 区分 authored 与 runtime Site，不用 authored 列表校验动态 Site |
| 6 | **SiteCore warfare 枚举异常** | `TickOccupation` 遍历 `PlayerPartyRuntime.Members` 时，位置解析再次读取 `Members`，getter 清空并重填同一投影列表 → `Collection was modified` | getter 每次返回独立快照（**PlayerParty group spatial authority** 相关） |
| 7 | **WorldMap UI 回归** | 曾出现 Continuous Surface 透出、被规划 overlay 覆盖、Header 被地图内容覆盖、flyout 改变视口、点击优先级错乱 | 恢复不透明全屏底色与视口底色；恢复顶层输入阻断；打开时关闭背包/建造/任务 UI 并冻结 LocalVisible 旅行；固定 Header + Full viewport + 按需 flyout；战略面板点击优先级；真实 checkbox UI |
| 8 | **Cave transition 回归** | Survey／reveal 生命周期混乱（成功后 `RefreshMapStampsOnly` 触发旧 MapLayout 全图刷新导致伪切场景）；presentation 未分离；Save/Load 后回 Outdoor；右键离开语义不明 | Survey 只 reveal + 轻量 toast；`LocalMapVisibility` SeparateSpace-first；presentation restore 优先级；`SeparateSpaceSessionSnapshotRestore`；**physical exit trigger** 取代右键/菜单 |

---

# 15. Current Known Issues / Deferred Work

> 只列有证据的问题。每项含 Severity / Current behavior / Desired behavior / Likely files-services / Recommended fix direction。

## Issue 1 — SPACE-01 Final Hardening deferred

- **Severity:** High（SPACE-01 封板前置）
- **Current:** §12 的 A～F 全部仍存在（membership fallback、leave 过宽、失能/尸体未验证、`AtHex` 语义复用、洞内 NPC 落点不持久化）。
- **Desired:** transition membership 严格化；逃离只迁移 `ShouldMemberTransitionWithParty`；失能/尸体/落单不被 teleport；`InSeparateSpace` 语义；洞内 resident/enemy 落点持久化。
- **Files:** `Assets/Scripts/Core/Exploration/SeparateSpaceTransitionService.cs`、`SeparateSpaceSessionSnapshotRestore.cs`、`LoadedLocalMapPlacementSnapshotRestore.cs`、`Assets/Scripts/Core/World/PartyWorldPresenceMode.cs`、`Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs`、`HostSnapshotSessionRehydration.cs`
- **Direction:** 先写清楚四态目标行为（对照 ADR-0019 / CW-02），再改 Core，不改 Host 行为契约。

## Issue 2 — MAP-04 final physical cleanup pending

- **Severity:** Medium-High（技术债，非当前玩法阻断）
- **Current:** §13.2 的残留仍全部存在（Hex 113 文件命中、19 个 Hex strategic 服务、15 文件 `WorldRegion`、35 文件 LocalMap compat、2 个 HexWorld 导出器）。
- **Desired:** 逐 consumer 审计后删除，或明确标注为 intentional legacy。
- **Files:** 见 §13.2 表格。
- **Direction:** 先做 consumer audit 清单（每个文件 → 谁在 normal gameplay 调用 → 是否可删/需迁移），再分批删除；**不要**在 SPACE-01 未封板时激进删除地图 runtime。

## Issue 3 — Old-save migration coverage 尚需最终验证

- **Severity:** Medium
- **Current:** 旧档迁移路径已实现（旧 Outdoor LocalMap ID → Surface；旧 Hex 人物/军队位置 → Surface 世界坐标；旧遭遇图 ID → arena；SPACE-01 legacy → Separate Space），但**没有 Unity 人工验收记录**。
- **Desired:** 至少覆盖：旧 Outdoor LocalMap、纯 Hex 人物位置、军队旅行、遭遇战术图 ID、Cave 内旧档。
- **Files:** `HostSnapshotSessionRehydration.cs`、`StrategicSnapshotHelper.cs`、`FormalArmySnapshotRestore.cs`、`SeparateSpaceSessionSnapshotRestore.TryMigrateLegacySeparateSpace`
- **Direction:** 制作人用真实旧档人工验收；信息不足时继续保持 `SnapshotInvalid` 明确失败，不做 silent teleport。

## Issue 4 — Remaining Legacy Hex consumer

- **Severity:** Medium
- **Current:** 113 个文件仍命中 Hex 字段名；其中 `PlayerPartyHexTravelService`、`PlayerPartyHexPursuitService`、`ArmyHex*`（7 个）、`HexStrategicRuntime`、`BattleEngagementHexDistance`、`HexRightClickResolver`、`HexResidualContextQuery`、`HexStrategicLegacyGuard`、`HexFootprintSpatial*`、`LocalMapHexDirectionProjection`、`FormalArmyHexWorldPositionResolver`、`HexActiveEnemyArmyQuery` 仍在 Runtime。
- **Desired:** 正常 gameplay 不依赖任何 Hex authority；Hex 只留在明确的 legacy/derived 边界。
- **Files:** 见 §13.2。
- **Direction:** 以 `HexStrategicLegacyGuard` / `HexWorld.HasGrid` 为入口做 consumer 分类，不做全局正则删除。

## Issue 5 — Remaining Outdoor LocalMap consumer

- **Severity:** Medium
- **Current:** `WildernessLocalMapFallback`、`PlayerPartyLocalMapMaterializationService`、`BackgroundCharacterWildernessLocalMapMaterialization`、`BattleLocalMapResolver`、`LoadedLocalMapBelonging*`、`PlayerPartyWildernessTransitionService` 等仍在（35 文件命中 compat token）。
- **Desired:** Outdoor 不再有 LocalMap authority；LocalMap 只剩 Separate Space（Cave/Interior/Dungeon/Arena）语义。
- **Files:** 同上 + `Assets/Scripts/Unity/Host/LocalMapVisibility.cs`
- **Direction:** 区分 “Outdoor LocalMap legacy” 与 “Separate Space（正当使用者）”；后者保留，前者清。

## Issue 6 — `Apps/` 生成产物陈旧（Build All 未在删除 Editor 后成功重跑）

- **Severity:** Medium（工具链/交付，非 gameplay）
- **Current（实测）:** `ExternalTools/ContentAuthoring/Apps/` 仍含 **12 个 exe**，包括已从仓库删除的 `RegionEditor.exe`（2026-09-16 02:13:04）与 `WorldGraphEditor.exe`（2026-09-16 02:13:10）；`build-all.log` 末次成功记录为 **2026-09-16 02:13:10 exit 0**。245 记录了 09-17 的一次 `publish.ps1` 尝试：10 项 publish 成功，但把候选 `Apps` 移入正式目录时 Windows `Access to the path ... denied`，脚本回滚，最终 exit 1。
- **Desired:** 关闭占用 `Apps`/staging 的外部进程后重跑 Build All，得到与当前 manifest（10 项，无 RegionEditor/WorldGraphEditor）一致的扁平产物。
- **Files:** `ExternalTools/ContentAuthoring/publish.ps1`、`EditorManifest.json`、`Apps/`（gitignored 生成产物）
- **Direction:** 制作人允许后重跑一次 Build All；`Apps/` 不是版本真源，不因陈旧而 commit。

## Issue 7 — Cave 内非 occupant NPC 落点不持久

- **Severity:** Medium（见 §12 E）
- **Current:** `LoadedLocalMapPlacementSnapshotRestore.Capture()` 只保存 active map 的 **occupants**；洞内居民/敌人移动后不落点。
- **Desired:** active Separate Space 内全部真实 entity 的 local position 随 Save/Load 保留。
- **Files:** `Assets/Scripts/Core/Persistence/LoadedLocalMapPlacementSnapshotRestore.cs`
- **Direction:** 扩展 capture 范围为 active map 的全部可见 entity；注意与 `[SeparateSpaceRestoreInvariantFailure]` 的 saved placement 校验语义保持一致。

## Issue 8 — `PartyWorldPresenceMode.AtHex` 语义复用

- **Severity:** Low-Medium
- **Current:** Separate Space 激活时把 `PartyWorld.Mode` 写成 `AtHex`（5 处），使独立空间与 Hex 兼容语义混淆。
- **Desired:** 新增 `InSeparateSpace`（或等价中性值）。
- **Files:** `Assets/Scripts/Core/World/PartyWorldPresenceMode.cs`、`SeparateSpaceTransitionService.cs`、`SeparateSpaceSessionSnapshotRestore.cs`、`PlayableHostBootstrap.cs`、`HostSnapshotSessionRehydration.cs`、`SnapshotActiveControlledLocalMapResolver.cs`
- **Direction:** 加枚举值 + 迁移所有写入点 + 确认 Snapshot 序列化兼容（`Mode` 是整型）。

## Issue 9 — 两处恒等/死分支（代码卫生，不改行为）

- **Severity:** Low
- **Current（实测）:** ① `SeparateSpaceTransitionService.Leave()` 末行 `return continuousReturn ? Result.Success() : Result.Success();`；② `HostNpcContextMenu` 的 LocalCombat 分支内 `if (IsActiveStrategicCombatTarget(...)) return false; return false;`。
- **Desired:** 语义清晰（若真需区分则返回不同结果；否则删除冗余分支）。
- **Files:** 上述两文件。
- **Direction:** 纯清理；**本轮不做**。

## Issue 10 — 未验证项（不得当作已验证）

- **Severity:** Informational
- **Current:** 本轮**没有**打开 Unity、没有跑 PlayMode / Unity Test Runner / batchmode。只有离线编译（Core/Data/Unity `ALL_OK`）。SPACE-01 的 Unity 端表现（视觉、输入遮挡、真实 Save/Load 落点、physical exit 手感、失能边界）**没有**本轮证据。
- **Desired:** 由制作人人工验收。
- **Direction:** 按 §16 的 checklist 执行。

---

# 16. Do Not Regress

> 未来任何重构**必须保持**下列行为。违反即视为回归。

1. **WorldMap 不显示 gameplay Hex grid。**
2. **WorldMap 打开时是独立 planning overlay**（地图内容不覆盖 Header）。
3. **WorldMap open 不移动 Player。**
4. **closing WorldMap 才继续 AutoTravel。**
5. **River unwalkable / Bridge walkable。**
6. **Outdoor Site remains same Continuous Surface**（不切旧 Outdoor LocalMap）。
7. **Cave / Interior alone can be separate space。**
8. **Outdoor CharacterEncounter uses independent battle**（BattleOffer → 独立战场 → 战后精确回归）。
9. **Separate Space combat is in-place**（不建 BattleOffer / 第二战场）。
10. **PlayerParty current members auto-enter Separate Space**（无队员选择弹窗）。
11. **Site Core exact world position。**
12. **FactionFlag exact world position。**
13. **Battle returns exact world position。**
14. **runtime chunk != authoring unit**（50×50 chunk 只是 streaming 分区；authoring 是 World Editor Cell / Surface Cell）。
15. **no Hex authority resurrection**（不得新增任何 Hex gameplay authority）。
16. **no Outdoor LocalMap authority resurrection。**
17. **WorldMap marker 是 world-space scaling**（图标与文字随 zoom 变大小）；Header/Flyout 才是 fixed screen-space。
18. **Forest 是 overlay、可通行；River/Road 是曲线 overlay，不是独立地图。**
19. **Authoring Source ≠ Runtime Publish**（authoring 不得直接当 runtime gameplay definition 加载）。
20. **Separate Space Load 不重新 Enter**（沿用 snapshot session/occupants/saved placements）。
21. **Separate Space presentation restore 优先于 Outdoor Surface restore。**
22. **Leave Separate Space 只能通过 physical exit trigger**（不是右键菜单/Action Menu）。
23. **Actual Control 与 theoretical range 分离**；行政资产/建造/公库消费 Actual Control。
24. **Permanent Site 的 fixed Council Hall 不可直接永久删除**；FactionFlag 被敌人打爆直接摧毁。
25. **FactionFlag 世界坐标 authority 与 Actual Control 判定不得回退 Hex anchor。**

---

# 17. Recommended Resume Order（已归档）

本节原顺序已全部失效：SPACE-01、MAP-04、LEGACY-FINAL-A／B／C 与 Final Seal 后续均已验收并提交。不得复制旧 hardening／consumer audit／seal 指令重新开工；当前入口只有页首摘要与系统现状总表。

---

# 18. Historical Producer / Codex Workflow Rules（已被现行规范替代）

本节旧规则不再单独生效。现行执行与验收规则只看 `AGENTS.md`、[52 协作规范](52-ai-collaboration-protocol.md) 与页首“执行与验收规则”；特别是不编写／运行自动测试、不启动 Unity、普通实施不自动提交、“封板”才授权选择性 commit。

---

# 19. Git / Working Tree 历史快照（2026-09-19；非当前状态）

> 生成时间：2026-09-19 00:33～00:45（本地）

- **current branch:** `dev_openworld`
- **HEAD commit hash:** `c05a3d26651693d0346db09e1b86220c3965d757`（`先传一版洞府也好了`，author `Patoooo`，2026-09-19 00:32:36 +0800）
- **remote:** `origin` = `https://github.com/Siyuan-Yu/ProjectCultiva.git`
- **last pushed checkpoint:** 本会话**未执行 `git fetch`**；按本地 remote-tracking ref，`origin/dev_openworld` 与 HEAD **一致**（`git status -sb` 无 ahead/behind）→ 视为 `c05a3d2` 已推送。**下次会话建议先 `git fetch` 确认。**
- **git status summary（生成本文件前）：** **clean**（无 staged / unstaged / untracked）。
- **生成本文件后：** 预期仅新增一个未跟踪文件 `docs/40-process/247-project-handoff-current-state-2026-09-18.md`（+ 本轮可能的文档同步改动），**未 `git add` / `commit` / `push`**。
- **MAP-04 是否已 commit：** **部分**。第一批大清理 = `596d9c9`（`清理hex相关先传一个`，329 files changed, +3591/-22554），第二批 + FormalArmy / Snapshot 回归修复 = `54141d1`（`势力范围这一块基本好了……`）。**仍有 §13.2 的剩余项未做，且无 seal 提交。**
- **SPACE-01 是否已 commit：** **是**，= `c05a3d2`（46 files changed, +2093/-830），包含全部 SPACE-01 代码、新增 `docs/40-process/246-*.md` 与 `tools/space-01-sanity.ps1`，以及 `docs/00-project/03-glossary.md`／`245`／`42-devlog.md` 的更新。**由制作人在本会话审计期间提交，非本会话所为。**

## 19.1 审计开始时的工作树快照（已解析进 `c05a3d2`）

按 subsystem 分组：

- **SPACE-01 Core（新增）:** `Core/Exploration/SeparateSpaceExitEdgeTrigger.cs`、`SeparateSpaceKind.cs`、`SeparateSpaceResolver.cs`、`SeparateSpaceTransitionService.cs`、`Core/Persistence/SeparateSpaceSessionSnapshotRestore.cs`、`Core/World/Strategic/SeparateSpaceCombatPolicy.cs`
- **SPACE-01 Host（新增）:** `Unity/Host/HostSeparateSpaceExitTrigger.cs`
- **SPACE-01 Core（修改）:** `ExplorationService.cs`、`LocalMapSession.cs`、`SnapshotActiveControlledLocalMapResolver.cs`、`StrategicSnapshotHelper.cs`、`WorldSnapshot.cs`、`CharacterEncounter.cs`
- **SPACE-01 Host（修改）:** `ContinuousOutdoorSurfaceRuntime.cs`、`HostActionMenu.cs`、`HostCaveEntranceQuery.cs`、`HostCaveSurveyPresenter.cs`、`HostCharacterEncounter.cs`、`HostCommandBridge.cs`、`HostLevelTesterCheatPanel.cs`、`HostNpcContextMenu.cs`、`HostNpcMeleeAssault.cs`、`HostPlayerPartyController.cs`、`HostSnapshotSessionRehydration.cs`、`HostWorldMapPanel.cs`、`LocalMapVisibility.cs`、`PlayableHostBootstrap.cs`
- **SPACE-01 Host（删除）:** `HostLocalMapEnterPrompt.cs`（+ meta）
- **Content:** `Content/BaseGame/Data/Maps/ch01_cave_map.json`（+`spaceKind: cave`）
- **Docs:** `docs/00-project/03-glossary.md`、`docs/40-process/245-*.md`、`docs/40-process/246-*.md`（新增）、`docs/40-process/42-devlog.md`
- **Tools:** `tools/space-01-sanity.ps1`（新增）
- **其它:** `Assets/Scripts.zip`（M）、`Assets/Scripts.zip.meta`（D）

**审计开始时的完整 `git status --short`（原样保留）：**

```text
 M Assets/Scripts.zip
 D Assets/Scripts.zip.meta
 M Assets/Scripts/Core/Exploration/ExplorationService.cs
 M Assets/Scripts/Core/Exploration/LocalMapSession.cs
 M Assets/Scripts/Core/Persistence/SnapshotActiveControlledLocalMapResolver.cs
 M Assets/Scripts/Core/Persistence/StrategicSnapshotHelper.cs
 M Assets/Scripts/Core/Persistence/WorldSnapshot.cs
 M Assets/Scripts/Core/World/Strategic/CharacterEncounter.cs
 M Assets/Scripts/Data/Content/ContentPackageLoader.cs
 M Assets/Scripts/Data/Content/DefinitionSchema.cs
 M Assets/Scripts/Data/Content/MapLayoutDefinition.cs
 M Assets/Scripts/Unity/Host/ContinuousOutdoorSurfaceRuntime.cs
 M Assets/Scripts/Unity/Host/HostActionMenu.cs
 M Assets/Scripts/Unity/Host/HostCaveEntranceQuery.cs
 M Assets/Scripts/Unity/Host/HostCaveSurveyPresenter.cs
 M Assets/Scripts/Unity/Host/HostCharacterEncounter.cs
 M Assets/Scripts/Unity/Host/HostCommandBridge.cs
 M Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs
 D Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs
 D Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs.meta
 M Assets/Scripts/Unity/Host/HostNpcContextMenu.cs
 M Assets/Scripts/Unity/Host/HostNpcMeleeAssault.cs
 M Assets/Scripts/Unity/Host/HostPlayerPartyController.cs
 M Assets/Scripts/Unity/Host/HostSnapshotSessionRehydration.cs
 M Assets/Scripts/Unity/Host/HostWorldMapPanel.cs
 M Assets/Scripts/Unity/Host/LocalMapVisibility.cs
 M Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs
 M Content/BaseGame/Data/Maps/ch01_cave_map.json
 M docs/00-project/03-glossary.md
 M docs/40-process/245-map-04-physical-legacy-cleanup-2026-09-17.md
 M docs/40-process/42-devlog.md
?? Assets/Scripts/Core/Exploration/SeparateSpaceExitEdgeTrigger.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceExitEdgeTrigger.cs.meta
?? Assets/Scripts/Core/Exploration/SeparateSpaceKind.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceKind.cs.meta
?? Assets/Scripts/Core/Exploration/SeparateSpaceResolver.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceResolver.cs.meta
?? Assets/Scripts/Core/Exploration/SeparateSpaceTransitionService.cs
?? Assets/Scripts/Core/Exploration/SeparateSpaceTransitionService.cs.meta
?? Assets/Scripts/Core/Persistence/SeparateSpaceSessionSnapshotRestore.cs
?? Assets/Scripts/Core/Persistence/SeparateSpaceSessionSnapshotRestore.cs.meta
?? Assets/Scripts/Core/World/Strategic/SeparateSpaceCombatPolicy.cs
?? Assets/Scripts/Core/World/Strategic/SeparateSpaceCombatPolicy.cs.meta
?? Assets/Scripts/Unity/Host/HostSeparateSpaceExitTrigger.cs
?? Assets/Scripts/Unity/Host/HostSeparateSpaceExitTrigger.cs.meta
?? docs/40-process/246-space-01-separate-space-interior-transition-v1-2026-09-18.md
?? tools/space-01-sanity.ps1
```

**审计开始时的 `git diff --stat`：**

```text
 Assets/Scripts.zip                                 | Bin 1862803 -> 1874506 bytes
 Assets/Scripts.zip.meta                            |   7 -
 .../Scripts/Core/Exploration/ExplorationService.cs | 172 +-------
 Assets/Scripts/Core/Exploration/LocalMapSession.cs | 101 ++++-
 .../SnapshotActiveControlledLocalMapResolver.cs    |  30 ++
 .../Core/Persistence/StrategicSnapshotHelper.cs    |   4 +
 Assets/Scripts/Core/Persistence/WorldSnapshot.cs   |  23 +
 Assets/Scripts/Core/World/Strategic/CharacterEncounter.cs |   4 +
 .../Scripts/Data/Content/ContentPackageLoader.cs    |   3 +-
 Assets/Scripts/Data/Content/DefinitionSchema.cs    |   2 +-
 Assets/Scripts/Data/Content/MapLayoutDefinition.cs |   4 +
 .../Unity/Host/ContinuousOutdoorSurfaceRuntime.cs  |   3 +
 Assets/Scripts/Unity/Host/HostActionMenu.cs        |   6 -
 Assets/Scripts/Unity/Host/HostCaveEntranceQuery.cs |  30 +-
 .../Scripts/Unity/Host/HostCaveSurveyPresenter.cs  |  13 +-
 Assets/Scripts/Unity/Host/HostCharacterEncounter.cs |  21 +-
 Assets/Scripts/Unity/Host/HostCommandBridge.cs     | 109 ++--
 Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs |  17 +
 Assets/Scripts/Unity/Host/HostLocalMapEnterPrompt.cs | 478 ---------------------
 Assets/Scripts/Unity/Host/HostNpcContextMenu.cs    | 114 ++---
 Assets/Scripts/Unity/Host/HostNpcMeleeAssault.cs   |  20 +-
 .../Scripts/Unity/Host/HostPlayerPartyController.cs |   6 +
 Assets/Scripts/Unity/Host/HostSnapshotSessionRehydration.cs |  40 +-
 Assets/Scripts/Unity/Host/HostWorldMapPanel.cs     |   8 +
 Assets/Scripts/Unity/Host/LocalMapVisibility.cs    |  61 +++
 Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs | 215 ++++++++-
 Content/BaseGame/Data/Maps/ch01_cave_map.json      |   1 +
 docs/00-project/03-glossary.md                     |   6 +-
 ...45-map-04-physical-legacy-cleanup-2026-09-17.md |   5 +-
 docs/40-process/42-devlog.md                       |  22 +
 31 files changed, 696 insertions(+), 840 deletions(-)
```

## 19.2 最近提交历史（`git log -10 --oneline`）

```text
c05a3d2 先传一版洞府也好了                       ← SPACE-01（2026-09-19 00:32:36）
54141d1 势力范围这一块基本好了，现在有洞府还没有做，只做到一半没额度了，之后再来。  ← MAP-04 第二批 + 回归修复
596d9c9 清理hex相关先传一个                       ← MAP-04 第一批大清理（pull --ff）
b04920b docs: seal MAP-03 continuous surface cutover
37de675 删压缩包
e01af91 很好
e133d57 mp-03 暂时版
5bf6734 feat: 完成连续世界制作与世界地图收口
7c29454 重置方向基本正常
9a4a428 编辑器 中途先传一版
```

**提交信息风格说明：** 本项目提交信息常为制作人中文口语短句，不代表 milestone 状态。**milestone 状态以 process docs 的 Producer Acceptance 记录为准。**

---

# 20. 同步的 Current Docs

本轮（外加 SPACE-01 已提交的那一轮）对 current docs 的最小同步：

| 文档 | 同步内容 |
|---|---|
| **本文件 247**（新增） | Project Handoff / Resume Snapshot —— 新会话入口 |
| **[245 MAP-04](245-map-04-physical-legacy-cleanup-2026-09-17.md)** | 顶部状态改为 `Paused / Producer Acceptance Pending` + 暂停原因（SPACE-01 dependency）+ 「勿写成 MAP-04 Accepted」；本轮追加真实 checkpoint 与剩余项 |
| **[246 SPACE-01](246-space-01-separate-space-interior-transition-v1-2026-09-18.md)** | 追加当前基本通过的功能 + **Deferred Final Hardening A～F** + 提交 checkpoint（`c05a3d2`）/ 未验收状态 |
| **[41-roadmap](41-roadmap.md)** | 顶部状态改为 `SPACE-01 final hardening pending` / `MAP-04 paused / resume after SPACE-01` |
| **[2N](../20-systems/2N-continuous-surface-world-authoring-and-composition.md)** | 只做简短 **Implementation Status** 更新（不大改系统正文），指向本文件 |
| **[42-devlog](42-devlog.md)** | 追加本轮 Documentation / Handoff 条目 |

**未修改历史 acceptance 文档：** [240](240-editor-toolchain-cleanup-2026-09-15.md)、[242](242-map-01-worldcomposer-fineeditor-production-v1-2026-09-16.md)、[243](243-map-02-continuous-surface-worldmap-acceptance-2026-09-16.md)、[244](244-map-03-normal-gameplay-surface-authority-cutover-2026-09-16.md) —— 已 Accepted / Sealed，**不得重写**，只被本文件引用。

---

# 21. Archived Copy-Paste Context（禁止继续使用）

> 以下整段仅保留 2026-09-18～19 的历史恢复证据，包含已完成的 WIP 与过期指令。**不得复制到新会话。** 新会话只读页首 Current handoff、ADR-0038 与最新 devlog。

```text
ARCHIVED / DO NOT EXECUTE — SPACE-01、MAP-04、LEGACY-FINAL-A/B/C 已全部封板。
项目：Unity 2D top-down 仙侠 RPG（修仙世界模拟 + RPG + 战略层）。仓库 D:\UnityProjects\XianXia，分支 dev_openworld，最新提交 c05a3d2。请把它当成“已实现大量内容、正在收口”的项目，不要重新设计架构。

【架构（已冻结）】
- Continuous Surface 已是正常 Outdoor 世界：一大陆一张 Final Continuous Surface；正常户外以 SurfaceId + exact WorldPosition +
  SurfaceGroundNavigation + Continuous SiteCore + Actual Administrative Control 为 authority；普通户外没有 LocalMap 切换。
- Hex 与 Outdoor LocalMap 正在退役（CurrentHex/DestinationHex/AnchorHex/PresenceHex/OccupiedHexes/BattleAnchorHex/
  StrategicAnchor/旧 mapLayout 均为 Legacy or Derived Compatibility）。还能 grep 到 Hex 不等于没做完。
- 比例术语固定：Surface Cell = 1×1 gameplay 地形最小单位；World Editor Cell = 10×10 Surface Cells（Composer 大陆级 macro
  authoring）；Runtime Chunk = 50×50，只是 streaming 分区，不是 authoring 单位；Blueprint / Detail Patch 尺寸任意。
- 地形只有 Plain/Mountain/Water；Forest 是 overlay 且可通行；River/Road 是曲线 overlay，不是独立地图。
- Authoring Source（WorldComposer/FineEditor，UI 要求中文）与 Runtime Bake 产物必须分离。
- WorldMap 是同一 Surface 的战略 LOD：exact WorldPosition 投影与点选、不显示 gameplay Hex grid、固定 Header + 全屏视口 +
  按需 Flyout、地图不得盖住 Header、稀疏坐标网格无 X/Y 数字。Site marker：永久 Site 画房屋、FactionFlag 画旗，anchor 是
  exact CoreWorldPosition；图标与文字随 zoom 缩放（world-space），只有 Header/Flyout 是屏幕空间。打开 WorldMap 是独立
  planning overlay：不移动玩家，关闭后才继续 AutoTravel。
- 独立空间：Outdoor ↔ Separate Space（Cave/Interior/Dungeon/秘境）。Domain 真源 SeparateSpaceTransitionService；authority
  是 ActiveMapLayoutId + Interior local position；离开必须回到进入前 exact Surface return（禁止回 Hex 或 Site 中心）。独立空间内
  是原地直接战斗（不进 BattleOffer、不开第二战场）；Outdoor 的 CharacterEncounter 才是 BattleOffer → 独立战场 → 战后精确位置回归。
  进洞时当前随队成员全部自动进入（已无队员选择窗）；离开只能靠物理出口触发（主控走进出口几何），不是右键菜单。

【已验收封板】Editor Toolchain Cleanup；MAP-01 WorldComposer/FineEditor（09-16）；MAP-02 Surface WorldMap（09-16）；
MAP-03 正常玩法 Surface authority cutover（09-17）。

【当前 WIP，未验收】
- SPACE-01 Separate Space V1：已实现并提交（c05a3d2），Cave 是样板。已基本可用：隐蔽洞口 + 近处气息提示 → 神识 Survey 只揭示
  （不激活内景）→ 远处右键只走近、近处才出进入 → 随队成员自动进入 → 洞内原地战斗（随从自动协战）→ 洞内存读档留在洞内 →
  物理出口回到 exact 户外位置。状态：Implementation Complete / Producer Acceptance Pending，**不是 Accepted**。
- MAP-04 物理删除旧地图栈：In Progress 且 Paused（等 SPACE-01）。已删旧 WorldGraphEditor/RegionEditor、Shared/HexWorld、
  35 个 Outdoor MapLayout、34 个 Outdoor LocalPlaceSet、world_regions.json、HexWorld JSON、旧 Hex WorldMap 渲染/拾取/投影、
  WorldRegionBootstrap 等；仍剩约 36 个 Hex 命名文件、113 个文件仍引用 Hex 字段、15 个文件仍含 WorldRegion、
  35 个文件仍含 Outdoor LocalMap 兼容代码、2 个 HexWorld 导出器、历史 EditMode Hex fixtures。

【SPACE-01 尚未完成（封板前必须做；动手前先向制作人确认）】
A) CollectTransitionMembers 在收集为空时会 fallback 抓所有 Player characters，应删除该 fallback；
B) 撤离应严格只迁移 PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty 为真的成员；
C) 主控出洞时失能/尸体/脱队/落单队员行为未验证，必须确保不被错误传送；
D) Separate Space 仍复用 PartyWorldPresenceMode.AtHex，未来加 InSeparateSpace；
E) 洞内非 occupant 的 NPC（居民/敌人）移动后的 Local position 读档后不保留，需补；
F) 以上完成前不得封板。

【工作流纪律】Unity 由制作人自己打开并人工验收；开发侧不打开 Unity、不跑 Unity Test Runner/batchmode、不跑大量自动化测试，
只做离线编译（tools/offline-compile.ps1）、静态检查、Content 校验、git diff --check。每轮给制作人一份可复制的唯一指令 +
简短验收 checklist；制作人确认验收前不要 commit/push，除非明确授权 checkpoint。不要把 commit 里的口语短句当 milestone 状态，
状态以 docs/40-process 的 Producer Acceptance 为准。实质改动更新 docs/40-process/42-devlog.md，重大决定写 ADR，术语走
docs/00-project/03-glossary.md；不要修改已封板的历史文档（240/242/243/244）。Core/Data 禁止 UnityEngine；随机用 IRandomSource；
WorldTick 是唯一世界时间轴；RelationshipLedger 是唯一关系真源；Dead ≠ Removed。

【CIVILIAN-LIFE-01 P1 当前实现补充（2026-09-28）】人物命令已统一为物理 approach：Attack/Capture 追逐移动目标并在到达后才触发 Encounter/capture request，Recruit 到达后进行 5 秒非缩放轻交互，Release/Execute 同样要求近距。Flee 已改走 MovementIntent 与 Host CompositeWalkGrid，持久保存来源 Site/出口序号并在失败时轮换；StorageRoom 有 8 个访问槽并在 A* 失败后轮换。有效世界命令解除 ManualPaused，ModalHardPaused 不受影响。第一发致命伤只进入 Incapacitated，死亡确认记录原因，Encounter 生命倒计时使用非缩放现实时间。荒村 `place_wall_040130496` 缩短一格恢复巡卫住房门洞。Site defense 改为 `DefendedSiteId` 显式绑定：主管单人驻军守荒村，巡卫三人属于无绑定 patrol squad。Displaced 近场保持真实人物 idle／基础 Needs，离屏复用 BackgroundCharacterTravel 与低频 Needs 数据推进，且不再有失守 Site 公粮权限。状态：**Implementation Complete / Producer Acceptance Pending**；需制作人人工验收，未提交。

【历史建议，已完成，禁止执行】SPACE-01 hardening、MAP-04 consumer audit 与物理清理、LEGACY-FINAL-A/B/C 均已验收封板。

【入口文档】交接 docs/40-process/247-project-handoff-current-state-2026-09-18.md；SPACE-01 = 246；MAP-04 = 245；
系统真源 = docs/20-systems/2N-continuous-surface-world-authoring-and-composition.md；决策 ADR-0036 / ADR-0037；
路线图 docs/40-process/41-roadmap.md；日志 docs/40-process/42-devlog.md
```

---

# 22. 本轮未做实现修改的确认

- 本轮**没有**修改任何 Gameplay 逻辑、Content 逻辑、ExternalTools 功能。
- 本轮**没有**打开 Unity、没有运行 PlayMode / Unity Test Runner / batchmode。
- 本轮**没有**继续实现任何 TODO，没有修 SPACE-01 deferred hardening、没有做 MAP-04 Hex cleanup、没有删除 Editor。
- 本轮**没有**执行 `git add` / `git commit` / `git push`。
- 本轮实际执行的验证仅为：只读 git 状态查询、文档阅读、Content JSON 解析核对、`findstr` 静态检索、`tools/offline-compile.ps1`（Core/Data/Unity 离线编译 → `ALL_OK`）。
- 工作树中出现的 `c05a3d2` 提交由**制作人从外部提交**，不是本会话所为（见 §2 并发提交说明）。

---

# 22.1 ROOM-01／ROOM-02 current handoff（2026-10-14）

状态：**A／B／C Implementation Complete / Producer Acceptance Pending**。

- 正式建设包含六个房间模板、独立 1×2 床和木／石墙段；ROOM-02 将外轮廓定为普通 4×6／中级 8×12／高级 16×24，成本、工时、耐久与独立墙段不变。房间一次工单产生地面、外墙和南侧两格门洞，建房不送床。
- `RoomBedFacilityService` 是住房容量、用途、长期分配、临时预约和实际占用真源。合法使用点从真实网格、净内区与家具冲突解析；自动睡眠、玩家 `BedRestAction`、医疗搬运和 Detained 床位共用本地 Surface／Site 门禁，专属床不能被他人占用。
- 荒村作者源与正式 runtime 已发布为 22 房／27 床，20 个开局人物按 SpawnKey 唯一绑定。旧三组住宅、旧医疗／囚室、四个遗留设施、15 段旧墙和五个住房 WorkArea 已删除；新房紧凑迁回清空区域，主管为 8×12 中级石房，医疗房 3 床、牢房 4 床。
- Snapshot 当前为 v15；Restore 校验作者身份与不可变几何，不比较可变 `RoomUsage`，也不重播初始分配。LevelTester 实际相机 maxSize 为 44（原 22），默认开局缩放不变。全程序集 offline compile `ALL_OK`，ContentAuthoring solution 0 error；未启动 Unity，未运行自动测试或人工游玩。

# 23. FINAL-SEAL Consolidated Acceptance Closure（2026-09-21）

状态：上一轮 **Producer Accepted / Sealed**；后续 Legacy-only 最小静态尾项已完成，保持未提交。

熟练系统现在从正式学习、自然增长、修为灌注、突破、效果重挂到 Snapshot 恢复使用同一份已加载 profile。当前全部功法／斗技均显式配置 entry→minor→major→perfect→transcendent，四段门槛为 20/30/40/50，灵草与粗木各需 1/2/3/4。Host 学习仪式在开始和完成时都调用 Core 正式资格；关闭详情页后，选中该角色的常驻面板仍绘制仪式读条。重复学习已知技能幂等且不消耗 RNG。

Snapshot 在 Content rehydrate 后统一规范化 mastery 门槛并幂等恢复 modifier／修炼速度；同时修复 NPC-only Snapshot 被错误要求 ControlledSquadId 的集成回归。任务 `learnmanual` 保持立即直授例外，满熟练不影响正常 CultivateAction 或斗技施放。

关联复核另发现并关闭失能 NPC 在 Action 启动前仍可预占工位、Host 农作／拆毁会话只拦 dead 而未拦 incapacitated 的可达序列；现统一使用自主行动生命资格，且只释放本人的预约／表现移动。

验证：Core／Data／Unity／Tests 离线编译通过；稳定 headless 集中矩阵 40/40，覆盖熟练链、行动取消与预约、奖励事务、普通户外授权、singleton／Surface／Return／Interior 关键回归和 Snapshot。未启动 Unity，未运行 PlayMode、Unity Test Runner 或 batchmode。

未扩协议：剧情 Boards 的完整永久存档、特殊空间公库继承仍未定义。对应轮次的运行行为人工验收已由制作人确认通过；这些未定义政策不是本专项授权的新实施任务。

# 24. Legacy-only concentrated closeout（2026-09-21）

状态：**Producer Accepted / Sealed（2026-09-22）**。本节原 Pending／smoke 说明仅保留为当轮历史记录，不再是当前任务。

本轮删除匿名 modal pause 兼容计数、无调用 StrategicClockFreeze host-presentation 闭包、Host saved-speed／旧原地战包装、退役住房 UI 字段与未使用 snapshot 局部变量；保留具名 pause owner、现代 CharacterEncounter freeze、ManualPaused 和输入限制。该测试契约当前现名为 `LegacyPartyFocusCompatibility.SyncPartyFocus`，因 EditMode 回归仍有四个直接调用而保留。

代码注释与局部命名已从 Army／StrategicEncounter／WORLD_COMBAT 旧语境改为 Squad／CharacterEncounter／owner；`HexMetrics` 明确只服务 legacy Hex、工具与测试，不是 Continuous Surface authority。项目入口、systems／tech 正文、路线图、ADR 索引与本交接统一指向 ADR-0038；§1～22 的旧 WIP、Resume Order 与复制指令已归档且禁止继续执行。

验证：Core／Data／Unity／EditMode Tests assembly 离线编译 `ALL_OK`；现代 CharacterEncounter 定向纯 C# 回归 8/8；目标符号、已知链接与 `git diff --check` 通过。旧 Phase2C Hex／Outdoor LocalMap 测试套件仍在共同 fixture 前置失败，本轮未删除或改写这些历史测试。

上一轮窄范围 smoke 覆盖具名 modal 暂停恢复、CharacterEncounter 全流程、Continuous travel／WorldMap 与合法旧档兼容读取；不要求重跑 A／B／C、MAP、SPACE 的完整历史验收。

制作人已确认上述上一轮人工 smoke 全部通过。其后静态尾项只删除空 `StrategicDayHandler`／bootstrap 注册、恒空 `LegacyArmyId` 诊断，并修正 `HostDemoTileMap.EndInstanceBuild` 注释；不要求重跑玩法验收。`SyncPartyFocus` 因 `PlayerPartyContinuousWorldPhase2CTests.ARRIVAL_02／07／11` 共 4 次调用而继续保留，仅服务旧 travel fixture 的防回写契约，不是现代 Gameplay 入口。静态尾项离线编译 Core／Data／Unity／Tests `ALL_OK`，引用复查与 `git diff --check` 通过。

# 25. Hex／Army naming compatibility documentation closure（2026-09-22，历史实现）

状态：**Producer Accepted / Sealed（2026-09-22）。** 本节保留物理退役前的当时事实；当前实现以页首和 ADR-0038 §6.1 为准。

- **A — 当时的 Army Content 输入：** 外部协议曾由 runtime adapter 接受；当前 Loader 已拒绝，三类 identity 规则移至离线转换器。
- **B — 稳定 Snapshot／Encounter 协议：** `SquadCommandKind.LegacyFormalArmyWorldMotion = 2`、Encounter spatial `LegacyFormalArmy = 2` 保持数值；`EncounterCharacter.LegacySourceFormalArmyId` 对应 wire `sourceFormalArmyId`。PlayerParty Snapshot `CurrentHexQ/R` 与 JSON `currentHexQ/R` 不改。
- **C — 当时的 Hex 边界：** runtime 容器与 Core Hex 几何随后已物理删除；当前只有离线转换器实现必要的历史 Odd-R 换算。
- **D — PlayerParty compatibility：** `PlayerPartyWorldMotion` 的旧专用 API 已逐成员显式 Legacy：`LegacyCurrentHex`、Legacy path／destination、`LegacyHexSegmentIndex/Progress`、Legacy departure／travel presentation 状态，以及无 SurfaceId 的 WorldPosition 写入、Hex-center snap 与旧 route 方法均不再以普通名称暴露。正常 Surface 上 `LegacyCurrentHex` 是兼容摘要；旧 executor 中可为已提交路线格／旧缓存，不保证即时投影。`LegacyPartyFocusCompatibility.SyncPartyFocus` 只服务旧 EditMode fixture。
- **E — 当时的尺度边界：** 旧 Hex 尺度适配随后已由 `outdoorSurface.movementScale` 唯一注入取代；正常 authority 始终为 `SurfaceId + exact WorldPosition`、`SquadWorldMotion`、`CharacterEncounter`。

本轮复验：Core／Data／Unity／Tests offline compile `ALL_OK`，阈值定向纯 C# 检查 10/10，`git diff --check` 通过。本轮未启动 Unity，未运行 PlayMode、Unity Test Runner 或 batchmode。

# 26. WorldSite／Hex footprint naming documentation closure（2026-09-22，历史实现）

状态：**Producer Accepted / Sealed（2026-09-22）。** 本节登记物理退役前的当时磁盘实现名；当前实现以页首和 ADR-0038 §6.1 为准。

- `WorldSite` 当前兼容成员为 `LegacyAnchorHex`、`LegacyPresenceHex`、`LegacyOccupiedHexes`、`SetLegacyHexFootprint`、`EnsureLegacyPresenceHexValid`、`HasLegacyPresenceAnchorMismatch`、`EnumerateLegacyFootprintHexes`、`OccupiesLegacyHex`；旧 `WorldSite.HexCoord` alias 已删除。
- `WorldSiteBoard` 当前查询为 `TryGetAtLegacyHex`、`GetSiteIdsAtLegacyHex`、`TryResolveLegacySitePresenceHex`。相关类为 `LegacyWorldSiteHexLocationCompatibility`、`WorldSiteHexFootprintValidator`、`WorldSiteHexFootprintSpatialMapping`、`WorldSiteHexFootprintBakeTransform`；`WorldSitePhysicalRegionQuery` 与 `WorldSiteOutdoorMigrationPolicy` 保持现名。
- 当前 compatibility invariant 强制 `LegacyPresenceHex == LegacyAnchorHex`。`LegacyPresenceHex` 是旧兼容代表格，不随 Surface 位置即时派生；`DerivedPresenceHex` 才是 `WorldToHex(CanonicalWorldSurfacePosition)` 的独立只读结果。
- Hex footprint 的非空、六邻接连通、star-shaped non-empty kernel 三项规则只约束旧 Hex footprint 输入，不是 Continuous Surface、SiteCore 理论范围或 Actual Administrative Control 的通用几何规则。`WorldSiteHexFootprintBakeTransform` 仍有 `sitePlacements`／`sitePlaces`／`OpeningEntityAnchors` 与旧 LocalPosition migration 消费者，不是 dead legacy runtime。
- 历史页 ADR-0027、212、213、245 仅增加旧名到现名索引；正文历史、旧 devlog 与已 superseded 的 ADR-0025 等保持原样。外部 `anchorQ/R`、`presenceQ/R`、`footprint[]` key 不变。
- `WorldVec2`、`HostStrategicRosterPanelLayout`、`PlayableHostSession` 的代码改动仅为注释纠正，无行为变化。Core／Data／Unity／Editor／Tests／PlayModeTests／Assembly-CSharp 离线编译 `ALL_OK`；旧精确符号、外部 key、脚本 GUID 唯一性与 `git diff --check` 均已复核。未启动 Unity，未运行 PlayMode、Unity Test Runner 或 batchmode；工作区保持未暂存、未提交。

# 27. EVENT-EDITOR-V2.5 可读人物选择（2026-09-23）

EventEditor 的人物入口统一使用 Shared `CharacterPicker`，目录 authority 是当前已加载 ContentPackage 的 `character` definitions，而非 Authoring CSV。制作界面显示中文名、稳定 ID 与定义来源，支持搜索和来源筛选；Event Inspector、新建事件与指定 Speaker 共用实现。左侧 Browser 保持中文名主标题与灰色 ID，来源仅放 Tooltip。磁盘 Event 仍只保存 DefinitionId，未修改 Runtime、Content schema/组织或 Graph 行为。

# 28. EVENT-EDITOR-V2.6 Repeat 语义与全局人物来源（2026-09-23）

Resolver 现状已满足 Repeatable／Once／Priority／fallback 契约：`once=false` 不读写 fired gate；Once 只在完成时按 scope 写 fired；候选只返回最高 Priority 同层。当前“验收·问点私事”为 P50 repeatable、0 Conditions，P100 完成后在没有更高层时每次交谈都应重新进入 P50 candidate layer。Runtime 未修改。

EventEditor 顶部统一持有人物来源 filter，四个人物入口与按对象 Browser 共用；Picker 内不再有独立来源下拉。被筛掉的当前 binding 保留并提示，不写 Content、不 dirty。重复与保底文案已拆清，未改 Graph、Opportunity、Snapshot、Quest、Character schema 或 Content layout。

# 29. EVENT-EDITOR-V2 Final Patch 与 Producer Acceptance Seal（2026-09-23）

顶部人物来源现按规范化绝对 Package Root 保存在 `%LOCALAPPDATA%\XianXia\EventEditor\settings.json`；重开同一 Package 时只恢复仍存在的实际 source，否则回退“全部来源”。设置读写失败仅在状态栏提示，不阻止编辑；恢复和切换均不进入 Content、layout、Undo 或 dirty。

制作人确认 EventEditor V2 全范围实际验收通过。当前正式状态为 **Producer Accepted / Sealed**；EVENT-02 Opportunity Director、随机 NPC/Object、tag/archetype binding、ContentIntent、NPC 找玩家／跟随、概率、剧情持久化、新 Graph node 或新 Runtime framework 均未实施，属于后续里程碑。
