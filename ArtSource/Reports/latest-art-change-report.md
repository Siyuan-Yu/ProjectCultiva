# 本次美术文件变更报告

> 生成时间：2026-10-23T15:15:14.371200+00:00（UTC）。273 TSV 是资产族进度真源；本报告只做文件级盘点。

相对上一快照：新增 0、修改 3、删除 0。仅哈希相同的路径搬移可进一步判为迁移。

## 当前汇总

- 扫描范围：ArtSource（不含自动生成的 Reports）及 Assets/Art；文件 895，373844323 字节。
- 原图 127；Frames 目录图像 157；Spine 工程/导出文件 0。
- 273 全部56个资产族：登记已接入待验 30，其余 26；其中本次文件可关联 40 个AssetID（已接入待验 30、其余 10）。这些数不是文件级人工验收。
- 同 SHA-256 图片 117 组，额外副本 117 个；理论重复占用 104359147 字节。重复不等于可删除。
- 当前可直接删除的图像候选：0；manifest、原图来源、Unity引用及加工脚本均可能依赖旧路径。
- HumanCommonRig：001/002 两批基础部件共12套Skin、25身份；003闭眼、004斜向头身、005侧臂见各批manifest及docs/40-process/297-art-rig-and-source-cleanup-current-handoff-2026-10-18.md。Unity人工验收待完成，Spine不是依赖。

## 本次差异

- 新增：无
- 修改：Assets/Art/UI/Resources/ART-UI-003_grain_pouch_v02.png.meta、Assets/Art/UI/Resources/ART-UI-003_rough_stone_v02.png.meta、Assets/Art/UI/Resources/ART-UI-003_spirit_herb_bundle_v02.png.meta
- 删除：无
- 迁移/接入：静态代码引用仅证明已接线，Unity/LevelTester 人工验收仍待完成。
- 待返工：以273与各批 manifest 的候选问题为准；文件清单本身不代表视觉验收。
- 受影响 AssetID：ART-UI-003

## HumanCommonRig 文件映射

001/002基础透明部件/附件的运行副本与原图 SHA-256 相同；003～005 另补闭眼、斜向头身和侧臂。旧 Unity 逐帧资源仅作回退。以下仅证明接线，Unity 视觉/Build/性能待制作人验收。

- ART-CHAR-001 / Villager：`ArtSource/Batches/Batch_ModularRig_001/Originals/ART-CHAR-001_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-001_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `cf21f18048f11b9ceca9de571a5b3a9306ddad3811220452aca28e5fdf999d9c`。
- ART-CHAR-002 / Protagonist：`ArtSource/Batches/Batch_ModularRig_001/Originals/ART-CHAR-002_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-002_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `fafe5a2990e104c670150b5953b0a01638d5ba6167ecfdf7a3f5180bd41ba3e5`。
- ART-CHAR-004 / Guard：`ArtSource/Batches/Batch_ModularRig_001/Originals/ART-CHAR-004_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-004_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `80179df0b1d226f16bdd64197b7e97b5c2b05a84199a2931e6856db1d88ebab2`。
- ART-ANIM-002,ART-ANIM-004 / SharedEquipment：`ArtSource/Batches/Batch_ModularRig_001/Originals/ART-ANIM-002_004_HumanCommonRig_tools_v01.png` → `Assets/Art/Characters/Resources/ART-ANIM-002_004_HumanCommonRig_tools_v01.png`；3x2: spear/axe/hoe; pick/sickle/back basket；SHA-256 `1e56f4645c7c408d9519ef5a2654333637d749aadcf3b24e10aa120316bc9361`。
- ART-CHAR-001,ART-CHAR-002,ART-CHAR-004 / SharedSideHeads：`ArtSource/Batches/Batch_ModularRig_001/Originals/ART-CHAR-001_002_004_HumanCommonRig_sideheads_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-001_002_004_HumanCommonRig_sideheads_v01.png`；1x3: villager/protagonist/guard facing east; west mirrored；SHA-256 `d686dda0d0c2b6db4792d24d1b0ef9e258b760f45129c1f05804dc413d03abfe`。
- ART-CHAR-002,ART-CHAR-003,ART-CHAR-005 / CompanionA/CompanionB/Supervisor/FarmerHoe：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-002_003_005_HumanCommonRig_sideheads_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-002_003_005_HumanCommonRig_sideheads_v01.png`；east-facing detached heads；SHA-256 `03b5dadabc9bdb30b9dc112d99e9799e7e5e6f58bfe63d1b0bc22afbe60040b1`。
- ART-CHAR-002 / companion_a：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-002_companion_a_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-002_companion_a_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `a1779009ca8ce1e5f2211898aa1fa30a90d50d6fe4a76f66e2b4edb176f9878d`。
- ART-CHAR-002 / companion_b：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-002_companion_b_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-002_companion_b_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `d05c19705a161e16cadf4680c89ad6f12ab791d7e88955660142b89734fbdd7d`。
- ART-CHAR-003 / supervisor：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-003_supervisor_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-003_supervisor_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `e62ecd12e9e9815b1abea25acc8fd6dbe295a0f01f7aa59f11aa420734d6d3cf`。
- ART-CHAR-005,ART-CHAR-006,ART-CHAR-007 / FarmerApron/ResidentBasket/Merchant/Other：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-005_006_007_HumanCommonRig_sideheads_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-005_006_007_HumanCommonRig_sideheads_v01.png`；east-facing detached heads；SHA-256 `02a22751ab1b2e897d56a5bd228ae99b5474fb8357ae0088810f9a0cc31fad88`。
- ART-CHAR-005 / apron：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-005_apron_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-005_apron_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `cb06df3420714b9c0a665f96dc76c9fc371e5347e687817004a4bea48e0e833e`。
- ART-CHAR-005 / basket：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-005_basket_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-005_basket_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `d58a92b3895a48f663d3410a71eb1bb0610a694808d3b655577ec3fd1ba159d4`。
- ART-CHAR-005 / hoe：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-005_hoe_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-005_hoe_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `4a24a0351e3967e1a3b436f54750edef4f361f3089d4acac45c0dd576ab7c34a`。
- ART-CHAR-006 / merchant：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-006_merchant_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-006_merchant_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `f2aa6d5c6821099fe8a52b37bec2eb648fc2dc6a3629411a9951addeb786d0a6`。
- ART-CHAR-007 / JiangLao：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-007_jiang_lao_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-007_jiang_lao_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `38ddb51e5ba0e507cb97537a9bfce188149c3193d44d77b68dac8e7a1c02aa08`。
- ART-CHAR-007 / JiangLao：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-007_jiang_lao_HumanCommonRig_sidehead_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-007_jiang_lao_HumanCommonRig_sidehead_v01.png`；east-facing detached heads；SHA-256 `e67cd16d2ca4bfa25cfdc7323d740eff716ff072767ea1bea0150934bda81c01`。
- ART-CHAR-007 / other：`ArtSource/Batches/Batch_ModularRig_002/Originals/ART-CHAR-007_other_HumanCommonRig_parts_v01.png` → `Assets/Art/Characters/Resources/ART-CHAR-007_other_HumanCommonRig_parts_v01.png`；3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON；SHA-256 `71529c43c928e5a984162ebcdb75875f2bafc81ab633b5ee3f7bf2677c639f87`。
- 正背保留基础手臂；纯侧向与四斜向切换对应侧臂，左视角整体镜像；八向头身与闭眼差分分别见004/003批次。
- 静态代码引用：`HumanCommonRigView.Resources` 在默认 ModularRig 驱动下读取运行图；Unity 实际加载与缩放仍待人工确认。
