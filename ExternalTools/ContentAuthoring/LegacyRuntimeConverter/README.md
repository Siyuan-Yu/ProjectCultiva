# LegacyRuntimeConverter

仅用于两类有明确无损规则的输入：

- 把旧 `formalArmy` Content 转为 `npcSquad`，并把 `initialFormalArmyIds` 转为
  `initialNpcSquadIds`；
- 在 Snapshot 的全部 current authority 已完整、只剩 FormalArmy 待迁时，把旧 Army
  转为 current `Squad` / `SquadWorldMotion`。

输入始终只读；输出必须是不同且尚不存在的文件。

```powershell
dotnet run --project .\LegacyRuntimeConverter\LegacyRuntimeConverter.csproj -- `
  content --input C:\temp\legacy-content.json --output C:\temp\current-content.json

dotnet run --project .\LegacyRuntimeConverter\LegacyRuntimeConverter.csproj -- `
  snapshot --input C:\temp\legacy-save.json --output C:\temp\current-save.json `
  --surface-id base:main_surface --movement-scale 1
```

只有 Hex 坐标而没有精确 Surface 位置时，必须同时提供 `--surface-id` 和正数
`--movement-scale`。转换使用历史 Pointy-top Odd-R 中心公式：
`x = scale * sqrt(3) * (q + 0.5 * (r & 1))`，`y = scale * 1.5 * r`。

`type=hexWorld`、`openingHexWorldId` 及其他需要重建地图语义的旧 Content **不会转换**。
工具会在写输出前明确失败；请使用现有 WorldComposer/SurfaceAuthoring Legacy migration
路径，没有迁移样例时不得猜测，也不得声称已生成 `outdoorSurface`。

Snapshot 转换前后都会验证：每个 entity 的 current `hasEntityLocation` 字段、
exact-Surface `characterWorldPresences`、current `playerPartyTravel`、`factionFlags`、
`runtimeWorldSites`、format 3 `territoryClaims`、current `squads` /
`squadWorldMotions`，以及 format 4 CharacterEncounter 的完整 Origin/Return/Tactical
空间字段。Residual、TerritoryRegion controller、RetreatingArmy、PendingEngagement、
ArmyStack、旧 Squad command/owner 或缺 authority 均会失败，不生成部分输出。

最小可执行验证：

```powershell
dotnet run --project .\LegacyRuntimeConverter\LegacyRuntimeConverter.csproj -- --self-test
```

它覆盖 formalArmy Content 成功、hexWorld Content 失败且无输出、完整 hybrid Snapshot
成功、缺 current authority Snapshot 失败且无输出，以及拒绝覆盖。
