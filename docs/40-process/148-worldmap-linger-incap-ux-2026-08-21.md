# 148 · 大地图弥留交互与点击修补（2026-08-21）

> 状态：**已落地（手操跳过，待补验）**｜日期：2026-08-21  
> 相对提交：`97e3ba7` → **本篇对应提交**  
> 上级：[147 接战点／弥留残留](147-battlefield-linger-no-teleport-2026-08-21.md)／[139 大地图 RTS](139-world-map-rts-orders-2026-08-17.md)  
> 下级：[149 残留战场批 2](149-lingering-battlefield-batch2-2026-08-21.md)  
> 游玩入口：`Assets/Scenes/LevelTester.unity`  
> 飞书：https://my.feishu.cn/docx/J8FsdDl4ooiTE0xd6sZcpCdDnef

---

## 1. 一句话

在 [147](147-battlefield-linger-no-teleport-2026-08-21.md) 残留战场基础上，修补 **大地图弥留头像交互**、**敌军点击／接战流程**（远处可下令、到站再弹窗），并删除已废弃的 **战中 JoinOngoing 增援**。

---

## 2. 背景

147 落地后手操反馈：弥留再入、接战点点击优先级、右键分工等。本轮先完成 **批 1 + 战略层接战一致性**，手操清单暂跳过，由 [149](149-lingering-battlefield-batch2-2026-08-21.md) 接续探望到站与 Core 下沉。

---

## 3. 产品规则

### 3.1 我方弥留头像

| 操作 | 条件 | 行为 |
|------|------|------|
| **左键** | 任意 | 不加入 `_selected`；弥留／尸体不可选 |
| **右键** | 未选可下令活人 | 「**进入残留战场**」（支援范围内可进；无活人可 solo 弥留再入） |
| **右键** | 已选可下令活人 | **派人探望**（移动确认） |
| **右键** | 活人已在旁且仍有残留 | 无法再移动时改开进入菜单 |

### 3.2 敌军栈（活／弥留统一）

| 规则 | 说明 |
|------|------|
| 选人 | 左键选活人；无选人不弹攻击菜单 |
| 远处攻击 | **可出菜单、可下令**；**不立刻弹接战** |
| 到站 | 宏观追击／移动 → **与栈重合** 后 `AfterTravelTick` 弹接战 |
| 弥留敌军 | 同一套 Offer（手动／自动）；处决勾选可彻底击杀弥留 |

### 3.3 进入残留战场（我方专用入口）

- 支援半径内活人优先组队；无活人则弥留 solo。
- `TryResolveBattleAnchor`：Participants 缺失时 fallback 弥留 presence。
- 仍要求 `BattlefieldLingering == true`。
- **不经接战 Offer**（批 3 再对齐）。

### 3.4 接战排队

- 上一场 Offer／Modal 占用时，新攻击 **入队**。
- **出队 promote** 时：人 **未到栈旁** → 只 **追击**，不弹 Offer；到了才 `ActivateOffer`。

### 3.5 已删除

- **JoinOngoing 战中增援**（`JoinEngagedMembers`、加入战斗 UI）：手动战时间停止，不再半路上加人。

---

## 4. 改动文件

| 层 | 文件 |
|----|------|
| Host | `HostWorldMapPanel.cs`（批 1 交互、`ExecuteAttackStack` 只走 Pursuit） |
| Core | `BattleOfferService.cs`（排队 promote 到站检查） |
| Core | `StrategicEncounterSpawner.cs`（删 JoinEngagedMembers） |
| Host | `HostStrategicInterruptPresenter.cs`、`PlayableHostBootstrap.cs`（删 Join UI） |
| Test | `StrategicPhaseTests.cs` |

---

## 5. 手操清单（跳过待补）

1. 弥留 solo / 半径内活人 → 进入残留战场  
2. 探望移动 vs 进入菜单分工  
3. 远处攻击 → 人动 → 到站接战  
4. 接战排队：人未到不弹窗  
5. 147 回归  

**状态：** 制作人 **手操跳过（2026-08-21）**，待补验。

---

## 6. 留给 149／批 3

| 项 | 说明 |
|----|------|
| 探望到站衔接 | 149 批 2 |
| `CollectLingeringViewParty` 下沉 Core | 149 批 2 |
| 再进走 Offer | 批 3 |
| Encounter LocalMapId | 138 方案 A |

---

## 7. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-21 | 收束：排队 promote 到站检查；删 JoinOngoing；攻击只走 Pursuit；文档／飞书／提交 |
| 2026-08-21 | 批 1 + 敌军交互澄清 + `ExecuteAttackStack` 修正 |
