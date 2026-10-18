# 人物闭眼头部差分：本轮实施范围

> 2026-10-18｜Design Confirmed / Implemented / Producer Acceptance Pending / Not Committed。制作人确认保留平时的简化五官，睡眠等卧姿切换闭眼头部。前置：[289 卧姿与眼睛取舍](289-character-rest-pose-bed-layering-and-eye-policy-2026-10-18.md)、[290 床上可见与选取](290-bed-occupant-visibility-and-rest-selection-2026-10-18.md)。

## 唯一实施指令

在已有 12 套 HumanCommonRig Skin 上制作正面与侧面闭眼头部差分：正常清醒时原头像不变；已进入真实睡眠、弥留或死亡视觉状态时，当前所用正面／侧面头切到同一人物的闭眼静态差分；背面无眼睛不另造差分。被搬者的失能状态沿用闭眼，清醒搬运状态不得无故闭眼。变化只在 Host 表现和新版本化资源，复用既有骨架、头部 pivot/布局、床层级/选取/血条，不移动 Entity 世界根、Collider、床位或 Core 生命/睡眠判定。静态差分不引入眨眼、口型、逐帧脸部动画或新玩法。

项目原头图保持不覆盖。新闭眼图必须对照本人原图，保留发型、脸型、服饰、alpha 轮廓与方向；正面与侧面覆盖全部 12 Skin，缺资源须有明确诊断且安全回退原头像，不得让整架骨架失败。出床/苏醒/救治清除闭眼状态，切换旧驱动时不把骨架闭眼叠到旧图。

只作基础编译、必要资源路径与文件完整性检查；不启动 Unity，不编写或运行自动测试。最终只把代码与资源记为 Implemented，现场视觉、Build 与性能仍由制作人验收。Core／Data／Content、场景、Packages、ProjectSettings、Freeze 不改，不自动提交或推送。

## 实际实施与资源

- 使用 Codex 内置 image_gen 对原透明图集做闭眼编辑，保留原图与其 GUID，不覆盖。16 张编辑源图及生成方式、源/导出文件 SHA-256 见 [Batch_ModularRig_003 manifest](../../ArtSource/Batches/Batch_ModularRig_003/manifest.json)。从编辑图按原布局无缩放裁出 12 张正面及 12 张侧面闭眼头，置于 `Assets/Art/Characters/Resources/ClosedEyes/`，每张有独立 Unity `.meta`。源编辑图包含完整图集供后续修订，运行时仅加载 24 张小头像，不加载整张编辑图。
- `HumanCommonRigView` 沿用原头像布局的宽高、pivot、PPU 和可见网格生成闭眼 Sprite。清醒/背面继续用原头像；睡眠、弥留和死亡使用闭眼头。被搬者只有本身失能时闭眼。取图只改变现有 `HeadSprite`，沿用 290 的床上头部 SortingGroup；切姿态时不另建头像节点。缺失或尺寸不符时按资源 key 去重告警并回退本人睁眼头像，不令整架初始化失败。
- `273` 的 ART-ANIM-005 行登记新资源与待验状态。此次不产生新动作帧、眼球追踪或正式装备/睡眠规则变更。

## 核对与边界

`pwsh -NoProfile -File tools/offline-compile.ps1 -Only XianXia.Unity`：154 个 Unity 源文件基础编译 `ALL_OK`。静态核对 24/24 运行头像的尺寸与原布局匹配、SHA-256 与 manifest 匹配，24 个新 PNG GUID 各不重复；目检编辑图的全部正/侧可见眼睛已闭合。未启动 Unity、未编写或运行自动测试，也未做实际 Build、性能或运行画面验收。

生成式编辑会使眼周以外的头发/脸部笔触有细微变化；虽然裁框和 pivot 未变，清醒↔闭眼瞬间是否视觉跳变仍须在正常游戏缩放下人工确认。原 `LegacySprite` 预览/资源失败回退沿用旧图片，没有新闭眼差分；本轮不把旧路径当成正式骨架验收。原人物/床 PNG、Core／Data／Content、场景、Packages、ProjectSettings、Freeze 均未修改；未暂存、提交或推送。

## 简短人工验收

1. 重新进入 LevelTester，让阿石、阿土、阿禾在床上真正入睡：只露对应人物的闭眼头，身体仍在被子里；起床后眼睛恢复，头像不跳位置或改变身份。
2. 抽查主角/一名同伴与江老的地面睡眠或弥留/死亡：正面及左右侧头像闭眼，救治/醒来睁眼；普通走路、工作和清醒被搬者不误闭眼。
3. 选中卧床/倒地者并缩放镜头：290 的选中圈、血条和床上头像层级保持正确，没有双头或睁闭同时出现。
