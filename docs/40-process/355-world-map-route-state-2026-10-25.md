# 355｜世界地图路线瞬间消失修复

> 2026-10-25。状态：Implemented / Producer Acceptance Pending / 未提交推送。

制作人反馈：世界地图右键目标后路线短暂显示，随即消失。现有路线由 `PlayerPartyWorldMotion` 直接提供，地图打开期间本地执行被输入锁冻结；因此正常规划成功后，路线应持续显示至明确取消、有效中断或完成抵达。路线失败应持续显示原因，不应只留下短暂视觉反馈。

本轮限定为 Host 输入／表现与现有旅行状态衔接：地图持有规划输入时，来自本地表现清理的 Stop 不得作为玩家主动取消整队路线；地图自己的“停止旅行”按钮继续直接取消。记录已接受的路线版本，若其它有效系统中断路线，地图持续显示“旅行已中断”而非静默抹线。荒村开局村界约束仍按既有规则拒绝越界目标，并在地图上显示原因。不得改变 Continuous Surface、exact WorldPosition、WorldTick 或 Snapshot 权威。

实施：`HostCommandBridge` 和 `HostMoveController` 在地图持有输入期间不把本地表现 Stop 当作主动取消整队路线；地图按钮仍通过 `PlayerPartySurfaceTravelService.Cancel` 明确停止。地图记住已接受的 `TravelPlanVersion`，如果有效外部系统使路线消失，显示持续可见的中断提示，并在 Editor／Development Build 记录一次版本、位置与主控，便于区分合法中断和其它问题。已有越界／不可达拒绝提示保持可见。

Host 基础编译 0 错误；未运行自动测试或自行启动 Unity。人工验收：解锁荒村后在世界图连续右键两个远处合法目标，第二条路线应持续显示；关闭地图后人物移动，再开图路线仍显示。地图打开期间的本地 Stop 不应清路线；地图“停止旅行”应立即清路线。开局锁定时右键村界外应持续显示“村界仍被封锁”，不会假装出发。若再次发生意外中断，请保留 Console 中 `[WorldMapTravelInterrupted]` 行供精确定位。
