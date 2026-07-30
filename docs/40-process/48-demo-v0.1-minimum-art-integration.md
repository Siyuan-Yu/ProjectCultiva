# 第一批最小可用素材接入清单

> 状态：已接入占位资源 | 优先级：P0 | 最后更新：2026-07-31
> 上级：`45-demo-v0.1.md`
> 关联：`46-demo-v0.1-art-assets.md`、`47-demo-v0.1-ai-art-batches.md`

## 1. 接入原则

- 原型不等待最终美术；缺失资源先用色块占位。
- 每个角色、NPC、地块、建筑和 UI 图标均以**独立 PNG → Sprite → Prefab 引用**接入。
- 后续替换时保留原文件名与同名 `.meta` 文件，Unity 会沿用 GUID，场景和 Prefab 无需重新挂载。
- 导入基准：Sprite、Point、Compression None、32 PPU；角色与建筑 Pivot 为 Bottom Center，地块与图标为 Center。
- 占位图只表达类别和可读性，不代表最终画风。

## 2. 当前最小接入清单

| 文件名 | 用途 | 预期尺寸 | 放置目录 | 可先用占位图 |
|---|---|---:|---|---|
| `CHR_PlayerA_Idle_Down.png` | 玩家角色 A；选择与移动 | 64×64 | `Assets/Art/Characters/Players/Player_A/` | 是，已配置 |
| `CHR_PlayerB_Idle_Down.png` | 玩家角色 B；选择与移动 | 64×64 | `Assets/Art/Characters/Players/Player_B/` | 是，已配置 |
| `CHR_PlayerC_Idle_Down.png` | 玩家角色 C；选择与移动 | 64×64 | `Assets/Art/Characters/Players/Player_C/` | 是，已配置 |
| `CHR_NPC_Supervisor_Idle_Down.png` | 荒村主管站位 | 64×64 | `Assets/Art/Characters/NPCs/Supervisor/` | 是，已配置 |
| `CHR_NPC_Merchant_Idle_Down.png` | 商人／情报 NPC 站位 | 64×64 | `Assets/Art/Characters/NPCs/Merchant/` | 是，已配置 |
| `CHR_NPC_Guard_Idle_Down.png` | 守卫站位；Prefab 可复用 | 64×64 | `Assets/Art/Characters/NPCs/Guard/` | 是，已配置 |
| `TILE_Ground_Grass_01.png` | 荒村基础草地 | 32×32 | `Assets/Art/Environment/Tiles/Ground/` | 是，已配置 |
| `TILE_Ground_DirtRoad_01.png` | 村内道路与区域连接 | 32×32 | `Assets/Art/Environment/Tiles/Ground/` | 是，已配置 |
| `TILE_Ground_Farmland_01.png` | 农田区域 | 32×32 | `Assets/Art/Environment/Tiles/Farmland/` | 是，已配置 |
| `TILE_Ground_ForestFloor_01.png` | 森林探索区域 | 32×32 | `Assets/Art/Environment/Tiles/Forest/` | 是，已配置 |
| `TILE_Ground_SpiritSite_01.png` | 隐藏灵地区域 | 32×32 | `Assets/Art/Environment/Tiles/SpiritSite/` | 是，已配置 |
| `BLD_House_Common_01.png` | 普通住宅；重复摆放 | 128×160（4×5 格） | `Assets/Art/Environment/Buildings/Houses/` | 是，已配置 |
| `BLD_SupervisorHouse_01.png` | 主管府／控制核心 | 192×160（6×5 格） | `Assets/Art/Environment/Buildings/SupervisorHouse/` | 是，已配置 |
| `BLD_Warehouse_01.png` | 仓库／任务交付点 | 128×128（4×4 格） | `Assets/Art/Environment/Buildings/Warehouse/` | 是，已配置 |
| `UI_SelectRing_Player.png` | 玩家单位选中反馈 | 64×64 | `Assets/Art/UI/HUD/` | 是，已配置 |

## 3. Prefab 挂载位置

| 类别 | Prefab 目录 | 结构 |
|---|---|---|
| 玩家 | `Assets/Prefabs/Characters/Players/` | 根节点：碰撞与移动；`Visual`：`SpriteRenderer + ReplaceableSprite`；`SelectionRing`：选择反馈 |
| NPC | `Assets/Prefabs/Characters/NPCs/` | 根节点：碰撞；`Visual`：`SpriteRenderer + ReplaceableSprite` |
| 地块 | `Assets/Prefabs/Environment/Tiles/` | `Visual` 独立引用单个地块 Sprite |
| 建筑 | `Assets/Prefabs/Environment/Buildings/` | 根节点：碰撞；`Visual` 独立引用建筑 Sprite |
| UI | `Assets/Prefabs/UI/` | 独立引用 UI Sprite |

`ReplaceableSprite` 保存逻辑素材 ID、目标 `SpriteRenderer` 和 Sprite 引用。直接覆盖同路径 PNG 后，现有 Prefab 与场景引用不会改变。

## 4. 当前场景内容

场景：`Assets/Scenes/Demo_v0_1.unity`

- 28×18 格荒村灰盒地图
- 草地、十字土路、农田、森林、隐藏灵地
- 三栋住宅、主管府、仓库
- 玩家三人、主管、商人、两名守卫
- 左键单选、Shift+左键多选、右键编队移动
- 屏幕左上显示原型操作说明

## 5. 生成状态与后续替换

Unity 2022.3.6f1 个人版许可证已激活，当前资源已经成功导入并生成：

- `Assets/Scenes/Demo_v0_1.unity`
- 15 个可替换 Sprite
- 15 个角色／NPC／地块／建筑／UI Prefab
- Build Settings 已加入 Demo 场景

后续操作：

1. 使用 Unity **2022.3.6f1** 打开项目根目录。
2. 打开 `Assets/Scenes/Demo_v0_1.unity`，进入 Play Mode。
3. 如需重建，可执行菜单：`XianXia > Build Demo v0.1 Prototype`。
4. 收到正式 PNG 后，覆盖本表对应 PNG，**不要删除 `.meta`**。

生成器遵守“文件已存在则不覆盖 PNG”的规则，因此重复生成 Prefab／场景时不会误删后续提供的正式素材。

## 6. 本批不包含

- 行走、攻击、受伤等复杂动画
- 战斗、修炼、突破、占领逻辑
- 城市、大地图、宗门与高阶技能
- 最终 UI 皮肤及完整图标库

这些内容不会阻塞当前的场景、单位控制和素材替换验证。
