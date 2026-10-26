using SurfaceAuthoring.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length != 2)
    throw new ArgumentException("Usage: FinalWorldPublish <worldcomposition.json> <Content root>");

var compositionPath = Path.GetFullPath(args[0]);
var contentRoot = Path.GetFullPath(args[1]);
var composition = SurfaceAuthoringJson.LoadWorldComposition(compositionPath);
var linked = LinkedSourceLoader.Load(composition, compositionPath);
var errors = SurfaceAuthoringValidation.Validate(composition, linked)
    .Where(x => x.Severity == ValidationSeverity.Error).ToArray();
if (errors.Length > 0)
    throw new InvalidDataException(string.Join(Environment.NewLine, errors.Select(x => x.Message)));

var result = CompatibilityPublisher.Publish(composition, linked, contentRoot);
// WorldComposer's compatibility publisher retains imported W2A landmark captions.
// Replace those historical labels with this final map's display-only geography.
var geographyRoot = JsonNode.Parse(File.ReadAllText(result.GeographyRuntimePath))!.AsObject();
var geography = geographyRoot["definitions"]![0]!.AsObject();
var landmarks = new JsonArray();
var world = composition.RuntimeSurface!;
foreach (var (id, label, x, y) in new (string, string, int, int)[]
{
    ("magnetic", "磁乱谷", 1750, 5730),
    ("qingxiao", "青霄盆地", 1030, 3790),
    ("zhenyue", "镇岳高原", 3910, 4780),
    ("beast", "魔兽山脉·妖域", 9200, 4100),
    ("beast_core", "核心高原", 9130, 3510),
    ("fire", "火脉高原", 1450, 640),
    ("snow", "极寒雪原", 13900, 6020),
    ("gale", "飓风区", 14400, 4030),
    ("thunder", "雷暴区", 14300, 2400),
    ("desert", "蛇人沙漠", 13200, 690),
})
    landmarks.Add(new JsonObject
    {
        ["stableId"] = "base:final_region:" + id,
        ["label"] = label,
        ["worldX"] = world.OriginWorldX + x * world.SurfaceCellWorldSize,
        ["worldY"] = world.OriginWorldY + y * world.SurfaceCellWorldSize
    });
geography["landmarks"] = landmarks;
File.WriteAllText(result.GeographyRuntimePath,
    geographyRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Published {result.CompositionId}: {result.WidthCells}x{result.HeightCells}, " +
                  $"{result.RiverCount} rivers, {result.BridgeCount} bridges, {result.RoadCount} roads.");
