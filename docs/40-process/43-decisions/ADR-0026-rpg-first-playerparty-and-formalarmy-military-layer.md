# ADR-0026：RPG-First — 单 Active Character、PlayerParty、连续 Hex 世界与 FormalArmy 军事层

- 状态：**已采纳**
- 日期：2026-08-25（Decision #12 补钉：2026-08-26）
- 决策者：项目负责人（核心玩法与世界架构方向调整）
- 关联：[2K RPG-First 系统真源](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)、[2A](../../20-systems/2A-factions-armies-diplomacy-and-capture.md)、[2J](../../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)、[ADR-0020](ADR-0020-focus-vs-control-authority.md)、[ADR-0024](ADR-0024-real-cultivators-and-army-strategic-model.md)、[ADR-0025](ADR-0025-strategic-spatial-model-hexgrid.md)

## Context

在 Pure Hex、FormalArmy 真实成员、Multi-Hex WorldSite 落地后，产品体验逐渐偏向：

- 多单位 WorldMap **RTS／4X** 操作感过强  
- Character 可替换、上帝附身远距离切换，**修仙 RPG 身份变弱**  
- 「跨 Hex 必须 Army」把个人旅行绑死在军事组织上  
- WorldMap／LocalMap 关卡式进出，**连续世界与未来飞行**难以获得真正空间意义  
- 远方 Army 可手操切入，进一步强化「玩家=势力意志」而非「玩家=修仙者」

制作人明确：**游戏本质首先是修仙 RPG，不是 4X／Total War。**

## Decision

采用 **RPG-First** 控制与世界存在模型（细则见 [2K](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)）：

1. **Single ActiveControlledCharacter** — 任意时刻最多直接即时控制 1 名角色。  
2. **PlayerParty max 6** — 1 Active + Followers AI；Follow ≡ 入队。  
3. **Background Character Simulation** — 非 Party、非 Army 角色可后台旅行／战斗，WorldMap 不常驻头像，无政治 Capture 权；**可 World Travel 但不可被玩家远程指定 Hex／路径**（移动由 AI／Policy／剧情／系统目标驱动，非隐藏 RTS）。  
4. **FormalArmy = 军事远征层** — 不再是世界移动资格；我方 Site 组／解散；默认 Auto Battle；Party 距离 ≤1 可介入但不接管 Army；接受**战略军事命令**（与 PlayerParty 世界旅行命令分离）。  
5. **Continuous HexWorld topology** — HexWorld=世界本身；LocalMap=近景；WorldMap=总览／旅行视图。  
6. **PresenceHex** — Multi-Hex Site 上 Character 的固定世界位置代理（与 AnchorHex 职责分离）。  
7. **Succession V1** — Party 全灭进入继承流程，不默认 Game Over；合格角色：同 Faction、Alive、可行动、未 Captured、不在出征 Army、位于己方 Site；**无境界门槛**。  
8. **Character Policy** — 非 Active 以长期权限／倾向控制，不做远程逐步 RTS 命令。  
9. **PlayerParty Capture** — 攻占据点须完整 **War + CaptureObjective + Capture**（2A）；特权仅为不必转 FormalArmy 且可 LocalMap 手动战。  
10. **宗门公共资源** — Sect/Faction Storage 默认仅玩家分配；NPC 不得自主领取（未来开放须玩家授权）。  
11. **LocalMap Camera（2026-08-25 补钉）** — **仅 WASD Direct Movement** 触发 Snap＋Hard Follow；**RTS／右键寻路完全不控制镜头**；中键仅自由 Pan。细则见 [2K §1.1](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)。  
12. **Continuous WorldPosition + WorldMap 命令精度锁（2026-08-26 · Phase 2C）** — Runtime 位置真源为 **Continuous WorldPosition**；`CurrentHex = WorldToHex(...)` 为派生。**WorldMap 命令精度永久仅限 Hex／WorldSite**；**PreciseWorldDestination／点击像素作目的地 FOREVER FORBIDDEN**。`WorldLocation`（`AtWorldSite`｜`AtWorldPosition`）与 `MovementState`（`Idle`｜`AutoTravel`）分离。全体 WorldSite 为 Aggregated（LocalMap 只改 LocalPosition；WorldMap 投影=PresenceHex）。Phase **2C 实现 PlayerParty 连续旅行**（非 FormalArmy；无 Fake Army）；Background Continuous Travel／FormalArmy continuous **Deferred**。细则见 [2K §5.8](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)。

### 对既有 ADR 的关系

| ADR | 关系 |
|-----|------|
| **ADR-0024** | **部分 supersede**：废除「跨点必须 Army」；保留真实 Character 成员、LOD、战损回写、禁止匿名修士兵力 |
| **ADR-0020** | **补充对齐**：Active ≈ DirectControl；Focus／FactionLeader／PlayerIdentity 仍分离；Succession 细化全灭路径 |
| **ADR-0025 / Pure Hex** | **保持并扩展**：Hex 从「Army 棋盘」升格为「唯一世界拓扑」 |
| **ADR-0007** | **仍然有效**：分级模拟映射到 Party Hot／Background Low／Army Strategic |
| **2A 铁则 4** | **Superseded**（见 2K OLD-01／02） |

## Consequences

- Host **RTS 多选／右键多单位下令**需迁移为 Legacy（Phase 1）。  
- **Army 职责边界**变更：组军 UX、WorldMap 选中、旅行入口需改（Phase 3）。  
- **World Presence** 需区分 Party／Background／Army（Phase 2）。  
- **Manual Battle 权限**收紧：远方 Army 不可手操切入（Phase 4）。  
- **Continuous LocalMap↔Hex** 与 Party AutoTravel：契约见 2K §5.8（Phase 2C）；Background／Army 连续移动、Flight／Policy／Sect Mission 分阶段 Future。  
- Snapshot／Save 可能需增加 Party／Policy／PresenceHex 字段（实现阶段再定 schema）。  
- **不**删除 FormalArmy 系统；是重新定义职责，不是推倒。

## 非目标（本 ADR 不授权实现）

写 Runtime C#、改 JSON／Editor 实现细节；本 ADR **锁定产品契约**。Background Continuous Travel、FormalArmy Continuous Movement、Flight、Territory Tint、Dynamic Bandit 仍不在本决策授权范围内。

## 未决

**已关闭（2026-08-25 补钉）：** Succession V1 合格条件、PlayerParty Capture 须走 War + CaptureObjective — 见 [2K §4／§9](../../20-systems/2K-rpg-first-character-control-playerparty-and-continuous-hex-world.md)。

**仍 Deferred：** Background Battle 通知／日志 UX 粒度（不阻塞架构）。

```text
No hard product-level blockers for starting Phase 1 implementation planning.
```

技术类名／字段名本 ADR **不锁定**。
