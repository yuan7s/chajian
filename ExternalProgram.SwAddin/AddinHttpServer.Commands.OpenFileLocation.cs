using SldWorks;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    private object GetActiveOrSelectedModelPath()
    {
        var model = GetActiveModel();
        var selectedComponent = GetSelectedComponent(model);
        if (selectedComponent != null)
        {
            var selectedPath = Safe(selectedComponent.GetPathName) ?? "";
            if (string.IsNullOrWhiteSpace(selectedPath))
                selectedPath = Safe(() => (selectedComponent.GetModelDoc() as ModelDoc2)?.GetPathName()) ?? "";
            if (!string.IsNullOrWhiteSpace(selectedPath)) return new { path = selectedPath, selected = true };
        }

        var path = Safe(model.GetPathName) ?? "";
        if (!string.IsNullOrWhiteSpace(path)) return new { path, selected = false };

        return new { path = Safe(model.GetTitle) ?? "", selected = false };
    }
}
