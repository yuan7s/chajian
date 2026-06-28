using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExternalProgram;

public static class DrawingBatchScanner
{
    private static readonly string[] ModelExtensions = { ".sldprt", ".sldasm" };

    public static List<string> Scan(string folder, bool recursive)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return new List<string>();

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder, "*.*", option);
        }
        catch (Exception)
        {
            return new List<string>();
        }

        return files
            .Where(IsModelFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsModelFile(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("~$", StringComparison.Ordinal))
            return false;

        var ext = Path.GetExtension(path);
        return ModelExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
    }
}
