# 角色关系网与人物档案 V1 封板记录

> 状态：**已实现／已人工验收／已封板（SEALED）**
> 日期：2026-09-07
> 封板范围：Character Social Relations V1、Character Profile UI V1
> 系统真源：[角色社会关系 V1](../20-systems/2M-character-social-relations-v1.md)
> 架构记录：[ADR-0030](43-decisions/ADR-0030-social-bond-attitude-and-snapshot-boundary.md)

## 1. Social Bond 与 Attitude 分层

- `SocialBondBoard` 保存角色之间“是什么关系”的客观事实。
- `RelationshipLedger` 保存角色 A 当前“怎么看角色 B”的单向态度事件，是态度唯一 Runtime authority；`RelationshipComponent` 仅为可重建缓存。
- Bond 与 Attitude 不合并。即使亲子之间好感为负、仇恨很高，`ParentChild` Bond 仍然存在。
- Friend／Enemy 不写入 Bond，而是从当前 Attitude 动态派生；仇恨阈值优先于好感阈值。

## 2. 五种正式 Bond

| Bond | 方向与存储规则 |
|---|---|
| `ParentChild` | `From=Parent` → `To=Child` |
| `Sibling` | 对称 canonical pair |
| `Spouse` | 对称 canonical pair |
| `MasterDisciple` | `From=Master` → `To=Disciple` |
| `SwornSibling` | 对称 canonical pair |

同一角色 pair 可以同时存在不同种类 Bond；同 Kind、同方向或同 canonical pair 不得重复。V1 不做关系闭包，不根据共同父母推导手足，也不根据共同子女推导配偶。

## 3. 五维 Attitude

正式五维为：好感 `Affection`、信任 `Trust`、敬重 `Respect`、畏惧 `Fear`、仇恨 `Grudge`。前三项范围为 -100～100，后两项范围为 0～100。写入统一经过 `RelationshipService`，Ledger 只记录 Clamp 后的实际变化量；旧 `Score` 兼容语义固定为 `Affection`。

## 4. 社会事件与 SocialTick

V1 已接入 `CharacterAttacked`、`CharacterHelped`、`CharacterRescued`、`CharacterKilled`。态度后果在 Core 同步结算，Host 只做表现。旧随机 Help／Slight `SocialTick` 已退出正常 Gameplay，陌生角色不会因无真实事件的随机模拟自动漂移关系。

Gift／送礼没有实现，继续作为后续事项。

## 5. CharacterKilled 与 Attachment

角色真正进入 Dead 且能够解析具体 killer 时，全世界立即查询与死者存在 Bond 或显著 Attitude 的角色并结算反应。V1 明确不做 Witness、Knowledge、Rumor、消息距离或传播延迟；远处亲属立即反应属于正式规则。

Attachment 由最强 Bond 基值、reactor→victim 好感与仇恨即时派生，不单独存储。正 Attachment 分三档产生对 killer 的好感下降与仇恨上升；负 Attachment 产生好感上升，但不降低既有仇恨。

## 6. Death Attribution

- 直接致死或补刀使用本次明确 attacker。
- 被打入 Incapacitated 后自然死亡，使用当时记录的 `ResponsibleAttacker`。
- `ResponsibleAttacker` 随 Entity Snapshot 保存，Save→Load 后仍可完成正确死亡归因。
- Recover／Captured 清除旧 attribution。
- killer 未知时保持未知，不猜测为 Player、当前主控或 PlayerFaction，也不触发击杀关系网后果。

## 7. 即时关系反馈

`HostSocialNotificationOverlay` 是屏幕空间、非 Modal、不中断时间的队列，同时最多显示三条。正式玩家反馈只显示好感方向和三档强度：`↑／↑↑／↑↑↑` 或 `↓／↓↓／↓↓↓`，不显示内部精确 delta 或仇恨数值。

通知 Target 始终是被态度指向的具体 Character。若 killer 是 PlayerParty 的非当前主控成员，反应仍指向该具体 killer，不重定向到 ActiveCharacter。

## 8. 人物档案四页

正式入口保持：场上点击角色只改变 Selection，底部人物信息栏显示选中者，玩家点击 `[人物]` 后才打开统一 `HostCharacterSheetPanel`。`[关系]` 直接进入同一档案的人际关系页，旧独立 Relation Window 不再用于正常 Gameplay。

统一档案固定左侧身份栏，顶部四页为：

1. **人物属性**：人物概况、个人属性、战斗属性、修炼／灵根、性格／背景／天赋，以及 subject→ActiveCharacter 的精确五维态度。
2. **人物故事**：只读 CharacterBio、authored bio 与真实 Relationship history，按 DayClock 显示近期经历时间线；不暴露 raw reasonTag、EntityId 或内部 enum，不新增 CharacterStory authority。
3. **亲族关系**：只绘制显式 ParentChild／Sibling／Spouse；父母在上、Subject 居中、配偶与手足在侧、子女在下。
4. **人际关系**：按好友、仇视、师徒、结义、其他分组；人物详情方向固定为 subject→selectedPeer，并显示五维精确值。

玩家可见术语统一使用“仇恨”。当前没有 Gender authority，因此亲族称谓只使用父母、子女、兄弟姐妹、配偶，不细分父亲／母亲等称谓。

## 9. 第一章亲族验收配置

正式开局 `base:scenario_ch01_reference` 显式包含：

- 阿石（`base:character_ch01_ref_mortal_a`）→ 阿土（`base:character_ch01_ref_farmer_b`）：`ParentChild`。
- 阿兰（`base:character_ch01_ref_herb_b`）→ 阿土：`ParentChild`。

这两条配置不附加 Affection／Trust seed，不推导阿石与阿兰的 Spouse，也不推导其它 Sibling 或亲属。阿土死亡时，阿石与阿兰仅凭 Parent→Child BondBase 即可对具体 killer 产生强烈反应。

## 10. SaveLoad authority

- `SocialBondBoard`、五维 `RelationshipLedger` 事件与 `ResponsibleAttacker` 都是正式 Runtime State，并由 Snapshot 保存。
- Restore 只恢复状态，不发布 `SocialReaction`、不重新结算 `CharacterKilled`、不重新 Apply Opening Bonds。
- 新档 `socialBonds=[]` 是 authoritative empty state；不得因列表为空而回退 Opening Content。
- Opening Bonds 只负责 New Game 初始化，运行后以 Snapshot 为准。

## 11. 明确 Deferred

以下内容不属于 V1，封板时均未实现：Gift／送礼与 NPC 礼物喜好、婚姻 Gameplay、拜师／收徒 Gameplay、结义 Gameplay、Revenge AI／寻仇、Crime、Wanted、Faction Reputation、Witness、Knowledge、Rumor、Social Event 消息传播、Relationship Graph 大型节点可视化、完整 CharacterStoryLedger、Gender 细分亲属称谓。

## 12. 封板验证

- Core／Data／Host／EditMode Tests 使用 Unity Bee response files 离线编译：0 error，仅保留既有 warning。
- Character Social Relations V1 定向测试：9/9 通过，其中覆盖五维与 Bond、社会事件与击杀反应、Snapshot 五维／Bond／死亡归因、旧事件兼容及严格 Content pipeline。
- 静态检查确认：生产路径无 `enableSocialTick:true`；无 Friend／Enemy Bond；无 Knowledge／Witness／Rumor；Selection／Avatar 点击文件未修改；`[人物]／[关系]` 均进入统一档案；Host 玩家文案无“怨恨”；临时 `.codex-*` 与 `.utmp` 已清空。
- `git diff --check`：通过，仅有工作树既有换行转换提示。

## 13. 封板结论

Character Social Relations V1 与 Character Profile UI V1 已完成人工验收并进入 **SEALED** baseline。除后续出现明确 Bug／Regression 外，不主动修改本页规则，不扩写 V2 功能。
