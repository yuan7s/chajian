using System;
using System.IO;
using System.Linq;
using ExternalProgram;
using Xunit;

public class DrawingBatchScannerTests : IDisposable
{
    private readonly string _root;

    public DrawingBatchScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dbs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private string Touch(string relative)
    {
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, "x");
        return full;
    }

    [Fact]
    public void Finds_parts_and_assemblies_recursively_when_recursive()
    {
        Touch("a.sldprt");
        Touch(@"sub\b.SLDASM");
        Touch("c.txt");
        Touch("d.slddrw");

        var result = DrawingBatchScanner.Scan(_root, recursive: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.EndsWith("a.sldprt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, p => p.EndsWith("b.SLDASM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Top_level_only_when_not_recursive()
    {
        Touch("a.sldprt");
        Touch(@"sub\b.sldasm");

        var result = DrawingBatchScanner.Scan(_root, recursive: false);

        Assert.Single(result);
        Assert.EndsWith("a.sldprt", result[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Excludes_solidworks_temp_files()
    {
        Touch("good.sldprt");
        Touch("~$temp.sldprt");

        var result = DrawingBatchScanner.Scan(_root, recursive: true);

        Assert.Single(result);
        Assert.EndsWith("good.sldprt", result[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_folder_returns_empty_list_not_throw()
    {
        var result = DrawingBatchScanner.Scan(Path.Combine(_root, "nope"), recursive: true);
        Assert.Empty(result);
    }

    [Fact]
    public void Result_is_sorted()
    {
        Touch("z.sldprt");
        Touch("a.sldprt");
        Touch("m.sldasm");

        var result = DrawingBatchScanner.Scan(_root, recursive: true);

        var sorted = result.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, result);
    }
}
