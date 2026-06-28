using System;
using System.IO;
using System.Reflection;

namespace ReadBom.SwAddin;

internal static class AddinLog
{
    private const string AddinDirectoryDataName = "ReadBom.SwAddin.AddinDirectory";
    private const string ExternalAddinDirectoryDataName = "ExternalProgram.SwAddin.AddinDirectory";
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
            var configuredDirectory = AppDomain.CurrentDomain.GetData(AddinDirectoryDataName) as string
                                      ?? AppDomain.CurrentDomain.GetData(ExternalAddinDirectoryDataName) as string;
            if (!string.IsNullOrWhiteSpace(configuredDirectory)) return configuredDirectory;
        }
        catch
        {
        }

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
