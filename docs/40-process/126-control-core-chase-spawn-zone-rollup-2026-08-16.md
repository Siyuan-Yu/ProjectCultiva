# 126 · 主管府正式近战／追击固着／刷怪区表（2026-08-16）

> 状态：**代码已落地／编辑器已重发**｜日期：2026-08-16  
> 相对提交：`a7a7881` 之后 → 本轮 `main`  
> 飞书：https://my.feishu.cn/docx/UWOvdUOlao8e3ExK2ywc84fUnnh  
> 上一轮：[125 战斗／斗技／体魄](125-combat-arts-physique-acceptance-rollup-2026-08-16.md)

---

## 1. 一句话

主管府突击对齐正式近战公式；互砍追击直到死或玩家打断；MapEditor 通用刷怪区＋`spawnTable`（不做独立敌人编辑器／暂不做钉点）。

---

## 2. 交付对照

| 主题 | 做什么 | 入口／文件 |
|------|--------|------------|
| **主管府正式伤** | 删 `TestMeleeDamagePerHit`；一击＝攻−建筑防/2；间隔同互砍；挥砍特效 | `ControlCoreService.ApplyStrikeFromAttacker`／`HostControlCoreAssault` |
| **近战追击固着** | 下令攻击后持续追砍；到位不再 `HoldStandby→Stop→脱战`；右键地面／S 打断 | `HostNpcMeleeAssault`／`HostMoveController` |
| **刷怪表** | 新 `type=spawnTable`（权重／数量区间，引用角色定义） | `SpawnTables/*.json`／`SpawnTableDefinition` |
| **刷怪区** | `placement.kind=spawnZone`；绑 `spawnTableId`＋`boundLocationId`；开局 `SpawnZoneApplier` | MapEditor「2 · 分区」／`SpawnZoneApplier` |
| **洞府残影迁移** | 名册／`residentNpc` 去掉；洞府 map 放刷怪区＋表 | `ch01_cave_map.json`／`cave_shade_spawn_table.json` |
| **敌对姿态（既有）** | 人物 `tags` 含 `hostile`＝敌对；非势力关系表 | `HostNpcInteraction`／残影 JSON |
| **编辑器重发** | `publish.ps1` 全量打包 Apps | `启动-MapEditor.cmd` 等 |

---

## 3. 操作流（制作人）

```text
【追击】选中己方 → 右键 NPC「攻击」→ 拉开距离应跟砍 → 右键地面／S 才停
【主管府】选中己方 → 右键府「攻击」→ 靠近按属性拆耐久 → 破门站满占领
【刷怪区】MapEditor →「2 · 分区」→「刷怪区」→ 填 boundLocationId／spawnTableId → 存 JSON
【残影】进洞府内室应能看见残影（开局由 SpawnZoneApplier 生成，地表不可见）
【敌对】残影 tags 有 hostile → 免确认攻击；主管等无标 → 攻击需确认
```

刷怪表可用 MapEditor「编辑／新建刷怪表…」GUI；JSON 仍为真源。

---

## 4. 规则摘要

| 规则 | 现行 |
|------|------|
| 敌对 | 内容标签 `hostile`（非势力外交） |
| 府耐久伤 | max(1, 攻击 − 建筑 Defense/2)，近战间隔 |
| 交战结束 | 目标死／己方倒／玩家移动或 Stop |
| 刷怪 | 区＋表；引用 Characters；不做敌人分叉 schema |
| 钉点 | **本轮不做**；命名 NPC 仍 Scenario／名册 |

**明确未做（至 126）：** MapEditor 命名钉点；敌人主动寻仇；势力驱动敌对；进洞才刷（现开局即 ApplyAll）。  
**127 已补：** 刷怪表 GUI；击败不瞬移；进出洞相机／表现坐标。

---

## 5. 下一步建议

1. 见 [127](127-defeat-teleport-cave-camera-spawn-table-gui-2026-08-16.md)  
2. 洞府清残影任务／遭遇装配  
3. WorldGraph backlog（[113](113-world-graph-local-map-architecture-revision-v0.1.md)）
