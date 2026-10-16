# 当前已有美术接入核查（2026-10-16）

范围：`Assets/Scenes/LevelTester.unity` 的现有 Host 表现链。`273-current-art-asset-register.tsv` 是状态真源，本报告说明每个 AssetID 的实际接线范围。没有运行 Unity Editor、PlayMode 或人工视觉验收。没有改动 Core、Content JSON、地图布局、碰撞、床位规则或结算。

## 计数与口径

55 个登记项：A 18、B 1、C 11、D 14、E 6、F 5。A 只表示列明的运行对象已经取到新资源，全部仍为“已接入待验”；部分 AssetID 只接入其中的子件或对象，不表示整项美术覆盖完毕。B 为有候选但未接运行入口，C 为候选有质量/尺寸/拼接障碍而保留旧表现，D 为没有合适的当前资源，E 为程序表现无需 PNG 替换，F 为当前范围暂缓。未把复制 PNG 或预览示意算作 A。

本轮在此前 Step A 的 7 套角色图集、三床、三树、矿堆基础上，接入 7 张身份静态角色图、草地、仓储货箱、透明控制核心、洞口，以及 8 张正式 UI 图标。角色源图按真实 DefinitionId 映射；缺失动作始终保持该人物的静态外观，不借用会换衣服的村民动画。运行 PNG 按人物、环境、UI 分类迁入 `Assets/Art/{Characters,Environment,UI}/Resources`，连同原 `.meta` 保留 GUID；`Resources.Load` 在 Build 中可用，同一进程共享缓存，不为每名人物创建 Texture2D。地图静态替换沿用 `HostMapObjectVisualFactory` 的原占地/交互对象及视觉子层。

## 逐项结论

| AssetID | 类别 | 实际接入或保留原因 |
| --- | --- | --- |
| ART-CHAR-001 | A | 基础村民两种已确认身份使用四方向 Idle/Walk/Work/Attack；其他居民不强套。 |
| ART-CHAR-002 | A | 主角三人按各自 DefinitionId 使用对应南向静图；其他方向/动作维持本人的静图，完整动画仍缺。 |
| ART-CHAR-003 | A | 主管身份使用官帽南向静图；动作仍缺。 |
| ART-CHAR-004 | A | 三巡卫共用四方向 Idle/Walk/Attack。 |
| ART-CHAR-005 | A | 阿土锄具、阿禾围裙、阿青背篓使用不同南向静图；其他居民旧映射/回退，多方向差分仍缺。 |
| ART-CHAR-006 | D | 商店/拍卖执事没有身份一致的正式切件；旧 Merchant 占位保留。 |
| ART-CHAR-007 | D | 江老等其他名录人物没有已确认的身份差分。 |
| ART-ANIM-001 | A | 村民和巡卫现有完整外观以真实移动状态及位移方向播放；静态身份不冒充动画。 |
| ART-ANIM-002 | A | 基础村民的真实工作信号驱动 Work；其余身份无对应动作帧。 |
| ART-ANIM-003 | C | 背负挂点样本为预览，遮挡/锚点未通过运行验证；保留既有搬运 View。 |
| ART-ANIM-004 | A | 已映射村民徒手与巡卫长枪由正式 Host 攻击事件触发；其他身份保持本人静图。 |
| ART-ANIM-005 | E | 保留同一人物的现有生命状态染色/标注；预览变换不是可运行睡姿图集。 |
| ART-ROOM-001 | C | 地板独立切件有，但与真实房间格距和接缝未核，不能用整图盖住室内。 |
| ART-ROOM-002 | C | 墙直段/墙角已切，实际墙格、可破坏段及拼缝未核。 |
| ART-ROOM-003 | C | 门洞切件未证明与正式可通行格及墙段匹配。 |
| ART-ROOM-004 | D | 没有木/石×等级可用差分。 |
| ART-ROOM-005 | C | 用途符号仍在旧候选拼图，未形成合格独立小标识；床用途美术另见 006。 |
| ART-ROOM-006 | A | 实际床 View 随 RoomUsage 使用住宅/医疗/囚禁三床，使用点/碰撞不变。 |
| ART-ROOM-007 | A | `storageRoom` 运行 View 使用货箱 v03；货物其他子件与仓储室结构未铺设。 |
| ART-ROOM-008 | A | `controlCore` 运行 View 使用透明 v03；独立 `HostFactionFlagPresenter` 的旗帜仍是原表现，三色旗未接。 |
| ART-ROOM-009 | D | 当前无已确认可运行修炼垫/恢复点图；旧物件保留。 |
| ART-ROOM-010 | A | `cave` 运行 View 使用洞口 v02；山壁边缘未铺设。 |
| ART-GROUND-001 | A | 草地紧凑 Mesh 保留原几何/批处理，换成边缘融合 v02 贴图；普通 `grass` View 也映射该图。运行缩放/接缝待人工验。 |
| ART-GROUND-002 | C | 六道路切件直段重复预览有明显接口缝，保留旧路。 |
| ART-GROUND-003 | D | 林地目前仅旧色块/灰盒图，无已确认的新无缝底图。 |
| ART-GROUND-004 | D | 石地/矿区没有合格新底色；不拿矿堆替代地面。 |
| ART-GROUND-005 | C | 农田旧合图含成熟植株，不能表示独立生长层；保持旧地块。 |
| ART-GROUND-006 | C | 药田缺合格独立地块与植物层；保持旧地块。 |
| ART-GROUND-007 | A | `treeS/M/L` 真实 View 用三个新切件，砍伐对象与碰撞不变。 |
| ART-GROUND-008 | E | 树木受损/耗尽继续依靠已有状态表现，不增 HP 分档 PNG。 |
| ART-GROUND-009 | A | `ore/mine` 真实 View 使用矿堆切件，资源身份/采集不变。 |
| ART-GROUND-010 | C | 农作物试验植株层有残土、叶片 Alpha 缺口，未用坏分层替换正式状态。 |
| ART-GROUND-011 | C | 药草试验层有抠图缺口，旧表现保留。 |
| ART-GROUND-012 | D | 灵地只有旧灰盒地面，气息未有适用的新资源。 |
| ART-GROUND-013 | D | 河流与岸线没有合格模块。 |
| ART-GROUND-014 | F | 远景/季节扩展不在本轮。 |
| ART-UI-001 | C | HUD 框是整体候选，尚未验证 IMGUI 正式多个面板的九宫格边界与文字留白；不遮盖按钮。 |
| ART-UI-002 | A | `HostFormalHud` 饱食、精力条加载对应图标；弥留/睡眠/押送三个图标未找到安全的同布局入口。 |
| ART-UI-003 | A | `HostInventoryPanel` 5 个现有 ItemId 的背包格显示图标，名称/数量/按钮仍动态；其他仓库列表未改。 |
| ART-UI-004 | D | 没有三档灵石合格候选。 |
| ART-UI-005 | D | 没有商店/拍卖/任务三入口合格候选。 |
| ART-UI-006 | E | 现有程序选中圈/目标提示保留。 |
| ART-UI-007 | A | `HostMapGraybox` 的真实地点标签使用石牌底图、文字仍动态；交互符文和独立旗帜仍未接。 |
| ART-UI-008 | D | 人物头像查询仍无身份一致头像。 |
| ART-UI-009 | B | 两种功法物品图标已用于物品格；独立技能/功法面板仍是旧字标，不能算技能入口接入。 |
| ART-UI-010 | F | Debug/作弊 UI 不美化。 |
| ART-FX-001 | E | 保留程序受击颜色反馈，不按人物画受击 PNG。 |
| ART-FX-002 | E | 保留程序挥砍/弹体，不覆盖有效特效。 |
| ART-FX-003 | E | 工作动作/目标反馈仍是现有程序行为，未有更合适独立图。 |
| ART-FX-004 | D | 医疗/囚禁提交反馈无已确认专属资源。 |
| ART-FX-005 | D | 修炼/恢复没有已确认的轻效果资源；旧表现保留。 |
| ART-FX-006 | F | 高阶技能特效不在当前玩法范围。 |
| ART-REF-001 | F | 已有风格图只作制作参考，含文字且非运行 Sprite。 |
| ART-REF-002 | D | 独立关键人物立绘样本尚无合格交付。 |
| ART-REF-003 | F | 剧情正式立绘范围暂缓。 |

## 最小返工/补图与人工检查

最小素材返工：房间真实格距的地板/墙/角/门拼接；道路直段和交界无缝；农田/药田干净植株 Alpha 层。最小新增：主角三人、主管、围裙/锄具居民与各自身份一致的方向/动作帧；未接 UI 图标仅在正式入口尺寸确定后补。其余 B/D/F 不以“有原图”冒充成品。

主要表现变动：`HostStepAArt` 的身份、地图物件和 UI 资源缓存映射；`EntityView` 的动作缺失静态回退；`HostDemoTileMap` 的草地贴图替换；`HostInventoryPanel` 的物品图标；`HostFormalHud` 的饱食/精力图标；`HostMapGraybox` 的地点标签底图。此前 Step A 的 `EntityViewSpawner`、`HostMapObjectVisualFactory`、`HostMapPlotCell` 等接线继续有效。没有编辑 Prefab 或 `.meta` 的既有 GUID；新 PNG 有独立 `.meta`。

已执行：`pwsh -File tools/offline-compile.ps1` 全程序集 `ALL_OK`（测试程序集有两条既存编译警告）；`git diff --check` 无空白错误；TSV→XLSX 项目导出脚本 55 行全字段回读通过。仓库规则不运行自动化测试/Unity；本轮未运行 Unity Editor、PlayMode 或真实视觉验收。

LevelTester 最短人工检查：新开局看村民/巡卫四方向移动、工作与攻击；看主角/主管/阿土/阿禾/阿青静态身份是否稳定；检查房屋和床仍开面、可进出与使用；查看草地、树、矿、仓储、核心、洞口的尺寸/遮挡；打开背包和人物面板确认图标不挡文字/按钮；缩放和存读档后复看。道路/田地及独立旗帜仍预期显示旧表现。
