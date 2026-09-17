using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Npc;
using XianXia.Core.Persistence;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class SiteCoreWarfareTests
    {
        const string FixedSite = "base:site_huangcun", OutpostFlag = "base:flag_q1_r10";
        static PlayableDayBootstrapResult Start()
        {
            var path = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ?? Path.GetFullPath("Content/BaseGame");
            var loaded = new ContentPackageLoader().Load(new[] { path });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            var started = new PlayableDayBootstrap().Start(loaded.Value, new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var b = started.Value;
            var party = new PlayerPartyRuntime(); party.BindWorld(b.World);
            Assert.IsTrue(party.TryInitialize(b.CharacterIds[0], out var failure), failure);
            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(b.World);
            var surface = b.Registry.OutdoorSurfaces.Values.First(s => s.SurfaceId == "base:surface_main_wilderness_v1");
            var site = b.World.Strategic.Sites.Sites[FixedSite];
            var population = b.World.WorldPresence.All.Where(p => p.Value.SiteId == FixedSite).Select(p => new EntityId(p.Key)).ToList();
            var plan = new System.Collections.Generic.List<OpeningPlacementPlanRow>();
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryBuildPlan(b.World, b.Registry, surface, site, population, plan, out failure), failure);
            foreach (var row in plan)
            {
                var presence = b.World.WorldPresence.GetOrCreate(row.EntityId);
                presence.PersonalSurfaceId = surface.SurfaceId; presence.HasContinuousWorldPosition = true;
                presence.WorldPosX = row.BakedWorldPosition.X; presence.WorldPosY = row.BakedWorldPosition.Y;
            }
            return b;
        }

        static WorldSiteCoreTarget Target(PlayableDayBootstrapResult b, string siteId = FixedSite)
        {
            var result = WorldSiteCoreWarfareService.Resolve(b.World, siteId, out var target);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            return target;
        }

        static void War(PlayableDayBootstrapResult b, WorldSiteCoreTarget target)
        {
            Assert.IsTrue(StrategicMilitaryAggressionService.TryCommit(b.World, b.World.Strategic.PlayerFactionId, target.OwnerFactionId, out var reason), reason);
        }

        static CharacterEncounterState Prepare(PlayableDayBootstrapResult b, WorldSiteCoreTarget target)
        {
            var w = b.World; War(b, target);
            var actor = w.Strategic.PlayerPartyContext.ActiveCharacterId;
            var p = w.WorldPresence.GetOrCreate(actor);
            p.PersonalSurfaceId = target.SurfaceId; p.HasContinuousWorldPosition = true;
            p.WorldPosX = target.WorldX; p.WorldPosY = target.WorldY; p.Mode = PartyWorldPresenceMode.AtSite;
            var defender = WorldSiteDefenseCharacterQuery.FindNearest(w, target, w.Strategic.PlayerFactionId);
            Assert.IsFalse(defender.IsNone, "Content has no real eligible defender");
            var result = CharacterEncounterService.PrepareForWorldSiteAssault(w, actor, defender, target.SiteId, out var state);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            Assert.IsTrue(CharacterEncounterService.Begin(w, state).IsSuccess);
            return state;
        }

        [Test]
        public void ControlCoreBindSiteUpdatesBothIndexesTransactionally()
        {
            var world = new XianXia.Core.Simulation.SimulationWorld();
            world.RegisterWorkArea(new WorkAreaDefinition
                { Id = "test", Name = "Core", LocationId = "loc", IsControlCore = true });
            Assert.IsTrue(world.ControlCores.BindWorldSite("test", "site:new").IsSuccess);
            Assert.IsTrue(world.ControlCores.BindWorldSite("test", "site:new").IsSuccess);
            Assert.IsTrue(world.ControlCores.TryGetBoundSiteId("test", out var siteId));
            Assert.AreEqual("site:new", siteId);
            Assert.IsTrue(world.ControlCores.TryGetByWorldSite("site:new", out var core));
            Assert.AreEqual("test", core.WorkAreaId);
        }

        [Test]
        public void HuangcunControlCoreUsesCanonicalOutdoorBindingWithoutWorldRegion()
        {
            var b = Start();
            var world = b.World;
            var surface = b.Registry.OutdoorSurfaces.Values.First(s =>
                s.SurfaceId == "base:surface_main_wilderness_v1");
            var placement = surface.SitePlacements.Single(p =>
                p.StableId == "base:site_huangcun:block_supervisor_mansion");
            Assert.AreEqual(FixedSite, placement.SiteId);
            Assert.AreEqual("base:loc_ref_road_hub", placement.BoundLocationId);
            Assert.IsTrue(world.ControlCores.TryGetByLocation(placement.BoundLocationId, out var core));
            Assert.IsTrue(WorldSiteCoreWarfareService.TryGetBoundSiteForFixedCore(world, core.WorkAreaId, out var site));
            Assert.AreEqual(FixedSite, site.SiteId);
            Assert.AreEqual(placement.StableId, site.CoreAssetId);
            Assert.IsTrue(WorldSiteCoreWarfareService.TryGetFixedCore(world, FixedSite, out var reverse));
            Assert.AreSame(core, reverse);

            world.LocalPlaces.ClearLocations();
            var target = Target(b);
            var attacker = world.Strategic.PlayerPartyContext.ActiveCharacterId;
            Assert.IsTrue(WorldSiteCoreWarfareService.Validate(world, attacker, target, requireWar: false).IsSuccess);
            War(b, target);
            Assert.IsTrue(WorldSiteCoreWarfareService.Validate(world, attacker, target).IsSuccess);
            var hp = core.CurrentDurability;
            var strike = ControlCoreService.ApplyStrikeFromAttacker(world, core.WorkAreaId, attacker, out var damage);
            Assert.IsTrue(strike.IsSuccess, strike.IsFailure ? strike.Error.ToString() : "");
            Assert.Greater(damage, 0);
            Assert.AreEqual(hp - damage, core.CurrentDurability);

            var restored = RoundTrip(b);
            restored.LocalPlaces.ClearLocations();
            Assert.IsTrue(WorldSiteCoreWarfareService.TryGetFixedCore(restored, FixedSite, out var restoredCore));
            Assert.AreEqual(core.WorkAreaId, restoredCore.WorkAreaId);
            Assert.IsTrue(WorldSiteCoreWarfareService.Resolve(restored, FixedSite, out _).IsSuccess);
        }

        [Test]
        public void FixedCaptureDoesNotRequireTerritoryRegionProjection()
        {
            var world = new XianXia.Core.Simulation.SimulationWorld();
            world.Strategic.PlayerFactionId = "test:player";
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = "test:fixed", OwnerFactionId = "test:enemy", CoreIsRemovable = false,
                TerritoryRegionId = "test:missing_projection"
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "test:core", Name = "Core", LocationId = "test:loc", IsControlCore = true,
                MaxDurability = 10, OccupyHoldSeconds = 1f
            });
            Assert.IsTrue(WorldSiteCoreWarfareService.BindFixedCore(world, "test:core", "test:fixed").IsSuccess);
            WarGateService.DeclareWar(world, "test:player", "test:enemy");
            world.ControlCores.ApplyDamage("test:core", 20, out _, false);
            world.ControlCores.AddOccupyProgress("test:core", 1f, out _);
            var captured = ControlCoreService.TryCapture(world, "test:core", "test:player");
            Assert.IsTrue(captured.IsSuccess, captured.IsFailure ? captured.Error.ToString() : "");
            Assert.AreEqual("test:player", world.Strategic.Sites.Sites["test:fixed"].OwnerFactionId);
        }

        [Test]
        public void LegacyCaptureObjectiveSnapshotMigratesOnlyPhysicalCoreState()
        {
            var world = new XianXia.Core.Simulation.SimulationWorld();
            var dto = new StrategicSnapshotDto();
            dto.LegacyCaptureObjectives.Add(new LegacyCaptureObjectiveSnapshotDto
            {
                ObjectiveId = "capture:test:core", SiteId = "ignored:site", WorkAreaId = "test:core",
                CurrentHp = 0, MaxHp = 999, OccupyProgressSeconds = 4f, OccupyHoldSeconds = 99f,
                Completed = false
            });
            Assert.IsTrue(StrategicSnapshotHelper.Restore(world, dto).IsSuccess);
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "test:core", Name = "Core", LocationId = "test:loc", IsControlCore = true,
                MaxDurability = 10, OccupyHoldSeconds = 5f
            });
            Assert.IsTrue(world.ControlCores.TryGet("test:core", out var core));
            Assert.AreEqual(0, core.CurrentDurability);
            Assert.AreEqual(4f, core.OccupyProgressSeconds);
            Assert.IsTrue(core.CaptureAvailable);
            Assert.AreEqual(10, core.MaxDurability, "Content shell remains physical configuration authority.");
            Assert.IsFalse(world.ControlCores.TryGetBoundSiteId("test:core", out _),
                "Legacy SiteId must not become canonical binding authority.");

            var completedWorld = new XianXia.Core.Simulation.SimulationWorld();
            var completed = new StrategicSnapshotDto();
            completed.LegacyCaptureObjectives.Add(new LegacyCaptureObjectiveSnapshotDto
            {
                ObjectiveId = "capture:test:completed", SiteId = "ignored:site",
                WorkAreaId = "test:completed", CurrentHp = 0, OccupyProgressSeconds = 5f, Completed = true
            });
            Assert.IsTrue(StrategicSnapshotHelper.Restore(completedWorld, completed).IsSuccess);
            completedWorld.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "test:completed", Name = "Core", LocationId = "test:loc2",
                IsControlCore = true, MaxDurability = 12, OccupyHoldSeconds = 5f
            });
            Assert.IsTrue(completedWorld.ControlCores.TryGet("test:completed", out var completedCore));
            Assert.AreEqual(12, completedCore.CurrentDurability);
            Assert.AreEqual(0f, completedCore.OccupyProgressSeconds);
            Assert.IsFalse(completedCore.CaptureAvailable);

            var corruptNew = new StrategicSnapshotDto { HasControlCoreSnapshotAuthority = true };
            corruptNew.ControlCores.Add(new ControlCoreRuntimeSnapshotDto
            {
                WorkAreaId = string.Empty,
                CurrentDurability = 1,
                OccupyProgressSeconds = 0f
            });
            corruptNew.LegacyCaptureObjectives.Add(new LegacyCaptureObjectiveSnapshotDto
            {
                WorkAreaId = "test:legacy_fallback",
                CurrentHp = 1
            });
            Assert.IsTrue(StrategicSnapshotHelper.Restore(
                    new XianXia.Core.Simulation.SimulationWorld(), corruptNew).IsFailure,
                "Corrupt new controlCores authority must fail instead of falling back to legacy data.");
        }

        [Test]
        public void StrictReferenceContentHasFixedDefendersFarmAndRemovableOutpost()
        {
            var b = Start(); var target = Target(b); War(b, target);
            var w = b.World;
            var defender = WorldSiteDefenseCharacterQuery.FindNearest(w, target, w.Strategic.PlayerFactionId);
            if (defender.IsNone)
            {
                Console.WriteLine("TARGET " + target.SiteId + " " + target.OwnerFactionId + " " + target.WorldX + "," + target.WorldY);
                foreach (var e in w.Entities.All)
                    if (e.TryGet<FactionMembershipComponent>(out var f) && f.FactionId == target.OwnerFactionId)
                    {
                        w.WorldPresence.TryGet(e.Id, out var p);
                        Console.WriteLine(e.DisplayName + " " + e.Id.Value + " role=" + f.Role + " pos=" + p?.WorldPosX + "," + p?.WorldPosY + " surface=" + p?.PersonalSurfaceId + " mode=" + p?.Mode + " exact=" + p?.HasContinuousWorldPosition + " squad=" + w.Strategic.Squads.TryGetForCharacter(e.Id, out _));
                    }
            }
            Assert.IsFalse(defender.IsNone);
            Assert.IsTrue(w.Strategic.Squads.TryGetForCharacter(defender, out var squad));
            var farms = w.OutdoorAdministrativeAssetAnchors.Anchors.Values.Where(a =>
                WorldSiteAdministrativeControlResolver.TryResolve(w, a.SurfaceId, a.WorldX, a.WorldY, out var manager, out _) && manager.SiteId == target.SiteId).ToArray();
            Assert.IsNotEmpty(farms);
            var flag = w.Strategic.FactionFlags.Flags[OutpostFlag];
            var outpost = Target(b, flag.SiteId);
            Assert.IsTrue(outpost.CoreIsRemovable); Assert.IsFalse(target.CoreIsRemovable);
            Console.WriteLine("ACCEPTANCE fixed=" + target.SiteId + " defender=" + defender.Value + " squad=" + squad.SquadId + " farmCells=" + farms.Length +
                " outpost=" + outpost.SiteId + " outpostDefender=" + WorldSiteDefenseCharacterQuery.FindNearest(w, outpost, w.Strategic.PlayerFactionId).Value);
        }

        [Test]
        public void FixedBreachContestedOccupationPreservesClaimsFarmAndNpcIdentityAndPersists()
        {
            var b = Start(); var w = b.World; var target = Target(b); var state = Prepare(b, target);
            var claims = w.Strategic.TerritoryClaims.Claims.Select(c => c.ClaimId + ":" + c.AcquiredOrder).ToArray();
            var identities = w.Entities.All.Where(e => e.TryGet<FactionMembershipComponent>(out _)).ToDictionary(e => e.Id, e => e.Get<FactionMembershipComponent>().FactionId);
            var farm = w.OutdoorAdministrativeAssetAnchors.Anchors.Values.First(a =>
                WorldSiteAdministrativeControlResolver.TryResolve(w, a.SurfaceId, a.WorldX, a.WorldY, out var manager, out _) && manager.SiteId == target.SiteId);
            WorldSiteCoreWarfareService.TryGetFixedCore(w, target.SiteId, out var core);
            w.OutdoorStatefulObjects.SetFarmPlot(farm.StableAssetId, "crop_grain", OutdoorFarmCropStage.Growing, .37f);
            Assert.IsTrue(ControlCoreService.ApplyStrike(w, core.WorkAreaId, core.MaxDurability + core.Defense).IsSuccess);
            Assert.IsTrue(core.CaptureAvailable); Assert.IsTrue(w.Strategic.Sites.TryGet(target.SiteId, out var site));
            foreach (var p in state.Participants) { p.TacticalX = target.WorldX; p.TacticalY = target.WorldY; }
            WorldSiteCoreWarfareService.TickOccupation(w, core.WorkAreaId, 5, out var contested, (x,y) => Math.Abs(x-target.WorldX)<.1f);
            Assert.IsTrue(contested); Assert.AreEqual(0, core.OccupyProgressSeconds);
            foreach (var p in state.Participants.Where(p => p.Enemy)) p.TacticalX += .5f;
            Assert.IsTrue(WorldSiteCoreWarfareService.TickOccupation(w, core.WorkAreaId, core.OccupyHoldSeconds, out contested, (x,y) => Math.Abs(x-target.WorldX)<.1f));
            Assert.IsFalse(contested); Assert.AreEqual(w.Strategic.PlayerFactionId, site.OwnerFactionId);
            Assert.AreEqual(core.MaxDurability, core.CurrentDurability);
            CollectionAssert.AreEqual(claims, w.Strategic.TerritoryClaims.Claims.Select(c => c.ClaimId + ":" + c.AcquiredOrder));
            foreach (var pair in identities) Assert.AreEqual(pair.Value, w.Entities.All.First(e => e.Id == pair.Key).Get<FactionMembershipComponent>().FactionId);
            Assert.AreSame(farm, w.OutdoorAdministrativeAssetAnchors.Anchors[farm.StableAssetId]);
            Assert.IsTrue(w.OutdoorStatefulObjects.TryGetFarmPlot(farm.StableAssetId, out var crop)); Assert.AreEqual(.37f, crop.Growth);
            Assert.IsTrue(WorldSiteAdministrativeControlResolver.TryResolve(w, farm.SurfaceId, farm.WorldX, farm.WorldY, out var managing, out _));
            Assert.AreSame(site, managing); Assert.IsTrue(state.Objective.Resolved); Assert.AreEqual(CharacterEncounterPhase.ReadyToEnd, state.Phase);
            var restored = RoundTrip(b);
            Assert.AreEqual(site.OwnerFactionId, restored.Strategic.Sites.Sites[target.SiteId].OwnerFactionId);
            Assert.IsTrue(restored.Strategic.CharacterEncounter.Objective.Resolved);
            WorldSiteCoreWarfareService.TryGetFixedCore(restored, target.SiteId, out var restoredCore);
            Assert.AreEqual(core.MaxDurability, restoredCore.CurrentDurability);
            Assert.IsTrue(restored.OutdoorStatefulObjects.TryGetFarmPlot(farm.StableAssetId, out var restoredCrop)); Assert.AreEqual(.37f, restoredCrop.Growth);
        }

        static XianXia.Core.Simulation.SimulationWorld RoundTrip(PlayableDayBootstrapResult b)
        {
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var saved = service.CaptureJson(b.World, b.Loop, b.World.Strategic.PlayerPartyContext);
            Assert.IsTrue(saved.IsSuccess, saved.IsFailure ? saved.Error.ToString() : "");
            var restored = service.RestoreJson(saved.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var shell = RuntimeContentShellBootstrap.Rehydrate(restored.Value.world, b.Registry);
            Assert.IsTrue(shell.IsSuccess, shell.IsFailure ? shell.Error.ToString() : "");
            b.Registry.TryGetOpeningScenario(DefinitionId.Parse("base:scenario_ch01_reference").Value, out var scenario);
            Assert.IsTrue(LegacyHexStrategicMapContentAdapter.TryApplyToSession(restored.Value.world, b.Registry, scenario).IsSuccess);
            var fixedCores = ContentRuntimeBootstrap.RebindPresetWorldSiteCoreMetadata(restored.Value.world, b.Registry);
            Assert.IsTrue(fixedCores.IsSuccess, fixedCores.IsFailure ? fixedCores.Error.ToString() : "");
            var snapshot = new JsonSnapshotSerializer().Deserialize(saved.Value).Value;
            var politics = StrategicSnapshotHelper.RestoreHexPoliticalState(restored.Value.world, snapshot.Strategic);
            Assert.IsTrue(politics.IsSuccess, politics.IsFailure ? politics.Error.ToString() : "");
            CollectionAssert.AreEqual(b.World.Strategic.TerritoryClaims.Claims.Select(c => c.ClaimId + ":" + c.AcquiredOrder),
                restored.Value.world.Strategic.TerritoryClaims.Claims.Select(c => c.ClaimId + ":" + c.AcquiredOrder));
            XianXia.Core.Settlement.SettlementAuthoritySync.Rebuild(restored.Value.world);
            return restored.Value.world;
        }

        [Test]
        public void FlagDestructionRetainsInactiveEnemySiteClaimsAndSurvivesLoad()
        {
            var b = Start(); var w = b.World; var flag = w.Strategic.FactionFlags.Flags[OutpostFlag];
            var target = Target(b, flag.SiteId); War(b, target);
            var claims = w.Strategic.TerritoryClaims.Claims.Select(c => c.ClaimId + ":" + c.AcquiredOrder).ToArray();
            Assert.IsTrue(FactionFlagService.TryApplyAssault(w, w.Strategic.PlayerPartyContext, w.Strategic.PlayerFactionId, flag.FlagId, flag.MaxHp).IsSuccess);
            Assert.IsFalse(w.Strategic.FactionFlags.Flags.ContainsKey(flag.FlagId));
            var site = w.Strategic.Sites.Sites[flag.SiteId]; Assert.IsFalse(site.IsCoreActive); Assert.AreEqual(target.OwnerFactionId, site.OwnerFactionId);
            CollectionAssert.AreEqual(claims, w.Strategic.TerritoryClaims.Claims.Select(c => c.ClaimId + ":" + c.AcquiredOrder));
            var restored = RoundTrip(b);
            Assert.IsFalse(restored.Strategic.FactionFlags.Flags.ContainsKey(flag.FlagId));
            Assert.IsFalse(restored.Strategic.Sites.Sites[site.SiteId].IsCoreActive);
            var request = new FactionFlagSitePlacementRequest { SurfaceId = target.SurfaceId,
                WorldPosition = new XianXia.Core.World.WorldVec2(target.WorldX, target.WorldY), StrategicAnchor = flag.AnchorHex };
            var build = XianXia.Core.Construction.ConstructionService.TryConstructFactionFlagSite(w,
                "base:building_faction_control_post", w.Strategic.PlayerFactionId, request, out var ownFlag, out var ownSite);
            Assert.IsTrue(build.IsSuccess, build.IsFailure ? build.Error.ToString() : "");
            Assert.AreNotEqual(flag.FlagId, ownFlag); Assert.AreNotEqual(site.SiteId, ownSite);
            Assert.IsFalse(site.IsCoreActive); Assert.AreEqual(target.OwnerFactionId, site.OwnerFactionId);
            Assert.AreEqual(w.Strategic.PlayerFactionId, w.Strategic.Sites.Sites[ownSite].OwnerFactionId);
        }

        [Test]
        public void EnemyDefeatDoesNotCaptureAndUnresolvedObjectivePersists()
        {
            var b = Start(); var target = Target(b); var state = Prepare(b, target);
            foreach (var p in state.Participants.Where(p => p.Enemy)) b.World.Entities.All.First(e => e.Id.Value == p.CharacterId).Get<LifecycleComponent>().State = LifecycleState.Incapacitated;
            CharacterEncounterService.Advance(b.World, .1f);
            Assert.AreEqual(CharacterEncounterPhase.ReadyToEnd, state.Phase);
            Assert.IsFalse(state.Objective.Resolved); Assert.AreEqual(target.OwnerFactionId, b.World.Strategic.Sites.Sites[target.SiteId].OwnerFactionId);
            var restored = RoundTrip(b);
            Assert.AreEqual(target.SiteId, restored.Strategic.CharacterEncounter.Objective.SiteId);
            Assert.IsFalse(restored.Strategic.CharacterEncounter.Objective.Resolved);
            Assert.IsTrue(CharacterEncounterService.CommitAndReturn(b.World).IsSuccess);
            Assert.AreEqual(target.OwnerFactionId, b.World.Strategic.Sites.Sites[target.SiteId].OwnerFactionId);
        }

        [Test]
        public void ExistingEncounterObjectiveJoinPreservesCombatAndCandidatesAndPersists()
        {
            var b = Start(); var w = b.World; var target = Target(b); var state = Prepare(b, target);
            state.Objective = null; // Ordinary CharacterEncounter may escalate into Site warfare.
            var actor = w.Strategic.PlayerPartyContext.ActiveCharacterId;
            Assert.IsTrue(WorldSiteCoreWarfareService.BindObjective(w, actor, target).IsSuccess);
            var member = state.Find(actor.Value); member.Cooldown = .73f; member.ArtCooldowns[0] = 2.4f;
            w.Entities.TryGet(actor, out var entity); entity.Get<CombatVitalsComponent>().CurrentHp -= 3;
            var hp = entity.Get<CombatVitalsComponent>().CurrentHp;
            state.ElapsedSeconds = 1.25f;
            var candidates = state.Candidates.ToArray();
            var defender = state.Candidates.SelectMany(c => c.Members).First(p =>
                w.Entities.All.First(e => e.Id.Value == p.CharacterId).TryGet<FactionMembershipComponent>(out var f) && f.FactionId == target.OwnerFactionId);
            var before = state.Participants.Count;
            var frozen = state.Candidates.First(c => c.SquadId == defender.SquadId);
            frozen.Phase = EncounterCandidatePhase.Announced; frozen.Roll = 0; frozen.ArriveAt = 99;
            var result = CharacterEncounterService.TryJoinObjectiveDefenderSquad(w, new EntityId(defender.CharacterId), c => false);
            Assert.IsTrue(result.IsFailure); Assert.AreEqual(before, state.Participants.Count);
            result = CharacterEncounterService.TryJoinObjectiveDefenderSquad(w, new EntityId(defender.CharacterId), c => true);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            Assert.AreEqual(hp, entity.Get<CombatVitalsComponent>().CurrentHp); Assert.AreEqual(.73f, member.Cooldown);
            Assert.AreEqual(2.4f, member.ArtCooldowns[0]); Assert.AreEqual(1.25f, state.ElapsedSeconds);
            CollectionAssert.AreEqual(candidates, state.Candidates); Assert.AreEqual(2, state.RosterVersion);
            Assert.AreEqual(PartyWorldPresenceMode.InEncounter, w.WorldPresence.All[defender.CharacterId].Mode);
            Assert.IsTrue(CharacterEncounterService.TryJoinObjectiveDefenderSquad(w, new EntityId(defender.CharacterId)).IsSuccess);
            Assert.AreEqual(2, state.RosterVersion);
            Assert.IsTrue(CharacterEncounterService.ValidateRestored(w, state).IsSuccess);
            var restored = RoundTrip(b);
            Assert.AreEqual(2, restored.Strategic.CharacterEncounter.RosterVersion);
            Assert.AreEqual(.73f, restored.Strategic.CharacterEncounter.Find(actor.Value).Cooldown);
            foreach (var p in state.Participants.Where(p => p.Enemy)) w.Entities.All.First(e => e.Id.Value == p.CharacterId).Get<LifecycleComponent>().State = LifecycleState.Incapacitated;
            CharacterEncounterService.Advance(w, .1f);
            Assert.AreEqual(CharacterEncounterPhase.ReadyToEnd, state.Phase, "Objective-joined candidate must not remain a pending arrival");
            var other = Target(b, w.Strategic.FactionFlags.Flags[OutpostFlag].SiteId);
            var rejected = WorldSiteCoreWarfareService.Validate(w, actor, other, false);
            Assert.IsTrue(rejected.IsFailure); StringAssert.Contains("不在当前战场", rejected.Error.Message);
            state.Width = state.Height = 50; // Separate fixture for the one-unresolved-objective guard.
            rejected = WorldSiteCoreWarfareService.Validate(w, actor, other, false);
            Assert.IsTrue(rejected.IsFailure); StringAssert.Contains("已有攻城目标", rejected.Error.Message);
        }

        [Test]
        public void DefenderUsesExactWarSideAndDistanceThenId()
        {
            var b = Start(); var w = b.World; var target = Target(b); War(b, target);
            var eligible = w.Entities.All.Where(e => e.TryGet<FactionMembershipComponent>(out var f) && f.FactionId == target.OwnerFactionId).OrderBy(e => e.Id.Value).Take(2).ToArray();
            foreach (var e in eligible)
            {
                var p = w.WorldPresence.GetOrCreate(e.Id); p.PersonalSurfaceId = target.SurfaceId;
                p.HasContinuousWorldPosition = true; p.WorldPosX = target.WorldX; p.WorldPosY = target.WorldY;
            }
            Assert.AreEqual(eligible[0].Id, WorldSiteDefenseCharacterQuery.FindNearest(w, target, w.Strategic.PlayerFactionId));
            eligible[0].Get<LifecycleComponent>().State = LifecycleState.Incapacitated;
            var war = w.Strategic.Wars.EnumerateActive().First(r => r.IsAttacker(w.Strategic.PlayerFactionId) && r.IsDefender(target.OwnerFactionId));
            war.AddDefender("test:ally"); eligible[1].Get<FactionMembershipComponent>().Assign("test:ally", FactionRoleKind.Member);
            Assert.AreEqual(eligible[1].Id, WorldSiteDefenseCharacterQuery.FindNearest(w, target, w.Strategic.PlayerFactionId));
            w.WorldPresence.GetOrCreate(eligible[1].Id).PersonalSurfaceId = "test:other_surface";
            Assert.AreNotEqual(eligible[1].Id, WorldSiteDefenseCharacterQuery.FindNearest(w, target, w.Strategic.PlayerFactionId));
        }

        [Test]
        public void FlagObjectiveCompletesWithLivingEnemiesAndKeepsEnemyOwner()
        {
            var b = Start(); var w = b.World; var flag = w.Strategic.FactionFlags.Flags[OutpostFlag]; var target = Target(b, flag.SiteId);
            var guard = w.Entities.All.First(e => e.TryGet<FactionMembershipComponent>(out var f) && f.FactionId == target.OwnerFactionId);
            var p = w.WorldPresence.GetOrCreate(guard.Id); p.PersonalSurfaceId = target.SurfaceId;
            p.HasContinuousWorldPosition = true; p.WorldPosX = target.WorldX; p.WorldPosY = target.WorldY;
            var state = Prepare(b, target);
            Assert.IsTrue(FactionFlagService.TryApplyAssault(w, w.Strategic.PlayerPartyContext, w.Strategic.PlayerFactionId, flag.FlagId, flag.MaxHp).IsSuccess);
            Assert.IsTrue(state.Objective.Resolved); Assert.AreEqual(CharacterEncounterPhase.ReadyToEnd, state.Phase);
            Assert.IsTrue(CharacterEncounterService.IsLiving(w, guard.Id.Value));
            Assert.AreEqual(target.OwnerFactionId, w.Strategic.Sites.Sites[target.SiteId].OwnerFactionId);
            Assert.IsTrue(RoundTrip(b).Strategic.CharacterEncounter.Objective.Resolved);
        }

        [Test]
        public void ObjectiveFormatRequiresCompleteNewFieldsAndMigratesOnlyExplicitLegacy()
        {
            var b = Start(); var state = Prepare(b, Target(b));
            var json = CharacterEncounterJson.Write(state); json.Object.Remove("objective");
            Assert.Throws<FormatException>(() => CharacterEncounterJson.Read(json));
            json.Object["version"] = JsonValue.FromNumber(1); json.Object.Remove("objectiveDefenderSquads");
            var old = CharacterEncounterJson.Read(json);
            Assert.IsNull(old.Objective); Assert.AreEqual(CharacterEncounterState.Format, old.Version);
            var round = CharacterEncounterJson.Read(CharacterEncounterJson.Write(old)); Assert.IsNull(round.Objective);
            json = CharacterEncounterJson.Write(state); json.Object["objective"].Object["kind"] = JsonValue.FromNumber(1.5);
            Assert.Throws<FormatException>(() => CharacterEncounterJson.Read(json));
        }
    }
}
