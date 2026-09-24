using System;
using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Cross-reference check for chapter production packages (quest／event／flag／npc／location…).
    /// </summary>
    public sealed class ContentReferenceValidator
    {
        static readonly HashSet<string> DynamicOpportunityObjectKinds = new HashSet<string>(StringComparer.Ordinal)
        { "treeS", "treeM", "treeL", "ore", "cushion", "rock", "cave", "loot", "herbField", "rallyPoint" };
        public ValidationReport Validate(DefinitionRegistry registry)
        {
            var report = new ValidationReport();
            if (registry == null)
            {
                report.Add(ErrorCode.InvalidArgument, "DefinitionRegistry is null.");
                return report;
            }

            var locations = CollectLocationIds(registry);
            if (registry.OutdoorSurfaces.Count > 0 && registry.SpatialRules == null)
                report.Add(ErrorCode.MissingRequiredField, "Continuous world requires worldSpatialRules.");
            ValidateOutdoorSurfaceMetrics(registry, report);
            if (registry.SpatialRules != null)
                foreach (var pair in registry.Buildings)
                    if (pair.Value.CreatesWorldSite)
                        try { registry.SpatialRules.RequireLevel(pair.Value.InitialSiteLevel); }
                        catch (InvalidOperationException ex) { report.Add(ErrorCode.InvalidArgument, ex.Message, pair.Key.ToString()); }
            var producedFlags = new HashSet<string>(StringComparer.Ordinal);
            var consumedFlags = new HashSet<string>(StringComparer.Ordinal);

            ValidateScenarios(registry, locations, report);
            ValidateNpcSquads(registry, report);
            ValidateStrategicFactions(registry, report);
            ValidateWorldRegions(registry, locations, report);
            ValidateLocalPlaceSets(registry, locations, report);
            ValidateItems(registry, report);
            ValidateBuildings(registry, report);
            ValidateSpawnTables(registry, report);
            ValidateWorldOpportunities(registry, locations, producedFlags, consumedFlags, report);
            ValidateMapSpawnZones(registry, locations, report);
            ValidateOutdoorSurfaceSitePlaceIdentities(registry, report);
            ValidateOutdoorControlCores(registry, report);
            ValidateOutdoorStorageRooms(registry, report);
            ValidateFactionFlagSiteCores(registry, report);
            ValidateWorldSiteEconomies(registry, report);
            ValidateQuests(registry, locations, producedFlags, consumedFlags, report);
            ValidateContentEvents(registry, locations, producedFlags, consumedFlags, report);
            ValidateChapters(registry, locations, producedFlags, consumedFlags, report);
            ValidateFlagConsumers(producedFlags, consumedFlags, report);

            return report;
        }

        static void ValidateOutdoorSurfaceMetrics(DefinitionRegistry registry, ValidationReport report)
        {
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface == null || string.IsNullOrWhiteSpace(surface.SurfaceId) ||
                    !IsFinite(surface.OriginWorldX) || !IsFinite(surface.OriginWorldY) ||
                    !(surface.MovementScale > 0f) || !IsFinite(surface.MovementScale) ||
                    !(surface.CellSize > 0f) || !IsFinite(surface.CellSize) ||
                    !(surface.ChunkWidth > 0f) || !IsFinite(surface.ChunkWidth) ||
                    !(surface.ChunkHeight > 0f) || !IsFinite(surface.ChunkHeight) ||
                    surface.Chunks == null || surface.Chunks.Count == 0)
                {
                    report.Add(ErrorCode.InvalidArgument,
                        "Outdoor Surface requires identity, finite metric, and authored chunk coverage.",
                        pair.Key.ToString());
                    continue;
                }
                if (registry.TryGetOutdoorSurfaceGeography(surface.SurfaceId, out var geography) &&
                    geography?.Navigation != null &&
                    Math.Abs(geography.Navigation.CellSize - surface.CellSize) > .000001f)
                    report.Add(ErrorCode.InvalidArgument,
                        "Outdoor Surface and geography cellSize must match.", surface.SurfaceId);
            }
        }

        static void ValidateOutdoorControlCores(DefinitionRegistry registry, ValidationReport report)
        {
            var knownSites = CollectAuthoredSiteIds(registry);
            var controlCoreLocationCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in registry.WorkAreas)
                if (pair.Value != null && pair.Value.IsControlCore &&
                    !string.IsNullOrWhiteSpace(pair.Value.LocationId))
                {
                    controlCoreLocationCounts.TryGetValue(pair.Value.LocationId, out var count);
                    controlCoreLocationCounts[pair.Value.LocationId] = count + 1;
                }
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface?.SitePlacements == null || surface.AcceptanceOnly) continue;
                var coreSites = new HashSet<string>(StringComparer.Ordinal);
                var chunks = new HashSet<XianXia.Core.World.Surface.SurfaceChunkCoord>();
                if (surface.Chunks != null)
                    for (var c = 0; c < surface.Chunks.Count; c++)
                        if (surface.Chunks[c] != null) chunks.Add(surface.Chunks[c].Coord);
                for (var i = 0; i < surface.SitePlacements.Count; i++)
                {
                    var placement = surface.SitePlacements[i];
                    if (placement == null ||
                        !string.Equals(placement.Kind, "controlCore", StringComparison.OrdinalIgnoreCase)) continue;
                    var context = surface.SurfaceId + ".controlCore[" + i + "]";
                    if (string.IsNullOrWhiteSpace(placement.StableId))
                        report.Add(ErrorCode.MissingRequiredField,
                            "Outdoor controlCore.stableId required.", context);
                    if (string.IsNullOrWhiteSpace(placement.SiteId))
                        report.Add(ErrorCode.MissingRequiredField,
                            "Outdoor controlCore.siteId required.", context);
                    if (string.IsNullOrWhiteSpace(placement.BoundLocationId))
                        report.Add(ErrorCode.MissingRequiredField,
                            "Outdoor controlCore.boundLocationId required.", context);
                    else if (!controlCoreLocationCounts.TryGetValue(placement.BoundLocationId, out var coreCount) ||
                             coreCount != 1)
                        report.Add(ErrorCode.NotFound,
                            "Outdoor controlCore.boundLocationId must reference exactly one ControlCore WorkArea.",
                            context + ":" + placement.BoundLocationId);
                    if (!coreSites.Add(placement.SiteId ?? string.Empty))
                        report.Add(ErrorCode.DuplicateDefinitionId,
                            "Outdoor WorldSite has more than one authored controlCore.", context);
                    if (!knownSites.Contains(placement.SiteId ?? string.Empty))
                        report.Add(ErrorCode.NotFound,
                            "Outdoor controlCore references an unknown WorldSite.", context);
                    if (registry.SpatialRules == null)
                        report.Add(ErrorCode.MissingRequiredField,
                            "Outdoor controlCore requires worldSpatialRules.", context);
                    else
                        try { registry.SpatialRules.RequireLevel(1); }
                        catch (InvalidOperationException ex)
                        { report.Add(ErrorCode.InvalidArgument, ex.Message, context); }
                    var centerX = placement.WorldX + placement.WorldWidth * .5f;
                    var centerY = placement.WorldY + placement.WorldHeight * .5f;
                    var centerIsAuthored = false;
                    if (surface.ChunkWidth > 0f && surface.ChunkHeight > 0f)
                    {
                        var centerChunk = new XianXia.Core.World.Surface.SurfaceChunkCoord(
                            (int)Math.Floor((centerX - surface.OriginWorldX) / surface.ChunkWidth),
                            (int)Math.Floor((centerY - surface.OriginWorldY) / surface.ChunkHeight));
                        centerIsAuthored = chunks.Contains(centerChunk);
                    }
                    if (!centerIsAuthored)
                        report.Add(ErrorCode.InvalidArgument,
                            "Outdoor controlCore center is outside authored Surface chunks.", context);
                }
            }
        }

        static void ValidateOutdoorStorageRooms(DefinitionRegistry registry, ValidationReport report)
        {
            var knownSites = CollectAuthoredSiteIds(registry);
            var storageSites = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface?.SitePlacements == null || surface.AcceptanceOnly) continue;
                for (var i = 0; i < surface.SitePlacements.Count; i++)
                {
                    var placement = surface.SitePlacements[i];
                    if (placement == null || !string.Equals(placement.Kind, "storageRoom", StringComparison.Ordinal))
                        continue;
                    var context = surface.SurfaceId + ".storageRoom[" + i + "]";
                    if (string.IsNullOrWhiteSpace(placement.SiteId) || !knownSites.Contains(placement.SiteId))
                        report.Add(ErrorCode.NotFound, "Outdoor storageRoom references an unknown WorldSite.", context);
                    else if (!storageSites.Add(placement.SiteId))
                        report.Add(ErrorCode.DuplicateDefinitionId,
                            "Outdoor WorldSite has more than one authored storageRoom.", context);
                    if (placement.SourceCellsW != 3 || placement.SourceCellsH != 3 || !placement.BlocksMovement)
                        report.Add(ErrorCode.InvalidArgument,
                            "Outdoor storageRoom must be 3x3 and block movement.", context);
                    if (placement.WorldWidth <= 0f || placement.WorldHeight <= 0f ||
                        surface.CellSize <= 0f || surface.ChunkWidth <= 0f || surface.ChunkHeight <= 0f)
                    {
                        report.Add(ErrorCode.InvalidArgument,
                            "Outdoor storageRoom requires positive Surface metric and physical bounds.", context);
                        continue;
                    }
                    var centerX = placement.WorldX + placement.WorldWidth * .5f;
                    var centerY = placement.WorldY + placement.WorldHeight * .5f;
                    var materializationChunk = new XianXia.Core.World.Surface.SurfaceChunkCoord(
                        (int)Math.Floor((centerX - surface.OriginWorldX) / surface.ChunkWidth),
                        (int)Math.Floor((centerY - surface.OriginWorldY) / surface.ChunkHeight));
                    if (placement.ChunkX != materializationChunk.X || placement.ChunkY != materializationChunk.Y)
                        report.Add(ErrorCode.InvalidArgument,
                            "Outdoor storageRoom declared chunk must own its physical center for SingleCentered materialization.",
                            context);
                    if (!OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, centerX, centerY) ||
                        !OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface,
                            placement.WorldX + placement.WorldWidth - surface.CellSize * .001f,
                            placement.WorldY + placement.WorldHeight - surface.CellSize * .001f) ||
                        !OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface,
                            placement.WorldX + surface.CellSize * .001f,
                            placement.WorldY + surface.CellSize * .001f))
                        report.Add(ErrorCode.InvalidArgument,
                            "Outdoor storageRoom physical footprint must remain within authored Surface coverage.", context);
                }
            }
        }

        static void ValidateFactionFlagSiteCores(DefinitionRegistry registry, ValidationReport report)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var surfacePair in registry.OutdoorSurfaces)
            {
                var surface = surfacePair.Value;
                if (surface?.FactionFlags == null || surface.AcceptanceOnly) continue;
                for (var i = 0; i < surface.FactionFlags.Count; i++)
                {
                    var flag = surface.FactionFlags[i];
                    if (flag == null) continue;
                    var context = surface.SurfaceId + ".factionFlags[" + i + "]";
                    if (!ids.Add(flag.FlagId ?? string.Empty))
                        report.Add(ErrorCode.DuplicateDefinitionId,
                            "FactionFlag flagId must be globally unique.", context);
                    if (!IsFinite(flag.WorldX) || !IsFinite(flag.WorldY))
                        report.Add(ErrorCode.InvalidArgument,
                            "Site-Core FactionFlag world position must be finite.", context);
                    RequireFaction(registry, flag.FactionId, context + ".factionId", report, false);
                    if (flag.CoreLevel < 1)
                        report.Add(ErrorCode.InvalidArgument,
                            "Site-Core FactionFlag coreLevel must be positive.", context);
                    else if (registry.SpatialRules == null)
                        report.Add(ErrorCode.MissingRequiredField,
                            "Site-Core FactionFlag requires worldSpatialRules.", context);
                    else
                        try { registry.SpatialRules.RequireLevel(flag.CoreLevel); }
                        catch (InvalidOperationException ex)
                        { report.Add(ErrorCode.InvalidArgument, ex.Message, context); }

                    if (!OutdoorSurfaceCoverageResolver.TryResolveAtWorldPosition(
                            registry, flag.WorldX, flag.WorldY, out var resolved) ||
                        resolved == null ||
                        !string.Equals(resolved.SurfaceId, surface.SurfaceId, StringComparison.Ordinal))
                        report.Add(ErrorCode.InvalidArgument,
                            "Site-Core FactionFlag precise point is outside its unique authored Surface.", context);
                }
            }
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static HashSet<string> CollectLocationIds(DefinitionRegistry registry)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in registry.WorldRegions)
                AddLocationIds(set, kv.Value?.Locations);
            foreach (var kv in registry.LocalPlaceSets)
                AddLocationIds(set, kv.Value?.Locations);
            foreach (var kv in registry.OutdoorSurfaces)
            {
                var placements = kv.Value?.SitePlacements;
                if (placements == null)
                    continue;
                foreach (var placement in placements)
                    if (!string.IsNullOrWhiteSpace(placement?.BoundLocationId))
                        set.Add(placement.BoundLocationId);
            }
            return set;
        }

        static void AddLocationIds(HashSet<string> set, System.Collections.Generic.List<WorldLocationEntry> locs)
        {
            if (locs == null)
                return;
            for (var i = 0; i < locs.Count; i++)
            {
                if (!string.IsNullOrEmpty(locs[i].Id))
                    set.Add(locs[i].Id);
            }
        }

        static void ValidateItems(DefinitionRegistry registry, ValidationReport report)
        {
            foreach (var kv in registry.Items)
            {
                var item = kv.Value;
                if (item == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(item.TeachesManualId))
                {
                    RequireDef(
                        registry,
                        item.TeachesManualId,
                        "cultivation",
                        item.Id + ".teachesManualId",
                        report);
                }

                if (!string.IsNullOrWhiteSpace(item.TeachesArtId))
                {
                    RequireDef(
                        registry,
                        item.TeachesArtId,
                        "combatArt",
                        item.Id + ".teachesArtId",
                        report);
                }
            }
        }

        static void ValidateSpawnTables(DefinitionRegistry registry, ValidationReport report)
        {
            foreach (var kv in registry.SpawnTables)
            {
                var table = kv.Value;
                if (table?.Entries == null)
                    continue;
                for (var i = 0; i < table.Entries.Count; i++)
                {
                    var e = table.Entries[i];
                    if (e == null)
                        continue;
                    RequireDef(
                        registry,
                        e.DefinitionId,
                        "character",
                        table.Id + ".entries[" + i + "].definitionId",
                        report);
                }
            }
        }

        void ValidateWorldOpportunities(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            var surfaces = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in registry.WorldOpportunityDirectors)
            {
                var d = pair.Value;
                var ctx = d.Id.ToString();
                if (string.IsNullOrWhiteSpace(d.SurfaceId) || !DefinitionId.TryParse(d.SurfaceId, out var surfaceId) ||
                    !registry.OutdoorSurfaces.ContainsKey(surfaceId))
                    report.Add(ErrorCode.NotFound, "worldOpportunityDirector.surfaceId missing.", ctx + ":" + d.SurfaceId);
                if (!surfaces.Add(d.SurfaceId ?? string.Empty))
                    report.Add(ErrorCode.DuplicateDefinitionId, "同一个 Surface 只能配置一个 worldOpportunityDirector。", d.SurfaceId);
                if (d.TargetActiveMin < 0 || d.TargetActiveMax < d.TargetActiveMin)
                    report.Add(ErrorCode.InvalidArgument, "worldOpportunityDirector target range invalid.", ctx);
            }

            foreach (var pair in registry.WorldOpportunities)
            {
                var o = pair.Value;
                var ctx = o.Id.ToString();
                if (string.IsNullOrWhiteSpace(o.SurfaceId) || !DefinitionId.TryParse(o.SurfaceId, out var surfaceId) ||
                    !registry.OutdoorSurfaces.ContainsKey(surfaceId))
                    report.Add(ErrorCode.NotFound, "worldOpportunity.surfaceId missing.", ctx + ":" + o.SurfaceId);
                if (o.Weight < 0 || o.MaxActive <= 0 || o.DurationDays < 1)
                    report.Add(ErrorCode.InvalidArgument, "worldOpportunity weight must be non-negative; maxActive/durationDays must be positive.", ctx);
                if (o.MinPlayerDistanceWorld < 0f || o.MaxPlayerDistanceWorld < o.MinPlayerDistanceWorld)
                    report.Add(ErrorCode.InvalidArgument, "worldOpportunity distance range invalid.", ctx);
                var npc = string.IsNullOrWhiteSpace(o.SpawnKind) || o.SpawnKind == "npc";
                var worldObject = o.SpawnKind == "worldObject";
                if (!npc && !worldObject)
                    report.Add(ErrorCode.InvalidArgument, "worldOpportunity.spawnKind must be npc or worldObject.", ctx);
                if (npc) RequireDef(registry, o.SpawnTableId, "spawnTable", ctx + ".spawnTableId", report);
                if (worldObject && (string.IsNullOrWhiteSpace(o.WorldObjectKind) ||
                    o.WorldObjectWorldWidth <= 0f || o.WorldObjectWorldHeight <= 0f))
                    report.Add(ErrorCode.InvalidArgument, "worldObject opportunity requires kind and positive width/height.", ctx);
                else if (worldObject && !DynamicOpportunityObjectKinds.Contains(o.WorldObjectKind))
                    report.Add(ErrorCode.InvalidArgument, "worldObjectKind is not an approved MapKindCatalog prop.", ctx + ":" + o.WorldObjectKind);
                if (string.Equals(o.DiscoveryMode, "hidden", StringComparison.OrdinalIgnoreCase))
                    report.Add(ErrorCode.InvalidArgument,
                        "discoveryMode 'hidden' is invalid; use hiddenUntilDiscovered.", ctx);
                else if (!string.Equals(o.DiscoveryMode, "worldVisible", StringComparison.Ordinal) &&
                         !string.Equals(o.DiscoveryMode, "publicNotice", StringComparison.Ordinal) &&
                         !string.Equals(o.DiscoveryMode, "hiddenUntilDiscovered", StringComparison.Ordinal))
                    report.Add(ErrorCode.InvalidArgument, "worldOpportunity.discoveryMode must be worldVisible, publicNotice, or hiddenUntilDiscovered.", ctx);
                if (npc && string.Equals(o.DiscoveryMode, "hiddenUntilDiscovered", StringComparison.Ordinal))
                    report.Add(ErrorCode.InvalidArgument, "V1 hiddenUntilDiscovered 仅支持动态 WorldObject。", ctx);
                if (worldObject && string.Equals(o.DiscoveryMode, "hiddenUntilDiscovered", StringComparison.Ordinal) &&
                    o.DiscoveryRadiusWorld <= 0f)
                    report.Add(ErrorCode.InvalidArgument, "hiddenUntilDiscovered requires discoveryRadiusWorld > 0.", ctx);
                if (string.Equals(o.DiscoveryMode, "publicNotice", StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(o.PublicNoticeText))
                    report.Add(ErrorCode.MissingRequiredField, "publicNoticeText required for publicNotice.", ctx);
                if (!string.Equals(o.DiscoveryMode, "publicNotice", StringComparison.Ordinal) &&
                    o.PublicNoticeRevealExactLocation)
                    report.Add(ErrorCode.InvalidArgument, "publicNoticeRevealExactLocation is only valid for publicNotice.", ctx);
                ScanConditions(o.Conditions, registry, locations, producedFlags, consumedFlags, ctx + ".conditions", report);
                for (var i = 0; i < o.ExpireOutcomes.Count; i++)
                {
                    var outcome = o.ExpireOutcomes[i];
                    var kind = outcome?.Kind?.Trim().ToLowerInvariant() ?? string.Empty;
                    if (kind != "setflag" && kind != "clearflag" && kind != "addcounter" && kind != "setcounter")
                        report.Add(ErrorCode.InvalidArgument,
                            "EVENT-02 V1 expireOutcomes only allow setFlag/clearFlag/addCounter/setCounter.", ctx);
                }
                ScanOutcomes(o.ExpireOutcomes, registry, locations, producedFlags, consumedFlags, ctx + ".expireOutcomes", report);
            }
        }

        /// <summary>
        /// §8：opening population 的 LocationId → Site 解析必须**唯一可消解**。同名 locationId 被多个 Site
        /// 复用却缺少 source localMapId 时，解析无法确定归属 —— 必须报 Content validation error，
        /// 绝不允许运行期静默选第一个 Site。
        /// </summary>
        static void ValidateOutdoorSurfaceSitePlaceIdentities(
            DefinitionRegistry registry,
            ValidationReport report)
        {
            var index = ContinuousOutdoorSitePlaceIndex.Build(registry);
            var issues = index.ValidateAmbiguities();
            for (var i = 0; i < issues.Count; i++)
                report.Add(ErrorCode.DuplicateDefinitionId, issues[i], "outdoorSurface.sitePlaces");
        }

        static void ValidateMapSpawnZones(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.MapLayouts)
            {
                var layout = kv.Value;
                if (layout?.Placements == null)
                    continue;
                for (var i = 0; i < layout.Placements.Count; i++)
                {
                    var p = layout.Placements[i];
                    if (p == null ||
                        !string.Equals(p.Kind, "spawnZone", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var ctx = layout.Id + ".placements[" + p.Id + "]";
                    if (string.IsNullOrWhiteSpace(p.SpawnTableId))
                    {
                        report.Add(
                            ErrorCode.MissingRequiredField,
                            "spawnZone.spawnTableId required.",
                            ctx);
                    }
                    else
                        RequireDef(registry, p.SpawnTableId, "spawnTable", ctx + ".spawnTableId", report);
                    if (string.IsNullOrWhiteSpace(p.BoundLocationId))
                    {
                        report.Add(
                            ErrorCode.MissingRequiredField,
                            "spawnZone.boundLocationId required.",
                            ctx);
                    }
                    else if (!locations.Contains(p.BoundLocationId))
                    {
                        report.Add(
                            ErrorCode.NotFound,
                            "spawnZone.boundLocationId missing in worldRegion.",
                            ctx + ":" + p.BoundLocationId);
                    }
                }
            }
        }

        void ValidateScenarios(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.OpeningScenarios)
            {
                var s = kv.Value;
                var ctx = s.Id.ToString();
                foreach (var entry in s.StartingInventory)
                    if (entry == null || entry.Count <= 0 || !DefinitionId.TryParse(entry.ItemId, out var itemId) ||
                        (!registry.Resources.ContainsKey(itemId) && !registry.Items.ContainsKey(itemId)))
                        report.Add(ErrorCode.InvalidArgument, "Invalid startingInventory item/count.", ctx);

                RequireDef(registry, s.OpeningWorldRegionId, "worldRegion", ctx + ".openingWorldRegionId", report);
                RequireDef(registry, s.OpeningLocalPlaceSetId, "localPlaceSet", ctx + ".openingLocalPlaceSetId", report);
                RequireDef(registry, s.OpeningChapterId, "chapter", ctx + ".openingChapterId", report);

                OutdoorWorldSurfaceDefinition openingSurface = null;
                if (string.IsNullOrWhiteSpace(s.OpeningSurfaceId))
                {
                    report.Add(ErrorCode.MissingRequiredField,
                        "Current openingScenario requires openingSurfaceId. Legacy Hex/LocalMap content must be converted offline.",
                        ctx + ".openingSurfaceId");
                }
                else
                {
                    if (!DefinitionId.TryParse(s.OpeningSurfaceId, out var surfaceId) ||
                        !registry.TryGetOutdoorSurface(surfaceId, out openingSurface) ||
                        openingSurface == null || openingSurface.AcceptanceOnly ||
                        !registry.TryGetOutdoorSurfaceGeography(s.OpeningSurfaceId, out var surfaceGeography) ||
                        surfaceGeography?.Navigation == null)
                        report.Add(ErrorCode.NotFound, "openingSurfaceId requires a normal Surface and navigation geography.",
                            ctx + ".openingSurfaceId:" + s.OpeningSurfaceId);
                }

                var worldSites = new HashSet<string>(StringComparer.Ordinal);
                if (openingSurface?.SiteRegions != null)
                    foreach (var region in openingSurface.SiteRegions)
                        if (region != null && !string.IsNullOrEmpty(region.SiteId))
                            worldSites.Add(region.SiteId);
                if (openingSurface?.OpeningEntityAnchors != null)
                {
                    var anchorKeys = new HashSet<string>(StringComparer.Ordinal);
                    registry.TryGetOutdoorSurfaceGeography(s.OpeningSurfaceId, out var openingGeography);
                    foreach (var anchor in openingSurface.OpeningEntityAnchors)
                    {
                        if (anchor == null || !worldSites.Contains(anchor.SiteId) ||
                            string.IsNullOrWhiteSpace(anchor.SpawnKey) ||
                            string.IsNullOrWhiteSpace(anchor.DefinitionId) ||
                            !anchorKeys.Add(anchor.SpawnKey) ||
                            openingGeography?.Navigation == null ||
                            !openingGeography.Navigation.IsWalkable(anchor.WorldX, anchor.WorldY))
                            report.Add(ErrorCode.InvalidArgument, "Invalid or duplicate opening Surface entity anchor.",
                                ctx + ".openingSurfaceId:" + s.OpeningSurfaceId);
                    }
                }

                if (s.Spawns == null)
                    continue;
                var spawnOrdinals = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < s.Spawns.Count; i++)
                {
                    var spawn = s.Spawns[i];
                    if (spawn == null || string.IsNullOrWhiteSpace(spawn.DefinitionId))
                    {
                        report.Add(ErrorCode.InvalidArgument, "Opening spawn definitionId missing.", ctx + ".spawn[" + i + "]");
                        continue;
                    }
                    var spawnDefinitionId = spawn.DefinitionId.Trim();
                    spawnOrdinals.TryGetValue(spawnDefinitionId, out var ordinal);
                    spawnOrdinals[spawnDefinitionId] = ordinal + 1;
                    if (openingSurface != null)
                    {
                        var key = XianXia.Core.World.OpeningSpawnIdentityBoard.BuildStableKey(spawnDefinitionId, ordinal);
                        if (!ContinuousOutdoorOpeningAnchorResolver.TryFindOpeningEntityAnchor(
                                openingSurface, key, spawnDefinitionId, out var matchedAnchor,
                                out var anchorFailure))
                            report.Add(ErrorCode.InvalidArgument,
                                "Opening spawn requires exactly one matching Surface anchor: " + anchorFailure,
                                ctx + ".spawn[" + i + "]:" + key);
                        else if (!string.IsNullOrWhiteSpace(spawn.WorldSiteId) &&
                                 !string.Equals(spawn.WorldSiteId.Trim(), matchedAnchor.SiteId,
                                     StringComparison.Ordinal))
                            report.Add(ErrorCode.InvalidArgument,
                                "Opening spawn worldSiteId differs from Surface anchor SiteId.",
                                ctx + ".spawn[" + i + "]:" + key);
                    }
                    RequireDef(registry, spawn.DefinitionId, "character", ctx + ".spawn[" + i + "]", report);
                    if (!string.IsNullOrWhiteSpace(spawn.JobId))
                        RequireDef(registry, spawn.JobId, "job", ctx + ".spawn[" + i + "].jobId", report);

                    // Optional authored placement validation.
                    if (!string.IsNullOrWhiteSpace(spawn.LocalLocationId))
                    {
                        if (!locations.Contains(spawn.LocalLocationId))
                        {
                            report.Add(
                                ErrorCode.NotFound,
                                "spawn.localLocationId missing in any localPlaceSet/worldRegion.",
                                ctx + ".spawn[" + i + "].localLocationId:" + spawn.LocalLocationId);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(spawn.WorldSiteId))
                    {
                        if (openingSurface == null)
                        {
                            report.Add(
                                ErrorCode.InvalidArgument,
                                "spawn.worldSiteId requires scenario.openingSurfaceId.",
                                ctx + ".spawn[" + i + "].worldSiteId:" + spawn.WorldSiteId);
                        }
                        else if (!worldSites.Contains(spawn.WorldSiteId))
                        {
                            report.Add(
                                ErrorCode.NotFound,
                                "spawn.worldSiteId missing in opening world sites.",
                                ctx + ".spawn[" + i + "].worldSiteId:" + spawn.WorldSiteId);
                        }
                    }
                }

                if (s.OpeningRelations != null)
                {
                    for (var i = 0; i < s.OpeningRelations.Count; i++)
                    {
                        var e = s.OpeningRelations[i];
                        RequireDef(registry, e.FromDefinitionId, "character", ctx + ".relation.from", report);
                        RequireDef(registry, e.ToDefinitionId, "character", ctx + ".relation.to", report);
                    }
                }

                if (s.OpeningBonds != null)
                {
                    var uniqueBonds = new HashSet<string>(StringComparer.Ordinal);
                    for (var i = 0; i < s.OpeningBonds.Count; i++)
                    {
                        var bond = s.OpeningBonds[i];
                        if (bond == null)
                            continue;
                        var bondCtx = ctx + ".openingBonds[" + i + "]";
                        RequireDef(registry, bond.FromDefinitionId, "character", bondCtx + ".from", report);
                        RequireDef(registry, bond.ToDefinitionId, "character", bondCtx + ".to", report);
                        if (string.IsNullOrWhiteSpace(bond.FromDefinitionId) ||
                            string.IsNullOrWhiteSpace(bond.ToDefinitionId) ||
                            string.Equals(bond.FromDefinitionId, bond.ToDefinitionId, StringComparison.Ordinal))
                        {
                            report.Add(ErrorCode.InvalidArgument, "openingBond endpoints must be distinct.", bondCtx);
                            continue;
                        }
                        var from = bond.FromDefinitionId;
                        var to = bond.ToDefinitionId;
                        if (SocialBond.IsSymmetric(bond.Kind) && string.CompareOrdinal(from, to) > 0)
                        {
                            var swap = from;
                            from = to;
                            to = swap;
                        }
                        var key = ((int)bond.Kind) + "|" + from + "|" + to;
                        if (!uniqueBonds.Add(key))
                            report.Add(ErrorCode.DuplicateDefinitionId, "openingBond duplicate.", bondCtx);
                    }
                }

                if (s.InitialNpcSquadIds != null)
                {
                    for (var i = 0; i < s.InitialNpcSquadIds.Count; i++)
                        RequireDef(registry, s.InitialNpcSquadIds[i], "npcSquad",
                            ctx + ".initialNpcSquadIds[" + i + "]", report);
                }
            }
        }

        /// <summary>
        /// Strategic Faction cross-reference for current Surface, Squad, scenario and roster content.
        /// 未知引用 = Content Validation ERROR（不得静默随机颜色）。空引用不校验。
        /// </summary>
        static void ValidateStrategicFactions(DefinitionRegistry registry, ValidationReport report)
        {
            // CharacterDefinition.defaultFaction* 一致性 + 存在性（Case A/B）。
            foreach (var kv in registry.Characters)
            {
                var character = kv.Value;
                if (character == null)
                    continue;
                var hasDefaultFaction = !string.IsNullOrWhiteSpace(character.DefaultFactionId);
                var hasDefaultRole = !string.IsNullOrWhiteSpace(character.DefaultFactionRole);
                if (hasDefaultFaction)
                {
                    RequireFaction(registry, character.DefaultFactionId, character.Id + ".defaultFactionId", report, allowEmpty: false);
                    if (!hasDefaultRole ||
                        !Enum.TryParse(character.DefaultFactionRole.Trim(), true, out FactionRoleKind role) ||
                        role == FactionRoleKind.None)
                    {
                        report.Add(
                            ErrorCode.InvalidArgument,
                            "Character defaultFactionId requires a non-None defaultFactionRole.",
                            character.Id + ".defaultFactionRole");
                    }
                }
                else if (hasDefaultRole)
                {
                    report.Add(
                        ErrorCode.InvalidArgument,
                        "Character defaultFactionRole requires defaultFactionId.",
                        character.Id + ".defaultFactionRole");
                }
            }

            foreach (var kv in registry.NpcSquads)
            {
                var def = kv.Value;
                if (def == null)
                    continue;
                RequireFaction(registry, def.FactionId, def.Id + ".factionId", report, allowEmpty: false);
            }

            foreach (var kv in registry.OpeningScenarios)
            {
                var scenario = kv.Value;
                if (scenario == null)
                    continue;
                RequireFaction(registry, scenario.OpeningFactionId, scenario.Id + ".openingFactionId", report, allowEmpty: true);
                ValidateStrategicOpening(registry, scenario, report);
                if (scenario.Spawns == null)
                    continue;
                for (var i = 0; i < scenario.Spawns.Count; i++)
                {
                    var spawn = scenario.Spawns[i];
                    if (spawn == null)
                        continue;
                    ValidateSpawnMembership(registry, spawn, scenario.Id + ".spawns[" + i + "]", report);
                }
            }

            foreach (var kv in registry.CharacterRosters)
            {
                var roster = kv.Value;
                if (roster?.Entries == null)
                    continue;
                for (var i = 0; i < roster.Entries.Count; i++)
                {
                    var entry = roster.Entries[i];
                    if (entry == null)
                        continue;
                    ValidateSpawnMembership(registry, entry, roster.Id + ".entries[" + i + "]", report);
                }
            }

            foreach (var kv in registry.OutdoorSurfaces)
            {
                var surface = kv.Value;
                if (surface == null || surface.AcceptanceOnly)
                    continue;
                var surfaceCtx = surface.SurfaceId;
                if (surface.SiteRegions != null)
                {
                    for (var i = 0; i < surface.SiteRegions.Count; i++)
                    {
                        var site = surface.SiteRegions[i];
                        if (site == null)
                            continue;
                        RequireFaction(
                            registry,
                            site.OwnerFactionId,
                            surfaceCtx + ".siteRegions[" + i + "]:" + site.SiteId + ".ownerFactionId",
                            report);
                    }
                }
                if (surface.FactionFlags != null)
                {
                    for (var i = 0; i < surface.FactionFlags.Count; i++)
                    {
                        var flag = surface.FactionFlags[i];
                        if (flag == null)
                            continue;
                        RequireFaction(
                            registry,
                            flag.FactionId,
                            surfaceCtx + ".factionFlags[" + i + "]:" + flag.FlagId + ".factionId",
                            report);
                    }
                }
            }
        }

        static void ValidateBuildings(DefinitionRegistry registry, ValidationReport report)
        {
            foreach (var kv in registry.Buildings)
            {
                var building = kv.Value;
                if (building == null)
                    continue;
                if (building.PlacementKind != "factionFlag" && building.PlacementKind != "farmField" &&
                    building.PlacementKind != "recoverySpot" && building.PlacementKind != "storageRoom")
                    report.Add(ErrorCode.InvalidArgument, "Unknown building placementKind.",
                        building.Id + ".placementKind:" + building.PlacementKind);
                if (building.PlacementKind == "farmField" && (building.CreatesWorldSite ||
                    !XianXia.Core.Exploration.OutdoorAdministrativeAssetSemantics.IsAdministrativeAssetKind(building.OutdoorKind) ||
                    building.FootprintCellsW <= 0 || building.FootprintCellsH <= 0))
                    report.Add(ErrorCode.InvalidArgument, "Invalid farmField kind, dimensions or createsWorldSite.", building.Id.ToString());
                if (building.PlacementKind == "recoverySpot" && (building.CreatesWorldSite ||
                    !string.Equals(building.OutdoorKind, "recoverySpot", StringComparison.Ordinal) ||
                    building.FootprintCellsW != 2 || building.FootprintCellsH != 2))
                    report.Add(ErrorCode.InvalidArgument, "Invalid recoverySpot kind, 2x2 dimensions or createsWorldSite.", building.Id.ToString());
                if (building.PlacementKind == "storageRoom" && (building.CreatesWorldSite ||
                    !string.Equals(building.OutdoorKind, "storageRoom", StringComparison.Ordinal) ||
                    building.FootprintCellsW != 3 || building.FootprintCellsH != 3 ||
                    building.DismantleRefundRate != 0f))
                    report.Add(ErrorCode.InvalidArgument,
                        "Invalid storageRoom kind, 3x3 dimensions, createsWorldSite or dismantle refund.", building.Id.ToString());
                if (building.Costs == null)
                    continue;
                for (var i = 0; i < building.Costs.Count; i++)
                {
                    var cost = building.Costs[i];
                    if (cost == null || string.IsNullOrWhiteSpace(cost.ItemId) ||
                        !DefinitionId.TryParse(cost.ItemId, out var id) ||
                        (!registry.Resources.ContainsKey(id) && !registry.Items.ContainsKey(id)))
                        report.Add(ErrorCode.NotFound, "building cost must reference a resource or item.",
                            building.Id + ".costs[" + i + "]:" + (cost?.ItemId ?? string.Empty));
                }
            }
        }

        static void ValidateStrategicOpening(DefinitionRegistry registry, OpeningScenarioDefinition scenario, ValidationReport report)
        {
            var s = scenario.StrategicOpening;
            if (s == null) return;
            var ctx = scenario.Id + ".strategicOpening";
            RequireFaction(registry, s.PlayerFactionId, ctx + ".playerFactionId", report, false);
            var vassals = new Dictionary<string, string>(StringComparer.Ordinal);
            var allianceMembers = new HashSet<string>(StringComparer.Ordinal);
            var alliancePairs = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < s.Vassalages.Count; i++)
            {
                var v = s.Vassalages[i]; var p = ctx + ".vassalages[" + i + "]";
                RequireFaction(registry, v.VassalFactionId, p + ".vassalFactionId", report, false); RequireFaction(registry, v.OverlordFactionId, p + ".overlordFactionId", report, false);
                if (v.VassalFactionId == v.OverlordFactionId) report.Add(ErrorCode.InvalidArgument, "Opening vassal and overlord must differ.", p);
                if (vassals.ContainsKey(v.VassalFactionId)) report.Add(ErrorCode.InvalidArgument, "Faction '" + v.VassalFactionId + "' has duplicate/conflicting authored overlord.", p);
                else vassals[v.VassalFactionId] = v.OverlordFactionId;
            }
            foreach (var v in vassals) if (vassals.ContainsKey(v.Value)) report.Add(ErrorCode.InvalidArgument, "Opening vassalage nesting is not supported: " + v.Key + " -> " + v.Value, ctx + ".vassalages");
            for (var i = 0; i < s.Alliances.Count; i++)
            {
                var a=s.Alliances[i]; var p=ctx+".alliances["+i+"]"; RequireFaction(registry,a.FactionAId,p+".factionAId",report,false); RequireFaction(registry,a.FactionBId,p+".factionBId",report,false);
                var key=string.CompareOrdinal(a.FactionAId,a.FactionBId)<=0?a.FactionAId+"|"+a.FactionBId:a.FactionBId+"|"+a.FactionAId;
                if(a.FactionAId==a.FactionBId||!alliancePairs.Add(key)||!allianceMembers.Add(a.FactionAId)||!allianceMembers.Add(a.FactionBId)||vassals.ContainsKey(a.FactionAId)||vassals.ContainsKey(a.FactionBId)) report.Add(ErrorCode.InvalidArgument,"Opening alliance is invalid or conflicts with authored membership/vassalage.",p);
            }
            var wars=new HashSet<string>(StringComparer.Ordinal);
            for(var i=0;i<s.InitialWars.Count;i++){var w=s.InitialWars[i];var p=ctx+".initialWars["+i+"]";RequireFaction(registry,w.DeclarerFactionId,p+".declarerFactionId",report,false);RequireFaction(registry,w.TargetFactionId,p+".targetFactionId",report,false);var key=string.CompareOrdinal(w.DeclarerFactionId,w.TargetFactionId)<=0?w.DeclarerFactionId+"|"+w.TargetFactionId:w.TargetFactionId+"|"+w.DeclarerFactionId;if(w.DeclarerFactionId==w.TargetFactionId||!wars.Add(key)||alliancePairs.Contains(key))report.Add(ErrorCode.InvalidArgument,"Opening war declarer and target must differ and cannot duplicate/allied pair.",p);}
        }

        static void ValidateSpawnMembership(
            DefinitionRegistry registry,
            OpeningSpawnEntry entry,
            string context,
            ValidationReport report)
        {
            var hasFaction = !string.IsNullOrWhiteSpace(entry.FactionId);
            var hasRole = !string.IsNullOrWhiteSpace(entry.FactionRole) &&
                          Enum.TryParse(entry.FactionRole.Trim(), true, out FactionRoleKind role) &&
                          role != FactionRoleKind.None;
            var modeExplicit = entry.FactionModeExplicit;

            switch (entry.FactionMode)
            {
                case OpeningFactionMode.Override:
                    // Override：factionId 非空 + role 有效 + faction 存在。
                    if (!hasFaction)
                    {
                        report.Add(ErrorCode.InvalidArgument,
                            "Spawn factionMode=Override requires factionId.", context + ".factionId");
                        return;
                    }
                    RequireFaction(registry, entry.FactionId, context + ".factionId", report, allowEmpty: false);
                    if (!hasRole)
                        report.Add(ErrorCode.InvalidArgument,
                            "Spawn factionMode=Override requires a non-None factionRole.", context + ".factionRole");
                    return;

                case OpeningFactionMode.Unaffiliated:
                    // Unaffiliated：禁止 factionId/factionRole。
                    if (hasFaction || !string.IsNullOrWhiteSpace(entry.FactionRole))
                    {
                        report.Add(ErrorCode.InvalidArgument,
                            "Spawn factionMode=Unaffiliated must not carry factionId/factionRole.", context + ".factionId");
                    }
                    return;

                case OpeningFactionMode.CharacterDefault:
                default:
                    if (modeExplicit)
                    {
                        // 显式 CharacterDefault：新格式 spawn 自己不得带 factionId/factionRole。
                        if (hasFaction || !string.IsNullOrWhiteSpace(entry.FactionRole))
                        {
                            report.Add(ErrorCode.InvalidArgument,
                                "Spawn factionMode=CharacterDefault must not carry factionId/factionRole (inherit CharacterDefinition).",
                                context + ".factionId");
                        }
                        return;
                    }

                    // mode 缺省：区分三态。
                    if (hasFaction)
                    {
                        // Legacy Explicit Override：无 mode 但显式 factionId → 按 Override 校验（deprecated）。
                        RequireFaction(registry, entry.FactionId, context + ".factionId", report, allowEmpty: false);
                        if (!hasRole)
                            report.Add(ErrorCode.InvalidArgument, "Spawn factionId requires a non-None factionRole.", context + ".factionRole");
                        return;
                    }

                    if (entry.AssignOpeningFaction)
                    {
                        // Legacy assignOpeningFaction：role 与 scenario.openingFactionId 的隐式继承仍被接受。
                        if (!hasRole)
                            report.Add(ErrorCode.InvalidArgument, "Legacy assignOpeningFaction requires a non-None factionRole.", context + ".factionRole");
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(entry.FactionRole))
                        report.Add(ErrorCode.InvalidArgument, "Spawn factionRole requires factionId.", context + ".factionRole");
                    return;
            }
        }

        /// <summary>factionId 必须能解析为 DefinitionId 且存在于 StrategicFactions；成员资格不检查 territorySelectable。</summary>
        static void RequireFaction(
            DefinitionRegistry registry,
            string factionId,
            string ctx,
            ValidationReport report,
            bool allowEmpty = true)
        {
            if (string.IsNullOrEmpty(factionId))
            {
                if (!allowEmpty)
                    report.Add(ErrorCode.MissingRequiredField, "strategicFaction reference required.", ctx);
                return;
            }
            if (!DefinitionId.TryParse(factionId, out var id) ||
                !registry.TryGetStrategicFaction(id, out _))
            {
                report.Add(
                    ErrorCode.NotFound,
                    "strategicFaction reference missing: " + factionId,
                    ctx);
            }
        }

        static void ValidateNpcSquads(DefinitionRegistry registry, ValidationReport report)
        {
            var squadIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in registry.NpcSquads)
            {
                var def = pair.Value;
                var ctx = def.Id.ToString();
                if (!squadIds.Add(def.SquadId)) report.Add(ErrorCode.DuplicateDefinitionId, "Duplicate npcSquad.squadId.", ctx);
                RequireFaction(registry, def.FactionId, ctx + ".factionId", report, false);
                if (def.Members == null || def.Members.Count == 0) report.Add(ErrorCode.MissingRequiredField, "npcSquad.members empty.", ctx);
                else for (var i = 0; i < def.Members.Count; i++)
                    RequireDef(registry, def.Members[i].CharacterDefinitionId, "character", ctx + ".members[" + i + "].characterDefinitionId", report);
                if (def.InitialSurfacePosition != null)
                {
                    var position = def.InitialSurfacePosition;
                    if (!registry.TryGetOutdoorSurfaceGeography(position.SurfaceId, out var geography) ||
                        geography?.Navigation == null)
                        report.Add(ErrorCode.NotFound, "npcSquad initial position Surface geography missing.",
                            ctx + ":" + position.SurfaceId);
                    else if (!geography.Navigation.Contains(position.WorldX, position.WorldY) ||
                             !geography.Navigation.IsWalkable(position.WorldX, position.WorldY))
                        report.Add(ErrorCode.InvalidArgument, "npcSquad initial position is outside or blocked.", ctx);
                }
                else if (def.InitialSurfaceDeployment != null)
                {
                    var deployment = def.InitialSurfaceDeployment;
                    if (!registry.TryGetOutdoorSurfaceGeography(deployment.SurfaceId, out var geography) ||
                        geography?.Navigation == null)
                        report.Add(ErrorCode.NotFound, "npcSquad deployment Surface geography missing.",
                            ctx + ":" + deployment.SurfaceId);
                    else if (!NpcSquadContentBootstrap.TryResolveCoreCenter(registry,
                                 deployment.AnchorSiteId, deployment.SurfaceId, out var anchor))
                        report.Add(ErrorCode.NotFound, "npcSquad deployment anchor Site is not exactly resolvable.",
                            ctx + ":" + deployment.AnchorSiteId);
                    else
                    {
                        var point = new WorldVec2(
                            anchor.X + deployment.OffsetCellsX * geography.Navigation.CellSize,
                            anchor.Y + deployment.OffsetCellsY * geography.Navigation.CellSize);
                        if (!geography.Navigation.Contains(point.X, point.Y) ||
                            !geography.Navigation.IsWalkable(point.X, point.Y))
                            report.Add(ErrorCode.InvalidArgument,
                                "npcSquad deployment result is outside or blocked.", ctx);
                    }
                }
                else if (!TryResolveNpcSquadSiteArrival(registry, def.AssemblySiteId,
                             out var arrivalSurface, out var arrival))
                    report.Add(ErrorCode.NotFound, "npcSquad AssemblySite has no unique SiteArrival.",
                        ctx + ":" + def.AssemblySiteId);
                else if (!registry.TryGetOutdoorSurfaceGeography(arrivalSurface, out var arrivalGeography) ||
                         arrivalGeography?.Navigation == null ||
                         !arrivalGeography.Navigation.Contains(arrival.X, arrival.Y) ||
                         !arrivalGeography.Navigation.IsWalkable(arrival.X, arrival.Y))
                    report.Add(ErrorCode.InvalidArgument,
                        "npcSquad AssemblySite SiteArrival is outside or blocked.", ctx);
            }
        }

        static bool TryResolveNpcSquadSiteArrival(DefinitionRegistry registry, string siteId,
            out string surfaceId, out WorldVec2 arrival)
        {
            surfaceId = string.Empty;
            arrival = default;
            if (registry == null || string.IsNullOrWhiteSpace(siteId)) return false;
            var found = false;
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface?.SiteRegions == null) continue;
                for (var i = 0; i < surface.SiteRegions.Count; i++)
                {
                    var region = surface.SiteRegions[i];
                    if (region == null || !string.Equals(region.SiteId, siteId, StringComparison.Ordinal)) continue;
                    if (found) return false;
                    surfaceId = string.IsNullOrWhiteSpace(region.SurfaceId)
                        ? surface.SurfaceId : region.SurfaceId;
                    arrival = new WorldVec2(region.ArrivalWorldX, region.ArrivalWorldY);
                    found = true;
                }
            }
            return found;
        }

        void ValidateWorldRegions(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.WorldRegions)
            {
                var region = kv.Value;
                var ctx = region.Id.ToString();
                if (!string.IsNullOrEmpty(region.StartLocationId) && !locations.Contains(region.StartLocationId))
                    report.Add(ErrorCode.NotFound, "startLocationId missing in locations.", ctx + ":" + region.StartLocationId);

                if (region.Locations == null)
                    continue;
                for (var i = 0; i < region.Locations.Count; i++)
                {
                    var loc = region.Locations[i];
                    var lctx = ctx + "." + loc.Id;
                    if (loc.AdjacentIds != null)
                    {
                        for (var a = 0; a < loc.AdjacentIds.Count; a++)
                        {
                            if (!locations.Contains(loc.AdjacentIds[a]))
                                report.Add(ErrorCode.NotFound, "adjacent location missing.", lctx + "->" + loc.AdjacentIds[a]);
                        }
                    }

                    RequireDef(registry, loc.OpportunitySiteId, "opportunitySite", lctx + ".opportunitySiteId", report);
                    RequireDef(registry, loc.ResidentNpcDefinitionId, "character", lctx + ".residentNpc", report);
                    RequireDef(registry, loc.ResourceOnExploreId, "resource", lctx + ".resourceOnExploreId", report);
                    ScanConditions(loc.EnterConditions, registry, locations, null, null, lctx + ".enter", report);
                    if (loc.QuestOfferIds != null)
                    {
                        for (var q = 0; q < loc.QuestOfferIds.Count; q++)
                            RequireDef(registry, loc.QuestOfferIds[q], "quest", lctx + ".questOffer", report);
                    }
                }
            }
        }

        void ValidateLocalPlaceSets(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.LocalPlaceSets)
            {
                var set = kv.Value;
                var ctx = set.Id.ToString();
                if (!string.IsNullOrEmpty(set.MapLayoutId))
                {
                    var mapParsed = DefinitionId.Parse(set.MapLayoutId);
                    if (mapParsed.IsFailure || !registry.MapLayouts.ContainsKey(mapParsed.Value))
                    {
                        report.Add(
                            ErrorCode.NotFound,
                            "localPlaceSet.mapLayoutId missing.",
                            ctx + ":" + set.MapLayoutId);
                    }
                }

                if (!string.IsNullOrEmpty(set.StartLocationId) && !locations.Contains(set.StartLocationId))
                    report.Add(ErrorCode.NotFound, "startLocationId missing in locations.", ctx + ":" + set.StartLocationId);

                if (set.Locations == null)
                    continue;
                for (var i = 0; i < set.Locations.Count; i++)
                {
                    var loc = set.Locations[i];
                    var lctx = ctx + "." + loc.Id;
                    if (loc.AdjacentIds != null)
                    {
                        for (var a = 0; a < loc.AdjacentIds.Count; a++)
                        {
                            if (!locations.Contains(loc.AdjacentIds[a]))
                                report.Add(ErrorCode.NotFound, "adjacent location missing.", lctx + "->" + loc.AdjacentIds[a]);
                        }
                    }

                    RequireDef(registry, loc.OpportunitySiteId, "opportunitySite", lctx + ".opportunitySiteId", report);
                    RequireDef(registry, loc.ResidentNpcDefinitionId, "character", lctx + ".residentNpc", report);
                    RequireDef(registry, loc.ResourceOnExploreId, "resource", lctx + ".resourceOnExploreId", report);
                    ScanConditions(loc.EnterConditions, registry, locations, null, null, lctx + ".enter", report);
                    if (loc.QuestOfferIds != null)
                    {
                        for (var q = 0; q < loc.QuestOfferIds.Count; q++)
                            RequireDef(registry, loc.QuestOfferIds[q], "quest", lctx + ".questOffer", report);
                    }
                }
            }
        }

        void ValidateQuests(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var kv in registry.Quests)
            {
                var q = kv.Value;
                var ctx = q.Id.ToString();
                var commission = string.Equals(q.RuntimeMode, "characterCommission", StringComparison.OrdinalIgnoreCase);
                if (commission && !string.Equals(q.AcceptanceMode, "interaction", StringComparison.OrdinalIgnoreCase))
                    report.Add(ErrorCode.InvalidArgument, "characterCommission requires acceptanceMode=interaction.", ctx);
                if (commission && q.AutoOffer)
                    report.Add(ErrorCode.InvalidArgument, "characterCommission cannot autoOffer.", ctx);
                for (var i = 0; i < q.DeliveryRequirements.Count; i++)
                {
                    var requirement = q.DeliveryRequirements[i];
                    if (requirement == null || requirement.Amount <= 0)
                        report.Add(ErrorCode.InvalidArgument, "delivery requirement amount must be positive.", ctx);
                    else if (!DefinitionId.TryParse(requirement.ItemId, out var deliveryId) ||
                             (!registry.Resources.ContainsKey(deliveryId) && !registry.Items.ContainsKey(deliveryId)))
                        report.Add(ErrorCode.NotFound, "delivery resource/item reference missing.",
                            ctx + ".deliveryRequirements:" + requirement.ItemId);
                }
                ScanConditions(q.OfferConditions, registry, locations, producedFlags, consumedFlags, ctx + ".offer", report);
                ScanConditions(q.CompleteConditions, registry, locations, producedFlags, consumedFlags, ctx + ".complete", report);
                ScanConditions(q.FailConditions, registry, locations, producedFlags, consumedFlags, ctx + ".fail", report);
                ScanOutcomes(q.Rewards, registry, locations, producedFlags, consumedFlags, ctx + ".reward", report);
                ScanOutcomes(q.FailResults, registry, locations, producedFlags, consumedFlags, ctx + ".failResult", report);
            }
        }

        void ValidateContentEvents(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var kv in registry.ContentEvents)
            {
                var e = kv.Value;
                var ctx = e.Id.ToString();
                ContentEventStructureValidator.Validate(e, report);
                if (string.Equals(e.Trigger, "onTalk", StringComparison.OrdinalIgnoreCase))
                {
                    RequireDef(registry, e.NpcDefinitionId, "character", ctx + ".npcDefinitionId", report);
                    RequireDef(registry, e.WorldOpportunityId, "worldOpportunity", ctx + ".worldOpportunityId", report);
                    for (var i = 0; i < e.NpcTags.Count; i++)
                        if (string.IsNullOrWhiteSpace(e.NpcTags[i]))
                            report.Add(ErrorCode.InvalidArgument, "contentEvent.npcTags cannot contain blank tags.", ctx);
                }
                if (string.Equals(e.Trigger, "onInspect", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(e.WorldObjectId) &&
                    HasStaticWorldObjectCatalog(e.WorldObjectKind) &&
                    !WorldObjectIdExists(registry, e.WorldObjectKind, e.WorldObjectId))
                    report.Add(ErrorCode.NotFound, "contentEvent.worldObjectId missing for kind.",
                        ctx + ":" + e.WorldObjectKind + ":" + e.WorldObjectId);
                if (string.Equals(e.Trigger, "onInspect", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.WorldObjectKind, "opportunityObject", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(e.WorldOpportunityId))
                        report.Add(ErrorCode.MissingRequiredField, "opportunityObject onInspect requires worldOpportunityId.", ctx);
                    else if (!DefinitionId.TryParse(e.WorldOpportunityId, out var opportunityId) ||
                             !registry.WorldOpportunities.TryGetValue(opportunityId, out var opportunity) ||
                             opportunity.SpawnKind != "worldObject")
                        report.Add(ErrorCode.InvalidArgument, "onInspect worldOpportunityId must reference a worldObject opportunity.", ctx);
                    if (!string.IsNullOrWhiteSpace(e.WorldObjectId))
                        report.Add(ErrorCode.InvalidArgument, "opportunityObject onInspect must not author a runtime worldObjectId.", ctx);
                }
                foreach (var step in e.Steps)
                {
                    var sc = ctx + ".step." + step.Id;
                    if (!string.IsNullOrEmpty(step.SpeakerRef) && step.SpeakerRef != "@actor" &&
                        step.SpeakerRef != "@target" && step.SpeakerRef != "@issuer")
                        RequireDef(registry, step.SpeakerRef, "character", sc + ".speakerRef", report);
                    ScanOutcomes(step.Outcomes, registry, locations, producedFlags, consumedFlags, sc, report);
                    foreach (var c in step.Choices)
                    {
                        ScanConditions(c.Conditions, registry, locations, producedFlags, consumedFlags, sc + "." + c.Id, report);
                        ScanOutcomes(c.Outcomes, registry, locations, producedFlags, consumedFlags, sc + "." + c.Id, report);
                    }
                }
                if (!string.IsNullOrEmpty(e.LocationId) && !locations.Contains(e.LocationId))
                    report.Add(ErrorCode.NotFound, "contentEvent.locationId missing.", ctx + ":" + e.LocationId);
                RequireDef(registry, e.QuestId, "quest", ctx + ".questId", report);
                ScanConditions(e.Conditions, registry, locations, producedFlags, consumedFlags, ctx + ".cond", report);
                if (e.Choices == null)
                    continue;
                for (var i = 0; i < e.Choices.Count; i++)
                {
                    var c = e.Choices[i];
                    var cctx = ctx + ".choice." + c.Id;
                    ScanConditions(c.Conditions, registry, locations, producedFlags, consumedFlags, cctx, report);
                    ScanOutcomes(c.Outcomes, registry, locations, producedFlags, consumedFlags, cctx, report);
                }
            }
        }

        static bool WorldObjectIdExists(DefinitionRegistry registry, string kind, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return true;
            if (string.Equals(kind, "controlCore", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "housing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "workArea", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var pair in registry.WorkAreas)
                {
                    var area = pair.Value;
                    if (!string.Equals(pair.Key.ToString(), id, StringComparison.Ordinal)) continue;
                    if (string.Equals(kind, "controlCore", StringComparison.OrdinalIgnoreCase)) return area.IsControlCore;
                    var housing = area.ResidentTags.Count > 0 || area.Tags.Contains("home");
                    if (string.Equals(kind, "housing", StringComparison.OrdinalIgnoreCase))
                        return !area.IsControlCore && housing;
                    return !area.IsControlCore && !housing;
                }
                return false;
            }
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface == null || surface.AcceptanceOnly) continue;
                if (string.Equals(kind, "factionFlag", StringComparison.OrdinalIgnoreCase))
                    for (var i = 0; i < surface.FactionFlags.Count; i++)
                        if (string.Equals(surface.FactionFlags[i]?.FlagId, id, StringComparison.Ordinal)) return true;
                for (var i = 0; i < surface.SitePlacements.Count; i++)
                {
                    var p = surface.SitePlacements[i];
                    if (p == null) continue;
                    if (string.Equals(kind, "destructible", StringComparison.OrdinalIgnoreCase) &&
                        OutdoorStatefulPlacementResolver.IsDestructibleKind(p.Kind))
                    {
                        if (string.Equals(p.StableId, id, StringComparison.Ordinal)) return true;
                        for (var y = 0; y < Math.Max(1, p.SourceCellsH); y++)
                            for (var x = 0; x < Math.Max(1, p.SourceCellsW); x++)
                                if (string.Equals(OutdoorStatefulPlacementResolver.ResolveObjectId(p, x, y), id, StringComparison.Ordinal)) return true;
                    }
                    var expected = string.Equals(kind, "farmPlot", StringComparison.OrdinalIgnoreCase) ? "farmField" : kind;
                    if (string.Equals(p.Kind, expected, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.StableId, id, StringComparison.Ordinal)) return true;
                }
            }
            return false;
        }

        static bool HasStaticWorldObjectCatalog(string kind) =>
            string.Equals(kind, "controlCore", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "factionFlag", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "destructible", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "housing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "workArea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "recoverySpot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "storageRoom", StringComparison.OrdinalIgnoreCase);

        void ValidateChapters(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var kv in registry.Chapters)
            {
                var ch = kv.Value;
                var ctx = ch.Id.ToString();
                // openingScenarioId is documentary; warn only if set and missing
                if (!string.IsNullOrEmpty(ch.OpeningScenarioId))
                    RequireDef(registry, ch.OpeningScenarioId, "openingScenario", ctx + ".openingScenarioId", report);

                for (var i = 0; i < ch.QuestChainIds.Count; i++)
                    RequireDef(registry, ch.QuestChainIds[i], "quest", ctx + ".questChain", report);
                for (var i = 0; i < ch.EventChainIds.Count; i++)
                    RequireDef(registry, ch.EventChainIds[i], "contentEvent", ctx + ".eventChain", report);

                for (var b = 0; b < ch.DayBeats.Count; b++)
                {
                    var beat = ch.DayBeats[b];
                    var bctx = ctx + ".dayBeat[" + beat.DayIndex + "]";
                    ScanConditions(beat.Conditions, registry, locations, producedFlags, consumedFlags, bctx, report);
                    for (var i = 0; i < beat.QuestOfferIds.Count; i++)
                        RequireDef(registry, beat.QuestOfferIds[i], "quest", bctx + ".questOffer", report);
                    for (var i = 0; i < beat.ContentEventIds.Count; i++)
                        RequireDef(registry, beat.ContentEventIds[i], "contentEvent", bctx + ".event", report);
                    for (var i = 0; i < beat.SetFlags.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(beat.SetFlags[i]))
                            producedFlags.Add(beat.SetFlags[i]);
                    }
                }
            }
        }

        void ScanConditions(
            IList<ContentCondition> conditions,
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            string ctx,
            ValidationReport report)
        {
            if (conditions == null)
                return;
            for (var i = 0; i < conditions.Count; i++)
            {
                var c = conditions[i];
                if (c == null || string.IsNullOrEmpty(c.Kind))
                    continue;
                var kind = c.Kind.Trim().ToLowerInvariant();
                switch (kind)
                {
                    case "atlocation":
                    case "exploredlocation":
                        if (!string.IsNullOrEmpty(c.Id) && !locations.Contains(c.Id))
                            report.Add(ErrorCode.NotFound, "condition location missing.", ctx + ":" + c.Id);
                        break;
                    case "laboratlocation":
                    case "uniquelaboratlocation":
                    case "uniqueharvestatlocation":
                        if (!string.IsNullOrEmpty(c.Id) && !locations.Contains(c.Id))
                            report.Add(ErrorCode.NotFound, "condition location missing.", ctx + ":" + c.Id);
                        if (kind == "laboratlocation" && !string.IsNullOrEmpty(c.CharacterId))
                            RequireDef(registry, c.CharacterId, "character", ctx + "." + c.Kind, report);
                        break;
                    case "characteratlocation":
                        if (!string.IsNullOrEmpty(c.Id) && !locations.Contains(c.Id))
                            report.Add(ErrorCode.NotFound, "condition location missing.", ctx + ":" + c.Id);
                        if (!string.IsNullOrEmpty(c.CharacterId))
                            RequireDef(registry, c.CharacterId, "character", ctx + "." + c.Kind, report);
                        break;
                    case "knowssite":
                        RequireDef(registry, c.Id, "opportunitySite", ctx + ".knowsSite", report);
                        break;
                    case "stockatleast":
                        RequireDef(registry, c.Id, "resource", ctx + ".stock", report);
                        break;
                    case "questactive":
                    case "questcompleted":
                    case "questofferablefromtarget":
                    case "questactivefromtarget":
                    case "questhandedinfromtarget":
                    case "questdeliveryavailablefromtarget":
                    case "questreadytoclaimfromtarget":
                    case "questcompletedfromtarget":
                    case "questfailedfromtarget":
                        RequireDef(registry, c.Id, "quest", ctx + ".quest", report);
                        break;
                    case "affectionatleast":
                        ValidateCharacterReference(c.Id, registry, ctx + ".affection.from", report);
                        ValidateCharacterReference(c.CharacterId, registry, ctx + ".affection.to", report);
                        break;
                    case "hasmanual":
                        RequireDef(registry, c.Id, "cultivation", ctx + ".manual", report);
                        break;
                    case "counteratleast":
                    case "missingdailyflag":
                    case "hasdailyflag":
                        if (string.IsNullOrEmpty(c.Id))
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                c.Kind + " requires id.",
                                ctx);
                        }

                        break;
                    case "encountercleared":
                        if (string.IsNullOrEmpty(c.Id))
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                "encounterCleared requires id.",
                                ctx);
                        }
                        else if (consumedFlags != null)
                        {
                            consumedFlags.Add(ContentConditionEvaluator.EncounterFlag(c.Id));
                        }

                        break;
                    case "hasflag":
                    case "storyflag":
                    case "missingflag":
                    case "missingstoryflag":
                        if (!string.IsNullOrEmpty(c.Id) && consumedFlags != null)
                            consumedFlags.Add(c.Id);
                        break;
                }
            }
        }

        void ScanOutcomes(
            IList<ContentOutcome> outcomes,
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            string ctx,
            ValidationReport report)
        {
            if (outcomes == null)
                return;
            for (var i = 0; i < outcomes.Count; i++)
            {
                var o = outcomes[i];
                if (o == null || string.IsNullOrEmpty(o.Kind))
                    continue;
                var kind = o.Kind.Trim().ToLowerInvariant();
                switch (kind)
                {
                    case "setflag":
                    case "setstoryflag":
                        if (!string.IsNullOrEmpty(o.Id) && producedFlags != null)
                            producedFlags.Add(o.Id);
                        break;
                    case "clearflag":
                    case "clearstoryflag":
                        if (!string.IsNullOrEmpty(o.Id) && consumedFlags != null)
                            consumedFlags.Add(o.Id);
                        break;
                    case "addstock":
                    case "removestock":
                    {
                        var outcomeName = kind == "removestock" ? "removeStock" : "addStock";
                        if (string.IsNullOrEmpty(o.Id) || !DefinitionId.TryParse(o.Id, out var stockId))
                        {
                            RequireDef(registry, o.Id, "resource", ctx + "." + outcomeName, report);
                            break;
                        }

                        if (registry.Resources.ContainsKey(stockId) || registry.Items.ContainsKey(stockId))
                            break;
                        report.Add(
                            ErrorCode.NotFound,
                            "resource/item reference missing.",
                            ctx + "." + outcomeName + ":" + o.Id);
                        break;
                    }
                    case "startquest":
                    case "acceptquestfromtarget":
                    case "deliverquesttotarget":
                        RequireDef(registry, o.Id, "quest", ctx + ".startQuest", report);
                        break;
                    case "resolvecurrentopportunity":
                        if (!string.IsNullOrWhiteSpace(o.Id) || o.Amount != 0)
                            report.Add(ErrorCode.InvalidArgument, "resolveCurrentOpportunity takes no id or amount.", ctx);
                        break;
                    case "discoversite":
                        RequireDef(registry, o.Id, "opportunitySite", ctx + ".discoverSite", report);
                        break;
                    case "learnmanual":
                        RequireDef(registry, o.Id, "cultivation", ctx + ".learnManual", report);
                        break;
                    case "addcounter":
                    case "setcounter":
                    case "setdailyflag":
                    case "cleardailyflag":
                        if (string.IsNullOrEmpty(o.Id))
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                o.Kind + " requires id.",
                                ctx);
                        }

                        break;
                    case "setencountercleared":
                        if (string.IsNullOrEmpty(o.Id))
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                o.Kind + " requires id.",
                                ctx);
                        }
                        else if (producedFlags != null)
                        {
                            producedFlags.Add(ContentConditionEvaluator.EncounterFlag(o.Id));
                        }

                        break;
                    case "relationdelta":
                        ValidateCharacterReference(o.FromDefinitionId, registry, ctx + ".relation.from", report);
                        if (o.ToDefinitionIds.Count > 0)
                        {
                            for (var ti = 0; ti < o.ToDefinitionIds.Count; ti++)
                            {
                                var targetId = o.ToDefinitionIds[ti];
                                if (IsContextCharacterReference(targetId))
                                    continue;
                                RequireDef(registry, targetId, "character", ctx + ".relation.to", report);
                            }
                        }
                        else if (!string.IsNullOrEmpty(o.ToDefinitionId))
                        {
                            if (!IsContextCharacterReference(o.ToDefinitionId))
                                RequireDef(registry, o.ToDefinitionId, "character", ctx + ".relation.to", report);
                        }
                        else
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                "relationDelta requires toDefinitionId or toDefinitionIds.",
                                ctx);
                        }

                        break;
                }
            }
        }

        static bool IsContextCharacterReference(string value) =>
            string.Equals(value, "@party", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "@actor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "@target", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "@issuer", StringComparison.OrdinalIgnoreCase);

        static void ValidateCharacterReference(string value, DefinitionRegistry registry,
            string context, ValidationReport report)
        {
            if (IsContextCharacterReference(value)) return;
            RequireDef(registry, value, "character", context, report);
        }

        static void ValidateFlagConsumers(
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var flag in consumedFlags)
            {
                if (IsRuntimeProducedFlag(flag))
                    continue;
                if (!producedFlags.Contains(flag))
                {
                    report.Add(
                        ErrorCode.NotFound,
                        "Flag consumed but never produced by content outcomes／dayBeats.",
                        flag);
                }
            }
        }

        static bool IsRuntimeProducedFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
                return true;
            // Core exploration writes explored:<locationId>
            if (flag.StartsWith("explored:", StringComparison.Ordinal))
                return true;
            // setEncounterCleared writes encounter:<id>
            if (flag.StartsWith("encounter:", StringComparison.Ordinal))
                return true;
            // SupervisorPressureHandler writes this at day end.
            if (string.Equals(flag, "story:supervisor_pressure", StringComparison.Ordinal))
                return true;
            return false;
        }

        static void ValidateWorldSiteEconomies(DefinitionRegistry registry, ValidationReport report)
        {
            var siteIds = CollectAuthoredSiteIds(registry);
            var boundSites = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in registry.WorldSiteEconomies)
            {
                var economy = pair.Value;
                var context = pair.Key.ToString();
                if (economy == null || string.IsNullOrWhiteSpace(economy.SiteId) || !siteIds.Contains(economy.SiteId))
                    report.Add(ErrorCode.NotFound, "worldSiteEconomy.siteId must reference an authored WorldSite.", context);
                else if (!boundSites.Add(economy.SiteId))
                    report.Add(ErrorCode.DuplicateDefinitionId, "WorldSite has duplicate economy definitions.", economy.SiteId);
                var resources = new HashSet<string>(StringComparer.Ordinal);
                if (economy?.InitialPublicStock == null) continue;
                for (var i = 0; i < economy.InitialPublicStock.Count; i++)
                {
                    var entry = economy.InitialPublicStock[i];
                    if (entry == null || entry.Amount < 0 || string.IsNullOrWhiteSpace(entry.ResourceId) ||
                        !DefinitionId.TryParse(entry.ResourceId, out var resourceId) ||
                        !registry.Resources.ContainsKey(resourceId))
                        report.Add(ErrorCode.InvalidArgument, "Invalid worldSiteEconomy stock resource/amount.", context + "[" + i + "]");
                    else if (!resources.Add(entry.ResourceId))
                        report.Add(ErrorCode.DuplicateDefinitionId, "Duplicate worldSiteEconomy resource.", context + ":" + entry.ResourceId);
                }
            }
        }

        static HashSet<string> CollectAuthoredSiteIds(DefinitionRegistry registry)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in registry.OutdoorSurfaces)
                if (pair.Value?.SiteRegions != null && !pair.Value.AcceptanceOnly)
                    foreach (var region in pair.Value.SiteRegions)
                        if (!string.IsNullOrWhiteSpace(region?.SiteId)) ids.Add(region.SiteId);
            return ids;
        }

        static void RequireDef(
            DefinitionRegistry registry,
            string idText,
            string expectedKind,
            string context,
            ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(idText))
                return;
            if (!DefinitionId.TryParse(idText, out var id))
            {
                report.Add(ErrorCode.InvalidDefinitionId, "Invalid DefinitionId.", context + ":" + idText);
                return;
            }

            var ok = false;
            switch (expectedKind)
            {
                case "character":
                    ok = registry.Characters.ContainsKey(id);
                    break;
                case "quest":
                    ok = registry.Quests.ContainsKey(id);
                    break;
                case "contentEvent":
                    ok = registry.ContentEvents.ContainsKey(id);
                    break;
                case "chapter":
                    ok = registry.Chapters.ContainsKey(id);
                    break;
                case "openingScenario":
                    ok = registry.OpeningScenarios.ContainsKey(id);
                    break;
                case "worldRegion":
                    ok = registry.WorldRegions.ContainsKey(id);
                    break;
                case "localPlaceSet":
                    ok = registry.LocalPlaceSets.ContainsKey(id);
                    break;
                case "job":
                    ok = registry.Jobs.ContainsKey(id);
                    break;
                case "workArea":
                    ok = registry.WorkAreas.ContainsKey(id);
                    break;
                case "schedule":
                    ok = registry.Schedules.ContainsKey(id);
                    break;
                case "resource":
                    ok = registry.Resources.ContainsKey(id);
                    break;
                case "opportunitySite":
                    ok = registry.OpportunitySites.ContainsKey(id);
                    break;
                case "cultivation":
                    ok = registry.Cultivations.ContainsKey(id);
                    break;
                case "combatArt":
                    ok = registry.CombatArts.ContainsKey(id);
                    break;
                case "item":
                    ok = registry.Items.ContainsKey(id);
                    break;
                case "spawnTable":
                    ok = registry.SpawnTables.ContainsKey(id);
                    break;
                case "worldOpportunity":
                    ok = registry.WorldOpportunities.ContainsKey(id);
                    break;
                case "npcSquad":
                    ok = registry.NpcSquads.ContainsKey(id);
                    break;
            }

            if (!ok)
                report.Add(ErrorCode.NotFound, expectedKind + " reference missing.", context + ":" + idText);
        }
    }
}
