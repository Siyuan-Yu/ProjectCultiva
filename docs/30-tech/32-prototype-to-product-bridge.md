# Prototype → Product Bridge（Demo 到正式开发桥接）

> 状态：**已冻结（v0.1）** | 优先级：P0 | 最后更新：2026-07-31  
> 上级：`docs/00-project/00-overview.md`  
> 关联：`33-architecture-core-rules-freeze-v0.1.md`、`49-demo-v0.1-prototype-status.md`、`31-architecture.md`  
> **目的：** 把 Demo 已验证的玩法语义冻结为正式接口需求；后续重构只换实现，不改语义。  
> **本阶段不写实现代码。**

## 1. 桥接原则

1. **Demo 是语义探路，不是正式架构实现。** 当前 `Assets/Scripts` 下的 MonoBehaviour 原型可参考，不可当作 Core 边界。
2. **已验证语义 = 接口契约。** 重构后玩家可感知行为应保持一致（或仅在文档标明的「故意升级」处变化）。
3. **禁止借重构偷偷改玩法。** 若要改语义，先改设计文档与本桥接表，再改代码。
4. **项目已停止扩展 Demo。** 不再增加突破／夺府／潜行判定等 Demo 功能；这些进入正式设计与后续实现阶段。

## 2. Demo 已验证清单（冻结为需求）

| 语义 | Demo 中的表现 | 正式接口需求（不得丢） |
|---|---|---|
| 三人控制 | 点选／框选／Shift；独立移动与行动 | 至少 3 名可独立下令的可控实体；可扩展到第一层修士上限 |
| 时间推进 | 暂停／1x／2x／5x；暂停停进度 | 表现层倍速／暂停 → 逻辑层少推或不推 Tick |
| 时间表 | 全村劳役表；可查看；测试可改 | 身份时间表；前期只读；夺权后可改（权限见 `21`） |
| 工作循环 | 右键／下令 → 走近 → Working → 按时间产资源 | 意图式指令；到达交互距后进入工作态；可中断 |
| 资源循环 | 木材／粮食／草药库存与任务进度 | 资源账本；任务统计「发布后新增」类规则可保留 |
| 多角色分工 | 一人修炼、他人工作互不覆盖 | 每实体独立行动槽／指令队列 |
| 修炼入定语义 | 走近灵地后 Cultivating；非工作式选目标战法 | 修炼是收敛行动；可与移动／工作互斥 |
| 个人暴露显示 | 昼夜／主管附近影响数值；敛息草降低 | 映射为「个人隐匿风险」层；正式可加重罚，不合并三层 |
| NPC 自主运转 | 守卫 Patrol/Rest、主管昼夜、村民群体状态 | 日程驱动；群体层不逐人位置（对齐四层模拟） |
| 统一行动框架 | Move／Gather／Cultivate 状态机 | 正式 `Intent`／`Action` 模型的语义前身 |

## 3. Demo 明确未验证（不要假装已有）

| 内容 | 说明 |
|---|---|
| 第一次突破事件 | 仅有修为增长；无事件／异象／失败 |
| 真战斗伤害与技能 | `A` 仅为交战占位 |
| 发现／追捕／潜行判定 | NPC 日程无感知逻辑 |
| 夺府与管理权限 | 未做 |
| Modifier 管道 | 直接改数值，非正式管道 |
| Tick 逻辑层 | 连续游戏分钟，非正式 Tick |
| 怀疑值／势力敌意 | 未做独立层 |

## 4. 语义 → 正式模块映射

| Demo 概念 | 正式归属 | 迁移策略 |
|---|---|---|
| `GameClock` | 表现层时钟 + Tick 调度器 | 显示保留；结算改走 Tick（`33` §2） |
| `CharacterActionController` | Core：Intent／Action 执行器 | 状态名可保留；逻辑迁出 Unity |
| `UnitCultivation` | Core：修为／灵力／个人隐匿风险 | 字段对齐 `2B`；增量改走 Modifier／Tick |
| `ScheduleService` | Core：时间表服务 | 从「仅全村一张」扩展为多身份，语义保留 |
| `WorkSystem`／工位 | Core：生产＋World 交互点 | 产量公式进配置；禁止脚本写死 |
| `CultivationSystem` | Core：修炼＋环境＋隐匿层输入 | 暴露三层按 `33` §6 拆分 |
| `AmbientNpcActor` | 第二层实体日程；第四层用群体呈现器 | 守卫／主管保留实体；村民以群体为主 |
| `NpcScheduleConfig` | 数据驱动日程 | CSV／JSON 真源；SO 仅缓存 |
| `ResourceInventory` | Core：资源账本 | 保持可追溯增减 |
| `DemoPrototypeHud` | 临时 IMGUI；非正式 UI | 正式 UGUI／Toolkit 另立 ADR |

## 5. 重构顺序（建议，仍不编码）

```
1. 建立 asmdef：Core / Data / Unity / Tests（边界先于功能）
2. Tick 调度 + 把现有分钟结算映射为 Tick
3. AttributeModifier 管道 + 把修为／产出／暴露改为管道写入
4. Intent／Action 对齐 Demo 行动语义
5. 实体分层：可控修士／关键 NPC／群体组件拆分
6. 再实现第一次突破事件（第一章）
```

每一步的验收标准：**旧 Demo 可复述的玩家操作，在新架构下行为一致。**

## 6. 接口需求摘要（给实现期用）

正式系统对外至少保证：

1. `IGameTime`：当前 Tick、推进 N Tick、暂停标志。  
2. `IModifierSink`：`AddModifier`／按来源移除／查询 Final 与溯源列表。  
3. `IActionQueue`：下达／插队／取消意图；状态含 Idle／MovingTo／Working／Cultivating／Interrupted。  
4. `ISchedule`：按实体或身份查询当前时段活动。  
5. `IResourceLedger`：增减资源并记录原因。  
6. `IConcealment`：个人隐匿风险、按 NPC 的怀疑值、势力敌意——三套 API，不合并。

具体签名在实现前可再定；**职责分离已冻结**。

## 7. 文档与阶段

| 文档 | 作用 |
|---|---|
| `49-demo-v0.1-prototype-status.md` | Demo 玩法快照（冻结，不再当开发任务板） |
| `33-architecture-core-rules-freeze-v0.1.md` | 架构核心规则 |
| 本文 `32` | Demo ↔ 正式桥接 |

**当前阶段：架构冻结阶段** — 完善设计契约，等待下一阶段规则确认后再编码。
