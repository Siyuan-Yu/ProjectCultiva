# 122 · 境界／打坐／人物面板与突破仪式（2026-08-15）

> 状态：**已落地；突破蓄势／结果弹窗手操验收通过**｜日期：2026-08-15  
> 相对提交：`3a68885` 之后 → 本轮 `main`  
> 飞书：https://my.feishu.cn/docx/ZNYIdDIFDoEmSgxhOwFcLm04nQb  
> 设计对照：[25 修炼与突破](../20-systems/25-cultivation-and-breakthrough.md)｜[22 境界](../20-systems/22-realms-and-abilities.md)｜[SCHEMA](../../Content/BaseGame/Data/SCHEMA.md)  
> 上一轮：[121 住房／主管府](121-housing-assignment-and-control-core-2026-08-15.md)

---

## 1. 一句话

Host 侧补齐「可看、可打坐、可手动突破」闭环：底栏概况＋右侧人物／境界／关系暂停窗；感应→炼气阶梯可配置；打坐 F6；突破约 10 秒黄条蓄势，可取消／可打断，结束后暂停弹窗展示成败与属性变化。炼气起灵力护盾。

---

## 2. 交付对照

| 主题 | 做什么 | 入口／文件 |
|------|--------|------------|
| **人物／境界／关系 UI** | 独立暂停窗；入口在底栏状态板右侧竖条，不在脚下 | `HostCharacterSheetPanel`／`HostCultivationPanel`／`HostRelationPanel`／`HostFormalHud` |
| **概况底栏** | 体魄／耐力等条；打坐 F6／底栏修炼钮 | `HostFormalHud`／`HostCultivateConfirmPrompt` |
| **境界阶梯** | 感应前中后 → 炼气 1–10 → 筑基；内容可配 | `realm_ladder.json`／`RealmLadderBoard`／`type=realmLadder` |
| **修为节奏** | 每 5 游戏分 +5；积满不自动突破 | `CultivationProgressRules`／`CultivateAction` |
| **打坐** | 随时可坐；就地确认；倍速跟 `PresentationDeltaTime` | `CultivationAttemptGate`／`HostWorkLoop` |
| **功法门槛** | **感应境突破不需功法**；炼气及以后需已得功法 | `CultivationService.CanAttemptBreakthrough` |
| **突破仪式** | ~10s 黄条 → 结算报告弹窗（暂停世界） | `HostBreakthroughRitual`／`BreakthroughReport` |
| **取消／打断** | Esc／F1／「取消」＝干净取消；移动／受伤／其他指令＝失败＋修为小损 | `HostBreakthroughRitual`／`FailBreakthroughChannel`／指令桥／移动 |
| **头顶提示** | 蓄势中显示「冲击瓶颈」 | `HostActivityPresenter`／状态板活动文案 |
| **灵力护盾** | 炼气起受伤先扣灵力 | `CombatVitalsComponent`／`CombatDamageRules` |
| **悬停可读** | Label／按钮悬停文字不刷白（墨色锁定） | `HostImguiStyles` |
| **内容 SCHEMA** | `realmLadder` 字段说明 | `SCHEMA.md` |

---

## 3. 操作流（制作人手操）

```text
选中角色 → 右侧「境界」→ 看修为／下一关
修为满瓶颈 →「尝试突破」
关闭面板，底栏上方黄条蓄势约 10 秒
  · Esc／F1／取消 → 干净停下（不扣修为）
  · 移动／受伤／其他指令 → 失败弹窗＋修为小损
蓄势满 → 暂停世界 → 弹窗（成功：境界 A→B＋属性差分；失败：仍为原境＋损失）
「知道了」→ 恢复时间

打坐：F6 或底栏修炼钮 → 就地确认 → 每 tick +5 修为
```

---

## 4. 规则摘要

| 规则 | 现行 |
|------|------|
| 感应境小阶 | Mortal minor 0/1/2＝前／中／后期 |
| 炼气 | QiRefining minor 1–10 |
| 突破 | 玩家主动；不自动升境 |
| 功法 | 感应冲击不要求；炼气+要求 `LearnedManualId` |
| 蓄势时长 | 默认 10 现实／表现秒（跟倍速） |
| 失败损耗 | 当前瓶颈需求修为的约 1/10（打断与骰点失败同量级） |
| 灵力 | 入炼气后 `grantSpiritPower`／护盾 |

**明确未做（本轮）：** 天气／灵地修正成功率；炼气后功法获取玩法闭环；真战斗完整系统；美术换皮。

---

## 5. 代码／内容索引

| 层 | 新增／主要改动 |
|----|----------------|
| Core | `RealmLadder*`／`BreakthroughReport`／`CultivationProgressRules`／`RealmDisplay`／`CultivationService` 突破报告＋打断失败／`Combat*` |
| Data | `RealmLadderDefinition`／`RealmLadderMapper`／加载 `realmLadder` |
| Content | `Cultivation/realm_ladder.json`；`SCHEMA.md` |
| Host | `HostBreakthroughRitual`／人物／境界／关系／打坐确认／`HostImguiStyles`；`HostFormalHud` 侧栏与概况；指令／移动／活动文案挂钩 |
| Tests | `CultivationSliceTests` 等更新 |

---

## 6. 手操验收（短）

1. 感应境打坐积满 → 境界面板「尝试突破」→ 黄条 → 成功弹窗见境界变化与属性。  
2. 蓄势中点「取消」或 Esc → 无失败弹窗、修为不因取消被扣。  
3. 蓄势中下令移动 → 失败弹窗写明打断、修为小损。  
4. 悬停面板文字仍为墨色，不融进羊皮纸底。  
5. 入炼气后受伤优先掉灵力（若已有灵力）。

**签收：2026-08-15 制作人确认突破蓄势／结果弹窗验收通过。**
