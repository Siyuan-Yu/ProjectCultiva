using System.Collections.Generic;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Editor 通用 faction read model（未来 WorldGraphEditor / CharacterEditor / ArmyEditor 共用）。
    /// 本工程暂无独立 ContentAuthoring.Shared asmdef；放在 Data.Content 供任何引用 Data 的
    /// Editor 复用 —— 不要在编辑器里各自重复解析 factions.json。
    /// </summary>
    public sealed class StrategicFactionAuthoringDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>#RRGGBB（authoring 原样，不转 float）。</summary>
        public string MapColor { get; set; } = "#B3945C";
        public bool TerritorySelectable { get; set; } = true;
        public int SortOrder { get; set; }
    }

    /// <summary>从已加载 registry 投影 faction read model（编辑器优先复用 Loader 结果，不做二次文件 IO）。</summary>
    public static class StrategicFactionAuthoringQueries
    {
        public static IReadOnlyList<StrategicFactionAuthoringDto> FromRegistry(DefinitionRegistry registry)
        {
            var list = new List<StrategicFactionAuthoringDto>();
            if (registry?.StrategicFactions == null)
                return list;
            foreach (var kv in registry.StrategicFactions)
            {
                var def = kv.Value;
                if (def == null)
                    continue;
                list.Add(new StrategicFactionAuthoringDto
                {
                    Id = def.Id.ToString(),
                    Name = def.Name,
                    MapColor = def.MapColor,
                    TerritorySelectable = def.TerritorySelectable,
                    SortOrder = def.SortOrder
                });
            }

            list.Sort((a, b) =>
            {
                var bySort = a.SortOrder.CompareTo(b.SortOrder);
                if (bySort != 0)
                    return bySort;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }
    }
}
