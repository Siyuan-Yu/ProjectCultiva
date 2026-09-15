using System;
using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Construction;
using XianXia.Core.Inventory;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Surface;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Maps quest／content-event definitions onto session boards.</summary>
    public static class ContentRuntimeBootstrap
    {
        public static Result Apply(SimulationWorld world, DefinitionRegistry registry, OpeningScenarioDefinition openingScenario = null)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ContentRuntime bootstrap args null.");

            RehydrateInventoryCatalog(world, registry);
            var inventory = OpeningInventoryBootstrap.Apply(world, openingScenario);
            if (inventory.IsFailure) return inventory;
            RehydrateSurfaceGround(world, registry);
            var assetAnchors = OutdoorAdministrativeAssetAnchorBootstrap.Rehydrate(world, registry);
            if (assetAnchors.IsFailure)
                return assetAnchors;
            RehydrateConstructionCatalog(world, registry);
            var fixedCores = RebindPresetWorldSiteCoreMetadata(world, registry);
            if (fixedCores.IsFailure)
                return fixedCores;
            var flagSites = XianXia.Core.World.Strategic.FactionFlagSiteCoreBootstrap
                .EnsureAuthoredSiteCores(world, true, ErrorCode.ContentLoadFailed);
            if (flagSites.IsFailure)
                return flagSites;
            var claims = XianXia.Core.World.Strategic.TerritoryClaimService
                .EstablishBaselineFromLegacy(world);
            if (claims.IsFailure)
                return claims;

            foreach (var kv in registry.Quests)
            {
                var def = kv.Value;
                var spec = new QuestSpec
                {
                    Id = def.Id.ToString(),
                    Name = def.Name ?? string.Empty,
                    Description = def.Description ?? string.Empty,
                    AutoOffer = def.AutoOffer,
                    Abandonable = def.Abandonable,
                    DeadlineDays = def.DeadlineDays
                };
                spec.OfferConditions.AddRange(def.OfferConditions);
                spec.CompleteConditions.AddRange(def.CompleteConditions);
                spec.FailConditions.AddRange(def.FailConditions);
                spec.Rewards.AddRange(def.Rewards);
                spec.FailResults.AddRange(def.FailResults);
                world.Quests.Register(spec);
            }

            foreach (var kv in registry.ContentEvents)
            {
                var def = kv.Value;
                var spec = new ContentEventSpec
                {
                    Id = def.Id.ToString(),
                    Name = def.Name ?? string.Empty,
                    Body = def.Body ?? string.Empty,
                    Trigger = def.Trigger ?? string.Empty,
                    LocationId = def.LocationId ?? string.Empty,
                    QuestId = def.QuestId ?? string.Empty,
                    NpcDefinitionId = def.NpcDefinitionId ?? string.Empty,
                    Once = def.Once
                };
                spec.Conditions.AddRange(def.Conditions);
                for (var i = 0; i < def.Choices.Count; i++)
                {
                    var c = def.Choices[i];
                    var choice = new ContentEventChoiceSpec
                    {
                        Id = c.Id ?? string.Empty,
                        Text = c.Text ?? string.Empty
                    };
                    choice.Conditions.AddRange(c.Conditions);
                    choice.Outcomes.AddRange(c.Outcomes);
                    spec.Choices.Add(choice);
                }

                world.ContentEvents.Register(spec);
            }

            return Result.Success();
        }

        /// <summary>
        /// 从静态内容恢复物品目录。背包槽位与数量不属于此处，不会被清空或重发。
        /// </summary>
        internal static void RehydrateInventoryCatalog(SimulationWorld world, DefinitionRegistry registry)
        {
            var catalog = world.InventoryCatalog;
            catalog.Clear();
            foreach (var kv in registry.Resources)
            {
                var r = kv.Value;
                var id = r.Id.ToString();
                var tags = new List<string> { "resource" };
                AppendHeuristicTags(id, tags);
                catalog.Register(id, r.Name ?? id, 99, tags);
            }

            foreach (var kv in registry.Items)
            {
                var item = kv.Value;
                var id = item.Id.ToString();
                catalog.Register(
                    id, item.Name ?? id, item.MaxStack, item.Tags,
                    item.TeachesManualId, item.TeachesArtId);
            }

            world.Inventory.SetSlotCapacity(PartyInventory.DefaultSlotCapacity);
        }

        internal static void RehydrateConstructionCatalog(SimulationWorld world, DefinitionRegistry registry)
        {
            world.Strategic.SpatialRules = registry.SpatialRules;
            world.ConstructionCatalog.Clear();
            foreach (var kv in registry.Buildings)
            {
                var definition = kv.Value;
                if (definition == null)
                    continue;
                var placementKind = definition.PlacementKind == "factionFlag" ? ConstructionPlacementKind.FactionFlag :
                    definition.PlacementKind == "farmField" ? ConstructionPlacementKind.FarmField :
                    throw new System.InvalidOperationException("Unknown construction placementKind: " + definition.PlacementKind);
                var spec = new BuildingConstructionSpec
                {
                    BuildingId = definition.Id.ToString(),
                    DisplayName = string.IsNullOrWhiteSpace(definition.Name) ? definition.Id.ToString() : definition.Name,
                    Description = definition.Description ?? string.Empty,
                    UnlockedByDefault = definition.UnlockedByDefault,
                    PlacementKind = placementKind,
                    OutdoorKind = definition.OutdoorKind,
                    FootprintCellsW = definition.FootprintCellsW,
                    FootprintCellsH = definition.FootprintCellsH,
                    CreatesWorldSite = definition.CreatesWorldSite,
                    CreatedSiteName = definition.CreatedSiteName ?? string.Empty,
                    CreatedSiteType = definition.CreatedSiteType ?? string.Empty,
                    InitialSiteLevel = definition.InitialSiteLevel,
                    DismantleRefundRate = definition.DismantleRefundRate
                };
                for (var i = 0; i < definition.Costs.Count; i++)
                {
                    var cost = definition.Costs[i];
                    spec.Costs.Add(new ConstructionMaterialCost { ItemId = cost.ItemId, Count = cost.Count });
                }
                world.ConstructionCatalog.Register(spec);
            }
        }

        internal static void RehydrateSurfaceGround(SimulationWorld world, DefinitionRegistry registry)
        {
            world.SurfaceSpatial.Clear();
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface == null || surface.AcceptanceOnly) continue;
                var chunks = new List<SurfaceChunkCoord>();
                if (surface.Chunks != null)
                    for (var i = 0; i < surface.Chunks.Count; i++)
                        if (surface.Chunks[i] != null) chunks.Add(surface.Chunks[i].Coord);
                world.SurfaceSpatial.Register(new OutdoorSurfaceSpatialMetric(
                    surface.SurfaceId,
                    surface.OriginWorldX,
                    surface.OriginWorldY,
                    surface.CellSize,
                    surface.ChunkWidth,
                    surface.ChunkHeight,
                    chunks));
            }
            world.SurfaceGround.ClearRegistered();
            foreach (var pair in registry.OutdoorSurfaceGeographies)
                world.SurfaceGround.Register(pair.Value?.Navigation);
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface?.SiteRegions == null) continue;
                for (var i = 0; i < surface.SiteRegions.Count; i++)
                {
                    var region = surface.SiteRegions[i];
                    if (region == null) continue;
                    world.SurfaceGround.RegisterSiteArrival(
                        string.IsNullOrWhiteSpace(region.SurfaceId)
                            ? surface.SurfaceId : region.SurfaceId,
                        region.SiteId,
                        new XianXia.Core.World.Hex.WorldVec2(
                            region.ArrivalWorldX, region.ArrivalWorldY));
                }
            }
            XianXia.Core.World.Strategic.FormalArmyContinuousTravelService
                .RebindPendingSurfaceRoutes(world);
        }

        public static Result RebindPresetWorldSiteCoreMetadata(
            SimulationWorld world, DefinitionRegistry registry)
        {
            if (world?.Strategic?.Sites == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Preset SiteCore binding requires world and registry.");

            var rows = new List<PresetControlCoreBinding>();
            var sites = new HashSet<string>(StringComparer.Ordinal);
            var workAreas = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface?.SitePlacements == null || surface.AcceptanceOnly) continue;
                for (var i = 0; i < surface.SitePlacements.Count; i++)
                {
                    var placement = surface.SitePlacements[i];
                    if (placement == null ||
                        !string.Equals(placement.Kind, "controlCore", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var context = "Surface=" + (surface.SurfaceId ?? string.Empty) + " Placement=" +
                                  (placement.StableId ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(placement.StableId) ||
                        string.IsNullOrWhiteSpace(placement.SiteId) ||
                        string.IsNullOrWhiteSpace(placement.BoundLocationId))
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "Outdoor controlCore requires stableId, siteId and boundLocationId.", context);
                    if (!world.Strategic.Sites.TryGet(placement.SiteId, out var site) || site == null || site.IsRuntimeCreated)
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "Outdoor controlCore references an unavailable authored WorldSite.", context);
                    if (site.CoreIsRemovable)
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "Outdoor controlCore cannot bind a removable WorldSite.", context);
                    if (!world.ControlCores.TryGetByLocation(placement.BoundLocationId, out var core) || core == null)
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "Outdoor controlCore boundLocationId has no ControlCore WorkArea.", context);
                    if (!sites.Add(site.SiteId))
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "Authored fixed WorldSite has more than one controlCore placement.", site.SiteId);
                    if (!workAreas.Add(core.WorkAreaId))
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "ControlCore WorkArea is bound by more than one authored WorldSite.", core.WorkAreaId);
                    if ((!string.IsNullOrEmpty(site.CoreAssetId) &&
                         !string.Equals(site.CoreAssetId, placement.StableId, StringComparison.Ordinal)) ||
                        (!string.IsNullOrEmpty(site.CoreSurfaceId) &&
                         !string.Equals(site.CoreSurfaceId, surface.SurfaceId, StringComparison.Ordinal)))
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "Authored WorldSite core metadata conflicts with its controlCore placement.", context);
                    var binding = CaptureObjectiveService.ValidateControlCoreWorldSiteBinding(
                        world, core.WorkAreaId, site.SiteId);
                    if (binding.IsFailure)
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "Outdoor controlCore canonical binding is invalid.", binding.Error.ToString());
                    try
                    {
                        registry.SpatialRules.ResolveLevel(world, 1, surface.SurfaceId);
                    }
                    catch (Exception e)
                    {
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "Outdoor controlCore range cannot be resolved.", context + " " + e.Message);
                    }
                    rows.Add(new PresetControlCoreBinding(surface, placement, site, core.WorkAreaId));
                }
            }

            // All content relationships and spatial ranges were validated above. From here each
            // mutation is deterministic and cannot depend on the current WorldRegion/LocalMap.
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var bound = CaptureObjectiveService.BindControlCoreToWorldSite(
                    world, row.WorkAreaId, row.Site.SiteId);
                if (bound.IsFailure)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Outdoor controlCore canonical binding failed.", bound.Error.ToString());
                var placement = row.Placement;
                var site = row.Site;
                var surface = row.Surface;
                    site.CoreAssetId = placement.StableId ?? string.Empty;
                    site.CoreSurfaceId = surface.SurfaceId ?? string.Empty;
                    site.HasCoreWorldPosition = true;
                    site.CoreWorldX = placement.WorldX + placement.WorldWidth * .5f;
                    site.CoreWorldY = placement.WorldY + placement.WorldHeight * .5f;
                    site.CoreLevel = 1;
                    registry.SpatialRules.Bind(world, site);
                    site.IsCoreActive = true;
                    site.CoreIsRemovable = false;
            }
            return Result.Success();
        }

        sealed class PresetControlCoreBinding
        {
            public PresetControlCoreBinding(OutdoorWorldSurfaceDefinition surface,
                OutdoorSurfacePlacementDefinition placement, WorldSite site, string workAreaId)
            { Surface = surface; Placement = placement; Site = site; WorkAreaId = workAreaId; }
            public OutdoorWorldSurfaceDefinition Surface { get; }
            public OutdoorSurfacePlacementDefinition Placement { get; }
            public WorldSite Site { get; }
            public string WorkAreaId { get; }
        }

        static void AppendHeuristicTags(string id, List<string> tags)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (id.IndexOf("wood", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tags.Add("wood");
            if (id.IndexOf("herb", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tags.Add("herb");
            if (id.IndexOf("grain", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tags.Add("grain");
            if (id.IndexOf("conceal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                tags.Add("consumable");
        }
    }
}
