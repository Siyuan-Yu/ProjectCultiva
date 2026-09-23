using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace EventEditor;

sealed class EventEditorUserSettings
{
    sealed class SettingsDocument
    {
        [JsonPropertyName("lastCharacterSourceByPackage")]
        public Dictionary<string, string> LastCharacterSourceByPackage { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly Dictionary<string, string> _lastCharacterSourceByPackage;

    EventEditorUserSettings(Dictionary<string, string>? values = null)
    {
        _lastCharacterSourceByPackage = new Dictionary<string, string>(
            values ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
    }

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XianXia", "EventEditor", "settings.json");

    public static EventEditorUserSettings Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(SettingsPath)) return new EventEditorUserSettings();
            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(SettingsPath), JsonOptions);
            return new EventEditorUserSettings(document?.LastCharacterSourceByPackage);
        }
        catch (Exception ex)
        {
            warning = "人物来源个人设置读取失败，已使用全部来源：" + ex.Message;
            return new EventEditorUserSettings();
        }
    }

    public string GetLastCharacterSource(string packageRoot)
    {
        var key = NormalizePackageRoot(packageRoot);
        return _lastCharacterSourceByPackage.TryGetValue(key, out var source) ? source : "";
    }

    public bool TrySetLastCharacterSource(string packageRoot, string source, out string? warning)
    {
        warning = null;
        try
        {
            var key = NormalizePackageRoot(packageRoot);
            if (string.IsNullOrWhiteSpace(source)) _lastCharacterSourceByPackage.Remove(key);
            else _lastCharacterSourceByPackage[key] = source.Trim();

            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            var tempPath = SettingsPath + ".tmp";
            var document = new SettingsDocument
            {
                LastCharacterSourceByPackage = new Dictionary<string, string>(
                    _lastCharacterSourceByPackage, StringComparer.OrdinalIgnoreCase)
            };
            File.WriteAllText(tempPath, JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, SettingsPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            warning = "人物来源个人设置保存失败，本次筛选仍然有效：" + ex.Message;
            return false;
        }
    }

    static string NormalizePackageRoot(string packageRoot) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageRoot));
}
