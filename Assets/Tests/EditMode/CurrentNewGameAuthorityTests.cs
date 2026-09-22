using System;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>Current NewGame authority: Surface, authored NPC squads and checked-in exact anchors.</summary>
    public sealed class CurrentNewGameAuthorityTests
    {
        const string ScenarioId = "base:scenario_ch01_reference";
        const string SurfaceId = "base:surface_main_wilderness_v1";

        [Test]
        public void CurrentScenarioDeclaresOnlyItsOutdoorSurfaceForOpeningWorldAuthority()
        {
            var boot = Boot();
            Assert.IsTrue(boot.Registry.TryGetOpeningScenario(
                DefinitionId.Parse(ScenarioId).Value, out var scenario));

            Assert.AreEqual(SurfaceId, scenario.OpeningSurfaceId);
            Assert.IsEmpty(scenario.OpeningWorldRegionId);
            Assert.IsEmpty(scenario.OpeningLocalPlaceSetId);
            Assert.IsTrue(boot.Registry.TryGetOutdoorSurface(
                DefinitionId.Parse(scenario.OpeningSurfaceId).Value, out var surface));
            Assert.IsFalse(surface.AcceptanceOnly);
            Assert.Greater(surface.OpeningEntityAnchors.Count, 0);
        }

        [Test]
        public void CurrentScenarioCreatesEveryAuthoredNpcSquadWithSurfaceMotion()
        {
            var boot = Boot();
            Assert.IsTrue(boot.Registry.TryGetOpeningScenario(
                DefinitionId.Parse(ScenarioId).Value, out var scenario));
            Assert.Greater(scenario.InitialNpcSquadIds.Count, 0);

            foreach (var definitionId in scenario.InitialNpcSquadIds)
            {
                Assert.IsTrue(boot.Registry.TryGetNpcSquad(
                    DefinitionId.Parse(definitionId).Value, out var definition), definitionId);
                Assert.IsTrue(boot.World.Strategic.Squads.TryGet(
                    definition.SquadId, out var squad), definition.SquadId);
                Assert.AreEqual(definition.SquadId, squad.SquadId);
                Assert.IsTrue(boot.World.Strategic.SquadWorldMotions.TryGet(
                    definition.SquadId, out var motion), definition.SquadId);
                Assert.IsTrue(motion.HasPosition, definition.SquadId);
                Assert.AreEqual(SurfaceId, motion.SurfaceId, definition.SquadId);
            }
        }

        [Test]
        public void CurrentOpeningSpawnsMatchCheckedInExactAnchors()
        {
            var boot = Boot();
            Assert.IsTrue(boot.Registry.TryGetOpeningScenario(
                DefinitionId.Parse(ScenarioId).Value, out var scenario));
            Assert.IsTrue(boot.Registry.TryGetOutdoorSurface(
                DefinitionId.Parse(SurfaceId).Value, out var surface));

            var validated = ContinuousOpeningSpawnPresenceResolver.ValidateOpening(
                boot.World, surface, scenario.Spawns, out var census);

            Assert.IsTrue(validated.IsSuccess,
                validated.IsFailure ? validated.Error.ToString() : string.Empty);
            Assert.AreEqual(scenario.Spawns.Count, census.AnchorCount);
            Assert.AreEqual(census.AnchorCount, census.SpawnedEntityCount);
            Assert.AreEqual(census.AnchorCount, census.PresenceCount);
            Assert.AreEqual(0, census.MissingPresenceCount);
            Assert.AreEqual(0, census.WrongSiteCount);
            Assert.AreEqual(0, census.MissingExactPositionCount);
        }

        [Test]
        public void CurrentOpeningMovementScaleIsExplicitOneAndIndependentFromCellSize()
        {
            var boot = Boot();
            Assert.IsTrue(boot.Registry.TryGetOutdoorSurface(
                DefinitionId.Parse(SurfaceId).Value, out var surface));

            Assert.AreEqual(1f, surface.MovementScale, 0.000001f);
            Assert.AreEqual(1f, boot.World.ContinuousWorldMovementScale, 0.000001f);
            Assert.AreNotEqual(surface.CellSize, surface.MovementScale);
        }

        static PlayableDayBootstrapResult Boot()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath() });
            Assert.IsTrue(loaded.IsSuccess,
                loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var started = new PlayableDayBootstrap().Start(
                loaded.Value,
                new PlayableDayOptions { OpeningScenarioId = ScenarioId });
            Assert.IsTrue(started.IsSuccess,
                started.IsFailure ? started.Error.ToString() : string.Empty);
            return started.Value;
        }

        static string BaseGamePath()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
#else
            var fromEnvironment = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME");
            return string.IsNullOrEmpty(fromEnvironment)
                ? Path.GetFullPath(Path.Combine("Content", "BaseGame"))
                : fromEnvironment;
#endif
        }
    }
}
