# Phase 5S-B2-1 — 战斗 LocalMap 解析契约

- 状态：已新增 Core 契约；未接入运行时，未运行 Unity，未提交。

`BattleLocalMapLocation` 携带语义类别以及 SiteId、BattleHex 或 ExplicitLocalMapId。`BattleLocalMapResolver.Resolve(world, location)` 无副作用地返回成功状态、LocalMapId、类别、SiteId、BattleHex 和失败诊断。

- `WorldSite`：SiteId → `WorldSite.LocalMapId`；缺少 Site／空地图明确失败，绝不默认回退。
- `Wilderness`：BattleHex → `WildernessLocalMapFallback.TryResolve`；失败绝不默认回退。
- `ExplicitEncounterMap`：只有该类别可消费显式地图标识；这是遗留／特殊遭遇的兼容边界。

`DefaultEncounterLocalMapId` 使用者分类：`BattleOfferService`、`HostStrategicInterruptPresenter`、`PlayableHostBootstrap`、`LocalMapVisibility`、`BattleParticipantSnapshot`、残留战场／结算／生成器在 B2-2 按遭遇来源分类前均保留为遗留兼容。`BattleOfferService` 和 `HostStrategicInterruptPresenter` 的未来普通世界战斗入口属于 B2-2 迁移对象。目录常量本身属于其它兼容用途。

普通 `WORLD_COMBAT` 只有 PlayerParty／当前主控实际是参与者时才允许手动进入；远处 FormalArmy 战斗保持后台自动战斗。普通手动世界战斗在同一真实 LocalMap 结束，无结算／弹窗／返回。本规则不改变 B2-1 仅实现 resolver 契约的范围。
