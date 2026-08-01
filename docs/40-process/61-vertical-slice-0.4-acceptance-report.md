# Vertical Slice 0.4 验收报告 — Unity Playable Host

> 状态：**已通过（自动化门禁）**｜日期：2026-08-01  
> 计划：[59-vertical-slice-0.4-unity-playable-host-plan-v0.1.md](59-vertical-slice-0.4-unity-playable-host-plan-v0.1.md)

## 1. 范围回顾

将 VS0.1～0.3 Core 闭环接入最小 Unity Host：Content 加载、EntityView、RTS 选择、PlayerCommand 桥、调试 HUD／时钟、事件反馈、Snapshot 存读。

**非目标（保持）：** 正式 UI、地图／寻路、战斗、Demo Runtime 迁移、改 Freeze。

## 2. Phase 与 Commit

| Phase | Commit | 说明 |
|---|---|---|
| V4-A | `4769cd1` | Host bootstrap／Content／World |
| V4-B | `eafa290` | EntityView |
| V4-C | `a61afcc` | RTS selection |
| V4-D | `1ac9e28` | Command bridge（Labor／Rest／Observe／Cultivate） |
| V4-E | `9fb2b63` | HUD + pause／speed／step |
| V4-F | `2595859` | DomainEvent feed |
| V4-G | `d15d351` | Snapshot UI |
| V4-H | （本提交） | 一日可玩自动化验收 |

## 3. 验收清单（对照 §13）

- [x] Editor Host 加载 BaseGame 并创建三角色 World  
- [x] 三人可见绑定（EntityView）且可点选／框选  
- [x] Labor／Rest／Observe／Cultivate 经 `PlayerCommandRequest`→Port  
- [x] HUD：Day／Hour、Action、Schedule、Quota、Risk、Realm 只读一致  
- [x] DomainEvent 调试反馈覆盖发现／拒绝／日界相关事件  
- [x] Snapshot 存读可用；Load 后 View 重建  
- [x] 自动化：Schedule → Observe 发现 → Cultivate → 跨日／后果路径烟测（`HostPlayableDayPhaseHTests`）  
- [x] 无地图／寻路／战斗／正式 UI／Demo 污染  
- [x] 未修改 Freeze；Snapshot schema 仍为 v1  
- [x] 每 Phase 独立 commit；EditMode 全绿  

## 4. 手操速查（Editor PlayableHost 场景）

| 输入 | 作用 |
|---|---|
| 左键／拖拽／Shift | 点选／框选／Toggle |
| 1–4 | Labor／Rest／Observe／Cultivate |
| Space | 暂停／继续 |
| `.`／N | 单步 Tick |
| `[`／`]` | 倍速 1→2→5 |
| F5／F9 | 存／读 Snapshot |
| F1／F2 | HUD／事件面板 |

## 5. 测试门禁

- EditMode：含 Phase H 整合测在内全绿（提交前跑 `tools/run-editmode-tests.ps1`）  
- PlayMode：既有 Host 烟测保持绿  

## 6. 后续

进入 **VS0.5 社会／人格／关系 Alpha**（Core 优先；不扩战斗／地图／正式 UI）。
