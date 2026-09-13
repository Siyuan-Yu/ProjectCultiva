using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Playable ???Hex ???????</summary>
    public static class HexStrategicSessionBootstrap
    {
        public const string DefaultStartSiteId = "base:site_huangcun";

        public static Result ApplyOpening(
            SimulationWorld world,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup = null,
            IList<OpeningSpawnEntry> spawnEntries = null,
            DefinitionRegistry registry = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "HexStrategic session bootstrap args null.");

            world.WorldPresence.Clear();
            world.Strategic.ContinuousManualCombat.Clear();
            world.Strategic.ManualBattleSettlement.Clear();
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.LocalMapId = string.Empty;

            var startSiteId = DefaultStartSiteId;
            if (!world.Strategic.Sites.TryGet(startSiteId, out var site) || site == null)
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "Default start WorldSite missing.",
                    startSiteId);
            }

            var openingCharacters = CollectOpeningCharacterEntityIds(scenario, lookup, spawnEntries);

            // 初始 macro presence：统一由 OpeningSpawnWorldPresenceApplier 解析
            // （character／npc；显式 worldSiteId ／ DefaultStartSite ／ 荒村 authored residence）。
            // openingCharacters 只肩负责 PlayerParty travel sync 名单，NPC 不得进入。
            var presenceApplied = OpeningSpawnWorldPresenceApplier.Apply(
                world, registry, scenario, lookup, spawnEntries);
            if (presenceApplied.IsFailure)
                return presenceApplied;

            // StrategicContentBootstrap creates FormalArmy before this opening boundary. The
            // intentional WorldPresence.Clear above therefore removes the members' derived
            // records as well. Restore only missing, still army-owned living members after
            // authored opening spawns have had first claim; WorldMotion remains the authority.
            RestoreMissingFormalArmyMemberPresence(world);

            if (world.Strategic.Sites.TryResolveSitePresenceHex(startSiteId, out var presenceHex))
            {
                var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                    ? world.HexWorld.HexSize
                    : 1f;
                // Phase 2C：开局必须是 AtWorldSite，禁止 SetIdleAt（会写成 AtWorldPosition）。
                world.PlayerPartyTravel.SetAtWorldSite(startSiteId, presenceHex, hexSize);
                // 仅主控跟随 PlayerPartyTravel 同步；Background 同伴保持独立 AtWorldSite。
                world.PlayerPartyTravel.CaptureTravelingMembers(
                    CollectOpeningTravelSyncMemberIds(openingCharacters));
            }

            var enter = WorldTravelService.EnterWorldSiteScene(world, startSiteId, string.Empty);
            if (enter.IsFailure)
                return enter;

            WorldTravelService.SyncPartyFocus(world);
            // 确保 Travel 与 PartyWorld 同为 AtWorldSite（EnterWorldSiteScene 不改 Travel）。
            if (world.Strategic.Sites.TryResolveSitePresenceHex(startSiteId, out var presenceAgain))
            {
                var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                    ? world.HexWorld.HexSize
                    : 1f;
                world.PlayerPartyTravel.SetAtWorldSite(startSiteId, presenceAgain, hexSize);
                world.PlayerPartyTravel.CaptureTravelingMembers(
                    CollectOpeningTravelSyncMemberIds(openingCharacters));
            }

            return Result.Success();
        }

        static void RestoreMissingFormalArmyMemberPresence(SimulationWorld world)
        {
            if (world?.Strategic?.FormalArmies == null || world.WorldPresence == null)
                return;

            foreach (var pair in world.Strategic.FormalArmies.Armies)
            {
                var army = pair.Value;
                if (army == null || !army.WorldMotion.HasPosition ||
                    FormalArmyMemberPresenceSync.IsArmyEngaged(world, army))
                    continue;

                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var memberId = new EntityId(army.MemberCharacterIds[i]);
                    if (memberId.IsNone ||
                        (world.WorldPresence.TryGet(memberId, out var existing) && existing != null) ||
                        !world.Entities.TryGet(memberId, out _) ||
                        !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId) ||
                        !ArmyService.TryGetArmyForCharacter(world, memberId, out var bound) ||
                        bound == null ||
                        !string.Equals(bound.ArmyId, army.ArmyId, StringComparison.Ordinal))
                        continue;

                    FormalArmyMemberPresenceSync.SyncMember(world, army, memberId);
                }
            }
        }

        static List<EntityId> CollectOpeningCharacterEntityIds(
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup,
            IList<OpeningSpawnEntry> spawnEntries)
        {
            var list = new List<EntityId>(8);
            if (lookup == null)
                return list;
            var entries = spawnEntries ?? scenario?.Spawns;
            if (entries == null)
                return list;
            for (var i = 0; i < entries.Count; i++)
            {
                var spawn = entries[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.DefinitionId))
                    continue;
                if (!string.IsNullOrEmpty(spawn.EntityKind) &&
                    !string.Equals(spawn.EntityKind, "character", StringComparison.OrdinalIgnoreCase))
                    continue;
                // Remote authored character（worldSiteId 指向非默认开局 Site）：
                // 不属于「默认开局主角团 macro presence」，不得被塞回 DefaultStartSite，
                // 也不得进入 PlayerPartyTravel 旅行成员。
                if (!string.IsNullOrWhiteSpace(spawn.WorldSiteId) &&
                    !string.Equals(spawn.WorldSiteId.Trim(), DefaultStartSiteId, StringComparison.Ordinal))
                    continue;
                if (!lookup.TryGetEntity(spawn.DefinitionId, out var id) || id.IsNone)
                    continue;
                list.Add(id);
            }

            return list;
        }

        /// <summary>
        /// 开局仅主控（首个 character spawn）跟随 PlayerPartyTravel 同步；同伴保持 Background AtWorldSite。
        /// </summary>
        internal static List<EntityId> CollectOpeningTravelSyncMemberIds(IReadOnlyList<EntityId> openingCharacters)
        {
            var list = new List<EntityId>(1);
            if (openingCharacters == null || openingCharacters.Count == 0)
                return list;
            if (!openingCharacters[0].IsNone)
                list.Add(openingCharacters[0]);
            return list;
        }
    }
}
