using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
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

            if (dto.WorldSiteOwners != null)
            {
                for (var i = 0; i < dto.WorldSiteOwners.Count; i++)
                {
                    var site = dto.WorldSiteOwners[i];
                    if (site == null || string.IsNullOrEmpty(site.SiteId))
                        continue;
                    WorldSiteOwnershipService.SetOwner(world, site.SiteId, site.OwnerFactionId ?? string.Empty);
                }
            }

            if (dto.TerritoryRegionControllers != null)
            {
                for (var i = 0; i < dto.TerritoryRegionControllers.Count; i++)
                {
                    var region = dto.TerritoryRegionControllers[i];
                    if (region == null || string.IsNullOrEmpty(region.RegionId))
                        continue;
                    TerritoryControlService.SetRegionController(
                        world, region.RegionId, region.ControlFactionId ?? string.Empty);
                }
            }

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
            return Result.Success();
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
                };
                for (var p = 0; p < armyMotion.HexPathCount; p++)
                {
                    var coord = armyMotion.HexPath[p];
                    armyDto.HexPath.Add(new HexCoordSnapshotDto { Q = coord.Q, R = coord.R });
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
                        SiteId = presence.SiteId
                    });
                    continue;
                }

                if (presence.UsesHexPresence)
                {
                    dto.CharacterWorldPresences.Add(new CharacterWorldPresenceSnapshotDto
                    {
                        CharacterId = presence.EntityId.Value,
                        Mode = (int)PartyWorldPresenceMode.AtHex,
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
                    OwnerFactionId = site.OwnerFactionId ?? string.Empty
                });
            }

            foreach (var regionKv in world.Strategic.TerritoryRegions.Regions)
            {
                var region = regionKv.Value;
                if (region == null || string.IsNullOrEmpty(region.RegionId))
                    continue;
                dto.TerritoryRegionControllers.Add(new TerritoryRegionControllerSnapshotDto
                {
                    RegionId = region.RegionId,
                    ControlFactionId = region.ControlFactionId ?? string.Empty
                });
            }
            dto.HasFactionFlagSnapshotAuthority = true;
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value; if (flag == null) continue;
                dto.FactionFlags.Add(new FactionFlagSnapshotDto { FlagId=flag.FlagId, FactionId=flag.FactionId,
                    AnchorQ=flag.AnchorHex.Q, AnchorR=flag.AnchorHex.R, EstablishedOrder=flag.EstablishedOrder,
                    CurrentHp=flag.CurrentHp, MaxHp=flag.MaxHp, HasLocalPosition=flag.HasLocalPosition, LocalX=flag.LocalX, LocalZ=flag.LocalZ });
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

            foreach (var kv in world.Strategic.CaptureObjectives.All)
            {
                var obj = kv.Value;
                if (obj == null)
                    continue;
                dto.CaptureObjectives.Add(new CaptureObjectiveSnapshotDto
                {
                    ObjectiveId = obj.ObjectiveId,
                    SiteId = obj.SiteId,
                    WorkAreaId = obj.WorkAreaId,
                    CurrentHp = obj.CurrentHp,
                    MaxHp = obj.MaxHp,
                    OccupyProgressSeconds = obj.OccupyProgressSeconds,
                    OccupyHoldSeconds = obj.OccupyHoldSeconds,
                    Completed = obj.Completed
                });
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
                    CurrentHexR = motion.CurrentHex.R
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
            PendingEngagementSnapshotRestore.Capture(world, dto);
            return dto;
        }

        public static Result Restore(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic == null || dto == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Strategic snapshot restore requires world and dto.");

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
            world.Strategic.Wars.Clear();
            world.Strategic.Diplomacy.Clear();
            world.Strategic.Alliances.Clear();
            world.Strategic.Vassalages.Clear();
            world.Strategic.RetreatingArmies.Clear();
            world.Strategic.CaptureObjectives.Clear();

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
                        var army = BuildFormalArmyFromSnapshot(a);
                        world.Strategic.FormalArmies.Register(army);
                    }
                }
            }

            var memberships = RestoreArmyMemberships(world, dto.ArmyMemberships);
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
                    if (p.Mode == (int)PartyWorldPresenceMode.AtSite &&
                        !string.IsNullOrEmpty(p.SiteId))
                    {
                        world.WorldPresence.SetAtSite(id, p.SiteId);
                        continue;
                    }

                    if (p.Mode == (int)PartyWorldPresenceMode.AtHex &&
                        p.HexQ != int.MinValue &&
                        p.HexR != int.MinValue)
                    {
                        var hex = new HexCoord(p.HexQ, p.HexR);
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

            if (dto.CaptureObjectives != null)
            {
                for (var i = 0; i < dto.CaptureObjectives.Count; i++)
                {
                    var c = dto.CaptureObjectives[i];
                    if (c == null || string.IsNullOrEmpty(c.ObjectiveId))
                        continue;
                    world.Strategic.CaptureObjectives.Register(new CaptureObjectiveState
                    {
                        ObjectiveId = c.ObjectiveId,
                        SiteId = c.SiteId ?? string.Empty,
                        WorkAreaId = c.WorkAreaId ?? string.Empty,
                        CurrentHp = c.CurrentHp,
                        MaxHp = c.MaxHp,
                        OccupyProgressSeconds = c.OccupyProgressSeconds,
                        OccupyHoldSeconds = c.OccupyHoldSeconds,
                        Completed = c.Completed
                    });
                }
            }

            RestorePlayerPartyTravel(world, dto.PlayerPartyTravel);
            RestoreBackgroundCharacterTravels(world, dto.BackgroundCharacterTravels);
            LoadedLocalMapPlacementSnapshotRestore.BeginRestoreFromSnapshot(dto);
            PendingEngagementSnapshotRestore.Restore(world, dto);
            return Result.Success();
        }

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

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                if (kv.Value != null)
                    FormalArmyMemberPresenceSync.SyncAll(world, kv.Value);
            }

            ArmyStackAdapter.EnsurePresentationStacksFromFormalArmies(world);

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                if (kv.Value != null)
                    ArmyHexPursuitService.RestoreAttackOrderIfNeeded(world, kv.Value);
            }
        }

        static FormalArmy BuildFormalArmyFromSnapshot(FormalArmySnapshotDto a)
        {
            var army = new FormalArmy
            {
                ArmyId = a.ArmyId,
                FactionId = a.FactionId ?? string.Empty,
                LeaderCharacterId = new EntityId(a.LeaderCharacterId),
                State = (FormalArmyState)a.State
            };
            army.ReplaceMembers(a.MemberCharacterIds);
            army.UsesHexStrategicPosition = true;
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
                    world.WorldPresence.SetAtWorldPosition(id, pos, derived);
                }

                if (!t.IsTraveling || t.HexPath == null || t.HexPath.Count < 2)
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

        static void RestorePlayerPartyTravel(SimulationWorld world, PlayerPartyTravelSnapshotDto travel)
        {
            if (world?.PlayerPartyTravel == null || travel == null || !travel.HasPosition)
                return;

            var motion = world.PlayerPartyTravel;
            // Phase 5S-B2-3.5：pursuit target 与普通 PlayerParty travel 同契约 —— Save→Load 后
            // Movement 恢复 Idle，pursuit 亦清空（不单独引入更强 persistence）。
            motion.ClearAttackOrder();
            if (travel.LocationKind == (int)PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(travel.SiteId))
            {
                // Snapshot 的 AtWorldSite WorldX/Y 是 Site 内连续 Canonical 位置；不能用
                // PresenceHex center 覆盖，否则 Load 后 LocalVisible 出口路径会以错误起点重建。
                motion.RestoreIdleAtWorldSite(
                    travel.SiteId,
                    new WorldVec2(travel.WorldX, travel.WorldY),
                    new HexCoord(travel.CurrentHexQ, travel.CurrentHexR));
                return;
            }

            var pos = new WorldVec2(travel.WorldX, travel.WorldY);
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
            motion.SetAtWorldPosition(pos, derived);
        }
    }
}
