# 132 · 熟练度配置化：每档绝对值（2026-08-16）

> 状态：**已落地**｜日期：2026-08-16  
> 前置：[131](131-skill-mastery-study-ritual-2026-08-16.md)

---

## 1. 一句话

功法／斗技熟练效果改为 **JSON 每档写死绝对值**（不连乘）；突破进度与材料也按定义表配置。缺表时回落缺省生成。

---

## 2. 契约

- 功法：`cultivation.mastery.tiers[].cultivationSpeed`
- 斗技：`combatArt` 新类型＋`mastery.tiers[].damageAttackMult`／`attackBonusPercent`
- 突破：`mastery.breakthroughs[]`（`from`／`to`／`progressRequired`／`costs`）
- 属性 `grantedModifiers`：**不**随熟练缩放

样例：`Cultivation/cultivation.json`（将老／洞府）、`CombatArts/combat_arts.json`

---

## 3. 验收

1. 学会将老残谱：入门打坐 +8；冲击小成后 +10  
2. 裂爪进入门 200%、小成 220%  
3. 改 JSON 数值重进 Play 生效；无 `mastery` 的旧功法仍可玩  
