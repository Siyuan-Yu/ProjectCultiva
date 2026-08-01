# Demo 手感对齐进度（2026-08-02）

> 缺口真源：[91](91-demo-v0.1-to-formal-gap-audit.md)  
> 依据：[49]＋[32]；不复活 Demo Runtime；不补 Out 项（攻击／可改课表／真战斗等）

## 本轮已完成

1. **缺口审计** [91]  
2. **PKG-A 2D 表现**：Sprite EntityView／XY 正交／中键拖／选中环／Sprite 区片地图（告别 Capsule／Quad）  
3. **PKG-B 命令**：Stop／W＝Labor／C／X／G；右键工区产资源；右键灵地修炼；移动前中断  
4. **暴露**：昼／夜／近主管（Cultivate）  
5. **HUD**：资源含粮／敛息草；课表只读；头顶活动字  
6. EditMode：**179/179**

## 仍待补（文档内 Partial）

| 项 | 下一步 |
|---|---|
| 满幅 80×50 Demo Prefab 铺砖 | 复用 `Assets/Prefabs/Environment/Tiles` 铺满 Demo 布局 |
| 每日配额三资源任务线 | Content 日任务对齐木／粮／药 |
| PKG-E 一比一关验收报告 | [49]§5 清单自动化＋正式验收文档 |

## 手操（当前 PlayableHost）

默认 `base:scenario_ch01_reference`；`F6` 五面板＋课表；中键拖图；`W/S/C/X/G`；右键地面／工区／灵地。
