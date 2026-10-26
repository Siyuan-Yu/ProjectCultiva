# 荒村炉具补齐与占领前循环复核

> 本页循环盘点记录的是补炉具时的状态。之后 393 已新增两人开局／被囚同伴、限额配给、城外修炼与私人营地；因此下方“三人开局”“自由修炼”“未限制公库”等不再代表当前版本。当前交付、版本、剩余工作及提交范围统一见 [394](394-huangcun-survival-checkpoint-and-backlog-2026-10-26.md)，炉具配方仍未实现。

## 本轮授权

在现有丹室／炼器室各放置一座独立可见炉具，保留房间尺寸和入口，不实现配方、制造订单、材料消耗或产出。两炉为作者预置设施，本轮不扩建造系统或炉具制作玩法。制作人同时要求复核原预期与当前荒村循环；盘点不等于授权自动补齐所有旧草案系统。

炉具独立稳定 ID、占地和阻挡进入作者蓝图与正式发布，不仅在 Host 临时贴图。炼丹炉 3×3、炼器炉 3×4，置于室内靠后，门口及两侧留通路。使用 imagegen 生成透明独立贴图，接现有 MapKind／Inspect 表现，明确“制作功能暂未开放”，不冒充可生产工位。

## 发布结果与验收

`0.0.39-huangcun-furnaces` 正式三件套发布完成：世界仍为18450×8700，8段河／13桥／36道路。运行物件9227→9229，仅新增两座炉具；静态逐项对照原9227物件无变化，Site地点／出生锚点／旗保持一致，Site地区逐属性值无变化（序列化属性顺序可变化）。炉具唯一且位于对应房间两格内缩的可用区，阻挡为true；房间、城镇归属及尺度未改。

导航／大地图 sourceHash 均为 `ac35a249c0af12dff8e771a7c4e47c09a19855b600d92bdf3387beaa40bdf397`。四程序集基础编译通过，发布工具构建0警告／0错误，两PNG透明背景已检查；未启动Unity、未运行自动测试、未提交推送。旧备份保留，不作为交付真源。

需新开局验收作者预置设施：去东北丹室／炼器室查看不同外形炉具、门口与侧边通行、人物遮挡和Inspect说明。当前是 **Implemented / Producer Acceptance Pending**，贴图已目检不等于游戏内表现已验收。旧档不保证自动补新增预置物件。

## 当前可玩的循环（源代码／内容复核，非实玩验收）

玩家在封闭盆地中的荒村起步，自选劳动、交谈、修炼或探索；维持饱食、精力和医疗状态，获取粮木药矿；通过村民／将老对话、棋局与洞府找成长机会；实际打坐、主动突破、取得书册并研读、使用境界能力和斗技；准备战力及补给，按敌对确认／守军／核心耐久／驻留占领链夺村；随后解锁外界、势力管理与建设。上述不是线性任务链，不要求先交完委托或达到筑基才允许挑战。

当前已经有“可选择的活动与成长出口”，但是否形成好玩的循环，仍不能只凭这些系统存在就认定达标。

## 原预期与当前差距

| 体验目标 | 证据与当前状态 | 结论／后续方向 |
|---|---|---|
| 自由成长，无固定天数和强制教学 | `huangcun_life_chapter.json` plannedDays=0，开局可选自由行动；391 帮助默认收起 | 主方向符合；保留非线性，不增加任务解锁锁链 |
| 从受压迫劳役到能反抗的人 | `QuotaConsequenceHandler` 仍记录缺额／训斥；`SupervisorPressureHandler` 写压力旗标，但引用的 `base:event_ch01_ref_supervisor_pressure` 在当前事件内容中未找到。不能据此宣称完整训斥、扣粮、惩罚闭环 | 体验反馈不完整；后续先讨论轻量、可理解的劳动压力后果，不直接恢复旧稿全部惩罚 |
| 秘密修炼与藏修选择 | `ConcealmentExposureRules` 计算风险，代码明确为 display-only、无追捕处罚；出村巡卫为作息／近场门禁 | 目前更接近自由修炼加出入口限制，尚不等于“隐藏修士”完整博弈。复杂追捕本轮不做 |
| 生产支持生存和成长 | 粮药有生活／医疗消耗，木矿有资源与后续建设用途；`WorldSiteStorageAccessPolicy` 明确允许直属附庸共享宗主库存，并非越权 bug | 初始存粮和取用规则可能弱化采集必要性，需要看实际消耗；占领前木矿及丹器材料的用途仍偏薄，不擅自封库或加硬性饥饿门槛 |
| 探索与关系带来机会 | 有洞府书册、将老棋局传承、真实交付与关系增减；相关任务非必做 | 基础成立，但可重复的独特事件与关系后续仍有限。交付任务改说明不等于玩法差异已经充足 |
| 成长能明显改变行动能力 | 修为、瓶颈、研读、炼气／筑基等能力与斗技已有实际系统 | 待实玩确认新能力能否及时被理解和使用；不要靠增加修炼时长填内容 |
| 三人分工和失败恢复 | 三人轮换／同行、食宿、伤员与医疗有系统 | 玩家是否需要分工、受伤是否可救、失败是否可重整仍待实玩；不是已经确认全部失效 |
| 40～60 分钟进入炼气（早期文档体验目标） | 20／2G 的旧节奏目标仍可作为观察参考，但缺少当前版本正常开局实测 | 不能当作已达标指标，也不能把这段时间误当作夺村总时长。本轮不改修炼数值 |
| 真正夺村后得到自由与经营权 | 核心耐久／驻留占领与真实所有权解锁已接线 | 架构出口符合；守军难度、物资消耗、失败恢复及占领后的体验变化尚需完整一轮人工确认 |
| 功能房中有对应设施 | 此前341明确仅房屋，本轮补两座独立炉具 | 外观／空间补齐，配方生产按制作人要求仍不做，不计作完成炼丹炼器系统 |

旧文档不是一张可以直接全部照做的欠账清单：2I 的强制筑基夺权、固定阶段叙述等已被后续真实占领及自由成长方向覆写；姓名背景生成、告密、复杂社会分支、中高级工坊等也不能因为在旧草案中出现就自动实施。

## 下一步建议（尚未实施）

先从正常初始三人完整玩到夺村，记录“最长无决策等待”“哪类物资确实消耗”“同伴是否需要分工”“每次受阻能否看懂”“打输能否恢复”。优先补真实循环的薄处，而不是继续增加教程或房间：劳动压力的清晰轻量反馈、各成长机会的不同收益、战前准备和战败恢复。是否加后果、调整库存／节奏需要制作人下一轮确认，不在本轮暗改。

结论：**自由成长→夺村的主干符合当前方向；压迫与秘密成长的主题、资源用途密度、正常成长节奏及失败恢复尚不足以认定“荒村体验完整完成”。**

## 美术与实现来源

本轮使用 imagegen 技能生成两张透明独立炉具，已目检并复制到 `Assets/Art/Environment/Resources/ART-ROOM-013_alchemy_furnace_v01.png` 与 `ART-ROOM-014_smithing_furnace_v01.png`，附独立 GUID 的导入 meta。丹炉青铜圆鼎，器炉石铁方炉；复用现有贴图缓存、物件显示／Inspect 机制，不添加每帧动画、灯光或新的材质实例。

作者真源：`Content/BaseGame/Authoring/ContinuousSurface/Blueprints/huangcun.worldsiteblueprint.json`。正式发布沿现有 FinalWorldPublish，不手改缓存。炉具没有生产按钮和假产出；暂不提供玩家建造炉具入口。

生成方式：内置 image_gen，非 CLI，`transparent_background=true`，两次独立生成，无输入图片。最终提示词：

```text
Single isolated game prop sprite: a low-level Chinese xianxia alchemy furnace, squat round aged bronze tripod cauldron with lid, two handles, small dark fire opening, no active fire. Orthographic elevated front view looking down about 55 degrees, front faces straight down screen, no isometric rotation. Hand painted realistic 2D colony simulation prop, muted earthy bronze, clear readable silhouette at small size, entire object centered with modest transparent margin. No room, no floor, no scene, no text, no UI, no extra objects. Transparent background. Output a single square PNG game asset.

Single isolated game prop sprite: low-level Chinese xianxia blacksmith refining furnace. Compact rectangular dark stone forge, arched black fire chamber with dim reddish coal inside, short stout chimney at back, iron bands around masonry, clear distinct box shape unlike a cauldron. Orthographic elevated front view looking down about 55 degrees, front faces straight down screen, no isometric rotation. Hand painted realistic 2D colony simulation prop, muted earthy stone and iron, readable at small size. Entire object centered in square with modest transparent margin. No room, floor, scene, people, words, UI or extra tools. Transparent background. Single PNG game asset.
```
