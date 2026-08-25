# 架构决策记录（ADR）索引

> 状态：现行 | 最后更新：2026-08-25  
> 上级：[`00-overview.md`](../../00-project/00-overview.md)、[`33` 冻结 v0.2](../../30-tech/33-architecture-core-rules-freeze-v0.2.md)  
> 模板：[`adr-template.md`](../../90-templates/adr-template.md)  
> **编号 0009 预留正式 UI 方案。**

## 怎么用

- 每条 ADR 只记录**一次已拍板决策**及其背景／影响。  
- 日常通读：先扫本表，再点开相关条目。  
- 与 `33` 冲突时：以较新的冻结版＋对应 ADR 为准，并回头修订旧文。

## 决策一览

| 编号 | 标题 | 状态 | 要点 |
|---|---|---|---|
| [0001](ADR-0001-unity-version.md) | Unity 版本与渲染管线 | 已采纳 | 2022.3.6f1 Built-in |
| [0002](ADR-0002-no-unity-ecs.md) | 不采用 Unity ECS | 已采纳 | 普通 C# 组合模型 |
| [0003](ADR-0003-dual-time-model.md) | 双层时间模型 | 已采纳 | 见 v0.2 澄清 ADR-0018 |
| [0004](ADR-0004-csv-json-data-source.md) | CSV／JSON 配置真源 | 已采纳 | SO 仅缓存 |
| [0005](ADR-0005-snapshot-save.md) | 快照存档 | 已采纳 | 不做完整回放 |
| [0006](ADR-0006-layered-maps-and-routes.md) | 分层地图与 Route | 已采纳 | 见 v0.2 澄清 ADR-0021 |
| [0007](ADR-0007-multi-party-lod-simulation.md) | 多队伍分级模拟 | 已采纳 | M1 不做跨 Region 离屏 |
| [0008](ADR-0008-army-group-aggregate.md) | ArmyGroup 聚合 | 已采纳 | 非 Core 第一阶段重点 |
| 0009 | （预留）正式 UI | 预留 | — |
| [0010](ADR-0010-permanent-death-default.md) | 默认永久死亡 | 已采纳 | 剧情重要≠不死 |
| [0011](ADR-0011-player-agency.md) | PlayerAgency | 已采纳 | Focus + 动态领导权 |
| [0012](ADR-0012-faction-control-separation.md) | 势力四权分离 | 已采纳 | 归属／职位／关系／控制 |
| [0013](ADR-0013-mod-ready-phased.md) | Mod Ready 分阶段 | 已采纳 | 当前不做 Mods/ 加载 |
| [0014](ADR-0014-unified-content-package.md) | 统一 ContentPackage | 已采纳 | 官方与 Mod 同管线 |
| [0015](ADR-0015-namespaced-definition-id.md) | DefinitionId 命名空间 | 已采纳 | `namespace:local_id` |
| [0016](ADR-0016-no-arbitrary-script-mods.md) | 禁止任意脚本 Mod | 已采纳 | 初期 |
| [0017](ADR-0017-relationship-ledger-source-of-truth.md) | RelationshipLedger 唯一真源 | 已采纳 | **Freeze v0.2** |
| [0018](ADR-0018-worldtick-actionclock-duties.md) | WorldTick／ActionClock 职责 | 已采纳 | **Freeze v0.2** |
| [0019](ADR-0019-dead-vs-removed.md) | Dead ≠ Removed | 已采纳 | **Freeze v0.2** |
| [0020](ADR-0020-focus-vs-control-authority.md) | Focus 与控制权分离 | 已采纳 | **Freeze v0.2** |
| [0021](ADR-0021-world-region-localmap.md) | World／Region／LocalMap | 已采纳 | **Freeze v0.2** |
| [0022](ADR-0022-core-milestone-1-scope.md) | Core Milestone 1 范围 | 已采纳 | **Freeze v0.2** |
| [0023](ADR-0023-manual-encounter-freezes-worldtick.md) | Manual Encounter 冻结 WorldTick | 已采纳 | 全战式 Modal；补充 0018 |
| [0024](ADR-0024-real-cultivators-and-army-strategic-model.md) | 修士真实 Character + Army | 已采纳／**部分 superseded** | 「跨点必须 Army」→ [0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)；真实成员／LOD 仍有效 |
| [0025](ADR-0025-strategic-spatial-model-hexgrid.md) | 战略空间 = HexGrid | 已采纳 | **SUPERSEDED** Route 正式移动；见 [155](../155-hex-strategic-worldmap-migration-2026-08-23.md) · [158](../158-hex-world-content-authoring-pipeline-2026-08-23.md) |
| [0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) | RPG-First：单 Active／PlayerParty／连续 Hex／Army 军事层 | 已采纳 | 真源 [2K](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)；迁移 [163](../163-rpg-first-architecture-audit-and-migration-plan-2026-08-25.md) |

战略接战时间纪律另见 **[ADR-0023](ADR-0023-manual-encounter-freezes-worldtick.md)**（2026-08-21）。

RPG-First 控制模型另见 **[ADR-0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)** + **[2K](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)**（2026-08-25）。

战略势力／Army 军事规则另见 **[ADR-0024](ADR-0024-real-cultivators-and-army-strategic-model.md)** + **[2A](../../20-systems/2A-factions-armies-diplomacy-and-capture.md)**（跨点必须 Army 已 supersede）。

战略 Hex 空间与 Content Authoring 另见 **[ADR-0025](ADR-0025-strategic-spatial-model-hexgrid.md)** + **[155](../155-hex-strategic-worldmap-migration-2026-08-23.md)** + **[158](../158-hex-world-content-authoring-pipeline-2026-08-23.md)**（2026-08-23）。
