# Phase 5S-A.1 — 现有战斗／多 Hex 兼容性审计

- 状态：仅审计；未修改运行时代码，未运行 Unity，未提交。

## 功能矩阵

| 能力 | 现状 |
|---|---|
| PlayerParty 主动攻击 Character | 已实现：`MeleeCombatService` / `CombatLifeStateService` |
| PlayerParty 攻击 FormalArmy / WorldSite | 未实现：未发现正式目标入口 |
| FormalArmy 攻击 WorldSite | 部分实现：接战／战略状态存在，未见完整 Site 战斗结算入口 |
| FormalArmy vs FormalArmy | 部分实现：`BattleEngagement*` 接战与参与者收集存在 |
| Battle Context | `BattleEngagement` / `BattleEngagementSupportArea` 体系 |
| 支援者收集 | 已实现（FormalArmy）：按冻结 SupportArea 与阵营／参与资格筛选 |
| 手动/后台战斗 | 部分实现：Encounter/Combat 与 WorldTravel 接线存在，完整 Site 战斗流程未闭合 |
| 胜负回写 / Capture | Character lifecycle Capture 已实现；WorldSite ownership capture 未见完整战后入口 |

## 现有支援空间权威

`BattleEngagementSupportArea.ResolveAndFreeze` 已存在：FormalArmy 防守者在 `AtWorldSite` 且 presence 位于 footprint 时，BattleArea 使用 `site.EnumerateFootprintHexes()`；否则使用单一 presence hex。SupportArea 由每个 BattleArea hex 的 `HexMath.Neighbor` union 构造，但当前 `Contains` 集合包含 BattleArea 自身，且未按 `HexWorld.Contains` 过滤边界。

因此“WorldSite 周围一格支援”已有实现，但只在 FormalArmy 接战路径，存在两个 multi-hex 兼容差异：非 Site 防守者仍是单格；SupportArea 语义包含 BattleArea；边界不做 world-valid filtering。

## 单格假设与位置

- `PresentationAnchorHex` 仅用于展示/战斗锚点，未作为多格 BattleArea authority。
- `BattleEngagementSpatialQuery` / `ArmyHexBattleAnchorService` 提供 FormalArmy committed/derived hex 查询；需继续审计调用者是否把 anchor 当战场。
- PlayerParty 战略位置应使用 Canonical→DerivedSurfaceHex；当前接战 debug/query 存在 `TryGetCommittedPartyHex`，尚未发现 Site footprint 接战入口。
- `WorldSite` 目标身份在现有旅行/ownership 模型中以 `SiteId` 为主；本轮未发现完整 WorldSite BattleContext 创建器。

## 候选策略处置

`WorldSiteBattleSpatialPolicy` 与 `BattleEngagementSupportArea` 存在部分重复。不要形成双真源：建议下一轮将新 Policy 保留为纯几何候选，先以测试明确“SupportRing 不含 BattleArea、可选 world filtering”的目标契约，再评估把现有接战 area 构造委托给它；本轮不接线、不删除旧 authority。

## 最小后续范围

1. 明确现有 `BattleEngagementSupportArea` 的包含语义（Area 与 Ring 是否分离）。
2. 为 FormalArmy Site 接战补 whole-footprint + world-boundary 测试。
3. 仅在确认契约后合并几何 authority；再调查 PlayerParty Site 战斗入口与 SiteId-first Capture。
