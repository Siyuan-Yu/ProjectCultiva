# 内容生产接线收口与现行短流程（2026-10-21）

> 2026-10-21 后续作者工具、正式生效名册、WorldComposer 非荒村蓝图与自定义 Rig 配置修复见 [316](316-content-production-readiness-repair-2026-10-21.md)；本页记录第一轮实施时点，艺术素材仍待现场验收。

> 状态：Design Confirmed（制作人授权 2026-10-21）／Implemented／Producer Acceptance Pending；制作人已另行明确要求全部提交并上传，Git 结果以仓库历史为准。对应静态审查：[314](314-pre-content-production-readiness-review-2026-10-21.md)。本轮只修 Host、作者工具、构建入口、表现和入口文档；不改 Core／Data 规则、Freeze、Packages 或 ProjectSettings。

## 已接线

- ContentAuthoring `SchemaFields` 补齐当前 BaseGame 的正常 definition 类型与已使用字段。作者工具仍保留自身编辑提示；合法 JSON 不再因白名单落后而报未知 type／字段。正式内容加载与交叉引用仍以 `ContentPackageLoader`＋`ContentReferenceValidator` 为准。
- 正式构建 `PlayerBuildProfiles.ValidatePackage` 在复制内容前调用现有 `ContentPackageValidationFacade`；加载或引用失败即阻止出包。正式开局的 Scenario／可选 Roster／可选独立地图从 `Content/BaseGame/formal-opening.json` 读取，并核对 ID 存在，再写入构建中的场景 Host。编辑器 LevelTester 场景仍是工具台。本轮选择文件沿用当前参考 Scenario、测试名册和洞府地图，不代写首段剧情、钱包或人物数值。
- 人物 Profile 与物品图标／地面轮廓映射移至 `Assets/Resources/HostAppearanceMappings.json`。缺项安全回退至现有通用人物／包裹表现；新独特外观只需更新映射与资源。`HostMortalWorkPanel` 优先显示当前行动、时段、目标工区名称、床位状态和阻碍说明，不再暴露日程、床、移动权限等内部 ID。
- ART-ROOM-009 恢复垫、ART-GROUND-004 石地／矿区底色已作为候选素材接入；矿堆仍是独立对象。ART-UI-001 复用既有框图，在工作、背包、人物详情面板叠低透明九宫格边框；并加入命中、职业确认、玩家收获三种轻量程序反馈音。均不改变时间轴、伤害或资源结算。
- [273 TSV](273-current-art-asset-register.tsv) 是唯一可编辑美术资产真源；XLSX 已由现有导出器同步。上述三项是“已接入待验”，不是制作人验收。

## 新一段内容的最短制作流程

1. 明确首段体验、稳定 ID、人物／任务／事件／Surface 依赖和目标时长；规则或边界变化先走 Architecture／System Design／ADR。普通 Outdoor 用 WorldComposer／FineEditor 的 Continuous Surface 作者源；室内、洞府、独立地图才用 MapEditor／LocalPlaceEditor。不要用历史 Demo Runtime 制作正式玩法。
2. 从 `Content/BaseGame/Authoring/` 的作者源编辑；WorldComposer 的导出仅产生独立候选目录，不自动安装。核对候选 geography、Surface、Blueprint、Detail Patch 及其引用后，明确安装到 `Content/BaseGame/Data/`；其他内容编辑器保存的 Data JSON 同样检查稳定 ID 和文件归属。字段定义见 `Content/BaseGame/Data/SCHEMA.md` 与 [84 命名规范](84-chapter-content-naming-standards.md)。
3. 用 PackageBrowser 检查包；在 Unity 编辑器运行 `XianXia/Content/Validate BaseGame Package`。正式 Windows 构建也会重复加载／引用校验并拒绝错误内容。若加入新人物／物品外观，更新 `HostAppearanceMappings.json` 并确认资源可由 `Resources.Load` 找到。
4. 首段剧情与数值被确认后，在 `Content/BaseGame/formal-opening.json` 选择正式 Scenario、可选 Roster、可选独立地图。指定 Roster 时，其 entries 会覆盖 Scenario 的人物 spawns；要改真实开局人选，必须同时检查该选择。Scenario 自身决定 Chapter、Surface、初始资源与关系，不在选择文件复制数值。
5. 制作人从正式包人工体验：从开局进入首段、接任务／事件、完成主要行动、存档退出后读取并继续。若最近改动影响洞府掉落或医疗，只做对应窄范围复看；已验的基本出包、存档和整套民生无需重跑。

## 本轮人工验收焦点

1. PackageBrowser 打开当前 BaseGame 不再因正常字段报未知类型／字段；故意错引用的候选不应进入正式包。只在独立候选副本中试错，勿污染正式 Data。
2. 打开正式构建后确认开局与本轮之前相同；工作面板能读懂行动与目标，背包、工作及人物详情框的文字对比度、缩放不遮挡按钮。
3. 同屏看修炼垫与青色恢复垫、洞府石地和矿点：尺寸、透明边缘、平铺接缝、脚点与人物／掉落物遮挡；听命中、职业确认、玩家收获音量。ART-UI-001 其他正式面板的统一皮肤留待首段 UI 版式确定后再扩，不在未验收的样本上批量铺开。
4. 顺路复看 313 洞府书卷完整可见并可拾、医疗治疗后返工及一份携药去向；307／308 的选中、上床、搬运观感可同屏看。上述均未由代理启动 Unity 验证。

本轮基础编译与静态文件检查不是制作人人工验收。视觉素材由 Codex 内置 imagegen 生成石地与恢复垫；HUD 框复用仓库既有 `Batch_Codex_002` 候选。生成提示的关键约束分别为“俯视、低对比、可平铺石地、无矿堆物体”和“俯视、透明背景、青色恢复垫、单张 Sprite”。
