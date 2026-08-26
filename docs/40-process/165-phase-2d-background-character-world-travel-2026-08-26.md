# Phase 2D：Background Character World Travel Core（2026-08-26）

> 状态：**实现完成，待人工验收**｜最后更新：2026-08-26  
> 产品契约真源：[2K §5.9](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)

---

## 1. 范围

| 项 | 状态 |
|----|------|
| Background Travel Intent → Route → Execution → Arrival | ✅ |
| WorldLocation（WorldPresence）与 TravelState 分离 | ✅ |
| 复用 HexPathfinder + PlayerParty 连续推进预算 | ✅ |
| WorldSite Full-Footprint 边界出口（非 PresenceHex） | ✅ |
| Save/Load 中途旅行恢复 | ✅ |
| F12 DEBUG 面板 | ✅ |
| Autonomous AI / Encounter / Activity | Deferred |

---

## 2. 核心类型

- `BackgroundCharacterTravelService` — 开始/取消/推进/到达
- `BackgroundCharacterTravelBoard` — 每角色 route 状态（Traveling 时）
- `WorldAgentPresence.AtWorldPosition` — 连续 WorldPosition 真源
- `CharacterWorldMovementAuthorityQuery` — Party / Army / Local / Background 互斥

---

## 3. 人工验收（最短）

1. Active + 王尘 → Stop Follow → F12 → Travel To 青石镇 → 推进时间 → 确认 AtWorldSite
2. 中途 Save/Load → 位置与 Destination 延续
3. 王尘 Join Party → Background Travel 立即停止
