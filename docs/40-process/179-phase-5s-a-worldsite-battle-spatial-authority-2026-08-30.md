# Phase 5S-A — WorldSite 战斗区域与支援环权威

- 状态：已新增 Core 空间权威和 EditMode 测试；未运行 Unity，未提交。

`BattleArea` 严格等于 `WorldSite.OccupiedHexes`。`SupportRing1` 是每个区域格六方向 `HexMath.Neighbor` 候选的并集，去除区域自身并去重。可选的 `HexWorld` 过滤只移除不存在／越界 Hex；Water/Mountain 等地形仍保留在空间环内。

AnchorHex、PresenceHex、DisplayName 和 SiteType 都不影响两个集合。任何 BattleArea Hex 都归属同一 SiteId，因此未来对应同一个 Site Battle Context。

已新增 `WorldSiteBattleSpatialPolicy` 和四个几何测试。未接入战斗、单位、势力、旅行、暂停或表现。未来接线点是 FormalArmy 战略 Hex 与 PlayerParty Canonical→DerivedSurfaceHex；本阶段尚未接入 CombatContext。
