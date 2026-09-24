# VASSAL-WORK-01 — Direct Vassal Labor Access

> 状态：Producer Accepted / Sealed
> 日期：2026-09-24
> Snapshot：v8 unchanged

## 产品规则

行政资产的管理权与劳作使用权分离。实际管理势力始终来自该资产当前位置的 `WorldSite`；行动势力只有在以下任一条件成立时可劳作：

1. 行动势力就是实际管理势力；
2. `FactionDiplomacyRelationQuery.GetRelation(world, actingFactionId, managingFactionId)` 返回 `Overlord`，即行动势力是实际管理势力的直接附庸。

许可单向生效：附庸可在宗主的劳动资产上工作，宗主不会因此获得附庸资产的使用权。联盟、中立、兄弟附庸、战争以及已解除的附庸关系均不授权。本轮实际接入资产仅为 `herbField` 与 `grainField`。

## Authority 与接线

严格管理 authority `WorldAdministrativeAssetAuthorizationService` 保持原样。新增 `WorldAdministrativeAssetWorkAuthorizationService`：先解析严格管理结果，只有严格结果为 `ManagedByOtherFaction` 时才查询当前外交关系；结果明确区分 `AllowedAsManager` 与 `AllowedAsVassalWorker`，两者的 `IsAllowed` 均为 true，但 `ManagingFactionId` 始终是真实 Site Owner。

以下农作入口统一消费 Work Authorization：

- `HostFarmFieldLabor.BeginForSelection`：玩家手动开工；
- `HostFarmFieldLabor.IsCellAuthorized`：选格、抵达、完成前持续复核；
- `HostWorkTargetMode.ResolveFarmAuthorization`：Hover、光标与实际执行一致；
- `WorldAdministrativeFarmWorkAreaAuthorizationService`：NPC ActivityResolver 候选；
- `WorldSiteFarmHarvestService`：NPC 日程收获提交。

关系不缓存。解除附庸或进入战争后，下一次逐格检查即停止现有自动农作，并提示“已失去该农田的劳作权限，自动农作停止”。

## 明确保留的隔离

- 玩家手控 Party 收获仍进入 `PartyInventory`；NPC schedule 收获仍进入实际 Managing Site 的 `WorldSitePublicStock`。
- `PlayerStrategicResourceService` 仍只允许玩家自己拥有的 Site 进入仓储网络；宗主公库不计入玩家可访问库存，也不可取出。
- `OutdoorFactionConstructionAuthorizationService` 仍要求 Site Owner 与建造势力完全相同。
- Site ownership、TerritoryClaim、拆除、住房、时间表、ControlCore 与行政管理权限均未放宽。
- 未实现贡赋、产出分成、仓库共享、建造共享、联盟劳作、工资、宗主命令或 NPC AI。

## Ch01 真实开局依据

`base:scenario_ch01_reference` 的 `strategicOpening` 已把 `base:faction_player` 登记为 `base:sect_huangcun_labor` 的直接附庸；荒村 Site 与现有粮田／药田由该宗门实际管理。因此不需要剧情 ID hardcode 或额外 fixture，新游戏会通过正式 `VassalageBoard` 自然取得劳作许可。

## 制作人人工验收（CASE 1～10 对应产品回归）

1. Ch01 New Game，确认玩家仍为压迫宗门直接附庸。
2. 悬停荒村粮田／药田，应显示“宗主领地，附庸可劳作”，并可播种、照料、收获。
3. 玩家手控收获进入随身背包。
4. 宗主领地内“势力仓库”仍灰色，宗主库存不可读、不可取。
5. 宗主实际控制范围仍不能作为玩家自己的农田或 StorageRoom 建造范围。
6. 宗主对附庸田、中立、联盟、兄弟附庸均保持拒绝。
7. 解除附庸后同一宗主农田立即拒绝；正在进行的自动农作停止并显示通用失权提示。
8. 即使脏状态同时保留附庸与战争，外交查询的 War 优先级也使劳作被拒绝。
9. 宗主本势力 NPC 继续正常劳作；玩家势力 NPC 作为直接附庸可由日程选择宗主农田。
10. 附庸 NPC 的日程收获进入宗主实际 Managing Site 公库，不进入附庸仓储。

## 验证记录

- `tools/offline-compile.ps1`：全部程序集 `ALL_OK`；仅保留既有两条测试程序集编译 warning。
- 按仓库 `AGENTS.md`，执行代理不得新增或运行自动测试；CASE 1～10 保留为上述制作人人工验收，不冒充已执行。
- 未启动 Unity；最终静态差异检查以交付报告为准。
- 未 stage、commit 或 push。

## VASSAL-WORK-01-P1：首次新游戏右键农田（2026-09-24）

制作人验收确认附庸劳动授权本身已生效，但首次 New Game 的 Active 选择发生在 Continuous EntityView materialize 之前，`HostSelectionController.SelectEntity` 因 Registry 尚无 Active View 而失败；最终 opening population barrier 过去未补选。于是 Context Gate 把空 selection 合法解释为 Active，农田执行却只遍历显式 selection，首次右键被消费但没有 Worker。

修复保持外交授权不变：

- `FinalizeContinuousOutdoorOpeningPopulation` 在 Refresh/Prune/Spawn 完成后调用统一 helper；仅当 selection 为空且 Active View 已存在时选择 Active，已含 Active 或已有明确 selection 时不覆盖。
- `HostPlayerMoveCommandGate.CollectPartyWorkersOrActive` 统一 party-work actor 语义：合法非空 selection 返回其中 Party members；空 selection 返回可行动 Active；非法非空 selection 不回退 Active。
- `HostFarmFieldLabor.BeginForSelection` 使用该 resolver，保留多人农作；没有合法命令上下文或可执行角色时显示明确反馈。
- Farm Context Target 只有实际 Farm component 缺失时返回 false；合法 target 开工失败时由农作入口显示授权／选择／无活反馈，不再静默消费。
- Snapshot restore 的既有 rebind-selection 流程未改，Snapshot 仍为 v8。

P1 人工验收：完全 New Game 后不点角色、不按 E，直接右键宗主农田；Active 应已有绿色选中环并直接开始农作。随后清空 selection 再右键，应隐式使用 Active；多选 Active＋Follower 应共同农作；非空且不含 Active 的选择不得偷偷回退。

## 2026-09-24 制作人封板

制作人已实际验收直接附庸在宗主粮田／药田劳动、宗主 Storage 与 Construction 继续隔离、解除附庸后失权，以及 New Game 无需先按 E 的首次右键、empty selection fallback Active 和合法多人农作。VASSAL-WORK-01 与 VASSAL-WORK-01-P1 当前均为 **Producer Accepted / Sealed**。P1 的根因是 Host Active/View/Selection 初始化时机，不是 Direct Vassal Work Authorization 失败。
