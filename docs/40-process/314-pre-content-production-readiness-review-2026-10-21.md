# 内容制作前准备度复核（2026-10-21）

> 状态：Static Review（2026-10-21 审查时点）；下列建议在制作人随后授权后进入 [315 实施记录](315-content-production-prep-implementation-2026-10-21.md)。基线：`dev_openworld` / `c698256`。审查开始时工作区干净；未启动 Unity、未编写或运行自动测试。
> 本页替代 [309](309-pre-content-readiness-audit-2026-10-20.md) 的当前建议；功能、验收及美术状态仍分别以 267、各实施记录与 273 为真源，不新建第二份验收台账。

## 结论与已有基础

现有 Continuous Surface、Squad、CharacterEncounter、控制接替、内容事件／任务、交易／拍卖、民生与存档基础足以支撑首段内容。当前没有需要重新建设世界或战斗架构的具体证据。内容制作前最值得收口的是作者工具与运行格式的一致性、正式开局配置选择、外观绑定和玩家能理解的反馈。

- 正式包基本打包／运行／存读档、CIVILIAN-LIFE-01 整体沿用制作人已给的验收结论，不重做旧验收大清单。
- [310](310-visible-ranged-hit-damage-timing-2026-10-20.md) 已改为直线弹道，制作人已确认，修正提交 `7f6a47e`；不能恢复跟踪弹，也不再列为未实施。
- [312](312-medic-bedside-treatment-2026-10-20.md)／[313](313-medical-closure-and-ground-loot-2026-10-20.md) 医者取药、床边治疗、恢复／苏醒、药物去向与地面物品已实施并随 `c698256` 封板。309 的“Medic 只有按钮、没有行为”已过时。当前 Snapshot v17，正常 JSON 读取支持 v15／v16 补齐；封板不补造最新视觉与医疗边界的逐项验收。

## 内容生产前的具体接缝

| 优先级／时点 | 当前证据与影响 | 建议的最小范围 |
|---|---|---|
| P0：批量用作者工具前 | `ExternalTools/ContentAuthoring/Shared/SchemaFields.cs:7–14、36–40、91–96` 的白名单缺少当前合法的 `defaultFactionId`、`initialMortalProfession`、`openingSurfaceId`、`startingWallet`、`spaceKind` 等字段；`PackageValidator.cs:25–46` 将未知类型／字段报 error，`PackageBrowser/MainWindow.xaml.cs:65` 实际调用。运行 Loader 已读取这些字段，人物编辑器自己也会写默认势力字段。 | 对齐现行 definition 类型、字段与实际常用作者表单，令合法数据不会被误报。复用既有加载／引用规则，避免再维护一套含义不同的规则。此处只确认校验误报，未证明编辑器会丢字段，不能扩大结论。 |
| P0：内容进入发布包前 | `Assets/Scripts/Unity/Editor/PlayerBuildProfiles.cs:77–94` 的构建检查只核 manifest、Data 与 Prefab；复制 JSON 前没有调用完整 Loader／引用校验。已有 `Assets/Scripts/Unity/Content/ContentPackageValidationFacade.cs:18–25` 可完成加载和交叉引用检查。 | 构建入口复用现成静态校验，错误给出文件／DefinitionId／引用目标，阻止把错引用交给玩家启动时才发现。无需新增自动测试体系或另建发布平台。 |
| P0：首段正式开局配置时 | `Assets/Scenes/LevelTester.unity:215508–215510` 仍选择 `scenario_ch01_reference` 与 `roster_level_tester`。`PlayableDayBootstrap.cs:88–104` 在指定 roster 后用其 entries 覆盖 scenario.spawns；只改 Scenario 的人物列表不会改变真实开局。当前名册包含验收人物，参考开局钱包为 10000／5／2。 | 明确一组正式启动绑定：Scenario、可选 Roster、Chapter、Surface，以及初始人物／职业／资源／任务入口。保留参考开局作工具台；正式数值随首段内容设计，不在本次审查中擅改。不可仅按 acceptance 文件名批量删除内容。 |
| P1：批量新增人物／物品前 | `HostStepAArt.cs:72–84` 仅硬编码 5 个物品图标，`:118–150` 硬编码人物 ID→Profile，陌生人物回退 Other。`HostGroundLootVisual.cs:53–55` 的缺图类别还依赖 ID 子串。新内容虽可生成，但独特外观仍需改 C#。 | 先设计一份有限的 Host 表现映射：Character DefinitionId→已有 Skin/Profile；ItemId→图标资源键和明确地面类别。沿用现有 12 套共用骨架、稳定 ID 与安全回退，不把 Unity 类型塞进 Core/Data，不泛化所有常量。 |
| P1：交给内容作者前 | `docs/40-process/80-chapter-content-production-guide.md:12、76、119` 仍介绍旧快捷键、普通 worldRegion 制作与“会话态即可”；`Content/BaseGame/Data/README.md:30` 仍称通用内容状态未接线。总览及部分入口仍有民生未实施、旧 Snapshot 或已提交内容未提交的现行措辞。 | 只更新当前制作短流程与入口摘要：作者源→导出候选→现行加载／引用校验→明确安装→启动配置→制作人人工体验。链接既有稳定 ID 约定、267／312／313；历史记录保留，Freeze 不改。 |

## 美术与玩家界面

1. **先解决信息可读性。** `HostMortalWorkPanel.cs:133–149` 仍直接显示 ScheduleId、床 StableAssetId、内部工区 ID 与 MovementOwner。正式面板应优先说明当前行动、目标地点、缺少条件和下一步；技术字段留诊断入口。Medic 已能工作，不能再建议禁用该职业来掩盖旧缺口。
2. **补首段真正使用的三类缺口。** 273 中 `ART-ROOM-009` 修炼垫／恢复点仍共用 Cushion；`ART-GROUND-004` 尚缺专用石地／矿区底色，洞府已有中性程序石地回退；`ART-UI-001` 九宫格皮肤仍为候选，正式 HUD 主要使用程序纯色。HUD 信息布局先稳定，再接统一皮肤。它们是首段视觉完善范围，不要求先做完整大陆美术。
3. **用一处同屏场景校准。** 房屋、床、门墙、田／药田、树、矿、人物、掉落物与常用 HUD 一起核对尺度、脚点、遮挡、接缝、文字与选中反馈。`HostStepAArt.cs:264–358` 已接道路、草地、资源、建筑、地板、门饰和种植阶段；不按旧台账把这些重新当作未制作。
4. **少量声音可随样本补入。** 当前 `Assets` 未找到常用音频资源，正式 Unity 脚本未找到 `AudioSource`／`AudioClip`／`PlayOneShot` 接线。交互确认、命中、工具接触、收获／突破等少数反馈有价值；完整配乐、音频中间件、全套高阶特效可延期。
5. **先校准台账再批量采购或生成。** 273 仍未记录307物件框、撤掉的地点石牌、已验直线弹道及313地面书卷的接线，部分人物也落后于现有骨架映射。同步 TSV／XLSX 时只据实际接线和明确反馈改变状态，不把“已生成”“已接入”“已验收”混写。

## 最近改动的窄范围复看

首段包含洞府取书时，先确认313最新 `spaceKind` 文件加载与地面排序修复后，人物／书册／卷轴完整可见且能拾取；目前有修复代码，尚无最新现场通过记录，不能直接断言仍坏。医疗只看最近变动的“治疗→苏醒返工”、卧床精力及一份携药归还／落地，不重复旧民生全套。307／308 的选中、上床与搬运观感可在同一场景顺路复看；311空表不是要求重走所有条目的理由。

## 建议下一轮与完成边界

**下一轮建议优先完成“内容生产接线收口”：作者字段校验对齐＋构建复用完整内容引用校验＋现行制作短流程。** 这是本次最明确、有现成基础且能降低后续内容返工的范围。外观映射可紧接一轮，先覆盖现有 Skin 与物品类别；正式开局绑定与同屏美术样本随首段内容准备一起明确。

之后即可用现有能力制作一小段内容。装备制造／炼丹、跨 Site 物流、工作优先级矩阵、主动灵力治疗、通用尸体战利品、泛化 NPC 战略 AI、全人物独特头像和全地图装饰，只有首段明确依赖时才成为前置。当前动态掉落只承诺可信 Surface 落点；洞府作者配置的拾取物已支持，不等于已支持任意独立空间掉落。上述边界应进入内容设计，不能靠新增框架无限延后内容制作。

以上为审查时点的 Proposed；后续实施状态见 [315](315-content-production-prep-implementation-2026-10-21.md)，不能把本页旧时态当作当前未实施结论。
