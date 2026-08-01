# Chapter 01 Reference Level 验收报告

> 状态：**已通过（自动化门禁）**｜日期：2026-08-02  
> 计划：[87](87-chapter-01-reference-level-plan-v0.1.md)  
> 制作流程：[88](88-chapter-01-reference-level-production-guide.md)  
> 目标：**正式样例参考关卡＝未来所有章节制作模板**（非最终第一章剧情）

## 1. 完成内容

| 能力 | 交付 |
|---|---|
| 2D 俯视地图灰盒 | `base:region_ch01_reference`（8 地点：杂役区／房屋／树林／矿洞／药田／灵泉／废弃洞府／道路枢纽）；`HostMapGraybox` |
| 正式 RTS | 选择／框选（既有）＋`HostMoveController` 右键移动＋`HostActionMenu`（V）＋`HostCommandBridge` 角色指令 |
| 正式 UI 基础 | `HostFormalHud`（F6）：角色／资源／时间／事件／任务 |
| 三类 AI | `schedule_mortal_day`／`cultivator_day`／`supervisor_day`；`NpcAiRoleComponent`；主管日终压力事件 |
| 角色示范 | 三主角＋凡人／修士 NPC＋主管＋可招者（`base:scenario_ch01_reference`） |
| 内容 Data | 人物／功法（既有青云功法）／任务／事件／地点／章节分文件 |
| 制作流程文档 | [88](88-chapter-01-reference-level-production-guide.md) |

### 关键 ID

| 类型 | Id |
|---|---|
| Scenario | `base:scenario_ch01_reference` |
| Chapter | `base:chapter_ch01_reference` |
| Region | `base:region_ch01_reference` |
| Host 默认开局 | `PlayableHostBootstrap.openingScenarioId` → 上列 Scenario |

### 内部 Phase

| Phase | 状态 |
|---|---|
| REF-0 计划 | ✅ |
| REF-A 地图 Data＋灰盒 | ✅ |
| REF-B RTS 移动／菜单 | ✅ |
| REF-C FormalHud | ✅ |
| REF-D 三类 AI＋主管压力 | ✅ |
| REF-E Reference Scenario／内容 | ✅ |
| REF-F 文档＋验收 | ✅ |

## 2. 测试

- EditMode：**175/175 Passed**（含 `Chapter01ReferenceLevelAcceptanceTests`）
- Snapshot schema **仍为 v1**
- 未改 Architecture Freeze；未复活 Demo Runtime

## 3. 制作人怎么用本模板

1. 对照 [88](88-chapter-01-reference-level-production-guide.md) 复制区域／NPC／功法／事件流程  
2. PlayableHost Play：默认即参考关；`F6` 看五面板；右键移动；`V` 行动菜单  
3. 新章节 Scenario 改 `openingWorldRegionId`／`openingChapterId`／`spawns`  
4. 命名与校验仍见 [84](84-chapter-content-naming-standards.md)＋Templates

## 4. 不做（本阶段）

最终第一章剧情正文、精美美术、战斗、产品级 UGUI 皮肤。

## 5. 结论

**Chapter 01 Reference Level 达成。** 可作为未来所有章节制作的标准模板关卡。
