# 148 · 大地图弥留交互与点击修补（2026-08-21）

> 状态：**已落地（代码待手操验）**｜日期：2026-08-21  
> 相对提交：`88fb4b5` → **（本篇对应提交）**  
> 上级：[147 接战点／弥留残留](147-battlefield-linger-no-teleport-2026-08-21.md)／[139 大地图 RTS](139-world-map-rts-orders-2026-08-17.md)  
> 游玩入口：`Assets/Scenes/LevelTester.unity`  
> 飞书：https://my.feishu.cn/docx/J8FsdDl4ooiTE0xd6sZcpCdDnef

---

## 1. 一句话

在 [147](147-battlefield-linger-no-teleport-2026-08-21.md) 残留战场基础上，修补 **大地图弥留头像交互**（左键／右键分工、派人探望、查看再入）与 **接战点拥挤时的点击优先级／敌军吸附**，避免误触攻击、无法派人、无法查看。

---

## 2. 背景

147 落地后手操反馈：

- 我方弥留后，**无法**在不先选中附近活人的情况下「查看再入」；
- 接战点敌军栈 **吸附过大**，右键派人／点弥留常被误判为攻击；
- 右键敌军不出攻击菜单（移动逻辑抢先）；
- 弥留头像 **左键／右键表现相同**（都被当普通选中）；
- 已选活人时，右键弥留应 **派人探望**，而非一律弹查看菜单。

本轮 **仅改 Host 大地图交互**（`HostWorldMapPanel.cs`），Core 战略规则未动。

---

## 3. 产品规则（本轮实现）

### 3.1 我方弥留头像

| 操作 | 条件 | 行为 |
|------|------|------|
| **左键** | 任意 | 不加入 `_selected`；底栏提示「右键查看／先选活人再右键派人」 |
| **右键** | 未选可下令活人 | 打开 **查看菜单**（「查看（再入战场）」） |
| **右键** | 已选可下令活人 | **派人探望**：`WorldTravelConfirm` 移动到弥留者所在节点／路段／接战锚点 |
| **右键** | 活人已在弥留者旁且仍有残留战场 | 无法再移动时，改开 **查看菜单** |

弥留我方头像：**红 tint**（与敌兵「弥」字头像区分）。

### 3.2 敌军栈点击

| 操作 | 优先级 | 吸附 |
|------|--------|------|
| **左键** | 我方头像 → 敌军 → 节点 | 圆形半径 **+16px**；与头像重叠时 **+8px** |
| **右键** | 我方头像 → 敌军 → 节点／道路移动 | 同上（左／右统一半径，避免「左键好中右键难点」） |

### 3.3 查看再入（`EnterLingeringBattlefield`）

- `CollectLingeringViewParty`：优先 **接战锚点半径内活人**；若无活人则允许 **锚点上弥留者单独再入**（不要求另选活人）。
- 仍要求 `BattlefieldLingering == true` 且 `Participants` 有 BattleAnchor。

### 3.4 Debug（暂保留）

- 支援半径 **绿圈** + 底栏滑块：Debug 用，正式版再关（见会话约定）。

---

## 4. 实现要点

| 项 | 说明 |
|----|------|
| `CollectLingeringViewParty` | 残留战场再入队伍收集（锚点 + 支援半径） |
| `TryDispatchSelectedLivingToIncap` | 已选活人 → 探望移动确认 |
| `TryBuildTravelTargetForIncap` | 由弥留 presence／BattleAnchor 解析 `WorldTravelTarget` |
| `CollectSelectedLivingOrderableParty` | 选中集过滤弥留／尸体，仅活人可下令 |
| `TryHitArmyStack` | 圆形吸附 + `ResolveArmyStackHitPad`（拥挤时缩小） |
| `OpenIncapAvatarMenu` | 统一打开弥留查看菜单 |

**改动文件：** `Assets/Scripts/Unity/Host/HostWorldMapPanel.cs`（约 +400 行净增，相对 `eece220`）。

**编译修复：** 左键头像分支重复声明 `id`（CS0128）已删除。

---

## 5. 手操清单（待验）

1. 战后我方弥留 → **不选人** → 右键红头像 → 查看菜单 →「查看（再入战场）」进图  
2. **不选人** → 单人全队弥留 → 仍可通过查看菜单再入（无活人接应）  
3. 左键选 1～2 活人 → 右键弥留 → **移动确认**（探望），非查看菜单  
4. 活人走到接战点旁 → 再右键弥留 → 查看菜单可再入  
5. 接战点：左键选我方 → 右键敌军 → **攻击菜单**（非移动）  
6. 接战点：右键节点空白 → **移动确认**（非误触敌军）  
7. 147 原清单 1～7 回归（无瞬移、残留再攻、支援滑块等）  

**状态：** 制作人 **尚未签收**。

---

## 6. 已知缺口（未在本轮修）

| 缺口 | 说明 |
|------|------|
| 再入 bypass Offer | `EnterLingeringBattlefield` 不经接战弹窗，与「和进战斗一样的前置交互」仍有差距 |
| 探望到站 | 到站后无自动衔接「查看战场」；需再右键弥留或手动 |
| `CollectLingeringViewParty` 在 UI 层 | 应下沉 Core，与 `EnterLingeringBattlefield` 共用校验 |
| Encounter 图 | 仍多为 `base:map_world_node_stub`（138 方案 A 未做） |
| 队伍收集 API | `CollectOrderableParty`／`FilterOrderableParty`／Living 版三处并存 |
| `HostWorldMapPanel` 体量 | ~1800 行，宜拆 hit-test／menu／linger presenter |

---

## 7. 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-21 | 初版：弥留左／右键分工、探望移动、点击优先级、再入队伍收集、CS0128 |
