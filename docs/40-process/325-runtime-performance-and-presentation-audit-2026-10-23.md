# 运行表现与内容扩展性能收口

> 恢复后追加：围墙等已加载可破坏物的鼠标拾取改为几何失效时懒重建分桶，列表复用并保留原选择顺序；NPC每帧临时冷却列表复用及可见集合遍历，距离先于关系判定。只减少确定工作量，无实测FPS。见329。

> 日期：2026-10-23｜状态：Design Confirmed / Implemented / Producer Acceptance Pending / 未提交。基础编译结果与现场性能结论分别记录。
> 本轮经制作人授权进行全项目优化；本页负责非战斗 Host、场景物件表现及相关构建辅助。沿用 320～322 的 7600×3400 世界、948 个静态物件及 844 段逐格木围墙，不改变内容布局、稳定 ID、玩法、存档或美术分辨率。

## 代码证据与本轮范围

1. `ContinuousOutdoorSurfaceRuntime.BuildBakedOutdoorSitePlacements` 在每个 Chunk 分别扫描全部静态物件寻找洞口和分组；`BuildSiteBlockerGrid` 在每次邻域导航合成中再次逐 Chunk 扫描全部物件。随着逐格墙增加，同一份静态布局被重复过滤。将建立仅属于 Host 的稀疏 Chunk→placement 索引，保留原有严格矩形相交判断及源列表顺序；每个物件跨越的 Chunk 都参与，不能只按作者中心 Chunk 归类。
2. `BuildRuntimeConstructedOutdoorPlacements` 对每个运行建筑调用线性的 `IsAuthoredPlacement`，并在判断该物件是否属于当前 Chunk／已呈现之前创建临时定义。以稳定 ID 集合做静态来源查询，先按原坐标矩形和已有 owner 过滤，再为真正需要呈现的建筑创建描述。玩家建造、拆除和 Snapshot 后的运行资产仍实时读取。
3. 检查大量重复物件生成时的 SpriteRenderer/组件枚举。仅在有确定调用证据时复用工作缓冲；保留既有木墙单 Renderer、共享 Sprite／材质、逐格交互与破坏语义。不得为追求合批把整圈墙变成一个逻辑物件。
4. `HostMortalWorkPanel.OnGUI` 对每次 IMGUI 事件都收集／排序当前 Site 人员，并绘制包含屏幕外人员的全部卡片；每张卡片再次 `Enum.GetValues` 创建职业数组。本轮将人员收集限制为每帧一次（World／Site 切换立即失效），只计算可见及边缘卡片，职业选项复用静态数组。职业命令、暂停与数据权威不变。
5. WorldMap 已按 sourceHash 复用地形纹理，但持有纹理的普通 C# renderer 没有跟随面板销毁释放。本轮补对称销毁；普通关闭／重开地图继续复用缓存，不提高纹理尺寸或改变地图坐标。
6. `HostWorldMapPanel.DrawGeography` 首次打开地图时额外遍历 7600×3400 共 25,840,000 个导航格，并在每格执行 Cell→World→Cell 转换来收集道路。允许对 `Core/World/Surface/SurfaceGroundNavigation` 增加窄只读派生接口：在它本来必需的导航逐格初始化过程中顺便收集精确水平道路段，Host 首次打开时只消费稀疏段列表。筛选仍为 Road 且非 Water/Bridge/Solid，桥梁由原 MapPrimitives 绘制；不按显示纹理采样道路、不丢窄路、不改寻路或 Snapshot。此举消除首次开图的额外全图遍历，并把少量派生工作并入已有加载流程；不承诺加载时间或 FPS 的测量结果。

缓存只保存已加载内容定义的派生索引；重新绑定、切换 Surface、退出表现或读取新世界时失效，动态破坏／建造状态不缓存进静态索引。`RoadSpans` 只是不可变导航格的只读显示投影，不新增 Core／Data authority；不修改 `Packages`、`ProjectSettings`、Freeze 或历史 Demo Runtime，不启动封板迁移清理。

## 全局性能与呈现审查目录（8 项）

下列行号是本轮审查定位，后续编辑可直接按方法名查找；未测量项不作为已发生卡顿的断言。

| 范围 | 当前证据 | 本轮结论／边界 |
|---|---|---|
| 静态物件与 Chunk | `ContinuousOutdoorSurfaceRuntime.cs:1231、1327、1448` | 已用稀疏 Chunk 索引替代建表现／合成阻挡时的重复全表筛选；原严格边界和列表顺序保留，破坏状态仍实时读。 |
| 运行建筑与创建分配 | `ContinuousOutdoorSurfaceRuntime.cs:1278`、`HostMapObjectVisualFactory.cs:50` | 已先过滤空间与既有 owner，再创建临时表现描述；共享组件列表池消除每段墙多次数组分配，同一次缩放／对齐共用 Renderer 列表。 |
| NPC 路径与感知预算 | `HostNpcScheduleMover.cs:31、165、336` 每帧最多 2 次 A*，但仍扫世界实体；`HostNpcAutonomousCombat.cs:19、171` 约 0.3 秒感知、可见对象双层候选比较 | 已有路径预算不能写成“毫无预算”；密集 NPC 的扫描／感知仍是需要独立审查的 CPU 路径。本页只读，不改本轮 NPC 负责范围，最终改动以其实施记录为准。 |
| 人员 UI | `HostMortalWorkPanel.cs:19、104、123` | 已减少同帧 IMGUI 重复收集和离屏卡片查询，复用职业数组；打开／世界切换／Site 切换及时刷新。暂停、职业命令和显示字段不变。 |
| 墙体渲染与拾取 | `WoodWallTile.prefab:36、55` 单 SpriteRenderer、共享默认材质；`HostMapObjectVisualFactory.cs:39` 单层木墙分支；`HostWorkTargetMode.cs:216`→`HostMapObjectRegistry.cs:133` 悬停仍扫描已加载可破坏物 | 本轮保留低复杂度墙体和逐格身份。844 段可能同时加载，不能把共享材质等同于一个 draw call。逐格排序、对象数量及悬停扫描仍需现场 profiling；不无证据引入整圈合批或改点选优先级。 |
| 世界地图 | `HostWorldMapPanel.cs:321` 消费 `SurfaceGroundNavigation.RoadSpans`；`HostSurfaceWorldMapRenderer.cs:35` 最长边≤4096且按 sourceHash 缓存 | 已消除首次开图额外 25,840,000 格道路重扫，保留精确窄路与桥显示；面板销毁释放地形纹理。仍有首次地形颜色数组／纹理上传及道路段 GUI 绘制开销，未测耗时，不扩大分辨率。 |
| 地表与地物密度 | `HostDemoTileMap.cs:148、664、713、785` 连续 Chunk 草地采用紧凑 Mesh 与 sharedMaterial | 已有地表合并和邻域加载继续复用，不能误称每格草地都新建 GameObject。本轮没有增加草木数量、材质副本或资产复杂度；对象密度与场景构图由内容作者决定。 |
| 遮挡与交互可读性 | `HostMapVisualGeometry.cs:35` 共用脚点排序；`HostHousingAreaSelection.cs:174` 仅 Repaint 绘选中细框；`HostMortalWorkPanel.cs:139` 使用行动／住处／阻碍文本 | 保留脚点、房门与床层及真实交互范围；未凭静态代码判定视觉已合格。围墙接缝、人物遮挡、窄门选取和滚动后的职业按钮需短场景复看，不以更高分辨率掩盖布局问题。 |

## 实际实施与验证记录

- 只修改本页对应的 Host 文件和 `Core/World/Surface/SurfaceGroundNavigation.cs` 窄只读投影；320～322 原有布局、单格木墙与相关未提交修改均保留。`HostDemoTileMap` 同时移除两个已无调用的私有组件处理副本，Bounds 统一复用 factory。
- 索引／组件池阶段 `XianXia.Unity` 基础离线编译通过（162 sources / `ALL_OK`）；随后人员面板、纹理释放与道路投影增量由主会话统一基础编译，尚不把前一次结果覆盖为这批增量的编译结论。
- 本轮涉及文件的 `git diff --check` 无错误；静态核对确认道路筛选位掩码与原 WorldMap 一致，导航仍读取 `_cells`／`WalkGrid`，道路显示不反向影响规则。未写／运行自动测试，未启动 Unity，未测 FPS、draw call、加载或 GC 峰值。

## 验证与人工观察

只做基础编译和必要的静态引用／差异检查，不编写或运行自动测试，不启动 Unity。静态减少重复扫描与短命对象不等于已测得帧率提升；实际首次加载、跨 Chunk 卡顿和帧率仍需制作人观察。

人工焦点：新开局沿荒村四侧缺口进出并跨 Chunk；逐格选中／破坏木墙，确认阻挡随破坏更新；放置一段运行建造物后离开重进及存读档，确认外观、交互和通行一致。工作面板滚到首／末名人物后切换职业，确认按钮仍作用于当前姓名；首次／再次打开世界地图观察道路、桥与加载停顿。洞口显隐及房屋跨 Chunk 接缝顺路观察，不重跑旧系统整套验收。
