# 413 — 抱持显示坐标统一与俘虏救护隔离

状态：Design Confirmed / Implemented / Producer Acceptance Pending。

制作人现场报错encounter:52／阿石：ViewPosition与ExpectedPosition恰差(-0.28,-0.16)，ReadyToEnd视图门禁失败；同时明确己方俘虏不得被敌方凡人经普通救护抢走、放床。属于412／ADR-0069抱持权威的直接接线返修，不新增夺俘系统。

## 证据与单轮实施指令

1. AlignIndependentEncounterViews仍使用旧押送固定(-0.28,-0.16)偏移；ValidateIndependentEncounterViews始终将View与Tactical脚底坐标比较；HostNpcScheduleMover又使用CarryFacing×0.38。三处必须统一：Core Tactical／WorldPresence／EntityLocation override保持搬运者脚底，只有View表现加同一朝向偏移，合法抱持目标按该派生位置校验，不放宽普通人物错位检查。
2. CaptureIndependentField与单人物capture入口不得把被抱人物的视觉偏移回灌Tactical；只采集搬运者位置，再按唯一关系同步被抱者。校正、读档重建、LateUpdate和健康检查读同一搬运关系及显示公式。
3. 普通自动救护排除Captured、CapturedEscorted、Detained及当前独立战场所有权中的人物；原所属势力不是抢夺拘押的权限。候选、已存在任务的重验、真实拾起、放床提交均不得绕过。正常自由同势力伤员救护保持原规则，合法监管／探视治疗与正式牢床交接不受此禁令误伤。
4. 建立俘虏关系时清理以此人为目标的旧救护任务；既有会话推进／加载校验也清理已失去目标权限的救护记录。清理只撤该救护任务和仍由它使用的床预约，不移动俘虏、不解除真实押送／牢床、不覆盖战术位置。
5. 不改半血苏醒、稳定Snapshot版本或枚举，不新增抢夺战斗或自动救人。保留现有脏工作区、地图备份、Freeze／ProjectSettings／Packages。仅离线四程序集编译和最少静态检查，不启动Unity、不写／运行自动测试，不提交推送。

## 实施与检查

- HostCharacterVisualContext提供唯一抱持显示公式CarryFacing×0.38和当前实际搬运者查询；HostNpcScheduleMover、独立场Align及Validate统一读取。旧ReconcileOutdoorEntityMaterialization的(-0.28,-0.16)写入也移除；EntityLocation override始终是脚底，不再混入视觉偏移。普通非搬运人物仍严格检查0.02容差，不删除健康检查、不隐藏错误。
- 独立场capture先处理未被搬运的参与者，再按Core押送关系同步；单人物capture遇到被抱者也只复制实际抱持者Tactical，不采集被抱View偏移，避免累积漂移。碰撞忽略与搬人动画同时排除已失去救护权限的旧Rescue关系。
- CanReceiveAutomaticRescue作为Core公共门禁，被Captured／押送／Detained或独立战场所有权占用的人不能成为普通救护目标；CanRescueCommunity和真实床位提交都复验。普通救护不再以原Faction相同为理由夺取俘虏，正式Prison床交接仍走原授权校验。
- CaptureDefeatedInBattle建立关系后、日常Tick及加载运输校验前撤失效救护任务；只清该任务对应的移动意图。DropRescue失权时不再把人写到旧救援者位置，也不全局解除其床位，只清原医疗／住宅床的空占用旧预约。实际押送／拘押不受取消任务影响。不能修复任意已损坏存档；已有存档仍须通过原物理一致性校验。

Core／Data／Unity／Unity.Editor离线基础编译全部OK，git diff --check通过。未启动Unity、未写／运行自动测试，未提交推送；内容0.0.46／Snapshot v21保持不变。现场操作与存读档尚待制作人人工验收，不把基础编译当运行验收。

人工验收：抱起阿石后在Active／ReadyToEnd移动、转向、停下，控制台不再报该合法抱持偏移的Aligned=False，战场持续推进；结束／存读档不累计漂移，俘虏跟随真实抱持者。敌方凡人不得新接或继续旧救护来抢走俘虏、不得搬去医疗／住宅床；取消旧任务不使俘虏瞬移。正常未被拘押伤员仍可救护，己方押送到己方牢床仍可安置。
