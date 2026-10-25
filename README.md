# 修仙游戏项目（暂定名 XianXia）

> 当前实施以 [357 金丹精力与城镇实体范围](docs/40-process/357-golden-core-energy-and-town-footprints-2026-10-25.md)为准：等级 1～5 的控制范围长宽为 180／270／360／450／540 格；108 座固定城镇实体和外墙已按当前等级调整，荒村为 180×180 格。金丹及以上日常与打坐不再扣精力。内容包 `0.0.23-town-footprints-080` 需新开局；**Implemented / Producer Acceptance Pending**，世界图偶发旅行取消仍待现场追踪。下文为此前阶段记录。

> 最新：[350 城镇墙体与石屋／Chunk 接缝返修](docs/40-process/350-town-walls-stone-room-art-and-chunk-seams-2026-10-25.md)。青石与其余 79 处原无墙据点补石墙并留门，108 处固定 Site 均有墙；石屋改用独立石墙贴图，草地 Chunk 边缘重新铺齐。内容包 `0.0.21-town-walls-and-art` 需新开局；基础编译通过，Unity 视觉／通行／帧时待制作人人工验收，未提交推送。

> 最新：[348 自然错落的城镇与青石街区](docs/40-process/348-natural-town-layout-and-qingshi-urban-quarter-2026-10-25.md)。108 座城镇在 15200×6800 世界内改为不等距错列，五级范围最近仍隔 96 格；青石镇改为 240×210 格的正交街区，补石制公共建筑、沿街铺居和边缘田圃；其余 106 处补 258 间沿街房。内容包 `0.0.20-natural-towns`，旧档需新开局；Unity 人工验收待制作人进行，未提交推送。

> 最新：[347 五级城镇分离与世界扩图](docs/40-process/347-five-level-town-separation-implementation-2026-10-25.md)。同宗及异宗的 108 处据点已在 15200×6800 格世界重排，全部升到五级后最近两座行政方框仍隔 175 格；道路、蓝图和锚点同步迁移。内容版本 `0.0.19-separated-world` 需新开局。基础编译／静态检查后仍待 Unity 人工验收；未提交推送。

> 最新：[345 固定议政厅据点实施](docs/40-process/345-fixed-sites-and-town-levels-architecture-change-2026-10-25.md)。102 旧旗据点改为固定可占领议政厅，荒村一级、青石与 102 据点三级，五级范围／升级费用已接线；青石街区扩展并铺正交道路，内容版本 `0.0.18-fixed-council-sites` 需新开局。基础编译与静态完整性通过，Unity 人工验收待制作人进行；未提交推送。

> 最新：[342 世界聚落与公开传承](docs/40-process/342-world-settlements-and-public-techniques-2026-10-24.md)。108处现有据点已有街区，含16宗门主城商业NPC；新增16功法／16斗技／32书册，石木功能房与树木外观变体。版本 `0.0.17-world-settlements` 需新开局；运行待人工验收，未提交推送。

> 最新：[341 荒村四区调整](docs/40-process/341-huangcun-quadrants-resources-and-workrooms-2026-10-24.md)。田地紧排、住房分区、室外棋盘、两倍散生树、铁铜银灵散矿与低级木制功能房已配置。版本 `0.0.16-huangcun-quadrants` 需新开局；基础编译／静态引用通过，运行待人工验收，未提交推送。

> 最新：[340 荒村生活与聚落内容](docs/40-process/340-huangcun-daily-life-and-settlement-content-2026-10-24.md)。开局引导释义、巡卫／主管规律作息、近田药圃、树林／修炼院／医疗床、青石生活设施与8委托已实施；版本 `0.0.15-huangcun-daily-life` 需新开局。四程序集基础编译和内容静态校验通过，运行待人工验收，未提交推送。制作人确认此前卡顿基本解决，200 FPS仍未实测确认。

> 2026-10-24主控与性能返修见[339](docs/40-process/339-pointer-facing-and-movement-performance-2026-10-24.md)：制作人最新13:46 F9报告平均133.40 FPS，全程未暂停，120不是限帧值。继续向200 FPS预算优化覆盖／床边重复查询、临时分配与隐藏HUD点击区；新增Ctrl+F9临时不限帧对照，关闭F9恢复原设置。基础编译通过，追加收益与200 FPS待新采样，完整交互待验；未提交推送。当前宗门密度暂保留，位置精调后续另做。

> 最新领地：[338 十六宗门正式势力范围](docs/40-process/338-sixteen-sect-territories-2026-10-24.md)。16宗领地已配置，新增90旗，世界与荒村尺寸不变；内容0.0.14，需要新开局。城内设施／驻军仍待补，运行待验收，未提交推送。

> 最新：[337 金丹化翼实施与验收](docs/40-process/337-golden-core-flight-design-2026-10-24.md)。制作人已批准框架扩展；金丹突破、F3飞行／轻量翅膀、空地75%闪避及Snapshot v18已接线，内容0.0.13-golden-core-flight。基础编译通过，运行待人工验收，未提交／推送；F2仍为独立远程模式。

> 最新操作见[336](docs/40-process/336-directional-ranged-control-and-polish-2026-10-24.md)：筑基F2纱衣后，WASD移动、鼠标瞄准、长按左键定向普攻，命中才伤害；骨架瞄准与UI互斥已接线，基础编译通过，待人工验收，未提交推送。

> 宗门打法最新见[335](docs/40-process/335-sixteen-sect-combat-identities-2026-10-24.md)：16套功法／斗技补齐攻防血量、回血、连击冷却与修炼难度取舍。版本0.0.12-sect-combat-identities；已配置，待人工验收；普攻加速及元素特殊机制待框架批准。未提交推送。

> 2026-10-24最新布局见[334](docs/40-process/334-compact-faction-territories-and-single-core-huangcun-2026-10-24.md)：同宗据点按约280格邻接收拢，荒村重排为220×200，全部村内物件均在自身议政厅225×225范围内；原补范围旗移至独立的衡河西岸驿。同步林间实物、出生床位、道路与宗门候选。版本`0.0.11-compact-territories`，需新开局；框架未改，运行待验，未提交推送。

> 2026-10-24最新内容见[333](docs/40-process/333-small-village-and-sixteen-sects-content-2026-10-24.md)与[十六宗门目录](docs/30-content/16-sect-world-content-catalog.md)：正式名册20人分布为荒村13／青石镇7，新增16套入门功法与斗技、32书册、青石书院购书与4项交付换书。正式内容现22任务／67生活事件／6机会模板，版本`0.0.10-small-village-sect-books`，需新开局。16宗城与附属节点为作者候选，高阶人物和专属机制仅设计；未扩框架，待人工验收，未提交推送。

> 2026-10-23最新追加见[332](docs/40-process/332-huangcun-confined-opening-and-liberation-2026-10-23.md)：正式荒村开局在真实占领前限制离村、暂停外界自主模拟，占领后永久解锁外界、势力入口与建造。正式内容现20人／18任务／54生活事件／6机会模板，复用原资源和同屏上限；版本0.0.9-huangcun-liberation，需新开局。当前已实现、待人工验收，未提交推送。

单人开发的修仙题材游戏。Unity + Cursor。本仓库同时存放**策划文档**与**游戏工程**，用 Git 实现跨设备开发与开发史留存。

## 当前阶段

2026-10-23 换电脑续做入口：[328 暂停检查点与完整续做清单](docs/40-process/328-paused-work-cross-computer-handoff-2026-10-23.md)。本次备份包含扩图／整数格／木墙、编辑器保存修复、运行性能与 NPC 修复，以及荒村首段内容首稿；当前版本 `0.0.8-huangcun-living-opening`。正式绑定已切换，但旧验收内容隔离、将老分支修正、最终引用检查与统一编译尚未完成。检查点提交不代表全项目审查结束或制作人人工验收；新电脑需从源码重新发布外置编辑器。

2026-10-23 最新荒村布局见 [322](docs/40-process/322-huangcun-compact-housing-and-cell-wood-walls-2026-10-23.md)：住宿横向隔 1 格、排间隔 3 格，房床及住户锚点同步；240×200 村界新增 844 段 1×1 木墙，四面各留 8 格通路。新增轻量木墙 prefab，木墙建造模板也为 1×1；当前内容版本 `0.0.7-huangcun-wood-walls` 覆盖下文版本。基础编译通过，需新开局验收通行／破坏／帧率，未提交。

2026-10-23 最新占地规则见 [321](docs/40-process/321-integer-grid-footprints-and-use-cells-2026-10-23.md)：物件整数格放置，蒲团／棋盘 1×1，人物 1×1 连续移动且美术尺寸独立，棋盘两侧显示真实使用格脚印。当前版本 `0.0.6-grid-footprints`，覆盖下文扩图版本及“保留旧物件宽高”表述；世界、控制范围及道路河流宽度不变。已实现，需新开局人工验收，未提交。

2026-10-23 世界与荒村扩展见 [320](docs/40-process/320-world-expand-05-7600x3400-2026-10-23.md)：世界 **7600×3400**、荒村 **240×200**，一级议政厅／势力旗控制范围 **225×225**；道路河流与建筑家具保持原尺寸。已实现，待新开局人工验收；内容版本 `0.0.5-world-expand-05`，旧档保留、不自动迁移。未提交。

2026-10-21 当前工作区获制作人明确封板上传授权：[312医疗](docs/40-process/312-medic-bedside-treatment-2026-10-20.md)、[313医疗收尾与地面物品](docs/40-process/313-medical-closure-and-ground-loot-2026-10-20.md)、苏醒返工及洞府地面排序/spaceKind文件加载修复统一提交上传。8程序集离线编译通过；未逐项确认的Unity视觉与边界验收保持原状态，下文“未提交”为此前实施时点记录。

2026-10-20 医疗收尾与地面物品显示见[313](docs/40-process/313-medical-closure-and-ground-loot-2026-10-20.md)：卧床恢复精力、旧档零血囚犯补齐、携药转职归还/死亡落地，以及洞府功法/斗技按物品显示已接线，待现场验收。当前Snapshot为v17，正常读取v15/v16补齐，不覆盖旧档；下面v16记录为医疗初次实施时点。未提交。

2026-10-20 医者床边治疗已接线、待制作人现场验收，见[312](docs/40-process/312-medic-bedside-treatment-2026-10-20.md)与[ADR-0048](docs/40-process/43-decisions/ADR-0048-medical-recovery-and-carried-medicine.md)。公库/附近Party供药、持续药效、中级医疗床减缓弥留、HP超过50%苏醒及Snapshot v16已实施；新开局阿禾、阿杏、阿枝为医者。此前“Medic延期/医疗床永久稳定”以本条与267现行覆写为准，未提交。

2026-10-20 世界物件选中反馈见 [307](docs/40-process/307-world-object-selection-feedback-2026-10-20.md)：房间蓝色 Gizmos 调试圈已替换为正式选中细框，床、门、木墙等按各自范围显示；离线编译通过，待制作人现场验收，未提交。

2026-10-19 当前推进：[306 正式试玩与历史Demo构建分离](docs/40-process/306-formal-player-and-demo-build-separation-2026-10-19.md)。正式Windows入口为 `XianXia/Build/Formal Windows x64`（仅LevelTester），历史Demo使用独立菜单/产品名/输出/存档；内容随包加载与普通会话菜单已接线。制作人确认可打包、运行且打包后基本存读档未见问题；Formal／Demo／Editor 槽位隔离未逐项记录。[CIVILIAN-LIFE-01](docs/40-process/267-civilian-life-01-design-freeze-2026-09-27.md#当前功能与验收状态表唯一维护点2026-10-19-制作人反馈覆写)已获制作人整体人工验收，尚未封板；Medic 等延期功能不在范围。本轮没有改变参考开局经济或开始新玩法。以下历史段落以247最新覆写、267唯一状态表和306为准。

**Architecture Freeze v0.2＋ADR-0038 最终冻结。** Continuous Outdoor、统一 Squad／CharacterEncounter、Actual Administrative Control、MAP-01～04、SPACE-01、LEGACY-FINAL-A／B／C，以及 Hex／Army 正式运行依赖退役与 021915 统一收尾均已完成制作人验收并封板。封板提交为 `9b32fe0`。正常 Outdoor authority、合法 compatibility 边界和新会话唯一恢复摘要见 [247 交接](docs/40-process/247-project-handoff-current-state-2026-09-18.md)。
Unity 版本锁定 **2022.3.6f1 Built-in**（ADR-0001）。

2026-10-19 当前工作区剩余改动获制作人明确“全部封板提交并上传”授权。势力旗反击、WorldMap 图标缩放及窗口输入隔离按制作人已确认范围记 Producer Accepted；提交和推送不代表 Medic、其他系统残项或未现场验收的美术已完成。

系统残项与现有美术接线最新静态核对见 [305](docs/40-process/305-system-and-art-closure-audit-2026-10-19.md)；已补齐玩家／渔村两种现成旗帜 View，其他待验与真缺口分开记录，不代表全项目系统或美术已封板。

2026-10-19 势力旗验收覆写：制作人已确认独立战场反击功能验收通过；本轮状态为 Implemented / Producer Accepted / 未封板，尚未提交或推送。实施时的待验收描述以本条和 [ADR-0046](docs/40-process/43-decisions/ADR-0046-faction-flag-encounter-defense.md) 当前状态为准。

2026-10-19 势力旗后续扩展：已按制作人确认接入与议政厅同规则的独立战场远程攻击，无守军旗单独受攻也进入独立战场；当前为 Implemented / Producer Acceptance Pending，尚未提交，未进行 Unity 现场验收。见 [ADR-0046](docs/40-process/43-decisions/ADR-0046-faction-flag-encounter-defense.md)。

2026-10-19 最新覆写：议政厅独立战斗攻击及弹道、巡卫战后按入战位置返场、人物骨架与床层的两处 Sprite 顶点越界修复均已由制作人在 LevelTester 现场验收；本批获明确封板推送授权。议政厅正式规则见 [战斗 §12.2.1](docs/20-systems/23-combat.md)、[ADR-0045](docs/40-process/43-decisions/ADR-0045-council-hall-encounter-defense-authority.md)，本批状态与范围见 [247 最新交接](docs/40-process/247-project-handoff-current-state-2026-09-18.md) 及 [开发日志](docs/40-process/42-devlog.md)。下方“议政厅尚未实施／待验”相关段落属于当时阶段，不代表当前状态。

2026-10-18 人物美术与移动的最新实施/待验状态见 [297 当前交接](docs/40-process/297-art-rig-and-source-cleanup-current-handoff-2026-10-18.md)；12 套 Unity 共用骨架 Skin、八向与侧臂、卧姿和右键连续改路已接线，美术源文件清理有独立台账。版本封板不等于制作人人工视觉验收。
制作人复看人物动画后同意推进；当前黄村住宅—道路—田地同屏表现样板的实施与待验入口见 [298](docs/40-process/298-huangcun-environment-visual-slice-2026-10-18.md)。
住宅墙面现场反馈及实木墙、麦田／药田阶段图返修见 [299](docs/40-process/299-solid-wood-wall-and-farm-stage-presentation-2026-10-18.md)。
田地逐格缩小感与议政厅圆盘外观的返修接线见 [300](docs/40-process/300-field-scale-and-council-hall-exterior-2026-10-18.md)，仍待人工验收。
议政厅屋顶符阵已作静态美术接线；自动攻击的规则边界与待定数值见 [301 ACR](docs/40-process/301-acr-council-hall-spirit-defense-2026-10-18.md)，玩法尚未实施。
现有筑基纱衣远程普攻的通用可见弹道返修见 [302](docs/40-process/302-unified-visible-ranged-projectile-presentation-2026-10-19.md)；议政厅待正式参战伤害权威接入后复用。
同伴窄路慢速蹭行的 Host 移动返修及议政厅攻击现状核查见 [303](docs/40-process/303-party-corridor-transit-and-council-hall-attack-status-2026-10-19.md)。
制作人复验后要求人物行走暂时互相穿过；最新共用移动改动见 [304](docs/40-process/304-unblocked-character-transit-2026-10-19.md)，待现场验收。303 的在途避让现由 304 替代；议政厅攻击仍未实施。

远端：https://github.com/Siyuan-Yu/ProjectCultiva

## AI 多会话协作

三类长期工作流（架构／Unity 开发／剧情文案）的职责、同步与变更流程见：

**[`docs/40-process/52-ai-collaboration-protocol.md`](docs/40-process/52-ai-collaboration-protocol.md)**

（编号说明：`46` 已用于 Demo 美术资源表，本规范为 **52**。）

## 从哪开始读

新接手这个项目（包括几个月后的自己、别人、或新的 AI 会话），按这个顺序读：

1. `AGENTS.md` — AI 硬约束与当前阶段  
2. `docs/40-process/52-ai-collaboration-protocol.md` — 多会话职责与真源规则  
3. `docs/00-project/00-overview.md` — 最高层大纲与系统索引（**入口**）  
4. `docs/00-project/03-glossary.md` — 术语表  
5. `docs/40-process/247-project-handoff-current-state-2026-09-18.md` — 新会话恢复摘要、系统现状总表与当前 Proposal
6. `docs/30-tech/33-architecture-core-rules-freeze-v0.2.md` — 架构主契约
7. `docs/00-project/04-reading-guide.md` — 按下一议题选择少数正文
8. `docs/40-process/42-devlog.md` — 最近决策与历史理由（无需从头通读）
9. （Demo 参考）`45`～`49` — 原型范围与美术；正式语义以 Freeze／`32` 为准  
10. `docs/10-benchmark/14-borrow-and-differentiate.md` — 借鉴与不做

## 文档结构

```
docs/
├── 00-project/          项目定位与约束
│   ├── 01-vision.md             愿景、目标玩家、成功标准、待确认问题
│   ├── 02-scope-and-constraints.md  现实约束与范围控制原则
│   └── 03-glossary.md           术语表（命名唯一真源）
├── 10-benchmark/        竞品拆解
│   ├── 11-tale-of-immortal.md          鬼谷八荒
│   ├── 12-cultivation-simulator.md     了不起的修仙模拟器
│   ├── 13-comparison.md                横向对照
│   └── 14-borrow-and-differentiate.md  借鉴/差异化/不做
├── 20-systems/          系统设计（一系统一文档）
├── 30-tech/             技术架构
├── 40-process/          研发管理
│   ├── 41-roadmap.md            里程碑与完成标准
│   ├── 42-devlog.md             开发日志（倒序）
│   ├── 43-decisions/            ADR 架构决策记录
│   ├── 51-…-implementation-plan  Core M1 实施计划
│   └── 52-ai-collaboration-protocol.md  多 AI 会话协作规范
└── 90-templates/        模板
```

## 维护规范

这套规范存在的唯一目的：让**三个月后的你**和**接手的人**能在半小时内恢复完整上下文。

1. **文档先于代码**：系统设计文档没定稿，不写该系统的代码。
2. **devlog 必须写**：每次有实质进展就在 `42-devlog.md` 顶部追加一条，重点写"为什么"。
3. **贵的决定写 ADR**：以后改起来会痛的决定（版本、格式、架构边界、引入插件）必须留记录。
4. **命名走术语表**：新概念先登记 `03-glossary.md`，再写代码。
5. **文档头部三件套**：状态、优先级、最后更新日期，改内容就改日期。
6. **提交信息带前缀**：`feat/fix/docs/refactor/data/chore`。
7. **多 AI 会话**：遵守 `52-ai-collaboration-protocol.md`；重要结论必须落文档，禁止只在聊天里定案。
8. **项目文档默认使用中文**：设计、开发流程、架构、验收与 Devlog 正文默认使用中文；类名、方法名、字段名、JSON key、文件路径、DefinitionId 等技术标识保持原文。

## 跨设备开发

```bash
# 首次
git clone <远端地址> && cd XianXia

# 每次开工前
git pull

# 每次收工（只选择本轮相关路径；不要混入其他工作区改动）
git add -- <明确路径>
git diff --cached --check
git commit -m "docs: 更新xxx"
# 仅在制作人明确要求时 push
```

远端仓库：`https://github.com/Siyuan-Yu/ProjectCultiva`
Unity 版本：`2022.3.6f1（Built-in，见 ADR-0001）`
