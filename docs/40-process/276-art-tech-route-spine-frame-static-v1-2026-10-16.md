# 美术技术路线：Unity轻量部件骨骼、逐帧特效与静态 Sprite v2

> 2026-10-18 当前技术决策。文件名中的 spine-v1 为历史路径，链接保持稳定；本文内容已更新为 Unity Transform + SpriteRenderer 路线。唯一资产族真源为 [273 TSV](273-current-art-asset-register.tsv)，首批三 Skin 样机见 [278](278-human-common-rig-spine-sample-inputs-2026-10-16.md)，名录接线见 [279](279-human-common-rig-roster-migration-2026-10-17.md)，动画语义、步态和附件最新复核见 [280](280-animation-binding-audit-and-fix-2026-10-18.md)。Spine 编辑器、导出与 runtime 不是项目必需依赖。

> 2026-10-18 后续实施：[282 人物移动与工具动作标杆](282-character-locomotion-and-tool-action-polish-2026-10-18.md) 已完成代码实施，待人工验收。按制作人确认的俯视经营观感加强步态、有效移动接收、主副手握持、劳动接触与旋转床；不批量生图，不改位置或玩法真源。
> 最新定位与修正：[284 实际驱动](284-character-actual-driver-and-unified-work-binding-2026-10-18.md) 已由制作人截图确认进入骨架；[285 关节标定与姿态](285-human-rig-joint-registration-and-pose-repair-2026-10-18.md) 处理真实肩／颈、逐素材朝向和重复袖轮廓，视觉仍待人工验收。源码默认值不再作为现场驱动已正确的证据。
> 2026-10-18 眼睛差分：[292](292-character-closed-eye-head-variants-2026-10-18.md) 在 12 套 Skin 增加正面／侧面共 24 张闭眼静态头；原睁眼头像继续用于清醒状态，睡眠／弥留／死亡只切头部表现，床层级沿用 290。资源与接线已实施，Unity 现场视觉待制作人验收。

## 1. 正式路线与边界

| 对象 | 当前目标 | 状态 |
| --- | --- | --- |
| 正俯视小尺寸实时人物 | Unity Transform + SpriteRenderer 共用 HumanCommonRig、可换部件 Skin | 25 个名录身份已映射到共用骨架的 12 套部件 Skin，默认新驱动；视觉、Build 与性能待制作人验收 |
| 场景、建筑、家具 | 静态 Sprite、模块和图集 | 路线不变 |
| 田地、药草、树和矿 | 真实状态驱动的 Sprite 切换 | 路线不变 |
| 挥砍、弹体、灵气 | 短逐帧或程序效果 | 路线不变 |
| 受击、选中圈、简单光效 | 程序 / Shader | 路线不变 |
| 角色设定、头像、剧情插画 | 高精度静态参考 | 与实时小尺寸角色分工明确 |

画风保持国风修仙村落水墨淡彩。实时人物优先清晰轮廓、服装主色、发型和武器工具，减少五官、衣纹、飘带与布料模拟。不得复制其他游戏素材。高精旧人物 PNG 是 Character Visual Reference，不是合格新部件。只修改表现，不更动 EntityId、Core 世界位置、碰撞、移动、战斗伤害、工作产出、睡眠与死亡真源。

## 2. HumanCommonRig 样机结构

同一 `EntityView` 下新增唯一的 `HumanCommonRig` 视觉子层；`FootAnchor`、`Torso`、`Head`、`LeftArm`、`RightArm`、`LeftLeg`、`RightLeg`、`ToolSlot`、`WeaponSlot` 是共用 Transform 层级。背篓为可选 BackAccessory。关节和衣袖/裤腿部件在接缝处保留遮挡余量，不按立绘精度拆十几层。南面用正面躯干/头，北面用独立背面躯干/后脑，东西用侧面躯干/头像及镜像；不以整人旋转充作背面。方向、部件排序和脚点在正常镜头缩放下仍待人工验收。

首批普通村民、主角、巡卫三 Skin 保留；第二批新增两名同伴、主管、三类居民、商店执事、通用劳作者和江老九套部件 Skin。25 个现有名录身份按既有 DefinitionId 映射，不新增角色。商店/拍卖、其他散人/劳作者仍共用类别 Skin，个人差分待视觉验收后补。四种劳动工具、长枪和背篓是独立附件。部件图和离线编译不能等同于全员视觉验收。

## 3. 共用动作与步态依据

- `Idle` 呼吸，`Walk` 交替步态；`Work` 按真实执行阶段区分播种/照料/收割/清理/伐木/采矿/施工/修炼/进食。WorkAction 仅 Labor 视为劳动，巡逻/检查/休息不劳动。受管凡人只读其当前活动和已抵达的执行阶段，不借残留日程动作。播种/照料默认空手，工具与手势分离；未知劳动不默认拿镰刀。
- `HumanCommonRigEquipment` 管理可替换工作附件和独立战斗武器；动作不查整身逐帧图换工具。巡卫默认长枪来自该附件绑定，其他默认空手；视觉替换不创建正式装备/背包/属性 authority。实际成功出手驱动空手/挥击/前刺/远程施放；工作斧不自动成为战斗武器。床/地睡姿、弥留/尸体/被搬者使用本人的侧向静态部件。
- LegacySprite 与 ModularRig 共用 `HostCharacterArtState`。Walk 相位由移动执行者记录的有效位移累积，每约 3.2 世界表现单位一轮，最高 2.8 轮/真实秒；停走不清零相位，骨架关节和步态权重平滑过渡，攻击上身不冻结有效行走相位。有效位移样本一次消费，仅已有 Host 路径与合理实际位移可恢复漏报。阻挡、暂停、无路径分离、传送和搬运位置同步不推动步态。高倍速步频有上限，不能将此描述为完全消除滑步。动作不产生 Root Motion，也不改变世界速度。
- PNG 不变；`tools/export-human-rig-layouts.py` 从原图 alpha 连通部件导出裁框，再应用 `ArtSource/BatchInfo/human-common-rig-calibration.json` 的逐素材肩／掌心／颈根及躯干Socket标定。自动裁框不等于自动识别人体关节。原图方向按每份侧躯干的mirrorX标准化，头肩同步连接；重复固定袖由Sprite可见网格剔除，保留衣襟与下摆。中性臂由肩到掌心向量计算，工具可达约束不移动肩。UpperBodyBone 带动头臂，SortingGroup 保持整人与物件的脚点排序；所有视觉仍需人工验收。
- Core 决定工作、攻击、睡眠和生死。动画时钟在暂停时冻结；命中与产出不能由 SpriteRenderer 或 Transform 动画触发。

## 4. 现有 EntityView 接线与回退

```text
EntityView / Core 只读状态（同一个 EntityId、位置、Facing、行为、生命与床占用）
  → HostCharacterArtState（共用动作及位移步态相位）
  → LegacySprite 或 HumanCommonRigView（ModularRig）
```

`EntityViewSpawner` 正式启动与重建固定进入 ModularRig，不再读取可持久化的旧驱动字段。LegacySprite 只保留显式ContextMenu临时预览；资源失败有具体诊断与每2真实秒的绑定恢复，不整局静默停在旧静图。显式身份Skin保留，其他Character／Npc使用Other共用动作。两种驱动共用一个EntityView Root/Collider、选中圈、标签、镜头和排序。旧运行图集、Batch_Codex_002/003帧及人物原图保留。性能结论和旧资产退役须在制作人人工验收后另行决定。

## 5. 人工验收与性能记录

唯一人工主场景为 `Assets/Scenes/LevelTester.unity`，不调整其地图布局，不为样机增设正式 NPC。需要在常规镜头正交尺寸 12（可缩放 6–44）下观察名录人物与 12 Skin、四方向、低速/常速和 2x/5x、停止/阻挡/暂停恢复、Work、Carry、长枪出手、床/地睡姿和生命终局姿态。记录同屏人数、FPS或帧耗时、GC、SpriteRenderer 数和可得的 Draw Call。当前只完成离线编译与静态引用检查，尚无 Unity 人工视觉或性能数值。

## 6. 非人物资产原则

以下静态/状态/短特效规划延续 v1，不因人物技术路线变化而改玩法资产分类。

## 7. 静态、状态切图、短逐帧与程序效果边界

| 类型 | 制作与运行边界 |
| --- | --- |
| 静态 Sprite | 房间地板/墙角/门洞、家具、地面、UI模块；交付 PNG/Sprite/模块或图集、Pivot/拼接说明 |
| 状态切图 | 固定田地土壤 + 独立植物层；床按 RoomUsage，树/矿按真实资源状态换 Sprite；交付各状态图与数据映射表 |
| 短逐帧特效 | ART-FX-002 挥砍/弹体、ART-FX-005 灵气/光晕/法阵；交付短 PNG 序列/图集及时序配置，由程序掌控触发、轨迹、暂停、结束/回收 |
| 程序 / Shader | 闪白、选中圈、简单工作/交接反馈、轻光效、水面和轻旗动效；交付材质/Shader/粒子或表现参数，优先复用既有效果 |
| 高精静态 / 参考 | 头像、人物设定、立绘和插画；参考 PNG 与设计说明不计作实时角色动画交付 |

农田、药田生长阶段不是逐帧动画。Growing/Mature/Empty/Ruined 等仅按实际状态映射，不按固定帧率循环、不加速生长，采收后切回真实状态；未合格植物图层保留旧表现。床用途同理，换图不改变房间/床 ID 或用途规则。所有视觉缩放、裁切与特效包围盒不参与玩法占地、交互点、建造重叠或碰撞判定。

## 8. 全部 55 个 AssetID 技术路线索引

本表是 273 新字段的只读索引，不成为第二份进度真源。原 ID 集合、顺序与优先级不变；逐行制作/复用、目标交付与迁移规则均以 273 为准。

| AssetID | 推荐技术路线 |
| --- | --- |
| ART-CHAR-001 | Unity轻量部件骨骼/基础Skin |
| ART-CHAR-002 | Unity轻量部件骨骼/主角Skin |
| ART-CHAR-003 | Unity轻量部件骨骼/主管Skin |
| ART-CHAR-004 | Unity轻量部件骨骼/巡卫Skin/武器附件 |
| ART-CHAR-005 | Unity轻量部件骨骼/居民服装Skin |
| ART-CHAR-006 | Unity轻量部件骨骼/商店拍卖执事Skin |
| ART-CHAR-007 | Unity轻量部件骨骼/其他人物Skin |
| ART-ANIM-001 | Unity共用Idle/Walk位移步态 |
| ART-ANIM-002 | Unity共用Work/工具附件 |
| ART-ANIM-003 | Unity共用Carry/挂点 |
| ART-ANIM-004 | Unity共用Attack/长枪前刺 |
| ART-ANIM-005 | Unity静态Sleep/Downed/Dead姿态 |
| ART-ROOM-001 | 静态Sprite |
| ART-ROOM-002 | 静态Sprite |
| ART-ROOM-003 | 静态Sprite |
| ART-ROOM-004 | 静态Sprite/等级材质差分 |
| ART-ROOM-005 | 静态Sprite/用途状态切图 |
| ART-ROOM-006 | 静态Sprite/床用途变体 |
| ART-ROOM-007 | 静态Sprite |
| ART-ROOM-008 | 静态Sprite/程序旗帜轻动效 |
| ART-ROOM-009 | 静态Sprite/程序灵气效果 |
| ART-ROOM-010 | 静态Sprite |
| ART-GROUND-001 | 静态Sprite |
| ART-GROUND-002 | 静态Sprite/道路拼接模块 |
| ART-GROUND-003 | 静态Sprite |
| ART-GROUND-004 | 静态Sprite |
| ART-GROUND-005 | 静态Sprite/农田基础层 |
| ART-GROUND-006 | 静态Sprite/药田基础层 |
| ART-GROUND-007 | 静态Sprite/树木状态切图 |
| ART-GROUND-008 | 程序反馈/状态切图 |
| ART-GROUND-009 | 静态Sprite/矿物状态切图 |
| ART-GROUND-010 | 静态Sprite/农作物生长状态层 |
| ART-GROUND-011 | 静态Sprite/药草生长状态层 |
| ART-GROUND-012 | 静态Sprite/程序灵气效果 |
| ART-GROUND-013 | 静态Sprite/Shader水面效果 |
| ART-GROUND-014 | 参考图/远景季节扩展暂缓 |
| ART-UI-001 | 静态Sprite/九宫格 |
| ART-UI-002 | 静态Sprite/状态图标 |
| ART-UI-003 | 静态Sprite/物品图标 |
| ART-UI-004 | 静态Sprite/灵石图标 |
| ART-UI-005 | 静态Sprite/入口标识 |
| ART-UI-006 | 程序效果 |
| ART-UI-007 | 静态Sprite/程序轻动效 |
| ART-UI-008 | 高精静态人物头像 |
| ART-UI-009 | 静态Sprite/技能功法图标 |
| ART-UI-010 | 静态Sprite/暂缓 |
| ART-FX-001 | 程序/Shader |
| ART-FX-002 | 逐帧短特效+程序 |
| ART-FX-003 | 程序反馈 |
| ART-FX-004 | 程序反馈 |
| ART-FX-005 | 逐帧短特效+程序 |
| ART-FX-006 | 高阶特效参考/暂缓 |
| ART-REF-001 | 整体场景风格参考 |
| ART-REF-002 | 高精人物设定/立绘参考 |
| ART-REF-003 | 高精剧情插画/立绘 |

## 9. 旧素材与阶段状态

当前 LegacySprite 图集及 Assets/Art `.meta` 保留并能切换回退；旧高精人物原图作为设定参考，旧逐帧作为 Legacy Runtime / Migration Reference。两批共 12 Skin 已生成并按 25 名录身份接线待验；类别共享 Skin 不能记为每人独特外观完成。273 的原 AssetID、优先级、制作和验收字段保留，新增文件路径按 AssetID 补记。

## 10. 后续门槛

制作人先在 LevelTester 比较新旧正常缩放可读性、关节遮挡、步态距离、四方向、睡姿和性能；正式 Build 加载仍待核。不预设部件骨骼比逐帧更快。未通过前不批量迁移、不删除运行资源、不把全身旧 PNG 标作合格 Skin。若样机缺部件或画风/性能不合格，继续 LegacySprite 回退并逐项修复。

2026-10-18 八向增补见 [293](293-character-eight-direction-movement-2026-10-18.md)：12 Skin 已增加斜前/斜后右向头身并镜像得到四个斜向，共用原四肢和距离步频，资源与代码为 Implemented；原上文的“四方向/四方向验收”是制定时的历史基线，当前应按八向人工验收。旧四行逐帧仅留回退，Unity 视觉、Build 与性能仍待制作人确认。
