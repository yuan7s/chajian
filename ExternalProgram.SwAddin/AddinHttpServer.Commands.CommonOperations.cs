using System;
using System.Collections.Generic;
using System.IO;
using SldWorks;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    private object GetActiveDocumentInfo()
    {
        var model = GetActiveModel();
        return new
        {
            title = Safe(model.GetTitle) ?? "",
            path = Safe(model.GetPathName) ?? "",
            configuration = GetActiveConfigurationName(model),
            type = Safe(model.GetType)
        };
    }

    private object Rebuild()
    {
        var model = GetActiveModel();
        model.EditRebuild3();
        return new { rebuilt = true };
    }

    private object Save()
    {
        var model = GetActiveModel();
        model.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, 0, 0);
        return new { saved = true };
    }

    private object OpenDocument(Dictionary<string, object> args)
    {
        var path = GetArgString(args, "path");
        if (string.IsNullOrWhiteSpace(path)) throw CommandFailure("argument_required", "name", "path");
        if (!File.Exists(path)) throw CommandFailure("file_not_found", "path", path);

        var docType = GetDocumentTypeFromPath(path);
        if (docType == 0) throw CommandFailure("unsupported_file_type", "path", path);

        var existing = TryGetOpenModelByPath(path);
        if (existing != null) return new { path, title = Safe(existing.GetTitle), alreadyOpen = true };

        var errors = 0;
        var warnings = 0;
        var model = OpenDoc6WithDialogHandling(
            path,
            docType,
            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
            "",
            ref errors,
            ref warnings);
        return new { path, title = Safe(() => model?.GetTitle()), errors, warnings };
    }

    private object ListExternalReferences(Dictionary<string, object> args)
    {
        var path = GetArgString(args, "path");
        if (string.IsNullOrWhiteSpace(path)) throw CommandFailure("argument_required", "name", "path");
        if (!File.Exists(path)) throw CommandFailure("file_not_found", "path", path);

        var dependencies = ToStringArray(_swApp.GetDocumentDependencies2(path, false, true, false));
        return new { path, dependencies };
    }
}
