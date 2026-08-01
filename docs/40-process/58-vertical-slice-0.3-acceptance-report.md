# Vertical Slice 0.3 最终验收报告

> 状态：**验收快照（只读总结，本文件不启动下一阶段）**  
> 日期：2026-08-01  
> 前置：VS0.2 已验收（`56`）；计划 `57-vertical-slice-0.3-plan-v0.1.md`（A–D 已批准）  
> 相关提交：
>
> - `5dd54d5` feat(core): vs0.3 phase a day clock  
> - `8893906` feat(core): vs0.3 phase b-d observe cultivate quota  
>
> 测试门禁（A–D 完成后）：EditMode **100/100 Passed**
>
> **过程注记：** B–D 曾连续开发并合并为一提交，**不符合**「每 Phase 单独测试／commit」纪律。后续切片**严格**按 Phase 串行：实现 → EditMode 全绿 → 单独 commit → 停等确认 → 下一 Phase。

---

## 1. 目标回顾

VS0.3 验证**「第一天」规则可证明闭环**（非产品壳、非第一章导演）：

```text
DayClock 日循环
  + Schedule 默认劳役／休息
  + Observe → 抽象 OpportunitySite
  + CultivationAttemptGate → 既有 Cultivate（青云诀经机会获得）
  + PersonalConcealmentRisk（最小 0–100）
  + DayEnded QuotaConsequence（薄后果＋日切重置）
```

冻结口径：RTS PlayerOrder；Schedule＝默认行为可 Override；Narrative 仅 NameKey／短描述；无战斗／地图／寻路／主管视线／潜行／目击者／产品 UI。

---

## 2. 已完成能力汇总

### Phase A — DayClock／日循环

| 能力 | 说明 |
|---|---|
| `DayClock` | 由 `WorldTick` 派生 `dayIndex`／`tickInDay`／`hourOfDay`（不另存） |
| 日切事件 | 跨日：先 `DayEnded`，后 `DayStarted` |
| `IDayBoundaryHandler` | 日界钩子；供 D 接入结算 |

### Phase B — Observe＋OpportunitySite

| 能力 | 说明 |
|---|---|
| `ObserveAction`／`OrderType.Observe` | PlayerInput → Order → ActionClock → `ObservationResolved` |
| `OpportunitySite`／`KnownSitesComponent` | 抽象位点；无坐标／地图；发现写 KnownSites |
| Content | `sites.json`＋`OpportunitySiteDefinition`；NameKey＋短描述 |
| 事件 | `OpportunitySiteDiscovered`、`ObservationResolved` |

### Phase C — Cultivation Gate＋ConcealmentRisk

| 能力 | 说明 |
|---|---|
| `CultivationAttemptGate` | 无已知可修炼 Site → 拒绝；有 Site 可走修炼入口 |
| 青云诀 | BaseGame 保留定义；主角**不默认学**；经 Site `OfferedManualId` 在 Gate 中 Learn |
| Player `Cultivate` | Port 先 Gate 再入队；复用既有 `CultivateAction` |
| `PersonalConcealmentRisk` | 0–100 最小规则状态；Cultivate 推进时累加；**无**主管视线／潜行／目击者 |

### Phase D — QuotaConsequence

| 能力 | 说明 |
|---|---|
| `QuotaConsequenceHandler` | 默认挂在 `DayEnded` |
| 后果 | `QuotaConsequenceApplied`＋`PendingReprimand`／`LastSettledDeviation` |
| 日切重置 | 结算后清零当日 `CompletedAmount`／`Deviation`（Required 保留） |
| 不做 | 主管实体、训斥演出、关系网、没收／GameOver |

---

## 3. 当前完整玩家循环（规则层）

```text
角色（Schedule + DailyTask + KnownSites + Risk）
  ↓
ScheduleDriver：无人下令时 Labor／Rest
  ↓
玩家 RTS：Observe（可 Override 计划 → 可记 Deviation）
  ↓
发现 OpportunitySite → KnownSites
  ↓
玩家 RTS：Cultivate
  ↓
Gate：校验 Site →（若未学）Learn 青云诀 → CultivateAction → Progress↑
  ↓
PersonalConcealmentRisk 累加
  ↓
DayClock 跨日 → DayEnded
  ↓
QuotaConsequence：消费 shortfall／Deviation → 事件＋薄标记 → 日计数重置
  ↓
DayStarted；Snapshot 可恢复一致
```

EditMode 命令序列可证明；**不是**「点屏幕就能玩」的产品循环。

---

## 4. 当前可玩边界

### 4.1 已支持（规则／EditMode）

| 项 | 说明 |
|---|---|
| 日／时／刻派生与日界事件 | `DayClock`＋`DayEnded`／`DayStarted` |
| Schedule 默认行为＋Player Override | 继承 VS0.2 |
| Observe 发现抽象机会点 | 可强制发现率；无地图 |
| 经机会获得修炼入口并 Cultivate | 青云诀不默认学 |
| 隐匿风险记账 | 0–100 数值 |
| 日终 Quota 薄后果 | 事件＋`PendingReprimand` |
| Snapshot 往返 | Site／Manual／KnownSites／Risk／Quota 标记等 |

### 4.2 未支持（故意不做）

| 项 | 说明 |
|---|---|
| Unity RTS 输入表现 | 无镜头／点选／框选产品壳 |
| 地图／寻路／移动 | Site 无坐标 |
| 战斗 | — |
| NPC 高级 AI | Schedule≠效用 AI |
| 主管系统 | 无视线、训斥演出、Boss |
| 潜行／目击者模拟 | Risk 仅数值 |
| 第一章导演／固定剧情脚本 | Narrative 仅文案键 |
| 完整关系／处罚产品化 | 仅薄标记 |
| 产品 UI／Localization 产品壳 | — |

---

## 5. 架构观察

### 5.1 Order

| 观察 | 后续 |
|---|---|
| Player／Schedule 优先级与 Override 已稳定 | 紧急／战斗档仍缺，扩前对齐 `35` |
| Cultivate 经 Port＋Gate；直连 `CreateCultivateOrder` 仍可供旧测旁路 | 是否统一强制 Gate 需 ADR |
| Order 字段继续堆叠 | 类型增多后宜负载 DTO |

### 5.2 Event

| 观察 | 后续 |
|---|---|
| 日界／观察／发现／Quota 后果事件已齐 | payload 多字符串约定，膨胀后需结构化 |
| `QuotaDeviationCreated`（打断时）与 `QuotaConsequenceApplied`（日终）分工清晰 | 保持「记账 vs 消费」分离 |

### 5.3 Snapshot

| 观察 | 后续 |
|---|---|
| 已含 Sites／Manuals／KnownSites／Risk／PendingReprimand／发现率 | Schema 仍为 v1 字段膨胀；分片序列化待议 |
| 恢复后默认再挂 `QuotaConsequenceHandler` | 多 Handler 注册策略若变复杂需约定 |

### 5.4 Data Pipeline

| 观察 | 后续 |
|---|---|
| `opportunitySite` 类型＋`sites.json` 严格字段 | OfferedManual 交叉引用校验可加强 |
| NameKey／短描述进 Content，Core 不写剧情分支 | 保持；禁导演进 Core |
| Manual／Site 运行时需 `Register*` 进 World | Content→World 一键装配可另刀，非本切片债 |

### 5.5 是否需要 ADR（建议挂账，本报告不立项）

| 议题 | 建议 |
|---|---|
| Player Cultivate 是否**必须**经 Gate（废除测试旁路） | 值得短 ADR |
| Event payload 结构化 vs 继续字符串 | 事件种类再增前定 |
| Snapshot schema 升版策略（分片／版本） | 字段再涨前定 |
| Risk 累加公式（每 tick／仅偷修窗／与 Deviation 换算） | 玩法数值前定 |
| 日切重置范围（是否清 PendingReprimand、跨日保留） | 主管 consum 前定 |

**本验收不阻塞于上述 ADR**；未决则不得在下一切片静默扩大语义。

---

## 6. 下一阶段建议（只规划，不编码）

**本文件不自动开工下一阶段。** 候选方向（需单独确认）：

| 优先级（建议） | 方向 | 说明 |
|---|---|---|
| 1 | 薄调试 Host | 把 Port 接到 Scene，验证一日循环手感（非产品 UI） |
| 2 | Quota／Risk 消费者 | 日终标记被某条可读规则消费（仍可不做完整主管 AI） |
| 3 | Observe／Site Content 扩展 | 多位点、发现权重进表；交叉引用校验 |
| 4 | Opportunity→更多入口 | 非仅修炼（藏匿、交易线索等）接口切片 |
| 5 | 文档／ADR 清偿 | Gate 强制、Event／Snapshot 策略 |

**不建议紧接着铺开：** 完整地图、寻路、战斗、主管 Boss、章节导演、产品级 RTS UI。

---

## 7. 过程纪律（强制记录）

后续所有垂直切片／Phase **严格遵守**：

1. **每 Phase 单独实现**（范围外不连做）  
2. **每 Phase 单独跑 EditMode，全绿才允许进入下一 Phase**  
3. **每 Phase 单独 commit**（禁止 B–D 式合并提交）  
4. Phase 完成后停等确认，再开下一 Phase  

本次 B–D 合并提交作为反例记入，不作为先例。

---

## 8. 验收结论

| 项 | 结论 |
|---|---|
| Phase A／B／C／D 目标 | **达成**（规则层） |
| 完整循环（§3） | **可在 EditMode 证明** |
| 可玩产品壳 | **否**（见 §4.2） |
| 是否进入下一阶段 | **否**——停止于此；待新确认／计划后再编码 |

验收签字栏（人工）：

- [ ] A–D 交付与目标一致  
- [ ] 可玩边界无歧义  
- [ ] 同意过程纪律（§7）  
- [ ] 同意「不自动进入下一阶段」  
|
