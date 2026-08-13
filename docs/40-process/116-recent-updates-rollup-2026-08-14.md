# 116 · 近期更新收束（Ch01 手操弧／劳动／小队背包）— 2026-08-14

> 状态：**已推送**｜日期：2026-08-14  
> 相对提交：`ce6a371`（115 文档收束之后）→ 本轮 `main`  
> 相关：[115 上一轮](115-recent-updates-rollup-2026-08-13.md)｜[110 QuestEditor](110-content-studio-quest-editor-usage.md)｜[SCHEMA](../../Content/BaseGame/Data/SCHEMA.md)

---

## 1. 一句话

把 **第一章参考关收成可手操的三环任务链**（手动接取、劳动计次、物资读小队背包、集合点到位），并补上 **共用 50 格背包**、**任务日志／待领奖**、**约 10 秒采 1／自动续采**。

---

## 2. 交付对照（ce6a371 之后）

| 主题 | 做什么 | 入口 |
|------|--------|------|
| **Ch01 内容裁剪** | 仅保留三任务；关闭 `autoOffer`；清章节／地点自动挂接；事件只留 opening＋miner 等必要项 | `Quests/ch01_reference_quests.json`、`Events/`、`Chapters/`、`Regions/` |
| **任务 1：三人各采** | `uniqueHarvestAtLocation` @ 农田，`amount: 3`（不同角色各采到 ≥1） | `LocationLaborProgressBoard`、Quest 条件 |
| **任务 2：物资凑齐** | 粗粮／灵药／粗木各 `stockAtLeast` ≥3；**读小队背包**（非聚落仓库） | `ContentConditionEvaluator`、`PartyInventory` |
| **任务 3：三人集合** | 主角／甲／乙 `characterAtLocation` @ `base:loc_ref_party_rally` | 地图 `rallyPoint`＋地点 `boundLocationId` |
| **劳动／采集节奏** | 1x 约 **10 秒**采 1 份；可倍速；采完 **自动续采**；停止／改令结束 | `HostCommandBridge.GatherWallSecondsAt1x`、`HostWorkLoop` |
| **劳动进度板** | 按地点记劳动 ticks＋**harvest 计次**（按角色去重） | `LocationLaborProgressBoard` |
| **新条件 kind** | `laborAtLocation`／`uniqueLaborAtLocation`／`uniqueHarvestAtLocation`／`characterAtLocation` | SCHEMA、QuestEditor `ContentFieldCatalog` |
| **小队共用背包** | 全队 **一个** 50 槽包；堆叠；筛选；一键整理；采集／`addStock` 入包 | `PartyInventory`、`HostInventoryPanel`（**B**／顶栏） |
| **任务日志** | **J**／HUD「任务」：可接｜进行中｜已完成；完成→**ReadyToClaim**→领奖才发奖 | `HostQuestJournal`、`QuestService` |
| **输入门** | 日志／背包打开时挡相机与点选，并暂停 | `HostInputGate` |
| **顶栏资源** | 显示背包占用与粗粮／木／药／敛息草数量 | `HostFormalHud` |
| **物品名** | 粮食→粗粮；灵草显示名→灵药；Items 对齐 resource Id | `resources.json`、`items.json` |

---

## 3. Ch01 三环（当前真源）

| 顺序 | 任务 Id | 玩家做什么 | 完成条件 |
|------|---------|------------|----------|
| 1 | `base:quest_ch01_ref_inspect_yard` | 不同角色到农田各采到粗粮×1 | `uniqueHarvestAtLocation` ×3 |
| 2 | `base:quest_ch01_ref_dispatch_party` | 农田／药田／树凑齐粗粮·灵药·粗木各 ≥3 | `stockAtLeast` ×3（背包） |
| 3 | `base:quest_ch01_ref_gather_wood` | 三人到集合点 | `characterAtLocation` ×3 |

- 全部 **`autoOffer: false`**：需在任务日志手动接取（上一环完成后下一环出现在可接列表）。  
- 集合点约定：地图可多实例 `rallyPoint`，任务绑的是 **地点 Id** `base:loc_ref_party_rally`，不是「任意集合点 prefab」。

---

## 4. 小队背包（设计要点）

| 项 | 约定 |
|----|------|
| 归属 | **小队共用一个包**（非每人一份） |
| 容量 | 默认 **50** 槽；同类可堆叠（资源默认上限 99） |
| 入包 | 劳动采集、探索掉落、`addStock`、敛息草等走 `world.Inventory` |
| 任务 | `stockAtLeast` → `Inventory.GetCount` |
| UI | **B** 或顶栏「背包」；筛选（全部／资源／消耗／其它）；一键整理 |
| 与聚落库存 | 日结／设施仍可写 settlement stock；**玩家可见与任务进度以背包为准** |

代码：`Assets/Scripts/Core/Inventory/PartyInventory.cs`、`HostInventoryPanel.cs`；启动时 `ContentRuntimeBootstrap.ApplyInventoryCatalog` 从 Resources＋Items 注册。

---

## 5. 劳动与续采

| 项 | 说明 |
|----|------|
| 时长 | `GatherWallSecondsAt1x = 10`；随 Host 倍速缩短现实等待 |
| 产出 | 劳动完工发 **1** 份地点 `resourceOnExploreId`，并记 harvest |
| 续采 | `HostWorkLoop`：采完自动再下劳动令；**停止**或非劳动指令结束循环 |
| 进度 UI | 任务进度可用 `ProgressCount`／`ProgressMax`（如 1/3→3/3） |

---

## 6. 任务状态机增量

```text
Inactive → Active → ReadyToClaim（待领奖）→ Completed
```

- 条件满足进入 **ReadyToClaim**，奖励在日志「领取奖励」时发放。  
- 日志与背包互斥优先：日志开着时背包自动关；二者都挡世界输入并暂停。

---

## 7. 编辑器／SCHEMA

- QuestEditor 条件下拉已含劳动／采集／指定角色在地点四类（见 [110](110-content-studio-quest-editor-usage.md)）。  
- `SCHEMA.md` `condition.kind` 已补上述 kind；`abandonable`／ReadyToClaim 说明已写入。

---

## 8. 已知未做／注意

- EditMode 若仍断言 **settlement GetStock**，需改为读背包（部分测试可能未跟完）。  
- EventEditor **choices** 可视化、好感 `relationAtLeast`、ChapterEditor 仍未做。  
- 聚落仓库与玩家背包双轨：制作内容时勿把「任务物资」写到仅写 settlement 的路径。

---

## 9. 建议手操检查

1. Play Ch01 → **J** 手动接任务 1 → 三人轮流农田采到 3/3 → 领奖  
2. 接任务 2 → 三地采齐各 ≥3 → 顶栏／**B** 看背包数量 → 领奖  
3. 接任务 3 → 三人派到集合点 → 完成  
4. **B** 整理／筛选；故意堆满 50 槽确认采不到  
5. 1x／2x／5x 对比采集节奏；点停止确认不再续采  

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-14 | 初版：三环任务、劳动计次、续采、共用背包、任务日志、文档同步 |
