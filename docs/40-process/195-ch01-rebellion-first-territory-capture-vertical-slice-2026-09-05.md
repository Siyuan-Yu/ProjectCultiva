# 195 · 第一章起事与首次领地占领纵切（2026-09-05）

> 状态：实现完成，待 Unity 人工验收
> 范围：把既有附庸、战争、LocalMap 战斗、ControlCore、CaptureObjective、WorldSite Territory 与战略快照连接为第一章最小可玩闭环。

## 目标闭环

玩家在青石荒村满足炼气门槛后，主动选择「起事／反抗宗门」。正式领域流程为：

`解除附庸` → `建立玩家势力与压迫宗门的战争` → `合法攻击属敌军人／主管府` → `CaptureObjective` → `WorldSiteTerritoryTransferService.Transfer`。

占领 `base:site_huangcun` 后，Site Owner、关联 TerritoryRegion Controller 与 Region 全部 Hex Controller 必须由同一事务同步为玩家势力；`Ch01ScenarioProgressionHooks` 继续负责写入政治成立进度标记。

## 边界与权威

| 事项 | 正式权威 | 本轮处理 |
|---|---|---|
| 附庸解除 | `VassalageBoard` | 增加最小、通用的解除原语；Host／Scenario 不直接修改内部字典。 |
| 第一章起事资格与顺序 | `Ch01RebellionService` | 只处理第一章起事；不演化为通用外交服务。 |
| 战争 | `WarGateService` / WarBoard | 起事成功后调用既有宣战服务；不以 Diplomacy cache 作为真源。 |
| NPC 敌对与战斗 | `FactionMembershipComponent`、`LocalHostileActionRoutingService`、现有战斗入口 | 不写主管或守卫的剧情特判。 |
| 主管府攻击资格 | `CaptureObjectiveService.TryBeginMilitaryAssault` | 在实际第一击前由 `ControlCoreService` 兜底检查战争。 |
| 据点易主 | `WorldSiteTerritoryTransferService` | 保持既有唯一 Site＋Region＋Hex 事务。 |
| 读档后的主管府状态 | 已恢复的 Site Owner / CaptureObjective | 重注册 ControlCore 时从正式政治领地状态最小回填，避免已占领据点重生为敌方目标。 |

## 明确不做

- 通用外交界面、议和、联盟、外交 AI、谈判与贡赋；
- 新战斗系统、新 Capture 系统或 Territory 写入路径；
- Dynamic Bandit、敌方反攻、完整第一章剧情导演；
- Snapshot schema 大改；政治领地仍以既有战略快照为真源。

## 人工验收重点

1. 开局玩家势力仍为压迫宗门附庸，且荒村仍属压迫宗门。
2. 至少一位 PlayerParty 成员进入炼气后，在荒村主管府菜单确认起事。
3. 附庸关系消失，双方进入战争；主管与守卫经现有势力／战争路由成为合法目标。
4. 未起事时主管府不能损失耐久；起事后可正常破门并站立占领。
5. 占领后 WorldMap Site Owner、领土边界与 Region／Hex 控制权立即更新。
6. 保存、读取后战争、非附庸状态、荒村归属与已占领的主管府状态保持一致。

## 最小自动保障

- `Ch01RebellionServiceTests.TryBegin_ReleasesVassalageAndDeclaresWar`：验证起事成功后玩家势力不再是附庸，且 WarBoard 中存在玩家势力与压迫宗门的有效战争。
- 本轮不新增大规模 Unity 测试矩阵；Unity 编译和上述测试的实际执行留待具备 Unity Editor 的人工验收环境。
