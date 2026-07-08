using SldWorks;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    private object SetIsoStandard()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw CommandFailure("drawing_required");

        var extension = model.Extension;
        if (extension == null)
            throw CommandFailure("document_extension_unavailable");

        var ok = extension.SetUserPreferenceInteger(
            (int)swUserPreferenceIntegerValue_e.swDetailingDimensionStandard,
            0,
            (int)swDetailingStandard_e.swDetailingStandardISO);

        if (!ok)
            throw CommandFailure("solidworks_operation_failed", "operation", "set_iso_standard");

        MarkDocDirty(model);
        return new { standard = "ISO", success = true };
    }
}
