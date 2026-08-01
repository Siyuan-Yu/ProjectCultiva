# 第一章参考关 · 新章节制作流程

> 对照关卡：`base:scenario_ch01_reference`（PlayableHost 默认开局）  
> 目标：复制本关流程制作任意新章节模板，而非抄最终剧情。

## 1. 如何建地图

1. 在 `Content/BaseGame/Data/` 新增／扩展 `worldRegion`（参考 `ch01_reference_region.json`）。  
2. 每个地点填：`id`／`name`／`kind`／`adjacentIds`／`presentationX`／`presentationZ`。  
3. 道路＝邻接边；Host `HostMapGraybox` 自动画色块与连线。  
4. 可选：`resourceOnExplore*`／`opportunitySiteId`／`residentNpcDefinitionId`／`enterConditions`／`questOfferIds`。  
5. Scenario 的 `openingWorldRegionId` 指向该区域；`startLocationId` 为开局点。  
6. PlayableHost：菜单重建场景或直接 Play；俯视正交相机＋右键移动。

区域建议命名：杂役区／房屋／树林／矿洞／药田／灵泉／废弃洞府／道路枢纽。

## 2. 如何添加 NPC

1. `characters.json`（或分文件）新增 `character` 定义。  
2. Scenario `spawns[]` 增加条目：  
   - 主角：`entityKind=character`  
   - NPC：`entityKind=npc`，可招 `recruitable=true`  
3. AI 挂载：  
   - `aiRole`：`Mortal`｜`Cultivator`｜`Supervisor`  
   - `scheduleId`：`base:schedule_mortal_day`｜`cultivator_day`｜`supervisor_day`  
   - 主管：`factionRole=Supervisor`  
4. 驻地：地点 `residentNpcDefinitionId`＝该 NPC 定义 id。  
5. 跑 `XianXia/Content/Validate BaseGame Package`。

## 3. 如何添加功法

1. `cultivation.json` 增加 `cultivation` 定义（速度／突破 Progress／modifiers）。  
2. `sites.json` 的 `opportunitySite` 设 `allowsCultivation`＋`offeredManualId`。  
3. 地点挂 `opportunitySiteId`；玩家探索发现后 `Cultivate` 学得。  
4. 参考关中 `Cultivator` AI 会在开局尝试发现可修炼机缘并学功法。

## 4. 如何添加事件／任务／章节

1. 任务：`quests.json`（条件／奖励 Flag）。  
2. 事件：`content_events.json`（trigger／choices／outcomes）。  
3. 章节：`chapters.json`（`questChainIds`／`eventChainIds`／`dayBeats`）。  
4. Scenario：`openingChapterId`。  
5. 命名见 [84](84-chapter-content-naming-standards.md)；模板见 `Authoring/Templates/`。

## 5. RTS／UI 操作（参考关）

| 输入 | 作用 |
|---|---|
| 左键／框选 | 选择 |
| 右键地面 | 移动（靠近地点圆心同步 Location） |
| V | 行动菜单（劳动／休息／观察／修炼／探索／分工） |
| 1–0／T／Y | 原调试指令快捷键 |
| F6 | 正式 UI 五面板 |
| F1–F4 | 调试 HUD／事件／内容／跳日 |
| WASD／滚轮 | 俯视平移／缩放 |

## 6. 验收自检

- [ ] 地图可见 8 类区域＋道路  
- [ ] 三主角＋多名 NPC＋主管均在场  
- [ ] 凡人／修士／主管日程不同（F6 角色面板看 AI）  
- [ ] 任务／事件／资源／时间面板有读数  
- [ ] Content Validate 通过  
