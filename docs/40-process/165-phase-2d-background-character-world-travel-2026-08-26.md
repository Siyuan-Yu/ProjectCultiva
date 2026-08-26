# Phase 2D：Background Character World Travel Core（2026-08-26）

> 状态：**实现完成，待人工验收**｜最后更新：2026-08-26  
> 产品契约真源：[2K §5.9](../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)  
> **人工验收 Scene（唯一）：** `Assets/Scenes/LevelTester.unity`（复用 `PlayableHostBootstrap`，不要求 `PlayableHost.unity` 同步验收）

---

## 0. 内部实施顺序（Phase 2D 子阶段）

| 子阶段 | 内容 | 状态 |
|--------|------|------|
| **2D-A** Background Simulation Foundation | `BackgroundSimulationScheduler`：centralized low-frequency tick、bucket stagger、`elapsedWorldTime` 距离预算 | ✅ |
| **2D-B** Background Character Travel | Route planning（一次性 A*）+ 连续 WorldPosition 推进 + Arrival | ✅ |
| **2D-C** Save / Load + Authority | `WorldPresence` 真源、Authority 互斥、中途旅行快照 | ✅ |
| **2D-D** Debug / Acceptance | F12 DEBUG 面板 + EditMode 自动测试 + 500 角色结构基准 | ✅ |

**架构约束（2D-A）**

- Continuous World Time ≠ 全角色 Full Realtime Simulation
- Loaded LocalMap → Full Realtime；Background Traveling → centralized scheduler
- 禁止每 Character `MonoBehaviour.Update()` / 每帧全量遍历 + 每帧 A*
- Pause → `elapsedWorldTime = 0`（Simulation tick 不推进）
- 2x / 5x → Host 自动步进更多 world tick（增大 elapsed world time），非单次 tick 内乘倍率
- Staggered bucket（16）+ `currentWorldTick - lastProcessedWorldTick` 保证低频处理不降低实际移动速度

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

- `BackgroundSimulationScheduler` — Phase 2D-A 统一低频后台调度（Travel 首个消费者）
- `BackgroundCharacterTravelService` — 开始/取消/推进/到达
- `BackgroundCharacterTravelBoard` — 每角色 route 状态（Traveling 时）
- `WorldAgentPresence.AtWorldPosition` — 连续 WorldPosition 真源
- `CharacterWorldMovementAuthorityQuery` — Party / Army / Local / Background 互斥

---

## 3. 人工验收（最短）

1. Active + 王尘 → Stop Follow → F12 → Travel To 青石镇 → 推进时间 → 确认 AtWorldSite
2. 中途 Save/Load → 位置与 Destination 延续
3. 王尘 Join Party → Background Travel 立即停止
