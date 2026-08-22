using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>Phase K：战略层 Snapshot v2 捕获／恢复。</summary>
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
                var armyDto = new FormalArmySnapshotDto
                {
                    ArmyId = army.ArmyId,
                    FactionId = army.FactionId,
                    LeaderCharacterId = army.LeaderCharacterId.Value,
                    NodeId = army.NodeId,
                    State = (int)army.State,
                    RouteId = army.RouteId,
                    DestNodeId = army.DestNodeId,
                    RemainingTravelTicks = army.RemainingTravelTicks,
                    TravelTotalTicks = army.TravelTotalTicks,
                    RouteAnchorProgress = army.RouteAnchorProgress
                };
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

            foreach (var kv in world.WorldGraph.Nodes)
            {
                var node = kv.Value;
                if (node == null || string.IsNullOrEmpty(node.OwnerId))
                    continue;
                dto.NodeOwners.Add(new NodeOwnerSnapshotDto
                {
                    NodeId = node.Id,
                    OwnerFactionId = node.OwnerId
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
                    NodeId = retreat.NodeId
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
                    NodeId = obj.NodeId,
                    WorkAreaId = obj.WorkAreaId,
                    CurrentHp = obj.CurrentHp,
                    MaxHp = obj.MaxHp,
                    Completed = obj.Completed
                });
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
                        NodeId = a.NodeId ?? string.Empty,
                        State = (FormalArmyState)a.State,
                        RouteId = a.RouteId ?? string.Empty,
                        DestNodeId = a.DestNodeId ?? string.Empty,
                        RemainingTravelTicks = a.RemainingTravelTicks,
                        TravelTotalTicks = a.TravelTotalTicks,
                        RouteAnchorProgress = a.RouteAnchorProgress
                    };
                    army.ReplaceMembers(a.MemberCharacterIds);
                    world.Strategic.FormalArmies.Register(army);
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

            if (dto.NodeOwners != null)
            {
                for (var i = 0; i < dto.NodeOwners.Count; i++)
                {
                    var n = dto.NodeOwners[i];
                    if (n == null || string.IsNullOrEmpty(n.NodeId))
                        continue;
                    if (world.WorldGraph.TryGetNode(n.NodeId, out var node) && node != null)
                        node.OwnerId = n.OwnerFactionId ?? string.Empty;
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
                        NodeId = r.NodeId ?? string.Empty
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
                        NodeId = c.NodeId ?? string.Empty,
                        WorkAreaId = c.WorkAreaId ?? string.Empty,
                        CurrentHp = c.CurrentHp,
                        MaxHp = c.MaxHp,
                        Completed = c.Completed
                    });
                }
            }
        }
    }
}
