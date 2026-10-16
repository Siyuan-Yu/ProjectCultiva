# 人物动画、卧姿与 ArtSource 去重审计（2026-10-16）

状态：**Implemented / Producer Acceptance Pending**。主场景仅 `Assets/Scenes/LevelTester.unity`；未运行 Unity 或人工视觉验收。

## 已解决与实际接线

1. `HostStepAArt.CharacterFrame` 不再让 Protagonist、CompanionA、CompanionB 无条件返回南向静图。三人各有四方向 Idle、四方向各四帧 Walk；方向顺序 North/South/West/East，560×440 帧画布，300 PPU、(280,392) 顶坐标脚点。纹理按图集缓存，帧 Sprite 按需创建，角色之间复用。缺失 Work/Attack 时保留**本人**静态图，不切换成普通村民。
2. `EntityView.UpdateRuntimeArt` 结合原 Host 移动状态与本帧真实小幅位移显示 Walk，停步回 Idle；位移上限随既有 Host 移速与帧时变化，明显的单帧位置交接不当作行走。攻击事件优先于移动，弥留/死亡不播放攻击、行走和工作。
3. `EntityView` 从真实 `SleepingAtResidence`、`SleepingOnGround` 或运行中的 `BedRestAction` 判断睡眠。床上还需 `IsValidOccupiedBed`，并找到当前加载的同一 StableAssetId 床 View，才把唯一的非交互卧姿子层放到床面；站姿 SpriteRenderer 同时关闭。就地休息在人物现有位置显示卧姿；醒来撤销子层、恢复本人 Sprite。Root/Core 坐标、碰撞、选人圈、使用点、床位占用和结算均未改。
4. 九种当前 Profile（基础村民、巡卫、三主角、主管、锄具男、围裙女、背篓男）各有独立卧姿。被搬运者仍是原 EntityView 跟随 carrier，不新建可交互 Entity；弥留/尸体状态高于睡眠。生命状态恢复与运输脱离的真实效果仍需 PlayMode 检查。
5. `factionControlPost` 缺失的红旗 PNG 已补入正式 Resources；只有 `base:sect_huangcun_labor` 的正式旗帜 View 使用它，其他势力保留原旗帜，避免错误的全阵营红旗。已有树、矿、床、仓储和洞口接线未改。

## 新素材和残余问题

- `Batch_Codex_003/Originals`：12 张按身份/方向生成的四帧行走 strip，9 张不同服装卧姿，共 21 张新原图；内置生成通道无独立 medium 参数，只用提示词约束中等细节。
- `Sheets`：6 张 Idle/Walk 图集；`Frames`：60 张 Idle/Walk 单帧和 9 张卧姿；`Previews`：12 个 Idle→Walk 循环 GIF。原图未覆盖旧批次。`manifest.json` 记录来源 SHA-256、实际切片框、方向、帧号及脚点；加工脚本 `build_character_sheets.py` 可重建。
- 北、东、西 Idle 目前使用同方向 Walk 的近并步帧，技术上有朝向和身份，但静止感需目视审查；三主角 Work/Attack、主管和三类居民的方向/动作仍为本人静态回退。不能把这些记为完整动作集。
- 名录 25 人中 12 人已有 Profile，另 13 人仍无明确身份图：`herb_b`、`herb_c`、`woodcutter`、`wood_b`、`wood_c`、`jiang_lao`、`qingshi_acceptance_unaffiliated_a/b/hostile`、`qingshi_acceptance_shuofeng_civilian_a/b`、`qingshi_shopkeeper`、`qingshi_auction_attendant`。他们保留旧显示，不强套其他人物衣服；睡眠卧姿对这些人也仍缺。
- 睡眠状态图标尚无不改正式面板布局的安全入口；房间完整地板/墙角/门洞未过实际格距，农田/药田植物层 Alpha 与残土问题未解决，道路接口有接缝。以上继续旧表现。蓝、绿旗与其他势力身份的映射未建立。没有更改 Core、Content、地图位置或人物控制规则。

## SHA-256 清理清单

完整逐文件哈希在 `ArtSource/Batches/Batch_Codex_003/sha256_inventory.tsv`。哈希审计发现旧批次 **24 组**同字节图片：Inbox 对归档 Originals 10 组、Codex_001 Candidates 对 Originals 13 组、Codex_002 控制核心 Originals 对 Static 1 组。另有本批 Idle 对所复用 Walk 单帧的 9 组**有意复用**，以及 `.gitkeep` 小文件。旧重复图片的理论空间约 36.6 MiB，但并非已清理空间。

| 分类 | 范围 | 本轮处理 |
| --- | --- | --- |
| 保留 | 各批 Originals、正式运行源链、风格参考、manifest/提示词，以及本批有意复用的 Idle/Walk 帧 | 全部保留 |
| 完全重复但待引用迁移 | 10 个 Inbox 第一批副本、13 个 Codex_001 Candidates 副本、Codex_002 控制核心 Static/Originals 双份 | 文件哈希一致；BatchInfo manifest/索引、批次 manifest、273、加工脚本仍引用旧路径，因此未删除 |
| 被替代可删 | 旧错误切片、道路/田地实验件和预览缓存 | 仍承担失败证据或加工输入，未能证明无外部引用；暂缓 |
| 可重建缓存 | `Batch_Codex_002/__pycache__/build_characters.cpython-312.pyc`，11,870 字节 | 删除命令被当前执行策略拒绝；保留并如实列出 |
| 暂缓确认 | 项目外部可能直接引用的旧路径 | 无法从仓库内证实无引用，不删除 |

**实际删除：0 文件；实际释放：0 字节。** 无删除后断链风险。现存 ArtSource JSON 中的 `ArtSource/` 路径已静态检查，12 份 JSON、0 条缺失路径。当前 `HostStepAArt` 明文资源名与 `Assets/Art` PNG 静态对照，无缺失。

## 检查与人工验收

- `pwsh -File tools/offline-compile.ps1`：Core、Data、Unity、Editor 与其余程序集均 `ALL_OK`；测试源码编译有两条既有警告，不代表测试运行。
- 素材完整性：6 张图集尺寸与 Alpha 符合格式；9 张卧姿透明；12 个动画预览已生成。没有运行 Unity Editor、PlayMode、自动测试。仓库 `AGENTS.md` 禁止代理编写和运行自动测试，附件中的编辑器测试项留给制作人后续授权流程。
- TSV 为登记真源；项目既有 `tools/export-art-register.mjs` 已导出 XLSX，55 行全字段回读通过。状态均为“已接入待验”或明确缺口，没有标为已验收。

LevelTester 最短人工检查：①主角及两同伴四方向连续移动、停步、重新起步，看衣服/脚点/帧循环；②基础村民和围裙女/锄具男/背篓男到合法床边睡眠，确认床面只有一个卧姿且床仍可交互；③就地休息与醒来，确认站姿和控制恢复；④巡卫/主管移动和攻击时不换人；⑤弥留/尸体/搬运不与卧姿或行走重影；⑥检查红旗仅属于压迫宗门，同时复看床、树、矿原交互。
