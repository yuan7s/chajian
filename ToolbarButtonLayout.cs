using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ExternalProgram.Properties;

namespace ExternalProgram;

internal static class ToolbarButtonGroups
{
    public const string Pool = "Pool";
    public const string Part = "Part";
    public const string Drawing = "Drawing";
    public const string Assembly = "Assembly";

    public static readonly string[] DocumentGroups = { Part, Drawing, Assembly };

    public static bool IsDocumentGroup(string group)
    {
        return DocumentGroups.Any(item => string.Equals(item, group, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class ToolbarButtonDefinition
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public string[] DefaultGroups { get; init; } = Array.Empty<string>();
}

internal sealed class ToolbarButtonLayoutItem
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string Group { get; set; } = ToolbarButtonGroups.Pool;
    public int Order { get; set; }

    public ToolbarButtonLayoutItem CloneForGroup(string group, int order)
    {
        return new ToolbarButtonLayoutItem
        {
            Id = Id,
            Text = Text,
            Group = group,
            Order = order
        };
    }
}

internal static class ToolbarButtonLayoutStore
{
    public const string OpenFolder = "OpenFolder";
    public const string PartCoding = "PartCoding";
    public const string SaveDwg = "SaveDwg";
    public const string SavePdf = "SavePdf";
    public const string RotateView = "RotateView";
    public const string IsoView = "IsoView";
    public const string ReplaceDrawingSettings = "ReplaceDrawingSettings";
    public const string ReferencePlaneMate = "ReferencePlaneMate";
    public const string AssemblyCleanup = "AssemblyCleanup";
    public const string AssemblySort = "AssemblySort";
    public const string TreeSettings = "TreeSettings";
    public const string Rename = "Rename";
    public const string DeleteCustomProps = "DeleteCustomProps";
    public const string DeleteConfigProps = "DeleteConfigProps";
    public const string DeleteErrorMates = "DeleteErrorMates";
    public const string RunSwpMacro = "RunSwpMacro";

    public static readonly IReadOnlyList<ToolbarButtonDefinition> Definitions = new[]
    {
        new ToolbarButtonDefinition { Id = OpenFolder, Text = "打开目录", DefaultGroups = ToolbarButtonGroups.DocumentGroups },
        new ToolbarButtonDefinition { Id = PartCoding, Text = "图号编码", DefaultGroups = new[] { ToolbarButtonGroups.Part } },
        new ToolbarButtonDefinition { Id = SaveDwg, Text = "另存 DWG", DefaultGroups = new[] { ToolbarButtonGroups.Drawing } },
        new ToolbarButtonDefinition { Id = SavePdf, Text = "另存 PDF", DefaultGroups = new[] { ToolbarButtonGroups.Drawing } },
        new ToolbarButtonDefinition { Id = RotateView, Text = "旋转视图", DefaultGroups = new[] { ToolbarButtonGroups.Drawing } },
        new ToolbarButtonDefinition { Id = IsoView, Text = "ISO", DefaultGroups = new[] { ToolbarButtonGroups.Drawing } },
        new ToolbarButtonDefinition { Id = ReplaceDrawingSettings, Text = "标准格式" },
        new ToolbarButtonDefinition { Id = ReferencePlaneMate, Text = "基准面配合", DefaultGroups = new[] { ToolbarButtonGroups.Assembly } },
        new ToolbarButtonDefinition { Id = AssemblyCleanup, Text = "编码整理", DefaultGroups = new[] { ToolbarButtonGroups.Assembly } },
        new ToolbarButtonDefinition { Id = AssemblySort, Text = "排序", DefaultGroups = new[] { ToolbarButtonGroups.Assembly } },
        new ToolbarButtonDefinition { Id = TreeSettings, Text = "树设置", DefaultGroups = new[] { ToolbarButtonGroups.Assembly } },
        new ToolbarButtonDefinition { Id = Rename, Text = "重命名", DefaultGroups = new[] { ToolbarButtonGroups.Assembly } },
        new ToolbarButtonDefinition { Id = RunSwpMacro, Text = "运行宏", DefaultGroups = new[] { ToolbarButtonGroups.Assembly } },
        new ToolbarButtonDefinition { Id = DeleteErrorMates, Text = "删错配合", DefaultGroups = new[] { ToolbarButtonGroups.Assembly } },
        new ToolbarButtonDefinition { Id = DeleteCustomProps, Text = "删自定义", DefaultGroups = new[] { ToolbarButtonGroups.Assembly } },
        new ToolbarButtonDefinition { Id = DeleteConfigProps, Text = "删配置", DefaultGroups = new[] { ToolbarButtonGroups.Assembly } }
    };

    public static List<ToolbarButtonLayoutItem> Load(Settings settings)
    {
        var raw = settings.Toolbar_ButtonLayout;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<ToolbarButtonLayoutItem>>(raw);
                var normalized = Normalize(items);
                if (normalized.Count > 0) return normalized;
            }
            catch
            {
            }
        }

        return BuildDefault(settings);
    }

    public static List<ToolbarButtonLayoutItem> GetGroupItems(Settings settings, string group)
    {
        return Load(settings)
            .Where(item => string.Equals(item.Group, group, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Order)
            .ToList();
    }

    public static string Serialize(IEnumerable<ToolbarButtonLayoutItem> items)
    {
        return JsonSerializer.Serialize(Normalize(items));
    }

    public static ToolbarButtonDefinition GetDefinition(string id)
    {
        return Definitions.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static void ApplyLegacyVisibility(Settings settings, IEnumerable<ToolbarButtonLayoutItem> items)
    {
        var ids = new HashSet<string>(
            items.Select(item => item.Id).Where(item => !string.IsNullOrWhiteSpace(item)),
            StringComparer.OrdinalIgnoreCase);

        settings.Toolbar_ShowOpenFolder = ids.Contains(OpenFolder);
        settings.Toolbar_ShowPartCoding = ids.Contains(PartCoding);
        settings.Toolbar_ShowDrawingSaveDwg = ids.Contains(SaveDwg);
        settings.Toolbar_ShowDrawingSavePdf = ids.Contains(SavePdf);
        settings.Toolbar_ShowDrawingRotateView = ids.Contains(RotateView);
        settings.Toolbar_ShowDrawingIso = ids.Contains(IsoView);
        settings.Toolbar_ShowAssemblyCodingCleanup = ids.Contains(AssemblyCleanup);
        settings.Toolbar_ShowAssemblySort = ids.Contains(AssemblySort);
        settings.Toolbar_ShowAssemblyTreeSettings = ids.Contains(TreeSettings);
        settings.Toolbar_ShowAssemblyRename = ids.Contains(Rename);
        settings.Toolbar_ShowAssemblyDeleteCustomProps = ids.Contains(DeleteCustomProps);
        settings.Toolbar_ShowAssemblyDeleteConfigProps = ids.Contains(DeleteConfigProps);
    }

    private static List<ToolbarButtonLayoutItem> BuildDefault(Settings settings)
    {
        var items = new List<ToolbarButtonLayoutItem>();
        AddDefault(items, settings.Toolbar_ShowOpenFolder, OpenFolder, ToolbarButtonGroups.Part, ToolbarButtonGroups.Drawing, ToolbarButtonGroups.Assembly);
        AddDefault(items, settings.Toolbar_ShowPartCoding, PartCoding, ToolbarButtonGroups.Part);
        AddDefault(items, settings.Toolbar_ShowDrawingSaveDwg, SaveDwg, ToolbarButtonGroups.Drawing);
        AddDefault(items, settings.Toolbar_ShowDrawingSavePdf, SavePdf, ToolbarButtonGroups.Drawing);
        AddDefault(items, settings.Toolbar_ShowDrawingRotateView, RotateView, ToolbarButtonGroups.Drawing);
        AddDefault(items, settings.Toolbar_ShowDrawingIso, IsoView, ToolbarButtonGroups.Drawing);
        AddDefault(items, true, ReferencePlaneMate, ToolbarButtonGroups.Assembly);
        AddDefault(items, settings.Toolbar_ShowAssemblyCodingCleanup, AssemblyCleanup, ToolbarButtonGroups.Assembly);
        AddDefault(items, settings.Toolbar_ShowAssemblySort, AssemblySort, ToolbarButtonGroups.Assembly);
        AddDefault(items, settings.Toolbar_ShowAssemblyTreeSettings, TreeSettings, ToolbarButtonGroups.Assembly);
        AddDefault(items, settings.Toolbar_ShowAssemblyRename, Rename, ToolbarButtonGroups.Assembly);
        AddDefault(items, true, RunSwpMacro, ToolbarButtonGroups.Assembly);
        AddDefault(items, true, DeleteErrorMates, ToolbarButtonGroups.Assembly);
        AddDefault(items, settings.Toolbar_ShowAssemblyDeleteCustomProps, DeleteCustomProps, ToolbarButtonGroups.Assembly);
        AddDefault(items, settings.Toolbar_ShowAssemblyDeleteConfigProps, DeleteConfigProps, ToolbarButtonGroups.Assembly);
        return Normalize(items);
    }

    private static void AddDefault(List<ToolbarButtonLayoutItem> items, bool enabled, string id, params string[] groups)
    {
        if (!enabled) return;
        var definition = GetDefinition(id);
        if (definition == null) return;

        foreach (var group in groups)
        {
            items.Add(new ToolbarButtonLayoutItem
            {
                Id = definition.Id,
                Text = definition.Text,
                Group = group
            });
        }
    }

    private static List<ToolbarButtonLayoutItem> Normalize(IEnumerable<ToolbarButtonLayoutItem> items)
    {
        if (items == null) return new List<ToolbarButtonLayoutItem>();

        var normalized = new List<ToolbarButtonLayoutItem>();
        foreach (var group in ToolbarButtonGroups.DocumentGroups)
        {
            var order = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var groupItems = items
                .Select((item, index) => new { Item = item, Index = index })
                .Where(item => item.Item != null && string.Equals(item.Item.Group, group, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Item.Order)
                .ThenBy(item => item.Index);

            foreach (var groupItem in groupItems)
            {
                var definition = GetDefinition(groupItem.Item.Id);
                if (definition == null || !seen.Add(definition.Id)) continue;

                normalized.Add(new ToolbarButtonLayoutItem
                {
                    Id = definition.Id,
                    Text = definition.Text,
                    Group = group,
                    Order = order++
                });
            }
        }

        return normalized;
    }
}
