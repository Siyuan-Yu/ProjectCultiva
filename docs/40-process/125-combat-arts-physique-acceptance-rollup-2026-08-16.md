# 125 · 战斗／斗技／体魄拆分验收收束（2026-08-16）

> 状态：**本轮手操验收基本通过**｜日期：2026-08-16  
> 相对提交：`0135812`（将老／洞府／秘籍）之后 → 本轮 `main`  
> 飞书：https://my.feishu.cn/docx/W7iAdwYigo0PxlxJbaPcOKnjnXb  
> 上一轮：[124 将老／洞府／秘籍](124-jiang-lao-cave-manual-rollup-2026-08-15.md)

---

## 1. 一句话

把「洞府威胁可打」做成可手操竖切：互砍有特效与掉血、体魄与生命拆开、斗技可学可装可按／点 1–6 释放；并去掉入定静默保底学青云诀。

---

## 2. 交付对照

| 主题 | 做什么 | 入口／文件 |
|------|--------|------------|
| **敌对姿态** | `hostile`＝免确认／偏红／头顶「·敌对」；无标仍双确认 | `HostNpcInteraction`／`HostNpcContextMenu` |
| **近战互砍** | 走近自动互砍；挥砍弧＋闪白；交战 Hold；S／右键地面脱离 | `HostNpcMeleeAssault`／`HostMeleeStrikeVfx`／`MeleeCombatService` |
| **主管府突击** | 去掉每秒固定伤；靠近后按近战间隔／攻−防/2 拆耐久；破门站占不变 | `HostControlCoreAssault`／`ControlCoreService.ApplyStrikeFromAttacker` |
| **生命／护盾条** | 头顶＋概况「生命」；炼气先扣灵力护盾 | `HostCombatVitalsBars`／`CombatDamageRules` |
| **白名单修复** | `CombatVitals`／`CombatArts`／`EncounterLink` 进 Entity 白名单（否则互砍哑火） | `Entity.cs` |
| **体魄≠血条** | 新 `AttributeId.Physique`；`MaxHp` 显示「生命」 | `HostAttributeLabels`／角色 JSON |
| **斗技栏** | 选中底栏右侧竖列 1–6；角标＋名称；左键点放；键 1–6 共用 | `HostFormalHud`／`HostCombatSkillBar`／`HostCombatArtsPanel` |
| **数字键独占** | 劳动等调试键不再绑 0–9 | `HostCommandBridge`／场景序列化 |
| **功法展示** | 未学统一「还没有学功法」；**入定不再静默学青云诀** | `CultivationAttemptGate`／`HostCultivationPanel`／`ManualShortName` |

---

## 3. 操作流（制作人手操）

```text
选中己方 → 右键敌对残影 → 攻击（免确认）
走近 → 互砍特效／飘字／头顶生命条下降
底栏右侧 1–6：看装备斗技；点主动格或按键释放
「斗技」面板：学秘本、装到 1–6
境界／概况：未学功法显示「还没有学功法」（不会再因打坐保底变成青云诀）
学功法：将老／洞府秘籍 → 背包使用 → 显式研读
```

---

## 4. 规则摘要

| 规则 | 现行 |
|------|------|
| 生命 | `MaxHp`＋`CombatVitals.CurrentHp`；UI 称「生命」 |
| 体魄 | `Physique`；肉身属性，非血条 |
| 承伤 | 炼气+：灵力护盾 → 生命 |
| 斗技装备 | 最多 6；主动技可释放；被动只加成 |
| 学功法 | 秘籍／任务／outcome；**不**经 Cultivate Gate 保底 |
| 残影 | `hostile`＋`cave`／内室；击败可写 encounter 旗 |
| 主管府 | 近战间隔＋攻−防/2 拆耐久（非固定每秒伤）；破门站占 |

**明确未做（本轮后）：** 敌人主动寻仇；半自动斗技；WorldGraph；体魄进伤害／门槛公式细化；主管 NPC 被击败后的剧情／权限联动（仍靠打府占领）。

---

## 5. 下一步建议

1. 手操再验：突击主管府见属性伤／挥砍特效；破门站满占领；右键主管走正式互砍  
2. 遭遇／任务内容装配（洞府清残影等）  
3. 敌人进敌对范围主动开打（若要）  
4. WorldGraph／内室编辑器 backlog（[113](113-world-graph-local-map-architecture-revision-v0.1.md)）
