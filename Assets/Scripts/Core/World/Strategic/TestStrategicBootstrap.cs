using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase C 测试夹具：BanditLeader + A/B/C 真实 Character Entity。</summary>
    public static class TestStrategicBootstrap
    {
        const string BanditLeaderDef = "test:bandit_leader";
        const string BanditADef = "test:bandit_a";
        const string BanditBDef = "test:bandit_b";
        const string BanditCDef = "test:bandit_c";

        static readonly string[] BanditMemberKeys =
        {
            "strategic:bandit_leader",
            "strategic:bandit_a",
            "strategic:bandit_b",
            "strategic:bandit_c"
        };

        static readonly string[] BanditDefs = { BanditLeaderDef, BanditADef, BanditBDef, BanditCDef };
        static readonly string[] BanditNames = { "BanditLeader", "BanditA", "BanditB", "BanditC" };

        public static List<EntityId> EnsureBanditCharacters(SimulationWorld world, string nodeId)
        {
            var result = new List<EntityId>(4);
            if (world?.Entities == null)
                return result;

            for (var i = 0; i < BanditMemberKeys.Length; i++)
            {
                if (TryFindByKey(world, BanditNames[i], out var existing))
                {
                    world.WorldPresence.SetAtNode(existing, nodeId);
                    result.Add(existing);
                    continue;
                }

                var created = world.Entities.CreateNpc(
                    new DefinitionId("test", BanditDefs[i]),
                    BanditNames[i]);
                if (created.IsFailure)
                    continue;
                var entity = created.Value;
                entity.Get<FactionMembershipComponent>()
                    .Assign(StrategicFactionCatalog.BanditId, FactionRoleKind.Member);
                world.WorldPresence.SetAtNode(entity.Id, nodeId);
                result.Add(entity.Id);
            }

            return result;
        }

        public static List<EntityId> EnsureBanditScoutCharacters(SimulationWorld world, string nodeId)
        {
            var result = new List<EntityId>(4);
            if (world?.Entities == null)
                return result;

            var scoutNames = new[] { "BanditScoutLeader", "BanditScoutA", "BanditScoutB", "BanditScoutC" };
            var scoutDefs = new[] { "test:bandit_scout_leader", "test:bandit_scout_a", "test:bandit_scout_b", "test:bandit_scout_c" };
            for (var i = 0; i < scoutNames.Length; i++)
            {
                if (TryFindBanditByName(world, scoutNames[i], out var existing))
                {
                    world.WorldPresence.SetAtNode(existing, nodeId);
                    result.Add(existing);
                    continue;
                }

                var created = world.Entities.CreateNpc(
                    new DefinitionId("test", scoutDefs[i]),
                    scoutNames[i]);
                if (created.IsFailure)
                    continue;
                var entity = created.Value;
                entity.Get<FactionMembershipComponent>()
                    .Assign(StrategicFactionCatalog.BanditId, FactionRoleKind.Member);
                world.WorldPresence.SetAtNode(entity.Id, nodeId);
                result.Add(entity.Id);
            }

            return result;
        }

        static bool TryFindBanditByName(SimulationWorld world, string displayName, out EntityId id)
        {
            id = EntityId.None;
            foreach (var entity in world.Entities.All)
            {
                if (!string.Equals(entity.DisplayName, displayName, System.StringComparison.Ordinal))
                    continue;
                if (!entity.TryGet<FactionMembershipComponent>(out var mem) ||
                    !string.Equals(mem.FactionId, StrategicFactionCatalog.BanditId, System.StringComparison.Ordinal))
                    continue;
                id = entity.Id;
                return true;
            }

            return false;
        }

        static bool TryFindByKey(SimulationWorld world, string displayName, out EntityId id)
        {
            return TryFindBanditByName(world, displayName, out id);
        }
    }
}
