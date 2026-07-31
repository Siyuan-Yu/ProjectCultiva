# ADR-0015：DefinitionId 采用 namespace:local_id

- 状态：**已采纳**
- 日期：2026-07-31
- 决策者：项目负责人（架构冻结增量）

## 背景

多包并存时无命名空间必然 ID 冲突；显示名不能当 ID。

## 决策

`namespace:local_id`；官方 `base`；改名必须 `DataMigration`；禁止静默覆盖。

## 影响

存档与校验必须记录命名空间来源。见 `36`、`33`。
