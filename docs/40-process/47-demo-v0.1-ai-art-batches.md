# Demo v0.1 AI 美术生成批次计划

> 状态：可执行批次草案 | 优先级：P0 | 最后更新：2026-07-31
> 上级：`docs/40-process/46-demo-v0.1-art-assets.md`
> 关联：`docs/40-process/45-demo-v0.1.md`
> **本文件只规划批次，不立即生成全部 AI 素材。** 每批生成后先验收风格与可读性，再开下一批。
> 原型开发已用可替换占位图开工，见 `48-demo-v0.1-minimum-art-integration.md`；这不等于开始批量生成 AI 素材。

## 0. 执行原则

1. **第一批只验证整体美术方向，不超过 10 个素材。**
2. **第二批只服务核心闭环：** 荒村探索 → 修炼 → 战斗 → 击败主管 → 占领据点。
3. **后续批次增强体验**，不阻塞 Demo 可玩。
4. AI 优先产出**静态图／方向参考／单帧特效／图标**；4 方向逐帧行走表由人工或专用工具二次制作。
5. 每批结束后必须用同一验收清单通过，再进入下一批。

### 0.1 明确删除／延后（本计划不生成）

| 删除／延后项 | 原因 |
|---|---|
| 高阶技能与复杂法宝特效 | Demo 只有 1 个炼气技能 |
| 大地图／多大陆 | Demo 只有一张荒村 |
| 城市、城主府、宗门建筑 | 不在 Demo 范围 |
| 8 方向完整动画 | 高成本，不影响验证 |
| 独立受伤／死亡精修动画 | 可用闪白与单帧倒地占位 |
| 完整建筑内部场景 | 外部交互 + UI 即可 |
| 大量村民变体、换装纸娃娃 | 不服务核心闭环 |
| 完整 UI 皮肤库 | 先用色块面板 + 必要图标 |

### 0.2 全局 Prompt 片段

**风格尾缀（每条 Prompt 末尾追加）：**

```text
2D top-down 3/4 view game asset, low-detail Chinese xianxia pixel art style,
32-pixel tile scale, consistent proportions, muted earthy palette,
top-left lighting, clean readable silhouette, transparent background,
no text, no watermark, no UI frame
```

**负面词（每条都用）：**

```text
photorealistic, 3D render, side view, front portrait view, isometric mismatch,
blurry, anti-aliased white edge, complex background, text, logo, watermark,
extra limbs, inconsistent costume, cropped feet
```

### 0.3 全局 Unity 处理清单

进入实现阶段后，每个导入资源统一做：

1. PNG RGBA，透明背景干净
2. Texture Type = Sprite；Filter Mode = **Point**；Compression = **None**（Prototype）
3. Pixels Per Unit = **32**
4. 角色 Pivot = **Bottom Center**；物件按落地中心；Tile 居中
5. 裁切到推荐画布（角色 64×64，Tile 32×32，图标 64×64）
6. 统一降色／描边，避免白边
7. 放入 `Assets/Art/...` 对应目录（见美术需求表第九节）
8. AI 原图保留在 `Assets/Art/Source/AI_Generated/`，不覆盖成品

### 0.4 批次通过标准

- [ ] 视角一致（2D 3/4 俯视）
- [ ] 轮廓在缩小后仍可辨识
- [ ] 调色落入荒村土黄／森林绿／灵地青蓝／危险暗红范围
- [ ] 无文字、水印、多余肢体
- [ ] 可对齐 32 px 网格
- [ ] 需要一致性的角色，服装／发型／道具前后一致

---

## 第一批｜风格方向验证（最多 10 个）

**目标：** 确认“低细节中式修仙像素风”是否统一、是否适合荒村 Demo。  
**数量上限：** 10。  
**不要在本批做动画 Sheet。**

| 顺序 | 素材名称 | 数量 | 用途 | 是否需要角色一致性 | AI 生成 Prompt | 生成后的 Unity 处理 |
|---:|---|---:|---|---|---|---|
| 1 | `TILE_Ground_Grass_StyleTest` | 1 | 验证地面密度与色调 | 否 | `top-down fantasy village tileset, seamless grass ground tile, Chinese xianxia cultivation game style, seamless 32x32 pixel tile, tileable edges, low-detail pixel art, muted earthy green-brown palette, orthographic top-down 3/4 compatible, no objects, no text` + 风格尾缀 | 裁成 32×32；做 Tilemap；检查拼接缝 |
| 2 | `TILE_Ground_DirtRoad_StyleTest` | 1 | 验证道路与草地对比 | 否 | `seamless dirt road tile for top-down village, muted brown soil path, soft worn edges, 32x32 pixel tile, tileable, no objects, no text` + 风格尾缀 | 同上；与草地并排铺一段路 |
| 3 | `TILE_Ground_SpiritSite_StyleTest` | 1 | 验证灵地与凡俗色差 | 否 | `seamless spirit ground tile, cyan-teal glowing earth with subtle qi patterns, muted not neon, 32x32 pixel tile, tileable, top-down xianxia style, no objects, no text` + 风格尾缀 | 同上；确认一眼能从草地中区分 |
| 4 | `PROP_Tree_01` | 1 | 验证环境高度与遮挡 | 否 | `single isolated sparse rural tree, top-down 3/4 view, muted green canopy and brown trunk, fits 64x64 canvas, clear silhouette, transparent background` + 风格尾缀 | 64×64；Pivot Bottom Center；检查是否挡住角色脚底 |
| 5 | `PROP_Herb_Concealment_01` | 1 | 验证敛息草辨识度 | 否 | `single isolated concealment herb plant, small pale cyan-silver leaves, distinct from ordinary herbs, top-down prop, 32x32 or 64x64 canvas, transparent background` + 风格尾缀 | 32/64；与普通草药并排对比辨识度 |
| 6 | `BLD_House_Common_01` | 1 | 验证建筑占地与透视 | 否 | `small poor rural Chinese wooden house, top-down 3/4 view, visible roof and front wall, footprint about 4x5 tiles, muted wood and earth materials, transparent background, no surrounding terrain, no text` + 风格尾缀 | 对齐 32 网格；估占地；Pivot 底边中心 |
| 7 | `BLD_SupervisorHouse_01` | 1 | 验证控制核心辨识 | 否 | `rural xianxia supervisor residence, more authoritative than ordinary houses, dark wood and muted teal accents, courtyard gate, control-core landmark, top-down 3/4 view, footprint larger than common house, transparent background, no surrounding terrain` + 风格尾缀 | 与民宅并排对比；必须明显更大／更威严 |
| 8 | `CHR_PlayerA_Idle_Down` | 1 | 验证玩家角色比例 | **是（定 Player A 基准）** | `young impoverished village laborer, dark brown linen clothes, tied hair, carrying a woodcutting tool, full-body standing sprite facing down/front, feet visible, centered, 64x64 sprite canvas` + 风格尾缀 | 裁 64×64；Bottom Center；作为后续一致性参考图 |
| 9 | `CHR_Enemy_Beast01_Idle` | 1 | 验证敌人轮廓 | 否 | `small low-level xianxia beast, wolf-like spirit animal with ragged fur and dull red eyes, hostile but early-game tutorial enemy, compact readable silhouette, full body, feet visible, top-down 3/4 view, 64x64 sprite canvas` + 风格尾缀 | 裁 64×64；与玩家并排放置测敌我辨识 |
| 10 | `VFX_QiAttack_Single` | 1 | 验证灵力视觉语言 | 否 | `2D top-down game VFX sprite, compact cyan-white spiritual energy blast or palm wind, readable shape, transparent background, single frame or 4 evenly spaced frames, no character, no environment` + 风格尾缀 | 64/96；Additive 或普通透明；测战斗可读性 |

### 第一批验收通过后才能进入第二批

必须确认：

1. 草地／道路／灵地三者可区分
2. 民宅与主管府可区分
3. 玩家与妖兽可区分
4. 灵力特效是青白而非常态高亮污染
5. 风格统一到足以继续批量生成

若风格失败：只改 Prompt／调色／分辨率，**不要立刻扩量生成**。

---

## 第二批｜核心玩法闭环素材

**目标：** 支撑 `探索 → 修炼 → 战斗 → 击败主管 → 占领`。  
**仍不做：** 8 方向、城市、宗门、高阶技能、复杂动画。

### 2.1 生成顺序总览

| 顺序段 | 内容 | 目的 |
|---|---|---|
| A | 补齐荒村地表与关键物件 | 能探索与采集 |
| B | 补齐玩家三人＋主管＋守卫静态 | 能分工、对峙、开战 |
| C | 补齐妖兽与最小战斗特效 | 能打教学战与主管战 |
| D | 补齐灵地／山洞／修炼反馈 | 能修炼与突破 |
| E | 补齐占领与管理所需最小图标／标记 | 能夺府并感到身份翻转 |

### 2.2 详细任务表

| 顺序 | 素材名称 | 数量 | 用途 | 角色一致性 | AI 生成 Prompt | Unity 处理 |
|---:|---|---:|---|---|---|---|
| A1 | `TILE_Ground_Stone_01` | 1～2 | 工作区／石地 | 否 | `seamless stone ground tile, muted gray-brown cobble or packed stone, 32x32, tileable, top-down village, no objects` + 尾缀 | Tilemap |
| A2 | `TILE_Ground_Farmland_01` | 1～2 | 农田 | 否 | `seamless farmland tile, tilled brown soil with sparse crops hint, muted palette, 32x32, tileable, no characters` + 尾缀 | Tilemap |
| A3 | `TILE_Ground_ForestFloor_01` | 1 | 森林地表 | 否 | `seamless forest floor tile, dark muted green and brown leaf litter, 32x32, tileable` + 尾缀 | Tilemap |
| A4 | `PROP_Rock_01` | 1～2 | 阻挡／采集 | 否 | `single isolated gray rock prop, top-down, 32x32 or 64x64, transparent background` + 尾缀 | 阻挡碰撞可选 |
| A5 | `PROP_Herb_Common_01` | 1 | 普通草药 | 否 | `single ordinary medicinal herb plant, muted green leaves, clearly less magical than concealment herb, 32x32/64x64` + 尾缀 | 与敛息草对比 |
| A6 | `PROP_WoodPile_01` | 1 | 木材任务资源 | 否 | `small wood pile resource node, stacked logs, top-down prop, readable silhouette` + 尾缀 | 可交互高亮 |
| A7 | `PROP_Ore_01` | 1 | 矿石任务资源 | 否 | `small ore deposit or ore pile, dull metal flecks, top-down prop` + 尾缀 | 可交互高亮 |
| A8 | `PROP_CaveEntrance_01` | 1 | 进入隐藏灵地 | 否 | `small hillside cave entrance, dark opening, muted rock, top-down 3/4, readable but not flashy, transparent background` + 尾缀 | 交互点；可叠灵地 Tile |
| A9 | `BLD_Warehouse_01` | 1 | 仓储／交付 | 否 | `small rural warehouse shed, wood and thatch or tile, top-down 3/4, footprint about 3x4 tiles` + 尾缀 | 对齐网格 |
| A10 | `BLD_School_01` | 1 | 夺权后人才入口 | 否 | `small rural school or study hall, simple wooden building with plaque-ready front wall but no text, top-down 3/4` + 尾缀 | 夺权后高亮可用 |
| B1 | `CHR_PlayerA_Turnaround` | 1 | A 四方向参考 | **是（锁 A）** | `character turnaround reference sheet for young impoverished village laborer, dark brown linen clothes, tied hair, woodcutting tool, four directions front back left right, same exact costume and proportions, neutral standing pose, separated figures, flat neutral background` + 尾缀 | 不直接当动画；人工拆帧做 Walk |
| B2 | `CHR_PlayerB_Idle_Down` | 1 | 玩家 B 外观 | **是（锁 B）** | `young village laborer distinct from woodcutter, lighter gray-green worn clothes, short hair, carrying a basket or herb pouch, full-body standing facing down, 64x64` + 尾缀 | 与 A 并排确认可区分 |
| B3 | `CHR_PlayerB_Turnaround` | 1 | B 四方向参考 | **是（B）** | 同 turnaround 模板，描述换成 B | 拆帧 Walk |
| B4 | `CHR_PlayerC_Idle_Down` | 1 | 玩家 C 外观 | **是（锁 C）** | `young village laborer, darker patched clothes, scarf or headband, carrying a spear-like wooden stick or hunting tool, full-body standing facing down, 64x64` + 尾缀 | 与 A/B 区分 |
| B5 | `CHR_PlayerC_Turnaround` | 1 | C 四方向参考 | **是（C）** | 同 turnaround 模板，描述换成 C | 拆帧 Walk |
| B6 | `CHR_NPC_Supervisor_Idle` | 1 | 主管静态 | **是（锁主管）** | `foundation-establishment cultivator village supervisor, dark teal robe, restrained spiritual aura, authoritative silhouette taller than laborers, full-body standing facing down, 64x64` + 尾缀 | 战斗与对话共用 |
| B7 | `CHR_NPC_Supervisor_Turnaround` | 1 | 主管方向参考 | **是（主管）** | turnaround，描述用主管 | Walk／Attack 二次制作 |
| B8 | `CHR_NPC_Guard_Idle` | 1 | 守卫 | **是（守卫基础体）** | `village gate guard in simple armor or dark uniform, spear, lower status than supervisor, full-body standing facing down, 64x64` + 尾缀 | 可换色复制第 2 个守卫 |
| B9 | `CHR_NPC_Merchant_Idle` | 1 | 商人／可能功法线索 | 否（单站位即可） | `traveling village merchant, layered cloth robe, shoulder bag, cautious friendly posture, full-body standing, 64x64` + 尾缀 | 可长期站定 |
| C1 | `CHR_Enemy_Beast01_Turnaround` | 1 | 妖兽方向 | 否 | turnaround／多朝向，描述同第一批妖兽 | 可用左右翻转减工作量 |
| C2 | `VFX_MeleeSlash_01` | 1 | 普通攻击 | 否 | `2D top-down melee slash arc VFX, pale yellow-white, compact, transparent background, 4 frames` + 尾缀 | 攻击挂点播放 |
| C3 | `VFX_HitFlash_01` | 1 | 命中反馈 | 否 | `small hit spark flash VFX, white-yellow, 3-4 frames, transparent` + 尾缀 | 命中时播放 |
| C4 | `VFX_QiShield_01` | 1 | 灵力护体 | 否 | `2D top-down spiritual shield outline, cyan translucent ring or aura shell, readable, transparent, 4 frames loop` + 尾缀 | 挂在角色脚上／身上 |
| C5 | `VFX_BeastAttack_01` | 1 | 妖兽攻击 | 否 | `beast claw swipe VFX, dull red-brown, top-down, 3-4 frames, transparent` + 尾缀 | 敌人攻击 |
| D1 | `VFX_SpiritSite_Ambient` | 1 | 灵地氛围 | 否 | `floating cyan-teal qi motes ambient VFX, soft, not neon, loopable 4-8 frames, transparent, no character` + 尾缀 | 灵地循环 |
| D2 | `VFX_Cultivate_SitAura` | 1 | 修炼中 | 否 | `subtle qi aura rising around a sitting point, cyan-white, soft loop, transparent, no character body` + 尾缀 | 修炼状态 |
| D3 | `VFX_Breakthrough_Gather` | 1 | 突破聚气 | 否 | `qi energy gathering toward center, cyan-white particles converging, 6-8 frames, transparent, no character` + 尾缀 | 突破演出 |
| D4 | `VFX_ConcealBreath_01` | 1 | 敛息反馈 | 否 | `spiritual aura retracting inward and fading, not glowing brighter, cyan to gray fade, 4 frames, transparent` + 尾缀 | 使用敛息时短播 |
| E1 | `UI_SelectRing_Set` | 3 | 选中圈绿／蓝／红 | 否 | `simple top-down ground selection ring icon set, three variants green blue red, flat readable, transparent, no text` + 尾缀 | 选单位／敌我 |
| E2 | `UI_MoveMarker` / `UI_AttackMarker` | 2 | 右键反馈 | 否 | `ground move marker circle and attack crosshair marker, simple top-down UI decals, transparent` + 尾缀 | 指令反馈 |
| E3 | `ICON_Item_Wood` 等核心图标包 | 8 | 木材／矿石／草药／敛息草／功法／技能／占领／时间控制 | 否 | 逐个用图标模板：`single game UI icon of [ITEM], Chinese xianxia pixel art, centered, 64x64, transparent, no text` | 统一 64→显示 32/48 |
| E4 | `FX_BuildingDamageOverlay` | 1 | 控制核心受击／破损 | 否 | `building damage crack overlay and dust debris, dark muted, transparent, usable over house sprites` + 尾缀 | 主管府受击／耐久归零 |
| E5 | `MARKER_OccupiedFlag` | 1 | 占领反馈 | 否 | `simple occupied settlement marker or small flag decal, warm gold accent, top-down readable, transparent` + 尾缀 | 夺权成功 |

### 2.3 第二批人工补齐（不必 AI 出最终动画）

以下用第一／二批静态与 turnaround **人工制作**，不要指望 AI 一次出可用 Sheet：

| 资源 | 做法 |
|---|---|
| 玩家三人 4 方向 Walk | 由 turnaround 拆帧／插帧，每方向 4 帧 |
| 主管 Walk／Attack | 同上；Attack 可复用近战弧线特效 |
| 妖兽 Walk／Attack | 可用 2～4 朝向 + 左右翻转 |
| 角色受伤 | **闪白**，不做独立动画 |
| UI 面板框 | 色块／9-Slice 占位，不 AI 生成整套皮肤 |

### 2.4 第二批完成标准

玩家仅靠这些素材应能看懂：

```
荒村探索 → 找到灵地／资源 → 修炼／突破反馈 → 打妖兽 → 打主管／砸主管府 → 占领标记出现
```

---

## 第三批｜增强体验（核心闭环之后）

仅在第二批可玩后执行。

| 顺序 | 素材名称 | 数量 | 用途 | 一致性 | Prompt 要点 | Unity 处理 |
|---:|---|---:|---|---|---|---|
| 1 | 村民变体 `CHR_Villager_01~03` | 3 | 人口存在感 | 否 | 普通村民，劳作服装，可区分男女老少 | 群体单位复用 |
| 2 | 学校人才头像 | 2 | 候选卡片 | 否 | portrait 256 风格，一名偏修炼、一名偏治理 | UI 头像 |
| 3 | 商人行走／简易动作 | 1 套 | 增强 NPC | 可选 | turnaround 或 2 帧 | 非必须 |
| 4 | 篝火／简单环境循环 | 1～2 | 夜间氛围 | 否 | fire loop VFX | 装饰 |
| 5 | 更多树木／栅栏／货架 | 若干 | 地图丰富度 | 否 | prop 模板 | 装饰 |
| 6 | 主管技能特效增强 | 1 | 最终战压迫感 | 否 | darker red-teal qi blast | 可选 |
| 7 | 对话头像扩展 | 3～5 | 对话沉浸 | 是（对已锁角色） | 以已锁全身图为参考生成 bust | 裁切 |
| 8 | 时间表／人口等功能图标补全 | 若干 | 管理可读性 | 否 | icon 模板 | UI |

---

## 第四批及以后｜正式版替换（记录，不执行）

- 8 方向完整动画
- 每角色独立战斗动作
- 城市／宗门／大地图 tileset
- 高阶技能与法宝
- 建筑内部与建造动画
- 正式 UI 皮肤与立绘

---

## 批次执行检查表（可直接勾选）

### 开做第一批前

- [ ] 已读 `46-demo-v0.1-art-assets.md` 分类规则
- [ ] 确认本批 ≤ 10
- [ ] 准备好统一负面词与风格尾缀
- [ ] 输出目录：`Art/Source/AI_Generated/Batch01/`

### 第一批完成后

- [ ] 10 个素材齐套
- [ ] 风格验收通过
- [ ] Player A 已锁定为一致性参考
- [ ] **尚未**开始批量生成三人全套动画

### 第二批完成后

- [ ] 能铺出可读荒村
- [ ] 三人外观可区分
- [ ] 主管与民宅可识别
- [ ] 妖兽教学战特效可读
- [ ] 修炼／突破／敛息有反馈
- [ ] 占领有标记
- [ ] 才允许进入第三批

### 禁止事项

- [ ] 不在第一批生成超过 10 个
- [ ] 不跳过风格验收直接开第二批海量角色
- [ ] 不要求 AI 直接交付最终 4/8 方向逐帧 Sheet 作为唯一来源
- [ ] 不生成城市、宗门、大地图、高阶技能

---

## 建议的实际开工命令（给后续会话）

原型代码已开工，但 AI 素材仍按批次执行，不因占位图接入而一次性批量出图：

1. `执行第一批：只生成 Batch01 的 10 个素材，生成后停下来等验收`
2. `第一批通过后，执行第二批 A 段（地形与物件）`
3. `再执行第二批 B 段（角色，严格保持 A/B/C/主管一致性）`
4. `再执行战斗特效与占领图标`
5. `全部可玩后，才做第三批增强`

---

## 未决问题

- [ ] 第一批是否用“伪像素插画再人工像素化”，还是直接要求模型输出硬像素
- [ ] 初级妖兽最终定狼型还是其他兽型（本计划暂用狼型便于辨识）
- [ ] Player B／C 的随身道具最终命名（影响 Prompt 锁定）
- [ ] 主管府占地格子数最终值
