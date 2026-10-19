# 统一可见远程弹道表现接线

> 状态：Design Confirmed / Implemented / Producer Acceptance Pending / Committed·Sealed｜2026-10-19

## 制作人需求与现状

制作人希望现有修士远程攻击与日后议政厅战斗内远程攻击共用一种能看见飞行过程、速度适中的仙术弹道。现行 [23 §6](../20-systems/23-combat.md) 和 `SpiritVeilRules` 表明：当前**普通攻击**到筑基且展开斗气纱衣才切换为远程；炼气的“基础灵力攻击”是设计方向，不等于当前已有可用的远程普攻。主管为筑基，可按既有条件远程普攻。

`HostMeleeStrikeVfx` 已有青色光核、飞行及抵达爆闪，`HostNpcMeleeAssault` 已调用；但 `HostCharacterEncounter` 的 Active/ReadyToEnd 只播放人物出手动作，没有把成功的纱衣远程普攻接到弹道。原飞行速度 16 表现单位/秒且最多 0.48 秒，易显得瞬现。议政厅规则仍为 [301 ACR](301-acr-council-hall-spirit-defense-2026-10-18.md) Proposed，不可仅凭特效制造攻击伤害。

## 本轮实施范围

- 保留一个通用 Host 弹道接口，现有纱衣普攻从攻方出手点飞向守方，沿途持续显示，抵达后短暂爆闪。独立 `CharacterEncounter` 的 Active 与 ReadyToEnd 纱衣普攻补接同一接口；原 `HostNpcMeleeAssault` 复用。
- 放缓飞行时长并增强光核/尾迹的可读性；效果只消费正式成功的远程攻击，不新增伤害或目标判断，不给近战/当前徒手斗技错误套弹道。
- 议政厅的屋脊符阵保留，但本轮不由它发射；待 301 的建筑参战/伤害权威正式落地时，调用同一通用特效即可。
- 不改 Core／Data／Content、境界门槛、伤害、冷却、Snapshot、Freeze 或 ProjectSettings；只做 Host 表现、基础编译与最少量引用检查，不启动 Unity、不编写或运行自动测试。

## 人工验收

LevelTester 里让筑基主管展开纱衣远程攻击：在独立遭遇和已有 NPC 对打两条路径观察出手后持续飞行的青色光核及抵达爆闪，能看清弹道且不会像瞬移；暂停时弹道静止。炼气未展开纱衣的普通近战不应出现远程光弹；议政厅目前不应凭静态屋脊符阵自动开火。攻击结算仍由既有规则完成，视觉飞行不代表本轮更改命中时点。

## 实际接线

`HostCharacterEncounter` 的 Active 与 ReadyToEnd 成功纱衣普攻改调共用 `HostMeleeStrikeVfx.PlayRangedBetween`，播放出手、飞行及抵达爆闪；未开纱衣仍保留近战出手。既有 `HostNpcMeleeAssault` 本就使用这个接口，不另建特效。通用飞行速度由 16 调到 8.5 表现单位/秒，允许 0.22–1.1 秒飞行，以线性位移避免头尾停顿，并用同一程序光核叠一层短尾迹；不引入新图片资产或不同攻击类型各自的弹道资源。

`XianXia.Unity` 基础离线编译 `ALL_OK`。未启动 Unity、未写或运行自动测试；弹道实际缩放可读性、暂停与场内表现待制作人人工验收。现有 Core 在成功出手时结算伤害，弹道仅延后视觉爆闪；若将来要求“弹丸到达才扣血”，那是战斗命中时点规则变更，需另经正式设计。议政厅尚无合法建筑伤害事件，故仍不发射。
