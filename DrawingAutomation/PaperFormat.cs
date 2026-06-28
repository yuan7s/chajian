using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ExternalProgram;

public static class PaperSizeCatalog
{
    public static readonly IReadOnlyList<string> Sizes =
        new[] { "A4", "A3", "A2", "A1", "A0" };

    public static bool IsKnownSize(string size)
    {
        if (string.IsNullOrWhiteSpace(size)) return false;
        foreach (var s in Sizes)
            if (string.Equals(s, size.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}

public sealed class PaperFormatEntry
{
    public string TemplatePath { get; set; } = "";
    public string SheetFormatPath { get; set; } = "";
}

public sealed class PaperFormatMap
{
    // key 用规范化的大写图幅名
    public Dictionary<string, PaperFormatEntry> Entries { get; set; } =
        new Dictionary<string, PaperFormatEntry>(StringComparer.OrdinalIgnoreCase);

    public PaperFormatEntry Get(string size)
    {
        if (!string.IsNullOrWhiteSpace(size) &&
            Entries.TryGetValue(size.Trim(), out var entry) && entry != null)
            return entry;
        return new PaperFormatEntry();
    }

    public void Set(string size, string templatePath, string sheetFormatPath)
    {
        if (string.IsNullOrWhiteSpace(size)) return;
        Entries[size.Trim().ToUpperInvariant()] = new PaperFormatEntry
        {
            TemplatePath = (templatePath ?? "").Trim(),
            SheetFormatPath = (sheetFormatPath ?? "").Trim()
        };
    }

    public string ToJson() => JsonSerializer.Serialize(Entries);

    public static PaperFormatMap FromJson(string json)
    {
        var map = new PaperFormatMap();
        if (string.IsNullOrWhiteSpace(json)) return map;
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, PaperFormatEntry>>(json);
            if (parsed != null)
            {
                foreach (var kvp in parsed)
                    if (kvp.Value != null)
                        map.Entries[kvp.Key] = kvp.Value;
            }
        }
        catch (JsonException)
        {
            // 容错：损坏的 JSON 当作空映射
        }
        return map;
    }
}
