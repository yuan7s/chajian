using System;
using System.IO;
using System.Reflection;

namespace ExternalProgram.SwAddin;

internal static class AddinLog
{
    private const string AddinDirectoryDataName = "ExternalProgram.SwAddin.AddinDirectory";
    public static readonly string DirectoryPath = GetAddinDirectory();
    private static readonly string LogPath = Path.Combine(DirectoryPath, "SwAddin.log");

    public static void Write(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SwAddin log write failed: " + ex.Message);
        }
    }

    private static string GetAddinDirectory()
    {
        try
        {
            var configuredDirectory = AppDomain.CurrentDomain.GetData(AddinDirectoryDataName) as string;
            if (!string.IsNullOrWhiteSpace(configuredDirectory)) return configuredDirectory;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SwAddin configured directory lookup failed: " + ex.Message);
        }

        try
        {
            var location = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrWhiteSpace(directory)) return directory;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SwAddin directory lookup failed: " + ex.Message);
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }
}
