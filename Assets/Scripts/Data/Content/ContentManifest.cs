using System.Collections.Generic;
using XianXia.Core.Domain;

namespace XianXia.Data.Content
{
    public sealed class ContentManifest
    {
        public string ModId { get; set; }
        public string Namespace { get; set; }
        public DataVersion Version { get; set; }
        public string CompatibleGameVersion { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public List<string> OptionalDependencies { get; set; } = new List<string>();
        public List<string> LoadAfter { get; set; } = new List<string>();
        public List<string> ContentFolders { get; set; } = new List<string>();
        public string PackageDirectory { get; set; }
    }
}
