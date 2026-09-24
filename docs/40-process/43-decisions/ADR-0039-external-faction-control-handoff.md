# ADR-0039：PlayerParty 外部势力控制转移

> 状态：已采纳；**Producer Accepted / Sealed**
> 日期：2026-09-24
> 关联：[2K](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[27](../../20-systems/27-characters-and-population.md)、[34](../../30-tech/34-entity-and-component-model.md)、[ADR-0034](ADR-0034-conflict-control-succession-and-airship-role.md)

## 背景

ADR-0034 只允许 PlayerParty 全员真正死亡后由玩家势力外部人物继承。实际验收确认：当前 Party 全员弥留、已经没有任何可控制成员时，如果远处仍有合法玩家势力人物，继续锁在 `TemporarilyUnavailable` 会阻止玩家回来营救原队伍。

## 决策

1. `External Faction Control Handoff` 是统一外部控制转移概念，复用一套候选过滤、`CombatPowerCalculator` 排序、稳定 EntityId tie-break、精确位置捕获与 Surface re-anchor。
2. `Emergency Takeover`：当前 Party 无任何 `CanActAsActive` 成员，但至少一名成员仍 Alive／Incapacitated。存在外部候选时，在战斗正式提交与 participant authority 清理后自动转移控制。
3. `Succession`：当前 Party 全员 Dead／Removed。继续沿用真正死亡继承语义。
4. 外部候选必须是实际 Character，属于玩家势力、不属于旧 Party、Alive、可担任 Active、不在未结束 Encounter，并由 `CharacterWorldPresenceQuery` 解析出 finite 且合法的 `SurfaceId + exact WorldPosition`。允许 AtWorldPosition、带 exact anchor 的 AtWorldSite 与当前 SquadWorldMotion；禁止 Site center、arrival point、旧 Hex、旧 battle location 或旧 Party location 猜测。两种模式只按 `CombatPowerCalculator.ForEntity` 最高者选择，同值按稳定 EntityId，不使用距离、名称、字典顺序或随机。
5. Emergency Takeover 不把生者当尸体退休。旧 Party 的 Alive／Incapacitated 成员保留生命、伤势、势力、精确位置，组成 `CommandKind=None` 的玩家势力 Recovery Squad；Dead／Removed 成员继续进入现有 singleton/corpse retirement。新 `squad:player` 只包含接管者。
6. Recovery Squad 不自动跟随、传送、治疗或抢回控制。成员恢复后仍是普通玩家势力人物，只能经正常 Party 管理重新加入。
7. 没有外部候选时不改 membership：全员弥留保持原 Party 与 `TemporarilyUnavailable`，恢复后可由原 Party 自动重获 Active；全员死亡保持 `AllMembersDead / IsAwaitingSuccession`。
8. Separate Space 只释放当前 PlayerParty 的 presentation authority，不执行正常 Leave，不移动或治疗旧成员，不改变其持久 EntityLocation。两种 handoff 共用同一释放入口。
9. Emergency Takeover 发布独立 `PlayerEmergencyControlTransferred`；`PlayerSuccessionResolved` 只用于真正死亡继承。
10. 现有 Snapshot v8 已能持久化 controlled squad、普通 squad、成员、精确位置和 motion，本决策不增加 schema 字段。
11. CharacterEncounter 的 `Committed` 是 report-only 状态，不拥有普通 Continuous Surface。独立战场释放后若存在 External Handoff，Host 跳过旧 Party Surface 重建，直接同步物化接管者目的地；Camera／Selection 必须晚于目标 Surface、邻域 reconcile 与接管者 EntityView 就绪。
12. 接管者的位置必须在 membership mutation 之前捕获。若来自 NPC Squad，只拆出本人；其余 roster、位置和 SquadWorldMotion 保留，必要时按稳定成员顺序补 Leader，并解除接管者的 BackgroundTravel／来源执行 authority，避免双 membership 或双 motion。
13. External Handoff 继续使用唯一 player-centered Continuous Surface streaming。跨 Surface 或同 Surface但超出旧 loaded 5×5 时 hard re-anchor；接管点已在当前邻域内时允许复用。成功返回给 Camera／Selection 前，Surface、ActiveSurfaceId、successor chunk、普通人口 reconcile 与 successor EntityView 必须全部就绪。
14. `Preparing / Active / ReadyToEnd` 才持有 Encounter spatial authority；`Committed` 只保留 Battle Report。`CloseReport` 结束 report lifecycle，不是 ordinary world reload trigger。

## 替代关系

**Superseded on 2026-09-24：** 本 ADR 定向替代 ADR-0034 Decision #6 及其“只有 PlayerParty 全员真正死亡才允许切到势力其它人物；全员弥留保持 TemporarilyUnavailable”的旧产品规则。现行触发边界是“No controllable current Party member”：仍有生者走 Emergency Takeover，全员 Dead／Removed 走 Succession。ADR-0034 的队内固定顺序接替、真正死亡继承候选规则、精确位置接管、空势力终局延期等未冲突部分继续有效。

## 边界

不增加 NPC 自动救援、自动寻路、恢复后自动回队、GameOver、继承者选择 UI、医疗重构或新的队伍管理 UI。SUCCESSION-01、CONTROL-HANDOFF-01 与 P1 已于 2026-09-24 完成制作人人工验收并封板。
