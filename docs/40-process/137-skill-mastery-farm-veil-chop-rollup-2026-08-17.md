# 137 · 熟练度／纱衣／田区／砍树收束（2026-08-17）

> 状态：**已落地；熟练冲击确认窗已手操验收**｜日期：2026-08-17  
> 专条：[130](130-local-place-editor-usage.md)／[131](131-skill-mastery-study-ritual-2026-08-16.md)／[132](132-skill-mastery-config-absolute-tiers-2026-08-16.md)／[133](133-manual-art-editor-and-cleanup-2026-08-16.md)／[134](134-spirit-veil-ranged-normal-attack-2026-08-16.md)／[135](135-world-object-inspect-and-tree-chop-2026-08-16.md)／[136](136-farm-field-zone-labor-2026-08-16.md)  
> 上一轮：[129](129-world-graph-host-travel-scene-isolation-2026-08-16.md)  
> 飞书：https://my.feishu.cn/docx/WaaNdNf2poof5qx2deVcFfKrnwb

---

## 1. 一句话

本轮在 WorldGraph 出行之后，落地功法／斗技蓄势研读与熟练度、斗气纱衣、世界物况栏与砍树掉木、田区自动农作，并修正幽灵农田检视与砍树产量；**熟练冲击成功率只在确认窗显示，结果弹窗不再重复。**

---

## 2. 交付对照

| 主题 | 做什么 | 专条／入口 |
|------|--------|------------|
| **地点编辑器** | LocalPlaceEditor；场景地点登记 | [130](130-local-place-editor-usage.md) |
| **蓄势研读＋熟练** | 黄条参悟；学习成功率；灌注／冲击；Snapshot | [131](131-skill-mastery-study-ritual-2026-08-16.md) |
| **熟练配置绝对值** | `mastery.tiers`／`breakthroughs`；`combatArt` JSON | [132](132-skill-mastery-config-absolute-tiers-2026-08-16.md) |
| **功法／斗技编辑器** | ManualArtEditor；清理非正式斗技样例 | [133](133-manual-art-editor-and-cleanup-2026-08-16.md) |
| **斗气纱衣** | 筑基远程普攻姿态；F2；NPC 交战可自动开 | [134](134-spirit-veil-ranged-normal-attack-2026-08-16.md) |
| **况栏＋砍树** | 只读物况；树／墙耐久；伐倒入包粗木 | [135](135-world-object-inspect-and-tree-chop-2026-08-16.md) |
| **田区农作** | 整片区走格；玩家／NPC Labor；去掉绿草幽灵工区 | [136](136-farm-field-zone-labor-2026-08-16.md) |
| **两级熟练 UI** | 斗技列表→详情；功法卡片→同款详情 | `HostCombatArtsPanel`／`HostCultivationPanel`／`HostSkillMasteryPanelUi` |
| **冲击确认窗** | 条件满足点冲击 → 问是否突破＋成功率；结果窗无成功率 | 见 §3 |

---

## 3. 熟练冲击确认（本轮验收点）

**规则**

1. 进度与材料满足后，点「冲击下一档」→ **先弹确认**，不立刻蓄势。  
2. 确认文案含：功法／斗技名、当前档 → 下一档、**突破成功率约 xx%**、材料（失败仍耗）。  
3. 点「确认冲击」后才走 `HostSkillStudyRitual` 黄条。  
4. 成功／失败结果弹窗只写结果与效果，**不写成功率**。  
5. 斗技与功法两边同一套（`DrawBreakthroughConfirm`）。

**Core：** `SkillMasteryService.EvaluateMasteryBreakthroughChance`；结果 `Body` 已去掉成功率句。

---

## 4. 砍树／田区修正摘要

| 项 | 现行 |
|----|------|
| 粗木产量 | 小 3／中 **10**／大 **40**（修 `ToLowerInvariant` 与 `treeM` 大小写错配） |
| 掉落 | 伐倒**先入背包再销毁**；无需地上拾取 |
| 树交互 | **不**注册 Work 热点；右键／F8 砍伐 |
| 农田检视 | 无旧绿草色带幽灵工区；只点耕种格出况栏 |

---

## 5. 手操验收（已通过项）

- [x] 斗技／功法冲击下一档：确认窗有成功率，结果窗无  
- [x] 砍中／大树产量与入包  
- [ ] （可选）纱衣 F2、田区农作、LocalPlace／ManualArt 编辑器全路径再扫一遍  

---

## 6. 明确未做

- 路上遭遇 LocalMap（E）  
- 作物格 Snapshot；区级水稻／玉米等  
- 墙拆后 WalkGrid；房屋独立血量  
- 小成以上熟练材料表扩表（配置可扩，内容未堆）  
