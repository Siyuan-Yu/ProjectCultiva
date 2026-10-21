# 内容生产前接缝修复（2026-10-21）

> 状态：Design Confirmed（制作人要求“都做了都修了”）／Implemented／Producer Acceptance Pending；后续制作人授权与 NPC AI 返修一并提交上传，Git 结果以仓库历史为准。本页接续 [314 静态复核](314-pre-content-production-readiness-review-2026-10-21.md) 与 [315 第一轮接线](315-content-production-prep-implementation-2026-10-21.md)。不改变首段剧情、开局数值、世界规则或已封板边界。未启动 Unity，未编写／运行自动测试；编译不能代替画面验收。

## 已修的内容生产风险

- CharacterNpcEditor 现在显示并保存 `Physique`，更新 `baseAttributes` 时保留未由表单管理的字段；保存 Scenario 出场不再暗中覆盖测试名册，导出名册仍须明确点击。QuestEditor 可编辑多条 `deliveryRequirements`，保存时不再只留下第一条。
- 正式开局改选独立 `base:roster_formal_reference`。它是当前测试名册的 25 人快照，保留既有正式开局人选和数值；后续 `base:roster_level_tester` 作者导出不会悄悄改变正式包。`formal-opening.json` 仍用参考 Scenario、洞府地图，剧情和正式数值由首段内容设计决定。
- 构建在现有完整内容加载／引用校验之外，再校验**实际生效**的 Scenario＋可选 Roster：人物定义、势力归属、职业、地点、Surface 的精确锚点及关系端点。Chapter 激活也依据生效名册选主角；没有可招募 NPC 不再阻止合法的首段开局。
- WorldComposer 的兼容候选导出现在发布非荒村的已链接 WorldSite Blueprint 对象，包含旋转后的格位、尺寸、朝向、地点中心与支持的运行字段。荒村已封板的精确迁移路径保持原样；无法无损映射的 `assetRef`／`contentRef` 会明确拒绝导出，避免静默丢对象。仍只写独立候选目录，安装前须核对候选和引用。
- `HostAppearanceMappings.json` 支持按人物 ID 配置部件底图、侧头图集／列数／索引、背篓、静态及睡姿回退。主管作为现有素材的配置实例；新的独特 Skin 只需相同命名的 Resources 素材与映射，不必再扩人物 switch。资源缺失、重复 ID、无效 Profile／地面类别在内容菜单和构建前报错。药草显式映射为 `herb` 地面轮廓。
- 11 个作者工具 EXE 已重新发布到 `Apps/`；启动脚本在源码晚于 EXE 时要求先执行完整发布，不再默默启动旧版。`Apps/` 是本机生成物，不是 Git 真源。
- [273 美术登记 TSV](273-current-art-asset-register.tsv) 校正人物／物品已配置映射及 307 撤掉的地点石牌叙述；保留“已接入待验／未验收”，并按既有单向导出器同步 XLSX 查看版。

## 当前人工验收（仅新增／最近改动）

1. 用副本在 CharacterNpcEditor 打开含 `Physique` 的人物，只改一项再保存，确认其他属性保留；Scenario 保存后检查正式参考名册未变化。QuestEditor 打开多交付物品任务，保存后确认每条仍在。
2. 用正式构建进入参考开局，确认人物、章节主角、关系、钱包和地图与此前一致。首段真正改人选时，同时改 `formal-opening.json` 指向的名册与 Surface 锚点；不要只改 Scenario 的 `spawns`。
3. WorldComposer 在独立目录导出一个非荒村的带旋转 Blueprint 候选，核对对象位置／尺寸／地点与界面预览一致，再决定是否安装。现有荒村候选也做一次差异核对。
4. Unity 内容校验菜单确认主管配置可加载；查看主管前／侧、睡姿及地面药草。再同屏复看恢复垫、石地矿区、HUD 框、掉落书卷、床边治疗及三种轻量反馈音的观感。美术素材和音量仍是**已接入待验**，未经制作人现场确认不写 Producer Accepted。

基础检查：ContentAuthoring 解决方案、11 个 Release 发布和 Unity 8 个程序集离线编译通过；现有 MapEditor 的 4 条 nullable 警告未在本轮改动。新增 JSON、名册锚点数及 `git diff --check` 做静态核对。未改动工作区预存的 `Packages` 音频依赖和 313 制作人状态更新；初次实施时未自动提交，后续按制作人明确授权提交上传。
