# 346｜2026-10-25 工作区整体 GitHub 检查点

> 目的：按制作人“先把现在所有改动传一份”的要求，将当前 `dev_openworld` 工作区中所有非忽略的已修改、新增文件连同文档状态一起提交并推送到 GitHub。**这是版本备份，不是新的 Producer Accepted／Sealed 或 Unity 运行验收。**

## 本次保存的范围

- 本次开始前 Git 工作区有 190 个已跟踪改动、276 个未跟踪文件；`origin/dev_openworld` 与本地 HEAD 无分叉。纳入的内容覆盖 `Assets` 玩法与 Host、`Content/BaseGame` 正式运行数据与作者源、`ExternalTools/ContentAuthoring`、`ArtSource` 与新美术、`README` 和项目设计／过程文档。Git 忽略的 Unity `Library`／`Temp` 等本机工作产物不属于本次仓库备份。
- 世界与内容工作主要见 [329](329-project-audit-resume-and-opening-completion-2026-10-23.md)～[343](343-world-streaming-and-tick-frame-budget-2026-10-24.md)：荒村开局与民生、宗门内容、战斗／飞行、世界城镇与公开功法、地图扩容后的性能返修。各页原有 Implemented、Producer Acceptance Pending、历史现场反馈等状态保持原义；本次提交不自动提升验收状态。
- [344](344-qingshi-urban-upgrade-proposal-2026-10-24.md)和[ADR-0055](43-decisions/ADR-0055-fixed-council-hall-sites-and-five-level-control-range.md)／[345](345-fixed-sites-and-town-levels-architecture-change-2026-10-25.md)记录最新城镇方向：青石正交、少分叉街路；议政厅五级长宽倍率；102 个旧旗 Site 保留身份并拟迁为固定可占领核心。**这部分仍是 Design Confirmed / Implementation Pending**，当前正式运行世界还未按 345 迁移，不能因本次 Git 提交而宣称已可玩。
- `Content/BaseGame/manifest.json` 当前内容版本为 `0.0.17-world-settlements`。345 所述未来新布局内容版本提升、旧档隔离与 Core／Data 接线尚未实施；不能在现有包上假称已完成旧旗转固定议政厅。

## 静态核对与交接

- 提交前核对 Git 分支／远端无分叉、所有非忽略文件清单、较大新增文件以及 `git diff --check`。本检查点不新增或运行自动测试，也不启动 Unity；此前各过程页记录的编译与制作人 F9／游戏体验反馈只在各自时间和范围内成立。
- 推送后可从 GitHub `dev_openworld` 拉取本次工作区状态继续制作。下一轮实施仍以 345 单轮指令和 ADR-0055 为准；须先解决固定核心初始等级、重叠 Claim 顺序、据点／道路落点及 102 个固定核心作者发布，再改正式运行内容。
