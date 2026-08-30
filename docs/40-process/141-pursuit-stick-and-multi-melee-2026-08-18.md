# 141 · 追击贴敌＋LocalMap 多选近战（2026-08-18）

> 状态：**v2 移动目标追击已实现（待测）**｜日期：2026-08-18；**backlog / Vision：** [154](154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md) §3、§3.4  
> 相对提交：`c3a036e` 之后 → 本轮 `main`  
> 上级：[140 收束](140-world-map-rts-battle-return-rollup-2026-08-18.md)｜[139 RTS 规则](139-world-map-rts-orders-2026-08-17.md)  
> 飞书：https://my.feishu.cn/docx/I36jdoafvos0YCx2SFscxl0anUb

---

## 1. 一句话

大地图「攻击」改为持续贴敌军栈，追上再弹接战；LocalMap 多选己方攻击同一敌人时全员一起打。

---

## 2. 交付对照

| 主题 | 做什么 | 入口／文件 |
|------|--------|------------|
| **追击贴敌** | 每 tick 把未重合的追击者改道到敌军栈当前宏观位置；重合再 BattleOffer | `StrategicPursuitService.SyncPursuersToStack`／`AfterTravelTick` |
| **道路行军也可追** | `StartTravelToStackAnchor` 按栈显示进度追，不只追 Dest 节点 | `WorldTravelService.StartTravelToStackAnchor` |
| **多选近战** | 多名己方可同时打同一目标；右键攻击对当前选中全员下指令 | `HostNpcMeleeAssault`／`HostNpcContextMenu.BeginAttack` |

---

## 3. 操作流

```text
【大地图攻击】选人 → 右键敌军栈 → 攻击 → 人上路；敌军挪位则改道贴上去 → 重合弹接战窗
【LocalMap 群殴】框选／多选己方 → 右键敌人 → 攻击 → 全员走近并一起打（交战条「某某 等N人」）
```

---

## 4. 规则

| 规则 | 现行 |
|------|------|
| 攻击／追击 | 下令当下不开战；贴上敌军栈才弹 BattleOffer |
| 敌军移动 | 追击名单每 tick 续追当前位置，不停在下令时的旧锚点 |
| LocalMap 攻击 | 多选＝多人同时攻同一目标；一人 Stop／移动只让该人脱离 |

### 4.1 Pursuit 与 Strategic Vision（当前 vs 未来）

| 阶段 | 规则 |
|------|------|
| **当前（无 Vision / Fog）** | `PursuitOrder` 持有 `TargetArmyId`；允许 **临时全知** 读取 Target FormalArmy 实时战略位置（开发兼容，非最终产品） |
| **未来（有 Vision / Fog）** | Target 在 Pursuer 势力 **有效战略视野内** → 续追；**离开视野** → **自动取消 Pursuit**（第一版不做 Last Known Position 续追） |

详见 [154 §3.4 Future Strategic Vision Integration](154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md)。

**禁止（未来）：** 目标已不可见仍经 `FormalArmyBoard` 全知改路 — 绕过 Fog of War。

**明确不是：** 另做一套「只跟不打」的跟随菜单（代码里仍有 `StrategicFollowService`，大地图无入口）。

---

## 5. 测试

- `Pursuit_RetargetsWhenStackMovesAlongRoute_ThenOffersBattle`：挪位后再贴上弹窗
- 既有：`PursuitTravel_StopsAtRouteAnchoredStack_NotDestination`

---

## 6. 已知问题（2026-08-23）

| 现象 | 说明 |
|------|------|
| **v2 移动目标追击** | 已实现（`ArmyPursuitTargetService` + PUR-01～11）；EditMode / Host **待签收** — 见 [154 §3](154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md) |
| **失去视野停止追击** | **延期** — 需 Strategic Vision / Fog of War；见 [154 §3.4](154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md) |

~~**决策：** 暂缓修复移动敌追击~~ → v2 已编码；Vision 约束未做。

---

## 7. 下一步

1. ~~手操验：攻击林间山匪、敌军沿路移动时人应跟着贴上去再出弹窗~~ → **v2 待签收（154 §3.3）**
2. 手操验：LocalMap 两人打同一敌人应两人都出手  
3. 跟随菜单／交谈仍暂缓  
4. ~~恢复追击专项~~ → v2 已交付；**Vision 约束** 等 Fog 系统 — [154 §3.4](154-formal-army-rts-rollup-and-pursuit-backlog-2026-08-23.md)
