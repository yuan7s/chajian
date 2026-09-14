using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SldWorks;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    /// <summary>
    /// 计算当前活动零件/装配体的包围盒尺寸，写入配置属性「下料尺寸」。
    /// </summary>
    private object WriteBlankSize()
    {
        var model = GetActiveModel();
        var documentType = Safe(model.GetType);
        var blankSize = GetBlankSizeText(model, documentType);
        if (string.IsNullOrWhiteSpace(blankSize)) throw CommandFailure("bounding_box_unavailable");

        var configuration = GetActiveConfigurationName(model);
        var manager = GetCustomPropertyManager(model, configuration);
        if (manager == null) throw CommandFailure("property_manager_unavailable");

        manager.Add3("下料尺寸", 30, blankSize, 2);
        MarkDocDirty(model);

        return new { blankSize, configuration, documentType };
    }

    /// <summary>
    /// 按文档类型取包围盒并格式化为下料尺寸文本（如 120.5x80x3）。
    /// 零件用 GetPartBox（不含隐藏实体），装配体用 GetBox（不含基准面/草图）。
    /// </summary>
    private static string GetBlankSizeText(ModelDoc2 model, int documentType)
    {
        object corners = null;
        if (documentType == (int)swDocumentTypes_e.swDocPART)
        {
            corners = GetPartDoc(model)?.GetPartBox(false);
        }
        else if (documentType == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            corners = GetAssemblyDoc(model)?.GetBox(0);
        }

        return FormatBlankSizeFromBox(ToDoubleList(corners));
    }

    /// <summary>
    /// 将包围盒六值（米）格式化为「长x宽x高」（毫米、1 位小数、降序、无单位）。
    /// </summary>
    private static string FormatBlankSizeFromBox(IReadOnlyList<double> values)
    {
        if (values == null || values.Count < 6) return string.Empty;

        var sizes = new[]
        {
            Math.Round(Math.Abs(values[3] - values[0]) * 1000, 1),
            Math.Round(Math.Abs(values[4] - values[1]) * 1000, 1),
            Math.Round(Math.Abs(values[5] - values[2]) * 1000, 1)
        };
        Array.Sort(sizes);
        Array.Reverse(sizes);
        return string.Join("x", sizes.Select(x => x.ToString("0.#", CultureInfo.InvariantCulture)));
    }

    private static List<double> ToDoubleList(object value)
    {
        var result = new List<double>();
        if (value is Array array)
        {
            foreach (var item in array) result.Add(Convert.ToDouble(item));
        }
        return result;
    }
}
