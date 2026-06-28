using System.Linq;
using ExternalProgram;
using Xunit;

public class PaperFormatTests
{
    [Fact]
    public void Catalog_lists_five_sizes_in_order()
    {
        Assert.Equal(
            new[] { "A4", "A3", "A2", "A1", "A0" },
            PaperSizeCatalog.Sizes.ToArray());
    }

    [Fact]
    public void IsKnownSize_is_case_insensitive()
    {
        Assert.True(PaperSizeCatalog.IsKnownSize("a3"));
        Assert.False(PaperSizeCatalog.IsKnownSize("B5"));
    }

    [Fact]
    public void Map_roundtrips_through_json()
    {
        var map = new PaperFormatMap();
        map.Set("A4", @"C:\t\a4.drwdot", @"C:\t\a4.slddrt");
        map.Set("A3", @"C:\t\a3.drwdot", "");

        var restored = PaperFormatMap.FromJson(map.ToJson());

        var a4 = restored.Get("A4");
        Assert.Equal(@"C:\t\a4.drwdot", a4.TemplatePath);
        Assert.Equal(@"C:\t\a4.slddrt", a4.SheetFormatPath);
        Assert.Equal(@"C:\t\a3.drwdot", restored.Get("A3").TemplatePath);
        Assert.Equal("", restored.Get("A3").SheetFormatPath);
    }

    [Fact]
    public void Get_unconfigured_size_returns_empty_entry_not_null()
    {
        var map = new PaperFormatMap();
        var entry = map.Get("A0");
        Assert.NotNull(entry);
        Assert.Equal("", entry.TemplatePath);
        Assert.Equal("", entry.SheetFormatPath);
    }

    [Fact]
    public void FromJson_tolerates_null_or_garbage()
    {
        Assert.NotNull(PaperFormatMap.FromJson(null));
        Assert.NotNull(PaperFormatMap.FromJson(""));
        Assert.NotNull(PaperFormatMap.FromJson("not json"));
    }
}
