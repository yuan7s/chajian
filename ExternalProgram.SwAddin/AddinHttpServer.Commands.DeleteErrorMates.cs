using System;
using SldWorks;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    /// <summary>
    /// 删除错误配合：遍历当前装配体的所有配合，移除处于错误状态的。
    /// </summary>
    private object DeleteErrorMates()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var mateGroups = 0;
        var selected = 0;
        var feature = Safe(() => model.FirstFeature() as Feature);
        while (feature != null)
        {
            var currentFeature = feature;
            var typeName = Safe(currentFeature.GetTypeName2) ?? "";
            if (string.Equals(typeName, "MateGroup", StringComparison.OrdinalIgnoreCase))
            {
                mateGroups++;
                model.ClearSelection2(true);

                var groupSelected = 0;
                var subFeature = Safe(() => currentFeature.GetFirstSubFeature() as Feature);
                while (subFeature != null)
                {
                    var currentSubFeature = subFeature;
                    var errorCode = GetFeatureErrorCode(currentSubFeature, out var isWarning);
                    if (errorCode != 0 && !isWarning)
                        if (Safe(() => currentSubFeature.Select2(true, 0)))
                        {
                            groupSelected++;
                            selected++;
                        }

                    subFeature = Safe(() => currentSubFeature.GetNextSubFeature() as Feature);
                }

                if (groupSelected > 0)
                    model.EditDelete();
            }

            feature = Safe(() => currentFeature.GetNextFeature() as Feature);
        }

        if (selected > 0)
        {
            model.EditRebuild3();
            MarkDocDirty(model);
        }

        return new { done = true, mateGroups, deleted = selected };
    }

    private static int GetFeatureErrorCode(Feature feature, out bool isWarning)
    {
        isWarning = false;
        try
        {
            return feature?.GetErrorCode2(out isWarning) ?? 0;
        }
        catch (Exception ex)
        {
            LogIgnoredException("GetFeatureErrorCode", ex);
            return 0;
        }
    }
}
