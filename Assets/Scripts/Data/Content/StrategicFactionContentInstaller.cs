using System.Collections.Generic;
using XianXia.Core.World.Strategic;

namespace XianXia.Data.Content
{
    /// <summary>
    /// DefinitionRegistry.StrategicFactions → Core StrategicFactionPresentation 的单点转换安装。
    /// ContentPackageLoader.Load 成功后自动调用（registry 无 strategicFaction 时 ResetInstall
    /// 回 fallback，保证 Content 状态不残留）。Core 不反向依赖 Data：Data 层做转换。
    /// </summary>
    public static class StrategicFactionContentInstaller
    {
        public static void Install(DefinitionRegistry registry)
        {
            if (registry?.StrategicFactions == null || registry.StrategicFactions.Count == 0)
            {
                StrategicFactionCatalog.ResetInstall();
                return;
            }

            var list = new List<StrategicFactionPresentation>(registry.StrategicFactions.Count);
            foreach (var kv in registry.StrategicFactions)
            {
                var def = kv.Value;
                if (def == null)
                    continue;
                var r = 0.55f;
                var g = 0.55f;
                var b = 0.55f;
                // Loader 已严格校验格式；此处防御性 miss 用中性灰（不中断安装）。
                if (!StrategicFactionCatalog.TryParseMapColor(def.MapColor, out r, out g, out b))
                {
                    r = 0.55f;
                    g = 0.55f;
                    b = 0.55f;
                }

                list.Add(new StrategicFactionPresentation(
                    def.Id.ToString(),
                    def.Name,
                    r,
                    g,
                    b,
                    def.TerritorySelectable,
                    def.SortOrder));
            }

            StrategicFactionCatalog.Install(list);
        }
    }
}
