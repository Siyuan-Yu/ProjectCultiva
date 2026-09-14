# CW-04：实际行政控制历史与非抢占扩张

> 状态：CW-04 Producer Accepted / Sealed
> 日期：2026-09-14  
> 决策真源：[ADR-0032](43-decisions/ADR-0032-sitecore-administrative-and-construction-range.md)

## 前置状态

CW-U4.2 已由制作人人工验收通过。CW-04 只收口 SiteCore 行政控制历史、重叠解析、升级扩张和战略投影；没有恢复玩家 FormalArmy 产品入口，也没有开始 CW-05。

## 实现

- 新增 `TerritoryClaimState/Board/Service`：保存 ClaimId、SiteId、SurfaceId、AcquiredOrder、中心和范围。Claim 不保存 Faction；当前政治 Owner 始终来自 `WorldSite.OwnerFactionId`。
- `WorldSiteAdministrativeControlResolver` 以精确 Surface 世界坐标筛选“Claim 包含且仍位于该 Site 当前理论范围”的候选，最小 AcquiredOrder 获胜；稳定 ClaimId/SiteId 只作异常平局顺序。
- 新建 Site 在 Site 与核心旗身份均注册后事务性创建初始 Claim；失败会移除 Site/Flag，外层 Construction 继续回滚材料。
- 理论范围允许重叠。新核心的中心只要不落入任何当前实际行政控制，并通过 Surface/战略地面与物理占地检查即可建立；不再以 Hex Controller 或 neutral gain 决定合法性。
- `WorldSiteCoreLevelService.TryUpgrade` 是唯一升级入口：只允许 Content 已配置的更高且不缩小等级，先追加较晚的扩张 Claim，再更新等级并重建投影。当前 Content 只有 Level 1，没有虚构 Level 2。
- 核心失效不删除 Claim；恢复沿用原 Claim 优先级。Site 易主只修改 Owner 并重建投影，不重写 Claim。
- Hex 格心逐点查询实际行政管理者；`HexCell.ControlFactionId` 与 TerritoryRegion.Hexes 是可重建战略投影。旧非 Site FactionFlag 只在没有 Site 管理者时 fallback。
- CharacterEncounter、Continuous Outdoor 当前 Site、Party 成员脱队位置、Interior 返回和相关开发入口改读实际行政 resolver。WorldSiteCoreCoverageResolver 只保留理论覆盖职责。
- WorldMap Site inspect 显示理论候选、实际 Site/Owner/Claim/Order、核心状态/等级、Claim 历史与有效 Hex 数。

## Persistence

- 新 Snapshot authority：`hasTerritoryClaimSnapshotAuthority + territoryClaims[]`，逐项带 `formatVersion=3`，保存解析后的 world-space Claim（包括当前失效 Site 的历史）。V1/V2 只对已知开发期 Level 1 baseline/initial 尺寸做受控迁移；无法证明历史 level/kind 的 Claim 返回 `SnapshotInvalid`。
- 新格式严格验证 Claim/Order 唯一、Site/Surface/中心/范围、单 Site 范围历史不缩小，以及最新 Claim 与当前 CoreLevel 范围一致；权威标记存在但数组缺失／非法不会静默补基线。
- 旧 v6 没有 Claim authority 时，只在 Content Site 核心元数据完成绑定后的显式 bootstrap/restore 阶段，按稳定 `ControlEstablishedOrder` 一次性建立 baseline；下一次保存写入正式 authority。
- Hex 控制和 TerritoryRegion 投影不作为 Claim 历史保存。

## 验证边界

当轮仅运行现成非 Unity offline compile（Core 461 / Data 78 / Unity Host 145 sources）：0 error、8 个既有 warning；定向静态查询与 `git diff --check` 通过。当时未运行 Unity、Test Runner、EditMode、PlayMode、batchmode、Bake 或新增自动测试。制作人随后于 2026-09-14 完成人工验收，CW-04 现为 **Producer Accepted / Sealed**。
