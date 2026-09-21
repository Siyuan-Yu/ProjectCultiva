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
                // In normal Continuous Outdoor, PlayerPartyWorldMotion is the group authority.
                // Eligible members are intentionally reconciled from that authority after the
                // content-dependent travel phase, so their stale per-member DTO must not override
                // the finalized Party position. Separate Space and CharacterEncounter were
                // excluded above/by the gate below and keep their own spatial ownership.
                var party = world.Strategic?.PlayerPartyContext;
                var normalPartyMember =
                    world.LocalMap?.IsActive != true &&
                    world.Strategic?.CharacterEncounter == null &&
                    party != null && party.IsMember(id) &&
                    PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(world, party, id);
                if (normalPartyMember)
                {
                    var motion = world.PlayerPartyTravel;
                    if (!world.WorldPresence.TryGet(id, out var memberPresence) ||
                        memberPresence == null ||
                        memberPresence.Mode != PartyWorldPresenceMode.AtWorldPosition ||
                        !memberPresence.HasContinuousWorldPosition ||
                        motion == null || !motion.HasPosition ||
                        !string.Equals(memberPresence.PersonalSurfaceId, motion.SurfaceId,
                            StringComparison.Ordinal) ||
                        Math.Abs(memberPresence.WorldPosX - motion.WorldPosition.X) > 0.0001f ||
                        Math.Abs(memberPresence.WorldPosY - motion.WorldPosition.Y) > 0.0001f)
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "PlayerParty member presence differs from finalized Surface authority.",
                            "CharacterId=" + saved.CharacterId);
                    continue;
                }
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

            return CharacterEncounterService.ValidateObjectiveWorldState(world);
        }

        public static StrategicSnapshotDto Capture(SimulationWorld world, PlayerPartyRuntime party = null)
        {
            var dto = new StrategicSnapshotDto
            {
                PlayerFactionId = world?.Strategic?.PlayerFactionId ?? string.Empty,
                Ch01FormationScenarioCompat = false
            };
            if (world?.Strategic == null)
                return dto;

            dto.HasSquadSnapshotAuthority = true;
            dto.HasSquadWorldMotionSnapshotAuthority = true;
            var controllingParty = party ?? world.Strategic.PlayerPartyContext;
            dto.ControlledSquadId = controllingParty?.ControlledSquadId ?? string.Empty;
            var captureSquadIds = new List<string>(world.Strategic.Squads.Squads.Keys);
            captureSquadIds.Sort(StringComparer.Ordinal);
            for (var captureSquadIndex = 0; captureSquadIndex < captureSquadIds.Count; captureSquadIndex++)
            {
                world.Strategic.Squads.TryGet(captureSquadIds[captureSquadIndex], out var squad);
                if (squad == null) continue;
                var isControlledSquad = !string.IsNullOrEmpty(dto.ControlledSquadId) &&
                    string.Equals(squad.SquadId, dto.ControlledSquadId, StringComparison.Ordinal);
                var squadDto = new SquadSnapshotDto
                {
                    SquadId = squad.SquadId,
                    DisplayName = squad.DisplayName,
                    FactionId = squad.FactionId,
                    LeaderCharacterId = squad.LeaderCharacterId.Value,
                    LegacyArmyId = string.Empty,
                    CommandKind = (int)(isControlledSquad
                        ? SquadCommandKind.FollowLeader
                        : squad.CommandKind == SquadCommandKind.LegacyFormalArmyWorldMotion
                            ? SquadCommandKind.SquadWorldMotion : squad.CommandKind),
                    CommandRevision = squad.CommandRevision,
                    CommandTargetCharacterId = isControlledSquad && controllingParty?.HasActive == true
                        ? controllingParty.ActiveCharacterId.Value : squad.CommandTargetCharacterId.Value
                };
                for (var i = 0; i < squad.MemberCharacterIds.Count; i++) squadDto.MemberCharacterIds.Add(squad.MemberCharacterIds[i]);
                dto.Squads.Add(squadDto);
            }

            var motionIds = new List<string>(world.Strategic.SquadWorldMotions.Motions.Keys);
            motionIds.Sort(StringComparer.Ordinal);
            for (var i = 0; i < motionIds.Count; i++)
            {
                world.Strategic.SquadWorldMotions.TryGet(motionIds[i], out var squadMotion);
                if (squadMotion == null || !squadMotion.HasPosition ||
                    string.Equals(squadMotion.SquadId, dto.ControlledSquadId, StringComparison.Ordinal) ||
                    !world.Strategic.Squads.TryGet(squadMotion.SquadId, out var motionSquad) ||
                    !SquadWorldMotionService.IsActiveNpcSquadAuthority(world, motionSquad, squadMotion)) continue;
                var motionDto = new SquadWorldMotionSnapshotDto
                {
                    SquadId = squadMotion.SquadId, SurfaceId = squadMotion.SurfaceId, SiteId = squadMotion.SiteId,
                    WorldX = squadMotion.WorldPosition.X, WorldY = squadMotion.WorldPosition.Y,
                    IsMoving = squadMotion.IsMoving, DestinationX = squadMotion.Destination.X, DestinationY = squadMotion.Destination.Y,
                    WaypointIndex = squadMotion.WaypointIndex, SegmentProgress = squadMotion.SegmentProgress,
                    SourceRevision = squadMotion.SourceRevision, SourceHash = squadMotion.SourceHash
                };
                for (var p = 0; p < squadMotion.Route.Count; p++)
                    motionDto.Route.Add(new WorldPointSnapshotDto { X = squadMotion.Route[p].X, Y = squadMotion.Route[p].Y });
                dto.SquadWorldMotions.Add(motionDto);
            }

            // FormalArmy and ArmyMembership DTO collections are legacy input only.
            // Modern capture deliberately leaves both collections empty.

            // ResidualCharacterPresences is legacy input only. Modern saves leave it empty.

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

                // AtHex is accepted only as old input. It is never re-emitted by modern capture.
                if (presence.UsesHexPresence)
                    continue;

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
                    AnchorQ = site.LegacyAnchorHex.Q,
                    AnchorR = site.LegacyAnchorHex.R,
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

            // RetreatingArmy is legacy input only. Modern saves intentionally keep the DTO empty.

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
                    SurfaceId = motion.SurfaceId ?? string.Empty,
                    CurrentOutdoorWorldSiteId = motion.CurrentOutdoorWorldSiteId ?? string.Empty,
                    WorldX = motion.WorldPosition.X,
                    WorldY = motion.WorldPosition.Y,
                    CurrentHexQ = motion.LegacyCurrentHex.Q,
                    CurrentHexR = motion.LegacyCurrentHex.R,
                    IsMoving = motion.IsMoving,
                    HasContinuousPhysicalDestination = motion.HasContinuousPhysicalDestination,
                    DestinationWorldX = motion.ContinuousPhysicalDestination.X,
                    DestinationWorldY = motion.ContinuousPhysicalDestination.Y,
                    ArrivalRadius = motion.ContinuousPhysicalArrivalRadius,
                    DestinationSiteId = motion.LegacyDestinationSiteId ?? string.Empty,
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
            // FormalArmy/ArmyMembership arrays remain in the schema as legacy inputs only.
            dto.FormalArmies.Clear();
            dto.ArmyMemberships.Clear();
            LegacyRuntimeInvariant.AssertModernSnapshot(dto);
            return dto;
        }

        public static Result Restore(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic == null || dto == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Strategic snapshot restore requires world and dto.");

            var squadValidation = ValidateSquadAuthority(world, dto);
            if (squadValidation.IsFailure) return squadValidation;
            var motionValidation = ValidateSquadWorldMotionAuthority(dto);
            if (motionValidation.IsFailure) return motionValidation;

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
            world.Strategic.Squads.Clear();
            world.Strategic.SquadWorldMotions.Clear();
            world.Strategic.Wars.Clear();
            world.Strategic.Diplomacy.Clear();
            world.Strategic.Alliances.Clear();
            world.Strategic.Vassalages.Clear();
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
                    var isControlledSquad = string.Equals(
                        item.SquadId, dto.ControlledSquadId, StringComparison.Ordinal);
                    var restoredCommand = isControlledSquad
                        ? SquadCommandKind.FollowLeader
                        : item.CommandKind == (int)SquadCommandKind.LegacyFormalArmyWorldMotion
                            ? SquadCommandKind.SquadWorldMotion : (SquadCommandKind)item.CommandKind;
                    var created = SquadMembershipService.Create(world, item.SquadId, members,
                        new EntityId(item.LeaderCharacterId),
                        dto.HasSquadWorldMotionSnapshotAuthority ? string.Empty : item.LegacyArmyId,
                        restoredCommand,
                        importingSnapshot: true, displayName: item.DisplayName, factionId: item.FactionId);
                    if (created.IsFailure) return Result.Failure(created.Error);
                    created.Value.CommandRevision = item.CommandRevision;
                    created.Value.CommandTargetCharacterId = isControlledSquad
                        ? new EntityId(dto.PlayerParty?.ActiveCharacterId ?? item.LeaderCharacterId)
                        : new EntityId(item.CommandTargetCharacterId);
                }
            }

            if (!dto.HasSquadWorldMotionSnapshotAuthority)
            {
                var migrated = LegacyFormalArmySnapshotMigration.RestoreSquads(world, dto);
                if (migrated.IsFailure) return migrated;
            }

            // ArmyMembership is accepted only as legacy migration input; no runtime component is rebuilt.

            // CharacterWorldPresences 是新版 authority（可携带 precise WorldPosition）；
            // 恢复时记录已恢复 id —— 旧 ResidualCharacterPresences 只作 legacy fallback，
            // 不覆盖新版（否则 SetLegacyAtHex 会把 HasContinuousWorldPosition 清掉）。
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
                        if (!string.IsNullOrEmpty(p.PersonalSurfaceId))
                            return Result.Failure(ErrorCode.SnapshotInvalid,
                                "AtHex legacy presence could not migrate to its Continuous Surface: CharacterId=" + p.CharacterId);
                        if (p.HasWorldPosition)
                            world.WorldPresence.SetLegacyResidualWorldPosition(
                                id, hex, new WorldVec2(p.WorldX, p.WorldY));
                        else
                            world.WorldPresence.SetLegacyAtHex(id, hex);
                        world.WorldPresence.GetOrCreate(id).PersonalSurfaceId = p.PersonalSurfaceId ?? string.Empty;
                        continue;
                    }

                    if (p.Mode == (int)PartyWorldPresenceMode.AtWorldPosition)
                    {
                        var hexSize = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                            ? world.LegacyHexWorld.HexSize
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
                    var hex = new HexCoord(r.HexQ, r.HexR);
                    if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                    {
                        var size = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                            ? world.LegacyHexWorld.HexSize : 1f;
                        HexMath.ToWorldPosition(hex, size, out var x, out var y);
                        var point = new WorldVec2(x, y);
                        if (!world.SurfaceGround.TryResolveContaining(point, out var surface))
                            return Result.Failure(ErrorCode.SnapshotInvalid,
                                "Legacy residual Hex cannot migrate to a Continuous Surface: CharacterId=" + r.CharacterId);
                        world.WorldPresence.SetAtWorldPosition(id, point, hex, surface.SurfaceId);
                    }
                    else
                        world.WorldPresence.SetLegacyAtHex(id, hex);
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
                    foreach (var sitePair in world.Strategic.Sites.Sites)
                    {
                        var site = sitePair.Value;
                        if (site == null || !string.IsNullOrEmpty(site.OwnerFactionId) ||
                            !string.Equals(site.TerritoryRegionId, r.RegionId, StringComparison.Ordinal))
                            continue;
                        WorldSiteOwnershipService.SetOwner(
                            world, site.SiteId, r.ControlFactionId ?? string.Empty);
                    }
                }
            }

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

            // Old RetreatingArmy containers are not restored. Real members already restored above
            // keep their Character presence/lifecycle; synthetic army identity is discarded.

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
            var backgroundRestore = RestoreBackgroundCharacterTravels(
                world, dto.BackgroundCharacterTravels);
            if (backgroundRestore.IsFailure) return backgroundRestore;
            LoadedLocalMapPlacementSnapshotRestore.BeginRestoreFromSnapshot(dto);
            var separateSpace = SeparateSpaceSessionSnapshotRestore.Restore(world, dto);
            if (separateSpace.IsFailure)
                return separateSpace;
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
                     (world.LegacyHexWorld == null || !world.LegacyHexWorld.IsInBounds(item.AnchorQ, item.AnchorR))) ||
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
                        world.LegacyHexWorld?.HexSize > 0f ? world.LegacyHexWorld.HexSize : 1f)
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
                    LegacyAnchorHex = anchor,
                    LegacyPresenceHex = anchor,
                    LocalMapId = string.Empty
                };
                site.SetLegacyHexFootprint(new[] { anchor });
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

        /// <summary>Stage 2: restore modern motion or migrate genuine legacy FormalArmy motion one-way.</summary>
        public static Result RestoreSquadWorldMotions(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic == null || dto == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Squad motion restore requires world and dto.");
            world.Strategic.SquadWorldMotions.Clear();
            if (dto.HasSquadWorldMotionSnapshotAuthority)
            {
                if (dto.SquadWorldMotions == null) return Result.Failure(ErrorCode.SnapshotInvalid, "SquadWorldMotions missing.");
                for (var i = 0; i < dto.SquadWorldMotions.Count; i++)
                {
                    var item = dto.SquadWorldMotions[i];
                    if (item != null && string.Equals(
                            item.SquadId, dto.ControlledSquadId, StringComparison.Ordinal))
                    {
                        if (world.Strategic.Squads.TryGet(item.SquadId, out var controlledSquad))
                            SquadCommandService.SetExecution(world, controlledSquad.SquadId,
                                SquadCommandKind.FollowLeader,
                                new EntityId(dto.PlayerParty?.ActiveCharacterId ??
                                             controlledSquad.LeaderCharacterId.Value));
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
                        System.Diagnostics.Debug.WriteLine(
                            "[SnapshotMigration] Dropped stale PlayerParty SquadWorldMotion: " + item.SquadId);
#endif
                        continue;
                    }
                    if (item != null && !item.IsMoving && !string.IsNullOrEmpty(item.SiteId))
                    {
                        var canonicalized = SquadWorldMotionService.InitializeAtSite(
                            world, item.SquadId, item.SiteId);
                        if (canonicalized.IsFailure)
                            return Result.Failure(ErrorCode.SnapshotInvalid,
                                "NPC Squad AtSite SiteArrival is unavailable.",
                                item.SquadId + ":" + item.SiteId + ":" + canonicalized.Error);
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (!string.Equals(item.SurfaceId, canonicalized.Value.SurfaceId,
                                StringComparison.Ordinal) ||
                            Math.Abs(item.WorldX - canonicalized.Value.WorldPosition.X) > .0001f ||
                            Math.Abs(item.WorldY - canonicalized.Value.WorldPosition.Y) > .0001f ||
                            Math.Abs(item.DestinationX - canonicalized.Value.Destination.X) > .0001f ||
                            Math.Abs(item.DestinationY - canonicalized.Value.Destination.Y) > .0001f)
                            System.Diagnostics.Debug.WriteLine(
                                "[SnapshotMigration] Canonicalized NPC Squad AtSite motion to SiteArrival: " +
                                item.SquadId + " SiteId=" + item.SiteId);
#endif
                        continue;
                    }
                    if (item == null || !world.Strategic.Squads.TryGet(item.SquadId, out var squad) ||
                        squad.CommandKind != SquadCommandKind.SquadWorldMotion ||
                        !world.SurfaceGround.TryGet(item.SurfaceId, out var navigation))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Squad world-motion target or Surface missing.", item?.SquadId ?? "<null>");
                    var position = new WorldVec2(item.WorldX, item.WorldY);
                    var destination = new WorldVec2(item.DestinationX, item.DestinationY);
                    if (!navigation.Contains(position.X, position.Y) || !navigation.IsWalkable(position.X, position.Y))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Squad world-motion position is invalid.", item.SquadId);
                    if (!navigation.Contains(destination.X, destination.Y) || !navigation.IsWalkable(destination.X, destination.Y))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Squad world-motion destination is invalid.", item.SquadId);
                    var route = new List<WorldVec2>();
                    if (item.Route != null)
                        for (var p = 0; p < item.Route.Count; p++)
                        {
                            var routePoint = new WorldVec2(item.Route[p].X, item.Route[p].Y);
                            if (!navigation.Contains(routePoint.X, routePoint.Y) ||
                                !navigation.IsWalkable(routePoint.X, routePoint.Y))
                                return Result.Failure(ErrorCode.SnapshotInvalid,
                                    "Squad world-motion route is invalid.", item.SquadId + ":" + p);
                            route.Add(routePoint);
                        }
                    var motion = new SquadWorldMotionState { SquadId = item.SquadId };
                    motion.Restore(item.SurfaceId, item.SiteId, position, item.IsMoving, destination, route,
                        item.WaypointIndex, item.SegmentProgress, item.SourceRevision, item.SourceHash);
                    if (!world.Strategic.SquadWorldMotions.Register(motion))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Duplicate Squad world-motion.", item.SquadId);
                    squad.LegacyArmyId = string.Empty;
                    if (squad.CommandKind == SquadCommandKind.LegacyFormalArmyWorldMotion)
                        squad.SetCommand(SquadCommandKind.SquadWorldMotion);
                }
                return Result.Success();
            }

            return LegacyFormalArmySnapshotMigration.RestoreMotions(world, dto);
        }

        /// <summary>
        /// Snapshot Restore 后收口现代 Squad runtime links（Content Shell 就绪后也可重复调用）。
        /// </summary>
        public static void FinalizeRuntimeLinks(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;

            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(world);

            // Modern runtime does not recreate FormalArmy, ArmyStack or ArmyMembership views.
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
            // Core/NPC-only snapshots may legitimately have no PlayerParty control context.
            // Once a controlled squad is declared it must resolve to authoritative Squad data.
            if (!string.IsNullOrEmpty(dto.ControlledSquadId) && !byId.ContainsKey(dto.ControlledSquadId))
                return Result.Failure(ErrorCode.SnapshotInvalid, "Controlled squad is missing.", dto.ControlledSquadId);
            // FormalArmy fields, when present beside modern Squad authority, are ignored compatibility input.
            return Result.Success();
        }

        static Result ValidateSquadWorldMotionAuthority(StrategicSnapshotDto dto)
        {
            if (!dto.HasSquadWorldMotionSnapshotAuthority) return Result.Success();
            if (dto.SquadWorldMotions == null) return Result.Failure(ErrorCode.SnapshotInvalid, "SquadWorldMotions authority missing.");
            var squadIds = new HashSet<string>(StringComparer.Ordinal);
            if (dto.Squads != null) foreach (var squad in dto.Squads) if (squad != null) squadIds.Add(squad.SquadId);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var motion in dto.SquadWorldMotions)
            {
                if (motion == null || string.IsNullOrWhiteSpace(motion.SquadId) || !seen.Add(motion.SquadId) ||
                    !squadIds.Contains(motion.SquadId) || string.IsNullOrWhiteSpace(motion.SurfaceId) ||
                    !Finite(motion.WorldX) || !Finite(motion.WorldY) || !Finite(motion.DestinationX) ||
                    !Finite(motion.DestinationY) || !Finite(motion.SegmentProgress) || motion.SegmentProgress < 0f ||
                    motion.Route == null || motion.WaypointIndex < 0 || motion.WaypointIndex > motion.Route.Count ||
                    (motion.IsMoving && motion.Route.Count == 0))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid SquadWorldMotion snapshot.", motion?.SquadId ?? "<null>");
                for (var i = 0; i < motion.Route.Count; i++)
                    if (motion.Route[i] == null || !Finite(motion.Route[i].X) || !Finite(motion.Route[i].Y))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid SquadWorldMotion route.", motion.SquadId);
            }
            return Result.Success();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void LogCharacterRestoreSkip(ulong characterId, string context, string reason)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Debug.WriteLine(
                "[SnapshotCharacterRestore] CharacterId=" + characterId +
                (string.IsNullOrEmpty(context) ? string.Empty : " Context=" + context) +
                " FAILED: " + reason);
#endif
        }

        static Result RestoreBackgroundCharacterTravels(
            SimulationWorld world,
            List<BackgroundCharacterTravelSnapshotDto> travels)
        {
            world?.BackgroundCharacterTravel?.Clear();
            if (world?.BackgroundCharacterTravel == null || travels == null)
                return Result.Success();

            var hexSize = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                ? world.LegacyHexWorld.HexSize
                : 1f;

            var seen = new HashSet<ulong>();
            for (var i = 0; i < travels.Count; i++)
            {
                var t = travels[i];
                if (t == null || t.CharacterId == 0)
                    continue;
                if (!seen.Add(t.CharacterId))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Duplicate Background Character travel snapshot.",
                        "CharacterId=" + t.CharacterId);
                var id = new EntityId(t.CharacterId);
                if (!Finite(t.WorldX) || !Finite(t.WorldY))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Background Character position is invalid.",
                        "CharacterId=" + t.CharacterId);

                if (world.Entities.TryGet(id, out var entity) && entity != null &&
                    entity.TryGet<EntityLocationSnapshotAuthorityComponent>(out var locationAuthority) &&
                    locationAuthority.SnapshotFieldPresent &&
                    entity.TryGet<EntityLocationComponent>(out var entityLocation) &&
                    entityLocation.HasLocation)
                {
                    if (t.IsTraveling || t.IsSurfaceRoute || !string.IsNullOrEmpty(t.SurfaceId))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Background travel conflicts with Interior EntityLocation authority.",
                            "CharacterId=" + t.CharacterId);
                    continue;
                }

                var pos = new WorldVec2(t.WorldX, t.WorldY);
                if (world.WorldPresence.TryGet(id, out var existing) && existing != null)
                {
                    if (existing.Mode == PartyWorldPresenceMode.InEncounter)
                    {
                        if (t.IsTraveling || t.IsSurfaceRoute || !string.IsNullOrEmpty(t.SurfaceId))
                            return Result.Failure(ErrorCode.SnapshotInvalid,
                                "Background travel conflicts with CharacterEncounter authority.",
                                "CharacterId=" + t.CharacterId);
                        continue;
                    }
                    if (existing.HasContinuousWorldPosition &&
                        (((!string.IsNullOrEmpty(t.SurfaceId) || t.IsSurfaceRoute) &&
                          ((t.LocationKind == (int)BackgroundCharacterLocationKind.AtWorldSite) !=
                           (existing.Mode == PartyWorldPresenceMode.AtSite))) ||
                         !SamePosition(existing.ContinuousWorldPosition, pos) ||
                         (!string.IsNullOrEmpty(t.SurfaceId) &&
                          !string.IsNullOrEmpty(existing.PersonalSurfaceId) &&
                          !string.Equals(t.SurfaceId, existing.PersonalSurfaceId,
                              StringComparison.Ordinal))))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Background travel conflicts with CharacterWorldPresence authority.",
                            "CharacterId=" + t.CharacterId);
                    // Modern CharacterWorldPresence is primary. Never downgrade its exact anchor.
                    if (existing.HasContinuousWorldPosition)
                        goto RestoreLegacyRoute;
                }

                if (t.LocationKind == (int)BackgroundCharacterLocationKind.AtWorldSite &&
                    !string.IsNullOrEmpty(t.SiteId))
                {
                    if (!string.IsNullOrEmpty(t.SurfaceId))
                        world.WorldPresence.SetAtSiteWithAnchor(id, t.SiteId, pos, t.SurfaceId);
                    else
                        world.WorldPresence.SetAtSite(id, t.SiteId);
                }
                else
                {
                    var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
                    // SurfaceGround is intentionally unavailable in phase one.  Preserve the DTO
                    // provenance verbatim; post-content finalization validates or migrates it.
                    world.WorldPresence.SetAtWorldPosition(id, pos, derived,
                        t.SurfaceId ?? string.Empty);
                }

            RestoreLegacyRoute:
                if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                {
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
            return Result.Success();
        }

        public static Result RestoreBackgroundSurfaceTravels(
            SimulationWorld world, List<BackgroundCharacterTravelSnapshotDto> travels)
        {
            if (world == null || travels == null ||
                !ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                return Result.Success();
            var seen = new HashSet<ulong>();
            for (var i = 0; i < travels.Count; i++)
            {
                var item = travels[i];
                if (item == null || item.CharacterId == 0)
                    continue;
                if (!seen.Add(item.CharacterId))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Duplicate Background Surface travel snapshot.",
                        "CharacterId=" + item.CharacterId);
            }
            for (var i = 0; i < travels.Count; i++)
            {
                var item = travels[i];
                if (item == null || item.CharacterId == 0) continue;
                var id = new EntityId(item.CharacterId);
                if (SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, id) ||
                    CharacterEncounterService.OwnsParticipantSpatialState(world, id))
                {
                    if (item.IsTraveling || item.IsSurfaceRoute || !string.IsNullOrEmpty(item.SurfaceId))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Background travel conflicts with another spatial owner.",
                            "CharacterId=" + item.CharacterId);
                    continue;
                }
                if (!Finite(item.WorldX) || !Finite(item.WorldY))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Background Character position is invalid after content shell.",
                        "CharacterId=" + item.CharacterId);

                var position = new WorldVec2(item.WorldX, item.WorldY);
                XianXia.Core.World.Surface.SurfaceGroundNavigation surface = null;
                if (!string.IsNullOrEmpty(item.SurfaceId))
                {
                    if (!world.SurfaceGround.TryGet(item.SurfaceId, out surface) || surface == null ||
                        !surface.Contains(position.X, position.Y))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Background Character explicit SurfaceId conflicts with its position.",
                            "CharacterId=" + item.CharacterId + " SurfaceId=" + item.SurfaceId);
                }
                else
                {
                    TryResolveUniqueContainingSurface(world, position, out surface, out var matchCount);
                    if (matchCount != 1)
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Legacy Background Character position does not identify one Surface.",
                            "CharacterId=" + item.CharacterId + " MatchCount=" + matchCount);
                }

                if (!string.IsNullOrEmpty(item.DestinationSiteId))
                {
                    if (!world.SurfaceGround.TryResolveSiteArrival(
                            item.DestinationSiteId, out var destinationSurfaceId, out var arrival) ||
                        !string.Equals(surface.SurfaceId, destinationSurfaceId, StringComparison.Ordinal))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Background destination Site is on a different or unknown Surface.",
                            "CharacterId=" + item.CharacterId +
                            " DestinationSiteId=" + item.DestinationSiteId);
                    if (item.IsSurfaceRoute &&
                        (!Finite(item.SurfaceDestinationX) || !Finite(item.SurfaceDestinationY) ||
                         !SamePosition(new WorldVec2(item.SurfaceDestinationX, item.SurfaceDestinationY), arrival)))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Background Surface destination conflicts with SiteArrival.",
                            "CharacterId=" + item.CharacterId);
                }

                var hexSize = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                    ? world.LegacyHexWorld.HexSize : 1f;
                if (item.LocationKind == (int)BackgroundCharacterLocationKind.AtWorldSite &&
                    !string.IsNullOrEmpty(item.SiteId))
                    world.WorldPresence.SetAtSiteWithAnchor(
                        id, item.SiteId, position, surface.SurfaceId);
                else
                    world.WorldPresence.SetAtWorldPosition(
                        id, position, HexMath.WorldToHex(position.X, position.Y, hexSize),
                        surface.SurfaceId);

                if (!item.IsTraveling)
                {
                    world.BackgroundCharacterTravel.Remove(id);
                    continue;
                }
                if (string.IsNullOrEmpty(item.DestinationSiteId))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Moving Background Surface travel has no destination Site.",
                        "CharacterId=" + item.CharacterId);
                world.BackgroundCharacterTravel.Remove(id);
                var restored = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                    world, id, item.DestinationSiteId);
                if (restored.IsFailure)
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Background Surface travel could not be rebuilt.",
                        "CharacterId=" + item.CharacterId +
                        " DestinationSiteId=" + item.DestinationSiteId +
                        " SurfaceId=" + (item.SurfaceId ?? string.Empty) +
                        " Reason=" + restored.Error);
                if (!world.BackgroundCharacterTravel.TryGet(id, out var motion) || motion == null ||
                    !motion.IsMoving || !motion.IsSurfaceRoute ||
                    (!string.IsNullOrEmpty(item.SurfaceId) &&
                     !string.Equals(item.SurfaceId, motion.SurfaceId, StringComparison.Ordinal)))
                {
                    world.BackgroundCharacterTravel.Remove(id);
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Background Surface travel restored with inconsistent authority.",
                        "CharacterId=" + item.CharacterId +
                        " DestinationSiteId=" + item.DestinationSiteId +
                        " SurfaceId=" + (item.SurfaceId ?? string.Empty));
                }
                motion.LastProcessedWorldTick = item.LastProcessedWorldTick > 0
                    ? item.LastProcessedWorldTick
                    : world.Tick.Value;
            }
            return Result.Success();
        }

        static bool SamePosition(WorldVec2 a, WorldVec2 b) =>
            Math.Abs(a.X - b.X) <= 0.0001f && Math.Abs(a.Y - b.Y) <= 0.0001f;

        /// <summary>
        /// Second phase of PlayerParty travel restore. Call only after SurfaceGround and authored
        /// Site shells are registered. This method restores Domain authority only; the Host owns
        /// the decision whether ordinary Outdoor member presence may be reconciled.
        /// </summary>
        public static Result FinalizePlayerPartyTravelAfterContentShell(
            SimulationWorld world, PlayerPartyTravelSnapshotDto travel)
        {
            if (world?.PlayerPartyTravel == null || travel == null || !travel.HasPosition)
                return Result.Success();
            if (!ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                return Result.Success();
            if (!Finite(travel.WorldX) || !Finite(travel.WorldY))
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "PlayerParty Continuous position is invalid.");

            var position = new WorldVec2(travel.WorldX, travel.WorldY);
            XianXia.Core.World.Surface.SurfaceGroundNavigation surface = null;
            var savedSurfaceId = travel.SurfaceId ?? string.Empty;
            if (!string.IsNullOrEmpty(savedSurfaceId))
            {
                if (!world.SurfaceGround.TryGet(savedSurfaceId, out surface) || surface == null ||
                    !surface.Contains(position.X, position.Y))
                {
                    TryResolveUniqueContainingSurface(world, position, out var containing, out var matchCount);
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "PlayerParty snapshot SurfaceId does not contain its exact position.",
                        "SavedSurfaceId=" + savedSurfaceId +
                        " Position=" + position +
                        " ResolvedContainingSurface=" + (containing?.SurfaceId ?? string.Empty) +
                        " MatchCount=" + matchCount);
                }
            }
            else
            {
                TryResolveUniqueContainingSurface(world, position, out surface, out var matchCount);
                if (matchCount != 1)
                    surface = null;
            }

            var oldContinuousSite =
                travel.LocationKind == (int)PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(travel.SiteId) &&
                world.Strategic.Sites.TryGet(travel.SiteId, out var site) && site != null &&
                WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site);
            if (travel.LocationKind == (int)PlayerPartyLocationKind.AtWorldSite && !oldContinuousSite)
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "PlayerParty AtWorldSite snapshot is not a Continuous Outdoor Site.",
                    travel.SiteId ?? string.Empty);

            if (surface == null && oldContinuousSite && string.IsNullOrEmpty(savedSurfaceId) &&
                world.SurfaceGround.TryResolveSiteArrival(
                    travel.SiteId, out var arrivalSurfaceId, out var arrival) &&
                world.SurfaceGround.TryGet(arrivalSurfaceId, out var arrivalSurface) &&
                arrivalSurface != null && arrivalSurface.Contains(arrival.X, arrival.Y))
            {
                surface = arrivalSurface;
                position = arrival;
            }
            if (surface == null)
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "PlayerParty exact position does not identify one registered Surface.",
                    "Position=" + position + " SavedSurfaceId=" + savedSurfaceId);

            var hexSize = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                ? world.LegacyHexWorld.HexSize
                : 1f;
            var motion = world.PlayerPartyTravel;
            motion.SetAtSurfacePosition(surface.SurfaceId, position,
                HexMath.WorldToHex(position.X, position.Y, hexSize));
            if (!string.IsNullOrEmpty(travel.CurrentOutdoorWorldSiteId))
                motion.SetCurrentOutdoorWorldSiteContext(travel.CurrentOutdoorWorldSiteId);
            else if (oldContinuousSite)
                motion.SetCurrentOutdoorWorldSiteContext(travel.SiteId);
            else if (WorldSitePhysicalRegionQuery.TryResolve(world, position, out var currentSite))
                motion.SetCurrentOutdoorWorldSiteContext(currentSite.SiteId);

            if (travel.IsMoving)
            {
                if (!travel.HasContinuousPhysicalDestination ||
                    !Finite(travel.DestinationWorldX) || !Finite(travel.DestinationWorldY) ||
                    !PlayerPartySurfaceTravelService.TryResumeAfterRestore(
                        world,
                        new WorldVec2(travel.DestinationWorldX, travel.DestinationWorldY),
                        travel.DestinationSiteId,
                        travel.ArrivalRadius))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "PlayerParty Surface travel route could not be rebuilt.",
                        "SurfaceId=" + surface.SurfaceId +
                        " Position=" + position +
                        " Destination=(" + travel.DestinationWorldX + "," + travel.DestinationWorldY + ")");
            }

            return ValidatePlayerPartyContinuousAuthorityAfterContentShell(world);
        }

        public static Result ValidatePlayerPartyContinuousAuthorityAfterContentShell(
            SimulationWorld world)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition ||
                !ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                return Result.Success();
            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                string.IsNullOrEmpty(motion.SurfaceId) ||
                !world.SurfaceGround.TryGet(motion.SurfaceId, out var surface) || surface == null ||
                !surface.Contains(motion.WorldPosition.X, motion.WorldPosition.Y))
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "PlayerParty Continuous Surface authority is invalid after content rehydrate.",
                    "LocationKind=" + motion.LocationKind +
                    " SurfaceId=" + (motion.SurfaceId ?? string.Empty) +
                    " WorldPosition=" + motion.WorldPosition);
            var hexSize = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                ? world.LegacyHexWorld.HexSize
                : 1f;
            var derived = HexMath.WorldToHex(motion.WorldPosition.X, motion.WorldPosition.Y, hexSize);
            if (!motion.LegacyCurrentHex.Equals(derived))
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "PlayerParty compatibility LegacyCurrentHex is not derived from WorldPosition.",
                    "Saved=" + motion.LegacyCurrentHex + " Derived=" + derived);
            if (motion.IsMoving &&
                (motion.ExecutionMode != PlayerPartyTravelExecutionMode.SurfaceVisible ||
                 !motion.HasContinuousPhysicalDestination ||
                 motion.ContinuousSurfaceRoute.Count == 0 ||
                 motion.ContinuousSurfaceRouteIndex < 0 ||
                 motion.ContinuousSurfaceRouteIndex > motion.ContinuousSurfaceRoute.Count))
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "PlayerParty moving Surface authority is incomplete.",
                    "ExecutionMode=" + motion.ExecutionMode +
                    " Route=" + motion.ContinuousSurfaceRouteIndex + "/" +
                    motion.ContinuousSurfaceRoute.Count);
            if (!motion.IsMoving && motion.ExecutionMode != PlayerPartyTravelExecutionMode.None)
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "Idle PlayerParty snapshot retained a travel executor.",
                    motion.ExecutionMode.ToString());
            return Result.Success();
        }

        static bool TryResolveUniqueContainingSurface(
            SimulationWorld world,
            WorldVec2 position,
            out XianXia.Core.World.Surface.SurfaceGroundNavigation surface,
            out int matchCount)
        {
            surface = null;
            matchCount = 0;
            if (world?.SurfaceGround?.Registered == null)
                return false;
            foreach (var pair in world.SurfaceGround.Registered)
            {
                var candidate = pair.Value;
                if (candidate == null || !candidate.Contains(position.X, position.Y))
                    continue;
                matchCount++;
                if (matchCount == 1)
                    surface = candidate;
                else
                    surface = null;
            }
            return matchCount == 1;
        }

        public static void RestorePlayerPartyTravel(SimulationWorld world, PlayerPartyTravelSnapshotDto travel)
        {
            if (world?.PlayerPartyTravel == null || travel == null || !travel.HasPosition)
                return;

            var motion = world.PlayerPartyTravel;
            // CW-U4.1: old PlayerParty AttackArmy/attack-chase orders are retired on load.
            // Preserve the canonical position, restore Idle, and never auto-declare war/create an encounter.
            var pos = new WorldVec2(travel.WorldX, travel.WorldY);
            var hexSize = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                ? world.LegacyHexWorld.HexSize
                : 1f;

            if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
            {
                XianXia.Core.World.Surface.SurfaceGroundNavigation surface = null;
                if (!string.IsNullOrEmpty(travel.SurfaceId))
                    world.SurfaceGround.TryGet(travel.SurfaceId, out surface);
                if (surface == null || !surface.Contains(pos.X, pos.Y))
                    world.SurfaceGround.TryResolveContaining(pos, out surface);

                var continuousSite = travel.LocationKind == (int)PlayerPartyLocationKind.AtWorldSite &&
                                     !string.IsNullOrEmpty(travel.SiteId) &&
                                     world.Strategic.Sites.TryGet(travel.SiteId, out var savedSite) &&
                                     savedSite != null &&
                                     WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(savedSite);
                if (continuousSite && (surface == null || !surface.Contains(pos.X, pos.Y)) &&
                    world.SurfaceGround.TryResolveSiteArrival(
                        travel.SiteId, out var arrivalSurfaceId, out var arrival) &&
                    world.SurfaceGround.TryGet(arrivalSurfaceId, out var arrivalSurface))
                {
                    pos = arrival;
                    surface = arrivalSurface;
                }

                if (surface == null &&
                    ((travel.WorldX == 0f && travel.WorldY == 0f &&
                      (travel.CurrentHexQ != 0 || travel.CurrentHexR != 0)) ||
                     !world.SurfaceGround.TryResolveContaining(pos, out _)))
                {
                    HexMath.ToWorldPosition(new HexCoord(travel.CurrentHexQ, travel.CurrentHexR),
                        hexSize, out var migratedX, out var migratedY);
                    var migrated = new WorldVec2(migratedX, migratedY);
                    if (world.SurfaceGround.TryResolveContaining(migrated, out var migratedSurface))
                    {
                        pos = migrated;
                        surface = migratedSurface;
                    }
                }

                if (surface != null && surface.Contains(pos.X, pos.Y))
                {
                    motion.SetAtSurfacePosition(surface.SurfaceId, pos,
                        HexMath.WorldToHex(pos.X, pos.Y, hexSize));
                    if (!string.IsNullOrEmpty(travel.CurrentOutdoorWorldSiteId))
                        motion.SetCurrentOutdoorWorldSiteContext(travel.CurrentOutdoorWorldSiteId);
                    else if (continuousSite)
                        motion.SetCurrentOutdoorWorldSiteContext(travel.SiteId);
                    else if (WorldSitePhysicalRegionQuery.TryResolve(world, pos, out var currentSite))
                        motion.SetCurrentOutdoorWorldSiteContext(currentSite.SiteId);
                    if (travel.IsMoving && travel.HasContinuousPhysicalDestination)
                        PlayerPartySurfaceTravelService.TryResumeAfterRestore(
                            world, new WorldVec2(travel.DestinationWorldX, travel.DestinationWorldY),
                            travel.DestinationSiteId, travel.ArrivalRadius);
                    return;
                }
            }

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
                // LegacyPresenceHex center 覆盖，否则 Load 后 LocalVisible 出口路径会以错误起点重建。
                motion.RestoreIdleAtLegacyWorldSite(
                    travel.SiteId,
                    sitePosition,
                    new HexCoord(travel.CurrentHexQ, travel.CurrentHexR));
                if (travel.IsMoving && travel.HasContinuousPhysicalDestination &&
                    ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                {
                    motion.SetAtLegacyWorldPosition(motion.WorldPosition, motion.LegacyCurrentHex);
                    PlayerPartySurfaceTravelService.TryResumeAfterRestore(
                        world, new WorldVec2(travel.DestinationWorldX, travel.DestinationWorldY),
                        travel.DestinationSiteId, travel.ArrivalRadius);
                }
                return;
            }

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
            motion.SetAtLegacyWorldPosition(pos, derived);
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
