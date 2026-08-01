using System.IO;
using System.Text;
using XianXia.Data.Content;

namespace XianXia.Unity.Content
{
    /// <summary>Host／Editor entry for BaseGame reference validation (Chapter Production Toolkit).</summary>
    public static class ContentPackageValidationFacade
    {
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
