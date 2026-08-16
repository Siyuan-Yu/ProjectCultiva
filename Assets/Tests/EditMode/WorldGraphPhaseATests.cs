using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>WorldGraph 阶段 A：SCHEMA／Loader／Ch01 小图可加载；旧 worldRegion 仍在。</summary>
    public sealed class WorldGraphPhaseATests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Ch01_WorldGraph_Loads_Alongside_WorldRegion()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            var registry = loaded.Value.Registry;

            Assert.IsTrue(new ContentReferenceValidator().Validate(registry).IsValid);

            Assert.IsTrue(registry.TryGetWorldGraph(new DefinitionId("base", "graph_ch01"), out var graph));
            Assert.AreEqual("base:node_huangcun", graph.StartNodeId);
            Assert.GreaterOrEqual(graph.Nodes.Count, 30);
            Assert.GreaterOrEqual(graph.Routes.Count, 20);

            WorldNodeEntry huangcun = null;
            foreach (var n in graph.Nodes)
            {
                if (n.Id == "base:node_huangcun")
                {
                    huangcun = n;
                    break;
                }
            }

            Assert.IsNotNull(huangcun);
            Assert.AreEqual("base:map_ch01_reference", huangcun.LocalMapId);
            Assert.IsTrue(registry.TryGetMapLayout(new DefinitionId("base", "map_ch01_reference"), out _));

            // 旧 Local 地点图：Ch01 已迁 localPlaceSet
            Assert.IsTrue(registry.TryGetLocalPlaceSet(new DefinitionId("base", "places_ch01_reference"), out _));
            Assert.IsFalse(registry.TryGetWorldRegion(new DefinitionId("base", "region_ch01_reference"), out _));
        }

        [Test]
        public void WorldGraph_Missing_LocalMap_Fails_Reference_Validation()
        {
            var registry = new DefinitionRegistry();
            Assert.IsTrue(registry.RegisterWorldGraph(new WorldGraphDefinition
            {
                Id = new DefinitionId("base", "graph_bad"),
                Name = "bad",
                StartNodeId = "base:node_a",
                Nodes =
                {
                    new WorldNodeEntry
                    {
                        Id = "base:node_a",
                        Name = "A",
                        Kind = "Village",
                        LocalMapId = "base:map_missing"
                    }
                }
            }).IsSuccess);

            var report = new ContentReferenceValidator().Validate(registry);
            Assert.IsFalse(report.IsValid);
        }
    }
}
