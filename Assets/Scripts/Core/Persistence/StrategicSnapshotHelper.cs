using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>??? Snapshot v6 ??????Pure Hex?? node/route DTO??</summary>
    public static class StrategicSnapshotHelper
    {
        /// <summary>Current-version saved personal spatial state must survive restore unchanged.</summary>
        public static Result ValidateRestoredCharacterWorldPresences(
            SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world == null || dto?.CharacterWorldPresences == null)
                return Result.Failure(ErrorCode.SnapshotInvalid, "Character world presence snapshot missing.");
            var seen = new HashSet<ulong>();
            foreach (var saved in dto.CharacterWorldPresences)
            {
                if (saved == null || saved.CharacterId == 0 || !seen.Add(saved.CharacterId))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Invalid or duplicate CharacterWorldPresence snapshot.");
                var id = new EntityId(saved.CharacterId);
                // Separate Space local placement supersedes a stale pre-SPACE-01 Outdoor DTO.
                // Rehydration removes that personal presence instead of teleporting the Character.
                if (SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, id))
                    continue;
                if (!world.Entities.TryGet(id, out _) ||
                    !world.WorldPresence.TryGet(id, out var actual) || actual == null ||
                    (int)actual.Mode != saved.Mode ||
                    !string.Equals(actual.SiteId, saved.SiteId ?? string.Empty, StringComparison.Ordinal) ||
                    !string.Equals(actual.PersonalSurfaceId, saved.PersonalSurfaceId ?? string.Empty,
                        StringComparison.Ordinal) ||
                    actual.HasContinuousWorldPosition != saved.HasWorldPosition ||
                    (saved.HasWorldPosition &&
                     (float.IsNaN(saved.WorldX) || float.IsInfinity(saved.WorldX) ||
                      float.IsNaN(saved.WorldY) || float.IsInfinity(saved.WorldY) ||
                      Math.Abs(actual.WorldPosX - saved.WorldX) > 0.0001f ||
                      Math.Abs(actual.WorldPosY - saved.WorldY) > 0.0001f)))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Restored CharacterWorldPresence differs from snapshot.",
                        "CharacterId=" + saved.CharacterId);
            }
            return Result.Success();
        }

        /// <summary>
        /// 静态 Hex／Site／Territory shell 建立后覆盖当前政治状态。
        /// Restore 本身不是新 Capture：不得发事件、重置 Core 或重套开局。
        /// </summary>
        public static Result RestoreHexPoliticalState(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic == null || dto == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Political snapshot restore requires world and dto.");

            // Flag active set 先完整验证并原子提交；失败时不允许继续覆盖其它政治状态。
            var flags = FactionFlagSnapshotRestore.TryApplyAuthoritativeSet(world, dto);
            if (flags.IsFailure)
                return flags;

            // Snapshots predating both flag and runtime-Site authority inherit the current
            // authored flag set. Mark those flags so their baseline claims may be added to
            // an otherwise authoritative legacy claim history below.
            if (!dto.HasFactionFlagSnapshotAuthority && !dto.HasRuntimeWorldSiteSnapshotAuthority)
                MarkAuthoredFlagsForLegacyBaselineMigration(world);

            var runtimeSites = RestoreRuntimeWorldSites(world, dto);
            if (runtimeSites.IsFailure)
                return runtimeSites;
            if (!dto.HasRuntimeWorldSiteSnapshotAuthority)
                world.Strategic.Sites.RemoveAllRuntimeSites();
            var allowLegacySiteCreation = HasAuthoredFlagLegacyMigration(world) ||
                (!dto.HasRuntimeWorldSiteSnapshotAuthority && !dto.HasFactionFlagSnapshotAuthority);
            var authoredFlagSites = FactionFlagSiteCoreBootstrap.EnsureAuthoredSiteCores(
                world, allowLegacySiteCreation, ErrorCode.SnapshotInvalid);
            if (authoredFlagSites.IsFailure)
                return authoredFlagSites;

            if (dto.WorldSiteOwners != null)
            {
                for (var i = 0; i < dto.WorldSiteOwners.Count; i++)
                {
                    var site = dto.WorldSiteOwners[i];
                    if (site == null || string.IsNullOrEmpty(site.SiteId))
                        continue;
                    if (site.CoreMetadataFormat != 0)
                    {
                        if (site.CoreMetadataFormat != 1 || site.CoreLevel < 1 ||
                            !IsFinite(site.CoreWorldX) || !IsFinite(site.CoreWorldY) ||
                            !world.Strategic.Sites.TryGet(site.SiteId, out var coreSite) ||
                            string.IsNullOrEmpty(site.CoreAssetId) || site.CoreAssetId != coreSite.CoreAssetId ||
                            string.IsNullOrEmpty(site.CoreSurfaceId) || site.CoreSurfaceId != coreSite.CoreSurfaceId)
                            return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid Site core metadata: " + site.SiteId);
                        try { world.Strategic.SpatialRules.ResolveLevel(world, site.CoreLevel, site.CoreSurfaceId); }
                        catch (InvalidOperationException e) { return Result.Failure(ErrorCode.SnapshotInvalid, e.Message); }
                        coreSite.CoreLevel = site.CoreLevel;
                        coreSite.CoreWorldX = site.CoreWorldX; coreSite.CoreWorldY = site.CoreWorldY;
                        coreSite.HasCoreWorldPosition = true; coreSite.IsCoreActive = site.CoreActive;
                        world.Strategic.SpatialRules.Bind(world, coreSite);
                    }
                    WorldSiteOwnershipService.SetOwner(world, site.SiteId, site.OwnerFactionId ?? string.Empty);
                }
            }

            var claims = RestoreTerritoryClaims(world, dto);
            if (claims.IsFailure)
                return claims;

            var publicStocks = RestoreWorldSitePublicStocks(world, dto);
            if (publicStocks.IsFailure)
                return publicStocks;

            // Legacy TerritoryRegionControllers are intentionally ignored. Region/Hex control is
            // a pure projection rebuilt from Site Owner + exact administrative Claim authority.
            StrategicTerritoryCoverageResolver.Rebuild(world);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || string.IsNullOrEmpty(site.TerritoryRegionId) ||
                    !world.Strategic.TerritoryRegions.TryGet(site.TerritoryRegionId, out var region) ||
                    region == null)
                    continue;
                if (!string.Equals(site.OwnerFactionId ?? string.Empty,
                        region.ControlFactionId ?? string.Empty, StringComparison.Ordinal))
                {
                    System.Diagnostics.Debug.Fail("[TerritoryRestore] Site/Region controller mismatch: " +
                        site.SiteId + ".");
                }
            }
#endif
            return CharacterEncounterService.ValidateObjectiveWorldState(world);
        }

        public static StrategicSnapshotDto Capture(SimulationWorld world, PlayerPartyRuntime party = null)
        {
            var dto = new StrategicSnapshotDto
            {
                PlayerFactionId = world?.Strategic?.PlayerFactionId ?? string.Empty,
                Ch01FormationScenarioCompat = world?.Strategic?.Ch01FormationScenarioCompat ?? false
            };
            if (world?.Strategic == null)
                return dto;

            dto.HasSquadSnapshotAuthority = true;
            dto.ControlledSquadId = party?.ControlledSquadId ?? string.Empty;
            foreach (var pair in world.Strategic.Squads.Squads)
            {
                var squad = pair.Value;
                if (squad == null) continue;
                var squadDto = new SquadSnapshotDto
                {
                    SquadId = squad.SquadId,
                    LeaderCharacterId = squad.LeaderCharacterId.Value,
                    LegacyArmyId = squad.LegacyArmyId,
                    CommandKind = (int)squad.CommandKind,
                    CommandRevision = squad.CommandRevision,
                    CommandTargetCharacterId = squad.CommandTargetCharacterId.Value
                };
                for (var i = 0; i < squad.MemberCharacterIds.Count; i++) squadDto.MemberCharacterIds.Add(squad.MemberCharacterIds[i]);
                dto.Squads.Add(squadDto);
            }

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null)
                    continue;
                var armyMotion = army.WorldMotion;
                var armyDto = new FormalArmySnapshotDto
                {
                    ArmyId = army.ArmyId,
                    FactionId = army.FactionId,
                    LeaderCharacterId = army.LeaderCharacterId.Value,
                    State = (int)army.State,
                    UsesHexStrategicPosition = army.UsesHexStrategicPosition,
                    CurrentHexQ = armyMotion.CurrentHex.Q,
                    CurrentHexR = armyMotion.CurrentHex.R,
                    DestinationHexQ = armyMotion.DestinationHex.Q,
                    DestinationHexR = armyMotion.DestinationHex.R,
                    StepProgress = army.StepProgress,
                    StepRemainingTicks = army.StepRemainingTicks,
                    StepTotalTicks = army.StepTotalTicks,
                    CurrentPathIndex = army.CurrentPathIndex,
                    LocationKind = (int)armyMotion.LocationKind,
                    SiteId = armyMotion.SiteId ?? string.Empty,
                    WorldX = armyMotion.WorldPosition.X,
                    WorldY = armyMotion.WorldPosition.Y,
                    DestinationSiteId = armyMotion.DestinationSiteId ?? string.Empty,
                    CurrentOrderKind = (int)armyMotion.CurrentOrderKind,
                    OrderTargetArmyId = armyMotion.OrderTargetArmyId ?? string.Empty,
                    SegmentProgress = armyMotion.SegmentProgress,
                    SegmentIndex = armyMotion.SegmentIndex,
                    TravelMode = (int)armyMotion.TravelMode,
                    HasSiteDepartureState = true,
                    IsSiteDeparturePending = armyMotion.IsSiteDeparturePending,
                    SiteDepartureVirtualX = armyMotion.SiteDepartureVirtualPosition.X,
                    SiteDepartureVirtualY = armyMotion.SiteDepartureVirtualPosition.Y,
                    SiteDepartureBoundaryX = armyMotion.SiteDepartureBoundaryEntry.X,
                    SiteDepartureBoundaryY = armyMotion.SiteDepartureBoundaryEntry.Y,
                    SiteDepartureFootprintQ = armyMotion.SiteDepartureFootprintHex.Q,
                    SiteDepartureFootprintR = armyMotion.SiteDepartureFootprintHex.R,
                    SiteDepartureExitQ = armyMotion.SiteDepartureExitHex.Q,
                    SiteDepartureExitR = armyMotion.SiteDepartureExitHex.R,
                    RouteKind = (int)armyMotion.RouteKind,
                    SurfaceId = armyMotion.SurfaceId ?? string.Empty,
                    SurfaceSourceRevision = armyMotion.SurfaceSourceRevision ?? string.Empty,
                    SurfaceSourceHash = armyMotion.SurfaceSourceHash ?? string.Empty,
                    PhysicalDestinationX = armyMotion.PhysicalDestination.X,
                    PhysicalDestinationY = armyMotion.PhysicalDestination.Y,
                    SurfaceWaypointIndex = armyMotion.SurfaceWaypointIndex,
                    RouteDiagnostic = armyMotion.RouteDiagnostic ?? string.Empty,
                };
                for (var p = 0; p < armyMotion.HexPathCount; p++)
                {
                    var coord = armyMotion.HexPath[p];
                    armyDto.HexPath.Add(new HexCoordSnapshotDto { Q = coord.Q, R = coord.R });
                }
                for (var p = 0; p < armyMotion.SurfacePathCount; p++)
                {
                    var point = armyMotion.SurfacePath[p];
                    armyDto.SurfacePath.Add(new WorldPointSnapshotDto { X = point.X, Y = point.Y });
                }
                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                    armyDto.MemberCharacterIds.Add(army.MemberCharacterIds[i]);
                dto.FormalArmies.Add(armyDto);
            }

            foreach (var entity in world.Entities.All)
            {
                if (entity == null || !entity.TryGet<ArmyMembershipComponent>(out var mem) ||
                    string.IsNullOrEmpty(mem.ArmyId))
                    continue;
                if (!world.Strategic.FormalArmies.TryGet(mem.ArmyId, out var membershipArmy) ||
                    membershipArmy == null || !membershipArmy.ContainsMember(entity.Id))
                {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
                    System.Diagnostics.Debug.Fail("[SnapshotCapture] Orphan ArmyMembership CharacterId=" +
                        entity.Id.Value + " ArmyId='" + mem.ArmyId + "'.");
#endif
                    continue;
                }
                dto.ArmyMemberships.Add(new ArmyMembershipSnapshotDto
                {
                    CharacterId = entity.Id.Value,
                    ArmyId = mem.ArmyId
                });
            }

            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || !presence.UsesHexPresence)
                    continue;
                if (!StrategicResidualPresenceService.IsResidualLifeCandidate(world, presence.EntityId))
                    continue;
                dto.ResidualCharacterPresences.Add(new ResidualCharacterPresenceDto
                {
                    CharacterId = presence.EntityId.Value,
                    HexQ = presence.HexQ,
                    HexR = presence.HexR
                });
            }

            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || presence.EntityId.IsNone)
                    continue;
                if (presence.Mode == PartyWorldPresenceMode.InEncounter)
                    continue;
                if (presence.Mode == PartyWorldPresenceMode.AtSite)
                {
                    if (string.IsNullOrEmpty(presence.SiteId))
                        continue;
                    dto.CharacterWorldPresences.Add(new CharacterWorldPresenceSnapshotDto
                    {
                        CharacterId = presence.EntityId.Value,
                        Mode = (int)PartyWorldPresenceMode.AtSite,
                        PersonalSurfaceId = presence.PersonalSurfaceId,
                        SiteId = presence.SiteId,
                        // AtSite 也可携带 authored／baked 精确锤点（Opening LocalPosition → canonical）；
                        // 无锚点时 HasWorldPosition=false，保持纯 Site 语义（旧存档一致）。
                        HasWorldPosition = presence.HasContinuousWorldPosition,
                        WorldX = presence.WorldPosX,
                        WorldY = presence.WorldPosY
                    });
                    continue;
                }

                if (presence.UsesHexPresence)
                {
                    dto.CharacterWorldPresences.Add(new CharacterWorldPresenceSnapshotDto
                    {
                        CharacterId = presence.EntityId.Value,
                        Mode = (int)PartyWorldPresenceMode.AtHex,
                        PersonalSurfaceId = presence.PersonalSurfaceId,
                        HexQ = presence.HexQ,
                        HexR = presence.HexR,
                        HasWorldPosition = presence.HasContinuousWorldPosition,
                        WorldX = presence.WorldPosX,
                        WorldY = presence.WorldPosY
                    });
                    continue;
                }

                if (presence.Mode == PartyWorldPresenceMode.AtWorldPosition &&
                    presence.HasContinuousWorldPosition)
                {
                    dto.CharacterWorldPresences.Add(new CharacterWorldPresenceSnapshotDto
                    {
                        CharacterId = presence.EntityId.Value,
                        Mode = (int)PartyWorldPresenceMode.AtWorldPosition,
                        PersonalSurfaceId = presence.PersonalSurfaceId,
                        HexQ = presence.HexQ,
                        HexR = presence.HexR,
                        HasWorldPosition = true,
                        WorldX = presence.WorldPosX,
                        WorldY = presence.WorldPosY
                    });
                }
            }

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null || string.IsNullOrEmpty(site.SiteId))
                    continue;
                dto.WorldSiteOwners.Add(new WorldSiteOwnerSnapshotDto
                {
                    SiteId = site.SiteId,
                    OwnerFactionId = site.OwnerFactionId ?? string.Empty,
                    CoreMetadataFormat = site.HasContinuousCore ? 1 : 0,
                    CoreAssetId = site.CoreAssetId, CoreSurfaceId = site.CoreSurfaceId,
                    CoreLevel = site.CoreLevel, CoreWorldX = site.CoreWorldX, CoreWorldY = site.CoreWorldY,
                    CoreActive = site.IsCoreActive
                });
            }

            dto.HasRuntimeWorldSiteSnapshotAuthority = true;
            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null || !site.IsRuntimeCreated) continue;
                dto.RuntimeWorldSites.Add(new RuntimeWorldSiteSnapshotDto
                {
                    CoreLevelFormat = 1,
                    SiteId = site.SiteId,
                    DisplayName = site.DisplayName,
                    SiteType = site.SiteType,
                    OwnerFactionId = site.OwnerFactionId,
                    ControlEstablishedOrder = site.ControlEstablishedOrder,
                    AnchorQ = site.AnchorHex.Q,
                    AnchorR = site.AnchorHex.R,
                    CoreAssetId = site.CoreAssetId,
                    SurfaceId = site.CoreSurfaceId,
                    HasWorldPosition = site.HasCoreWorldPosition,
                    WorldX = site.CoreWorldX,
                    WorldY = site.CoreWorldY,
                    CoreLevel = site.CoreLevel,
                    IsCoreActive = site.IsCoreActive,
                    CoreIsRemovable = site.CoreIsRemovable
                });
            }

            dto.HasTerritoryClaimSnapshotAuthority = world.Strategic.TerritoryClaims.HasAuthority;
            foreach (var claim in world.Strategic.TerritoryClaims.Claims)
            {
                dto.TerritoryClaims.Add(new TerritoryClaimSnapshotDto
                {
                    FormatVersion = 3,
                    ClaimId = claim.ClaimId,
                    SiteId = claim.SiteId,
                    SurfaceId = claim.SurfaceId,
                    AcquiredOrder = claim.AcquiredOrder,
                    CenterX = claim.CenterX,
                    CenterY = claim.CenterY,
                    Width = claim.Width,
                    Height = claim.Height
                });
            }

            dto.HasFactionFlagSnapshotAuthority = true;
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value; if (flag == null) continue;
                dto.FactionFlags.Add(new FactionFlagSnapshotDto { SiteCoreFormat=1,
                    FlagId=flag.FlagId, FactionId=flag.FactionId,
                    AnchorQ=flag.AnchorHex.Q, AnchorR=flag.AnchorHex.R, EstablishedOrder=flag.EstablishedOrder,
                    CurrentHp=flag.CurrentHp, MaxHp=flag.MaxHp, HasLocalPosition=flag.HasLocalPosition, LocalX=flag.LocalX, LocalZ=flag.LocalZ,
                    HasWorldPosition=flag.HasWorldPosition, WorldX=flag.WorldX, WorldY=flag.WorldY,
                    SiteId=flag.SiteId, SurfaceId=flag.SurfaceId, IsSiteCore=flag.IsSiteCore });
            }
            FactionFlagSnapshotRestore.LogDtos("FlagSnapshotCapture", dto.FactionFlags);

            foreach (var war in world.Strategic.Wars.EnumerateActive())
            {
                var warDto = new WarSnapshotDto
                {
                    WarId = war.WarId,
                    Active = war.Active
                };
                foreach (var a in war.Attackers)
                    warDto.Attackers.Add(a);
                foreach (var d in war.Defenders)
                    warDto.Defenders.Add(d);
                dto.Wars.Add(warDto);
            }

            foreach (var kv in world.Strategic.Alliances.All)
            {
                dto.Alliances.Add(new AllianceSnapshotDto
                {
                    AllianceId = kv.Key,
                    Members = new List<string>(kv.Value)
                });
            }

            foreach (var kv in world.Strategic.Vassalages.All)
            {
                dto.Vassalages.Add(new VassalageSnapshotDto
                {
                    VassalFactionId = kv.Key,
                    OverlordFactionId = kv.Value
                });
            }

            foreach (var kv in world.Strategic.RetreatingArmies.All)
            {
                var retreat = kv.Value;
                if (retreat == null)
                    continue;
                var rDto = new RetreatingArmySnapshotDto
                {
                    RetreatingArmyId = retreat.RetreatingArmyId,
                    SourceArmyId = retreat.SourceArmyId,
                    FactionId = retreat.FactionId,
                    HexQ = retreat.UsesHexPosition ? retreat.HexQ : int.MinValue,
                    HexR = retreat.UsesHexPosition ? retreat.HexR : int.MinValue
                };
                for (var i = 0; i < retreat.MemberCharacterIds.Count; i++)
                    rDto.MemberCharacterIds.Add(retreat.MemberCharacterIds[i]);
                dto.RetreatingArmies.Add(rDto);
            }

            dto.HasControlCoreSnapshotAuthority = true;
            foreach (var kv in world.ControlCores.All)
            {
                var core = kv.Value;
                if (core == null)
                    continue;
                dto.ControlCores.Add(new ControlCoreRuntimeSnapshotDto
                {
                    WorkAreaId = core.WorkAreaId,
                    CurrentDurability = core.CurrentDurability,
                    OccupyProgressSeconds = core.OccupyProgressSeconds
                });
            }

            dto.HasWorldSitePublicStockSnapshotAuthority = true;
            var stockSiteIds = new List<string>(world.Strategic.Sites.Sites.Keys);
            stockSiteIds.Sort(StringComparer.Ordinal);
            for (var i = 0; i < stockSiteIds.Count; i++)
            {
                var state = world.Strategic.SitePublicStocks.GetOrCreate(stockSiteIds[i]);
                var stockDto = new WorldSitePublicStockSnapshotDto { SiteId = state.SiteId };
                var resourceIds = new List<string>(state.Resources.Keys);
                resourceIds.Sort(StringComparer.Ordinal);
                for (var r = 0; r < resourceIds.Count; r++)
                    stockDto.Entries.Add(new WorldSitePublicStockEntrySnapshotDto
                    {
                        ResourceId = resourceIds[r], Amount = state.Resources[resourceIds[r]]
                    });
                dto.WorldSitePublicStocks.Add(stockDto);
            }

            var motion = world.PlayerPartyTravel;
            if (motion != null && motion.HasPosition)
            {
                dto.PlayerPartyTravel = new PlayerPartyTravelSnapshotDto
                {
                    HasPosition = true,
                    LocationKind = (int)motion.LocationKind,
                    SiteId = motion.SiteId ?? string.Empty,
                    WorldX = motion.WorldPosition.X,
                    WorldY = motion.WorldPosition.Y,
                    CurrentHexQ = motion.CurrentHex.Q,
                    CurrentHexR = motion.CurrentHex.R,
                    IsMoving = motion.IsMoving,
                    HasContinuousPhysicalDestination = motion.HasContinuousPhysicalDestination,
                    DestinationWorldX = motion.ContinuousPhysicalDestination.X,
                    DestinationWorldY = motion.ContinuousPhysicalDestination.Y,
                    ArrivalRadius = motion.ContinuousPhysicalArrivalRadius,
                    DestinationSiteId = motion.DestinationSiteId ?? string.Empty,
                    ExecutionMode = (int)motion.ExecutionMode
                };
            }

            dto.BackgroundCharacterTravels.Clear();
            if (world.BackgroundCharacterTravel?.All != null)
            {
                foreach (var kv in world.BackgroundCharacterTravel.All)
                {
                    var id = new EntityId(kv.Key);
                    if (id.IsNone || kv.Value == null)
                        continue;
                    if (!BackgroundCharacterTravelService.TryResolveCharacterWorldLocation(
                            world, id, out var kind, out var siteId, out var pos, out var derived))
                        continue;

                    var snap = new BackgroundCharacterTravelSnapshotDto
                    {
                        CharacterId = id.Value,
                        IsSurfaceRoute = kv.Value.IsSurfaceRoute,
                        SurfaceId = kv.Value.SurfaceId ?? string.Empty,
                        SurfaceDestinationX = kv.Value.SurfaceDestination.X,
                        SurfaceDestinationY = kv.Value.SurfaceDestination.Y,
                        LocationKind = (int)kind,
                        SiteId = siteId ?? string.Empty,
                        WorldX = pos.X,
                        WorldY = pos.Y,
                        CurrentHexQ = derived.Q,
                        CurrentHexR = derived.R,
                        IsTraveling = kv.Value.IsMoving,
                        DestinationHexQ = kv.Value.DestinationHex.Q,
                        DestinationHexR = kv.Value.DestinationHex.R,
                        DestinationSiteId = kv.Value.DestinationSiteId ?? string.Empty,
                        SegmentIndex = kv.Value.SegmentIndex,
                        SegmentProgress = kv.Value.SegmentProgress,
                        LastProcessedWorldTick = kv.Value.LastProcessedWorldTick
                    };
                    if (kv.Value.IsMoving)
                    {
                        var path = kv.Value.HexPath;
                        for (var pi = 0; pi < path.Count; pi++)
                        {
                            snap.HexPath.Add(new HexCoordSnapshotDto
                            {
                                Q = path[pi].Q,
                                R = path[pi].R
                            });
                        }
                    }

                    dto.BackgroundCharacterTravels.Add(snap);
                }
            }

            PlayerPartySnapshotRestore.Capture(party, dto);
            LoadedLocalMapPlacementSnapshotRestore.Capture(world, dto);
            SeparateSpaceSessionSnapshotRestore.Capture(world, dto, party);
            PendingEngagementSnapshotRestore.Capture(world, dto);
            return dto;
        }

        public static Result Restore(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic == null || dto == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Strategic snapshot restore requires world and dto.");

            var squadValidation = ValidateSquadAuthority(world, dto);
            if (squadValidation.IsFailure) return squadValidation;

            var armyIds = new HashSet<string>(StringComparer.Ordinal);
            if (dto.FormalArmies != null)
                for (var i = 0; i < dto.FormalArmies.Count; i++)
                {
                    var item = dto.FormalArmies[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.ArmyId))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "FormalArmy snapshot has empty identity.", "Index=" + i);
                    if (!armyIds.Add(item.ArmyId))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "FormalArmy snapshot has duplicate ArmyId.", item.ArmyId);
                }

            world.Strategic.PlayerFactionId = dto.PlayerFactionId ?? string.Empty;
            world.Strategic.Ch01FormationScenarioCompat = dto.Ch01FormationScenarioCompat;
            world.Strategic.FormalArmies.Clear();
            world.Strategic.Squads.Clear();
            world.Strategic.Wars.Clear();
            world.Strategic.Diplomacy.Clear();
            world.Strategic.Alliances.Clear();
            world.Strategic.Vassalages.Clear();
            world.Strategic.RetreatingArmies.Clear();
            world.ControlCores.PrepareRuntimeRestore();

            if (dto.HasSquadSnapshotAuthority && dto.Squads != null)
            {
                for (var i = 0; i < dto.Squads.Count; i++)
                {
                    var item = dto.Squads[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.SquadId))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Squad snapshot has empty identity.");
                    var members = new List<EntityId>();
                    for (var m = 0; m < item.MemberCharacterIds.Count; m++) members.Add(new EntityId(item.MemberCharacterIds[m]));
                    var created = SquadMembershipService.Create(world, item.SquadId, members,
                        new EntityId(item.LeaderCharacterId), item.LegacyArmyId, (SquadCommandKind)item.CommandKind,
                        importingSnapshot: true);
                    if (created.IsFailure) return Result.Failure(created.Error);
                    created.Value.CommandRevision = item.CommandRevision;
                    created.Value.CommandTargetCharacterId = new EntityId(item.CommandTargetCharacterId);
                }
            }

            if (dto.FormalArmies != null && dto.FormalArmies.Count > 0)
            {
                using (FormalArmyStrategicMutationDiagnostics.Scope(
                           FormalArmyStrategicMutationDiagnostics.MutationAllowance.SnapshotLoad,
                           nameof(Restore)))
                {
                    for (var i = 0; i < dto.FormalArmies.Count; i++)
                    {
                        var a = dto.FormalArmies[i];
                        if (a == null || string.IsNullOrEmpty(a.ArmyId))
                            continue;
                        var army = BuildFormalArmyFromSnapshot(world, a, dto.HasSquadSnapshotAuthority);
                        world.Strategic.FormalArmies.Register(army);
                    }
                }
            }

            var memberships = dto.HasSquadSnapshotAuthority
                ? RestoreDerivedArmyMemberships(world)
                : RestoreArmyMemberships(world, dto.ArmyMemberships);
            if (memberships.IsFailure)
                return memberships;

            // CharacterWorldPresences 是新版 authority（可携带 precise WorldPosition）；
            // 恢复时记录已恢复 id —— 旧 ResidualCharacterPresences 只作 legacy fallback，
            // 不覆盖新版（否则 SetAtHex 会把 HasContinuousWorldPosition 清掉）。
            var restoredCharacterWorldPresenceIds = new HashSet<ulong>();
            if (dto.CharacterWorldPresences != null)
            {
                for (var i = 0; i < dto.CharacterWorldPresences.Count; i++)
                {
                    var p = dto.CharacterWorldPresences[i];
                    if (p == null || p.CharacterId == 0)
                        continue;
                    var id = new EntityId(p.CharacterId);
                    if (!world.Entities.TryGet(id, out _))
                    {
                        LogCharacterRestoreSkip(p.CharacterId, string.Empty, "character not present in restored entities");
                        continue;
                    }

                    restoredCharacterWorldPresenceIds.Add(p.CharacterId);
                    if (!string.IsNullOrEmpty(p.PersonalSurfaceId) &&
                        ((p.Mode != (int)PartyWorldPresenceMode.AtSite &&
                          p.Mode != (int)PartyWorldPresenceMode.AtHex &&
                          p.Mode != (int)PartyWorldPresenceMode.AtWorldPosition) ||
                         !p.HasWorldPosition || float.IsNaN(p.WorldX) || float.IsInfinity(p.WorldX) ||
                         float.IsNaN(p.WorldY) || float.IsInfinity(p.WorldY)))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Personal spatial authority is invalid: CharacterId=" + p.CharacterId);
                    if (p.Mode == (int)PartyWorldPresenceMode.AtSite &&
                        !string.IsNullOrEmpty(p.SiteId))
                    {
                        if (p.HasWorldPosition)
                            world.WorldPresence.SetAtSiteWithAnchor(
                                id, p.SiteId, new WorldVec2(p.WorldX, p.WorldY));
                        else
                            world.WorldPresence.SetAtSite(id, p.SiteId);
                        world.WorldPresence.GetOrCreate(id).PersonalSurfaceId = p.PersonalSurfaceId ?? string.Empty;
                        continue;
                    }

                    if (p.Mode == (int)PartyWorldPresenceMode.AtHex &&
                        p.HexQ != int.MinValue &&
                        p.HexR != int.MinValue)
                    {
                        var hex = new HexCoord(p.HexQ, p.HexR);
                        if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                        {
                            WorldVec2 migrated;
                            if (p.HasWorldPosition)
                                migrated = new WorldVec2(p.WorldX, p.WorldY);
                            else
                            {
                                HexMath.ToWorldPosition(hex, 1f, out var x, out var y);
                                migrated = new WorldVec2(x, y);
                            }
                            if (world.SurfaceGround.TryResolveContaining(migrated, out var surface))
                            {
                                world.WorldPresence.SetAtWorldPosition(id, migrated, hex, surface.SurfaceId);
                                continue;
                            }
                        }
                        if (p.HasWorldPosition)
                        {
                            // 精确连续落点（Local Combat 倒下时 EntityView local → surface mapping）
                            world.WorldPresence.SetAtResidualWorldPosition(
                                id, hex, new WorldVec2(p.WorldX, p.WorldY));
                        }
                        else
                        {
                            world.WorldPresence.SetAtHex(id, hex);
                        }
                        world.WorldPresence.GetOrCreate(id).PersonalSurfaceId = p.PersonalSurfaceId ?? string.Empty;
                        continue;
                    }

                    if (p.Mode == (int)PartyWorldPresenceMode.AtWorldPosition)
                    {
                        var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                            ? world.HexWorld.HexSize
                            : 1f;
                        var pos = new WorldVec2(p.WorldX, p.WorldY);
                        var derived = p.HexQ != int.MinValue && p.HexR != int.MinValue
                            ? new HexCoord(p.HexQ, p.HexR)
                            : HexMath.WorldToHex(pos.X, pos.Y, hexSize);
                        world.WorldPresence.SetAtWorldPosition(id, pos, derived);
                        world.WorldPresence.GetOrCreate(id).PersonalSurfaceId = p.PersonalSurfaceId ?? string.Empty;
                    }
                }
            }

            if (dto.ResidualCharacterPresences != null)
            {
                for (var i = 0; i < dto.ResidualCharacterPresences.Count; i++)
                {
                    var r = dto.ResidualCharacterPresences[i];
                    if (r == null || r.CharacterId == 0)
                        continue;
                    if (restoredCharacterWorldPresenceIds.Contains(r.CharacterId))
                        continue;
                    var id = new EntityId(r.CharacterId);
                    if (!world.Entities.TryGet(id, out _))
                        continue;
                    world.WorldPresence.SetAtHex(id, new HexCoord(r.HexQ, r.HexR));
                }
            }

            if (dto.WorldSiteOwners != null)
            {
                for (var i = 0; i < dto.WorldSiteOwners.Count; i++)
                {
                    var s = dto.WorldSiteOwners[i];
                    if (s == null || string.IsNullOrEmpty(s.SiteId))
                        continue;
                    WorldSiteOwnershipService.SetOwner(world, s.SiteId, s.OwnerFactionId ?? string.Empty);
                }
            }

            if (dto.TerritoryRegionControllers != null)
            {
                for (var i = 0; i < dto.TerritoryRegionControllers.Count; i++)
                {
                    var r = dto.TerritoryRegionControllers[i];
                    if (r == null || string.IsNullOrEmpty(r.RegionId))
                        continue;
                    TerritoryControlService.SetRegionController(
                        world,
                        r.RegionId,
                        r.ControlFactionId ?? string.Empty);
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Phase 2J invariant（Load 后校验，不静默修）：绑定 Region 的 Fixed Site，
            // Owner == Region Controller == 全部 Hex.ControlFactionId。
            if (world.Strategic.Sites != null && world.Strategic.TerritoryRegions != null)
            {
                foreach (var siteKv in world.Strategic.Sites.Sites)
                {
                    var site = siteKv.Value;
                    if (site == null || string.IsNullOrEmpty(site.TerritoryRegionId))
                        continue;
                    if (!world.Strategic.TerritoryRegions.TryGet(site.TerritoryRegionId, out var region) || region == null)
                    {
                        System.Diagnostics.Debug.Fail(
                            "[TerritoryRestore] WorldSite '" + site.SiteId +
                            "' TerritoryRegionId '" + site.TerritoryRegionId + "' missing after restore.");
                        continue;
                    }

                    var owner = site.OwnerFactionId ?? string.Empty;
                    var controller = region.ControlFactionId ?? string.Empty;
                    if (!string.Equals(owner, controller, System.StringComparison.Ordinal))
                    {
                        System.Diagnostics.Debug.Fail(
                            "[TerritoryRestore] Owner/Controller mismatch after restore: WorldSite '" +
                            site.SiteId + "' Owner='" + owner + "' Region '" + region.RegionId +
                            "' Controller='" + controller + "'.");
                    }

                    for (var i = 0; i < region.Hexes.Count; i++)
                    {
                        var hex = region.Hexes[i];
                        if (world.HexWorld != null && world.HexWorld.TryGetCell(hex, out var cell) && cell != null &&
                            !string.Equals(cell.ControlFactionId ?? string.Empty, controller, System.StringComparison.Ordinal))
                        {
                            System.Diagnostics.Debug.Fail(
                                "[TerritoryRestore] Hex " + hex + " controller '" +
                                (cell.ControlFactionId ?? string.Empty) + "' != Region '" +
                                region.RegionId + "' Controller '" + controller + "'.");
                        }
                    }
                }
            }
#endif

            if (dto.Wars != null)
            {
                for (var i = 0; i < dto.Wars.Count; i++)
                {
                    var w = dto.Wars[i];
                    if (w == null || string.IsNullOrEmpty(w.WarId))
                        continue;
                    var war = new War { WarId = w.WarId, Active = w.Active };
                    if (w.Attackers != null)
                    {
                        for (var j = 0; j < w.Attackers.Count; j++)
                            war.AddAttacker(w.Attackers[j]);
                    }

                    if (w.Defenders != null)
                    {
                        for (var j = 0; j < w.Defenders.Count; j++)
                            war.AddDefender(w.Defenders[j]);
                    }

                    world.Strategic.Wars.Register(war);
                }
            }

            StrategicDiplomacyProjection.RebuildWarStances(world);

            if (dto.Alliances != null)
            {
                for (var i = 0; i < dto.Alliances.Count; i++)
                {
                    var a = dto.Alliances[i];
                    if (a?.Members == null || a.Members.Count < 2)
                        continue;
                    world.Strategic.Alliances.RestoreAlliance(a.AllianceId, a.Members);
                }
            }

            if (dto.Vassalages != null)
            {
                for (var i = 0; i < dto.Vassalages.Count; i++)
                {
                    var v = dto.Vassalages[i];
                    if (v == null)
                        continue;
                    world.Strategic.Vassalages.TryBindVassalage(v.VassalFactionId, v.OverlordFactionId);
                }
            }

            if (dto.RetreatingArmies != null)
            {
                for (var i = 0; i < dto.RetreatingArmies.Count; i++)
                {
                    var r = dto.RetreatingArmies[i];
                    if (r == null || string.IsNullOrEmpty(r.RetreatingArmyId))
                        continue;
                    var retreat = new RetreatingArmy
                    {
                        RetreatingArmyId = r.RetreatingArmyId,
                        SourceArmyId = r.SourceArmyId ?? string.Empty,
                        FactionId = r.FactionId ?? string.Empty,
                        HexQ = r.HexQ,
                        HexR = r.HexR
                    };
                    var members = new List<EntityId>(r.MemberCharacterIds?.Count ?? 0);
                    if (r.MemberCharacterIds != null)
                    {
                        for (var j = 0; j < r.MemberCharacterIds.Count; j++)
                            members.Add(new EntityId(r.MemberCharacterIds[j]));
                    }

                    retreat.SetMembers(members);
                    world.Strategic.RetreatingArmies.Register(retreat);
                }
            }

            if (dto.HasControlCoreSnapshotAuthority)
            {
                var cores = dto.ControlCores ?? new List<ControlCoreRuntimeSnapshotDto>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < cores.Count; i++)
                {
                    var c = cores[i];
                    if (c == null || string.IsNullOrWhiteSpace(c.WorkAreaId) || !ids.Add(c.WorkAreaId))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid or duplicate ControlCore runtime snapshot.", "Index=" + i);
                    var restored = world.ControlCores.RestoreRuntimeState(
                        c.WorkAreaId, c.CurrentDurability, c.OccupyProgressSeconds);
                    if (restored.IsFailure) return restored;
                }
            }
            else if (dto.LegacyCaptureObjectives != null)
            {
                for (var i = 0; i < dto.LegacyCaptureObjectives.Count; i++)
                {
                    var c = dto.LegacyCaptureObjectives[i];
                    if (c == null || string.IsNullOrWhiteSpace(c.WorkAreaId)) continue;
                    var restored = world.ControlCores.RestoreRuntimeState(
                        c.WorkAreaId, c.CurrentHp, c.OccupyProgressSeconds, c.Completed);
                    if (restored.IsFailure) return restored;
                }
            }

            RestorePlayerPartyTravel(world, dto.PlayerPartyTravel);
            RestoreBackgroundCharacterTravels(world, dto.BackgroundCharacterTravels);
            LoadedLocalMapPlacementSnapshotRestore.BeginRestoreFromSnapshot(dto);
            var separateSpace = SeparateSpaceSessionSnapshotRestore.Restore(world, dto);
            if (separateSpace.IsFailure)
                return separateSpace;
            PendingEngagementSnapshotRestore.Restore(world, dto);
            return Result.Success();
        }

        static Result RestoreRuntimeWorldSites(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (!dto.HasRuntimeWorldSiteSnapshotAuthority)
                return Result.Success();
            var source = dto.RuntimeWorldSites ?? new List<RuntimeWorldSiteSnapshotDto>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var coreIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item != null && item.CoreLevelFormat == 0 && item.CoreLevel == 0) item.CoreLevel = 1;
                if (item != null && item.CoreLevelFormat != 0 && item.CoreLevelFormat != 1)
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Unknown Site core level format.");
                if (item == null || string.IsNullOrWhiteSpace(item.SiteId) ||
                    string.IsNullOrWhiteSpace(item.CoreAssetId) ||
                    string.IsNullOrWhiteSpace(item.SurfaceId) || !item.HasWorldPosition ||
                    !IsFinite(item.WorldX) || !IsFinite(item.WorldY) || item.CoreLevel < 1 ||
                    item.ControlEstablishedOrder <= 0 ||
                    (!ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                     (world.HexWorld == null || !world.HexWorld.IsInBounds(item.AnchorQ, item.AnchorR))) ||
                    !ids.Add(item.SiteId) || !coreIds.Add(item.CoreAssetId))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Invalid runtime WorldSite snapshot entry.", "Index=" + i);
                if (item.IsCoreActive &&
                    (!world.Strategic.FactionFlags.Flags.TryGetValue(item.CoreAssetId, out var flag) ||
                     flag == null || !flag.IsSiteCore || !flag.HasWorldPosition ||
                     !string.Equals(flag.SiteId, item.SiteId, StringComparison.Ordinal) ||
                     !string.Equals(flag.SurfaceId, item.SurfaceId, StringComparison.Ordinal) ||
                     Math.Abs(flag.WorldX - item.WorldX) > .001f ||
                     Math.Abs(flag.WorldY - item.WorldY) > .001f))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Active runtime WorldSite core flag is missing or mismatched.", item.SiteId);
                if (world.Strategic.Sites.TryGet(item.SiteId, out var existing) &&
                    existing != null && !existing.IsRuntimeCreated)
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Runtime WorldSite collides with authored Site identity.", item.SiteId);
            }

            world.Strategic.Sites.RemoveAllRuntimeSites();
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                var anchor = ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world)
                    ? HexMath.WorldToHex(item.WorldX, item.WorldY,
                        world.HexWorld?.HexSize > 0f ? world.HexWorld.HexSize : 1f)
                    : new HexCoord(item.AnchorQ, item.AnchorR);
                ResolvedWorldSpatialRange controlRange;
                try { controlRange = world.Strategic.SpatialRules.ResolveLevel(world, item.CoreLevel, item.SurfaceId); }
                catch (Exception ex) { return Result.Failure(ErrorCode.SnapshotInvalid, "Site core level missing from Content.", ex.Message); }
                var site = new WorldSite
                {
                    SiteId = item.SiteId,
                    DisplayName = item.DisplayName ?? item.SiteId,
                    SiteType = item.SiteType ?? "Outpost",
                    OwnerFactionId = item.OwnerFactionId ?? string.Empty,
                    ControlEstablishedOrder = item.ControlEstablishedOrder,
                    UsesContinuousOutdoorSurface = true,
                    IsRuntimeCreated = true,
                    CoreAssetId = item.CoreAssetId,
                    CoreSurfaceId = item.SurfaceId,
                    HasCoreWorldPosition = true,
                    CoreWorldX = item.WorldX,
                    CoreWorldY = item.WorldY,
                    CoreLevel = item.CoreLevel,
                    CoreRangeWidth = controlRange.WidthWorld,
                    CoreRangeHeight = controlRange.HeightWorld,
                    IsCoreActive = item.IsCoreActive,
                    CoreIsRemovable = item.CoreIsRemovable,
                    AnchorHex = anchor,
                    PresenceHex = anchor,
                    LocalMapId = string.Empty
                };
                site.SetFootprint(new[] { anchor });
                try { world.Strategic.Sites.Register(site); }
                catch (Exception ex)
                {
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Runtime WorldSite restore commit failed.", ex.Message);
                }
                if (world.Strategic.FactionFlags.Flags.TryGetValue(item.CoreAssetId, out var flag) && flag != null)
                {
                    flag.SiteId = item.SiteId;
                    flag.SurfaceId = item.SurfaceId;
                    flag.IsSiteCore = true;
                    flag.FactionId = site.OwnerFactionId;
                }
            }
            return Result.Success();
        }

        static Result RestoreWorldSitePublicStocks(SimulationWorld world, StrategicSnapshotDto dto)
        {
            var board = world.Strategic.SitePublicStocks;
            board.Clear();
            if (!dto.HasWorldSitePublicStockSnapshotAuthority) return Result.Success();
            var siteIds = new HashSet<string>(StringComparer.Ordinal);
            var source = dto.WorldSitePublicStocks ?? new List<WorldSitePublicStockSnapshotDto>();
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item == null || string.IsNullOrWhiteSpace(item.SiteId) || !siteIds.Add(item.SiteId) ||
                    !world.Strategic.Sites.TryGet(item.SiteId, out _))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid/duplicate public stock SiteId.", "Index=" + i);
                var resources = new HashSet<string>(StringComparer.Ordinal);
                if (item.Entries == null) continue;
                for (var r = 0; r < item.Entries.Count; r++)
                {
                    var entry = item.Entries[r];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceId) || entry.Amount < 0 ||
                        !resources.Add(entry.ResourceId) || !world.InventoryCatalog.TryGet(entry.ResourceId, out _))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid/duplicate public stock entry.", item.SiteId + "[" + r + "]");
                    var set = WorldSitePublicStockService.SetInitial(world, item.SiteId, entry.ResourceId, entry.Amount);
                    if (set.IsFailure) return Result.Failure(ErrorCode.SnapshotInvalid, set.Error.Message, set.Error.Detail);
                }
                board.GetOrCreate(item.SiteId);
            }
            if (siteIds.Count != world.Strategic.Sites.Sites.Count)
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "Authoritative public stock set must contain every WorldSite.");
            board.HasSnapshotAuthority = true;
            board.DefaultsInitialized = true;
            return Result.Success();
        }

        static Result RestoreTerritoryClaims(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (!dto.HasTerritoryClaimSnapshotAuthority)
                return TerritoryClaimService.EstablishBaselineFromLegacy(world);
            if (dto.TerritoryClaims == null)
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "Territory claim authority is present but its history is missing.");
            var claims = new List<TerritoryClaimState>(dto.TerritoryClaims.Count);
            var claimedSites = new HashSet<string>(StringComparer.Ordinal);
            var usedOrders = new HashSet<long>();
            for (var i = 0; i < dto.TerritoryClaims.Count; i++)
            {
                var item = dto.TerritoryClaims[i];
                if (item == null || item.FormatVersion < 1 || item.FormatVersion > 3)
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Unknown territory claim snapshot format.", "Index=" + i);
                var width = item.Width;
                var height = item.Height;
                if (item.FormatVersion < 3)
                {
                    var migrated = TryMigrateLegacyTerritoryClaim(world, item, out width, out height);
                    if (migrated.IsFailure) return migrated;
                }
                var restored = new TerritoryClaimState
                {
                    ClaimId = item.ClaimId,
                    SiteId = item.SiteId,
                    SurfaceId = item.SurfaceId,
                    AcquiredOrder = item.AcquiredOrder,
                    CenterX = item.CenterX,
                    CenterY = item.CenterY,
                    Width = width,
                    Height = height
                };
                claims.Add(restored);
                claimedSites.Add(restored.SiteId ?? string.Empty);
                usedOrders.Add(restored.AcquiredOrder);
            }
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value;
                if (flag == null || !flag.NeedsAuthoredBaselineClaimMigration ||
                    string.IsNullOrWhiteSpace(flag.SiteId) || claimedSites.Contains(flag.SiteId) ||
                    !world.Strategic.Sites.TryGet(flag.SiteId, out var site) || site == null)
                    continue;
                if (flag.EstablishedOrder <= 0 || usedOrders.Contains(flag.EstablishedOrder))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Authored FactionFlag baseline order collides with snapshot claim history.", flag.FlagId);
                claims.Add(new TerritoryClaimState
                {
                    ClaimId = "claim:baseline:" + site.SiteId,
                    SiteId = site.SiteId,
                    SurfaceId = site.CoreSurfaceId,
                    AcquiredOrder = flag.EstablishedOrder,
                    CenterX = site.CoreWorldX,
                    CenterY = site.CoreWorldY,
                    Width = site.CoreRangeWidth,
                    Height = site.CoreRangeHeight
                });
                claimedSites.Add(site.SiteId);
                usedOrders.Add(flag.EstablishedOrder);
            }
            return TerritoryClaimService.RestoreAuthoritative(world, claims);
        }

        static void MarkAuthoredFlagsForLegacyBaselineMigration(SimulationWorld world)
        {
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value;
                if (flag != null && flag.IsAuthoredSiteCore)
                    flag.NeedsAuthoredBaselineClaimMigration = true;
            }
        }

        static bool HasAuthoredFlagLegacyMigration(SimulationWorld world)
        {
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value;
                if (flag != null && flag.IsAuthoredSiteCore &&
                    flag.NeedsAuthoredBaselineClaimMigration)
                    return true;
            }
            return false;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static Result TryMigrateLegacyTerritoryClaim(
            SimulationWorld world, TerritoryClaimSnapshotDto item, out float width, out float height)
        {
            width = height = 0f;
            if (world?.Strategic?.SpatialRules == null || item == null ||
                !world.Strategic.Sites.TryGet(item.SiteId ?? string.Empty, out var site) || site == null ||
                !string.Equals(site.CoreSurfaceId, item.SurfaceId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "Legacy territory claim source Site/Surface cannot be inferred reliably.", item?.SiteId);
            var initialOrBaseline = item.ClaimId != null &&
                (item.ClaimId.StartsWith("claim:baseline:", StringComparison.Ordinal) ||
                 item.ClaimId.StartsWith("claim:initial:", StringComparison.Ordinal));
            if (!initialOrBaseline || site.CoreLevel != 1 ||
                !IsKnownObsoleteLevelOneSize(item.FormatVersion, item.Width, item.Height))
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "Legacy territory claim level/kind cannot be migrated reliably.", item.SiteId);
            ResolvedWorldSpatialRange resolved;
            try { resolved = world.Strategic.SpatialRules.ResolveLevel(world, 1, item.SurfaceId); }
            catch (Exception ex)
            {
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "Legacy territory claim Surface metric unavailable.", ex.Message);
            }
            width = resolved.WidthWorld;
            height = resolved.HeightWorld;
            return Result.Success();
        }

        static bool IsKnownObsoleteLevelOneSize(int formatVersion, float width, float height)
        {
            // V1 wrote the mislabeled Content cell counts; V2 wrote their resolved Main-Surface
            // world sizes. These are the only controlled development migrations.
            if (formatVersion == 1)
                return IsSquare(width, height, 500f) || IsSquare(width, height, 250f);
            return formatVersion == 2 &&
                   (IsSquare(width, height, 14f) || IsSquare(width, height, 7f));
        }

        static bool IsSquare(float width, float height, float expected) =>
            Math.Abs(width - expected) <= .001f && Math.Abs(height - expected) <= .001f;

        /// <summary>Stage 2：Hex/Site shell 与政治覆盖完成后，按 Snapshot 精确恢复全部军队运动。</summary>
        public static Result RestoreFormalArmyMotions(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic == null || dto == null)
                return Result.Failure(ErrorCode.InvalidArgument, "FormalArmy motion restore requires world and dto.");
            if (dto.FormalArmies == null)
                return Result.Success();

            for (var i = 0; i < dto.FormalArmies.Count; i++)
            {
                var item = dto.FormalArmies[i];
                if (item == null || !world.Strategic.FormalArmies.TryGet(item.ArmyId, out var army) || army == null)
                    return Result.Failure(ErrorCode.SnapshotInvalid, "FormalArmy motion target missing.", item?.ArmyId ?? "<null>");
                var valid = FormalArmySnapshotRestore.Validate(world, item);
                if (valid.IsFailure)
                    return valid;
            }
            for (var i = 0; i < dto.FormalArmies.Count; i++)
            {
                var item = dto.FormalArmies[i];
                world.Strategic.FormalArmies.TryGet(item.ArmyId, out var army);
                var applied = FormalArmySnapshotRestore.Apply(world, army, item);
                if (applied.IsFailure)
                    return applied;
            }
            return Result.Success();
        }

        /// <summary>
        /// Snapshot Restore 后补全 FormalArmy→ArmyStack 展示链路与成员 WorldPresence（Content Shell 就绪后也可重复调用）。
        /// </summary>
        public static void FinalizeRuntimeLinks(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;

            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(world);

            // Snapshot member presence is saved state, including a Site/Hex without an exact
            // point. Fill only absent member projections from Army motion at this boundary.
            foreach (var kv in world.Strategic.FormalArmies.Armies)
                if (kv.Value != null)
                    FormalArmyMemberPresenceSync.SyncAll(world, kv.Value,
                        preservePersonalPositions: true);

            ArmyStackAdapter.EnsurePresentationStacksFromFormalArmies(world);

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                if (kv.Value != null)
                    ArmyHexPursuitService.RestoreAttackOrderIfNeeded(world, kv.Value);
            }
        }

        static FormalArmy BuildFormalArmyFromSnapshot(SimulationWorld world, FormalArmySnapshotDto a, bool squadAuthority)
        {
            var army = new FormalArmy
            {
                ArmyId = a.ArmyId,
                FactionId = a.FactionId ?? string.Empty,
                LeaderCharacterId = new EntityId(a.LeaderCharacterId),
                State = (FormalArmyState)a.State
            };
            var squadId = SquadMembershipService.ArmySquadId(a.ArmyId);
            if (!world.Strategic.Squads.TryGet(squadId, out var squad))
            {
                if (squadAuthority) throw new InvalidOperationException("Authoritative army squad missing: " + squadId);
                var members = new List<EntityId>();
                for (var i = 0; i < a.MemberCharacterIds.Count; i++) members.Add(new EntityId(a.MemberCharacterIds[i]));
                var created = SquadMembershipService.Create(world, squadId, members,
                    new EntityId(a.LeaderCharacterId), a.ArmyId, SquadCommandKind.FormalArmyWorldMotion,
                    importingSnapshot: true);
                if (created.IsFailure) throw new InvalidOperationException(created.Error.ToString());
                squad = created.Value;
            }
            army.BindSquad(squad);
            army.UsesHexStrategicPosition = a.UsesHexStrategicPosition;
            army.CurrentHex = new HexCoord(a.CurrentHexQ, a.CurrentHexR);
            army.DestinationHex = new HexCoord(a.DestinationHexQ, a.DestinationHexR);
            if (a.HexPath != null && a.HexPath.Count > 0)
            {
                var path = new List<HexCoord>(a.HexPath.Count);
                for (var p = 0; p < a.HexPath.Count; p++)
                {
                    var c = a.HexPath[p];
                    if (c != null)
                        path.Add(new HexCoord(c.Q, c.R));
                }

                army.SetHexPath(path, army.DestinationHex);
                army.CurrentPathIndex = a.CurrentPathIndex;
                army.StepProgress = a.StepProgress;
                army.StepRemainingTicks = a.StepRemainingTicks;
                army.StepTotalTicks = a.StepTotalTicks;
            }
            else
            {
                army.StepProgress = a.StepProgress;
                army.StepRemainingTicks = a.StepRemainingTicks;
                army.StepTotalTicks = a.StepTotalTicks;
                army.CurrentPathIndex = a.CurrentPathIndex;
                if (army.State == FormalArmyState.Moving)
                    army.State = FormalArmyState.Idle;
            }

            return army;
        }

        static Result ValidateSquadAuthority(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (!dto.HasSquadSnapshotAuthority) return Result.Success();
            var byId = new Dictionary<string, SquadSnapshotDto>(StringComparer.Ordinal);
            var members = new HashSet<ulong>();
            if (dto.Squads == null) return Result.Failure(ErrorCode.SnapshotInvalid, "Squad authority is missing.");
            foreach (var squad in dto.Squads)
            {
                if (squad == null || string.IsNullOrWhiteSpace(squad.SquadId) || byId.ContainsKey(squad.SquadId) ||
                    squad.MemberCharacterIds == null || squad.MemberCharacterIds.Count == 0 ||
                    !squad.MemberCharacterIds.Contains(squad.LeaderCharacterId) ||
                    (squad.CommandTargetCharacterId != 0 && !squad.MemberCharacterIds.Contains(squad.CommandTargetCharacterId)) ||
                    !Enum.IsDefined(typeof(SquadCommandKind), squad.CommandKind))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid authoritative squad identity, leader or command.");
                byId.Add(squad.SquadId, squad);
                foreach (var id in squad.MemberCharacterIds)
                    if (id == 0 || !members.Add(id) || !world.Entities.TryGet(new EntityId(id), out var entity) ||
                        (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0)
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid or duplicate authoritative squad member.", id.ToString());
            }
            foreach (var entity in world.Entities.All)
                if ((entity.Tags & (EntityTag.Character | EntityTag.Npc)) != 0 && !members.Contains(entity.Id.Value))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Character is missing authoritative squad.", entity.Id.ToString());
            if (string.IsNullOrEmpty(dto.ControlledSquadId) || !byId.ContainsKey(dto.ControlledSquadId))
                return Result.Failure(ErrorCode.SnapshotInvalid, "Controlled squad is missing.", dto.ControlledSquadId);
            var armies = new HashSet<string>(StringComparer.Ordinal);
            if (dto.FormalArmies != null)
                foreach (var army in dto.FormalArmies)
                {
                    if (army == null || string.IsNullOrEmpty(army.ArmyId) || !armies.Add(army.ArmyId) ||
                        !byId.TryGetValue(SquadMembershipService.ArmySquadId(army.ArmyId), out var squad) ||
                        !string.Equals(squad.LegacyArmyId, army.ArmyId, StringComparison.Ordinal))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Army has no authoritative squad mapping.");
                }
            foreach (var squad in byId.Values)
                if (!string.IsNullOrEmpty(squad.LegacyArmyId) && !armies.Contains(squad.LegacyArmyId))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Squad references missing legacy army.", squad.SquadId);
            return Result.Success();
        }

        static Result RestoreDerivedArmyMemberships(SimulationWorld world)
        {
            foreach (var entity in world.Entities.All)
            {
                if ((entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0) continue;
                ArmyInvariants.EnsureMembershipComponent(entity);
                var membership = entity.Get<ArmyMembershipComponent>();
                membership.ClearArmyId();
                if (world.Strategic.Squads.TryGetForCharacter(entity.Id, out var squad) &&
                    !string.IsNullOrEmpty(squad.LegacyArmyId)) membership.SetArmyId(squad.LegacyArmyId);
            }
            return Result.Success();
        }

        static Result RestoreArmyMemberships(
            SimulationWorld world,
            List<ArmyMembershipSnapshotDto> memberships)
        {
            if (memberships == null)
                return Result.Success();

            for (var i = 0; i < memberships.Count; i++)
            {
                var m = memberships[i];
                if (m == null || m.CharacterId == 0 || string.IsNullOrEmpty(m.ArmyId))
                    continue;
                var id = new EntityId(m.CharacterId);
                if (!world.Strategic.FormalArmies.TryGet(m.ArmyId, out var army) || army == null ||
                    !army.ContainsMember(id))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "ArmyMembership references a missing army or mismatched roster.",
                        "CharacterId=" + m.CharacterId + " ArmyId='" + m.ArmyId + "'.");
                if (!world.Entities.TryGet(id, out var entity))
                {
                    LogCharacterRestoreSkip(m.CharacterId, m.ArmyId, "army membership target missing");
                    continue;
                }

                ArmyInvariants.EnsureMembershipComponent(entity);
                entity.Get<ArmyMembershipComponent>().SetArmyId(m.ArmyId);
            }
            return Result.Success();
        }

        static void LogCharacterRestoreSkip(ulong characterId, string context, string reason)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Debug.WriteLine(
                "[SnapshotCharacterRestore] CharacterId=" + characterId +
                (string.IsNullOrEmpty(context) ? string.Empty : " Context=" + context) +
                " FAILED: " + reason);
#endif
        }

        static void RestoreBackgroundCharacterTravels(
            SimulationWorld world,
            List<BackgroundCharacterTravelSnapshotDto> travels)
        {
            world?.BackgroundCharacterTravel?.Clear();
            if (world?.BackgroundCharacterTravel == null || travels == null)
                return;

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;

            for (var i = 0; i < travels.Count; i++)
            {
                var t = travels[i];
                if (t == null || t.CharacterId == 0)
                    continue;
                var id = new EntityId(t.CharacterId);
                if (t.LocationKind == (int)BackgroundCharacterLocationKind.AtWorldSite &&
                    !string.IsNullOrEmpty(t.SiteId))
                {
                    world.WorldPresence.SetAtSite(id, t.SiteId);
                }
                else
                {
                    var pos = new WorldVec2(t.WorldX, t.WorldY);
                    var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
                    world.SurfaceGround.TryResolveContaining(pos, out var surface);
                    world.WorldPresence.SetAtWorldPosition(id, pos, derived,
                        surface?.SurfaceId ?? string.Empty);
                }

                if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                {
                    if (t.IsTraveling && !t.IsSurfaceRoute &&
                        !string.IsNullOrEmpty(t.DestinationSiteId))
                        BackgroundCharacterTravelService.BeginTravelToWorldSite(
                            world, id, t.DestinationSiteId);
                    continue;
                }

                if (t.IsSurfaceRoute || !t.IsTraveling || t.HexPath == null || t.HexPath.Count < 2)
                    continue;

                var path = new List<HexCoord>(t.HexPath.Count);
                for (var p = 0; p < t.HexPath.Count; p++)
                {
                    var c = t.HexPath[p];
                    if (c != null)
                        path.Add(new HexCoord(c.Q, c.R));
                }

                if (path.Count < 2)
                    continue;

                var motion = world.BackgroundCharacterTravel.GetOrCreate(id);
                motion.BeginTravel(
                    path,
                    new HexCoord(t.DestinationHexQ, t.DestinationHexR),
                    t.DestinationSiteId ?? string.Empty,
                    HexTravelMode.Ground);
                motion.SetSegment(t.SegmentIndex, t.SegmentProgress);
                motion.LastProcessedWorldTick = t.LastProcessedWorldTick > 0
                    ? t.LastProcessedWorldTick
                    : world.Tick.Value;
            }
        }

        public static void RestoreBackgroundSurfaceTravels(
            SimulationWorld world, List<BackgroundCharacterTravelSnapshotDto> travels)
        {
            if (world == null || travels == null ||
                !ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world)) return;
            for (var i = 0; i < travels.Count; i++)
            {
                var item = travels[i];
                if (item == null || !item.IsTraveling || !item.IsSurfaceRoute ||
                    item.CharacterId == 0 || string.IsNullOrEmpty(item.DestinationSiteId)) continue;
                BackgroundCharacterTravelService.BeginTravelToWorldSite(
                    world, new EntityId(item.CharacterId), item.DestinationSiteId);
            }
        }

        public static void RestorePlayerPartyTravel(SimulationWorld world, PlayerPartyTravelSnapshotDto travel)
        {
            if (world?.PlayerPartyTravel == null || travel == null || !travel.HasPosition)
                return;

            var motion = world.PlayerPartyTravel;
            // CW-U4.1: old PlayerParty AttackArmy/attack-chase orders are retired on load.
            // Preserve the canonical position, restore Idle, and never auto-declare war/create an encounter.
            motion.ClearAttackOrder();
            if (travel.LocationKind == (int)PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(travel.SiteId))
            {
                var sitePosition = new WorldVec2(travel.WorldX, travel.WorldY);
                if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                    !world.SurfaceGround.TryResolveContaining(sitePosition, out _) &&
                    world.Strategic.Sites.TryGet(travel.SiteId, out var authoredSite) &&
                    authoredSite != null)
                    sitePosition = new WorldVec2(authoredSite.CoreWorldX, authoredSite.CoreWorldY);
                // Snapshot 的 AtWorldSite WorldX/Y 是 Site 内连续 Canonical 位置；不能用
                // PresenceHex center 覆盖，否则 Load 后 LocalVisible 出口路径会以错误起点重建。
                motion.RestoreIdleAtWorldSite(
                    travel.SiteId,
                    sitePosition,
                    new HexCoord(travel.CurrentHexQ, travel.CurrentHexR));
                if (travel.IsMoving && travel.HasContinuousPhysicalDestination &&
                    ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                {
                    motion.SetAtWorldPosition(motion.WorldPosition, motion.CurrentHex);
                    PlayerPartySurfaceTravelService.TryResumeAfterRestore(
                        world, new WorldVec2(travel.DestinationWorldX, travel.DestinationWorldY),
                        travel.DestinationSiteId, travel.ArrivalRadius);
                }
                return;
            }

            var pos = new WorldVec2(travel.WorldX, travel.WorldY);
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                ((travel.WorldX == 0f && travel.WorldY == 0f &&
                  (travel.CurrentHexQ != 0 || travel.CurrentHexR != 0)) ||
                 !world.SurfaceGround.TryResolveContaining(pos, out _)))
            {
                HexMath.ToWorldPosition(new HexCoord(travel.CurrentHexQ, travel.CurrentHexR),
                    hexSize, out var migratedX, out var migratedY);
                var migrated = new WorldVec2(migratedX, migratedY);
                if (world.SurfaceGround.TryResolveContaining(migrated, out _))
                    pos = migrated;
            }
            var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
            motion.SetAtWorldPosition(pos, derived);
            if (travel.IsMoving && travel.HasContinuousPhysicalDestination)
            {
                // Route is deliberately recomputed from canonical position + exact intent.
                PlayerPartySurfaceTravelService.TryResumeAfterRestore(
                    world, new WorldVec2(travel.DestinationWorldX, travel.DestinationWorldY),
                    travel.DestinationSiteId, travel.ArrivalRadius);
            }
        }
    }
}
