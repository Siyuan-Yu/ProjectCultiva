using XianXia.Core.World;
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
            OpeningScenarioDefinition scenario,
            GameStartLookup openingLookup = null)
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

                var applied = ApplyArmy(world, registry, def, openingLookup);
                if (applied.IsFailure)
                    return applied;
            }

            return Result.Success();
        }

        static Result ApplyArmy(
            SimulationWorld world,
            DefinitionRegistry registry,
            FormalArmyDefinition def,
            GameStartLookup openingLookup)
        {
            if (world.Strategic.FormalArmies.TryGet(def.RuntimeArmyId, out _))
                return Result.Success();

            var hasSurfaceDeployment = def.InitialSurfaceDeployment != null ||
                                       def.InitialSurfacePosition != null;
            var deploymentPoint = default(WorldVec2);
            var deploymentSurfaceId = string.Empty;
            if (hasSurfaceDeployment)
            {
                var resolved = ResolveInitialSurfaceDeployment(world, registry, def,
                    out deploymentPoint, out deploymentSurfaceId);
                if (resolved.IsFailure) return resolved;
            }

            var memberIds = new List<EntityId>(def.Members.Count);
            var leaderId = EntityId.None;
            for (var i = 0; i < def.Members.Count; i++)
            {
                var member = def.Members[i];
                if (member == null || string.IsNullOrWhiteSpace(member.CharacterDefinitionId))
                    continue;

                XianXia.Core.Entities.Entity entity;
                if (member.ReuseOpeningSpawn)
                {
                    if (openingLookup == null ||
                        !openingLookup.TryGetEntity(member.CharacterDefinitionId, out var existingId) ||
                        !world.Entities.TryGet(existingId, out entity) || entity == null)
                        return Result.Failure(ErrorCode.NotFound, "FormalArmy reuseOpeningSpawn member missing from opening spawn.", member.CharacterDefinitionId);
                    if (!entity.TryGet<FactionMembershipComponent>(out var existingMembership) ||
                        !string.Equals(existingMembership.FactionId, def.FactionId, System.StringComparison.Ordinal))
                        return Result.Failure(ErrorCode.InvalidOperation, "FormalArmy reused member faction does not match authored army.", member.CharacterDefinitionId);
                }
                else
                {
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
                    entity = spawned.Value;
                    entity.Get<FactionMembershipComponent>().Assign(def.FactionId, FactionRoleKind.Member);
                    if (!hasSurfaceDeployment)
                        world.WorldPresence.SetAtSite(entity.Id, def.AssemblySiteId);
                }
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
                leaderId,
                initializeAtAssemblySite: !hasSurfaceDeployment);
            if (created.IsFailure)
                return Result.Failure(created.Error);

            // AssemblySiteId is organizational origin. A Surface deployment establishes the
            // first physical group anchor once, after membership is registered.
            if (hasSurfaceDeployment)
            {
                var deploy = DeployArmyToInitialSurfacePosition(world, created.Value, def,
                    deploymentPoint, deploymentSurfaceId);
                if (deploy.IsFailure)
                    return deploy;
            }
            else if (def.InitialHex != null)
            {
                var deploy = DeployArmyToInitialHex(world, created.Value, def);
                if (deploy.IsFailure)
                    return deploy;
            }

            ArmyStackAdapter.EnsureLinkedStackView(world, created.Value, def.RuntimeStackId, def.Name);

            return Result.Success();
        }

        static Result ResolveInitialSurfaceDeployment(SimulationWorld world,
            DefinitionRegistry registry, FormalArmyDefinition def,
            out WorldVec2 point, out string surfaceId)
        {
            point = default;
            surfaceId = def.InitialSurfaceDeployment?.SurfaceId ??
                        def.InitialSurfacePosition?.SurfaceId ?? string.Empty;
            if (def.InitialSurfaceDeployment != null && def.InitialSurfacePosition != null)
                return Result.Failure(ErrorCode.InvalidArgument,
                    "FormalArmy has two initial Surface deployment forms.", def.Id.ToString());
            if (string.IsNullOrWhiteSpace(surfaceId) ||
                !registry.TryGetOutdoorSurfaceGeography(surfaceId, out var geography) ||
                geography?.Navigation == null)
                return Result.Failure(ErrorCode.InvalidOperation,
                    "FormalArmy initial Surface navigation missing.", def.Id.ToString());
            if (def.InitialSurfaceDeployment != null)
            {
                var authored = def.InitialSurfaceDeployment;
                if (!TryResolveAuthoredCoreCenter(world, registry,
                        authored.AnchorSiteId, surfaceId, out var coreCenter))
                    return Result.Failure(ErrorCode.InvalidOperation,
                        "FormalArmy deployment anchor Site Core unavailable on Surface.",
                        def.Id.ToString());
                point = new WorldVec2(
                    coreCenter.X + authored.OffsetCellsX * geography.Navigation.CellSize,
                    coreCenter.Y + authored.OffsetCellsY * geography.Navigation.CellSize);
            }
            else
            {
                var authored = def.InitialSurfacePosition;
                point = new WorldVec2(authored.WorldX, authored.WorldY);
            }
            if (!geography.Navigation.Contains(point.X, point.Y) ||
                !geography.Navigation.IsWalkable(point.X, point.Y))
                return Result.Failure(ErrorCode.InvalidOperation,
                    "FormalArmy initial Surface deployment is outside bounds or blocked.",
                    def.Id + " @ " + point.X + "," + point.Y);
            return Result.Success();
        }

        static bool TryResolveAuthoredCoreCenter(SimulationWorld world,
            DefinitionRegistry registry, string siteId, string surfaceId,
            out WorldVec2 center)
        {
            center = default;
            if (string.IsNullOrWhiteSpace(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out var site) || site == null ||
                !site.IsCoreActive || site.CoreIsRemovable)
                return false;
            // New Game registers authored armies before fixed SiteCore runtime binding. Read
            // the exact same checked-in controlCore placement that binding will consume.
            var found = false;
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface == null || surface.AcceptanceOnly ||
                    !string.Equals(surface.SurfaceId, surfaceId, System.StringComparison.Ordinal))
                    continue;
                foreach (var placement in surface.SitePlacements)
                {
                    if (placement == null ||
                        !string.Equals(placement.SiteId, siteId, System.StringComparison.Ordinal) ||
                        !string.Equals(placement.Kind, "controlCore",
                            System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (found) return false;
                    center = new WorldVec2(placement.WorldX + placement.WorldWidth * .5f,
                        placement.WorldY + placement.WorldHeight * .5f);
                    found = true;
                }
            }
            return found;
        }

        static Result DeployArmyToInitialSurfacePosition(
            SimulationWorld world, FormalArmy army, FormalArmyDefinition def,
            WorldVec2 point, string surfaceId)
        {
            FormalArmyContinuousTravelService.InitializeAtWorldPosition(world, army, point, surfaceId);
            var invariant = ValidateInitialDeployment(world, army, def, point, surfaceId);
            if (invariant.IsFailure) return invariant;
            if (world.Strategic.Armies.TryGet(def.RuntimeStackId, out var linked) && linked != null)
                ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, linked);
            return Result.Success();
        }

        static Result ValidateInitialDeployment(SimulationWorld world, FormalArmy army,
            FormalArmyDefinition def, WorldVec2 point, string surfaceId)
        {
            var motion = army.WorldMotion;
            if (!motion.HasPosition || army.UsesHexStrategicPosition ||
                !string.Equals(motion.SurfaceId, surfaceId, System.StringComparison.Ordinal) ||
                motion.LocationKind != FormalArmyLocationKind.AtWorldPosition ||
                System.Math.Abs(motion.WorldPosition.X - point.X) > .001f ||
                System.Math.Abs(motion.WorldPosition.Y - point.Y) > .001f)
                return Result.Failure(ErrorCode.InvalidOperation,
                    "FormalArmy initial deployment motion invariant failed.", def.Id.ToString());
            foreach (var rawId in army.MemberCharacterIds)
            {
                var memberId = new EntityId(rawId);
                if (!FormalArmyMemberPresenceSync.IsArmyControlledMember(world, memberId)) continue;
                if (!world.WorldPresence.TryGet(memberId, out var personal) ||
                    personal.Mode != PartyWorldPresenceMode.AtWorldPosition ||
                    !personal.HasContinuousWorldPosition ||
                    !string.IsNullOrEmpty(personal.SiteId) ||
                    !string.Equals(personal.PersonalSurfaceId, surfaceId,
                        System.StringComparison.Ordinal) ||
                    System.Math.Abs(personal.WorldPosX - point.X) > .001f ||
                    System.Math.Abs(personal.WorldPosY - point.Y) > .001f)
                    return Result.Failure(ErrorCode.InvalidOperation,
                        "FormalArmy member has competing personal deployment anchor.",
                        def.Id + " member=" + memberId);
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
