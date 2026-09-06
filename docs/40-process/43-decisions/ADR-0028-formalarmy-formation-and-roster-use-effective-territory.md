# ADR-0028：FormalArmy 组建与编制管理使用 Friendly Effective Territory Hex

- 状态：**已采纳并实现**
- 日期：2026-09-06
- 决策者：项目负责人
- 关联：[2A](../../20-systems/2A-factions-armies-diplomacy-and-capture.md)、[2K](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[ADR-0026](ADR-0026-rpg-first-playerparty-and-formalarmy-military-layer.md)、[Control Asset Territory V1 SEALED](../200-control-asset-territory-and-faction-flag-v1-sealed-2026-09-06.md)

## Context

`ArmyService.CreateArmy` 与 Create／Add／Remove／ChangeLeader／Disband 的地点 gate 历史上绑定 Friendly `WorldSite`／`SiteId`。Control Asset Territory V1 封板后，这会错误排除由 `FactionFlag` 产生的己方 Effective Territory，也会让 Wilderness 中刚建立的 FormalArmy 无法继续管理编制。

## Decision

1. 玩家运行时组建 FormalArmy 的 location authority 改为 `HexCoord`。全部 selected members 必须由 `CharacterWorldPresenceQuery.TryGetWorldHex` 解析到同一个 Hex；不自动集合、不旅行、不 teleport。
2. Create 以及 Add／Remove／ChangeLeader／Disband 每次操作都实时读取 `TerritoryControlService.GetController(world, hex)`。只有 controller 与 Army faction **精确相等**才允许；联盟、附庸、宗主、中立与敌方均不算。
3. Policy 不读取 WorldSite Owner、FactionFlag Nominal Coverage、TerritoryRegion geometry 或 Control Asset 类型；它只消费 Territory Resolver 的最终 Effective Controller。
4. AtWorldSite 仍可组军；成员全部处于同一 Site context 时，新 Army 保持 `AtWorldSite`。Wilderness／普通 Hex 初始化为 `AtWorldPosition`，优先保留 Leader／首成员合法的 canonical continuous position，否则才使用 Hex center。
5. 组军后成员位置继续由 FormalArmy authority 与 `FormalArmyMemberPresenceSync` 接管。Wilderness Remove／Disband 后，角色留在 Army 当前 WorldPosition／Hex，不转移到最近 WorldSite。
6. **Garrison 不变**：仍要求 Army 实际处于所属势力拥有的 WorldSite；FactionFlag Territory 没有 Garrison facility。Mobilize 与 authored `CreateAuthoredArmy(assemblySiteId)` 不变。
7. Territory 丢失不删除或改变既有 Army，只阻止该地点后续需要 Friendly Territory 的 roster management。

## Supersede 范围

本 ADR 仅 supersede ADR-0026 Decision #4 与 2K／2A 中“组军／解散／编制管理必须位于己方 WorldSite”的地点限制。FormalArmy 的 RPG-first 军事职责、真实 Character membership、同 Faction、PlayerParty／Active 限制、macro-order living、War、Movement、Battle 与 Garrison Site-only 规则全部保持。

## Non-goals

- 自动 rally／远程组军
- 盟友或附庸共享组军权限
- Neutral formation
- Flag garrison
- 组军成本、计时、施工或 AI
- Territory、FactionFlag、WorldSite Capture、Movement 或 Battle 算法改动
