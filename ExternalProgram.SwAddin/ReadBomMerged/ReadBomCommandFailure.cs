using System;
using System.Collections.Generic;
using System.IO;

namespace ReadBom.SwAddin;

internal sealed class ReadBomCommandFailure
{
    public ReadBomCommandFailure(string code, Dictionary<string, object> details = null)
    {
        Code = code;
        Details = details ?? new Dictionary<string, object>();
    }

    public string Code { get; }

    public Dictionary<string, object> Details { get; }

    public static ReadBomCommandFailure FromException(Exception exception)
    {
        return exception is InvalidOperationException invalidOperation
            ? FromInvalidOperation(invalidOperation)
            : new ReadBomCommandFailure("internal_error");
    }

    public static ReadBomCommandFailure FromInvalidOperation(InvalidOperationException exception)
    {
        var message = exception.Message ?? string.Empty;

        if (message.StartsWith("未知命令:", StringComparison.Ordinal))
            return Create("unknown_command", "command", message.Substring("未知命令:".Length).Trim());
        if (message.Contains("没有活动文档"))
            return Create("active_document_required");
        if (message.Contains("属性名不能为空"))
            return Create("property_name_required");
        if (message.StartsWith("属性写入失败:", StringComparison.Ordinal))
            return Create("property_write_failed", "name", message.Substring("属性写入失败:".Length).Trim());
        if (message.StartsWith("保存模型失败", StringComparison.Ordinal))
            return Create("save_model_failed", "detail", message);
        if (message.Contains("无法确定当前配置属性名称"))
            return Create("configuration_property_name_unavailable");
        if (message.StartsWith("属性写入后回读不一致:", StringComparison.Ordinal))
            return Create("property_verify_failed", "detail", message.Substring("属性写入后回读不一致:".Length).Trim());
        if (message.Contains("name 不能为空"))
            return Create("argument_required", "name", "name");
        if (message.Contains("path 不能为空"))
            return Create("argument_required", "name", "path");
        if (message.Contains("缺少完整路径"))
            return Create("path_required");
        if (message.Contains("不支持的 SolidWorks 文件类型") || message.Contains("不支持的文件类型"))
            return Create("unsupported_solidworks_file_type", "path", ExtractPath(message));
        if (message.StartsWith("文件不存在:", StringComparison.Ordinal))
            return Create("file_not_found", "path", message.Substring("文件不存在:".Length).Trim());
        if (message.Contains("文件不存在"))
            return Create("file_not_found");
        if (message.StartsWith("无法打开模型", StringComparison.Ordinal))
            return Create("open_model_failed", "detail", message);
        if (message.StartsWith("无法获取配置属性管理器:", StringComparison.Ordinal))
            return Create("configuration_property_manager_unavailable", "configuration", message.Substring("无法获取配置属性管理器:".Length).Trim());
        if (message.Contains("无法获取自定义属性管理器"))
            return Create("custom_property_manager_unavailable");
        if (message.Contains("无法获取包围盒"))
            return Create("bounding_box_unavailable");
        if (message.Contains("InsertBomTable3 返回空"))
            return Create("bom_table_unavailable");
        if (message.Contains("导出 BOM CSV 失败"))
            return Create("bom_csv_export_failed");
        if (message.Contains("创建专用 BOM 模板失败"))
            return Create("bom_template_create_failed", "detail", message);

        return Create("command_failed");
    }

    private static ReadBomCommandFailure Create(string code, params object[] details)
    {
        var result = new Dictionary<string, object>();
        for (var i = 0; details != null && i + 1 < details.Length; i += 2)
        {
            var key = details[i]?.ToString();
            if (string.IsNullOrWhiteSpace(key)) continue;
            result[key] = details[i + 1];
        }

        return new ReadBomCommandFailure(code, result);
    }

    private static string ExtractPath(string message)
    {
        var index = message.LastIndexOf(':');
        if (index < 0 || index + 1 >= message.Length) return string.Empty;

        var value = message.Substring(index + 1).Trim();
        return Path.IsPathRooted(value) ? value : string.Empty;
    }
}
