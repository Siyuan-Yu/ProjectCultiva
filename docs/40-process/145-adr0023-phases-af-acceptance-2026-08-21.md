# 145 · ADR-0023 Phase A～F 实施验收（2026-08-21）

> 状态：**Phase A～F 代码落地；Host UX 打磨见 [146](146-adr0023-host-ux-polish-2026-08-21.md)；EditMode 已补；手操清单如下**｜日期：2026-08-21  
> 决策：[ADR-0023](43-decisions/ADR-0023-manual-encounter-freezes-worldtick.md)｜分期 [144](144-battle-worldtick-freeze-impact-and-phases-2026-08-21.md)｜打磨 [146](146-adr0023-host-ux-polish-2026-08-21.md)  
> 试玩：**LevelTester**（共用 `PlayableHostBootstrap`）

> 飞书：https://my.feishu.cn/docx/MlvqdC4SGoz0YnxJx9tc3SYNnje

---

## 1. 最终实现状态

| Phase | 状态 | 要点 |
|-------|------|------|
| A | **完成** | ClockFreeze；Tick 禁推；Modal 禁战略令／禁切图；PostBattle |
| B | **完成** | `BattleParticipantSnapshot`／`PreBattleWorldPresence`／支援＝**世界坐标半径**（默认 1.0） |
| C | **完成** | Offer UI：强制／可选勾选／敌援列表；Auto／Manual／撤退 |
| D | **完成** | FieldCleared→PostBattle；手动＝非强制「结束战斗」；可选支援还原 PreBattle |
| E | **完成** | `BattleInterruptQueue` 确定性串行；同栈去重；Finish 先解冻再出队 |
| F | **完成（自动化）** | `Adr0023BattlePhasesTests`＋修订旧 JoinOngoing 期望；UX 见 [146](146-adr0023-host-ux-polish-2026-08-21.md) |

**无阻塞级设计冲突。** Snapshot 仍不持久化 Strategic 全板（既有债）；freeze 运行时有效，存档中途战斗未做。

---

## 2. 核心文件

**新增**

- `StrategicClockFreeze.cs`
- `BattleParticipantSnapshot.cs`（含世界坐标 Range／`IsAutoSettlement`）
- `BattleInterruptQueue.cs`（含 SnapshotBuilder）
- `StrategicEncounterResolveService.cs`
- `Adr0023BattlePhasesTests.cs`

**修改**

- `BattleOfferService`／`StrategicBoard`／`SimulationLoop`／`WorldTravel*`／`StrategicPursuitService`／`StrategicEncounterSpawner`／`StrategicBootstrap`
- `HostStrategicInterruptPresenter`／`PlayableHostBootstrap`／`HostWorldMapPanel`／`HostWorldTravelDeparture`
- `StrategicPhaseTests`（JoinOngoing→Queue；Auto 需确认结算）

---

## 3. Host 手操验收（请你跑 · LevelTester）

1. 单人攻击→Offer→Tick 停→手动战→清场→右下角「结束战斗」（非强制，可继续补刀）→点结束→时间恢复  
2. 同节点近距：一人强制、一人勾选支援→结束→支援者仍在原位置（不瞬移）；邻村不可勾选  
3. 同势力近距第二敌军栈应出现在 Offer「敌援」  
4. 两场接战串行：第一场 Auto→确认结算→第二场 Offer；全确认后才走 Tick  
5. 手动战中：进其他节点场景／远方下令应失败  
6. AutoResolve→**结算弹窗**→确认；不推进 Tick  

细节与山匪可见性见 [146](146-adr0023-host-ux-polish-2026-08-21.md)。

---

## 4. 技术债（不阻塞）

- Strategic／Encounter／Queue **未写入** Snapshot schema  
- 遭遇图仍与 stub 共用 mapId  
- 战中 JoinOngoing UI 残留（默认改排队）  
- 搜刮／俘虏 PostBattle 仅预留文案槽  
- Friendly AI 支援未做  

---

## 5. 明确未实现

攻城、战场动态援军、飞行支援、ArmyGroup 实体化、复杂 NPC 支援 AI、伏击、阵型、战后追击。

---

## 6. 建议下一阶段

手操签收本清单＋[146](146-adr0023-host-ux-polish-2026-08-21.md) → 若过：可开占点／外交或 Snapshot 纳入 Strategic 冻结态。
