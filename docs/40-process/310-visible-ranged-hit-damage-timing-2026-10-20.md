# 可见远程弹体命中与伤害同步

> 状态：Design Confirmed / Implemented / Producer Acceptance Pending / Committed·Sealed（2026-10-20）

制作人指出主管、势力旗和议政厅的远程弹道尚未碰到人物就扣血。依据 [ADR-0047](43-decisions/ADR-0047-visible-ranged-impact-damage-timing.md)，本轮统一修改现有可见普通远程攻击：先发射，弹体到达当前目标位置时再请求 Core 结算；无有效命中则不扣血。近战和主动斗技不在本轮。

`HostMeleeStrikeVfx` 承载一次性命中回调，弹体以有限速度追向目标当前表现位置；进入目标附近才请求结算并播放爆闪，超过飞行寿命则消散且不造成伤害。`HostCharacterEncounter` 的人物远程、议政厅／势力旗以及 `HostNpcMeleeAssault` 的本地远程路径都改为回调结算。建筑的选目标／冷却与命中伤害拆成两个 Core 入口；伤害仍在 Core，Host 无权直接修改 HP。

飞行中的弹体不存档，读取后取消未命中弹体。暂停冻结弹道和命中。目标失效、战斗／世界更换及 SiteCore 破防取消结算。`XianXia.Core` 与 `XianXia.Unity` 离线基础编译通过；制作人现场验收待反馈。本轮未启动 Unity，未编写或运行自动测试。
