# ADR-0037：External Content Authoring 工具链与旧地图 Content 迁移方向

> 日期：2026-09-15
> 状态：**Accepted Design Direction / Not Implemented（已采纳设计方向／尚未实现）**
> 决策者：制作人
> 关联：[ADR-0036 连续世界制作与去 Hex 产品方向](ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md)、[2N 连续世界制作与合成](../../20-systems/2N-continuous-surface-world-authoring-and-composition.md)、[36 ContentPackage／Mod Ready](../../30-tech/36-content-package-and-mod-architecture.md)、[41 路线图](../41-roadmap.md)、[106 Content Authoring Editors Plan](../106-content-authoring-editors-plan-v0.1.md)、[112 MapEditor 用法](../112-map-editor-usage.md)、[128 WorldGraphEditor 用法](../128-world-graph-editor-usage.md)、[130 LocalPlaceEditor 用法](../130-local-place-editor-usage.md)、[215 W2A Surface Geography](../215-surface-geography-w2a-handoff-2026-09-11.md)
> **本 ADR 只负责两件事：External Editor 工具链／生命周期，以及旧地图 Content 的迁移方向。地图架构与去 Hex 产品方向由 [ADR-0036](ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) 锁定，本 ADR 不重写、不建立第二份地图 authority。**

## Context

### 现状审计（2026-09-15，对真实仓库静态核对）

**External Editor 现状**

- `ExternalTools/ContentAuthoring/` 下现有 **10 个独立编辑器工程**：`PackageBrowser`、`CharacterNpcEditor`、`ManualArtEditor`、`QuestEditor`、`EventEditor`、`WorkAreaEditor`、`LocalPlaceEditor`、`MapEditor`、`RegionEditor`、`WorldGraphEditor`；另有共享库 `Shared` 与 `Shared.Tests`，共 **12 个 `.csproj`**、**1 个 `ContentAuthoring.sln`**。
- 构建／启动脚本：`publish.ps1`、`_launch-editor.cmd`、`发布-所有编辑器.cmd`、10 个 `启动-<Editor>.cmd`。
- `Directory.Build.props` 已把 `bin` / `obj` 重定向到 `.build/<Project>/`（隐藏中间产物，避免误双击工程内的 `bin\Release\*.exe`）。
- `publish.ps1` 已统一使用 `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`；但：
  - app 列表**硬编码在脚本里**，与 `sln`／README／启动脚本之间没有任何单一真源；
  - 正式输出仍是**多层子目录** `Apps/<EditorName>/<EditorName>.exe`；
  - **没有** staging／all-or-nothing 语义，**没有** stale exe 清理；
  - 每次都是同名覆盖，但仍可能被未来会话改成时间戳目录（无契约约束）。
- `ExternalTools/ContentAuthoring/.gitignore` 已忽略 `.build/`、`Apps/`、`publish/`、`bin/`、`obj/` → **Apps/ 与 .build/ 是生成产物，不是版本真源**（方向正确，需要写成契约）。
- `Apps/` **审查时观察到**（2026-09-15 的一次性构建产物状态，**不是长期架构事实**）：10 个 `<EditorName>/<EditorName>.exe`，self-contained + single file，各约 154 MB。正式 Architecture 只锁发布目录、构建方式与生命周期（见 Decision）；当天磁盘里恰好有几个 exe、多大，不写入 long-term Current Architecture。
- `.build/` **审查时观察到**已积累 **18 个子目录**，其中包含**已删工程的遗留**：`JobWorkAreaEditor`、`NpcEditor`、`RoleTemplateEditor`、`TmpValidateHuangcun`、`_check_cne`、`CharacterNpcEditor_*_wpftmp` → 说明中间产物区会长期残留过期目录，需要由工具链自己收口。
- `README.md` **已过时**：正文仍写「**六个**独立工程」，编辑器表只列 **9** 个（漏 `WorldGraphEditor`），也没有任何 Active／Legacy 分区，没有「唯一构建入口」，也没有说明 Apps 的最终形态。

**Content 现状（真实计数）**

- `Content/BaseGame/Data/` 定义数：`mapLayout` **37**、`localPlaceSet` **35**、`hexWorld` **2**、`worldRegion` **1**、`outdoorSurface` **2**、`outdoorSurfaceGeography` **1**、`worldSpatialRules` **1**、`worldSiteEconomy` **1**（其余为人物／势力／任务等业务 Content）。
- `base:surface_main_wilderness_v1`：**646** chunks、**6** `siteRegions`（`site_chengzhen` / `site_guanai` / `site_huangcun` / `site_lingdi` / `site_linjian` / `site_zhuangyuan`）、**77** `sitePlacements`、**17** `sitePlaces`、**18** `openingEntityAnchors`（全部属于 `base:site_huangcun`）。
- 全部 646 个 chunk 的 `sourceMapLayoutId` 都是 `base:map_wilderness_plain_fallback` → **基础地形仍整体来自 fallback 图**；W2A geography 目前以独立 bake 定义叠加（见下）。
- `base:hex_world_travel_mvp_30x15`：7 个 site，其中 `continuousOutdoor=true` **6** 个；`base:site_editor_8` 为 MapEditor 遗留占位（`localMapId` 空、无 surface region）。
- `base:hex_world_ch01`：30 个 site，`continuousOutdoor=true` **0** 个（大图未迁移）。
- `base:scenario_ch01_reference` 仍通过 `openingHexWorldId` + `openingLocalPlaceSetId` 开局；另两个 scenario 仍用 `openingWorldRegionId`。
- **Authoring/Runtime 分离的先例已经存在**：`ContentAuthoring/Worlds/w2a_surface_geography_source_v1.json`（仓库根的 authoring source，**Git 跟踪**）经 baker 产出 `Content/BaseGame/Data/Worlds/w2a_surface_geography_baked_v1.json`（runtime definition，被 `ContinuousOutdoorSurfaceRuntime`、`HostDemoTileMap`、`HostWorldMapPanel`、`PlayerPartyHexTravelService` 消费）。这证明「authoring source → bake → runtime content」链路可行，但也说明它目前是**孤例**，尚未成为统一契约。

### 问题

1. 制作人没有一个稳定、唯一、不会被各轮会话重新发明的「一键编译所有编辑器」入口；交付路径（`Apps/<A>/<A>.exe` vs `.build/...`）也未被契约锁定。
2. Editor app 列表、生命周期状态（谁是正式工具、谁只是修旧 Content）、发布集合散落在脚本／README／sln 中，会持续漂移。
3. 旧地图 Content 与新一代 authoring（ADR-0036 的 Composer／FineEditor）之间**没有正式的 Content 边界与迁移分期**；如果不写清，最自然的错误做法就是把新地形／Blueprint 字段继续叠进旧 `mapLayout`，或把 `WorldGraphEditor` 的 Hex document 原地改造成新 Composer。

本 ADR 锁定方向；**不实施 MAP-01，不改任何代码、脚本、Content JSON、场景或 Prefab。**

---

## Decision

### 1. 唯一日常构建入口与正式发布输出

- External Content Editors 只允许存在**一个**正式日常构建入口：`编译-所有编辑器.cmd`（或仓库最终确认的同义中文入口）。现有的 `发布-所有编辑器.cmd` 是该入口的**当前形态**，方向是收敛为上述契约（名称最终由实现轮确认并保持唯一，不得同时存在多个等价入口）。
- 制作人**不应该**需要：逐工程 `dotnet publish`、手动翻 `bin/Release`、找时间戳目录、找临时 publish 文件夹。
- 正常 agent **不得**每轮自行创造新的 publish destination 或新的「发布」脚本。

**正式发布目录锁定为** `ExternalTools/ContentAuthoring/Apps/`，且所有正式可运行编辑器 exe **平铺**在该层：

```
ExternalTools/ContentAuthoring/Apps/
  PackageBrowser.exe
  CharacterNpcEditor.exe
  ManualArtEditor.exe
  QuestEditor.exe
  EventEditor.exe
  WorkAreaEditor.exe
  LocalPlaceEditor.exe
  MapEditor.exe
  RegionEditor.exe
  WorldGraphEditor.exe
  （未来）WorldComposer.exe
  （未来）FineEditor.exe
```

- **不再要求**用户进入 `Apps/MapEditor/MapEditor.exe`、`Apps/QuestEditor/QuestEditor.exe` 这类多层子目录。
- 当前发布已是 `SelfContained + PublishSingleFile`，因此 **flat exe output 技术上可行**，属于纯输出布局调整。
- 过渡期允许 `Apps/<EditorName>/` 旧目录存在，但**唯一正式入口**与文档只承认 `Apps/<EditorName>.exe`。

### 2. `Apps/` 与 `.build/` 的角色（生成产物，不是源码）

- `Apps/` 继续视为 **generated local build output**，保持 **gitignored**。用户可以删除整个 `Apps/`，然后通过唯一 Build All 入口完整重建。
- **exe 不是 Content source，也不是版本真源**；不得把 Apps 下的产物当作交付物提交或引用。
- `.build/` 继续**只**作为 `bin` / `obj` / publish staging。
- `.build/` **不可**作为用户日常启动 Editor 的位置。
- Agent **不得**向制作人回复「新版 exe 在 `.build/foo/random_timestamp/...`」；用户正式只认 `Apps/`。
- 由于 `.build/` 会残留已删工程的目录（审查时观察到 18 个，含 3 个已删工程与若干临时目录），未来 Build All／Clean 流程应能安全清理**不再受支持**的中间目录；清理范围与实现细节留实现轮。

### 3. Build All 的行为契约

未来正式实现必须满足：

1. **一键运行** → 构建所有当前仍受支持的 Editor → **全部成功以后** → 用新版本覆盖 `Apps/` 中旧 exe。
2. **all-or-nothing publication semantics**：推荐设计为「临时 staging → 全部 publish 成功 → 再统一替换 Apps」。**不允许**「Editor A 成功先覆盖正式 exe、Editor B 编译失败，最终 Apps 只更新了一半」。
3. **同名 Editor 永远覆盖同一个** `Apps/<EditorName>.exe`。**禁止**每次创建新日期目录、timestamp 文件夹、`publish-2` / `publish-final` / `release-new` 等重复产物。
4. **stale exe 清理**：如果某个 Editor 已从正式 manifest 中删除，下一次 Build All 后 `Apps/` 不应永久残留它的旧 exe，误导制作人继续启动已经退休的工具。
5. 具体 PowerShell 实现方式、staging 目录位置与替换策略：**留到 Toolchain implementation pass**（本 ADR 只锁行为契约）。

### 4. Editor manifest／单一 Editor 列表

- 未来应建立**单一 Editor metadata source（manifest）**，至少表达：`EditorName`、`LifecycleStatus`、`PublishByDefault`、`Replacement` / `Notes`。
- **禁止**在 `publish.ps1`、`sln`、README、各 `启动-*.cmd` 中分别手写一份互相漂移的列表。当前 app list 硬编码在 `publish.ps1` 正是要消除的状态。
- manifest 的具体载体（JSON / PowerShell data / MSBuild item）**尚未锁定** → `Implementation Detail — Open`。
- Build All、stale 清理、README 生成（可选）都应消费同一份 manifest。

### 5. Editor 生命周期正式概念

未来所有 External Editor 至少区分三类：

| 生命周期 | 含义 |
|---|---|
| **Active** | 正式推荐生产工具，制作人应使用它制作新 Content |
| **Legacy Compatibility** | 只为维护／迁移**既有** Content 而保留；不应再用它制作新 Content |
| **Planned Replacement（Transitional）** | 已有明确替代工具方向，等替代可用 + Content 迁完后退出 |

- **不得**因为「源码还在」就假装它仍是推荐生产工具。
- `README` 与启动器必须明确告诉制作人：哪些是正式工具，哪些只是为了修旧 Content 暂时保留。
- **`Lifecycle = Legacy` ≠ 无法运行**：只要一个 Legacy Editor 仍是修当前兼容 Content 的必要工具，`Build All` **仍然编译／发布它**。等它正式被删除后，才从 manifest 与 `Apps/` 中退出。

### 6. 各编辑器当前定位与未来命运

| Editor | 当前定位 | 未来命运 |
|---|---|---|
| **PackageBrowser** | Active | 与新地图迁移基本正交，长期保留（包总览／校验） |
| **CharacterNpcEditor** | Active | 长期保留（人物规则、工区偏好、可控制性、场景挂载） |
| **ManualArtEditor** | Active | 长期保留（功法／斗技） |
| **QuestEditor** | Active | 长期保留（任务） |
| **EventEditor** | Active | 长期保留（事件） |
| **WorkAreaEditor** | Active | 长期保留**规则编辑**（活动、容量、权限、resident tags）。未来「WorkArea 在世界哪里」改由 Blueprint／FineEditor placement 表达；FineEditor 可能吸收其空间放置，但**规则属性编辑仍有价值**——因此**当前不得删除** |
| **WorldGraphEditor** | **Legacy Compatibility** | `Planned Replacement: WorldComposer`。其数据模型高度依赖 `HexCoord`／`HexWorld`／Hex terrain／road／Site footprint／`AnchorHex`／`PresenceHex`／`OccupiedHexes`／Territory Hex／FactionFlag Hex，与 ADR-0036 的 future direction 冲突。**禁止**把它的 `HexDocument`／`HexViewport`／Hex content model 原地逐步改造成新 `WorldComposer` authority |
| **MapEditor** | **Legacy Compatibility**（Outdoor／LocalMap 格点） | `Planned Replacement: FineEditor`。其正式数据模型仍是旧 `mapLayout` / `worldRegionId` / `origin` / `cellSize` / LocalMap-oriented placements。**禁止**原地把 `mapLayout` schema 扩成新地图 authoring 超级格式 |
| **RegionEditor** | **Legacy Compatibility** | 编辑 `worldRegion` / `locations` / `adjacentIds` / presentation coordinate，属旧 world graph／LocalPlace 导航体系。最终 Outdoor migration 完成后删除；**当前不能立即物理删除**，因为仍有旧 Content／Quest／Event spatial references 可能依赖 worldRegion/location |
| **LocalPlaceEditor** | **Legacy Compatibility** | 普通 Outdoor WorldSite 未来不再依赖 `LocalPlaceSet → LocalMap`；但**真正** building interior／cave／dungeon／secret realm 仍可能需要局部 Place／marker authoring。职责将**收窄为 Interior / Isolated Surface authoring**，可能演化／改名为 `InteriorPlaceEditor`（名称与 schema **未锁定**） |

### 7. UI 复用的边界：可复用交互，不可继承数据模型

- 大型 Editor（`WorldGraphEditor`、`MapEditor`）各自重复了 viewport／pan／zoom／selection／undo；**MAP-01 不应再复制第三套**。
- 推荐建立一个小型 **`Shared.EditorFramework`**（或等价 shared UI layer），**只放**：viewport transform、pan、zoom、grid coordinate conversion、selection primitives、dirty state、undo/redo infrastructure。
- `Shared.EditorFramework` **绝对不能放**：Hex、`mapLayout`、Surface gameplay、WorldSite domain 等业务规则。其具体 API 为 `Open Implementation Detail`。
- 未来 `WorldComposer` / `FineEditor` 可以参考／提取 `MapEditor` 与 `WorldGraphEditor` 的**纯 UI interaction**（pan／zoom／viewport rendering／selection／undo-redo 使用模式），但**禁止**依赖 `HexCoord`、`HexWorldEditorDocument`、Hex site footprint model，也禁止把旧 `mapLayout` document 作为新架构的正式数据模型。**复用 UI mechanic ≠ 继承旧地图数据模型。**
- 当前 `Shared/HexWorld/*` 主要服务 `WorldGraphEditor` 与 Hex editor tests。`WorldGraphEditor` 正式退休后，这一整块 editor-only Hex authoring code 与对应 tests 可以一起删除；**不得**让新的 `WorldComposer` 复用它作为隐藏 dependency。

### 8. `WorldGraphEditor` 中的战略编辑功能必须迁出

- `WorldGraphEditor` 当前同时包含 `FactionManagerWindow` 与 `OpeningStrategicEditorWindow`，负责 Faction Content、opening alliances、vassals、wars、player opening faction。这些**不是 Hex 地图功能**。
- 因此：`WorldGraphEditor` 最终退休前，这些战略 Content 功能**必须迁出**，推荐形成独立的 **`StrategicEditor`** 或其它独立战略 Content Editor（最终名称 = `Open Naming Decision`）。
- **不得**因为 `WorldGraph` 退休而删除势力／开局外交的 authoring 能力。

### 9. 旧 Editor 不应现在物理删除

- 当前 `WorldComposer` / `FineEditor` **尚未存在**，旧 Content 也**尚未完成迁移**。
- 因此目前**不要删除** `WorldGraphEditor`、`MapEditor`、`RegionEditor`、`LocalPlaceEditor` 的源码。
- 正确顺序是：**Replacement usable → migrate source Content → production path no longer needs legacy editor → then delete editor**。

### 10. Authoring Source 与 Runtime Generated Content 的正式边界

地图相关 Content 未来必须分成两类，**不能继续混在同一个手改 JSON 模型里**：

| 类别 | 谁编辑 | 谁消费 | 说明 |
|---|---|---|---|
| **A. Authoring Source** | `WorldComposer` / `FineEditor` | 只有 baker | 制作人手工创作真源；不是 gameplay definition |
| **B. Runtime Generated Content** | 由 **Bake** 产生 | 游戏 runtime loader | 只读最终产物；不保留 authoring pieces |

### 11. Authoring Source 的存放位置方向

- 地图 authoring source **不应**继续作为正常 Runtime Data 直接塞进 `Content/BaseGame/Data`，也不应被 runtime loader 当作正式 gameplay definition 加载。
- 推荐独立 authoring root（方向，不是最终目录名）：

```
ContentAuthoring/
  Worlds/
    MainContinent/
      ...
```

  或项目最终统一的 authoring-source root。**具体目录名在实现阶段再定**；锁定的是 **Authoring Source ≠ Runtime Content**。
- 已有的 `ContentAuthoring/Worlds/w2a_surface_geography_source_v1.json` 是这一方向的**现存先例**，可作为起点，但 W2A 的临时 source/bake 文件格式**不承诺永久保留**。

### 12. Authoring Source 的概念类型（当前名，不锁 schema）

未来至少需要表达三类概念：

| 概念（当前名） | 含义 |
|---|---|
| **SurfaceWorldComposition** | 整张大陆的组合源：macro terrain、overlay、Blueprint 放置、Detail Patch 引用 |
| **WorldSiteBlueprint** | 任意整数 Surface Cell 尺寸的 Site 精细源（村／镇／城／宗门等） |
| **DetailPatch** | 任意尺寸 Surface Cell 的精修覆盖源 |

- **不要现在强锁**最终 `type` string、文件扩展名或 JSON schema 字段名。
- 这三类的职责、比例与合成优先级由 [ADR-0036](ADR-0036-continuous-surface-world-authoring-and-de-hex-product-direction.md) §2／§3／§4 与 [2N](../../20-systems/2N-continuous-surface-world-authoring-and-composition.md) 定义，本 ADR 不重复。

### 13. Runtime Output

Bake 之后，游戏正常**只读取** Final Continuous Surface runtime content，它至少包含／派生：

- Runtime chunks；
- final terrain 与 final geography；
- world object placements；
- WorldSite 位置与 content；
- navigation input；
- WorldMap LOD／cache input。

运行时**不需要知道**这个位置来自哪个 Blueprint、哪个 Detail Patch、哪张旧 LocalMap。

### 14. `main_wilderness_surface_v1` 的未来角色

- 它当前已经是主要 Continuous Surface **Runtime 容器**，但同时仍包含：chunk source、old Site bake、LocalMap bridge、fallback data。
- 未来它的等价物应成为**纯 Bake Runtime Output**，不再人工承担 WorldSite source authoring、LocalMap mapping 或 Hex authoring bridge 职责。

### 15. `siteRegions` / `sourceLocalMapId` 的最终退出

- 当前 Continuous Surface 仍用 `siteRegions[]` + `sourceLocalMapId` 把旧 Map Layout bake 到 Surface。这是 **Legacy Compatibility**。
- 未来链路是：`WorldSiteBlueprint` 放置在 `WorldComposition` → **Bake** → 直接得到最终 placements 与 world coordinates；runtime **不再需要知道** WorldSite 原来来自哪张 LocalMap。

### 16. 旧 Content 迁移分类（逐类）

**`mapLayout`（当前 37 个）—— 不是同一种未来命运：**

| 类别 | 例子 | 未来处理 |
|---|---|---|
| **A. Outdoor WorldSite maps** | 村庄、城镇、关隘、宗门 outdoor | → WorldSite Blueprint；迁移完成后旧 `mapLayout` 删除 |
| **B. True Interior / Cave / Dungeon** | `ch01_cave_map` 等 | 未来仍可能是 separate Interior Surface／Blueprint；**不得**因为 Outdoor de-LocalMap 就直接删除 |
| **C. Fallback / stub / obsolete test maps** | `wilderness_*_fallback_map`（当前 646/646 chunk 的 source）、`world_node_stub_map`（实测已无引用）、`huangcun_01.json`（实测仅自引用）、`player_camp_map`（已从 Surface `siteRegions` 退役，但仍被 `player_camp_places.json` 与 `Ch01HexPrototypeMapBuilder.PlayerCampLocalMapId` 引用） | Final Surface authoring 成立后 → 删除；删除前必须先清掉上述残留引用 |

具体**逐文件**分类在 MAP migration pass 执行；本 ADR 只锁定分类维度。

**其它 Content：**

| 对象 | 未来处理 |
|---|---|
| **`localPlaceSet`（当前 35 个）** | 普通 Outdoor：**不再作为空间 authority**。其中真正仍有 Gameplay 意义的内容拆到正式系统（WorkArea、Housing、Spawn、SiteCore、object placement、scenario anchor 等）。Interior／Cave：未来可保留新的局部 Place marker 概念。**不得**让 Outdoor LocalPlaceSet 继续决定 Continuous Surface 空间 |
| **`hexWorld` + `openingHexWorldId`（当前 2 个 hexWorld）** | 现在**不能删**（仍有正常 compatibility consumer）。Future：WorldMap／Travel／Site／Scenario 迁出 Hex 后，HexWorld runtime JSON 最终退出正常 BaseGame Content；Scenario opening 改由 Surface + exact WorldPosition + WorldSite anchor 等新来源表达（具体 schema 后续决定） |
| **`worldRegion` + `openingWorldRegionId`（当前 1 个 worldRegion；另 2 个 scenario 仍引用）** | 现在不能删。Future：普通 Outdoor Scenario bootstrap 直接进入 Continuous Surface + precise position／Site anchor → **Outdoor WorldRegion 最终退出正常 authority**。Interior 若需要逻辑 grouping，另行定义 |
| **W2A geography（`outdoorSurfaceGeography` + `ContentAuthoring/Worlds/*` source）** | 已证明 Road／Water／Bridge／Solid 可以从 authoring data bake 到 Surface，并同时服务 runtime navigation 与 WorldMap presentation——**这个设计原则保留**。但 W2A 的**临时 authoring/baked 文件格式不需要永久保留**，未来由 WorldComposer 的 terrain／geography layers 取代 |
| **fallback maps** | 当前 646/646 chunk 都指向 `map_wilderness_plain_fallback`——即基础地形仍是迁移期占位。Final geography 成立后按 §16 表 C 删除 |
| **业务 Content**（characters / factions / cultivation / combatArts / resources / items / buildings / siteEconomies / schedules / quests / events …） | **不重写**。地图迁移只迁移其中已存在的 **Hex / LocalMap / Location / WorldRegion 空间引用**，不得把地图迁移变成整个 Content package rewrite |
| **WorkArea Definition** | 规则部分（activity、capacity、privileges、rules）属 Gameplay Content，**继续存在**；「WorkArea 在世界哪里」未来由 WorldSite Blueprint／FineEditor placement 表达。**规则定义与空间 placement 分开** |

### 17. 禁止原地扩 `mapLayout` 成新地图格式（迁移纪律）

**禁止**未来采用这种演进方式：

```
旧 mapLayout
 + terrain layers
 + World Editor Cells
 + Blueprint
 + composition
 + Surface bake fields
 …不断叠字段，变成一个同时兼容所有时代的巨大格式
```

正式策略是：**新 Authoring Source schema + Baker + Runtime Final Surface**；旧 `mapLayout` **只作为 migration source**。

### 18. 迁移阶段（方向；编号可按 roadmap 调整）

| 阶段 | 内容 |
|---|---|
| **Editor Toolchain Cleanup** | 唯一 Build All 入口、`Apps/` 平铺输出、staging all-or-nothing、stale 清理、Editor manifest、README／启动器生命周期分区 |
| **MAP-01** | 建立新 Authoring schema／WorldComposer／FineEditor 基础；**青石荒村 pilot**；旧 source 暂留 |
| **MAP-02** | WorldMap 改为 Final Surface LOD／exact world projection |
| **MAP-03** | Scenario／PlayerTravel／WorldSite 正常产品链退出 Hex／LocalMap authority；开始逐个迁移 Outdoor Maps／LocalPlaces |
| **MAP-04** | 清理：HexWorld JSON、Outdoor WorldRegion、fallback maps、临时 W2A files、legacy editor/runtime compatibility |

**阶段顺序表达（roadmap 用）：**

```
Editor Toolchain Cleanup
  ↓
MAP-01 WorldComposer / FineEditor foundation + Huangcun pilot
  ↓
MAP-02 WorldMap Surface LOD
  ↓
MAP-03 product de-Hex / de-LocalMap consumers
  ↓
MAP-04 legacy content / editor retirement
```

> 这不代表任何一个阶段已经开始；具体 CW／MAP 编号以当时的 [41 路线图](../41-roadmap.md) 为准。

### 19. Legacy Editor 删除顺序与 Content 迁移绑定

| Editor | 删除前置条件 |
|---|---|
| `WorldGraphEditor` | Hex product paths 迁完 **且** 战略 faction/opening 功能迁出（→ `StrategicEditor`） |
| `MapEditor` | Outdoor Blueprint／Interior replacement 足够 **且** 旧 `mapLayout` 迁完 |
| `RegionEditor` | Outdoor worldRegion／location graph consumers 迁完 |
| `LocalPlaceEditor` | Outdoor LocalPlace consumers 迁完以后收窄成 Interior authoring，或由新 Interior tool 取代 |

**不得**先删编辑器再迁 Content。

### 20. 第一迁移样本：青石荒村（MAP-01 pilot）

**为什么选荒村：**

- 它是当前唯一有 **完整 authored 复杂度** 的 Site：`68` 个 `sitePlacements`（占 77 中的绝大多数）、`12` 个 `sitePlaces`、**全部 18 条** `openingEntityAnchors`，并且同时涉及住房、农田、工作区、主管住房、洞府入口、树木／墙／矿石等 kind。
- 它同时是 **NPC 日程／工作、opening population、Schedule 复工** 当前唯一被反复人工复验的场景 → 迁移后**表现必须与现状一致**，是天然的 A/B 对照样本。
- 其余 5 个连续 Site 各只有 1 个 placement / 1 个 place → 复杂度不足以验证 Blueprint 的任意尺寸、跨 chunk 与多 kind 表达能力。
- 迁成 **Huangcun WorldSite Blueprint** 后，Composer 在**原 world position** 放同一 Blueprint，Bake 后游戏表现应与现有荒村一致。

**Pilot 阶段纪律：**

- 第一轮允许**新 Blueprint/Bake 与旧 compatibility source 并存**用于 A/B 对照。
- 但必须避免 runtime **双重 materialize**（同一 Site 不得同时被旧 source 与新 bake 各物化一次）。
- 确认新路径成功后，**下一阶段**才正式删除旧荒村 map／localplace source。

### 21. 本轮只做文档：ExternalTools README 列为待办

- 本轮是 **Documentation only**，只允许修改 `docs/`。因此 `ExternalTools/ContentAuthoring/README.md` 与启动器脚本的**实际改写留到 Toolchain Cleanup 实现轮**。
- 该轮必须至少做到：① 修掉「六个独立工程」等过时数字；② 补齐遗漏的 `WorldGraphEditor`；③ 明确 **Recommended / Active Editors** 与 **Legacy Compatibility Editors** 两个分区；④ 说明 **Generated Apps location**（`Apps/<EditorName>.exe`）；⑤ 指明**唯一 Build All command**；⑥ 不再把 exe 平铺列出却不告诉制作人哪些已不该用于制作新 Content。

---

## Current Implementation vs Locked Future Direction

| 主题 | Current Implementation（2026-09-15 实测） | Locked Future Direction |
|---|---|---|
| 构建入口 | `publish.ps1` + `发布-所有编辑器.cmd`（等价但未定为唯一契约） | 单一 `编译-所有编辑器.cmd`；agent／制作人只用它 |
| 发布输出 | `Apps/<EditorName>/<EditorName>.exe`（多层） | `Apps/<EditorName>.exe`（平铺，self-contained single file） |
| 发布语义 | 逐 app 直接覆盖，无 staging、无 stale 清理 | staging → 全部成功 → 统一替换；all-or-nothing；清理 stale exe |
| App 列表 | 硬编码在 `publish.ps1` | 单一 Editor manifest（EditorName／LifecycleStatus／PublishByDefault／Replacement） |
| 生命周期 | README 未区分；漏 WorldGraphEditor；「六个工程」过时 | Active / Legacy Compatibility / Planned Replacement 三分区，README＋启动器可见 |
| `Apps/` `.build/` | 已 gitignored（方向正确） | 契约化：generated、可整体删除、可重建；`.build/` 只做 staging |
| Editor 数据模型 | `WorldGraphEditor`=Hex；`MapEditor`=`mapLayout` | 新 `WorldComposer`／`FineEditor` 用新 document；旧模型**不原地改造** |
| 战略 authoring | 混在 `WorldGraphEditor` 内 | 迁出为 `StrategicEditor`（名称 Open），能力不丢 |
| Authoring／Runtime Content | 混在 `Content/BaseGame/Data`；W2A 已有分离先例 | Authoring Source（独立 root）→ Bake → Runtime Final Surface |
| 旧 `mapLayout` 等 | 37 mapLayout／35 localPlaceSet／2 hexWorld／1 worldRegion 仍是现状依赖 | 按 §16 分类迁移；旧格式只作 migration source |
| 地图实现 | Surface Cell／Chunk streaming 已存在；全部 chunk 仍 fallback 地形；HexWorld shell | Final Continuous Surface 是唯一户外地理真源（ADR-0036） |

---

## Deferred / Open Design Questions

以下均为**故意未锁死**，且**不是 MAP-01 基础实现的 blocker**：

- Editor manifest 的具体文件格式（JSON / PowerShell data / MSBuild item）。
- `StrategicEditor` 的最终名称与组织方式。
- `LocalPlaceEditor` 最终是否改名 `InteriorPlaceEditor`（及其 schema）。
- `FineEditor` 与 `WorkAreaEditor` 的最终整合程度（空间 placement 与规则编辑的边界）。
- `WorldComposer` / `FineEditor` 是否永久保持两个独立 exe。
- Authoring source 的最终目录结构与 JSON `type` 命名。
- Runtime generated surface 的最终文件拆分格式。
- 旧 Content 的**逐文件**迁移分类（本轮只锁定分类维度）。
- Build All 的具体 staging 实现与替换策略。
- `Shared.EditorFramework` 的具体 API。

---

## Non-goals

本 ADR **不实施** Editor Toolchain Cleanup、**不实施** MAP-01，也**不开始** `WorldComposer`／`FineEditor`／Content migration。它不修改：`ExternalTools/ContentAuthoring` 的任何代码／脚本／`csproj`、`Assets/Scripts`、Content JSON、Unity Scene／Prefab、MapEditor 实现或任何 runtime 行为。它不构成任何代码开工授权。

---

## Relations（与已有文档的边界）

- **ADR-0036** 继续负责：Continuous Surface、World Editor Cell、Composer／FineEditor、Final Surface、de-Hex 产品方向（地图 authority）。
- **本 ADR-0037** 负责补充：External Editor 工具链与生命周期、统一发布机制、Authoring Source／Runtime Content 边界、旧 Content 迁移分期与 Legace Editor 退出顺序。
- **[2N](../../20-systems/2N-continuous-surface-world-authoring-and-composition.md)** 是上述两个 ADR 的系统级真源（比例、术语、合成层、MAP-01 proof scope）。
- **不在多个 ADR 重复写两套地图 authority。** 如后续发现冲突，以 ADR-0036 的地图 authority 为准，本 ADR 只管工具链与 Content 生命周期。
- 历史 devlog 与封板记录**不改写**；其中「六个编辑器」「Apps/<X>/<X>.exe」等描述保留为当时事实，以本 ADR 指向为待迁移契约。
