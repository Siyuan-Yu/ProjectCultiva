# 侧向与斜向手臂视角返修

> 2026-10-18｜Design Confirmed / Implemented / Producer Acceptance Pending / Temporary Seal Authorized。制作人截图指出侧向身体已转，双臂仍呈正面宽袖展开。

## 唯一实施指令

核对 HumanCommonRig 的手臂图源、肩膀 Socket、手掌挂点和前后层级。为既有 12 套 Skin 补匹配侧向视角的单臂切件，纯侧向与四斜向复用同套骨骼步态、工具与武器挂点；正背保持既有手臂。侧向的远臂应被躯干正确遮挡，近臂沿身体前后摆动，不再形成正面张开的双袖。资源缺失须安全回退而不令整架人物消失。

本轮只改 Host 表现及版本化美术资源，不改移动权威、Core／Data／Content、场景、ProjectSettings、Packages、Freeze。只作基础编译及最少必要静态资源核对，不启动 Unity、不写或运行自动测试；动作和美术观感由制作人在 LevelTester 人工验收，不自动提交／推送。

## 核查与实施

`HumanCommonRigView.Apply` 的躯干/头已按八向更换，但左右手臂在所有方向仍固定取原正面 `parts[2/3]`；侧向仅调整角度、层序。截图中正面宽袖随侧身展开，根因是手臂图源，而非寻路或步态驱动。

为 12 套 Skin 各补一张肩点到手掌连续的侧面衣袖切件，生成原图和 SHA 存于 [Batch_ModularRig_005](../../ArtSource/Batches/Batch_ModularRig_005/manifest.json)。导出器只读 alpha ≥ 16 的可见区域确定切片及肩/掌位置，原 PNG 不改像素；运行资源及布局放在 `Resources/SideArms`。纯侧与四斜向使用新切件，斜左/纯左由整架镜像；仍沿用当前肩骨节、走路摆臂、工作/攻击和工具挂点，握点按新切件校准。正背向及卧姿保留原手臂；资源缺失时回退原手臂并告警，整个人物不会被撤掉。

离线基础编译 `XianXia.Unity : OK`；12/12 切件、布局、源图 SHA 与握点约束的最少静态核对完成。未启动 Unity、未写/跑自动测试。侧行和斜行的袖型、远近遮挡、工具贴手与各 Skin 观感仍待制作人人工验收；此状态不等于 Producer Accepted。
