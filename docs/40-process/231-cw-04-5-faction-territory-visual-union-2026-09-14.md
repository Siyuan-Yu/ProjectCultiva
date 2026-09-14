# CW-04.5：Faction Territory Visual Union

> 状态：Implementation Completed / Producer Acceptance Pending
> 日期：2026-09-14
> 前置：CW-04 Producer Accepted / Sealed
> 范围：WorldMap 正常产品领土表现与文档状态收口；不进入 CW-05

## 为什么做

CW-04 已建立按 `TerritoryClaim` 取得历史解析的唯一 Actual Managing Site，并由 WorldMap 直接绘制精确 world-space 行政控制。原产品 overlay 仍按 Site 输出边界，因此同一 Faction 的相邻 Site 之间看起来像政治分界。CW-04.5 只增加派系层的只读视觉投影，使同势力接触的实际控制在产品视图中连续。

## 权威与实现

- `WorldSiteActualControlOverlayBuilder.Build(world)` 保持 per-Site 语义：Pieces、Bounds、BoundarySegments 与 SiteId 均不变，继续服务 Site inspector、LevelTester CW-04 diagnostics 和后续管理诊断。
- 新增 `BuildFactionUnion(world)`。它使用有效 Claim 边形成同一 exact partition，并对每个 partition cell 调用 `WorldSiteAdministrativeControlResolver.TryResolve`。FactionId 只由解析得到的 Actual Managing Site 的 `OwnerFactionId` 映射。
- 同一 Faction 的相邻 partition cell 合并为矩形 runs，并在连续 Y band 上 coalesce；不同 Faction 或一侧没有 Actual Manager 时保留边界。断开的同势力范围保留为同一 overlay 内的多个独立 pieces，不使用 bounding box 补齐空地。
- `HostHexWorldRenderer.DrawActualControlOverlay` 的正常“显示势力范围”改读 faction union，fill 继续使用 `StrategicFactionCatalog.MapTint` 及原透明度；同势力 Site 内部边界不再绘制。
- Site inspector 和 `HostLevelTesterCheatPanel` 继续调用 per-Site `Build(world)`，可检查同势力范围内实际由 Site A 还是 Site B 管理。未增加复杂 debug UI。

该投影不读取 `HexCell.ControlFactionId`、`TerritoryRegion.Hexes`、理论 Core overlap 或旗半径，不写入 Domain，也不进入 Snapshot。SiteId、Core/Flag identity、Site Owner、Claim identity、AcquiredOrder 和实际 manager 解析规则均未修改。

## 修改文件

- `Assets/Scripts/Core/World/Strategic/WorldSiteActualControlOverlayBuilder.cs`
- `Assets/Scripts/Unity/Host/HostHexWorldRenderer.cs`
- `docs/20-systems/26-territory-management.md`
- `docs/40-process/223`～`229` CW-04 记录
- `docs/40-process/41-roadmap.md`
- `docs/40-process/42-devlog.md`
- `docs/00-project/00-overview.md`
- `docs/00-project/04-reading-guide.md`
- 本记录

## 验证

- 现成非 Unity offline compile：Core、Data、Unity Host 与其余既有程序集编译通过。
- 定向静态搜索确认产品 renderer 消费 `BuildFactionUnion`；Site inspector 与 LevelTester diagnostics 继续消费 `Build(world)`；union 构建器只使用 Claim partition、Actual Administrative resolver 与 Site Owner。
- `git diff --check`。
- 未启动 Unity，未运行 EditMode、PlayMode、Test Runner、batchmode 或 Bake；产品视觉仍待制作人人工验收。

## 明确延期，不阻塞 CW-05

- FormalArmy、BattleOffer、Hex support 深层清理。
- NPC Squad macro movement 脱离 FormalArmyWorldMotion。
- Level 2／Level 3 正式范围与升级数值。
- Encounter relation intervention 参数调优。
- 飞舟。
- 建筑战争与 Site takeover，排在 CW-05 之后。

## CW-05 只记录原则，不在本轮实现

无人管理不等于世界时间停止。建筑 identity、WorldPosition、HP／destroyed、inventory、既有 crop state、corpse/object lifecycle 及其他自然过程继续由 World/Core time authority 推进；未来没有实际行政 manager 时暂停新 construction、production work order、work assignment、administrative upgrade、public inventory auto-consumption、organized labor 与其他 `RequiresAdministration` 行为。Host presentation 是否存在不能成为自然过程推进条件。当前 Farm passive growth 若仍依赖 `PresentationDeltaTime`，列为 CW-05 audit item。

## 人工验收入口

在 LevelTester 打开 WorldMap 并保持“显示势力范围”：检查同势力相邻 Site 的 fill 连续且无内部政治边界；不同势力和无控制区边界仍可见；两个不接触的同势力范围没有被填成 bounding box。随后使用现有 Site inspect／CW-04 diagnostics 检查相邻两侧仍解析到各自 SiteId、Claim 与 AcquiredOrder。
