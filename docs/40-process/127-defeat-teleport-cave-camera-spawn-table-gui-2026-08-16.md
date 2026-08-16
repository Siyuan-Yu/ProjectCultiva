# 127 · 击败瞬移／进出洞相机／刷怪表 GUI（2026-08-16）

> 状态：**已落地／MapEditor 已重发**｜日期：2026-08-16  
> 相对提交：`976215c` 之后 → 本轮 `main`  
> 飞书：https://my.feishu.cn/docx/K2qod9vD2oqtCVxqCQecAvxxnzh  
> 上一轮：[126 府近战／追击／刷怪区](126-control-core-chase-spawn-zone-rollup-2026-08-16.md)

---

## 1. 一句话

修击败后整图 Rebuild 导致瞬移；进出洞镜头对准己方并清旧图表现坐标；MapEditor 内 GUI 编辑／新建刷怪表（不必手改 JSON）。

---

## 2. 交付对照

| 主题 | 做什么 | 入口／文件 |
|------|--------|------------|
| **击败不瞬移** | 击败后只 `Despawn` 尸体，不再 `ReloadLocalMapPresentation` | `HostNpcMeleeAssault`／`HostCombatSkillBar`／`EntityViewSpawner.Despawn` |
| **走动记坐标** | 到位写 `PresentationOverride`，Rebuild 可保留站位 | `HostMoveController.SyncLocation` |
| **进出洞** | 换地点清 override；镜头优先对准可见己方 | `ExplorationService.SetEntityLocation`／`PlayableHostBootstrap.TryFrameCameraOnParty` |
| **刷怪表 GUI** | 「编辑／新建刷怪表…」选角色／权重／数量并保存 | `SpawnTableEditWindow`／MapEditor 属性栏 |
| **状态继承说明** | 生命／背包等 Core 进出洞本就同一世界；表现坐标才是本轮修的缺口 | 见 §4 |

---

## 3. 操作流（制作人）

```text
【瞬移】洞内／地表互砍击败 → 己方应停在交战位置，不弹回地点中心
【进出洞】进／出后镜头对着小队；落点在洞口／内室落点附近
【刷怪表】MapEditor 选刷怪区 →「编辑／新建刷怪表…」→ 选角色保存 → spawnTableId 自动填上
```

---

## 4. 规则摘要

| 规则 | 现行 |
|------|------|
| 击败刷新 | 只卸表现，不整图 Rebuild |
| 进出洞 Core 状态 | 同一 SimulationWorld，不靠存档继承 |
| 进出洞表现 | 换地点清旧 override；镜头跟己方 |
| 刷怪表制作 | GUI 优先；JSON 仍为真源文件 |

**明确未做：** 进洞才刷怪；命名钉点；清残影任务内容；WorldGraph。

---

## 5. 下一步建议

1. 手操确认瞬移／进出洞／刷怪表 GUI  
2. 洞府清残影任务／遭遇装配  
3. 可选：进 LocalMap 时再刷；敌人主动寻仇  
4. WorldGraph backlog（[113](113-world-graph-local-map-architecture-revision-v0.1.md)）
