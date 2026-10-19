# 270 当前阶段完整人工验收表与下一步建议

> 2026-10-19 制作人验收覆写：WorldMap 玩家／NPC 小队标记缩放与 U08 逐窗输入锁已获“验收了没有问题”；随后提供 U06 搜索输入截图，并确认未新开窗口、未改变暂停、背景房间窗口原本已打开。U06／U08 均在本次观察范围内 Producer Accepted。旧“输入旁路／legacy bool 覆盖”描述是修复前事实。

> 日期：2026-10-11  
> 用途：制作人填写本轮人工结果，并据此确定返修与封板顺序。  
> 基线：dev_openworld，HEAD 8ec01d2，加当前未提交的 LevelTester 单功能页、折叠菜单与关闭修复。  
> 本表为验收工作单，不替代 [267 当前功能状态表](267-civilian-life-01-design-freeze-2026-09-27.md#当前功能与验收状态表唯一维护点2026-10-10)。人工结果尚未填写，不改变任何里程碑的验收状态。

当前应完成 CIVILIAN-LIFE-01 与 UI 收尾的人工验收；DELAYED-EVENT-01 仍缺正式人工结论，可在本轮补齐。代码已确认囚犯食宿早退、凡人日程与课表 UI 不一致，以及文本输入未被所有快捷键尊重。先记录这些缺陷，再验其余独立链路，能够直接确定下一轮的修复范围。

## 1 当前阶段与本轮边界

| 范围 | 现有状态 | 本轮处理 |
|---|---|---|
| CIVILIAN-LIFE-01 | Implementation Complete / Producer Acceptance Pending；有已记录缺口 | 主验收；独立判断食宿、工作、建设、战后命运、救援、押送、招降和存读档 |
| LevelTester 单功能页、二级折叠菜单、关闭修复 | Implementation Complete / Producer Acceptance Pending | 主验收；文本输入与共享输入标志列为优先复现 |
| DELAYED-EVENT-01 | Implementation Complete / Producer Acceptance Pending | 补人工专项结果；以当前 v14 保存队列，不再按历史 v10 建档 |
| SHOP、AUCTION、SOCIAL、DYNAMIC、QUEST-INSTANCE、控制接替与继承 | Producer Accepted / Sealed | 关联冒烟，发现回归才返修对应入口 |
| Continuous Surface、Squad、CharacterEncounter、地图退役封板 | Producer Accepted / Sealed | 沿用现行 authority；不重启迁移或按旧名称扫描删除 |
| Medic 自动治疗、Repair、普通建筑拆除、玩家医院／监狱建造、Auction-02、贡献兑换、装备／制作、物流、战略 AI | 延期或未授权 | 不纳入本轮通过条件 |

结果填写：**通过／失败／前置不足／未验／延期**。失败记录实际观察，不以工具显示“准备成功”、编译成功或历史封板替代人工结果。同一缺陷阻断后续项时，写“被某项阻塞”，不重复等待。

## 2 开始前的实际配置与操作纪律

- 直接打开现有 Assets/Scenes/LevelTester.unity，建议先用 1920×1080。不要按 114 的旧地图参数重新配置，也不必重跑场景创建菜单。
- Content package override 为空，使用 Content/BaseGame；opening 为 base:scenario_ch01_reference，roster 为 base:roster_level_tester。正常出生在 base:surface_main_wilderness_v1；Inspector 中的 ch01_cave_map 是独立空间回退配置。
- 新开局的玩家势力是荒村宗主的直接附庸。劳动许可不等于据点管理权。需要自己管理 Site 的分支，先正式取得据点或使用对应工具准备合法前置。
- 当前 Scene 1x 为 3 现实秒／Tick，1 日为 288 Tick。忽略暂停时，完整一天约为 1x 14.4 分钟、5x 2.88 分钟、20x 43.2 秒。通用 SimulationTickPacing 的 1 秒常量不能替代该 Scene 配置。
- 先 1x 看动作，后 5x／20x 看长时间分支。取食、睡眠、救援、押送、施工、逃亡必须让正常 Host 帧循环实际运行。Core Tick／日界线工具只推进数据，不能证明真实到达。
- 保存工具只有 vs04_slot0.json 单槽。开始前从工具显示的实际路径备份原档；每次“保存”会覆盖该槽。不同分支的样本另存文件备份，避免把旧结果丢掉。
- 使用新开局生成的当前 v14。v1–v13、以及缺少新 authority 字段的早期 interim v14 均可能被明确拒绝，不属于本轮正常恢复失败。
- 每段先记录主控、所选人物 EntityId、Faction、Site、Needs、Loyalty、职业、Tick 和速度。需要接管／招募时记录变化前后。
- 四个食宿整场景准备入口已于 2026-10-13 删除。食宿验收使用仍保留的“设置饱食”“设置精力”“添加公库粗粮”“清空公库粗粮”组合前置；样本仍须是 Normal、非 PlayerParty 的普通受管凡人，床相关样本还须已有合法 Normal Residence。
- 医疗与囚犯一键准备均已删除。累计占用／容量须由正式流程或已有存档样本形成；没有足够样本时记录“前置不足”。
- 先做 UI，再分别做生活／建设、占领／捕获、救援、押送／招募。破坏性全队弥留／死亡回归放最后并使用独立新局。本轮可以分段完成，不要求一次连续游玩全部条目。

## 3 最先验收的工具与输入

| 编号 | 操作与前置 | 通过标准 | 代码核查结论 | 结果与备注 |
|---|---|---|---|---|
| U01 | 打开开发工具，逐组浏览九组及 74 个入口；只打开页面，不执行全部作弊 | 每个入口可达；右侧始终只有当前功能；空选／多选的人物操作不能偷偷使用主控 | 已实现，待人工验 | ____ |
| U02 | 默认打开后展开三个组，再收起当前页所在组 | 默认仅当前组展开；各组独立；收起不改右页、不执行操作 | 已实现，待人工验 | ____ |
| U03 | 记录展开状态；用中文搜索收起组的小功能，选中结果并清空 | 搜索临时显示命中组；原展开集合恢复；右页保留新选择 | 已实现，待人工验；先用中文避免快捷键缺口干扰 | ____ |
| U04 | 左右分别滚到底；窗口内滚轮、中键、右键；移到窗口外再操作 | 左右滚动独立；窗口内不缩放／平移／选中／命令世界；外部按原规则 | 已实现，待人工验 | ____ |
| U05 | 搜索框有焦点时分别点关闭按钮中心、文字、边缘；拖标题空白；重开 | 所有按钮位置都可关闭；不拖窗；当帧不穿透、下一帧无幽灵遮挡；重开偏好保留 | DragWindow 已改；外部 Hide 的 GUI 捕获延后清理仍需观察 | ____ |
| U06 | 新局暂停并选中主控，记录灵草／命令模式；聚焦搜索框，分别输入 j、b、m、空格、q、e、g；每字母独立观察，异常后清场或重开 | 只改变搜索文字；不打开其它窗、不改变暂停、不武装移动／交互、不下发行动 | **Producer Accepted（本次观察范围）**：制作人截图显示字母进入搜索栏，补充确认无新窗、无暂停变化，房间窗原本已打开；最近操作未见变化。其它文本输入点不外推 | 通过 |
| U07 | Session Reset、重置凡人验收场景等保留的二次确认进入待确认后，切页、关闭、重开；只按取消 | 待确认全部取消；不自动执行；当前工具／搜索／折叠偏好保留；业务未变 | 已实现，待人工验 | ____ |
| U08 | 人物页／修炼／工作／对话分别打开时试窗口外滚轮、中键、箭头和选人；若允许叠开，关一窗后继续观察另一窗 | 当前模态仍保护世界输入；关闭仅释放自己的暂停和输入；原手动暂停保持 | **Producer Accepted**：制作人确认本轮输入锁修复验收无问题；未单独声称每一种不可叠开的组合都已实际出现 | 通过 |
| U09 | 分别从运行／手动暂停两种底层状态打开并关闭普通模态 | 最后窗口关闭回到原底层暂停状态；仍有其它 modal 时不能恢复世界 | Named modal owner 已存在；与 U08 分开判断 | ____ |
| U10 | 逐窗执行第 7 节的按钮／返回／取消／结果关闭 | 可见状态结束、无自动重开、无续绘误点；已完成事务不回滚 | 入口修复与既有 handler 需真实点击验收 | ____ |

U06 或 U08 失败时，先记录一份最短复现。后续业务仍可在新局、用鼠标菜单构建前置后观察；受输入干扰的结果标“阻塞”，不能据此判 Gameplay 失败。

## 4 CIVILIAN 正式闭环

| 编号 | 操作与前置 | 通过标准 | 当前判断 | 结果与备注 |
|---|---|---|---|---|
| C01 | 选普通凡人、修士、永久玩家角色和敌方人物；对照底部卡、完整人物页、势力人员／工作页 | 势力与正式 Loyalty 一致；切选实时更新；没有 mortal-only／NPC-only 显示差异 | 已实现，待验 | ____ |
| C02 | 选择本场无 FactionMembership 人物；再在 C24 招募成功时对照新身份 | 无势力“忠诚：—”；换势力后立即读新势力 Loyalty，不缓存旧值 | 无势力样本不存在时记前置不足；招募分支可独立验证 | ____ |
| C03 | 唯一选中 Normal 非队员凡人，设置饱食 10、精力 80并添加公库粗粮，关闭工具后 1x 观察 | 实际前往 StorageRoom 访问点；出发前不扣粮；到达才扣一份并进食；Satiety 增长 | 仍须合法取粮权限；独立工具不移动人物或清理其其它状态 | ____ |
| C04 | 对有合法 Normal Residence 的同类人物设置饱食 80、精力 30 | 返家途中不恢复精力、不算已睡；到家才睡，达到醒来条件后结束 | 若人物正被其它移动 authority 接管，先按正式玩法解除 | ____ |
| C05 | 对合法普通凡人设置饱食 80、精力 5 | 精力 5 时就地睡，不先远行找床；地睡恢复慢于床 | 无住所分支需另找自然形成的无住所样本 | ____ |
| C06 | 清空公库粗粮并设置饱食 10、精力 5，诊断确认所有 food 来源都为空 | 缺粮原因可见；能紧急地睡；不在取食／睡眠之间振荡 | 清粗粮不保证其它 food 资源为空，先检查库存 | ____ |
| C07 | 饥饿 10、精力 80、合法 Site 清空 food；观察失败后再加粮 | 无粮不假进食；失败有原因、有界重试；加粮后重新走真实取食 | 与 C06 分开，避免低精力优先掩盖取食失败 | ____ |
| C08 | 工作中的 Farmer／HerbFarmer 设低需求，随后恢复需求；并观察多人取食 | 旧田格／旧路释放；需求打断工作；恢复后可工作；多人不得把库存扣成负数 | 无粮争抢样本不足时记录前置，不人为改 authority | ____ |
| C09 | 在己方 Site 的正式工作／人事 UI 单选 Unassigned、Farmer、HerbFarmer、Logger | 每人只一个职业；生产目标同 Site、同 owner；产物进入公库；非己方、囚犯和不合法目标不能管理 | 生产本身用正常帧推进；Medic 自动治疗延期 | ____ |
| C10 | Needs 满足的职业人物跨 08:00 与 18:00；休息时另有合法伤员 | 正常 Work／OffDuty 如实显示；休息不主动开专业生产；紧急救援可打断休息 | 固定日程执行已实现；与 C11 分开 | ____ |
| C11 | 改 managed mortal 的旧可编辑课表，对照工作面板与实际行为 | UI 提供的正式日程与实际 authority 一致 | **已知缺口 B**：UI 读 ScheduleComponent，执行读固定 08:00–18:00；先记录，不反复等一天 | ____ |
| C12 | 主控在己方 Site，以正常资源与可工作凡人前置，从正式建设 UI 放置当前合法农田／恢复处／储藏室 | 工人取材、搬运、交付、施工，完成一次；材料只扣一次 | 不再有一键建设准备；前置不足记“前置不足”，不要把它误记为玩法失败 | ____ |
| C13 | 建材搬运或施工时打断工人；单独验当前已有正式势力旗拆除入口 | 人物／载荷／工单无丢失，按正式规则取消或重新领取；拆除结算不重复 | Repair、普通建筑拆除不在当前范围 | ____ |
| C14 | 新局普通攻击任一巡卫；另一路正式攻击荒村核心 | 巡卫按三人 patrol 入战；核心守军按显式绑定收主管，不按“站在据点”抓全部居民 | 真实 Encounter roster 诊断为判据 | ____ |
| C15 | 正式核心战攻破并接管；观察阿青 10、阿土 60 | 阿青留原地待降；阿土真实缓慢逃亡；不自动换势力、不删除、不传送；只有正式接管改变 Site owner | 不用工具直接改 Owner 代替正式易主验收 | ____ |
| C16 | 远处追击逃亡者，选“制服并俘获”，完成正常战斗；另一次取消／失败 | 目标继续逃；接近才发起 Encounter；胜利且仍可捕获才 CapturedEscorted；取消／失败／死亡不错误俘获 | 当前无跳过俘虏战的一键准备，按正式链验收 | ____ |
| C17 | 逃亡者越过 Site 加 buffer；先近场观察，再离开其加载范围 | 近场保留真实人物，清旧工作／住房／粮权；离屏才后台避难；到达仍不自动分配身份与住房 | 无己方目标 Site 时保留 Displaced 原位；样本条件须记录 | ____ |
| C18 | 用保留的弥留工具或正式战斗建立同势力伤员，并保证有可工作的同势力凡人 | 救援者实际接近→搬起同一 Entity→搬到具体恢复点→放下；记录 SpotId；HP／生命周期不因送达自动治疗 | 不再有一键医疗准备；医疗区域中心不算目标 | ____ |
| C19 | 保留 C18 已送达，再添加同势力弥留者；累计使用三个点 | 1／2 优先；两点满后区外 3 仍有效；每点一人；碰巧在医疗区不算已送达 | **多人前置成本较高**；需足够已招募同势力人物 | ____ |
| C20 | 两名可工作的救援者、多个伤员；三个点满后再有第四名 | 不抢同一伤员／同一预订；无空位不假完成；中断释放预订 | 当前没有一键多人／满位准备；未构建场景时记前置不足 | ____ |
| C21 | 救援搬运中制造紧急需求或令搬运者参与 Encounter | 伤员在搬运者当前合法位置放下；carry 和预订清除；不瞬移到目的地 | 诊断记录 Target、Spot、Carrying 和实际位置 | ____ |
| C22 | 通过 C16 正式俘获或读取已有 CapturedEscorted 样本；远处右键交接点，再到点使用正式安置 | 真正押送至 prison_dropoff；远处不可 Detain；成功 Detained、Carrier 清、占用＋1；旧势力／Loyalty／Needs 保留 | 不再有跳过俘虏战的一键押送准备；交接点与囚室 Inspect 共用距离规则 | ____ |
| C23 | 保留四名 Detained；押第五名至点，另做押送中断 | 容量 4 满后拒安置；第五人保持合法押送或按中断规则放下；不丢人 | **容量样本前置不足可单列**；样本须由正式流程或已有存档形成 | ____ |
| C24 | 单独把 Detained Loyalty 设 10；正式接近招募，等待约 5 现实秒；另一次移动／暂停／取消 | 成功为 PlayerFaction、Loyalty 50、Unassigned；不自动入 PlayerParty；取消不转籍；显示立即刷新 | 可独立验招募；设置 10 不证明自然照料降忠诚 | ____ |
| C25 | Detained 有合法囚室和公粮，低 Needs，正常帧推进完整照料日 | 吃粮＋正常睡眠的完整日才 Loyalty −5；没粮／未睡不得下降 | **已知缺口 A**：Captured 被 Tick 的非 Alive 早退跳过，当前无法自然形成新照料日；无需重复空等 | ____ |
| C26 | 招募后在人事页改职业、主动加入队伍、切 Active，再普通 Stop Follow／离队 | 永久可管理资格正确；队伍行为正常；离队保留 Faction／职业／Needs／Loyalty | 与临时 Quest Companion 权限区别对照 | ____ |
| C27 | 两份独立样本分别执行囚犯释放、处决 | 释放清关押并真实逃离；处决走正式 prisoner action；Dead 仍保留 Entity；不回滚他人状态 | 与自由人物正常 Encounter 分开 | ____ |
| C28 | 对普通非可控 NPC、临时同行及永久队员依次选中 | 全部可查看；不可控者无手动操控／普通 Stop Follow；永久队员仍可正常管理 | 关联权限回归，实际按钮与后端一致 | ____ |

## 5 存档恢复必须覆盖的状态

每行以当时画面／诊断为基准，保存→推进或改变一点→读取，比较原 EntityId、身份、位置、库存与状态。食物获取路线、救援预订可以重评估；正式送达关系、材料载荷和身份不能丢失。

| 编号 | 保存样本 | 读取后的通过标准 | 注意 | 结果与备注 |
|---|---|---|---|---|
| S01 | C03 取食途中与 C04 返床途中各一次 | 身份、Needs、位置保持；重新合法取食不重复扣粮；返床不直接当已睡 | 瞬态路径不要求逐节点相同 | ____ |
| S02 | C18 已送达伤员 | 同 SpotId／EntityId／精确位置；单点占用保持；不重新搬运或自动治疗 | 必须先有真实送达关系 | ____ |
| S03 | C21 之前的救援搬运途中 | 同一伤员保留在合法位置，运输可安全重新求值；没有永久隐形／重复 Entity | 不要求保存瞬态 reservation | ____ |
| S04 | C22 押送途中、安置后各一次 | CapturedEscorted 的 Carrier 与位置一致；Detained 的囚室、占用、原势力／Loyalty／Needs 保持 | 不能用 C25 当前失败证明持久化错误 | ____ |
| S05 | C12 材料搬运和施工中各一次 | 工单、delivered 材料、单人载荷／劳动进度保持；不二次扣材或重复建成 | 正常帧推进才能完成 | ____ |
| S06 | Fleeing、Displaced、C24 招募后分别保存 | 逃亡／避难身份与目标保持；招募后新 Faction／Loyalty 50、职业、队伍关系保持 | 各样本独立备份单槽 | ____ |
| S07 | 对话／Active ContentEvent、Encounter Preparing、Committed 未关战报时尝试保存；再结束后保存 | 禁存状态明确拒绝、不覆盖正常档；合法状态保存／读取成功；工具旧结果／确认清理 | 旧版本拒绝可用备份旧档验证；没有旧样本记未验，不手工破坏当前唯一档 | ____ |

当前 schema 为 v14。v1–v13 的拒绝是既定策略；同为 v14 不代表所有早期临时格式合法。不要为了“兼容通过”覆盖当前存档或重新应用 Content 初始值。

## 6 补验延迟事件与最小历史回归

DELAYED 尚未有正式人工结论，以下是本轮应补的专项。商店／拍卖／同行等已封板项只做代表性回归；失败后才按实际影响扩大范围。

| 编号 | 操作与前置 | 通过标准 | 范围 | 结果与备注 |
|---|---|---|---|---|
| D01 | 生成两名同模板行商→正式交谈→“约定之后再谈”→“明日再谈” | 不立即执行；队列有独立 ID、原 Target 和 ExecuteTick；到点安全呈现一次 | DELAYED 待验；完成较高优先级旧话题后再找该话题 | ____ |
| D02 | 到期前保存／读取；行商 A／B 分别预约 | 不重新起算一日；Actor／Target／Opportunity 身份不串实例 | DELAYED 待验 | ____ |
| D03 | “明日再谈两次”；完成两个后续 | 同 EventId 的两个实例各执行一次，不覆盖，也不被 once 吞掉 | DELAYED 待验 | ____ |
| D04 | “六日后再谈”等原行商过期；另次设置后续条件不成立 | 原对象失效／条件失败明确取消，不找同模板替身；诊断有原因 | DELAYED 待验 | ____ |
| D05 | 已到期条目遇到正式对话／模态／战斗或过渡，再返回安全世界 | 不抢插；恢复安全后呈现；WorldTick 冻结时不伪造到期 | 先看真实 ExecuteTick；点击次日零点不一定已过完整 288 Tick | ____ |
| D06 | EventEditor 添加 scheduleEvent，选目标与正整数日，保存重开、Undo／Redo；再取消编辑 | 值与 dirty 正常；无新节点；取消不改正式内容 | 使用副本／Working Copy，不保存修改到生产验收 Content；仅有运行验收而未验 authoring 要如实标记 | ____ |
| R01 | 指定商店买／卖一次、关闭、再读档；普通 NPC 对照；只有中品余额试下品商品 | 有限库存／资金保持；出售不回 sale stock；无跨档支付；普通 NPC 无交易 | SHOP 已封板冒烟 | ____ |
| R02 | 拍卖出价、被超价退款；寄拍成交或流拍 Claim，关闭／读档／领取 | 同档 escrow／退款与物品托管正确；Claim 只领一次；关闭不撤销拍品 | AUCTION 已封板冒烟；价格与市场竞争按既有样本，不新增竞拍规则 | ____ |
| R03 | 正式邀请秘境临时同行；选中、一起移动／进秘境／战斗；任务结束后离队 | 不可切控／手动 Stop Follow；AI 协战；结束依 Quest lifecycle 安全离队 | SOCIAL、SPACE 已封板冒烟 | ____ |
| R04 | 一个隐藏石碑与一个通知活动：接近／调查／明确解决／看历史／读档；开关地图旅行 | 未发现不泄漏；调查不误解决；Activity/history 保持；地图只定位镜头且旅行接续 | DYNAMIC／MAP 已封板冒烟 | ____ |
| R05 | 独立新局准备继承候选后全队弥留；另一独立新局全队真死亡 | 弥留接管与真死继承语义分开；原人物保留；新主控与相机在真实位置 | 最后执行；毁掉验收前置时重开局，不污染主样本 | ____ |

## 7 逐窗关闭的完整点击清单

每窗都检查：按钮事件到达；正确隐藏；只清自己的子页／待确认／暂停／输入／等待；不自动重开；同次点击不发给底层；业务状态保持。单独窗口通过不证明 U06／U08 输入隔离通过。

| 界面 | 本轮实际要点击的入口 | 应有结果 | 结果与备注 |
|---|---|---|---|
| LevelTester | 关闭中心／边缘、反引号、正式商店打开触发外部 Hide | 关闭，无捕获／焦点残留；重开无危险确认 | ____ |
| Character Sheet | ×、Escape，主体失效分支有样本再验 | 关闭整窗，人物选择／身份保持 | ____ |
| Cultivation | 主页关闭、熟练详情返回／关闭、确认取消、Esc／C | 返回／取消只退本层；整窗关闭清子状态 | ____ |
| Combat Arts | 列表关闭、详情返回／关闭、熟练确认取消、Esc、启动研读 | 只清相应层，已启动仪式继续 | ____ |
| Inventory 背包 | B、关闭、转学习提示 | 关闭不续绘，库存／已完成整理保持 | ____ |
| Inventory 仓库 | B、关闭、取出后关闭 | 正式库存变化保留，无关闭误点 | ____ |
| Construction | 关闭、Toggle，选择建造后转 placement | 工单不删除；placement 接续 | ____ |
| Mortal Work | 关闭、Toggle | 职业分配保持，无额外暂停 | ____ |
| Quest Journal | J、入口“关闭”、日志“关闭” | 任务／追踪保留，无暂停残留 | ____ |
| WorldMap | M、顶部关闭，自动旅行时开关 | 保留旅行接续；无本帧误发地图命令 | ____ |
| Strategic Character List | 关闭 | 子窗消失，地图保持 | ____ |
| Faction Diplomacy | 关闭 | 子窗消失，外交／人员保持 | ____ |
| FormalHud Inspect | 建筑、住房、恢复点、囚室、交接点各 × | 只清检视目标，设施／入住／伤员／囚犯不变 | ____ |
| WorldActivity 详情 | 关闭 | 活动保留 | ____ |
| WorldActivity 历史 | 关闭、从历史打开详情 | 正确隐藏／切页，不删除历史 | ____ |
| Shop | 成功交易后关闭按钮、Esc | root 隐藏，provider 等待释放，交易不回滚 | ____ |
| Auction | 出价／寄拍后关闭按钮、Esc | root 隐藏，escrow／listing／claim 不回滚 | ____ |
| Manual Learn | 普通取消、覆盖确认取消、Esc、成功转仪式 | 不学习的取消无副作用；成功只启动一次 | ____ |
| Combat Art Learn | 取消、Esc、成功转仪式 | 不重复扣物品或启动 | ____ |
| Cultivate Confirm | 取消、确认 | 取消不下令；确认一次后结束提示 | ____ |
| Breakthrough Ritual | channel 取消／Esc／F1，结果“知道了” | 取消与结果确认分开，不重新结算 | ____ |
| Skill Study Ritual | channel 取消／Esc／F1，结果“知道了” | 与突破相同，学习结果不回滚 | ____ |
| TicTacToe | 真正完成后的“结束” | 单次完成回调，不重复发奖励 | ____ |
| Content Interrupt | 任务通知“知道了”，真实事件合法 choice | 通知结束；事件按 choice 结束，不用隐藏跳过 | ____ |
| Dialogue | fallback、社交话题选择、任务讨论的离开／Esc；真正 ContentEvent 页合法 choice | 前三类可按 CanDismiss 离开；真实事件不加万能跳过；NPC 等待最终释放 | ____ |
| NPC ContextMenu | Esc、外点、攻击／拆除等确认取消 | 不穿透，不执行取消的业务，等待按原规则解除 | ____ |
| CharacterEncounter | 可取消 Pending“取消主动攻击”、结束战斗、Committed 战报“继续” | 阶段校验与 CloseReport 正式执行一次；自动／restore 无未定义退出 | ____ |

Dialogue 的 CanDismiss 包含 fallback、topic selection、quest discussion。269 的“仅 fallback 可关闭”措辞不完整；本表按实际 Model 分层。WorldMap 顶部关闭和外部 Hide 的同帧／捕获仍列为运行观察点，不把存在 handler 当作已点击验证。

## 8 已确认缺口与证据

| 问题 | 静态事实 | 对验收的影响 | 下一步建议 |
|---|---|---|---|
| 文本输入绕过部分快捷键 | LevelTester 在焦点时 Acquire owner，但 QuestJournal J、Inventory B、WorldMap M、Bootstrap Space、FormalHud Q/E/G 直接读 Input.GetKeyDown | 搜索可能打开窗口、改变暂停、武装命令，甚至下发行动；上一轮“文本焦点阻断完整”结论须修正 | 优先做一个最小输入收尾，所有相关用户快捷键尊重已有文本输入 authority，保留正式退出规则 |
| 共享 legacy bool 覆盖 | QuestJournal 每帧只聚合自己＋Inventory＋Construction；普通 Close 的 Clear 仅清两 bool，但这些 bool 为多个面板共享 | named owner 不会被删，仍可能误解锁依赖 legacy bool 的人物页／修炼／工作／对话；相机与选人受 Update 顺序影响 | 修相关窗口自己的输入 owner 或等价现有按 owner 规则，避免全局清锁；不新建窗口框架 |
| 囚犯食宿早退 A | Mortal Tick 在 AdvanceNeeds 后以非 Alive continue；Detained 仍为 Captured，下面吃／睡分支到不了 | 无法自然形成新的完整照料日；自然 Loyalty −5→招降闭环受阻 | 使合法 Captured＋Detained 进入正式照料分支，保留其它失能／死亡限制 |
| 两套日程 B | HUD 的 ScheduleComponent 可编辑；凡人实际使用 FactionMortalSchedule 的 08:00–18:00 | UI 可改但实际不变，制作人无法判断真实日程 | 先明确现行统一 Work/OffDuty 在 managed mortal UI 的语义，再最小接线；个人玩家课表另保留原语义 |
| 多人样本不足 | 当前没有一键多伤员／多囚犯准备 | C19／C20／C23 容易因前置成本被漏验 | 用正式招募、捕获或已有存档构建样本；无法构建时明确记“前置不足”，不能跳过真实搬运／交接 |
| 当前说明过期 | 总览仍有 CIVILIAN 未开工与 schema v10／v11；247 医疗摘要仍称区域交付；114 旧初始地图；269 对话关闭描述过窄 | 容易按错规则或错误配置验收 | 随收尾定向更新当前段落；保留标明日期的真实历史，不再复制第二份状态矩阵 |

证据文件：

- [MortalCivilianSystem](../../Assets/Scripts/Core/Npc/MortalCivilianSystem.cs)：69–75 正式日程；483–490 囚犯早退；736–751 囚犯吃睡；1083 起救援；1638 起 Detain 验证。
- [LevelTester 主窗口](../../Assets/Scripts/Unity/Host/HostLevelTesterCheatPanel.cs)：88 Hide；145 OnGUI；166 文本焦点 owner；264 折叠目录。
- [输入门禁](../../Assets/Scripts/Unity/Host/HostInputGate.cs)：全模态、仅世界交互和文字焦点 owner 分别持有；只在会话边界 ResetSession 全清。
- [QuestJournal](../../Assets/Scripts/Unity/Host/HostQuestJournal.cs)：J 与自身 owner；[Inventory](../../Assets/Scripts/Unity/Host/HostInventoryPanel.cs)：B 与自身 owner；[WorldMap](../../Assets/Scripts/Unity/Host/HostWorldMapPanel.cs)：M 与自身 owner。
- [Bootstrap](../../Assets/Scripts/Unity/Host/PlayableHostBootstrap.cs)：344 Space，现有 session 生命周期；[FormalHud](../../Assets/Scripts/Unity/Host/HostFormalHud.cs)：1625 Q/E/G；1939 IssueFocus。
- [LevelTester 内容工具](../../Assets/Scripts/Unity/Host/LevelTesterCheatContentSection.cs)：2026-10-13 已删除医疗／囚犯／建设／食宿／主管生命专用准备逻辑；保留独立数值、库存、定位和只读诊断。
- [WorldSnapshot](../../Assets/Scripts/Core/Persistence/WorldSnapshot.cs)：8 当前 v14；[JsonSnapshotSerializer](../../Assets/Scripts/Data/Serialization/JsonSnapshotSerializer.cs)：85 版本拒绝、114／130／334 新 authority 必填。
- [LevelTester Scene](../../Assets/Scenes/LevelTester.unity)：215507 起 Bootstrap 配置、215540 的 3 秒 Tick；[保存工具](../../Assets/Scripts/Unity/Host/HostLevelTesterSnapshotOps.cs)：9／14 单槽。
- [DialogueModel](../../Assets/Scripts/Unity/Host/HostDialogueModel.cs)：18 CanDismiss。

目前没有证据要求重构 PlayableHostSession 的 pause owner：Bootstrap session 对象复用，初始化／读取替换 World；Shop／Auction 还明确保留获取暂停时的 session。验收仍检查读档清理，但不为假设扩改架构。

## 9 根据结果决定下一步

| 结果 | 决策 |
|---|---|
| U06／U08 或任一关闭入口失败 | 先集中修 UI 输入与生命周期。关闭可靠之后再复验受影响业务，避免把输入故障当 Gameplay 故障 |
| C03–C10、C12–C24 中某个独立动作失败 | 提交编号＋最短前置＋预期／实际＋诊断，归入 CIVILIAN 当前 P1 缺口收口，优先修身份丢失、材料重复扣除、错误送达、远程 Detain、卡死 |
| A／B 仍存在 | CIVILIAN 不整体封板。A 已有正式规则可按现契约修；B 先明确 UI 与固定 Work/OffDuty 的关系，随后直接接线 |
| C19／C20／C23 只有前置不足 | 独立记录需补样本；不写通过，也不判 Gameplay 失败。后续用正式流程或已有存档补验 |
| S01–S07 恢复丢身份／占用／材料 | 存档正确性优先于体验扩展，保留原坏档和步骤后返修 |
| DELAYED 六项通过 | 单独补 Producer Acceptance；它与 CIVILIAN 状态分开，不从“后续模块已封板”倒推已验收 |
| UI 和 CIVILIAN 当前必需项全部通过，剩余只有明确延期 | 制作人明确确认具体范围后，才进行选择性封板；本表本身不授权 commit／push |
| 已封板模块某项冒烟失败 | 最小回归修复对应入口；不撤回已封板架构，不开启全量历史重验 |
| 当前阶段收口后 | 建议做一段 15–30 分钟正常玩法串联：劳动／公粮→食宿→夺点→俘虏／招降→人员／队伍→交易／任务。先判断是否可读、可理解、连续可玩，再讨论新的功能里程碑 |

推荐执行顺序：**U01–U10 → C03–C13 → C14–C17 → C18–C21 → C22–C28 → 各分支 S → D → R。** C01／C02 贯穿人物状态变化。已知缺口先复现一次，后续依赖项记阻塞；不要求为证明同一问题重复等一天。

下一轮建议优先完成 UI 文本快捷键／输入 owner 收尾，再完成 CIVILIAN A＋B 的正式闭环。自动治疗、装备、制作、物流、Auction-02 和战略 AI 可继续留在未来范围，等当前正常游玩链成立后再择一推进。这是建议，未启动这些新功能。

## 10 返还结果的最小格式

每项填写“编号｜通过／失败／前置不足／未验｜实际现象”。失败另附：人物名字／EntityId、Faction／Site、Tick／速度、刚点的工具或正式入口、画面及对应诊断。保存问题另保留原 JSON，不用重置覆盖唯一坏档。

示例：C18｜失败｜阿石已搬起阿青，停在恢复点外；SpotId 为 recovery_spot_1；HostPath 为 PermanentFailure；没有正式送达记录。

本轮产出仅为验收表与建议。未启动 Unity／PlayMode／Runner，未执行或新增自动测试，未修改运行代码／Content／配置，未暂存、提交或推送。既有 UI、CIVILIAN 与 DELAYED 状态保持 Implementation Complete / Producer Acceptance Pending。

