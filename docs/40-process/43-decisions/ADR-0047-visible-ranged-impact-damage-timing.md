# ADR-0047 — 可见远程弹体命中时结算伤害

> 状态：Design Confirmed（制作人 2026-10-20 明确提出“实际子弹命中才扣血”）；Implemented，Producer Acceptance Pending；Committed·Sealed（2026-10-20）。

## 问题

此前 [302](../302-unified-visible-ranged-projectile-presentation-2026-10-19.md) 的人物远程普攻和 [ADR-0045](ADR-0045-council-hall-encounter-defense-authority.md)／[ADR-0046](ADR-0046-faction-flag-encounter-defense.md) 的 SiteCore 反击都在出手时立即结算 Core 伤害，Host 之后播放固定终点弹道。目标移动时会看到弹丸落在旧位置，人物 HP 却已降低。

## 决定

- 当前有可见弹体的普通远程攻击改为“合法发射 → 弹体跟随当前目标表现位置 → 可见抵达 → 领域服务结算”。发射只占用既有冷却；命中前不扣 HP、不触发弥留／击杀及伤害事件。近战和主动斗技保持原规则。
- Host 只报告弹体是否实际抵达，不自行计算伤害。人物仍由 `MeleeCombatService.ApplyStrike` 验证权限、计算攻防并进入生命状态；议政厅和势力旗仍由 `SiteCoreEncounterDefenseService` 验证 Site、Owner、战争关系、目标与本场权限并计算伤害。目标消失、战斗结束、世界／遭遇更换、建筑破防、关系改变或目标已失能时，未命中的弹体不结算伤害。
- 弹体是短暂的战场表现状态，不加入持久 Snapshot。若飞行途中存档再读取，尚未命中的弹体消失，旧伤害不得在读取后凭空兑现；已占用的攻击冷却按现有战斗状态继续。暂停时弹体与命中均暂停。
- 不新增 Character 身份、建筑武器系统、随机命中率、弹体碰撞穿透／掩体规则、WorldTick 或 Snapshot 稳定数值。此决定仅改变现有可见普通远程攻击的伤害时点；既有出手合法性与命中攻防数值不变。

## 验收边界

在独立遭遇和本地 NPC 对打中分别观察主管的纱衣远程攻击；议政厅、势力旗在独立遭遇中各观察一次。每次出手到弹丸触及人物期间，HP 不提前变化；触及时同帧出现受击反馈与 HP 变化。目标移动时弹丸追踪当前位置；目标在抵达前失能、战斗结束或设施被破防时不出现延迟扣血。制作人现场验收前仅记 Implemented。
