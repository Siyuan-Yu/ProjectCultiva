# Phase 5B — WorldMap ↔ LocalMap Travel View Takeover

- Date: 2026-08-29
- Baseline: `dev_1 @ 47b3f89`
- Status: **Accepted / Sealed**
- Manual acceptance: `Assets/Scenes/LevelTester.unity`

## 产品行为（已封板）

**相对 Phase 2C：**

| | 旧 | Phase 5B |
|---|---|---|
| AutoTravel 中关闭 WorldMap | `CancelTravel` → Expand LocalMap | **Preserve Travel** → Expand at continuous progress → `ExecutionMode=LocalVisible` |
| LocalVisible 期间 World Tick | （旧无此态） | **World Travel Advance 停止**（不推进 PlayerParty） |
| 再打开 WorldMap | （旅行已 Cancel） | 从**同一 Continuous Position** 恢复 World Advance |
| 多次开关 WorldMap | — | **无**位置漂移 / Route / Destination 异常 |

Idle 关闭 WorldMap：仍为原 Enter Local 行为。

本阶段**不**含角色自动走向 Exit / Local A*（留给 Phase 5C）。

## 验收结论（LevelTester）

人工验收通过，确认：

1. WorldMap Close **不再 Cancel** AutoTravel
2. LocalVisible 时 World Travel Advance **停止**
3. 再打开 WorldMap 可从同一 Continuous Position 继续
4. 多次开关 WorldMap 无位置漂移 / Route / Destination 异常
5. 场景：`Assets/Scenes/LevelTester.unity`

## 实现要点

1. `PlayerPartyTravelExecutionMode`：`None` / `World` / `LocalVisible`（在 `PlayerPartyWorldMotion`，不复制 Path/Progress/Position）
2. `PlayerPartyHexTravelService.CloseWorldMapTakeover`：AutoTravel 时不再 `CancelTravel`；设 LocalVisible + Preserve Enter
3. `AdvanceAll` / `AdvanceDistanceBudget`：LocalVisible 直接 return
4. `HostWorldMapPanel.Open` → `ResumeWorldTravelExecutionIfNeeded`（LocalVisible→World）
5. Materialize：LocalVisible Mid-Segment 用连续投影，避免刷 Hex 入口/中心
6. LevelTester Cheat：少量 Travel Debug 字段（MovementKind / ExecutionMode / Segment / WorldPos / LocalPos）

## 明确未做（非本阶段）

自动走 Exit、Local A*、Cross-Hex 续行、Marker/Route/Hex Math、SaveLoad、Timing、Follower、Battle Interrupt、大型 Executor 架构。

## Phase 5C — Wilderness Visible Continuous AutoTravel（已封板）

自动走 Exit / Local A* / Cross-Hex 续行由 Phase 5C 实现，LevelTester 人工验收通过。

Phase 5C accepted limitation:
Rare diagonal wilderness exit/local-navigation cases may leave
the Active Character near a map edge/corner.
Core travel remains valid; this is deferred to later navigation polish.
