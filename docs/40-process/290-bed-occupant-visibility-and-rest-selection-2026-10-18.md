# 床上头像可见、卧姿选中圈与血条定位

> 状态：Implemented／待制作人人工验收 | 2026-10-18 | 前置：[289 卧姿与床分层](289-character-rest-pose-bed-layering-and-eye-policy-2026-10-18.md)

## 截图与已确认原因

制作人第二次现场截图显示：三张床睡眠人物完全不见；选中圈落在床外；血条贴近被面／床尾。289基础编译通过并不代表这三个运行表现已验收。

反馈原图归档：[睡眠人物被盖住](../../ArtSource/Reports/HumanCommonRig_Feedback_20261018/12-sleeping-actor-hidden-feedback.png)、[卧姿选中圈与血条偏位](../../ArtSource/Reports/HumanCommonRig_Feedback_20261018/13-rest-selection-vitals-offset-feedback.png)。

- 床上头像仍与躯干在同一人体SortingGroup，整张床底图和被子前景之间仅有一个人体外部层级；头像未获明确的被子上层。截图证明本机运行结果不能只靠289推断。
- `EntityViewSpawner`的SelectionRing初始固定在人物根脚点下0.15；`EntityView.ApplyDepthSort`仅更新order，不读卧姿骨架或床，实际人体移动到枕位后圈仍留在原处。
- 血条基于卧姿骨架最低点定位，在床上未考虑床尾前景轮廓，容易画在被面或床尾，且看不出与被中人物的归属。

## 本轮统一实施范围

1. 床上躯干／肢体继续处在整床底图B和被子前景B+2之间；仅头部单独提升到B+3，沿用实际骨骼头Sprite、镜像、颜色与姿态，不新生图。离床、醒来、倒地及Rig停用时取消头像上层，避免双头或幽灵头。
2. 选中圈在卧姿时围绕实际人体可见范围，床上置于被子前景之上；直立时恢复原尺寸与脚点。点击／Collider仍按人体范围，不把床预约或人物世界位置改成图像坐标。
3. 床上血条锚定在人物所占床的可见外边缘下方，随床旋转后的实际Sprite bounds与世界缩放；地面卧姿维持人体下缘，直立维持原脚点。动态床框架失效回退，不悬挂旧床位置。

只改Host表现及直接接线，更新开发日志。按AGENTS约定仅做基础Unity程序集编译和最少静态检查；不启动Unity、不新增或运行自动测试、不提交。人工现场为Producer Acceptance。

## 实施结果

- 人物头骨节新增仅管理原`HeadSprite`子节点的嵌套SortingGroup；有效床姿启用`sortAtRoot`并设为B+3，绕过躯干B+1组，在被子B+2之上；卧姿退出即停用该头组，恢复原整架排序。Unity 2022.3的[SortingGroup官方说明](https://docs.unity3d.com/cn/2022.3/Manual/class-SortingGroup.html)明确`Sort At Root`会忽略父组，在场景根级与其他Renderer排序。未复制头Sprite，也未增加表情贴图。
- `EntityView`每帧卧姿时按本帧全部人体bounds对齐并缩放选中圈，床上环位于被子上层；直立重置原脚点、尺寸和层级。视觉Collider继续使用同一人体bounds。
- 点选及NPC交互按射线与人体所在的世界XY平面相交，再判断实际人体bounds。卧姿不再落到留在床外的旧世界根脚点球形兜底；直立仍保留原兜底。
- 血条在有效床上取床底图渲染后的世界bounds下缘与人体下缘中较低者，随被子／床的四方向拟合及镜头缩放；离床继续用人体下缘，直立仍用脚点。

## 验证与状态

- `pwsh -NoProfile -File tools/offline-compile.ps1 -Only XianXia.Unity`：154个Unity源文件，`ALL_OK`。静态核对头骨节退出床姿时停用独立SortingGroup、床B／躯干B+1／被B+2／头B+3／标签B+4／选中圈B+5、卧姿点击不回退到旧根脚点；未启动Unity，未新增或运行自动测试。截图归档仅用于人工对照。
- **Implemented**：本轮Host表现与直接选取接线。**Producer Accepted**：等待制作人现场验收。**Committed／Sealed**：未提交／未封板。当前仍使用睁眼头部素材；闭眼切件保持289的Proposed状态。

## 简短人工验收

1. 同截图让阿石、阿土入床：枕头上应出现对应人物头像，身体在被子下；切换四个床方向、住宅／医疗／监牢床时不消失、不双头。睡醒后头回到普通人体层。
2. 选中卧床与地面倒地人物：绿圈围绕完整卧姿人体，不留在床外旧脚点；点击枕头或被中的人体能选中，点击原旧脚点的空地不应误选。
3. 卧床时血条在床下缘之外；起身走路后回到稳定脚点下方，地面倒地时贴人体下缘。缩放镜头时与人物一起变化。
