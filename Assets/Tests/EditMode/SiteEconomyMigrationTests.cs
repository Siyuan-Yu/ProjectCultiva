using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Persistence;
using XianXia.Core.Events;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class SiteEconomyMigrationTests
    {
        const string Huangcun = "base:site_huangcun";
        const string Wood = "base:resource_rough_wood";
        const string Herb = "base:resource_spirit_herb";
        const string Grain = "base:resource_grain";
        const string Grass = "base:resource_conceal_grass";

        static string ContentPath => Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ??
                                            Path.GetFullPath("Content/BaseGame");

        static PlayableDayBootstrapResult Start(out DefinitionRegistry registry)
        {
            var loaded = new ContentPackageLoader().Load(new[] { ContentPath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            registry = loaded.Value.Registry;
            var started = new PlayableDayBootstrap().Start(
                loaded.Value, new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            return started.Value;
        }

        [Test]
        public void AuthoredDefaultsAreSiteKeyedAndLegacyFallbackIsIdempotent()
        {
            var b = Start(out var registry);
            var w = b.World;
            Assert.AreEqual(10, WorldSitePublicStockService.GetCount(w, Huangcun, Wood));
            Assert.AreEqual(2, WorldSitePublicStockService.GetCount(w, Huangcun, Herb));
            Assert.AreEqual(0, WorldSitePublicStockService.GetCount(w, Huangcun, Grain));
            Assert.AreEqual(3, WorldSitePublicStockService.GetCount(w, Huangcun, Grass));
            w.Events.Drain();
            Assert.IsTrue(WorldSitePublicStockService.TryAdd(w, Huangcun, Grain, 1).IsSuccess);
            Assert.IsTrue(w.Events.Drain().Any(e => e.Type == EventType.WorldSitePublicStockChanged));

            w.Strategic.SitePublicStocks.Clear();
            Assert.IsTrue(WorldSiteEconomyBootstrap.ApplyLegacySaveFallback(w, registry).IsSuccess);
            Assert.IsTrue(WorldSitePublicStockService.TryRemove(w, Huangcun, Wood, 1).IsSuccess);
            Assert.IsTrue(WorldSiteEconomyBootstrap.ApplyLegacySaveFallback(w, registry).IsSuccess);
            Assert.AreEqual(9, WorldSitePublicStockService.GetCount(w, Huangcun, Wood));
        }

        [Test]
        public void CaptureAndInactiveFlagKeepStockWhileNewFlagSiteStartsEmpty()
        {
            var b = Start(out _);
            var w = b.World;
            var huangcunStock = w.Strategic.SitePublicStocks.GetOrCreate(Huangcun);
            var identity = huangcunStock;
            Assert.IsTrue(WorldSitePublicStockService.TryAdd(w, Huangcun, Grain, 7).IsSuccess);
            WorldSiteOwnershipService.SetOwner(w, Huangcun, w.Strategic.PlayerFactionId);
            Assert.AreSame(identity, w.Strategic.SitePublicStocks.GetOrCreate(Huangcun));
            Assert.AreEqual(7, WorldSitePublicStockService.GetCount(w, Huangcun, Grain));

            var enemyFlag = w.Strategic.FactionFlags.Flags.Values.First(f => f.IsSiteCore &&
                !string.Equals(f.FactionId, w.Strategic.PlayerFactionId, StringComparison.Ordinal));
            var oldSiteId = enemyFlag.SiteId;
            Assert.IsTrue(WorldSitePublicStockService.TryAdd(w, oldSiteId, Grain, 4).IsSuccess);
            Assert.IsTrue(FactionFlagService.TryDestroy(w, enemyFlag.FlagId).IsSuccess);
            Assert.IsFalse(w.Strategic.Sites.Sites[oldSiteId].IsCoreActive);
            Assert.AreEqual(4, WorldSitePublicStockService.GetCount(w, oldSiteId, Grain));

            var request = new FactionFlagSitePlacementRequest
            {
                SurfaceId = enemyFlag.SurfaceId,
                WorldPosition = new WorldVec2(enemyFlag.WorldX, enemyFlag.WorldY),
                StrategicAnchor = enemyFlag.AnchorHex
            };
            var created = ConstructionService.TryConstructFactionFlagSite(
                w, "base:building_faction_control_post", w.Strategic.PlayerFactionId,
                request, out _, out var newSiteId);
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : "");
            Assert.IsTrue(w.Strategic.SitePublicStocks.TryGet(newSiteId, out var newStock));
            Assert.AreEqual(0, newStock.Resources.Count);
        }

        [Test]
        public void SnapshotWritesDeterministicAuthorityAndRestoresExactStock()
        {
            var b = Start(out var registry);
            var w = b.World;
            var party = new XianXia.Core.World.PlayerPartyRuntime();
            party.BindWorld(w);
            Assert.IsTrue(party.TryInitialize(b.CharacterIds[0], out var failure), failure);
            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(w);
            Assert.IsTrue(WorldSitePublicStockService.TryAdd(w, Huangcun, Grain, 7).IsSuccess);
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var saved = service.CaptureJson(w, b.Loop, w.Strategic.PlayerPartyContext);
            Assert.IsTrue(saved.IsSuccess, saved.IsFailure ? saved.Error.ToString() : "");
            StringAssert.Contains("\"worldSitePublicStocks\"", saved.Value);

            var restored = service.RestoreJson(saved.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            Assert.IsTrue(RuntimeContentShellBootstrap.Rehydrate(restored.Value.world, registry).IsSuccess);
            registry.TryGetOpeningScenario(DefinitionId.Parse("base:scenario_ch01_reference").Value, out var scenario);
            Assert.IsTrue(HexStrategicMapContentBootstrap.TryApplyToSession(
                restored.Value.world, registry, scenario).IsSuccess);
            Assert.IsTrue(ContentRuntimeBootstrap.RebindPresetWorldSiteCoreMetadata(
                restored.Value.world, registry).IsSuccess);
            var dto = new JsonSnapshotSerializer().Deserialize(saved.Value).Value;
            Assert.IsTrue(dto.Strategic.HasWorldSitePublicStockSnapshotAuthority);
            Assert.AreEqual(w.Strategic.Sites.Sites.Count, dto.Strategic.WorldSitePublicStocks.Count);
            CollectionAssert.AreEqual(
                dto.Strategic.WorldSitePublicStocks.Select(s => s.SiteId).OrderBy(id => id, StringComparer.Ordinal),
                dto.Strategic.WorldSitePublicStocks.Select(s => s.SiteId));
            var politics = StrategicSnapshotHelper.RestoreHexPoliticalState(restored.Value.world, dto.Strategic);
            Assert.IsTrue(politics.IsSuccess, politics.IsFailure ? politics.Error.ToString() : "");
            Assert.AreEqual(7, WorldSitePublicStockService.GetCount(restored.Value.world, Huangcun, Grain));
            Assert.AreEqual(10, WorldSitePublicStockService.GetCount(restored.Value.world, Huangcun, Wood));
        }

        [Test]
        public void CorruptAuthoritativeStockFailsInsteadOfApplyingLevelDefaults()
        {
            var b = Start(out _);
            var dto = new StrategicSnapshotDto { HasWorldSitePublicStockSnapshotAuthority = true };
            dto.WorldSitePublicStocks.Add(new WorldSitePublicStockSnapshotDto { SiteId = Huangcun });
            dto.WorldSitePublicStocks[0].Entries.Add(new WorldSitePublicStockEntrySnapshotDto
                { ResourceId = Grain, Amount = -1 });
            var restored = StrategicSnapshotHelper.RestoreHexPoliticalState(b.World, dto);
            Assert.IsTrue(restored.IsFailure);
            Assert.AreEqual(0, WorldSitePublicStockService.GetCount(b.World, Huangcun, Grain));
        }

        [Test]
        public void StrictContentIncludesEconomyFarmWorkAreaAndRealFarmers()
        {
            var b = Start(out var registry);
            var report = new ContentReferenceValidator().Validate(registry);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.IsTrue(registry.WorldSiteEconomies.Values.Any(e => e.SiteId == Huangcun));
            Assert.IsTrue(b.World.OutdoorAdministrativeAssetAnchors.TryGetByLocation(
                "base:loc_ref_labor_yard", out var cells));
            Assert.IsTrue(cells.Any(c => c.Kind == "grainField"));
            CollectionAssert.IsSubsetOf(new[] { "阿土", "阿禾" }, b.World.Entities.All.Select(e => e.DisplayName).ToArray());
        }
    }
}
