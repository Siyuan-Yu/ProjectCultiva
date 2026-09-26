# ADR-0044：个体凡人、势力劳动、需求与俘虏方向

**Accepted as Design Direction（2026-09-27）；Design Confirmed / Documentation Updated / Implementation Not Started；Producer Acceptance: Not Applicable Yet。** 来源：制作人 CIVILIAN-LIFE-01 设计冻结指令。

## Context

旧文档允许普通凡人由 `MortalPopulation`／岗位组作为匿名权威，只在受关注时实体化并可能归并。该方向无法可靠表达谁劳动、谁饥饿、谁逃亡、谁被俘与招募，也与战后个体命运和真实住房容量冲突。

## Decision

- 每个凡人是持久真实 Character；人口、劳动力与职业统计由人物派生，匿名 `MortalPopulation` 不再是现行人物或劳动 authority。
- 有归属凡人使用真实 FactionMembership；无归属凡人不接受势力排程。FactionMembership、PlayerParty 与 ActiveCharacter 保持分离。
- V1 需求只含 Satiety 与 Energy，并按危险／强制状态／需求／工作顺序仲裁；食物必须来自可访问库存。
- 每人最多一个专业职业：Unassigned、Farmer、HerbFarmer、Logger、Medic；所有有行动能力凡人均可搬运／救援与建设。
- Site 易主不改居民势力。战败势力居民按对当前 Faction 的 Loyalty 留下或真实 Fleeing；逃出 Site 后成为保留身份和位置的 Displaced，不删除。
- 捕获必须来自带 Capture/Subdue intent 的正常 CharacterEncounter。俘虏是同一 Character，由真实人物押送；没有合法 PrisonerOnly Residence 就不能成为 Detained。
- PrisonerOnly 复用现有 Residence 和真实容量。Detained 保留原 FactionId/Loyalty 与需求，不参加工作；获得食物和正常休息的日子才降低 Loyalty。
- 招募阈值为 Loyalty < 15；成功后加入 PlayerFaction 并建立新 Loyalty = 50，仍为凡人。释放转入 Fleeing；处决是囚犯操作。
- 住房仍使用当前可见表现。性能通过 Character LOD 演进，不使用住房黑箱、private presence、匿名囚犯或匿名劳动力。

## Supersession

本 ADR 定向取代旧文档中“普通凡人群体统计是真源、关注后才实体化、普通凡人可归并回人口池”的产品方向。历史 Freeze 与旧过程记录保留其时点事实；新实施不得据此恢复匿名凡人 authority。ADR-0024 关于**修士**为真实 Character 的规则继续有效。

## Consequences

既有农业／劳动需在实施时增量迁移到具名 worker；人员 UI 显示派生统计与人物行，不采用大型工作矩阵。Snapshot 未来必须增加需求、职业、忠诚、逃亡、押送、拘留与住房分配持久化，但本 ADR 不修改当前 schema。修士生活 AI、战略 NPC AI、巨型凡人军队及复杂囚犯系统均不在本决策范围。

完整规则、非目标、可调项与未来人工验收愿景见 [267](../267-civilian-life-01-design-freeze-2026-09-27.md)。
