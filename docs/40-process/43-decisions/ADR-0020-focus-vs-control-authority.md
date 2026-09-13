# ADR-0020：FocusCharacter 与 ControlAuthority 分离；失能不立即改玩家身份

> **PARTIALLY SUPERSEDED（2026-09-12）：** “否则早期 GameOver、后期继承待定”已由 [ADR-0034](ADR-0034-conflict-control-succession-and-airship-role.md) 替代：队内按固定顺序自动接替；仅全队真正死亡后自动选择玩家势力最强合格角色；空势力终局延期。DirectControl／Focus／FactionLeader／PlayerIdentity 分离及失能不立即抹掉玩家身份仍有效。

- 状态：**已采纳**（补充 ADR-0011）
- 日期：2026-07-31
- 决策者：项目负责人（Freeze v0.2）

## 背景

需避免把“当前镜头控制的人”“势力领袖”“玩家身份”混为一谈；Focus 失能时不应瞬间抹掉玩家身份。

## 决策

- `DirectControl ≠ FocusCharacter ≠ FactionLeader ≠ PlayerIdentity`。
- Focus 不可用 → `FocusCharacterUnavailable`；不立即改变玩家身份。
- 有同行／代理／合法继承 → 继续；否则早期 GameOver，后期继承流程。

## 影响

见 `33` v0.2 §14、`34`。
