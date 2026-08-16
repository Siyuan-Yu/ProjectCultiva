# 133 · 功法／斗技轻量编辑器＋非正式内容清理（2026-08-16）

> 状态：**已落地**｜日期：2026-08-16  
> 工程：`ExternalTools/ContentAuthoring/ManualArtEditor/`｜启动：`启动-ManualArtEditor.cmd`

---

## 1. 编辑器

- 双页：功法（`cultivation`）／斗技（`combatArt`）
- 可编：基础字段、`mastery.tiers`（绝对值）、`mastery.breakthroughs`（进度＋材料）
- 保存写回 `Content/BaseGame/Data/Cultivation`／`CombatArts`
- Shared：`SchemaFields`／`ContentPathRules` 已认 `combatArt`、`mastery`、`teachesArtId`

## 2. 清理

| 项 | 处理 |
|----|------|
| `art_spirit_strike`（无道具入口的早期被动样例） | 从 `combat_arts.json` 删除 |
| `RegisterBuiltinCombatArts` 大段硬编码熟练表 | 缩成仅无 JSON 时的薄保底（裂爪／开山） |
| 旧功法补 mastery | **未做**（缺表时 Core 自动生成缺省；正式数值用编辑器填） |

保留：`jiang_lao`／`dongfu`／青云／木语／吐纳（测试与机缘仍引用）。

## 3. 用法

1. 双击 `启动-ManualArtEditor.cmd`（首次会 publish）  
2. 选功法或斗技 → 改档位／突破 → 保存  
3. Unity 重新 Play  
