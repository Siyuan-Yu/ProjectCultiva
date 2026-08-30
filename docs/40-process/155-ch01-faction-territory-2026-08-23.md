# 155 · Ch01 势力领土配置（2026-08-23）

> **状态：** 已实现（`Ch01ScenarioStrategicSetup.ApplyCh01TerritoryOwners`）  
> **用途：** F8 外交验收、大地图节点染色、未宣战攻击门槛验证  
> **⚠️ 2026-08-24：** 本文 Node 级领土与单一 `base:faction_bandits` 为 **Legacy Prototype**。正式 Hex Territory / TerritoryRegion / 每寨独立 Bandit Faction 见 **[2J](../20-systems/2J-hex-territory-worldsites-and-dynamic-bandits.md)**。

---

## 势力总览（7 个战略 Faction + 山匪）

| 显示名 | FactionId | 节点数 | 说明 |
|--------|-----------|--------|------|
| **压迫宗门** | `base:sect_huangcun_labor` | 3 | 宗主领土 |
| **主角团** | `base:faction_player` | 0 | 压迫宗门附庸，无领土 |
| **沧澜渔盟** | `base:faction_fisher_village` | 3 | 海角三角 |
| **南堰庄盟** | `base:faction_nan_yan` | 2 | 南村一带 |
| **朔风堡** | `base:faction_shuofeng` | 2 | 北村一带 |
| **东林海会** | `base:faction_donglin` | 3 | 东林一带 |
| **西津渡帮** | `base:faction_xijin` | 2 | 西渡一带 |
| **山匪** | `base:faction_bandits` | 0 | 无领土游荡军；开局与主角团／压迫宗门交战 |

---

## 节点归属表

### 压迫宗门（3）

| 节点 | Id |
|------|-----|
| 青石荒村 | `base:node_huangcun` |
| 青云路 | `base:node_qingyun_lu` |
| 灵地 | `base:node_lingdi` |

### 南堰庄盟（2）— 南村附近

| 节点 | Id |
|------|-----|
| 南村 | `base:node_cunzhuang_nan` |
| 庄院 | `base:node_zhuangyuan` |

### 沧澜渔盟（3）— 海角附近

| 节点 | Id |
|------|-----|
| 海角 | `base:node_haijiao` |
| 水寨 | `base:node_shuizhai` |
| 渔村 | `base:node_yucun` |

### 朔风堡（2）— 北村附近

| 节点 | Id |
|------|-----|
| 北村 | `base:node_cunzhuang_bei` |
| 山口 | `base:node_shankou` |

### 东林海会（3）— 东林附近

| 节点 | Id |
|------|-----|
| 东林 | `base:node_shulin_dong` |
| 山神庙 | `base:node_miao` |
| 古道驿 | `base:node_gudao` |

### 西津渡帮（2）— 西渡附近

| 节点 | Id |
|------|-----|
| 西渡 | `base:node_dukou_xi` |
| 药田谷 | `base:node_yaotian` |

其余 Ch01 节点（关隘、矿山、林间、渡口、青石镇等）为 **无归属**，便于后续占点／剧情。

---

## 附庸

- **主角团** → 附庸于 **压迫宗门**（开局 `VassalageBoard` 已绑定）

## 开局战争（Prototype）

- 主角团 ↔ 山匪
- 压迫宗门 ↔ 山匪  
- 与区域五势力 **默认未宣战**（可在 F8 手动 Declare War 验收）

---

## 验证方式

1. **F8** → `[FACTIONS]` 看各势力 `Nodes: N`  
2. **F8** → `[NODES]` 看每节点 `Owner`  
3. **大地图** 左键节点 → 详情行 `Owner: …`  
4. **大地图** 节点框按势力色轻微染色  
5. EditMode：`Ch01ScenarioSetup_AssignsRegionalTerritoryOwners`
