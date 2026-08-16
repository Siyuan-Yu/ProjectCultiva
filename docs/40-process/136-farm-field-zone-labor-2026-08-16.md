# 136 · 田区自动农作（整片区 + 格上作物）

> 状态：已落地｜日期：2026-08-16（2026-08-17 去掉绿草幽灵工区）  
> 相关：只读况栏 [135](135-world-object-inspect-and-tree-chop-2026-08-16.md)  
> 收束：[137](137-skill-mastery-farm-veil-chop-rollup-2026-08-17.md)

## 拍板

- 药田／农田是**一整片区**（同 `boundLocationId` 下全部耕种格），不是「点一个热点干一次」。
- 作物状态在**每一格**：空闲 → 成长中 → 成熟 →（可损坏）。
- 玩家交互下令后，角色在区内**自动选格、走格、干活**（可多人分格，互不抢同一格）。
- NPC：日程 `Labor` 且工区 tags 含 `farm`／`herb`／`grain`、该 location 有田格时，自动接入**同一套**走格农作；离开 Labor／换工区则停。

## 操作

1. 选中己方 → 「交互」→ 左键药田／农田任意格（或工区热点）  
2. 自动循环：成熟优先收获 → 损坏清理 → 空闲播种 → 成长照料  
3. **F1／停止**或右键移动：中断农作（仅玩家下令侧）  
4. NPC：到农田／药田工区进入 `WorkAction` 后自动走格；头顶仍可显示「工作中／移动中」

## 规则（Host 竖切）

| 格状态 | 工作 | 结果 |
|--------|------|------|
| 空闲 | 播种 | → 成长中 0% |
| 成长中 | 照料 | 进度 +34%；满则成熟 |
| 成熟 | 收获 | 空闲 + 灵药／粮食 ×1 |
| 损坏 | 清理 | → 空闲 |

- 另有缓慢自然生长（约每秒 +1.2%），无需一直照料也能熟。  
- 药田产 `base:resource_spirit_herb`；农田产 `base:resource_grain`。  
- 玩家收获进 `world.Inventory`；NPC 收获进据点 `Settlement` 库存。  
- **播种不需要种子**；作物由田格 kind 决定（药田→灵药，农田→粮食）。区级指定水稻／玉米等以后再做。  
- 格颜色随状态变化（空／青／黄／褐）。
- **检视／命中**：农田／药田工区不再挂在旧绿草大色带上；左键「工区·农田／药田」只在点中耕种格时出现。地点 `presentation` 对齐各田块中心平均。

## 代码

| 文件 | 作用 |
|------|------|
| `HostFarmFieldRegistry`／`HostFarmFieldRules` | 按地点登记田格、选格优先级；`IsFarmTaggedWorkArea` |
| `HostFarmFieldLabor` | 走格／干活；`SyncNpcScheduleFarmers` 接日程 Labor |
| `HostMapPlotCell` | 作物阶段＋着色 |
| `HostWorkTargetMode.IssueWorkAtSpot` | 有田区则走农作，否则旧劳动循环 |

## 未做

- 损坏事件／灾害  
- Snapshot 持久化作物格状态（Rebuild 地图会重置）  
- 区内禁止走不可通行格的细寻路（当前直达格心）  
- NPC 收获按 location 精确归属据点（当前取首个 Settlement）
