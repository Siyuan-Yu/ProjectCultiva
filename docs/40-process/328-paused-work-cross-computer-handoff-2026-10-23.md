# 换电脑续做交接：全项目审查暂停检查点

- 日期：2026-10-23（Asia/Shanghai）；仓库 https://github.com/Siyuan-Yu/ProjectCultiva ，分支 `dev_openworld`。
- 制作人先要求暂停，随后明确要求记录全部进度并上传 GitHub。本次仅补文档和备份当前源码、内容及资产，不继续玩法实施。
- **开发中检查点，不是完成版或验收封板。** 提交／推送不提升人工验收状态；实际提交号以 Git history 和聊天回执为准。
- 原实施入口：[323](323-project-wide-audit-and-production-pass-2026-10-23.md)。本页是跨电脑恢复第一入口，覆盖 323～327 中未区分计划和实际进度的表述。

## 1. 持续目标与约束

继续整体审查和改善美术呈现、框架、战斗、NPC、境界能力、交互、地图丰富度、编辑器、性能和代码可读性；可以依既有第一章设计生产人物／流程／内容。复用现成美术，不增加纹理或模型精细度。

沿用 Freeze v0.2、ADR-0038、WorldTick、RelationshipLedger、Continuous Surface、Squad、CharacterEncounter。遵守 AGENTS：只做基础编译与必要静态核对，不创建／运行自动测试，不自行启动 Unity，不改 Packages／ProjectSettings／Freeze／Snapshot 数值版本。

## 2. 已经落盘

| 范围 | 真实进度 | 记录与验证边界 |
|---|---|---|
| 扩图 | 世界 7600×3400、荒村 240×200、一级议政厅／旗控制范围 225×225；道路河流桥宽度保留 | [320](320-world-expand-05-7600x3400-2026-10-23.md)，该阶段编译和静态发布核对完成，新布局待人工验收 |
| 整数格 | 物件整数占地；人物 1×1 连续移动；蒲团／棋盘 1×1；棋盘使用格与脚印共源；作者／加载约束 | [321](321-integer-grid-footprints-and-use-cells-2026-10-23.md)，该阶段编译和作者工具发布完成，运行待验 |
| 紧凑住宿与墙 | 19 间普通住宅横间隔 1 格、排间隔 3 格；22 房／27 床与 20 住户锚点同步；844 段 1×1 木墙，四面各 8 格开口；轻量 WoodWallTile prefab | [322](322-huangcun-compact-housing-and-cell-wood-walls-2026-10-23.md)，村内 943／Surface 948 物件；该阶段编译／静态核对完成，未测 FPS 或通行 |
| 作者编辑器 | 坏 JSON 定位／阻止不完整包保存；SHA 外部修改检查、互斥、原子替换；精确路由／重复 ID 保护；地点／生成池字段保真；多定义地图防截断、保存错误提示 | [324](324-authoring-ux-and-integrity-audit-2026-10-23.md)，2026-10-23 19:42:56 实际 11/11 Release 发布成功，原 4 条 nullable 警告保留，交互待验 |
| 性能表现 | Chunk 静态索引／StableId 集合、建筑提前过滤、组件列表池、人员面板每帧一次收集／可见行绘制、导航顺带生成精确 RoadSpans、WorldMap 纹理销毁 | [325](325-runtime-performance-and-presentation-audit-2026-10-23.md)，不再首次开图额外重扫 25,840,000 格；只有早期索引／组件池阶段 Unity 编译通过，后续增量未统一编译，未测性能 |
| NPC／战斗／委托 | 纱衣拒绝失能／力竭；离屏个人移动服从 Party／Squad／旅行／空间权威；接管清旧撤离；自主弹体不误补刀／重查敌对，失去目标结束旧追击；委托合计重复物资并复用事务 | [326](326-combat-npc-progression-audit-2026-10-23.md)，5 文件已改／限定 diff 检查完成，尚未统一编译或人工验收 |
| 荒村内容 | 6 份新 JSON：独立剧本、20 人名册、内部体验弧、8 任务、23 对话探索事件、3 动态机会及其 3 检视事件 | [327](327-huangcun-opening-content-production-2026-10-23.md)，仅核对 JSON 可解析与条目数量，正式引用检查／运行尚未完成 |

当前 manifest 版本 `0.0.8-huangcun-living-opening`。formal-opening 已指向 `base:scenario_huangcun_life`／`base:roster_huangcun_life`，地图仍为 `base:map_ch01_cave`。正式构建应用此绑定，现有 LevelTester 场景／Inspector 可能仍保存旧参考剧本，入口一致性未核完。

新文件都在 `Content/BaseGame/Data/`：
- `Scenarios/huangcun_life.json`
- `Rosters/huangcun_life_roster.json`
- `Chapters/huangcun_life_chapter.json`
- `Quests/huangcun_life_quests.json`
- `Events/huangcun_life_events.json`
- `WorldOpportunities/huangcun_life_opportunities.json`

任务为认人安顿、修行入门、实际达到炼气、探索洞府、前往青石镇，以及阿石粮食／阿杏灵药／阿木粗木三个自愿无期限委托。新名册去掉明确验收假人，初始保留 4 支正常 Squad；钱包为 0、背包 20 粗木＋6 粮食，均是未调优的首稿值。3 个机会复用石头／药草／掉落外观，调度总上限仍为 3，无新增精细美术。

## 3. 确切断点：以下都没做完

1. **正式／验收隔离未实施。** 新章会设置 story:huangcun_production，新内容以此为前置；旧 event01_acceptance_events、chapter1_harness_events、骨架事件和测试任务还未加排除条件。主管 Priority 100 验收对话仍可能压住正式剧情。327 的隔离文字是计划。
2. **任务日志隐藏未解决。** QuestJournalQuery 会把前置失败的 inactive 固定任务显示为 Locked，HostQuestJournal 放入“可接／未来”。只加 missingFlag 仍泄漏验收文字；需在保留正常未解锁任务的前提下解决。
3. **将老“改日再来”仍 startQuest。** ch01_reference_events.json 的 later 分支未修；要真正允许稍后并保留回头答应入口。旧将老文案“入炼气后才可学斗技”等也需对齐真实规则。
4. **独立遭遇两条线索仅发现、未修。** HostCharacterEncounter.cs 没有本轮修改：攻击缺少现场同源 WalkGrid 遮挡守门；显式手动纱衣补刀弹体命中只认 Living，可能打不到倒地者。续做先复核，自动不补刀与显式补刀分开，遮挡不能把建筑自身占地误当墙，友军穿行不应变成新遮挡规则。
5. **内容接线／文案待核。** 新章 plannedDays:0 合法性；LevelTester 与正式入口；事件优先级／话题可达性；委托正确 NPC／库存／领奖／放弃再接／关系对象；动态机会出现与一次性解决；洞府／秘籍的实际入口；把过于开发口吻的对白整理为有用提示。
6. **最新源码未统一编译。** 性能／NPC 增量不能借用前轮 ALL_OK。完成当前缺口后统一基础编译及正式内容引用检查，再交人工验收。
7. **系统文档仍有漂移。** 总览旧 Snapshot v10／凡人未实施、SCHEMA 和作者字段表等需按当前代码逐项校正；不要覆盖历史记录冒充最新。

## 4. 新电脑操作与续做顺序

1. 切到 dev_openworld 并拉取 origin；先读本页，再读 AGENTS／52／README／247 和 323～327。扩图、格点、墙与本轮优化一并备份，不回退或重做已有改动。
2. 使用 Unity **2022.3.6f1**、Windows 与 **.NET 8 SDK**。Git 保留正式作者源、已生成地图、源码、Prefab／meta、文档和 JSON；EXE／Apps、Library、Temp、日志、构建产物和 _backups 按现有 .gitignore 忽略。
3. 在仓库根运行 `pwsh -File ExternalTools/ContentAuthoring/publish.ps1`，成功后从 ExternalTools/ContentAuthoring/Apps 启动工具。另一台电脑需要重新构建 EXE。
4. Tools/offline-compile.ps1 依赖本机 Unity 路径 D:/UnityEditor/2022.3.6f1 和 Library/Bee 的 rsp。新电脑须由制作人先正常导入工程、核对安装路径，再使用对应基础编译方法；代理不自行启动 Unity。
5. 优先收口正式内容隔离／任务日志／将老分支，再核对新内容引用并统一编译。随后一次人工验收新开局对话委托、自主吃睡打坐、围墙通行破坏、地图打开、战斗失能和命令接管。
6. 一次性地图重排助手只在忽略的 Temp，续做不依赖它们；使用已提交作者源与现有 WorldComposer／FineEditor／发布器。保留旧档，按版本门禁处理，不删除或暗中迁移。

## 5. 审查还需继续的方向

- 性能：墙体悬停线性查询、844 Renderer 的实际排序／draw call、首次纹理上传、密集 NPC 感知扫描；需场景观察，静态热点不等于实测卡顿。
- NPC：冷区只有低频近敌移动／撤离／普攻，未释放主动斗技／纱衣远程。离屏战报、战略增援／掠取、投降后的正式处理应对照 317／318 区分已定未接线与规则待定。
- 成长：运行境界只有感应、炼气、筑基；感应突破无需功法，炼气起后续突破需功法。高阶飞行／法宝／神魂尚无可直接执行的完整规则。
- 内容决定：主管倒台门槛、同伴告密、第一章是否必须有筑基驭器／法宝、秘密灵地默认包装等冲突尚未解决，不能通过任务旗暗定。
- 工具／整洁度：字段白名单和 SCHEMA 同步、职业／商店／拍卖作者控件、大窗口职责拆分、文件级保护之外的跨文件保存体验，依真实使用继续审查。

本检查点不表示全项目审查或完整第一章完成。
