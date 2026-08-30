# 156 — Pure Hex World Model 替换

> 日期：2026-08-23  
> 状态：**已实现（战略 Runtime）** — 性能优化 DEFER

## 已完成

### 领域真源

| 类型 | 说明 |
|------|------|
| `HexWorld` | 唯一战略空间数据源（原 `HexGridBoard`） |
| `HexCell` | 最小空间单位（原 `HexTile`） |
| `WorldSite` | 地点实体，`AnchorHex` + `OccupiedHexes` 足迹 |
| `ArmyHexPosition` | `CurrentHex` + `NextHex` + `MoveProgress` 视图 |
| `HexTravelPlan` | 无 RouteId 的路径计划 |
| `HexWorldScale` | RimWorld 式小格尺度（`HexSize=0.22`） |

### 战略 Runtime（Hex-only）

- `StrategicTravelDriver`：仅 `HexWorld.HasGrid` 时推进 `ArmyHexTravelService` + `ArmyHexPursuitService`
- `ArmyHexCommandService` / `HostWorldTravelDeparture`：移动/攻击仅 Hex
- `ArmyTravelCommandService`：Hex 地图存在时拒绝 Route 移动
- `ArmyFormationSitePolicy`：组军/驻扎须在己方 `WorldSite` 足迹内
- `FormalArmyWorldPositionResolver`：Hex 地图存在时优先 Hex 坐标

### 视觉

- 密集小 Hex、军队格心对齐、敌军栈 Hex 投影
- Hex 模式下绘制敌军栈 + 弥留头像

### 测试

- 主要 Army/Strategic Phase 测试已迁移至 `HexTestWorldBootstrap` + `AnchorOnHex`

## 仍保留（非战略空间真源）

- `WorldGraph` / `WorldNode`：内容导入、LocalMap 元数据、`LegacyNodeId` 桥接
- `PartyWorldPresenceMode.AtNode`：角色驻留索引（映射到 WorldSite）
- `FormalArmy.NodeId`：内容/LocalMap 兼容字段，**非**战略位置真源

## 明确废弃（Runtime 不再用于移动）

- `WorldRoute` 战略拓扑
- `RouteProgress` / `OnRoute` 移动
- `MoveArmyToNode` / `MoveArmyToRouteProgress`（Hex 地图存在时拒绝）
- `StrategicPursuitService` 路线追击（Hex 模式 no-op）

## DEFER

- 大地图性能（RimWorld 式视口裁剪 / 合批 / LOD）
- `WorldNode`/`LegacyNodeId` 内容层完全删除
- Wilderness LocalMap

## 验收（Unity Runtime）

1. 密集小 Hex 格 + 军队对齐
2. 右键移动 / 攻击 / 追击接战
3. 组军仅在己方 WorldSite
4. 无 Route 战略移动

EOF
