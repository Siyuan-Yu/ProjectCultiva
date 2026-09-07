# 角色社会关系 V1

> 状态：**已实现／已人工验收／已封板**｜优先级：P0｜最后更新：2026-09-07
> 上级：`docs/00-project/00-overview.md`
> 关联：`28-jianghu-relations.md`、`2E-events-and-world-state.md`、`ADR-0017`、`ADR-0030`
> 封板记录：[202](../40-process/202-character-social-relations-and-profile-ui-v1-sealed-2026-09-07.md)

## 1. 系统边界

角色社会关系由两类互不替代的数据组成：

- `SocialBondBoard` 保存客观关系事实，例如亲子、兄弟姐妹、配偶、师徒和结义。
- `RelationshipLedger` 保存角色 A 对角色 B 的单向态度变化历史，是态度唯一真源；`RelationshipComponent` 只做可重建缓存。

个人关系与 `Faction Diplomacy` 分层，禁止把个人好感写入势力外交表，也禁止用势力关系反推个人态度。

## 2. Social Bond

V1 正式种类：

| `SocialBondKind` | 方向 | 角色含义 |
|---|---|---|
| `ParentChild` | `From=父母` → `To=子女` | 父母／子女 |
| `Sibling` | 对称、规范化端点 | 兄弟姐妹 |
| `Spouse` | 对称、规范化端点 | 配偶 |
| `MasterDisciple` | `From=师父` → `To=徒弟` | 师父／徒弟 |
| `SwornSibling` | 对称、规范化端点 | 结义 |

同一对角色可同时具有不同种类 Bond；同种类、同规范端点禁止重复。普通 Gameplay 不得解除 `ParentChild` 和 `Sibling`，Snapshot 恢复使用不发事件的专用恢复入口。

## 3. 五维态度

| 轴 | 范围 |
|---|---|
| `Affection` | -100～100 |
| `Trust` | -100～100 |
| `Respect` | -100～100 |
| `Fear` | 0～100 |
| `Grudge` | 0～100 |

所有变化必须经 `RelationshipService.RecordAttitudeDelta` 写入 Ledger。事件只记录边界裁剪后的实际变化量。旧 `Score`、`GetCachedToward` 和旧 `openingRelations.delta` 均继续表示 `Affection`。

V1 不启用按 Tick 自动漂移，生产启动明确使用 `enableSocialTick:false`。

## 4. 客观社会事件与后果

`SocialEventService` 先发布客观事件，再同步交给 `SocialConsequenceService`：

| 事件 | 定向后果 |
|---|---|
| Attack | 受害者 → 攻击者：好感 -10、仇恨 +10；每次新敌对交战只记一次 |
| Help | 受助者 → 帮助者：好感 +10、信任 +5 |
| Rescue | 被救者 → 救援者：好感 +25、信任 +15 |
| Kill | 全世界符合条件的角色按其对死者的 Attachment 立即反应 |

死亡责任者在进入弥留时写入 `CombatDeathAttributionComponent`，会随 Snapshot 保存。补刀显式攻击者优先；否则使用弥留归因。恢复或被俘会清除归因；未知责任者不触发击杀社会反应。

## 5. Attachment 与击杀反应

```text
Attachment = Clamp(最强 Bond 基值 + Affection / 2 - Grudge / 2, -100, 100)
```

Bond 基值：父母→子女 80、子女→父母 70、配偶 80、兄弟姐妹 60、师父→徒弟 50、徒弟→师父 65、结义 65。

| Attachment | 对凶手好感 | 对凶手仇恨 |
|---|---:|---:|
| ≥70 | -45 | +70 |
| 40～69 | -25 | +40 |
| 15～39 | -10 | +15 |
| -14～14 | 0 | 0 |
| -15～-39 | +10 | 0 |
| -40～-69 | +25 | 0 |
| ≤-70 | +45 | 0 |

负 Attachment 的“幸灾乐祸”只增加对凶手的好感，不降低既有仇恨。V1 为全局即时传播，不做目击、消息传播或知识系统。

## 6. Content 与开局

`openingRelations` 保持旧格式并只播种 `Affection`。新增可选 `openingBonds`：

```json
{
  "kind": "ParentChild",
  "fromDefinitionId": "base:character_parent",
  "toDefinitionId": "base:character_child"
}
```

Loader 严格拒绝未知字段与未知 kind；Reference Validator 校验两端 Character、禁止自指，并按方向／对称规范化后拒绝重复。验收数据只使用带 acceptance 语义的既有角色，不新增正式剧情设定。当前组合为：`unaffiliated_a`（死者）、`unaffiliated_b`（父母）、`unaffiliated_hostile`（兄弟姐妹）、两名 `shuofeng_civilian`（高／低好感）和 `shuofeng_guard`（完全无关）。

第一章正式开局 `base:scenario_ch01_reference` 另显式设定两条亲子事实：阿石（`base:character_ch01_ref_mortal_a`）→阿土（`base:character_ch01_ref_farmer_b`），阿兰（`base:character_ch01_ref_herb_b`）→阿土。这里只建立 `ParentChild` Bond，不推导阿石与阿兰的配偶关系，也不附加任何态度 seed。

## 7. Snapshot 与 UI

Schema v6 软附加：

- `relationshipEvents[].axis`、`contextEntityId`；旧档缺 `axis` 时按 `Affection`。
- 根级 `socialBonds`；旧档缺省为空。
- Entity 级死亡责任者；旧档缺省为未知。

不提升 Schema 版本。恢复时先恢复 Bond，再恢复 Ledger 并整体重建缓存。

Host 使用统一“人物档案”大面板：左侧身份栏固定，顶部为人物属性、人物故事、亲族关系、人际关系四页。属性页按概况、个人属性、战斗属性、修炼资质和人物标签分区；故事页只读人物小传与 `RelationshipLedger` 真实事件，使用 `DayClock` 时间线且不展示内部标签或变化数值；亲族页只绘制显式亲子、手足、配偶 Bond；人际页按好友、仇视、师徒、结义、其他分组，并以“当前人物 → 所选角色”为五维详情方向。旧独立关系窗只保留兼容转发，不再绘制。

右侧通知只呈现目标为 PlayerParty 成员的 `SocialReaction`，隐藏具体数值、使用箭头强度，最多同时三条且不阻断输入。

## 8. V1 非目标

不实现自动关系衰减、目击与消息传播、关系图谱编辑器、程序化家谱、恋爱／结拜／拜师完整玩法、势力外交联动、复仇任务或报复 AI。
