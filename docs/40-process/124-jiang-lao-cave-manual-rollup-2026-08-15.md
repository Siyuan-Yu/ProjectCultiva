# 124 · 将老对弈／秘籍／洞府勘查进出收束（2026-08-15）

> 状态：**竖切已落地；手操可走通**｜日期：2026-08-15  
> 相对提交：`285968f`（境界／突破）之后 → 本轮 `main`  
> 飞书：https://my.feishu.cn/docx/M4hBdXHm0oDcuoxRm3ccJ45HnXd  
> 契约：[123 功法任务条件／奖励](123-quest-manual-api-interfaces-2026-08-15.md)（飞书 https://my.feishu.cn/docx/XPE5dGfcYoI5iSxuL8zco64QnBc）｜Map：[112](112-map-editor-usage.md)｜WorldGraph 草案：[113](113-world-graph-local-map-architecture-revision-v0.1.md)  
> 上一轮：[122 境界／突破仪式](122-cultivation-breakthrough-host-ritual-2026-08-15.md)

---

## 1. 一句话

本轮把「炼气后功法从哪来」拆成两条可玩竖切：**将老井字棋三胜拿残谱**，以及 **勘查发现洞府 → 选人进洞 → 拾取洞府秘诀 → 出口离开**；秘籍均为背包道具，使用后选炼气队员学习。

---

## 2. 交付对照

| 主题 | 做什么 | 入口／文件 |
|------|--------|------------|
| **将老对弈** | 泉边 NPC；每日一局井字棋；累计胜 3 次任务完成；奖励残谱秘籍 | `character_jiang_lao`／`quest_jiang_lao_chess`／`HostTicTacToePanel`／`startMinigame` |
| **秘籍道具** | `teachesManualId`；背包使用 → 选炼气队员 → 消耗 1 本学会 | `ManualItemLearnService`／`HostManualLearnPrompt`／`items.json` |
| **功法展示** | `grade`／`effectSummary`；打坐跟 `cultivationSpeed` | `cultivation.json`／`CultivationManualSpec` |
| **任务 API** | 计数／日旗／遭遇清除／学功法等条件与 outcome | [123](123-quest-manual-api-interfaces-2026-08-15.md) |
| **洞府勘查** | F7／指令条；圆心＝选中己方（多人多圈）；半径＝神识×2；近距 toast | `HostCaveSurveyPresenter`／`OpportunityEntranceRules`／`SurveyEntrance` |
| **显形入口** | 未发现不刷 `cave` 戳；勘查成功只刷地表不挪镜头 | `HostDemoTileMap`／`RefreshMapStampsOnly` |
| **进洞** | 右键洞府 →「进入」→ 弹窗勾选随行 → 进 `map_ch01_cave` | `HostNpcContextMenu`／`HostLocalMapEnterPrompt`／`EnterLocalMap` |
| **出洞** | 洞内右键出口戳 →「离开」→ **洞内己方全员撤离**；已发现洞口可再进（含救人） | `HostCaveEntranceQuery.TryPickInteriorExit`／`LeaveLocalMap`／`LocalMapSession.OccupantIds` |
| **洞府秘诀** | 洞内地上物拾取进背包；黄阶中级；学后攻击 +6% | `loot`／`WorldLootPickupService`／`cultivation_dongfu_secret` |
| **倍速** | 顶栏含 20x | `HostFormalHud` |
| **MapEditor** | `cave`／`loot`＋`lootItemId`；洞府编辑说明 | [112](112-map-editor-usage.md) |

---

## 3. 操作流（制作人手操）

```text
【将老】
走近灵泉将老 → 对话 → 对弈（每日一局）→ 累计胜 3 → 领「将老残谱」→ 背包使用 → 选炼气队员学

【洞府】
走近洞府区 → toast「附近似有洞府」
F7 勘查（框选多人则每人一圈；半径＝神识×2）→ 洞口显形
选中己方 → 右键洞府 →「进入」→ 弹窗打勾随行 → 确认
洞内右键「洞府秘诀」地上物 → 拾取进背包
背包使用 → 选炼气队员学（攻击 +6%）
右键「出口（离开洞府）」→「离开」→ 回地表
```

---

## 4. 规则摘要

| 规则 | 现行 |
|------|------|
| 勘查半径 | 神识 × 2；足迹 padding ≈ 2.5 |
| 提示 toast | 半径 ≥ 勘查圈，避免「有提示却扫不到」 |
| 多选勘查 | 每个选中己方一圈 |
| 进洞随行 | 弹窗勾选；主导必进；未勾选留地表；仍在洞内者再进时默认勾选 |
| 出洞 | **全员撤离**（登记名单 ∪ 内室 Location）；洞内走动不再吸附地表地点 |
| 离开 | 仅移动洞内 `localMapId` 匹配的己方回落点 |
| 秘籍学习 | 需炼气；**秘籍不消耗**；一人一本，换功法覆盖（需确认） |
| 斗技 v0 | 可学多门、**装备最多 6 格**；**1–6 释放主动技**；秘本不消耗 |
| 裂爪击 | 洞府宝物；黄阶中级；三连击每段攻击力 200% |
| 开山拳 | 将老三胜奖励；黄阶中级；一击攻击力 500% |
| 战斗 Alpha | 地图内走近后自动互砍（属性＋斗技）；NPC 击倒可写 encounter 旗 |
| 洞府秘诀 | Percentage Attack 0.06；`ModifierGrant.Value` 现为 double |
| Snapshot | 未为本轮升 Freeze／Snapshot schema（日旗／计数／loot flag 走既有 Flags） |

**明确未做（本轮）：** 完整 WorldGraph（[113]）；1–6 技能栏／半自动释放；废功仪式。

---

## 5. 代码／内容索引（含战斗 Alpha）

| 层 | 要点 |
|----|------|
| Core | `MeleeCombatService`／`CombatArtsComponent`／`EncounterLinkComponent`；秘籍不消耗；换功法卸旧修饰 |
| Host | `HostNpcMeleeAssault`；功法覆盖确认；斗技研习窗 |
| Content | `character_cave_shade`＋`defeatEncounterId`；洞内斗技掉落 |

---

## 6. 下一步建议

1. 手操：洞内打残影 → 清 encounter → 捡秘籍／斗技  
2. 遭遇任务内容装配 `encounterCleared`  
3. 斗技装备 UI（现学即默认装备）  

---

## 7. 相关链接

- [123 接口契约](123-quest-manual-api-interfaces-2026-08-15.md)  
- [112 MapEditor](112-map-editor-usage.md)  
- [113 WorldGraph 草案](113-world-graph-local-map-architecture-revision-v0.1.md)  
- [62 项目现状](62-project-status-2026-08-01.md)  
