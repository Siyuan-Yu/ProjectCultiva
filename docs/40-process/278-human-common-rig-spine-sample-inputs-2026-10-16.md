# HumanCommonRig Unity 轻量部件骨骼样机

> 此页保留首批样机记录；2026-10-18 当前动作语义、部件范围/关节、可换工具/武器与步态修复以 [280](280-animation-binding-audit-and-fix-2026-10-18.md) 为准。

> 2026-10-17 首批三 Skin 样机记录：三套简化 Skin 部件与 Transform/SpriteRenderer 共用层级已接线，**离线编译完成，Unity/LevelTester 人工视觉和性能未验收**。本轮扩至名录人物的实施见 [279](279-human-common-rig-roster-migration-2026-10-17.md)。文件名保留历史 `spine` 字样仅为兼容既有链接；Spine 不再是必需工具。技术路线以 [276](276-art-tech-route-spine-frame-static-v1-2026-10-16.md) 和 [273 TSV](273-current-art-asset-register.tsv) 为准。

## 素材与身份

| 身份 | AssetID | 新部件源 | 运行资源 | 旧图用途 |
| --- | --- | --- | --- | --- |
| 普通村民 | ART-CHAR-001 | `ArtSource/Batches/Batch_ModularRig_001/Originals/ART-CHAR-001_HumanCommonRig_parts_v01.png` | `Assets/Art/Characters/Resources/` 同名 atlas | 高精村民、v02 逐帧仅设定/回退 |
| 一名主角 | ART-CHAR-002 | `ArtSource/Batches/Batch_ModularRig_001/Originals/ART-CHAR-002_HumanCommonRig_parts_v01.png` | 同名 atlas | 三人组合设定仅取主角一名；另两人未制作新 Skin |
| 巡卫 | ART-CHAR-004 | `ArtSource/Batches/Batch_ModularRig_001/Originals/ART-CHAR-004_HumanCommonRig_parts_v01.png` | 同名 atlas | 旧长枪逐帧继续回退 |

另有三身份侧头像 atlas `ART-CHAR-001_002_004_HumanCommonRig_sideheads_v01.png`，以及独立长枪/斧/锄/镐/采集镰/背篓 atlas `ART-ANIM-002_004_HumanCommonRig_tools_v01.png`。两图均保留 ArtSource 原图及同名 Unity Resources 副本。所有新图由 Codex 内置 image_gen 分别生成，是真透明 PNG；旧高精 PNG 只作视觉参考。具体路径、文件哈希和映射见该批 manifest 与最新文件级清单。

## 共用层级与动作

`EntityView` 是唯一角色根和原碰撞/位置/选中圈/标签载体。`HumanCommonRig` 是其纯视觉子层：FootAnchor 下为 Torso、Head、左右 Arm/Leg、BackAccessory；右臂有 ToolSlot/WeaponSlot。三 Skin 共用相同骨架参数、关节位置、方向和动作相位，不使用 Mesh Skinning 或第三方插件。正面、背面、侧面分别有躯干与头素材，西向镜像东向；不对称差分、手握姿势仍待人工核对。

`HostCharacterArtState` 从真实有效位移推进 Walk，相同相位喂给旧逐帧和新部件；Idle/Walk/Work/Attack 参数共用。巡卫长枪独立附件，攻击仅跟随已有 `PlayArtAttack` 事件。SleepBed/SleepGround 复用同一 Skin 部件静态侧卧于真实床或地面，不能移动 EntityView 根、占床点或碰撞。Downed/Dead 停止正常动作并使用原生命状态颜色；未制作独立倒地/尸体剪影。Carry 样机本轮未完成，保持旧表现。

## LevelTester 操作与待验

在 Play Mode 选中已有 `EntityViewSpawner`，把 `Prototype Visual Driver` 从 `LegacySprite` 改为 `ModularRig`，或在组件右键菜单使用 `Toggle HumanCommonRig prototype (Play Mode)`。再次切回 LegacySprite 比较；资源缺失自动回退。此开关作用于现有三身份 Profile 的 View，不增加 Core Entity、角色碰撞或 NPC。

请在常规游戏镜头正交尺寸 12 观察：人物高度/标签与脚点、手臂和腿关节遮挡、四方向及镜像、长枪握柄、工作工具识别、床上/地上侧卧、移动速度与步态、暂停/恢复、同屏多人。现有图属于首版运行样机，头身比例、接缝遮挡和横向剪影可能仍需返工。需记录同屏人数、帧率/帧耗时、GC、SpriteRenderer 数与可测的 Draw Call；**当前没有 Unity PlayMode、正式 Build 或人工视觉/性能结果**，不标为 Producer Accepted。

## 仍缺的制作与迁移条件

- 正常缩放下三 Skin 的人工画风/辨识度确认；关节 Pivot、遮挡余量、侧向不对称装备和床面位置精修。
- 更细的工具握持、长枪前刺关键姿态及 Work/Attack 幅度；当前是最小共用动作参数，不代表完整美术动画库。
- 主角同伴、主管、其他居民等其余 Skin；Carry、独立 Downed/Dead 视觉；正式 Build 资源加载与多人性能数据。
- 制作人验收后才决定是否扩批及是否退役任何旧逐帧运行资源。
