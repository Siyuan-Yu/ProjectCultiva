# 修仙游戏项目（暂定名 XianXia）

单人开发的修仙题材游戏。Unity + Cursor。本仓库同时存放**策划文档**与**游戏工程**，用 Git 实现跨设备开发与开发史留存。

## 当前阶段

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
