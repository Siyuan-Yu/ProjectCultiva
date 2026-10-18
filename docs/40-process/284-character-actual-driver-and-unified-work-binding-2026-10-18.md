# 人物实际动画驱动与统一工作接线修复

> 状态：Implementation Complete / Producer Acceptance Pending | 优先级：P0 | 最后更新：2026-10-18
> 前置：[283 四向步态](283-character-directional-gait-and-nonblocking-overlap-2026-10-18.md)、[282 人物移动与工具动作](282-character-locomotion-and-tool-action-polish-2026-10-18.md)。
> 后续制作人截图确认已进入骨架表现；脱臂、头颈和站姿缺陷转入同一授权范围的增量修复 [285](285-human-rig-joint-registration-and-pose-repair-2026-10-18.md)。这不表示全部人物姿态已验收。

## 本轮唯一实施范围

制作人反馈上一轮未解决主角移动动画，且只有阿土有工作动画，要求查清实际原因后统一修复。获授权范围是实际人物驱动选择、资源／绑定恢复、真实工作阶段到共用动作的接线，不继续凭朝向调步频，不扩张玩法。

先核对主角输入到有效位移、场景与 Play 前场景备份、实际编译产物、现有日志、两套表现路径和资源。正式人物统一使用现有共用部件骨架；已指定身份 Skin 保留，未指定专属 Skin 的 Character／Npc 使用现有 Other 共用 Skin，避免只有名录白名单能动。旧逐帧如保留诊断入口，须明确为临时调试，不让旧序列化值或一次加载失败静默锁定普通人物动画。资源失败报告具体原因，并在有界间隔恢复，不每帧重建状态。

工作动作从真实执行器／Running Action／已抵达的民生活动读取，补齐合法工作类型，不按姓名或职业强迫所有人物一直挥动。前往工作、等待、休息、睡眠、巡逻、搬运继续对应现有行为；没有真实医疗执行的医者不捏造治疗。同步修正已到达仍被标成移动的表现读数。

保持人物实际移动、统一位移步态相位、Core 时间、产出与伤害、存档及上一轮取消斥力的规则。不批量生图；不改 Core／Data／Content、Packages、ProjectSettings 或 Freeze，不启动 Unity，不编写或运行自动测试，只做基础编译和必要静态／资源核对。完成后更新本页与 devlog，交付短人工验收，不自动提交／推送。

## 已有证据与待核实项

- 右键与 WASD 都提交实际位移；四向共用同一 WalkPhase。未发现西向独立少计步或慢时钟，继续修改步频不能解释当前差异。
- 实际旧主角 `ART-CHAR-002_protagonist_walk_v03.png` 的北向换腿明显、西向四帧近似同姿，与制作人反馈吻合；共用骨架东西轨迹相同且镜像。
- 旧 `HostStepAArt.CharacterFrame` 只有 Villager／Guard 具备相应动态资源，多数其他 NPC 的 Work 回到静图；阿土为 Villager。正式骨架则支持全部现有映射身份。这能解释两个现象的共同表现路径，仍须核对实际驱动值，不能直接宣称现场已复现。
- 已有 Editor.log 记录阿木、阿柴、阿枝进入“砍伐中”、树木生命下降并产出，说明并非只有阿土执行过真实工作；日志没有逐角色 Driver 记录。
- 已编译 Unity DLL 含上一轮 `PoseWalkingLeg`／`PoseNorthLeg`，时间晚于源码；不能归因于上一轮代码完全未刷新。
- 确定接线缺口：一次 Rig 失败被 `_rigUnavailable` 长期视为有效绑定；WorkAction(Cultivate) 被工作解析丢弃；managed civilian 默认分支可能提前吞掉真实 Running Action；移动标签未排除 HostArrived。

### 实际驱动已确认

只读解析 Unity 已有 `Temp/__Backupscenes/0.backup`（2026-10-18 15:53:13），唯一 `LevelTester` 对象（GameObject path ID 1052075869）的 `EntityViewSpawner`（path ID 1052075885）实际保存 `prototypeVisualDriver = 0`，当前枚举 0 为 LegacySprite、1 为 ModularRig。脚本 GUID `d23ab1c8f4ab445a83b919b357c5ff5b` 精确对应当前脚本 meta。源备份 SHA-256：`d637bb9f1e2371623acebdde0cd670b19b52b0f5d4452aca0bbae9c5474d41e8`；小型解析记录在 `Temp/character-animation-diagnosis/preplay-spawner-driver.json`。

这是旧逐帧实际被选中的直接证据，不能再以“磁盘场景未写字段、源码默认 ModularRig”推断实际驱动。旧图集与旧路径静图分支共同解释了制作人的两项反馈；上一轮骨架曲线修改没有作用在该路径。修复重点转为去掉普通流程的持久旧驱动选择，保留显式临时预览，不再修改步态曲线。

解析只读取已有场景文件，没有启动 Unity 或运行模拟／测试。实际驱动证据也不替代修复后的现场视觉验收。

## 已实施

### 正式驱动与资源恢复

- `EntityViewSpawner` 删除可持久化的旧 `prototypeVisualDriver` 字段。正式驱动由非序列化的临时预览标记派生，默认 ModularRig；重建 session 时再次清除预览。已有场景或 Editor 内存保留的旧值 0 不再有恢复入口，无须制作人逐个改 Inspector。
- 保留右键 ContextMenu 中明确标注 Debug only 的旧帧临时预览，切换时记警告，不能保存成下次运行的默认。没有更改枚举数值或玩法存档。
- 创建、增量加载与已有视图恢复共用 `EnsureArt`。`EntityView` 把已配置与实际成功绑定分开；资源失败不再被当作 Rig 已就绪。按 2 个真实秒的间隔只重试视觉绑定，不重复调用整套 Configure，不重置步态／工作时钟；移除人物仍由原生命周期门禁隐藏。
- `HumanCommonRigView.InitializationFailureReason` 提供具体纹理／布局资源 key、JSON／parts 数量／矩形／关节点异常，失败布局不缓存。重复相同错误去重，恢复后清空失败读数。
- Editor／Development 首绑及驱动改变自动记录 `[CharacterAnimationBinding]`，包含 EntityId、姓名、实际 Driver、Profile、失败原因。正常应为 `Driver=HumanCommonRig`；失败时明确标为 recovery pending 并报告具体资源，不静默把静图当修好。
- `HostStepAArt` 保留所有显式身份 Skin；其他带 Character 或 Npc tag 的人物使用现有 Other Skin 共用动作，不再因为未列入专属美术映射而完全没有动画。

### 统一真实工作动作

- 阿土不是故意的动画特例。阿石／阿土／阿禾、阿青／阿兰／阿杏、阿木／阿柴／阿枝均按 EntityId 使用共用动作；旧 LegacySprite 中多数身份的静图回退是此前差异来源。
- 农田 Work phase、树木接触距离内的伐木执行器仍最高优先；接近／等待阶段不继承旧日程劳动。真实 Running Action 在民生活动 fallback 前统一解析，防止默认 Activity 提前吞掉实际劳动。
- `WorkAction(Labor)`／`LaborAction` 按真实工区／地点 tags 选择劳作；`WorkAction(Cultivate)`、`CultivateAction`／`RecoveryAction` 共用冥想；`WorkAction(Eat)` 与真实 Eating 阶段共用进食。施工沿用已到达且材料送达的既有条件。
- `HostMortalActivityPresentation` 的移动意图判断增加 `!HostArrived`，到达后不再永久标为“前往”。没有把职业名称直接当作工作执行信号。

没有改动步态曲线、位移采样或实际移动速度。巡逻／巡视是行走或站岗，休息／待料／无合法树或无农活不假装持续干活；搬运、睡眠、失能保持原姿态。医者当前没有真实治疗执行，本轮没有新增医疗玩法或伪造产出。

## 验证与限制

`pwsh -NoProfile -File tools/offline-compile.ps1 -Only XianXia.Unity`：152 个 Unity 源文件基础离线编译通过，`ALL_OK`。完成直接调用、旧字段恢复入口、配置／恢复分工与资源引用的最小静态复核；只读解析的场景备份是定位证据，不是自动行为测试。

没有启动 Unity，未编写或运行自动测试，未进行修复后现场视觉／Player Build／性能验收。未改图片、素材布局、GUID、场景布局、Core／Data／Content、Packages、ProjectSettings 或 Freeze，未暂存／提交／推送。人物共用部件仍有宽袖重复轮廓等素材局限，它们与本轮已确认的旧驱动误选分开记录，不再用素材推测替代实际驱动证据。

## 简短人工验收

1. 退出当前 Play 后重新进入，主角右键朝四向各走一段并停下：应看到骨架交替步态；选中主角 EntityView 的 Runtime animation diagnostics 应为 `HumanCommonRig`，不再是 LegacySprite。
2. 工作时段观察阿石／阿土／阿禾、药农和三名樵夫：真实到位工作时均有对应动作，樵夫持斧，农作跟当前耕／播／护／收阶段；前往、等待和休息时不原地乱挥。
3. 暂停再继续，并让一名 NPC 完成工作转去取食或休息：动作随阶段切换，不冻结在旧工作，也不恢复人物互推。若有资源失败，Console 会直接指出人物和具体 key；首绑日志可确认实际驱动。
