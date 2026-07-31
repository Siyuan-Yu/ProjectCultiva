# 会话交接：2026-07-31（Demo 原型）

> 用途：换设备、换 Cursor 会话时**先读这篇**即可恢复上下文。  
> 仓库：https://github.com/Siyuan-Yu/ProjectCultiva  
> 本地路径（本机）：`D:\UnityProjects\XianXia`  
> Unity：**2022.3.6f1 Built-in**（ADR-0001）

## 30 秒摘要

项目已从纯策划进入 **Demo v0.1 原型开发**。当前可玩灰盒验证：

**白天劳动 → 指派工作产资源 → 完成主管任务 → 夜晚偷修长修为 → 管理暴露风险**

尚未做：战斗、突破、正式功法／灵根玩法、占领据点、正式 UGUI、正式美术。

最新提交：`f3c56cc`（Milestone 3.5 RTS 工作指派）已推 `origin/main`。

## 新设备开工 5 步

1. `git clone https://github.com/Siyuan-Yu/ProjectCultiva.git`（或已有仓库则 `git pull`）
2. 用 **Unity 2022.3.6f1** 打开工程根目录
3. 打开 `Assets/Scenes/Demo_v0_1.unity` → Play
4. 若场景缺组件／过旧：菜单 **XianXia → Build Demo v0.1 Prototype** 重建
5. 再读本文件下方「操作」与 `docs/40-process/42-devlog.md` 顶部 3～5 条

## 已落地里程碑（代码）

| 里程碑 | 内容 | 提交参考 |
|---|---|---|
| 开工 | 可替换 Sprite、三人控制、占位场景生成器 | 更早 commits |
| 灰盒 + 时钟 | 80×50 图、镜头、GameClock、只读时间表雏形 | `3e224f9` |
| M3 生活循环 | 每日任务、资源、工作区、主管愤怒（只显示） | `dda70b8` 内 |
| M4 秘密修炼 | 灵地、Cultivating、修为、暴露、敛息草 | `dda70b8` 内 |
| 时间表网格 | 24h×三角色可点改（**测试可改**，正式应锁）+ 地块悬停灵气 | `dda70b8` |
| M3.5 工作交互 | 右键工作区下达 Working；空地移动；仅 Working 产资源 | `f3c56cc` |

## 当前怎么玩（验收操作）

- **选择**：左键；Shift 多选
- **工作**：选中后**右键森林／草药区／农田** → 前往并持续工作产木／药／粮
- **移动**：右键空地 → 只移动并取消工作
- **时间**：空格暂停；1／2／5 倍速
- **修炼**：角色到东南**隐藏灵地**，`C` 开始／`X` 停止／`G` 用敛息草
- **课表**：侧栏「课表」→ 点格子循环 睡→起→工→饭→闲（测试用）
- **地块**：鼠标悬停看属性能量／灵气／浓郁
- **HUD**：顶栏 + 左右侧窄条开关面板；`Tab` 全关；默认面板收起，中间可点选

## 关键代码入口

| 系统 | 路径 |
|---|---|
| 场景生成 | `Assets/Editor/DemoPrototypeBuilder.cs` |
| 时间 | `Assets/Scripts/Runtime/Time/` |
| 工作／区域 | `Assets/Scripts/Runtime/World/` |
| 任务／资源 | `Assets/Scripts/Runtime/Tasks/`、`Resources/` |
| 修炼／暴露 | `Assets/Scripts/Runtime/Cultivation/` |
| 愤怒 | `Assets/Scripts/Runtime/Obligation/` |
| 输入／单位 | `Assets/Scripts/Runtime/Input/`、`Presentation/` |
| HUD（IMGUI） | `Assets/Scripts/Runtime/UI/DemoPrototypeHud.cs` |
| 配置资产 | `Assets/Configs/` |

## 重要设计约束（别走偏）

- 原型 **IMGUI** 调试 HUD，不是正式 UGUI（已知需后期替换）
- 时间表正式版应锁定；现 `ScheduleService.allowEditForTesting = true`
- M3 旧逻辑「站在区内自动打工」已废止；必须 **Working** 才产资源
- 暴露／愤怒本阶段**只显示数值，不惩罚**
- 角色 Visual 缩放与碰撞分离；PNG 可原位替换（见 `48-demo-v0.1-minimum-art-integration.md`）

## 建议下一步（未开工）

按 Demo 闭环优先级，尚未实现：

1. 正式 UGUI（替换 IMGUI）
2. 突破事件最小脚本／第一次突破
3. 战斗（世界地图内 RTS 暂停战斗）
4. 敛息／隐藏修士玩法加深
5. 夺府／占领控制核心

不要擅自开战斗／占领，除非用户明确进入下一 Milestone。

## AI／人开工必读顺序

1. **本文件**
2. `AGENTS.md`
3. `docs/40-process/42-devlog.md` 顶部若干条
4. `docs/40-process/45-demo-v0.1.md`
5. 需要设计细节时再进 `docs/20-systems/` 与 `00-overview.md`

## 旧交接

策划向长文仍见：`44-session-handoff-2026-07-30.md`（方向与设定表仍有效；**工程进度以本文件与 devlog 为准**）。
