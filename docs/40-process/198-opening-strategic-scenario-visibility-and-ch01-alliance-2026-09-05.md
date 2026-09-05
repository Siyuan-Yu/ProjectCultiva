# 开局战略场景可见性与第一章参考关联盟核对

**日期：**2026-09-05  
**状态：**实现完成，待编辑器编译与新游戏人工验收

## 内容核对结果

`base:scenario_ch01_reference` 当前 `strategicOpening.alliances` 已存在一条有效联盟：

`base:faction_fisher_village ↔ base:sect_huangcun_labor`

联盟是无方向的，因此这与「压迫宗门 ↔ 沧澜渔盟」完全等价。没有重复新增同一对关系。`base:scenario_playable_day` 保留自己的同一联盟；`base:scenario_chapter1_harness` 继续保持空联盟。三个 Scenario 独立，绝不自动同步。

## 正式运行时链路

`Content Loader → OpeningScenarioDefinition.Alliances → StrategicOpeningContentBootstrap → AllianceBoard → FactionDiplomacyRelationQuery`

该链路无需修改。`PlayableHostBootstrap` 的默认 `openingScenarioId` 是 `base:scenario_ch01_reference`，所以新开游戏应加载上述联盟。压迫宗门对山匪的开局战争会在联盟已建立后应用，既有 Alliance war-binding 会将沧澜渔盟加入该战争。

开局战略只应用于新游戏。Save/Load 的联盟、战争、附庸由 Runtime Strategic Snapshot 恢复；不得为了更新 Content 而在读档时重新 Apply Opening。

## 编辑器可见性

WorldGraphEditor 的「开局战略」窗口继续允许作者自由选择任意 OpeningScenario，未硬编码默认选择，也不会自动判断或切换当前游戏场景。窗口现在在场景选择框下高亮显示：

- 当前编辑开局的正式名称；
- 当前编辑开局的完整 Scenario ID；
- 保存完成后的精确 Scenario ID 与仅影响新游戏的提示。

这只提升可发现性，不改变保存语义。

## 人工验收

使用 `base:scenario_ch01_reference` 新开游戏，不读取旧档；在势力总览确认压迫宗门与沧澜渔盟为联盟，并确认沧澜渔盟因联盟战争绑定对山匪处于战争。
