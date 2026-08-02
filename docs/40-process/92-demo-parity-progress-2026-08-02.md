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
| 满密铺砖 | `HostDemoTileMap.stride=1`（可选） |
| 日课三资源数值再对齐 | 可按 Demo DailyTasks 微调 |

## 同日后续（已并入 [97]）

打断／第一章全弧／RTS 引导已交付；手操入口改为 **`DemoParityHost`**（非默认 `PlayableHost`）。见 [97](97-ch01-playable-arc-and-ux-delivery-2026-08-02.md)。
