# 121 · 住房分配与主管府控制核心（2026-08-15）

> 状态：**已落地；制作人手操签收通过（2026-08-15）**｜日期：2026-08-15  
> 相对提交：`267d5c7` 之后 → 本轮 `main`  
> 飞书：https://my.feishu.cn/docx/NjepdWBA2o8O6kxzLxycQjgTnUf  
> 上级设计：[26 领地经营](../20-systems/26-territory-management.md)  
> 相关：[120 人物／名册／倍速](120-character-roster-editors-and-timescale-rollup-2026-08-15.md)｜[112 MapEditor](112-map-editor-usage.md)｜[114 Level Tester](114-level-tester.md)｜[SCHEMA](../../Content/BaseGame/Data/SCHEMA.md)

---

## 1. 一句话

**住房区 ≠ 主管府。** 住房靠工区＋`homeWorkAreaId`；主管府是 `controlCore`（可打、可占、授权限）。占领后可改住房归属与课表。Level Tester **Import** 重刷不再叠旧建筑。

---

## 2. 交付对照

| 主题 | 做什么 | 入口 |
|------|--------|------|
| **住房／府拆分** | 地图 `zoneHousing` 只划范围；休息＝Location←WorkArea←`homeWorkAreaId`；府＝`controlCore` 不是住房 | MapEditor／Region／WorkArea／CharacterNpc |
| **住房点选** | 左键空地住房区 → 看归属／入住；占领后可改（限玩家阵营） | `HostHousingAreaSelection`／`HousingAssignmentService` |
| **主管府状态** | 左键点建筑任意处 → 耐久／占领进度面板 | `HostControlCoreQuery` footprint |
| **攻击／占领** | 选中己方 → 右键建筑 →「攻击」→ 靠近后按正式近战节奏拆耐久（攻−防/2，间隔同互砍）；破门后站满 `occupyHoldSeconds`（默认 10）自动占 | `HostNpcContextMenu`／`HostControlCoreAssault`／`ControlCoreService.ApplyStrikeFromAttacker` |
| **权限** | 内容 `grantsPrivileges` → `SettlementAuthority`（`manageHousing`／`manageSchedules`） | `SettlementAuthorityBoard`；课表可点切换活动 |
| **无交互点** | `controlCore` 不再挂 Work 交互点（避免误劳动） | `MapKindCatalog` |
| **Import 清旧** | Clear 清空 `mapRoot` 全部子物体（修 `_built` 未序列化叠图） | `HostDemoTileMap.Clear` |
| **Map 清理** | 删「小房子」`house`／`SmallHouse.prefab`；`roadHub`→`controlCore` | MapEditor／Host |
| **内容字段** | `maxDurability`／`defense`／`occupyHoldSeconds`／`grantsPrivileges` | SCHEMA／WorkAreaEditor／`work_areas.json` |

---

## 3. 制作人：三个住房区怎么配

1. MapEditor：画 3 个 `zoneHousing`，label 分清凡人／巡卫／主管，各填 `boundLocationId`。  
2. RegionEditor：三个地点（样例 `loc_ref_houses`／`loc_ref_guard_housing`／`loc_ref_supervisor_housing`）。  
3. WorkAreaEditor：三个住房工区，`residentTags` = mortal／guard／supervisor。  
4. CharacterNpcEditor：每人 `homeWorkAreaId` 指向对应工区（**归属不在 MapEditor 填**）。

| 地图分区 | 地点 | 工区 | 谁休息 |
|----------|------|------|--------|
| 凡人住房区 | `loc_ref_houses` | `workarea_houses` | mortal |
| 巡卫住房区 | `loc_ref_guard_housing` | `workarea_guard_quarters` | guard |
| 主管住房区 | `loc_ref_supervisor_housing` | `workarea_supervisor_quarters` | supervisor |
| 主管府建筑 | `loc_ref_road_hub` | `workarea_supervisor_mansion`（`isControlCore`） | 不休息 |

---

## 4. 主管府：攻击／占领／权限

```text
左键建筑任意处 → 看血量
选中己方 → 右键建筑 →「攻击」→ 走到府外缘
靠近 footprint＋边距 → 按近战间隔挥砍；伤害＝max(1, 攻击−建筑防御/2)（与地图互砍同式）
耐久 0 → CaptureAvailable
继续站满 occupyHoldSeconds → 自动占领
SettlementAuthority.GrantAll(grantsPrivileges)
→ 可改住房归属＋右栏课表活动块
```

样例府：`maxDurability: 100`，`occupyHoldSeconds: 10`，`grantsPrivileges: [manageHousing, manageSchedules]`。

**建筑编辑器：** 本阶段不做。控制核心＝工区能力字段；种类多了再抽专用编辑器。

---

## 5. Level Tester Import

改 JSON 后再点 **Import** 会先清预览根下全部旧物件再刷，**不必**先切走再重进场景。若仍叠图，先 **Clear Preview** 再 Import。

---

## 6. 代码／内容索引

| 层 | 文件 |
|----|------|
| Core | `ControlCoreBoard`／`ControlCoreService`／`HousingAssignmentBoard`／`SettlementAuthorityBoard`／`WorkAreaDefinition` |
| Host | `HostControlCoreQuery`／`HostControlCoreAssault`／`HostHousingAreaSelection`／`HostNpcContextMenu`／`HostDemoTileMap`／`HostFormalHud` |
| 内容 | `WorkAreas/work_areas.json`、`Maps/ch01_reference_map.json`、`SCHEMA.md` |
| 编辑器 | WorkAreaEditor（占领字段）、MapEditor（zoneHousing／controlCore） |

---

## 7. 手操验收（短）

1. Level Tester Play；左键凡人住房区 → 见归属（未占领不可改）。  
2. 选中己方 → 右键主管府 → 攻击 → 靠近见耐久下降。  
3. 打穿后站满约 10 秒 → 占领；再改住房归属；右栏课表可点切换活动。  
4. MapEditor 改府位置 → 存 JSON → Import → 场景只有新位置（无叠旧）。

**签收：2026-08-15 制作人确认通过。**
