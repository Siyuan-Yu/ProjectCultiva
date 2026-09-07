# ADR-0030：Social Bond、五维态度与 Snapshot 边界

> 状态：已采纳｜日期：2026-09-06｜最后更新：2026-09-06

## 背景

既有个人关系只有 `RelationshipLedger` 的单一分数，无法区分客观亲属／师徒事实与角色主观态度，也无法表达“喜欢但不信任”或“痛恨且畏惧”。同时，角色可能在弥留若干 Tick 后死亡，击杀后果需要跨存读档保留责任者。

## 决策

1. `SocialBondBoard` 是客观关系事实的独立 Runtime authority；它不进入 `RelationshipLedger`，也不由五轴数值推断。
2. `RelationshipLedger` 继续是个人态度唯一真源，事件增加 `Axis` 与可选 `ContextEntityId`；`RelationshipComponent` 只缓存完整五轴。
3. 旧 `Score`／旧关系 API／旧 `openingRelations` 保持 `Affection` 语义，避免另起第二套关系系统。
4. Snapshot v6 使用可选字段软扩展：`socialBonds`、事件轴／上下文和死亡责任者；缺失字段按旧语义恢复，不提升版本。
5. 死亡责任者属于角色弥留期运行时连续性，保存在 Entity Snapshot；恢复与被俘时清除，确认死亡后消费并清除。
6. 客观社会事件与态度后果分层：`SocialEventService` 发布事实，`SocialConsequenceService` 统一写 Ledger 与 `SocialReaction`。

## 影响

- Bond 与态度可以独立变化，不产生重复权威。
- 老存档和老 Content 继续可读；新字段仍由当前 v6 serializer 明确写出。
- 击杀反应可在流血死亡和 Save→Load 后保持确定性。
- V1 的全局即时传播是明确简化；未来知识／目击系统只能改变事件传播门槛，不得改写 Ledger 与 Bond 的权威边界。
