# 世界地图阶段汇总与荒村下一轮交接

日期：2026-10-26。制作人已明确要求整理文档、提交并推送 GitHub；本页是本次提交的当前状态入口。提交不等于运行验收或封板。下述下一轮仅为盘点和建议，本次未实施。

## 1. 当前版本与范围

- 内容版本：`0.0.37-two-compact-sects`。城镇详情 UI 是其后的代码修订，不另升内容版本。
- 主世界：18450×8700 格，cell size 0.028，origin (-1.4, -1.4)。空地压缩不缩城镇实体和五级扩建空间，不修改飞行／旅行速度。
- 固定城镇 66 座，初始无主 14 座。10 个人类宗门及朔风堡合计拥有 52 城，玩家初始 0 城。
- 等级 1～5 城镇边长仍为 180／270／360／450／540 格。
- 正式河网 8 段河、13 桥、36 道路，新增 2 处小湖；东部大河延伸到北边界。此前候选河流图不再代表未实施状态。
- 荒村最终采用圆形盆地，可用内区直径约 960 格，保留五级 540×540 方形扩建空间及余量；720 格方形是已被替代的中间版本。
- 最新地图／归属应新开局查看；只看最新城镇详情 UI 不要求新开局。旧存档不承诺自动迁移到新布局与初始归属。

## 2. 本轮已实现的内容

### 地理与通行

定稿城址和山脉、盆地关系重建后压缩空地；城镇及扩建尺度保留。魔兽山脉四面围山，东南西北设置关城。南赢镇及盆地南移，桐溪镇迁往魔兽山脉和雪原之间。南赢及东西阵盆地封口，风暴城东迁，风雷区域填补原有平原夹缝，雷西关／雷南关作为雷暴区地面入口。20 座关城接缝采用精修地形，避免从城墙侧面绕入；飞行规则不变。实际通行仍需人工验收。

### 势力、主城与地图交互

| 势力 | 初始城数 | 地图主城 |
|---|---:|---|
| 执衡宗 | 5 | 衡岳城 |
| 镇岳宗 | 7 | 镇岳城 |
| 朔风堡 | 7 | 霜原城 |
| 合欢宗 | 5 | 西阵镇 |
| 青霄剑宗 | 5 | 青霄城 |
| 百草丹宗 | 4 | 百草城 |
| 铸星谷 | 4 | 火脉城 |
| 太衍阵宗 | 4 | 太衍城 |
| 玄煞门 | 3 | 玄衡城 |
| 天麓宗 | 4 | 镇魔北关 |
| 听雨宗 | 4 | 雷南关 |
| 无归属 | 14 | 无 |

同宗归属尽量集中：镇岳高原及北侧、朔风雪原、青霄盆地、合欢东西阵盆地及西北邻城。天麓宗为北都镇／平原驿／北崖关／镇魔北关；听雨宗为天行镇／雷南关／沙海关／沙西关。完整归属以作者配置与正式内容为准，地图显示以运行时真实所有权为准。

图例可折叠，显示颜色及实时占城数量；hover 放大强调所属城镇，移开恢复，不修改真实城镇范围。双击聚焦当前仍拥有的主城，只移动镜头；主城失守时回退至现有最高等级城镇并提示，无城／无主条目不给虚假主城。`mapCapitalSiteId` 是展示元数据，不是所有权真源。选城详情显示中文归属、色标、等级、主城标识及坐标，选中／旅行提示使用城名。

### 修复与性能

已处理床位房间净内区校验、出生床边位置、北部飞行角色深度排序和巡逻队部署阻挡问题。减少床位重复查询、交互点搜索和 HUD Layout 重复工作，并补充性能采样标记。不能把编译通过当作消失／卡床问题的完整运行验收，也不能据此宣称 FPS 提升或达到目标帧率。

## 3. 作者真源与发布交接

路径均相对仓库根目录：

- `Content/BaseGame/Authoring/ContinuousSurface/Worlds/final_world_site_plan_v1.csv`：原定稿城址。
- 同目录 `final_world_geography_v1.json`、`final_world_compaction_v1.json`、`final_world_river_network_v1.json`：地理、压缩／附加城／归属覆写与河网真源。
- `Blueprints/`、`DetailPatches/`：城镇及关城接缝、小湖精修源。
- `main_world.worldcomposition.json`：重建后的作者世界；不得只改运行缓存而遗漏真源。
- 运行三件套位于 `Content/BaseGame/Data/Worlds/`：`main_wilderness_surface_v1.json`、`w2a_surface_geography_baked_v1.json`、`main_world_surface_map_v1.json`。

当前导航／大地图共同 sourceHash：`6912b19f5f730d679d3753e62481b1b9a0dbecde84a9035ff02361c46bc832e2`。

正常完整发布命令（本次文档整理不需要重发）：

```powershell
python ExternalTools/ContentAuthoring/FinalWorldPublish/rebuild-source.py
dotnet build ExternalTools/ContentAuthoring/FinalWorldPublish/FinalWorldPublish.csproj --no-restore -v:q
dotnet ExternalTools/ContentAuthoring/.build/FinalWorldPublish/bin/Debug/net8.0/FinalWorldPublish.dll Content/BaseGame/Authoring/ContinuousSurface/Worlds/main_world.worldcomposition.json Content
```

仅归属调整支持受限 `--owners-only <修改前作者世界基线文件>` 发布：先保存基线，再改真源及重建；工具核对除 Site owner 外作者数据一致、旧缓存来源一致，才更新运行归属与 sourceHash。不得把该路径用于几何／地形变化。最近两轮归属发布已静态核对：运行三件套除 owner／sourceHash 外未变化。临时基线、预览、构建产物与 `_backups` 不作为正式源码提交。

## 4. 荒村占领议政厅前：现状与缺口

范围明确为新手阶段，到取得荒村议政厅并解锁外界为止。制作人已决定暂缓野外内容扩张，优先补完这一段。以下是源代码／内容盘点，不是自然游玩验收。

已有基础：开局选择和可重提引导、村民对话／委托、食宿医疗与劳动、棋局传承和书册学习、修炼突破、趁巡卫间隙出村探索封闭盆地及洞府、议政厅攻打／占领／真实归属解锁。不是从零补新手系统，也不增加固定日期剧情或强制筑基才能夺村。

| 优先级 | 缺口／待确认项 | 下一轮建议 |
|---|---|---|
| 高 | 修炼引导更多依赖听完对话；`quest_huangcun_practice` 以 `story:huangcun_practice_advice` 完成，后续跳到炼气目标 | 补第一次实际打坐、精力恢复、瓶颈突破、学习／使用功法的可理解反馈，不把可选教学变强制门槛 |
| 高 | 夺村准备提示笼统，玩家难以把提示对应到敌对确认、守军、核心耐久、驻留占领 | 优先补当前目标和不能继续的原因；沿用已有占领链，不重写规则 |
| 高，待实玩 | 正常凡人开局成长到夺村的时长、战力与物资节奏尚未验收 | 由制作人自然游玩确认；20 倍速／高境界调试不代表新手节奏，暂不武断调整数值 |
| 中 | 巡卫主要依赖作息与门口距离判定；并非完整视野、盘问、追捕、处罚系统，部分威胁台词偏重 | 先对齐反馈与文案；复杂追捕／惩罚须另行确认，不默认扩展 |
| 中 | 粮木药委托有同质化，未充分承担教学作用 | 选择现有代表性委托串联生产、恢复、关系和战前准备，不以新增数量为目标 |
| 中，待实玩 | 食宿医疗、同伴与失败后的继续游玩能否顺畅接续 | 验证当前版本，区分已修床位问题与尚未确认的体验风险 |
| 低，已确认未改 | `quest_huangcun_village_freedom` 描述仍有旧称“青石荒村” | 下一轮统一称谓 |

证据入口：`Content/BaseGame/Data/Quests/huangcun_life_quests.json`、`huangcun_settlement_commissions.json`，`Data/Events/huangcun_life_events.json`，`Data/Chapters/huangcun_life_chapter.json`；`Assets/Scripts/Core/World/Strategic/HuangcunOpeningAccess.cs` 与 `Assets/Scripts/Unity/Host/HostControlCoreAssault.cs`、`HostWorldSiteCoreWarfare.cs`、`HostNpcContextMenu.cs`。

炼丹／炼器功能房不等于完整制作玩法；完整告密追捕、其他城镇剧情、野外大规模铺内容不作为本次新手闭环的前置。建议下一轮目标：**玩家不读开发说明，也能理解并自行走通占领荒村前的流程**。先引导与反馈，再自然成长节奏及失败恢复，最后对齐文案。具体实施仍等制作人下一轮指令。

## 5. 验证与验收边界

本次提交前 Core／Data／Unity／Unity.Editor 四程序集离线基础编译通过；FinalWorldPublish 构建 0 警告、0 错误。未启动 Unity、未编写或运行自动测试。内容当前为 **Implemented / Producer Acceptance Pending**，不是全项 Producer Accepted。

后续人工关注：新开局地图归属和主城交互；地面关城接缝不能绕过而飞行规则不变；床边出生、长距离飞行角色显示；同场景同倍率性能采样；正常凡人开局至夺村的可理解性与恢复路径。未完成的人工项保持待验，不能由本次 Git 提交替代。

## 6. 本轮文档索引

- [378 定稿地理](378-final-world-geography-gate-handoff-2026-10-26.md)、`379-final-world-site-disposition-audit.tsv`：原定稿与城址核对。
- [380 压缩与表现](380-world-compaction-and-presentation-fixes-2026-10-26.md)、[381 圆盆地与魔兽关城](381-round-enclave-and-beast-gates-2026-10-26.md)。
- [382 盆地封口与迁址](382-basin-seals-and-tongxi-relocation-2026-10-26.md)、[383 风雷关城](383-storm-thunder-gates-2026-10-26.md)、[384 接缝与性能](384-gate-seams-and-runtime-cost-2026-10-26.md)。
- [385 图例与河流候选](385-world-map-legend-and-river-options-2026-10-26.md)是历史方案；[386 河网与主城导航](386-river-network-and-faction-navigation-2026-10-26.md)是实施记录。
- [387 势力集中](387-compact-faction-ownership-2026-10-26.md)、[388 两宗增补](388-two-compact-sects-and-next-step-2026-10-26.md)、[389 城镇详情](389-map-city-inspection-and-wilderness-discussion-2026-10-26.md)。
- 当前关系按本页与正式真源为准；旧页“无主 22”“720 方盆地”“未提交推送”等保留为当轮历史，不表示本检查点的现状。Git 提交／远端结果以真实仓库记录为准。
