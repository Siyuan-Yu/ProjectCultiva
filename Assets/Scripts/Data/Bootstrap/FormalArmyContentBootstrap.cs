using System.Collections.Generic;
using XianXia.Core.Bootstrap;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>
    /// Phase 5S：OpeningScenario.InitialFormalArmyIds → DefinitionRegistry.FormalArmies
    /// → 逐个 spawn member Entity → FormalArmy → ArmyStack 兼容视图。
    /// 角色 gameplay 数据只能来自 CharacterDefinition（本类禁止 SetBase Attack/Defense/Realm）。
    /// 幂等：runtime ArmyId 已存在（Save/Load 恢复）时跳过。
    /// </summary>
    public static class FormalArmyContentBootstrap
    {
        public static Result Apply(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario)
        {
            if (world?.Strategic == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "FormalArmy bootstrap requires world + registry.");
            if (scenario == null)
                return Result.Success();
            if (scenario.InitialFormalArmyIds == null || scenario.InitialFormalArmyIds.Count == 0)
                return Result.Success();

            for (var i = 0; i < scenario.InitialFormalArmyIds.Count; i++)
            {
                var idText = scenario.InitialFormalArmyIds[i];
                if (string.IsNullOrWhiteSpace(idText))
                    continue;

                var parsed = DefinitionId.Parse(idText.Trim());
                if (parsed.IsFailure)
                    return Result.Failure(parsed.Error);
                if (!registry.TryGetFormalArmy(parsed.Value, out var def) || def == null)
                {
                    return Result.Failure(
                        ErrorCode.NotFound,
                        "Opening scenario references missing FormalArmyDefinition.",
                        idText);
                }

                var applied = ApplyArmy(world, registry, def);
                if (applied.IsFailure)
                    return applied;
            }

            return Result.Success();
        }

        static Result ApplyArmy(
            SimulationWorld world,
            DefinitionRegistry registry,
            FormalArmyDefinition def)
        {
            if (world.Strategic.FormalArmies.TryGet(def.RuntimeArmyId, out _))
                return Result.Success();

            var memberIds = new List<EntityId>(def.Members.Count);
            var leaderId = EntityId.None;
            for (var i = 0; i < def.Members.Count; i++)
            {
                var member = def.Members[i];
                if (member == null || string.IsNullOrWhiteSpace(member.CharacterDefinitionId))
                    continue;

                var built = ContentGameStart.BuildSpawnFromDefinition(
                    registry,
                    member.CharacterDefinitionId,
                    entityKindNpc: true,
                    displayName: member.DisplayName);
                if (built.IsFailure)
                    return Result.Failure(built.Error);
                var spawned = GameStartBootstrap.SpawnIntoWorld(world, built.Value);
                if (spawned.IsFailure)
                    return Result.Failure(spawned.Error);

                var entity = spawned.Value;
                entity.Get<FactionMembershipComponent>().Assign(def.FactionId, FactionRoleKind.Member);
                world.WorldPresence.SetAtSite(entity.Id, def.AssemblySiteId);
                if (member.Leader)
                    leaderId = entity.Id;
                memberIds.Add(entity.Id);
            }

            if (memberIds.Count == 0 || leaderId.IsNone)
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "FormalArmy member spawn produced no members/leader.",
                    def.Id.ToString());
            }

            var created = ArmyService.CreateAuthoredArmy(
                world,
                def.RuntimeArmyId,
                def.FactionId,
                def.AssemblySiteId,
                memberIds,
                leaderId);
            if (created.IsFailure)
                return Result.Failure(created.Error);

            ArmyStackAdapter.EnsureLinkedStackView(world, created.Value, def.RuntimeStackId, def.Name);

            // Optional authored wilderness deployment：initialHex != null 时把
            // FormalArmy 部署到该 Hex（FormalArmy.WorldMotion = Hex authority）。
            // 绝不直接改 stack.CurrentHex / member.WorldPresence 绕过 WorldMotion。
            if (def.InitialHex != null)
            {
                var deploy = DeployArmyToInitialHex(world, created.Value, def);
                if (deploy.IsFailure)
                    return deploy;
            }

            return Result.Success();
        }

        static Result DeployArmyToInitialHex(
            SimulationWorld world,
            FormalArmy army,
            FormalArmyDefinition def)
        {
            if (world.HexWorld == null || !world.HexWorld.HasGrid)
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "formalArmy.initialHex requires a loaded HexWorld.",
                    def.Id.ToString());
            }

            var hex = new HexCoord(def.InitialHex.Q, def.InitialHex.R);
            if (!world.HexWorld.Contains(hex))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "formalArmy.initialHex out of bounds.",
                    def.Id + " (q=" + def.InitialHex.Q + ", r=" + def.InitialHex.R + ")");
            }

            if (world.HexWorld.TryGetCell(hex, out var cell) && cell != null && !cell.IsPassable)
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "formalArmy.initialHex not passable.",
                    def.Id + " (q=" + def.InitialHex.Q + ", r=" + def.InitialHex.R + ")");
            }

            ArmyHexTravelService.InitializeArmyAtHex(world, army, hex);
            if (world.Strategic.Armies.TryGet(def.RuntimeStackId, out var linked) && linked != null)
                ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, linked);
            return Result.Success();
        }
    }
}
