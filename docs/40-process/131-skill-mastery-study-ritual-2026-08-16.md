# 131 · 功法／斗技蓄势研读＋熟练度（2026-08-16）

> 状态：**已落地；冲击确认窗已验收**｜日期：2026-08-16（2026-08-17 补确认窗／两级 UI）  
> 设计对照：[2B](../20-systems/2B-attributes-and-affinity.md)｜[2D](../20-systems/2D-manuals-arts-and-equipment.md)｜境界仪式：[122](122-cultivation-breakthrough-host-ritual-2026-08-15.md)  
> 收束：[137](137-skill-mastery-farm-veil-chop-rollup-2026-08-17.md)

---

## 1. 一句话

背包点学改为约 8 秒黄条参悟＋**学习成功率**（成功＝入门；与战斗释放无关）；黄条与境界突破相同，贴在底栏状态板上方。熟练度可经使用／打坐／修为灌注增长；冲击下一档前弹确认（含成功率），结果弹窗不再显示成功率；熟练度进 Snapshot。

---

## 2. 交付对照

| 主题 | 做什么 | 入口／文件 |
|------|--------|------------|
| **Core 熟练** | 档位／状态／规则／服务 | `SkillMastery*.cs`／`CultivationComponent.ManualMastery`／`CombatArtsComponent` |
| **研读仪式** | 黄条＋可取消／打断＋结果窗 | `HostSkillStudyRitual` |
| **点学入口** | 背包／斗技面板走仪式；选人前显示学习成功率 | `HostManualLearnPrompt`／`HostCombatArtLearnPrompt`／`HostCombatArtsPanel` |
| **面板** | 斗技一级列表／二级熟练度页；功法点进同款二级页；灌注／冲击在详情底栏 | `HostCombatArtsPanel`／`HostCultivationPanel`／`HostSkillMasteryPanelUi` |
| **冲击确认** | 条件满足点冲击 → 问是否突破＋成功率；确认后才蓄势 | `DrawBreakthroughConfirm`／`EvaluateMasteryBreakthroughChance` |
| **增长** | 打坐涨功法；释放涨斗技；效果按档绝对值 | `CultivateAction`／`MeleeCombatService`／`CultivationService` |
| **存档** | 功法熟练＋已学／装备／斗技熟练 | `EntitySnapshotDto`／`SnapshotService` |
| **任务直授** | `learnManual` outcome 仍立刻入门 | 既有 `CultivationService.LearnManual` |

---

## 3. 手操验收

1. 背包用秘籍 → 选人 → 黄条 → 成功弹「入门」或失败仍可再试（结果窗无学习成功率）  
2. 境界面板：打坐／灌注涨熟练 → 满后备齐材料 →「冲击下一档」→ **确认窗有成功率** → 蓄势 → 结果窗无成功率  
3. 斗技同 2；被动／主动伤害随档位变  
4. F5 存读：熟练档位与进度不丢  

---

## 4. 本轮不做

- 小成以上材料表与突破（配置可扩）  
- 路上遭遇  
- 改变任务直授为蓄势  
