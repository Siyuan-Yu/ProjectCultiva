# Control Asset Territory Model + Faction Flag V1（2026-09-06）

> 本页是初次实现历史记录。后续 FactionFlag 已收正为真实战略建筑并于 2026-09-06 通过人工验收；当前正式封板基线见 [200](200-control-asset-territory-and-faction-flag-v1-sealed-2026-09-06.md)。本页早期“旗杆／直接扣 HP／脚下立旗”描述不再代表正式 Gameplay。

## 目标与边界

在已批准的 RPG-first 架构内落地 Control Asset 领地求解、FactionFlag 内容／Runtime／Snapshot／Host／WorldGraphEditor 闭环。未修改 Freeze、ProjectSettings 或 Packages；旗帜不是 Character；战后不保存自动续拆状态。

## 实现摘要

- Core：`FactionFlagBoard` / `FactionFlagService` / `StrategicTerritoryCoverageResolver`；求解缓存按 `SimulationWorld` 隔离，重建同步 Hex controller 与 WorldSite 有效 Region 索引。
- 战略攻击：`FactionFlagSiegeService` 复用 War、FormalArmy、BattleOffer 与冻结 SupportArea。无守军时才直接扣 HP；归零后移除并重建。
- Data/Snapshot：HexWorld JSON 增加 `controlEstablishedOrder` 与 `factionFlags[]`；Snapshot 保存完整活跃旗，以字段是否存在区分 legacy 旧档和权威空列表。
- Host：Wilderness LocalMap 显示旗杆、势力色旗面、名称与 HP；玩家可立旗或经侵略确认攻击敌旗；WorldMap 绘制势力色标记。
- Editor：共享 DTO/JSON/Validator 支持旗帜与 Order；“控制资产”页可新建／删除旗、编辑 Site Owner/Order，并按 first claim 预览有效覆盖。旧 Territory 笔刷标为 legacy/debug。
- Content：两份正式 HexWorld 已填入稳定 Site Order 及稀疏旗帜样例。

## 自动验证

- WorldGraphEditor Release：0 error。
- Shared.Tests：54/54，新覆盖旗 JSON 往返、创建／删除、正式内容控制资产校验、敌对名义重叠 warning。
- 注：完整 HexWorld validator 仍会报 `ch01_hex_world.json` 既存的 3 个孤立道路 Hex（92,34／120,82／124,82）；与本次 Control Asset 迁移无关，本工单未扩展修复范围。
- Unity Core/Data/Host/EditMode 程序集：使用 Unity Bee 响应文件离线 C# 编译 0 error（既有 warning 保留）。
- 新增 EditMode smoke：早到资产抢先／删除后扩展、Capture 保持 Order、Snapshot 恢复旗状态及防止 authored flag 复活。

## Unity 人工验收

1. 在无主荒野格立旗，确认 LocalMap 旗位于玩家当时位置，WorldMap 出现势力色标记。
2. 尝试在 Site footprint、敌方有效 Anchor、不能新增无主格的位置立旗，确认被拒绝。
3. 对无守军敌旗宣战后连续突击至摧毁，确认较晚资产自然扩展。
4. 在旗 Anchor+一环放置敌方 FormalArmy，确认生成 BattleOffer，旗不在参战者列表，战斗结束后不自动续拆。
5. 存读档检查旗 HP、LocalMap 位置、Order；摧毁 authored flag 后再存读，确认不复活。
