# ADR-0029：Construction Content、Runtime 与 Snapshot 边界

> 状态：已采纳
> 日期：2026-09-06
> 决策者：制作人

## 背景

LocalMap Construction V1 需要可扩展建筑目录、PartyInventory 材料成本、FactionFlag 建造与主动拆除，但不得把建筑伪装成物品、复制 FactionFlag／Territory 领域规则，或扩张 Snapshot 契约。

## 决策

1. `type = building` 是独立 ContentDefinition 类型，不属于 ItemDefinition。
2. Data `BuildingDefinition` 在启动和 Snapshot static-shell restore 时映射为纯 Core `ConstructionCatalog`；Catalog 是静态内容壳，不是存档 authority。
3. `ConstructionService` 是材料与世界对象组合提交的事务边界；Host UI／Presenter 不直接执行 FactionFlag mutation。
4. FactionFlag placement、创建、摧毁与领土重算继续由既有 FactionFlag Domain／Territory Resolver 承担。主动拆除是 Construction 操作，战斗摧毁不返料。
5. V1 建造与拆除均即时完成；持久结果仅由既有 FactionFlag Snapshot 与 PartyInventory Snapshot 保存，不新增 Construction Snapshot DTO，也不向 FactionFlagState 写入成本或来源。

## 影响

- 新建筑可继续通过 BuildingDefinition／ConstructionPlacementKind 扩展，不需要制造 deployable item。
- 静态 Definition 修改会影响现有同类建筑的未来拆除返还；V1 接受该内容语义。
- 解锁进度、施工过程、来源 provenance 若未来成为持久状态，必须另行决策并升级相应 Snapshot 契约。
