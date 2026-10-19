# 人物骨架与美术源文件当前交接（2026-10-18）

> 状态：实施完成／制作人人工验收待完成；本轮获制作人授权“全部封板并上传”。此处“封板”是代码与文件版本记录，不将未做的 Unity 现场验收写成 Producer Accepted。

> 2026-10-18 后续反馈：制作人复看人物动画认为“还可以”，同意进入 [298 黄村环境同屏样板](298-huangcun-environment-visual-slice-2026-10-18.md)。这是当前镜头可推进的观感反馈；未逐项复核的身份、倍率与正式 Build 仍不记为全面 Producer Accepted。

## 当前实施状态

- 正常人物表现默认使用 Unity `HumanCommonRig`：12 套 Skin 覆盖当前 25 个名录身份。头、躯干、双臂、腿与工具／武器是可替换部件；角色脚点、选中、碰撞和真实位置仍属于原 `EntityView`，并未引入 Spine 运行依赖。四正向与四斜向视角、侧向袖子、实际位移步态、NPC 工作动作、工作工具与攻击武器挂点均已接线。
- 睡眠、弥留和死亡使用卧姿；床的前后层级让头露在被面上，闭眼头是独立差分。人物下方血条、卧姿选中范围及人物之间的终点避让由 Host 表现处理。右键连续改目标从当前精确脚点规划，新路线成功后才替换旧路线；失败点击保留现有路线。
- 实施路径与问题依据见 [280](280-animation-binding-audit-and-fix-2026-10-18.md)～[296](296-right-click-repath-position-continuity-2026-10-18.md)，尤其 [293 八向](293-character-eight-direction-movement-2026-10-18.md)、[295 侧臂](295-side-facing-arm-presentation-2026-10-18.md)、[296 右键改路](296-right-click-repath-position-continuity-2026-10-18.md)。角色图片源图与 SHA 分别见 `ArtSource/Batches/Batch_ModularRig_001`～`005`，运行图在 `Assets/Art/Characters/Resources`，资产族状态以 [273 TSV](273-current-art-asset-register.tsv) 为准。

## 美术源文件清理

- `ArtSource/Reports/artsource-slimming-2026-10-17.tsv` 逐文件记录 122 个退役副本的路径、字节数、SHA-256 及保留源或替代路径。旧逐帧批次的重复 Candidates、机械切出的错误 Frames、单图深浅底 Previews、重复 Inbox 原图已从工作区移除；生成原图和必要批次记录仍保留。旧逐帧 Unity 运行回退资源没有因 ArtSource 清理而自动删除。
- 三个旧根目录 ZIP 指针（`ArtSource.zip`、`Assets.zip`、`Content.zip`）及被误纳入版本控制的 Python `__pycache__` 文件也已移除。压缩包、预览及机械切帧的退役只整理源文件，不改变 Core／Data／Content、Freeze、ProjectSettings 或 Packages。
- 文件级盘点见 `ArtSource/Reports/current-art-inventory.tsv`，项目美术相关路径入口见 `ArtSource/Reports/project-wide-art-file-list.md`。2026-10-16 的 `ArtSource/ArtSource_文件列表_2026-10-16.md` 只是历史快照，不能当成现有文件列表。
- 2026-10-18 重新导出的文件盘点覆盖 839 个 ArtSource/Assets 美术相关文件；项目美术相关路径列表覆盖 1132 个文件、552 张图片。这些是脚本的文件级盘点数量，和 273 的资产族完成/验收数量不同。122 项瘦身台账的退役路径均已不在工作区，留存源文件路径及 SHA 与台账一致。

## 验收与边界

按仓库约定，本轮不启动 Unity、不编写或运行自动测试。基础编译与最少文件完整性检查只能证明实施可交接，不能证明画面已通过制作人验收。现阶段应在 LevelTester 复看八向头颈/侧袖、工具贴手、床上头与卧姿选中/血条、多人避让，以及连续右键改目标在障碍旁和倍速下是否从当前脚点平顺续走。上述项在收到明确现场结果前均为 **Producer Acceptance Pending**；不据此自动启动新的美术批量生产或玩法阶段。

本轮 `XianXia.Unity` 离线基础编译为 `ALL_OK`；5 个改动的 JSON 清单可解析，122 项瘦身台账的留存路径与 SHA-256 对应。未运行自动测试，也未用编译结果代替 Unity 人工验收。
