# 可见远程弹体命中与伤害同步

> 状态：Design Confirmed / Implemented / Producer Accepted（2026-10-20）；直线修正待提交封板

制作人指出主管、势力旗和议政厅的远程弹道尚未碰到人物就扣血。依据 [ADR-0047](43-decisions/ADR-0047-visible-ranged-impact-damage-timing.md)，统一修改现有可见普通远程攻击：先发射、命中再扣血；近战和主动斗技不在本轮。首次实现为命中再扣血而让弹体追随目标，制作人复看后明确要求改为直线弹道、不要追踪，并于 2026-10-20 确认“现在这样挺好的”。

`HostMeleeStrikeVfx` 在发射时固定方向与射程，弹体沿直线匀速飞行；飞行中进入目标命中半径（`projectileHitRadius`，默认 0.42）才通过一次性回调请求 Core 结算并播放爆闪，飞满射程后消散且不造成伤害。`HostCharacterEncounter` 的人物远程、议政厅／势力旗 SiteCore 弹道以及 `HostNpcMeleeAssault` 的本地远程路径共用该直线弹道。建筑的选目标／冷却与命中伤害拆成两个 Core 入口；伤害仍在 Core，Host 无权直接修改 HP。

飞行中的弹体不存档，读取后取消未命中弹体。暂停冻结弹道和命中。目标失效、战斗／世界更换及 SiteCore 破防取消结算。`XianXia.Unity` 离线基础编译通过；制作人已现场验收直线弹道通过。本轮未由开发侧启动 Unity 复验，未编写或运行自动测试。
