# Control Asset Territory + Faction Flag V1 — SEALED

> 状态：**Implemented · Manual acceptance passed · V1 sealed**  
> 日期：2026-09-06  
> 适用：RPG-first Control Asset Territory、FactionFlag V1、FactionFlag Strategic Building Interaction、WorldGraphEditor Control Asset Authoring、WorldMap Territory / Army Layer Toggles  
> 规则真源：[2A](../20-systems/2A-factions-armies-diplomacy-and-capture.md)／[2J](../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)；实现记录：[199](199-control-asset-territory-and-faction-flag-v1-2026-09-06.md)

## 1. V1 Authority

- 政治因果只来自有 Owner 的 Fixed `WorldSite` 与存活的 `FactionFlag`；二者是正式 Control Asset。
- `WorldSite.OwnerFactionId`、`FactionFlag.FactionId` 与稳定 `EstablishedOrder` 是求解输入。
- `StrategicTerritoryCoverageResolver` 按 EstablishedOrder 从早到晚 first claim；Nominal Coverage 可重叠，Later 不覆盖 Earlier。
- `HexCell.ControlFactionId` 与 `TerritoryRegion` 是可重建的 derived / compatibility projection，不是第二政治真源；旧 per-Hex Territory authoring 仅保留 legacy/debug 用途。
- WorldSite Capture 与 Flag 摧毁之后统一重建；WorldSite 易主不改变 EstablishedOrder，Earlier Flag 移除后 Later Asset 可自然补入释放范围。

## 2. Geometry 与军事范围

- WorldSite Territory Nominal Coverage：完整 footprint + 外围一圈。
- FactionFlag Territory Nominal Coverage：anchor + 完整一圈。
- WorldSite Battle SupportArea：直接从 WorldSite footprint + 外围一圈冻结。
- FactionFlag Battle SupportArea：直接从 Flag anchor + 完整一圈冻结。
- 即使当前数值几何相同，军事 SupportArea 也不得从 Effective Territory 或 TerritoryRegion projection 反推。

## 3. User-facing Behavior

- WorldSite：攻击议政厅；有守军先进入 BattleOffer，无守军按建筑近战；破门并 Occupy 后 Owner 易主，议政厅立即恢复满耐久，可反复 A→B→A 占领。
- FactionFlag：LocalMap 中是带真实 prefab、4×4 footprint、WalkGrid 阻挡、HP 与右键菜单的野外控制建筑，不是 Character。
- 立旗：在合法 Wilderness LocalMap 进入 placement mode，鼠标选择合法建筑中心；V1 无资源、时间、工人或官职成本。Domain gate 继续要求合法 Anchor、至少新增有效无主控制，并允许 Nominal overlap。
- 攻旗：右键敌方建筑 → StrategicMilitaryAggression / War → `FactionFlagSiegeService`。范围内有 defender-side FormalArmy 时建立 BattleOffer；Flag 不进入 Character participant snapshot，也不阻塞 Tactical Battle End。
- 无守军时角色自动接近，按正常 melee interval、角色 Attack 与建筑 Defense 逐击造成伤害并播放 Strike VFX；不存在固定 GUI `-25 HP` 路径。
- Flag HP 归零：移除 Control Source、重建 Territory、建筑消失并安全重建基础 WalkGrid + 动态阻挡；不 Occupy、不自动归攻击方、不生成尸体或废墟。
- 战斗结束不会自动续拆，玩家必须重新右键仍存在的建筑。

## 4. SaveLoad

- Snapshot 保存完整 active Flag set：ID、Faction、Anchor、EstablishedOrder、HP 与 LocalPosition。
- Legacy Save 缺少 Flag snapshot authority 时才回退 Content authored Flags。
- New Save 只要存在 authority，`flags=[]` 就表示当前世界确实没有 Flag；不得按 Count==0 恢复 Content。
- 被摧毁的 authored Flag 存读档后不复活；玩家创建的 Flag 全字段保持。
- `LocalX/LocalZ` 的正式语义是 FactionFlag 4×4 控制建筑中心。

## 5. WorldGraphEditor

- 正式主流程是 author Control Asset：WorldSite Owner / Footprint / EstablishedOrder 与 FactionFlag Faction / Anchor / EstablishedOrder / 可选 LocalPosition。
- FlagId 与 EstablishedOrder 正常流程自动生成；自定义字段保留为高级入口。
- Duplicate FlagId 在创建前阻止，Validator 按 ID 聚合并报告所有 Anchor；不可通行 Anchor 报告 FlagId、坐标、Terrain 与 passability。
- 显式 LocalPosition 按完整 4×4 footprint、WalkGrid bounds、静态 blocker、Surface Exit 与 approach side 校验。
- Nominal overlap 合法；跨势力 overlap 是 warning，不阻止保存。旧 Territory brush 仅为 legacy/debug/preview。

## 6. WorldMap Presentation Filters

- “显示势力范围”使用真正 Checkbox：同时控制 Territory borders 与 FactionFlag WorldMap markers；不隐藏 WorldSite 或 LocalMap Flag building。
- “显示军队”默认 ON：OFF 时隐藏 FormalArmy、ArmyStack、residual 与其它战略头像，只保留 PlayerParty 主控；同时清 HitRects、军队选择/菜单与 Army path preview，并把选择权威切回 PlayerParty。
- 两个开关完全独立，只影响 Presentation/Input；不影响 Territory、Army Tick、Travel、War、Siege 或 Battle。
- 本 Session 内关闭/重开 WorldMap 保持选择；V1 不写 SaveGame Settings。

## 7. Static Seal Audit

- 正常 Gameplay 未发现 Pole/Cloth UnitSprite 旗杆、固定右下角攻击按钮、`AssaultDamage=25` 或按钮点击直接扣血；轻量 HUD 只负责进入 placement mode。
- 新政治 Gameplay 未发现绕过 Resolver 的逐 Hex mutation。现存直接写入属于 Resolver 派生写，或 Content/Snapshot legacy shell restore；后者随后统一执行 Resolver rebuild。
- TerritoryRegion 保持 derived / compatibility projection；Flag placement/assault 与 WorldSite Capture 均不以 Region controller 作为独立政治因果。
- WorldSite / Flag SupportArea 均读取原始 Control Asset geometry，不读取 Effective Territory。
- `HasFactionFlagSnapshotAuthority` 区分 legacy 缺字段与权威空数组，`flags=[]` 行为正确。

## 8. Future / Deferred（不属于 V1）

- **ControlCore Recovery V2**：占领后残破、随时间恢复、资源维修。
- **Flag Art / Structure Style**：不同势力的控制建筑风格。
- **Flag Construction**：资源成本、建造时间、施工过程。
- **Strategic AI**：AI 立旗、拔旗与扩张。
- **Territory Economy**：税收、资源、人口、补给、行政容量。
- **Advanced Territory**：只有未来明确需要时才单独设计 influence、pressure、contested 等机制。

## 9. Seal Validation

- `git diff --check`：通过，仅报告工作树既有 CRLF 转换提示。
- `Shared.Tests`：59/59 通过。
- `WorldGraphEditor` Release：0 warning / 0 error。
- Unity Core / Data / Host：使用现有 Unity Bee response files 离线编译，0 error；仅保留既有 obsolete / unreachable / unused-field warnings。
- 本轮没有修改 Gameplay 行为，无需再次人工验收。

## 10. Seal Rule

除明确 Bug / Regression 外，后续系统必须建立在本页 V1 baseline 上，不得改变 EstablishedOrder / first-claim、恢复 per-Hex painting authority、把 Flag 变 Character 或 UI 直扣 HP、让 Capture 重排 Order、从 Effective Territory 推导 SupportArea，或让已摧毁 authored Flag 在 Load 时复活。
