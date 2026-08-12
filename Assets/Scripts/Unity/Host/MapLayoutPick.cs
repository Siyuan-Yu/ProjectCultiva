using XianXia.Core.Domain.Ids;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 从已加载内容包中选出当前 LocalMap（Level Tester／Host 共用）。
    /// </summary>
    public static class MapLayoutPick
    {
        public static bool TryGet(PlayableHostSession session, out MapLayoutDefinition layout)
        {
            layout = null;
            if (session?.Registry?.MapLayouts == null || session.Registry.MapLayouts.Count == 0)
                return false;

            var preferred = session.PreferredMapLayoutId;
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                var parsed = DefinitionId.Parse(preferred.Trim());
                if (parsed.IsSuccess &&
                    session.Registry.TryGetMapLayout(parsed.Value, out layout) &&
                    layout != null)
                    return true;
            }

            foreach (var kv in session.Registry.MapLayouts)
            {
                layout = kv.Value;
                if (!string.IsNullOrEmpty(kv.Value.WorldRegionId) &&
                    kv.Value.WorldRegionId.IndexOf("ch01", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            foreach (var kv in session.Registry.MapLayouts)
            {
                layout = kv.Value;
                return layout != null;
            }

            return false;
        }
    }
}
