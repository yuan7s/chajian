using System;
using System.Collections.Generic;
using System.IO;
using SldWorks;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    private object RunSwpMacro(Dictionary<string, object> args)
    {
        var path = GetArgString(args, "path");
        if (string.IsNullOrWhiteSpace(path))
            throw CommandFailure("macro_file_required");
        if (!File.Exists(path))
            throw CommandFailure("file_not_found", "path", path);
        if (!string.Equals(Path.GetExtension(path), ".swp", StringComparison.OrdinalIgnoreCase))
            throw CommandFailure("unsupported_macro_file", "path", path);

        var ok = _swApp.RunMacro2(path, "", "main", 0, out var error);
        if (!ok || error != 0)
            throw CommandFailure("macro_run_failed", "error", error);

        CheckAndBroadcastDocChange();
        return new { done = true, path };
    }
}
