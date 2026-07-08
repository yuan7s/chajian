using System;
using System.IO;
using SldWorks;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    private object SyncCodingProps()
    {
        var model = GetActiveModel();
        var configName = GetActiveConfigurationName(model);

        var path = Safe(model.GetPathName) ?? "";
        var rawTitle = Safe(model.GetTitle) ?? "";
        var title = !string.IsNullOrWhiteSpace(path)
            ? Path.GetFileNameWithoutExtension(path)
            : Path.GetFileNameWithoutExtension(rawTitle);
        if (string.IsNullOrWhiteSpace(title)) title = rawTitle;

        var materialCode = Safe(() => model.GetCustomInfoValue(configName, "物料编码"));
        var partNumber = Safe(() => model.GetCustomInfoValue(configName, "零件图号"));

        if (title != materialCode || title != partNumber)
        {
            var config = model.GetActiveConfiguration() as Configuration;
            var cusPropMgr = config?.CustomPropertyManager;
            if (cusPropMgr == null) throw CommandFailure("configuration_property_manager_unavailable");

            cusPropMgr.Add3("物料编码", 30, title, 2);
            cusPropMgr.Add3("零件图号", 30, title, 2);
            cusPropMgr.Add3("文件名称", 30, title, 2);
            MarkDocDirty(model);
            return new { synced = true, title, materialCode, partNumber };
        }

        return new { synced = false, title, materialCode, partNumber, message = "已同步，无需更新" };
    }
}
