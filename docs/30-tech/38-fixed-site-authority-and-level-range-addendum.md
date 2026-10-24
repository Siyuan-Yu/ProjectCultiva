# 固定据点与五级议政厅范围：架构增补

> 2026-10-25；Producer Design Confirmed / Implementation Pending。决策以 [ADR-0055](../40-process/43-decisions/ADR-0055-fixed-council-hall-sites-and-five-level-control-range.md)为准；本页不改 [Freeze v0.2](33-architecture-core-rules-freeze-v0.2.md) 历史正文。

正常新游戏中，预设的每个 WorldSite 都由明确作者数据给出唯一、不可拆的固定 SiteCore。占领取得同一 Site；议政厅升级只扩张该 Site 的理论行政范围，实际控制继续由 WorldSite Owner、TerritoryClaim 和 Actual Administrative Control 决定。`SurfaceId + exact WorldPosition + Continuous Surface` 是道路、建筑、人物和核心的物理位置真源；等级覆盖不生成另一个地图，也不决定建筑密度。

现有 102 个旗帜生成 Site 是一次内容与运行入口迁移，**不是新增 102 个 Site**。迁移以原 Site ID 为 join key，保留名称、已有服务和角色引用；新的固定核心由作者蓝图显式声明，运行时不得再生成同 ID 旗 Site。旧存档的旗状态、动态玩家旗和 Claim 保留原文件，新布局以新的 BaseGame 内容版本隔离，正常读档给出不兼容与新开局提示，不用新游戏内容迁移直接覆盖。后续旧档导入需另立无损转换方案。任何 Snapshot schema、ID 语义或 Core／Data 边界改动均不在本增补中默许。

主 Surface 一级为 225×225 cells，二至五级的长宽为一级的 1.5、2、2.5、3 倍；精确数值见 ADR-0055。理论范围可重叠，实际管理权唯一且遵守既得 Claim。世界中心和道路可以重新制作，但蓝图建筑区不自动按行政范围等比例铺满；道路与据点位置调整必须同步核对世界地点、NPC、商店、任务、出生、路径和地形。现有 7600×3400 Surface 与 50×50 streaming chunks 保持同一连续世界边界，是否扩大世界另行决策。
