# 固定据点与五级议政厅范围：架构增补

> 2026-10-25 最新覆写：一级控制范围长宽由旧 225 改为 180 cells；二至五级为 270／360／450／540 cells。现有固定 Site 的实体围墙沿其当前等级范围边缘；此前 0.75 与下文旧绝对尺寸仅记录实施历史，见 [357](../40-process/357-golden-core-energy-and-town-footprints-2026-10-25.md)。

> 2026-10-25；Producer Design Confirmed / Implemented / Producer Acceptance Pending。实施见[345](../40-process/345-fixed-sites-and-town-levels-architecture-change-2026-10-25.md)，决策以 [ADR-0055](../40-process/43-decisions/ADR-0055-fixed-council-hall-sites-and-five-level-control-range.md)为准；本页不改 [Freeze v0.2](33-architecture-core-rules-freeze-v0.2.md) 历史正文。

正常新游戏中，预设的每个 WorldSite 都由明确作者数据给出唯一、不可拆的固定 SiteCore。占领取得同一 Site；议政厅升级只扩张该 Site 的理论行政范围，实际控制继续由 WorldSite Owner、TerritoryClaim 和 Actual Administrative Control 决定。`SurfaceId + exact WorldPosition + Continuous Surface` 是道路、建筑、人物和核心的物理位置真源；等级覆盖不生成另一个地图，也不决定建筑密度。

现有 102 个旗帜生成 Site 是一次内容与运行入口迁移，**不是新增 102 个 Site**。迁移以原 Site ID 为 join key，保留名称、已有服务和角色引用；新的固定核心由作者蓝图显式声明，运行时不得再生成同 ID 旗 Site。旧存档的旗状态、动态玩家旗和 Claim 保留原文件，新布局以新的 BaseGame 内容版本隔离，正常读档给出不兼容与新开局提示，不用新游戏内容迁移直接覆盖。后续旧档导入需另立无损转换方案。任何 Snapshot schema、ID 语义或 Core／Data 边界改动均不在本增补中默许。

主 Surface 当前一级为 180×180 cells；二至五级为 270／360／450／540 cells；历史决策及最新覆写见 ADR-0055 和 357。理论范围可重叠，实际管理权唯一且遵守既得 Claim。现有固定城镇外墙按当前等级范围边缘放置，内部建筑保留精确坐标、避免机械拉伸；道路与据点位置调整须同步核对世界地点、NPC、商店、任务、出生、路径和地形。下文旧 Surface 尺寸属于历史设计，本轮不改连续世界边界与 50×50 streaming chunks。
