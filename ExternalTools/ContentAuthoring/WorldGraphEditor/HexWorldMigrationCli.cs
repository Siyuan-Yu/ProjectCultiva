using System.IO;
using System.Text.Json.Nodes;
using ContentAuthoring.Shared;
using ContentAuthoring.Shared.HexWorld;

namespace WorldGraphEditor;

public static class HexWorldMigrationCli
{
    public static void MigrateCh01()
    {
        var root = PackagePaths.FindDefaultBaseGame();
        if (root == null)
            throw new InvalidOperationException("Content/BaseGame not found.");
        var graphPath = Path.Combine(root, "Data", "WorldGraphs", "ch01_world_graph.json");
        if (!File.Exists(graphPath))
            throw new FileNotFoundException("Missing ch01_world_graph.json", graphPath);

        var graphFile = JsonNode.Parse(File.ReadAllText(graphPath)) as JsonObject;
        if (graphFile == null)
            throw new InvalidDataException("Invalid world graph JSON.");
        JsonObject? graphDef = null;
        if (graphFile["definitions"] is JsonArray defs)
        {
            foreach (var token in defs)
            {
                if (token is JsonObject obj &&
                    string.Equals(obj["id"]?.GetValue<string>(), "base:graph_ch01", StringComparison.Ordinal))
                {
                    graphDef = obj;
                    break;
                }
            }
        }

        if (graphDef == null)
            throw new InvalidDataException("base:graph_ch01 definition not found.");

        var hexWorld = HexWorldContentGenerator.GenerateFromWorldGraph(
            graphDef,
            "base:hex_world_ch01",
            "Ch01 Hex Strategic");

        var outDir = ContentPathRules.TypeDataDir(root, HexWorldContentSchema.DefinitionType);
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "ch01_hex_world.json");
        HexWorldContentJson.SaveFile(outPath, hexWorld);
        Console.WriteLine("Wrote " + outPath);
    }
}
