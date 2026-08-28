using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>从 Snapshot 恢复 PlayerPartyRuntime（不创建 Character Domain Entity）。</summary>
    public static class PlayerPartySnapshotRestore
    {
        public static void Capture(PlayerPartyRuntime party, StrategicSnapshotDto dto)
        {
            if (party == null || dto == null || !party.HasActive)
                return;

            var snap = new PlayerPartyRuntimeSnapshotDto
            {
                ActiveCharacterId = party.ActiveCharacterId.Value
            };
            for (var i = 0; i < party.Members.Count; i++)
                snap.MemberCharacterIds.Add(party.Members[i].Value);
            dto.PlayerParty = snap;
        }

        public static void Apply(SimulationWorld world, PlayerPartyRuntime party, PlayerPartyRuntimeSnapshotDto dto)
        {
            if (world == null || party == null)
                return;

            party.Reset();
            if (dto != null && dto.MemberCharacterIds != null && dto.MemberCharacterIds.Count > 0)
            {
                if (TryApplyExplicit(world, party, dto))
                    return;
            }

            TryInferFromWorldPresence(world, party);
        }

        static bool TryApplyExplicit(
            SimulationWorld world,
            PlayerPartyRuntime party,
            PlayerPartyRuntimeSnapshotDto dto)
        {
            var members = new List<EntityId>(dto.MemberCharacterIds.Count);
            for (var i = 0; i < dto.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(dto.MemberCharacterIds[i]);
                if (id.IsNone || !world.Entities.TryGet(id, out _))
                    continue;
                if (!Contains(members, id))
                    members.Add(id);
            }

            if (members.Count < 1)
                return false;

            var active = new EntityId(dto.ActiveCharacterId);
            if (active.IsNone || !Contains(members, active))
                active = members[0];

            return party.TryRestoreFromSnapshot(active, members, out _);
        }

        /// <summary>
        /// 旧存档无 PlayerParty 字段：从 PlayerPartyTravel Site + Character WorldPresence 推断成员。
        /// </summary>
        public static bool TryInferFromWorldPresence(SimulationWorld world, PlayerPartyRuntime party)
        {
            if (world == null || party == null)
                return false;

            var travel = world.PlayerPartyTravel;
            if (travel == null || !travel.HasPosition)
                return false;

            var playerFaction = world.Strategic?.PlayerFactionId ?? string.Empty;
            var members = new List<EntityId>(PlayerPartyRuntime.MaxMembers);

            if (travel.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(travel.SiteId))
            {
                CollectPlayerCharactersAtSite(world, travel.SiteId, playerFaction, members);
            }
            else
            {
                foreach (var entity in world.Entities.All)
                {
                    if (entity == null || (entity.Tags & EntityTag.Character) == 0)
                        continue;
                    if (!IsPlayerFactionCharacter(world, entity.Id, playerFaction))
                        continue;
                    if (ArmyService.TryGetArmyForCharacter(world, entity.Id, out _))
                        continue;
                    if (!Contains(members, entity.Id))
                        members.Add(entity.Id);
                }
            }

            if (members.Count < 1)
                return false;

            members.Sort((a, b) => a.Value.CompareTo(b.Value));
            return party.TryRestoreFromSnapshot(members[0], members, out _);
        }

        static void CollectPlayerCharactersAtSite(
            SimulationWorld world,
            string siteId,
            string playerFaction,
            List<EntityId> into)
        {
            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || presence.EntityId.IsNone)
                    continue;
                if (presence.Mode != PartyWorldPresenceMode.AtSite ||
                    !string.Equals(presence.SiteId, siteId, System.StringComparison.Ordinal))
                    continue;
                if (!world.Entities.TryGet(presence.EntityId, out var entity) ||
                    (entity.Tags & EntityTag.Character) == 0)
                    continue;
                if (!IsPlayerFactionCharacter(world, presence.EntityId, playerFaction))
                    continue;
                if (ArmyService.TryGetArmyForCharacter(world, presence.EntityId, out _))
                    continue;
                if (!Contains(into, presence.EntityId))
                    into.Add(presence.EntityId);
            }
        }

        static bool IsPlayerFactionCharacter(SimulationWorld world, EntityId id, string playerFaction)
        {
            if (string.IsNullOrEmpty(playerFaction))
                return true;
            var faction = ArmyService.ResolveCharacterFactionId(world, id);
            return string.Equals(faction, playerFaction, System.StringComparison.Ordinal);
        }

        static bool Contains(List<EntityId> list, EntityId id)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == id)
                    return true;
            }

            return false;
        }
    }
}
