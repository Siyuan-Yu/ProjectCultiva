# 142 · 自动战结算＋弥留／尸体（2026-08-20）

> 状态：**已落地（未手操验收）**｜日期：2026-08-20  
> 相对提交：`07428d9` 之后 → 本轮待推送 `main`  
> 上级：[141 追击贴敌](141-pursuit-stick-and-multi-melee-2026-08-18.md)｜[140 收束](140-world-map-rts-battle-return-rollup-2026-08-18.md)  
> 策划对齐：`docs/20-systems/23-combat.md` §10 重伤／击杀  
> 飞书：https://my.feishu.cn/docx/B56IddrTtocNZIxVgi7c4O1Knzg

---

## 1. 一句话

大地图**自动战斗**现在有双方战力损耗结算；LocalMap 与自动战统一 **0 血→弥留、补刀→死亡+尸体**；接战弹窗可勾选**战胜时直接击杀**；选中弥留／尸体时底栏状态 UI 左上角显示对应角标。

---

## 2. 需求对照

| 主题 | 规则 | 实现 |
|------|------|------|
| **弥留之际** | HP 归零不立刻死；再受击才确认死亡 | `LifecycleState.Incapacitated` + `CombatLifeStateService` |
| **尸体** | 死亡留场；约 2 游戏日后消失（普通人；炼气 3 日、筑基 5 日） | `CorpseComponent.RemoveAfterTick` + `TickCorpseDecay` |
| **LocalMap 近战** | 敌我双方均适用弥留／补刀 | `MeleeCombatService`／`HostNpcMeleeAssault` |
| **自动战结算** | 按战力比结算双方伤亡，toast 可读摘要 | `AutoBattleCasualtyService` + `BattleOfferService.ResolveAuto` |
| **自动战败** | 敌方有概率直接击杀我方，或只打到弥留 | `ApplyPlayerDefeat` 概率链 |
| **自动战胜** | 勾选「战胜时直接击杀」→ 敌军栈全灭；不勾选 → 击溃（栈残存、人数／战力削减） | 接战 UI checkbox + `ApplyPlayerVictory(executeOnWin)` |
| **状态 UI** | 左键选中弥留／尸体，底栏左上角显示「弥留」或「尸体」 | `HostFormalHud.DrawLifeStateBadge` |
| **大地图菜单** | 左键节点不再镜头跳转导致菜单错位 | `HostWorldMapPanel` 锚定节点框 + 每帧重算 |

---

## 3. 核心文件

| 层 | 文件 | 职责 |
|----|------|------|
| Core | `CombatLifeStateService.cs` | 弥留、确认死亡、尸体寿命、角标文案 |
| Core | `CorpseComponent.cs` | 尸体到期 tick |
| Core | `MeleeCombatService.cs` | 0 血→弥留；弥留受击→死亡 |
| Core | `AutoBattleCasualtyService.cs` | 自动战胜／败伤亡与摘要 |
| Core | `BattleOfferService.cs` | `ResolveAuto(..., executeOnWin, out report)` |
| Core | `SimulationLoop.cs` | 每 tick 尸体 decay |
| Core | `StrategicEncounterSpawner.cs` | 清场计数只算 `Alive`；尸体仍留 tracking |
| Host | `HostStrategicInterruptPresenter.cs` | checkbox + 战报 toast |
| Host | `HostNpcMeleeAssault.cs` | 弥留停战、补刀、场景标签 |
| Host | `HostFormalHud.cs` | 底栏左上角角标 |
| Host | `HostCharacterSheetPanel.cs` | 人物详情标题角标 |
| Host | `EntityView`／`EntityViewSpawner` | 弥留／尸体色调与标签 |
| Host | `HostWorldMapPanel.cs` | 节点菜单锚定修复 |
| Test | `CombatLifeStateTests.cs` | 弥留／补刀／decay |
| Test | `StrategicPhaseTests.cs` | 自动战胜败／处决／击溃 |

---

## 4. 自动战流程

```text
接战弹窗 → （可选）勾选「战胜时直接击杀」→ 自动战斗
  ├─ 胜：ApplyPlayerVictory
  │     ├─ 勾选：移除敌军栈（全灭）
  │     └─ 未勾选：MemberCount／CombatPower 削减，栈保留
  │     └─ 我方可能轻负伤（chip damage）
  └─ 败：ApplyPlayerDefeat
        └─ 每名接战者：概率 直接击杀 / 弥留 / 负伤
→ toast 显示 AutoBattleReport.Summary
```

**胜率**仍用 `CombatPowerCalculator.EstimateAutoWinPercent`；伤亡在胜／败分支独立_roll。

---

## 5. LocalMap 近战流程

```text
普攻 → HP=0 → Incapacitated（弥留）→ 停战，标签「弥留」
再次攻击弥留目标 → Dead + CorpseComponent → 标签「尸体」
SimulationLoop 每 tick → RemoveAfterTick 到期 → Removed（表现隐藏）
```

遭遇战清场：`CountLivingTracked` 只计 `Alive`；弥留／尸体不挡 `FieldCleared`。

---

## 6. UI

| 位置 | 表现 |
|------|------|
| 场景单位 | EntityView 标签「弥留」／「尸体」+ 色调 |
| 底栏状态板 | 左上角角标 + 标题活动「弥留之际」／「尸体」 |
| 人物详情 | 标题左角标（与底栏一致） |
| 接战弹窗 | 「战胜时直接击杀（不勾选则仅击溃敌军）」 |

---

## 7. 测试（EditMode，未手操）

- `CombatLifeStateTests`：ZeroHp→Incap；补刀→Corpse；decay→Removed
- `StrategicPhaseTests`：AutoBattle_Defeat_*；ExecuteOnWin_RemovesStack；SpareOnWin_KeepsReducedStack

**手操待验：** 自动战 toast 摘要、弥留补刀、尸体 2 日消失、checkbox 处决 vs 击溃、大地图节点菜单不抽。

---

## 8. 明确未做

- 弥留救治／复活
- 阵亡者从编队 UI 自动剔除
- 自动战与 LocalMap 手动战的数值完全统一（自动战仍为抽象栈／接战名单）
- 「不攻击重伤敌人」玩家设置项（策划 §10.1；代码侧尚未做开关）

---

## 9. 下一步

1. 手操验收 §7 清单  
2. 若通过：占点 Phase 1／外交 UI 另开  
3. 可选：编队过滤 Dead／Incap；弥留求饶交互（对齐 §10.2）
