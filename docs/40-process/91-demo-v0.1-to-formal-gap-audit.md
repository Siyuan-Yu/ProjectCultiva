# Demo v0.1 → 正式框架缺口审计

> 状态：**执行中（PKG-A 已提交；PKG-B/C 进行中）**｜日期：2026-08-02  
> 对照：[49 Demo 冻结快照](49-demo-v0.1-prototype-status.md)＋[32 桥接](../30-tech/32-prototype-to-product-bridge.md)  
> 正式侧：PlayableHost＋Core＋Content／BaseGame  
> **纪律：**只补文档已承诺语义；不复活 Demo Runtime 为玩法真源；不新增文档外功能。

## 0. 判定规则

| 判定 | 含义 |
|---|---|
| **Done** | 正式侧已具备可验收等价 |
| **Gap** | [32]§2「正式不得丢」或 [49] 验收清单内，正式未齐 → **必须补** |
| **Out** | [49]§3／Freeze／硬停明确不做，或 [32]§3 未验证且正式已写不做 → **不补** |
| **Extra** | 正式已有、Demo 没有（保留，不回退） |

架构：`Demo Runtime` 只读参考；Host 薄适配；规则只进 Core／Content。

---

## 1. 总表

| # | Demo 能力（[49]） | 正式现状 | 判定 | 依据 |
|---|---|---|---|---|
| 1 | 三人点选／框选／Shift | HostSelectionController | Done | [61] |
| 2 | 双击全选三人 | 无 | **Gap** | [49]§4.1 |
| 3 | 右键地面移动且中断工作／修炼 | HostMove 仅表现插值，不取消 Action | **Gap** | [32]§2 移动；[49]§4.2 |
| 4 | 右键工位→走近→持续产木／粮／药 | 无工位；Labor／日产／Explore 抽象 | **Gap** | [32]§2 工作；[49]§5.1 |
| 5 | `W` 工位指针模式 | 无 | **Gap** | [49]§4.2；[32] 工作 |
| 6 | `S` 停止当前指令 | 无 Stop 命令 | **Gap** | [49]§4.2 |
| 7 | 右键灵地／`C` 入定修炼 | Cultivate 有；无灵地右键走近 | **Gap** | [32]§2；[49]§4.3 |
| 8 | `X` 出定 | 无专用出定 | **Gap** | [49]§4.3 |
| 9 | `G` 敛息草降暴露（开局 3） | 无敛息草资源／消耗 | **Gap** | [49]§4.3；[32] 映射 |
| 10 | 暴露：昼高／夜低／近主管加成（只显示） | Risk 仅 Cultivate 每 Tick+1 | **Gap** | [49]§4.3；[32]§6 |
| 11 | 暂停／1x／2x／5x | Host 已有 | Done | [61] |
| 12 | 全村劳役表 UI（**前期只读**） | Core Schedule 有；Host 无课表面板 | **Gap** | [32]§2「前期只读」 |
| 13 | 课表测试可改格 | — | **Out** | [32] 夺权后可改；现阶段只读 |
| 14 | 每日任务木／粮／药配额 | DailyTask＋木／灵草；缺粮；非工位产出 | **Gap** | [32]§2；[49] M3 |
| 15 | 主管愤怒（工时偷懒且靠近才涨，只显示） | 日终 SupervisorPressure 事件（语义已换） | **Gap** | [49]§4.5；[32] 映射 Obligation |
| 16 | NPC 日程（主管巡视／守卫 Patrol／Rest） | 三类 Schedule AI Partial | **Gap** | [32]§2；[49]§4.6 |
| 17 | 村民群体状态标签 | 无 VillageCrowdPresenter | **Gap** | [32] 层4；[49]§5.8 |
| 18 | 商人游荡占位 | 无 | **Gap** | [49]§4.6（占位氛围） |
| 19 | NPC／单位头顶活动字 | 无 | **Gap** | [49]§4.6；[32] 纯表现 |
| 20 | 80×50 Sprite 瓦片荒村＋工区／灵地 | 3D Quad 灰盒＋8 抽象点 | **Gap** | [46]–[48] Sprite；[49]；产品 2D |
| 21 | 正交 XY 相机＋中键拖＋滚轮 | 正交 XZ＋WASD | **Gap** | [49]；2D 约定 |
| 22 | 选中环／框选绿框／落点／飘字 | 高亮有；落点飘字弱 | **Gap** | [32] 纯 Unity 表现 |
| 23 | `A` 攻击占位（无伤害） | 无 | **Out** | [49]§3／[62] 不做战斗；[32]§2 必保表未列攻击 |
| 24 | 真战斗／夺府／发现追捕／突破（Demo 未做） | 突破 Formal 已有；其余不做 | **Out**／Extra | [49]§3；[74] |
| 25 | 正式 UGUI／精美美术 | IMGUI／占位 Sprite | **Out** | [49]§3；ADR-0009 |

---

## 2. 必须补齐的工作包（按依赖）

### PKG-A｜2D 表现 parity（不改玩法规则）

目标：PlayableHost 打开即是 **Sprite＋XY 正交**，不再出现 Capsule／Quad 3D 灰盒。

- EntityView → Demo 角色 Sprite Prefab（`ReplaceableSprite`／现有 Prefabs）
- 地图 → 复用 `Assets/Prefabs/Environment/Tiles/*` 与建筑 Prefab 铺装（项目无 Unity Tilemap，沿用 Demo Prefab 砖，**不新加 Tilemap 包**）
- 相机 → Demo 式正交 XY；中键拖；滚轮；WorldBounds
- 选择／移动射线 → Physics2D
- 选中环、头顶字、区标签、落点／飘字（Host 表现组件，订阅 Core／命令结果）

### PKG-B｜命令与行动语义（Core＋Host，对齐 [32]§2）

- `PlayerCommandKind.Stop`／`Move`（或等价取消＋Move Order）
- Move／新命令 **中断** 当前 Labor／Cultivate（[49]§5.4）
- WorkZone／WorkSpot 配置进 Content；右键／`W` → Gather／Farm 持续产出 → ResourceLedger 事件
- 资源补齐：粮食＋敛息草（木／药已有或改名对齐）
- `C`／灵地右键 → Cultivate；`X` → Stop cultivate
- `G` → 消耗敛息草降 `PersonalConcealmentRisk`
- 暴露 Tick：昼／夜／近主管（只改数值，不惩罚）

### PKG-C｜HUD／反馈（Host IMGUI，正式 UGUI 仍 Out）

- 劳役表只读面板
- 任务／资源／愤怒（只显示）／修炼／详情对齐 [49] 信息密度（可并入 FormalHud，不新发明玩法）
- 双击全选；底栏选中状态

### PKG-D｜NPC 氛围（Content＋薄表现）

- 主管／守卫日程与路径点（Content schedule＋地点）
- 村民群体标签（层4 表现）
- 商人游荡占位（日程移动表现）

### PKG-E｜一比一参考关（完成 A–D 后）

- 以 Demo 荒村布局为样板的 Content Region＋Scenario（工位数量：田5／林4／药3＋灵地）
- PlayableHost 默认开此关
- Play Mode 验收清单＝[49]§5（除攻击项）
- 自动化门禁＋验收报告

---

## 3. 明确不补（防范围膨胀）

1. 攻击键／交战占位／真伤害  
2. 课表可编辑（夺权前）  
3. 暴露／愤怒真实惩罚演出  
4. 夺府、发现追捕、潜行判定  
5. 产品级 UGUI、精美美术  
6. 解冻／迁移 Demo Runtime 规则代码进 Core  
7. Unity Tilemap 包、任意文档未写系统  

---

## 4. Formal 已有、Demo 没有（保留）

突破／功法、社会 Help／Slight／Recruit、据点分工日产、Travel／Explore、Quest／Chapter／Flags、Snapshot、Content 工具链。  
一比一关卡 **叠加** 在这些之上，不删除。

---

## 5. 执行顺序与验收

```text
PKG-A → PKG-B → PKG-C → PKG-D → 测试门禁 → PKG-E 一比一关 → [49]§5 手操清单自动化子集 → 验收报告
```

每包：实现 → EditMode 测 → Commit → Devlog。  
**完成前不请求人工审核。**

## 6. 与 Ch01 Reference Level 关系

[89] 灰盒模板关保留为「内容结构模板」；本审计的 **手感真源** 是 [49]。  
PKG-E 产出的一比一关替换／升级 Host 默认可玩验收场景。
