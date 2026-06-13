using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace readbom;

public sealed class AppSettingsConfig
{
    private const string UserConfigDirectoryName = "ReadBom";
    private const string UserSettingsFileName = "settings.json";

    public PropertySourceMode PropertySourceMode { get; init; } = PropertySourceMode.CurrentConfiguration;
    public bool SkipVirtual { get; init; } = true;
    public bool GroupByConfig { get; init; } = true;
    public bool ExcludeAssemblyIncludesMainAssembly { get; init; } = true;
    public string ExcludeAssemblyDocumentType { get; init; } = "装配体";
    public string ExcludeAssemblyPartType { get; init; } = "组件";

    [JsonIgnore]
    public string SourcePath { get; private init; } = string.Empty;

    public static AppSettingsConfig Load()
    {
        var path = GetUserSettingsPath();
        if (!File.Exists(path))
        {
            return Save(CreateDefault());
        }

        var config = JsonSerializer.Deserialize<AppSettingsConfig>(File.ReadAllText(path, Encoding.UTF8))
                     ?? CreateDefault();
        return Normalize(config, path);
    }

    public static AppSettingsConfig CreateDefault()
    {
        return new AppSettingsConfig();
    }

    public static AppSettingsConfig Save(AppSettingsConfig settings)
    {
        var path = GetUserSettingsPath();
        var normalized = Normalize(settings, path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(normalized, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }), Encoding.UTF8);
        return normalized;
    }

    private static string GetUserSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, UserConfigDirectoryName, UserSettingsFileName);
    }

    private static AppSettingsConfig Normalize(AppSettingsConfig settings, string sourcePath)
    {
        return new AppSettingsConfig
        {
            PropertySourceMode = Enum.IsDefined(settings.PropertySourceMode)
                ? settings.PropertySourceMode
                : PropertySourceMode.CurrentConfiguration,
            SkipVirtual = settings.SkipVirtual,
            GroupByConfig = settings.GroupByConfig,
            ExcludeAssemblyIncludesMainAssembly = settings.ExcludeAssemblyIncludesMainAssembly,
            ExcludeAssemblyDocumentType = NormalizeFilterValue(settings.ExcludeAssemblyDocumentType, "装配体"),
            ExcludeAssemblyPartType = NormalizeFilterValue(settings.ExcludeAssemblyPartType, "组件"),
            SourcePath = sourcePath
        };
    }

    private static string NormalizeFilterValue(string? value, string fallback)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }
}
