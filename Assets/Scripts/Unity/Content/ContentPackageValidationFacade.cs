using System.IO;
using System.Text;
using XianXia.Data.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Content
{
    /// <summary>Host／Editor entry for BaseGame reference validation (Chapter Production Toolkit).</summary>
    public static class ContentPackageValidationFacade
    {
        public static bool TryReadFormalOpening(string packageRoot, out string scenarioId,
            out string rosterId, out string mapId, out string message)
        {
            scenarioId = rosterId = mapId = string.Empty;
            try
            {
                var selection = XianXia.Data.Bootstrap.FormalOpeningSelection.Read(packageRoot);
                scenarioId = selection.openingScenarioId;
                rosterId = selection.characterRosterId;
                mapId = selection.preferredMapLayoutId;
                message = "OK";
                return true;
            }
            catch (System.Exception error) { message = error.Message; return false; }
        }

        public static bool TryValidateOpeningSelection(string packageDirectory, string scenario,
            string roster, string map, out string message)
        {
            var loaded = new ContentPackageLoader().Load(new[] { packageDirectory });
            if (loaded.IsFailure) { message = loaded.Error.ToString(); return false; }
            var registry = loaded.Value.Registry;
            if (!DefinitionId.TryParse(scenario, out var scenarioId) ||
                !registry.TryGetOpeningScenario(scenarioId, out var opening))
            { message = "Scenario 不存在：" + scenario; return false; }
            System.Collections.Generic.IList<OpeningSpawnEntry> spawns = opening.Spawns;
            if (!string.IsNullOrWhiteSpace(roster))
            {
                if (!DefinitionId.TryParse(roster, out var rosterId) ||
                    !registry.TryGetCharacterRoster(rosterId, out var selectedRoster))
                { message = "Roster 不存在：" + roster; return false; }
                spawns = selectedRoster.Entries;
            }
            if (!string.IsNullOrWhiteSpace(map) &&
                (!DefinitionId.TryParse(map, out var mapId) || !registry.MapLayouts.ContainsKey(mapId)))
            { message = "MapLayout 不存在：" + map; return false; }
            var selected = new ContentReferenceValidator().ValidateSelectedOpening(registry, opening, spawns);
            if (!selected.IsValid)
            {
                message = "正式开局与名册不一致：" + selected.Errors[0];
                return false;
            }
            message = "OK";
            return true;
        }
        public static bool TryValidateDirectory(string packageDirectory, out string message)
        {
            if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
            {
                message = "Package directory missing: " + packageDirectory;
                return false;
            }

            var loaded = new ContentPackageLoader().Load(new[] { packageDirectory });
            if (loaded.IsFailure)
            {
                message = loaded.Error.ToString();
                return false;
            }

            var report = new ContentReferenceValidator().Validate(loaded.Value.Registry);
            if (!report.IsValid)
            {
                var sb = new StringBuilder();
                sb.Append("error_count=").Append(report.Errors.Count);
                for (var i = 0; i < report.Errors.Count && i < 8; i++)
                    sb.Append(" | ").Append(report.Errors[i]);
                message = sb.ToString();
                return false;
            }

            message = "OK chapters=" + loaded.Value.Registry.Chapters.Count +
                      " quests=" + loaded.Value.Registry.Quests.Count +
                      " events=" + loaded.Value.Registry.ContentEvents.Count;
            return true;
        }

        public static bool TryValidateBaseGameFromDataPath(string dataPath, out string message)
        {
            var root = Path.GetFullPath(Path.Combine(dataPath, "..", "Content", "BaseGame"));
            return TryValidateDirectory(root, out message);
        }
    }
}
