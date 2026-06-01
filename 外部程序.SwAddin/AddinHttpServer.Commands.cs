using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldWorks;
using SwConst;

namespace 外部程序.SwAddin;

internal sealed partial class AddinHttpServer
{
    private object ExecuteCommand(CommandRequest request)
    {
        var args = request.Args ?? new Dictionary<string, object>();
        switch ((request.Command ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "ping":
                return new { message = "pong", time = DateTime.Now };

            case "active-document":
                return GetActiveDocumentInfo();

            case "save-dwg":
                return SaveActiveDrawingAs(".dwg", "DWG");

            case "save-pdf":
                return SaveActiveDrawingAs(".pdf", "PDF");

            case "open-file-location":
                return GetActiveOrSelectedModelPath();

            case "rotate-drawing-view":
                return RotateSelectedDrawingView();

            case "get-component-tree":
                return GetComponentTree();

            case "sort-components":
                return SortComponents();

            case "hide-config-names":
                return HideConfigNames();

            case "sync-coding-props":
                return SyncCodingProps();

            case "read-properties":
                return ReadProperties(args);

            case "write-properties":
                return WriteProperties(args);

            case "get-bounding-box":
                return GetBoundingBox(args);

            case "rename-component":
                return RenameComponent(args);

            case "coding-cleanup":
                return CodingCleanup(args);

            case "rebuild":
                return Rebuild();

            case "save":
                return Save();

            case "open-document":
                return OpenDocument(args);

            default:
                throw new InvalidOperationException($"未知命令: {request.Command}");
        }
    }

    private ModelDoc2 GetActiveModel()
    {
        var model = _swApp.ActiveDoc as ModelDoc2;
        if (model == null) throw new InvalidOperationException("没有活动文档");
        return model;
    }

    private static T Safe<T>(Func<T> work)
    {
        try { return work(); }
        catch { return default; }
    }

    private string GetArgString(Dictionary<string, object> args, string key, string fallback = null)
    {
        if (args.TryGetValue(key, out var val) && val != null) return val.ToString();
        return fallback;
    }

    private object GetActiveDocumentInfo()
    {
        var model = GetActiveModel();
        return new
        {
            title = Safe(() => model.GetTitle()),
            path = Safe(() => model.GetPathName()),
            configuration = Safe(() => model.ConfigurationManager.ActiveConfiguration.Name),
            type = Safe(() => (int)model.GetType())
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
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("path 不能为空");
        if (!File.Exists(path)) throw new InvalidOperationException("文件不存在");

        var docType = GetDocumentTypeFromPath(path);
        if (docType == 0) throw new InvalidOperationException("不支持的文件类型");

        var existing = TryGetOpenModelByPath(path);
        if (existing != null)
        {
            return new { path, title = Safe(() => existing.GetTitle()), alreadyOpen = true };
        }

        var errors = 0;
        var warnings = 0;
        var model = _swApp.OpenDoc6(path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);
        return new { path, title = Safe(() => model?.GetTitle()), errors, warnings };
    }

    private ModelDoc2 TryGetOpenModelByPath(string path)
    {
        try
        {
            var model = _swApp.GetOpenDocumentByName(path) as ModelDoc2;
            return model;
        }
        catch { return null; }
    }

    private int GetDocumentTypeFromPath(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        switch (ext)
        {
            case ".sldprt": return (int)swDocumentTypes_e.swDocPART;
            case ".sldasm": return (int)swDocumentTypes_e.swDocASSEMBLY;
            case ".slddrw": return (int)swDocumentTypes_e.swDocDRAWING;
            default: return 0;
        }
    }

    // --- Drawing Save ---

    private object SaveActiveDrawingAs(string extension, string formatName)
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw new InvalidOperationException("请在工程图环境下使用");

        var docPath = model.GetPathName();
        if (string.IsNullOrWhiteSpace(docPath))
            throw new InvalidOperationException($"当前工程图还没有保存，无法另存 {formatName}。");

        var outputPath = Path.ChangeExtension(docPath, extension);
        model.SaveAs3(outputPath, 0, 2);
        return new { outputPath, format = formatName };
    }

    // --- Open File Location ---

    private object GetActiveOrSelectedModelPath()
    {
        var model = GetActiveModel();
        var path = model.GetPathName();
        if (!string.IsNullOrWhiteSpace(path)) return new { path };

        if (model.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            try
            {
                var selMgr = model.SelectionManager as SelectionMgr;
                var comp = selMgr?.GetSelectedObjectsComponent(1) as Component2;
                if (comp != null)
                {
                    path = comp.GetPathName();
                    if (!string.IsNullOrWhiteSpace(path)) return new { path };
                }
            }
            catch { }
        }

        return new { path = model.GetTitle() };
    }

    // --- Rotate Drawing View ---

    private object RotateSelectedDrawingView()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw new InvalidOperationException("请在工程图环境下使用");

        var selMgr = model.SelectionManager as SelectionMgr;
        var swView = selMgr?.GetSelectedObject6(1, -1) as View;
        if (swView == null)
            throw new InvalidOperationException("请选择一个视图");

        if (swView.Angle > 4.5)
            swView.Angle = 0;
        else
            swView.Angle += Math.PI / 2;

        model.Extension.SelectByID2(swView.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        MarkDocDirty(model);

        return new { viewName = swView.Name, angle = swView.Angle };
    }

    private void MarkDocDirty(ModelDoc2 model)
    {
        try { model.SetSaveFlag(); } catch { }
    }

    // --- Stubs for Task 6 (Assembly Commands) ---

    private object GetComponentTree()
    {
        throw new InvalidOperationException("get-component-tree 尚未实现 (Task 6)");
    }

    private object SortComponents()
    {
        throw new InvalidOperationException("sort-components 尚未实现 (Task 6)");
    }

    private object HideConfigNames()
    {
        throw new InvalidOperationException("hide-config-names 尚未实现 (Task 6)");
    }

    private object SyncCodingProps()
    {
        throw new InvalidOperationException("sync-coding-props 尚未实现 (Task 6)");
    }

    // --- Stubs for Task 7 (Property/BoundingBox/Cleanup Commands) ---

    private object ReadProperties(Dictionary<string, object> args)
    {
        throw new InvalidOperationException("read-properties 尚未实现 (Task 7)");
    }

    private object WriteProperties(Dictionary<string, object> args)
    {
        throw new InvalidOperationException("write-properties 尚未实现 (Task 7)");
    }

    private object GetBoundingBox(Dictionary<string, object> args)
    {
        throw new InvalidOperationException("get-bounding-box 尚未实现 (Task 7)");
    }

    private object RenameComponent(Dictionary<string, object> args)
    {
        throw new InvalidOperationException("rename-component 尚未实现 (Task 7)");
    }

    private object CodingCleanup(Dictionary<string, object> args)
    {
        throw new InvalidOperationException("coding-cleanup 尚未实现 (Task 7)");
    }
}
