using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>Ch01 WorldSite → LocalMap 内容一致性：localMapId 非空且 Content 可加载。</summary>
    public sealed class Ch01WorldSiteLocalMapMappingTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void CH01_SITE_MAP_01_AllWorldSites_HaveLoadableLocalMapDefinitions()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);

            var registry = loaded.Value.Registry;
            Assert.IsTrue(
                registry.TryGetHexWorldContent(
                    DefinitionId.Parse(HexStrategicMapBootstrap.DefaultHexWorldContentId).Value,
                    out var hexWorld),
                "ch01 hex world content missing");
            Assert.IsNotNull(hexWorld);
            Assert.GreaterOrEqual(hexWorld.Sites.Count, 29, "expected full Ch01 site list");

            var missingLocalMapId = new List<string>();
            var missingMapDef = new List<string>();
            var missingPlaceSet = new List<string>();

            for (var i = 0; i < hexWorld.Sites.Count; i++)
            {
                var site = hexWorld.Sites[i];
                if (string.IsNullOrWhiteSpace(site.SiteId))
                    continue;

                if (string.IsNullOrWhiteSpace(site.LocalMapId))
                {
                    missingLocalMapId.Add(site.SiteId);
                    continue;
                }

                var mapParsed = DefinitionId.Parse(site.LocalMapId.Trim());
                Assert.IsTrue(mapParsed.IsSuccess, site.SiteId + " localMapId invalid: " + site.LocalMapId);
                if (!registry.TryGetMapLayout(mapParsed.Value, out var map) || map == null)
                    missingMapDef.Add(site.SiteId + " -> " + site.LocalMapId);
                else
                    Assert.Greater(map.Width, 0, site.LocalMapId + " width");

                var foundPlaceSet = false;
                foreach (var kv in registry.LocalPlaceSets)
                {
                    var set = kv.Value;
                    if (set == null)
                        continue;
                    if (string.Equals(set.MapLayoutId, site.LocalMapId.Trim(), System.StringComparison.Ordinal))
                    {
                        foundPlaceSet = true;
                        Assert.IsFalse(string.IsNullOrWhiteSpace(set.StartLocationId), site.SiteId);
                        break;
                    }
                }

                if (!foundPlaceSet)
                    missingPlaceSet.Add(site.SiteId + " -> " + site.LocalMapId);
            }

            Assert.AreEqual(0, missingLocalMapId.Count, "missing localMapId: " + string.Join(", ", missingLocalMapId));
            Assert.AreEqual(0, missingMapDef.Count, "missing mapLayout: " + string.Join(", ", missingMapDef));
            Assert.AreEqual(0, missingPlaceSet.Count, "missing localPlaceSet: " + string.Join(", ", missingPlaceSet));
        }

        [Test]
        public void CH01_SITE_MAP_02_PrototypeSiteMaps_UseDistinctMarkerPositions()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);

            var registry = loaded.Value.Registry;
            Assert.IsTrue(
                registry.TryGetHexWorldContent(
                    DefinitionId.Parse(HexStrategicMapBootstrap.DefaultHexWorldContentId).Value,
                    out var hexWorld),
                "ch01 hex world content missing");

            var markerKeys = new HashSet<string>();
            var checkedCount = 0;

            for (var i = 0; i < hexWorld.Sites.Count; i++)
            {
                var site = hexWorld.Sites[i];
                if (string.IsNullOrWhiteSpace(site.LocalMapId) ||
                    string.Equals(site.LocalMapId, "base:map_ch01_reference", System.StringComparison.Ordinal))
                    continue;

                var mapParsed = DefinitionId.Parse(site.LocalMapId.Trim());
                if (mapParsed.IsFailure ||
                    !registry.TryGetMapLayout(mapParsed.Value, out var map) ||
                    map == null ||
                    map.Placements.Count < 1)
                    continue;

                var p = map.Placements[0];
                var key = p.Kind + "@" + p.X + "," + p.Y;
                Assert.IsTrue(markerKeys.Add(key), "duplicate prototype marker for " + site.SiteId + ": " + key);
                checkedCount++;
            }

            Assert.GreaterOrEqual(checkedCount, 20, "expected many distinct prototype site markers");
        }
    }
}
