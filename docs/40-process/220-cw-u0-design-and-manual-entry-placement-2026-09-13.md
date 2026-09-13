# CW-U0 统一小队与遭遇设计／多人落点修复交接

> 日期：2026-09-13
> **CW-U0 Producer Accepted / Sealed**（制作人 2026-09-13 确认“验收了，没问题”）。封板范围仅含本页的设计收口与多人入场落点修复；统一小队实现见 [221](221-cw-u1-unified-squad-runtime-and-persistence-2026-09-13.md)。
> 主决策：[ADR-0035](43-decisions/ADR-0035-unified-squads-and-encounter-scope.md)。

## 状态和唯一阅读路径

CW-01、[CW-02](218-cw-02-pause-ownership-and-party-incapacitation-safety-exit-2026-09-12.md) 已记录的制作人验收范围保留。CW-03 制作人反馈主体“差不多验收完成”，仅按该确认范围记录，不虚构完整 A～E 签收或测试日志。

最新设计为 **Design Confirmed / Implementation Pending / Existing Compatibility / Producer Acceptance Pending**。现有 Continuous 原地手动战斗仍为过渡。读取 ADR-0035 → [2K](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md) 统一组织 → [23](../20-systems/23-combat.md) 范围／初始／候选 → [路线](41-roadmap.md)。旧 ADR-0026／0033／0034 已有准确 partial supersession。统一小队、独立战场、有限关系介入和地图攻击退役本轮没有实现。

## 真实缺陷与修复

`ContinuousOutdoorSurfaceRuntime.TryResolvePreparedPointNear` 原来先调用会修改 used 的 `TryAcceptPreparedPoint`，再检验 raw→candidate 连通；拒绝候选已污染占用集合。另有 raw blocked 时从 blocked 起点检查整段，导致附近合法候选持续失败。原 `TryAcceptPreparedPoint` 还隐式调用 nearest-walkable，会把候选和参考修正混在一起。

现在候选检查严格按 loaded Surface、网格可走、未占用、合法参考点直线连通完成，最后一条才写入本次 used。不隐式修正候选。优先保留当前 World 绑定／当前 Continuous materialized 的个人 View 位置（无 View 时用该 scope 的 override），再由 mapper 转换。旧 Army anchor gate 移到个人信息之后，群体锚点不再覆盖本场实际个人接战锚点。

blocked 原点仅允许检查相邻一格合法参考点，且合法邻点须可确认处于同一连通侧；多侧不连通明确失败，绝不任意选择墙／河另一边。取得可信合法参考后在固定六圈、八方向范围内稳定选点；重复占用和不连通候选不留下 used。没有个人近场证据的 Army 成员明确失败，不把群体位置伪装成每个人位置。

准备仍使用局部 plan 和全新 used；任何必要成员失败不发布 plan、不改 World、Offer、战争、伤亡或库存。当前实际名单完整保留，未删 Optional／Enemy，未扩大加载、改 content 或合成人物。成功仍进入原 CommitPreparedManualCombat → 战斗 → 唯一结算／报告链。

失败通过既有 `[ContinuousManualBattleEntry]` 一条汇总记录 Offer、阶段、姓名／EntityId、PlayerParty／旧 Army、位置来源、Surface／世界点、loaded、raw／reference 与具体失败原因。无数据明确 unknown，避免用假坐标充数。原冻结 BattleAnchorHex 与实际目标不一致仍是显式兼容拒绝，不放开门禁迁移旧资格。

## 仍延期的兼容问题

旧 Hex support 可能把无可信个人近场位置、范围外的人加入必要名单。这不是本轮候选占位算法错误；日志标记旧支援待 CW-U2B。不能通过瞬移、漏人、扩大 Surface 或使用陈旧 override 通过。范围内第三方关系候选将于 CW-U3 实现，并持久化判定／追加事实。

## 制作人最短人工路线（本轮未执行）

1. `Assets/Scenes/LevelTester.unity` 正常 New Game，沿现有 Ch01 流程带上同伴甲／乙（现有跟随／入队入口），在大地图查看 `荒村山匪`（`army:formal_bandit_patrol_1`，BanditLeader／A／B／C 四人）。该测试队的首选部署为荒村 `(3,7)` 偏移至 `(5,11)`，若当前地图已作合法地形回退，以实际 marker 为准。用普通前往到附近，关图后真实走近，等待双方实际人物出现并靠近，不使用地图直接攻击代替本轮地面路径。
2. 地面攻击 BanditLeader，检查当前 Offer 必要名单，保持本场既有真实双方，不额外勾选远处支援；进入手动战斗。所有必要成员确在 loaded Surface 且有可信个人位置时应成功，人数不丢失、落点不全堆一处且不跨墙／河。不要为通过而删除失败的必要名单成员。
3. 使用同一批人，在附近可通行障碍边缘接战，或先在村外已有树／墙附近接近再攻击：合法原点应原位保留、同点占用者只选连通候选；不能为构造测试改墙或坐标。blocked 原点跨两侧无法确定时应明确拒绝。自然玩法难以稳定触发“原点恰好 blocked”，此分支仅静态核对，需制作人现场证据补验，不新增注入按钮。
4. 完成战斗，点击结束并继续关闭报告；验证只结算一次、伤亡正确、世界人物隔离恢复且玩家原暂停状态保留。
5. 若入场失败，停止并保留 Offer，从 Unity Console 复制本次完整 `[ContinuousManualBattleEntry]`（选中条目后复制详细文本）。它直接消费实际 preflight 的失败结果；无需反复走路／换敌人猜原因。单人弱匪 `(10,6)` 的 WeakBandit 可补验已通过链不回归，但不代替四人敌队验收。

## 实际检查及编译纠正

现成离线入口用 PowerShell 数组执行：`& ./tools/offline-compile.ps1 -Only @('XianXia.Core','XianXia.Data','XianXia.Unity')`。Core 455、Data 77、Host 143 源文件实际编译成功，仅既有 warnings。

此前 `pwsh -File ... -Only XianXia.Core,XianXia.Data,XianXia.Unity` 将筛选参数作为一个字符串，脚本跳过全部程序集却返回 ALL_OK。因此前两次缺 using／确定赋值修复后的 ALL_OK 不能视为已编译证据。本次已纠正调用并看到逐程序集源文件数和 OK；未为此新增编译／测试框架。

已作落点调用链静态核对、文档相对链接与 `git diff --check`。未启动 Unity，未运行 EditMode／PlayMode／Test Runner／batchmode／自动测试或 Bake。制作人人工验收待完成。
