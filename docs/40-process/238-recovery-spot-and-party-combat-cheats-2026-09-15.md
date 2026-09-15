# 238｜恢复处与玩家队伍战斗数值作弊（2026-09-15）

## 目标与范围

本轮关闭两个制作验收缺口：提供可建造且可持久化的 Continuous Outdoor 恢复处；在 LevelTester 战斗页提供只作用于当前 PlayerParty 的数值按钮。没有扩展医疗、受伤、床位、收费、NPC 恢复 AI、Site Economy 或普通建筑拆除。

## 正式 Content

- Building：`base:building_recovery_spot`，显示名“恢复处”，2×2，粗木 5，`placementKind/outdoorKind=recoverySpot`，不创建 WorldSite。
- 荒村 authored placements：`base:site_huangcun:recovery_spot_1/2/3`，分别位于制作人指定的 source grid `(38,44)`、`(43,44)`、`(62,44)`；baked world metric 保持原值。
- MapKind：Cushion、SingleCentered、Recovery interact、非 plantable、非 farm，fallback 为青色。
- Loader/validator：恢复处必须有正 footprint，严格限定为 2×2、`createsWorldSite=false`、`outdoorKind=recoverySpot`；旧未知 placement kind 仍报错。

## 建造与空间权限

`OutdoorConstructedAssetBoard` 现在支持 grainField 与 recoverySpot，共用一个 `NextOutdoorConstructedAssetSequence`；旧农田 ID 保持 `asset:runtime:farm:*`，恢复处使用 `asset:runtime:recovery:*`。所有资产通过 `EnumerateGridCells` 表达物理 footprint；只有行政资产通过 `AdministrativeCellAnchors` 产生锚点。

恢复处建造链为：建筑页 → Recovery presenter → Surface 网格吸附与已加载/可通行/12 presentation units/世界对象预检 → `ConstructionService.TryConstructRecoverySpot` → 全 footprint Actual Control 重验 → 扣粗木 5 → 注册稳定资产 → 当前 loaded chunks 立即刷新。整个 footprint 可跨同势力 Site 边界；无主和敌方管理格拒绝。恢复处不产生管理者、Owner 或农田 cell state。

## 恢复交互与数值 authority

左键恢复处进入统一世界对象检视栏，显示说明、30 分钟耗时与战斗可用状态。右键显示“查看详情 / 休息恢复”；不进入 WorkTargetMode。角色未接近时先走到恢复处中心，抵达后再次核验目标、角色与战斗状态，再下发 `PlayerCommandKind.Recover`。

`RecoveryAction.DefaultDurationTicks=6`。只有完整完成才调用 `CombatRecoveryService.RestoreToMaximum`：上限读取 `AttributesComponent` 的最终 `MaxHp/SpiritPower`，当前池写入 `CombatVitalsComponent.CurrentHp/CurrentSpiritPower`，并保持 lifecycle 不变。取消、Stop 或新移动在完成前不会改变资源池；已满、弥留、死亡、Removed、CharacterEncounter Active 或战略时钟冻结均拒绝。普通 `RestAction` 仍只消耗时间，突破仍只提高上限并 clamp，不自动填当前池。

## 持久化

运行时恢复处继续使用 `OutdoorConstructedAssetSnapshotDto`，不新增 schema 或 Recovery 专属 DTO。序列恢复同时接受 farm/recovery 前缀。活动恢复通过 `ActiveActionSnapshotDto.Kind=Recover` 与 `TargetRef=RecoverySpotId` 保存总时长、剩余时长和状态；读取后恢复同一 `RecoveryAction`。

## LevelTester

战斗页顶部新增：

- 我方全员攻击 +10；
- 我方全员最大生命 +50 并回满；
- 我方全员生命/灵力回满。

目标集合只取 `bootstrap.Session.PlayerParty.Members`。数值增长做 int 饱和保护；缺组件或非存活成员跳过，回满复用恢复规则且不复活。面板显示成功/跳过数量；敌方、居民、盟友和 FormalArmy 不参与。

## 验证记录

- Strict Content：通过定向 Content load 测试验证 Building 与荒村三个 placement。
- 定向离线测试：恢复完成/取消、不可用生命周期、上限增加后再恢复、Actual Control 与同势力边界、运行时资产和 3/6 action snapshot、PlayerParty 与敌人隔离。
- 离线编译覆盖 Core、Data、Unity Host、Unity Editor、Tests、PlayModeTests、Assembly-CSharp、Assembly-CSharp-Editor。
- 未运行 Unity Test Runner、PlayMode suite、batchmode、Bake 或 Unity 人工验收。

## 制作人验收建议

新游戏在荒村分别左键、右键任一恢复处，受伤/耗灵后选择“休息恢复”，确认角色先接近、显示恢复中并在 30 分钟后回到当前上限。再从建筑页花粗木 5 放置恢复处，立即交互并 Save/Load；最后在 LevelTester 战斗页验证三按钮只改变当前队伍，敌人和死亡成员不被恢复。
