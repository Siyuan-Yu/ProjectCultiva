# ADR-0019：Dead 与 Removed 生命周期分离

- 状态：**已采纳**
- 日期：2026-07-31
- 决策者：项目负责人（Freeze v0.2）

## 背景

需区分永久死亡与“不再参与模拟”的清理／离场。

## 决策

- `Incapacitated` ≠ 死亡。  
- `Dead` = 永久死亡。  
- `Removed` = 独立：临时实体清理、离开模拟范围等。  
- **禁止** Dead=Removed；Removed 不自动等于死亡。  
- `Recovered` 为从 Incapacitated 回到 Alive 的结果，不作长期并行枚举抢戏。

## 影响

见 `33` v0.2 §13、`34`。
