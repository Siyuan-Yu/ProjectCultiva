# 139 · 大地图 RTS 下令与部队交互（2026-08-17）

> 状态：**已实现（Host + Core 第一刀）**｜日期：2026-08-17  
> 相对：[138 接战弹窗计划](138-world-strategic-battle-offer-plan-2026-08-17.md)｜[129 Host 出行](129-world-graph-host-travel-scene-isolation-2026-08-16.md)  
> 飞书（138 同系列，请同步本节）：https://my.feishu.cn/docx/Aodwd4XpNoPdzqxQ5wucY2LFnAg

---

## 1. 玩家操作（大地图 M）

| 操作 | 行为 |
|------|------|
| **左键头像** | 选中/多选己方角色 |
| **右键节点** | 确认弹窗 → 移动到该节点（多段路径自动续走，到点停下） |
| **右键道路** | 确认弹窗 → 移动到路上指定进度（到点停下） |
| **右键敌军/他方栈** | 上下文菜单（见下） |
| **Space / 倍速** | 全局时间（与 LocalMap 共用同一时钟） |

### 1.1 右键他方 ArmyStack 菜单

| 关系 | 菜单项 |
|------|--------|
| **敌对** | 攻击 · 跟随 · 查看详情 |
| **非敌对** | 攻击 · 跟随 · 交谈 · 查看详情 |

- **攻击**：与 138 一致——追击到栈位置；**先到先接战**，后到的弹「加入战斗」；手动接战进保底 Encounter LocalMap（路上）或节点 LocalMap（人在节点时）。
- **跟随**：RTS 跟随目标栈；栈移动则持续同步位置；**取消跟随**：对地图点/节点下新移动令。
- **交谈**：占位（尚未接 ContentEvent）。
- **查看详情**：状态栏显示栈名、帮派、人数、战力、位置、关系。

### 1.2 移动 vs 跟随

| 目标类型 | 到达后 |
|----------|--------|
| 节点 / 道路点 | **停下**（`RouteAnchored` 或 `AtNode`） |
| 他方栈（跟随） | **保持跟随**；目标栈动则续跟 |

---

## 2. 代码入口

| 模块 | 职责 |
|------|------|
| `WorldTravelTarget` | 节点目标 / 道路进度目标 |
| `WorldTravelPathService` | BFS 多段路径、道路点击、队列续走 |
| `StrategicFollowService` | `FollowStackId`、每 tick 同步到栈锚点 |
| `StrategicPursuitService` | 攻击追击（`PursuePartyIds`，与跟随分离） |
| `HostWorldMapPanel` | 选队、右键菜单、移动/攻击/跟随 UI |
| `HostWorldTravelConfirmPrompt` | 移动确认（大地图下令不强制 LocalMap 边缘） |
| `HostWorldTravelDeparture.BeginMacroOrder` | 宏观层直接开走 |

---

## 3. 接战规则（与 138 对齐）

- **不要**等全员到齐再接战。
- 追击名单 `PursuePartyIds` ≠ 已在战斗 `EngagedPartyIds`。
- 普通赶路**经过**路中敌军栈不被动弹窗；仅主动攻击/追击抵达弹窗。
- 战斗胜利后参战者 **RouteAnchored** 留在原地。

---

## 4. 已知限制（后续）

- [ ] 交谈接江湖关系 / ContentEvent
- [ ] 跟随距离上限、脱离战斗后是否保持跟随
- [ ] 大地图 UI 显示「跟随中」标记
- [ ] 飞书 138 正文合并本节（需人工粘贴或导出同步）

---

## 5. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-08-17 | 宏观移动重做：右键节点/道路、BFS 续走、BeginMacroOrder |
| 2026-08-17 | 右键栈菜单：攻击/跟随/交谈/详情；StrategicFollowService |
