# ADR-0008：ArmyGroup 使用群体数据与视觉代理

- 状态：**已采纳**（**2026-08-22：修士 Army 部分被 [ADR-0024](ADR-0024-real-cultivators-and-army-strategic-model.md) supersede／收窄**）
- 日期：2026-07-31
- 决策者：项目负责人（架构冻结收口）

## 背景

世界需要凡人反抗与势力冲突的规模感，但不能做成全面战争式千人微操。

## 选项

**A. 千人完整 AI 实体** — 与范围纪律冲突。  
**B. 完全抽象战报、无军队对象** — 难与修士机制交互。  
**C. ArmyGroup 群体数据 + 入镜有限视觉代理 + Core 结算**  

## 决策

选 **C**。军队不是当前核心玩法，但架构预留 ArmyGroup。

## 2026-08-22 修订（ADR-0024）

| 场景 | 模型 |
|------|------|
| **修士战略 Army** | 真实 Character + Army 载体（见 ADR-0024／[2A](../../20-systems/2A-factions-armies-diplomacy-and-capture.md)）— **本 ADR 对此部分 superseded** |
| **凡人大军／大规模非修士军队** | **仍适用** ArmyGroup 聚合 + 视觉代理 |
| **CultivatorPopulation 代表修士战争** | **不再**作为正式真源 |

原 ArmyGroup 聚合决定**继续适用于**需要聚合模拟的凡人群体军事力量；**不**再代表修士组成的战略 Army。

## 影响

- 见 `33` §10／§11。主控仍是 30～50 修士。  
- 修士 Army 见 ADR-0024；凡人 ArmyGroup 边界不变。
