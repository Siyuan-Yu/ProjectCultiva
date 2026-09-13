# CW-U1 连接补齐至 CW-U4 连续实施记录

日期：2026-09-13。基线：`4277654e85f9854ad03800d1d36e88e8750e733f`，开始时工作区与暂存区均干净。

制作人确认 CW-U1 当前正常玩法人工验收通过。本轮明确授权连续执行 C0→C4，中间不等待逐轮人工验收；覆盖 ADR-0035 的旧逐阶段等待要求。新增行为仍待合并人工验收，不扩大 U1 已验收范围。

| 检查点 | 当前状态 | 改动／检查 | 本地提交 | 剩余边界 |
|---|---|---|---|---|
| C0／U1 连接补齐 | 检查点准备中 | 成员事务、创建边界、派生位置、存档严格校验、共享命令接线；Core 456／Data 77／Host 143 编译通过 | 提交后记录 | 旧档无 Squad 字段仅做迁移；独立遭遇尚未实现 |
| C1／U2A | 未完成 | 同源独立战场、个人锚点、战术时钟与稳定态存读档 | 无 | 待实现 |
| C2／U2B | 未完成 | 统一两队入口、多目标战斗与地图攻击退役 | 无 | 待实现 |
| C3／U3 | 未完成 | 有限关系介入及同场名单追加事务 | 无 | 待实现 |
| C4／U4 | 未完成 | 整合与旧档兼容、合并验收路线 | 无 | 待实现 |

## 检查纪律

仅使用现成 `tools/offline-compile.ps1`，分别确认 Core／Data／Unity Host 实际源文件数量与结果。未启动 Unity，未运行自动测试或 Bake。
首轮 C0 编译发现 `WorldAgentPresence.UsesHex` 名称错误；已按实际 `UsesHexPresence` 修正，必须重编成功后才形成阶段检查点。后续程序集当次因旧 Core 产物缺少新增符号失败，不计为通过。

## 下一动作

C0 最后定向 diff 后提交，再接 C1。同源独立场地尚未替换当前 Continuous 入场，不能把 C0 或已有 helper 宣称为 C1～C4 完成。

## C0 接线

- `SquadBoard.Register` 完整预验证；离队复用 Transfer 的队长、反向成员、空 Army／stack 清理。Army 解散先验证所有成员及战斗锁；拒绝不先清 ArmyId 或提交位置。正常 Create 最多六人，旧超限仅显式 snapshot import。
- 真实 `EntityStore.CreateCharacter/CreateNpc` 完成时建立 singleton；可见列表刷新不再写入成员权威。正式恢复在候选 World 校验 Squad 全覆盖、唯一身份、队长、命令目标及 Army 映射，控制绑定预检成功才切换 Session.World；新版不回退旧 DTO 推断。
- 共同命令持有 executor kind、目标人物、revision；玩家跟随消费目标，Army 消费既有 WorldMotion 路线并在启动／停止／替换时更新命令。Core／Host 的个人日程使用同一 ownership predicate，取消仅限 Schedule source，释放占位；普通旅行只在编组成功后取消。
- 弥留／尸体保持成员关系；SyncMember、近场 Army presenter、residual predicate 不再让组织身份拖走残留者。全部失能时停止 Army 行程，不解散队伍。已有 precise residual 不重复锚定。
- 开发诊断原 FormalArmyNearField 输出补充 command、revision、共同目标；未新增面板、测试或 Unity 调用。
