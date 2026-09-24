# 架构决策记录（ADR）索引

## SOCIAL-QUEST-01 最终封板（2026-09-25）

**Producer Accepted / Sealed**。制作人已验收主流程和最终不可控、不可手动停止跟随 P1。

Temporary quest companion participates in party travel and combat, but is not a player-controllable character.
临时同行跟随受控队伍、随队进入 Separate Space、通过 NPC AI 参战并占用容量；可以选中查看，不能成为 ActiveCharacter、不能接受玩家手动战斗命令或普通 Stop Follow。可控性从永久 Character roster / 既有玩家势力管理 authority 派生，首先排除 QuestCompanion binding；UI、手动命令后端、自动 Active 候选和恢复共用判定。成员、空间和自动战斗 authority 保留。

[ADR-0041](ADR-0041-secret-realm-quest-companion-and-snapshot-v11.md)：Producer Accepted / Sealed。


> 最新：[ADR-0039](ADR-0039-external-faction-control-handoff.md) — PlayerParty 外部势力控制转移；区分全员弥留的 Emergency Takeover 与真正死亡 Succession。
> 正式封板（2026-09-22）：废弃运行入口清理、Hex／Army 命名与兼容身份、WorldSite／Hex footprint 命名尾项均已完成；制作人确认此前运行行为人工验收通过，后续限定同体改名与说明收尾经静态复核通过。C# compatibility 入口已统一为 `Legacy*`，外部 Content／Snapshot wire 名保持稳定。
> 地图／工具方向：[ADR-0036](ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)／[ADR-0037](ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md)；MAP-01～04 已实现、验收并封板，Future 扩展仍不视为已实现。

> 状态：现行 | 最后更新：2026-09-24
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
| [0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md) | RPG-First：单 Active／PlayerParty／连续 Hex／Army 军事层 | 已采纳／**空间与 Army 部分 superseded** | 单 Active／PlayerParty 仍有效；当前 Surface／Squad authority 见 0035／0038 与 [2K](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) |
| [0027](ADR-0027-canonical-world-surface-position-and-worldsite-spatial-mapping.md) | Canonical World Surface Position 与 WorldSite Spatial Mapping | 已采纳 | 当前 WorldPosition / LocalMap mapping 真源；也是 Future Surface bridge |
| [0028](ADR-0028-formalarmy-formation-and-roster-use-effective-territory.md) | FormalArmy Formation 与 Roster 的 Effective Territory | **SUPERSEDED** | FormalArmy runtime 已退休；当前 Squad／控制边界见 0035／0038 |
| [0029](ADR-0029-construction-content-runtime-and-snapshot-boundary.md) | Construction Content／Runtime／Snapshot 边界 | 已采纳 | BuildingDefinition 独立于 Item；Catalog 是静态壳；结果复用 Flag + Inventory Snapshot |
| [0030](ADR-0030-social-bond-attitude-and-snapshot-boundary.md) | Social Bond、五维态度与 Snapshot 边界 | 已采纳 | Bond 与主观态度分离；Ledger 仍是态度真源；v6 软兼容 |
| [0031](ADR-0031-continuous-outdoor-world-surface-architecture.md) | Continuous Outdoor World Surface Architecture | 已采纳；正常 Surface 主线已封板 | 普通 Outdoor 物理连续；Future 制作能力另列 |
| [0032](ADR-0032-sitecore-administrative-and-construction-range.md) | SiteCore、实际行政控制与建设范围 | 已采纳；当前主线已实现／验收 | 一个 Site 一个核心；允许重叠；既有控制保留；拆旗不删资产 |
| [0033](ADR-0033-source-faithful-independent-encounter-and-world-anchor-return.md) | 同源独立遭遇、停表与战前锚点回归 | 已采纳；CharacterEncounter 主线已封板 | 当前地形建筑；各回战前位置；真实战果保留；部分替代 0023 |
| [0034](ADR-0034-conflict-control-succession-and-airship-role.md) | 冲突、控制继承与飞舟职责 | 已采纳；当前冲突／接替边界已实现，飞舟仍 Future | 人物攻击≠宣战；建筑战争确认；顺序接替／最强继承；飞舟只运输 |
| [0035](ADR-0035-unified-squads-and-encounter-scope.md) | 统一小队与固定范围独立遭遇 | 已采纳；当前主线已封板 | 小队唯一组织；独立遭遇范围与入场规则 |
| [0036](ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) | 连续世界制作与去 Hex 产品方向 | **MAP-01～04 Producer Accepted / Sealed** | Final Surface、Composer/Fine Editor、WorldMap LOD、正常 Gameplay Surface authority；Future 自动化能力仍未实现 |
| [0037](ADR-0037-external-content-authoring-toolchain-and-legacy-map-content-migration-direction.md) | External Content Authoring 工具链与旧地图 Content 迁移方向 | **MAP-01～04 Producer Accepted / Sealed** | Authoring Source ≠ Runtime Content；旧 Outdoor Content／Editor 已按 MAP 分期退休 |
| [0038](ADR-0038-continuous-world-legacy-migration-final-seal.md) | Continuous World Legacy Migration Final Seal | **已采纳；Implementation Complete / Producer Accepted / Sealed** | 冻结 Surface／Squad／CharacterEncounter／Actual Control authority；旧 schema／DTO／adapter 仅限明确兼容边界；废弃入口及 Hex／Army／WorldSite 命名边界专项已封板 |
| [0039](ADR-0039-external-faction-control-handoff.md) | PlayerParty 外部势力控制转移 | **已采纳；Producer Accepted / Sealed（2026-09-24）** | 全员弥留可由外部势力人物紧急接管；生者保留为 Recovery Squad；真正死亡继承语义保持；Committed 仅保留战报 |

战略接战时间纪律另见 **[ADR-0023](ADR-0023-manual-encounter-freezes-worldtick.md)**（2026-08-21）。

RPG-First 控制模型另见 **[ADR-0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)** + **[2K](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)**（2026-08-25）。

战略势力／Army 军事规则另见 **[ADR-0024](ADR-0024-real-cultivators-and-army-strategic-model.md)** + **[2A](../../20-systems/2A-factions-armies-diplomacy-and-capture.md)**（跨点必须 Army 已 supersede）。

战略 Hex 空间与 Content Authoring 另见 **[ADR-0025](ADR-0025-strategic-spatial-model-hexgrid.md)** + **[155](../155-hex-strategic-worldmap-migration-2026-08-23.md)** + **[158](../158-hex-world-content-authoring-pipeline-2026-08-23.md)**（2026-08-23）。
