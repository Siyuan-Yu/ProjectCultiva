using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>??? Snapshot v6 ??????Pure Hex?? node/route DTO??</summary>
    public static class StrategicSnapshotHelper
    {
        public static StrategicSnapshotDto Capture(SimulationWorld world)
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
                    CurrentHexQ = army.CurrentHex.Q,
                    CurrentHexR = army.CurrentHex.R,
                    DestinationHexQ = army.DestinationHex.Q,
                    DestinationHexR = army.DestinationHex.R,
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
                    SegmentProgress = armyMotion.SegmentProgress,
                    SegmentIndex = armyMotion.SegmentIndex,
                };
                for (var p = 0; p < army.HexPathCount; p++)
                {
                    var coord = army.HexPath[p];
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
                        HexR = presence.HexR
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
                        WorldX = presence.WorldPosX,
                        WorldY = presence.WorldPosY
                    });
                }
            }

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null || string.IsNullOrEmpty(site.OwnerFactionId))
                    continue;
                dto.WorldSiteOwners.Add(new WorldSiteOwnerSnapshotDto
                {
                    SiteId = site.SiteId,
                    OwnerFactionId = site.OwnerFactionId
                });
            }

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

            return dto;
        }

        public static void Restore(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.Strategic == null || dto == null)
                return;

            world.Strategic.PlayerFactionId = dto.PlayerFactionId ?? string.Empty;
            world.Strategic.Ch01FormationScenarioCompat = dto.Ch01FormationScenarioCompat;
            world.Strategic.FormalArmies.Clear();
            world.Strategic.Wars.Clear();
            world.Strategic.Alliances.Clear();
            world.Strategic.Vassalages.Clear();
            world.Strategic.RetreatingArmies.Clear();
            world.Strategic.CaptureObjectives.Clear();

            if (dto.FormalArmies != null)
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

                        world.Strategic.FormalArmies.Register(army);
                        FormalArmySnapshotRestore.Apply(world, army, a);
                    }
                }
            }

            if (dto.ArmyMemberships != null)
            {
                for (var i = 0; i < dto.ArmyMemberships.Count; i++)
                {
                    var m = dto.ArmyMemberships[i];
                    if (m == null || m.CharacterId == 0 || string.IsNullOrEmpty(m.ArmyId))
                        continue;
                    if (!world.Entities.TryGet(new EntityId(m.CharacterId), out var entity))
                        continue;
                    ArmyInvariants.EnsureMembershipComponent(entity);
                    entity.Get<ArmyMembershipComponent>().SetArmyId(m.ArmyId);
                }
            }

            if (dto.CharacterWorldPresences != null)
            {
                for (var i = 0; i < dto.CharacterWorldPresences.Count; i++)
                {
                    var p = dto.CharacterWorldPresences[i];
                    if (p == null || p.CharacterId == 0)
                        continue;
                    var id = new EntityId(p.CharacterId);
                    if (!world.Entities.TryGet(id, out _))
                        continue;
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
                        world.WorldPresence.SetAtHex(id, new HexCoord(p.HexQ, p.HexR));
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
                        Completed = c.Completed
                    });
                }
            }

            RestorePlayerPartyTravel(world, dto.PlayerPartyTravel);
            RestoreBackgroundCharacterTravels(world, dto.BackgroundCharacterTravels);
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

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            var motion = world.PlayerPartyTravel;
            if (travel.LocationKind == (int)PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(travel.SiteId))
            {
                if (world.Strategic.Sites.TryResolveSitePresenceHex(travel.SiteId, out var presence))
                    motion.SetAtWorldSite(travel.SiteId, presence, hexSize);
                else
                    motion.SetAtWorldSite(travel.SiteId, new HexCoord(travel.CurrentHexQ, travel.CurrentHexR), hexSize);
                return;
            }

            var pos = new WorldVec2(travel.WorldX, travel.WorldY);
            var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
            motion.SetAtWorldPosition(pos, derived);
        }
    }
}
