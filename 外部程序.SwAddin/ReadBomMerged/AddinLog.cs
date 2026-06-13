using System;
using System.IO;
using System.Reflection;

namespace ReadBom.SwAddin;

internal static class AddinLog
{
    public static readonly string DirectoryPath = GetAddinDirectory();
    private static readonly string LogPath = Path.Combine(DirectoryPath, "ReadBom.SwAddin.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static string GetAddinDirectory()
    {
        try
        {
            var location = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrWhiteSpace(directory)) return directory;
        }
        catch
        {
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }
}
