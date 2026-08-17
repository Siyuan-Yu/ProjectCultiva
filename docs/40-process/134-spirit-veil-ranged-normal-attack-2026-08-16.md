# 134 · 斗气纱衣：筑基远程普攻姿态

> 状态：竖切已落地｜日期：2026-08-16（2026-08-17 补统一远程弹道）  
> 相关：`23-combat.md` §6｜`22-realms-and-abilities.md` §6  
> 收束：[137](137-skill-mastery-farm-veil-chop-rollup-2026-08-17.md)

## 拍板

- 筑基起可召唤**斗气纱衣**（固定灵力开销，非比例）。
- 展开后普攻变为**远程灵力外放**：射程变长，**伤害／攻速与近战相同**（远程纯优，不是弓近战取舍）。
- 贴脸仍打远程。
- **灵力打空**或**本场交战结束**自动卸下；玩家可手动收起（选中筑基单位后 **F2**／底栏「纱衣」）。
- **非玩家（Npc）**：交战开始且境界≥筑基、灵力足够 → **自动召唤**；玩家不自动开。
- **表现**：所有远程攻击暂用同一套弹道特效（青色光核飞行→命中爆闪＋受击闪白）；近战仍用挥砍弧。

## 数值（第一版硬编码，`SpiritVeilRules`）

| 项 | 值 |
|----|-----|
| 最低境界 | 筑基 |
| 近战交战距离 | 1.85 |
| 筑基远程半径 | 7 |
| 筑基召唤开销 | 60 灵力（约满灵力 ~180 的 1/3） |
| 召唤门槛 | 当前灵力 **>** 开销（扣完至少留 1，避免瞬开瞬卸） |

更高境界射程／开销以后加行即可。

## 统一远程弹道（2026-08-17）

| 项 | 现行 |
|----|------|
| 入口 | `HostMeleeStrikeVfx.PlayRangedBetween`（纱衣普攻经 `HostNpcMeleeAssault`） |
| 飞行 | 程序化软圆光核，沿攻→守插值；速度约 16 单位／秒，时长钳制 0.12～0.48s |
| 命中 | 抵达后小爆闪；再对守方 `PlayHitFlash`（不再开打瞬间闪白） |
| 贴图 | `HostSpriteFactory.RangedProjectileSprite`（可 Resources 换皮） |
| 范围 | **暂作全远程共用**；按境界／斗技换皮以后再拆 |

## 代码

| 层 | 内容 |
|----|------|
| Core | `SpiritVeilComponent`／`SpiritVeilRules`／`SpiritVeilService` |
| Host | `HostSpiritVeilController`；交战 `Begin` 对 Npc 自动开纱衣；`HostNpcMeleeAssault` 按姿态取射程；**远程统一弹道** `HostMeleeStrikeVfx.PlayRangedBetween` |
| 贴图 | `HostSpriteFactory.RangedProjectileSprite` |
| 内容 | 杂役主管：`initialRealm`＝筑基，`SpiritPower`＝180 |
| 测试 | `SpiritVeilTests` |

## 未做

- 按境界／斗技换弹道皮（当前全员共用青色光核弹道）
- 纱衣独立护甲层（仍用现有灵力护盾池）
- 元婴等更高境界半径表
