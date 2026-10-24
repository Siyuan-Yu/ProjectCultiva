using System;
using System.IO;
using XianXia.Data.Serialization;

namespace XianXia.Data.Bootstrap
{
    /// <summary>正式构建和 LevelTester 共用同一开局绑定文件，不依赖 Inspector 历史默认值。</summary>
    public sealed class FormalOpeningSelection
    {
        public string openingScenarioId, characterRosterId, preferredMapLayoutId;

        public static FormalOpeningSelection Read(string packageRoot)
        {
            var path = Path.Combine(packageRoot, "formal-opening.json");
            var json = SimpleJson.Parse(File.ReadAllText(path));
            if (json.Kind != JsonValueKind.Object) throw new FormatException("正式开局绑定必须为对象：" + path);
            var selection = new FormalOpeningSelection {
                openingScenarioId = json.GetString("openingScenarioId", string.Empty),
                characterRosterId = json.GetString("characterRosterId", string.Empty),
                preferredMapLayoutId = json.GetString("preferredMapLayoutId", string.Empty)
            };
            if (string.IsNullOrWhiteSpace(selection.openingScenarioId) ||
                string.IsNullOrWhiteSpace(selection.characterRosterId) ||
                string.IsNullOrWhiteSpace(selection.preferredMapLayoutId))
                throw new FormatException("正式开局需指定剧本、名册和地图：" + path);
            return selection;
        }
    }
}
