# 141 · 追击贴敌＋LocalMap 多选近战（2026-08-18）

> 状态：**已落地**｜日期：2026-08-18  
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

**明确不是：** 另做一套「只跟不打」的跟随菜单（代码里仍有 `StrategicFollowService`，大地图无入口）。

---

## 5. 测试

- `Pursuit_RetargetsWhenStackMovesAlongRoute_ThenOffersBattle`：挪位后再贴上弹窗
- 既有：`PursuitTravel_StopsAtRouteAnchoredStack_NotDestination`

---

## 6. 下一步

1. 手操验：攻击林间山匪、敌军沿路移动时人应跟着贴上去再出弹窗  
2. 手操验：LocalMap 两人打同一敌人应两人都出手  
3. 跟随菜单／交谈仍暂缓
