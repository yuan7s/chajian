using System;
using System.Diagnostics;
using System.IO;
using SldWorks;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    private object OpenFileLocation()
    {
        var model = GetActiveModel();
        var selectedComponent = GetSelectedComponent(model);
        string path = null;
        var selected = false;

        if (selectedComponent != null)
        {
            path = Safe(selectedComponent.GetPathName) ?? "";
            if (string.IsNullOrWhiteSpace(path))
                path = Safe(() => (selectedComponent.GetModelDoc() as ModelDoc2)?.GetPathName()) ?? "";
            if (!string.IsNullOrWhiteSpace(path)) selected = true;
        }

        if (string.IsNullOrWhiteSpace(path))
            path = Safe(model.GetPathName) ?? "";

        var opened = LaunchExplorerForPath(path);

        if (string.IsNullOrWhiteSpace(path))
            path = Safe(model.GetTitle) ?? "";

        return new { path, selected, opened };
    }

    private static bool LaunchExplorerForPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            if (File.Exists(path))
            {
                using var p = Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
                return true;
            }
            if (Directory.Exists(path))
            {
                var dir = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                using var p = Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\"") { UseShellExecute = true });
                return true;
            }
        }
        catch (Exception ex)
        {
            LogIgnoredException("LaunchExplorerForPath", ex);
        }
        return false;
    }
}
