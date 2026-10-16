# 外观—必要动作—实际文件／复用矩阵（候选，不接入）

范围按 `base:roster_level_tester` 的 25 条名录和当前玩法。这里的“有图”仅表示候选文件存在；没有真实外观一致的帧时明确记缺，不能把动作前缀重复算作独立 ANIM 资产。

| 名录对象／数量 | 外观候选 | Idle / Walk | Work / Attack | 当前结论 |
| --- | --- | --- | --- | --- |
| `base:character_protagonist`（1） | `Static/ART-CHAR-002_protagonist_idle_south_v02.png`，蓝灰服 | 仅南向静帧；可参考 `ART-CHAR-001` 通用动作结构，但不能冒充同一服装/发型的完成帧 | 工作与攻击需保持本角色外观；现有蓝灰村民/巡卫动作只能作动作参考 | P0 外观动作未完成 |
| `base:character_companion_a`（1） | `Static/ART-CHAR-002_companion_a_idle_south_v02.png`，粉白服长发 | 仅南向静帧；其他方向/移动缺 | 工作与攻击同样缺；背负样本 `Previews/ART-ANIM-003_carry_actual_appearance_mount_sample_v02.png` 使用她实际静态外观，挂点仍待修 | P0 未完成 |
| `base:character_companion_b`（1） | `Static/ART-CHAR-002_companion_b_idle_south_v02.png`，绿服散发 | 仅南向静帧；其他方向/移动缺 | 工作与攻击缺 | P0 未完成 |
| `base:character_ch01_ref_supervisor`（1） | `Static/ART-CHAR-003_supervisor_idle_south_v02.png`，官帽长袍 | 仅南向静帧；通用动作可以作时序参考，但官帽衣摆在其他方向和动作中缺 | 根据正式战斗/工作角色实际触发补齐；不能套蓝灰村民外观假称主管 | P0 未完成 |
| `base:character_ch01_ref_guard_a/b/c`（3） | 共同使用 `ART-CHAR-004` 巡卫蓝白服与长枪 | `Frames/ART-CHAR-004/idle/` 四方向各 1 帧，`walk/` 四方向各 4 帧 | `attack/` 四方向各 4 帧；不按 3 人重复绘制 | 36 帧通过技术检查；动作切换预览已出，人工/Unity 未验 |
| `base:character_ch01_ref_mortal_a` 至 `base:character_village_recruit` 的 10 名村民/招募者（10） | `Static/ART-CHAR-005_resident_basket/apron/hoe_idle_south_v02.png` 为背篓男、围裙女、绿服锄具男三种真实可见差分 | 背篓男可使用 `Frames/ART-CHAR-001/idle/` 与 `walk/` 共 20 帧；围裙女与锄具男仅南向静帧，其他方向不一致 | 背篓男 `work/` 与徒手 `attack/` 四方向各 4 帧；农耕/采药/伐木/采矿/施工共用 ART-ANIM-002 时序，工具差异未独立交付；围裙/绿服攻击外观待补 | 部分 P0 覆盖，差分动作缺 |
| 江老、青石散人甲/乙、敌对散人、朔风平民甲/乙、商店掌柜、拍卖执事（8） | 暂按 ART-CHAR-001 基础身形或既有灰盒；正式身份差分属 ART-CHAR-006/007（P1） | 可复用 `ART-ANIM-001` 结构，当前不宣称 8 人外观完成 | Work/Attack 依正式触发，复用结构，不按人头新建动作族 | P1 差分待制作，不计本批完成 |

`ART-ANIM-001` 对应 `ART-CHAR-001/004` 的 Idle/Walk 帧结构；`ART-ANIM-002` 对应 `ART-CHAR-001/work`，不因 CHAR 前缀再计一套 Work；`ART-ANIM-004` 对应 `ART-CHAR-001/attack` 徒手与 `ART-CHAR-004/attack` 长枪样本，其他身份外观/武器仍不齐。`ART-ANIM-003` 只交付真实被搬运者静态组合样本，非可运行挂点。`ART-ANIM-005` 只交付同一外观的静态旋转/降饱和样本，非复杂动画或 Unity 状态机。

上述文件路径均相对本批 `ArtSource/Batches/Batch_Codex_002/`。主角、主管、居民差分没有四方向/动作连贯帧，不能写“待接入”。
