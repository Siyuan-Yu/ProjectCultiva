using SurfaceAuthoring.Core;

if (args.Length != 2 && !(args.Length == 4 && args[2] == "--owners-only"))
    throw new ArgumentException("Usage: FinalWorldPublish <worldcomposition.json> <Content root> [--owners-only <baseline composition>]");

var compositionPath = Path.GetFullPath(args[0]);
var contentRoot = Path.GetFullPath(args[1]);
var composition = SurfaceAuthoringJson.LoadWorldComposition(compositionPath);
var linked = LinkedSourceLoader.Load(composition, compositionPath);
var errors = SurfaceAuthoringValidation.Validate(composition, linked)
    .Where(x => x.Severity == ValidationSeverity.Error).ToArray();
if (errors.Length > 0)
    throw new InvalidDataException(string.Join(Environment.NewLine, errors.Select(x => x.Message)));

if (args.Length == 4)
{
    OwnershipPublication.Publish(composition, linked, contentRoot, args[3]);
    return;
}
var result = CompatibilityPublisher.Publish(composition, linked, contentRoot);
Console.WriteLine($"Published {result.CompositionId}: {result.WidthCells}x{result.HeightCells}, " +
                  $"{result.RiverCount} rivers, {result.BridgeCount} bridges, {result.RoadCount} roads.");
