using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SldWorks;
using SolidWorks.Interop.swdocumentmgr;
using SwConst;

namespace ExternalProgram.SwAddin;

// ReSharper disable CatchAllClause
#pragma warning disable CA1031 // SolidWorks COM APIs throw broad COM/runtime exceptions; command handlers isolate and log recoverable failures.

internal sealed partial class AddinHttpServer
{
    // ──────────── 编码整理 ────────────
    // 用于识别标准件/外购件的属性名
    private static readonly string[] StandardComponentFlagProperties = ["标准件"];
    private static readonly string[] PurchasedComponentFlagProperties = ["外购件"];

    // ──────────── 工程图操作 ────────────

    private string _lastDocPath;

    private void CheckAndBroadcastDocChange()
    {
        try
        {
            var model = _swApp.ActiveDoc as ModelDoc2;
            var currentPath = model != null ? Safe(model.GetPathName) ?? "" : "";
            if (currentPath != _lastDocPath)
            {
                _lastDocPath = currentPath;
                var title = model != null ? Safe(model.GetTitle) ?? "" : "";
                BroadcastEvent("doc-changed", new { title, path = currentPath });
            }
        }
        catch (Exception ex)
        {
            LogIgnoredException("CheckAndBroadcastDocChange", ex);
        }
    }

    private object ExecuteCommand(CommandRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var args = request.Args ?? new Dictionary<string, object>();
        object result;

        // ─── 命令路由表 ───
        // 按功能分组：基础 / 工程图 / 装配体 / 属性 / 编码
        switch ((request.Command ?? string.Empty).Trim().ToUpperInvariant())
        {
            // ─── 基础 ───
            case "PING":
                result = new { message = "pong", time = DateTime.Now };
                break;

            case "ACTIVE-DOCUMENT":
                result = GetActiveDocumentInfo();
                break;

            // ─── 工程图导出 ───
            case "SAVE-DWG":
                result = SaveActiveDrawingAs(".dwg", "DWG");
                break;

            case "SAVE-PDF":
                result = SaveActiveDrawingAs(".pdf", "PDF");
                break;

            case "OPEN-FILE-LOCATION":
                result = OpenFileLocation();
                break;

            // ─── 工程图视图/标注/标准 ───
            case "ROTATE-DRAWING-VIEW":
                result = RotateSelectedDrawingView();
                break;

            case "SET-ISO-STANDARD":
                result = SetIsoStandard();
                break;

            case "REPLACE-DRAWING-SETTINGS":
                result = ReplaceDrawingSettings(args);
                break;

            case "REPLACE-DRAWING-STANDARD":
                result = ReplaceDrawingStandard(args);
                break;

            case "REPLACE-SHEET-FORMAT":
                result = ReplaceSheetFormat(args);
                break;

            // ─── 装配体操作 ───
            case "MATE-REFERENCE-PLANES":
                result = MateReferencePlanes();
                break;

            case "DELETE-ERROR-MATES":
                result = DeleteErrorMates();
                break;

            case "RUN-SWP-MACRO":
                result = RunSwpMacro(args);
                break;

            case "PICK-SWP-MACRO":
                result = PickSwpMacro();
                break;

            case "GET-COMPONENT-TREE":
                result = GetComponentTree();
                break;

            case "SELECT-COMPONENT":
                result = SelectComponent(args);
                break;

            case "SORT-COMPONENTS":
                result = SortComponents(args);
                break;

            case "HIDE-CONFIG-NAMES":
                result = HideConfigNames();
                break;

            // ─── 属性读写 ───
            case "SYNC-CODING-PROPS":
                result = SyncCodingProps();
                break;

            case "DELETE-CUSTOM-PROPS":
                result = DeleteCustomProperties();
                break;

            case "DELETE-CONFIG-PROPS":
                result = DeleteConfigurationProperties();
                break;

            case "READ-PROPERTIES":
                result = ReadProperties(args);
                break;

            case "WRITE-PROPERTIES":
                result = WriteProperties(args);
                break;

            case "GET-BOUNDING-BOX":
                result = GetBoundingBox(args);
                break;

            case "WRITE-BLANK-SIZE":
                result = WriteBlankSize();
                break;

            // ─── 重命名 ───
            case "RENAME-TARGET":
                result = GetRenameTargetInfo();
                break;

            case "CHECK-NAME-CONFLICT":
                result = CheckNameConflict(args);
                break;

            case "SAVE-AS-NEW":
                result = SaveAsNew(args);
                break;

            case "SAVE-AS-REPLACE":
                result = SaveAsReplace(args);
                break;

            case "RENAME-COMPONENT":
                result = RenameComponent(args);
                break;

            case "CODING-CLEANUP":
                result = CodingCleanup(args);
                break;

            // ─── 通用操作 ───
            case "REBUILD":
                result = Rebuild();
                break;

            case "SAVE":
                result = Save();
                break;

            case "OPEN-DOCUMENT":
                result = OpenDocument(args);
                break;

            case "LIST-EXTERNAL-REFERENCES":
                result = ListExternalReferences(args);
                break;

            default:
                throw CommandFailure("unknown_command", "command", request.Command);
        }

        CheckAndBroadcastDocChange();
        return result;
    }

    /// <summary>
    /// 获取 SW 当前活动文档。无文档时抛出 active_document_required。
    /// </summary>
    private ModelDoc2 GetActiveModel()
    {
        var model = _swApp.ActiveDoc as ModelDoc2;
        if (model == null) throw CommandFailure("active_document_required");
        return model;
    }

    /// <summary>
    /// 创建业务异常，客户端根据 errorCode 显示中文提示。
    /// </summary>
    private static CommandFailureException CommandFailure(string code, params object[] details)
    {
        return CommandFailureException.Create(code, details);
    }

    /// <summary>
    /// 安全执行 COM 调用。捕获异常后记录日志并返回 default(T)。
    /// 用于非关键 COM 调用（如读取属性），失败时静默降级。
    /// </summary>
    private static T Safe<T>(Func<T> work)
    {
        try
        {
            return work();
        }
        catch (Exception ex)
        {
            LogIgnoredException("Safe", ex);
            return default;
        }
    }

    private static void LogIgnoredException(string context, Exception ex)
    {
        AddinLog.Write(context + " ignored: " + ex.Message);
    }

    private static object[] GetComponentChildren(Component2 component)
    {
        if (component == null) return Array.Empty<object>();

        try
        {
            return ToObjectArray(component.GetChildren());
        }
        catch (Exception ex)
        {
            LogIgnoredException("GetComponentChildren", ex);
            return Array.Empty<object>();
        }
    }

    private static object[] GetFeatureArray(ModelDoc2 model)
    {
        var featureManager = model?.FeatureManager;
        if (featureManager == null) return Array.Empty<object>();

        try
        {
            return ToObjectArray(featureManager.GetFeatures(true));
        }
        catch (Exception ex)
        {
            LogIgnoredException("GetFeatureArray", ex);
            return Array.Empty<object>();
        }
    }

    private static object[] ToObjectArray(object value)
    {
        if (value == null) return Array.Empty<object>();
        if (value is object[] objectArray) return objectArray;
        if (value is Array array) return array.Cast<object>().ToArray();
        return Array.Empty<object>();
    }

    private static string[] ToStringArray(object value)
    {
        return ToObjectArray(value)
            .Select(item => item?.ToString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
    }

    private static IEnumerable<Component2> EnumerateComponents(object[] items)
    {
        foreach (var item in items)
            if (item is Component2 component)
                yield return component;
    }

    private static CustomPropertyManager GetCustomPropertyManager(ModelDoc2 model, string configurationName)
    {
        return model?.Extension?.CustomPropertyManager[configurationName ?? ""];
    }

    private static string GetActiveConfigurationName(ModelDoc2 model)
    {
        return Safe(() => model?.ConfigurationManager?.ActiveConfiguration?.Name) ?? "";
    }

    private static AssemblyDoc GetAssemblyDoc(ModelDoc2 model)
    {
        if (model == null || Safe(model.GetType) != (int)swDocumentTypes_e.swDocASSEMBLY) return null;

        object swModel = model;
        return (AssemblyDoc)swModel;
    }

    private static PartDoc GetPartDoc(ModelDoc2 model)
    {
        if (model == null || Safe(model.GetType) != (int)swDocumentTypes_e.swDocPART) return null;

        object swModel = model;
        return (PartDoc)swModel;
    }

    private static string GetArgString(Dictionary<string, object> args, string key, string fallback = "")
    {
        if (args != null && args.TryGetValue(key, out var val) && val != null) return val.ToString();
        return fallback;
    }

    private static bool GetArgBool(Dictionary<string, object> args, string key, bool fallback = false)
    {
        if (args == null || !args.TryGetValue(key, out var val) || val == null) return fallback;
        if (val is bool boolValue) return boolValue;
        return bool.TryParse(val.ToString(), out var parsed) ? parsed : fallback;
    }

    private static List<string> GetArgStringList(Dictionary<string, object> args, string key)
    {
        var result = new List<string>();
        if (args == null || !args.TryGetValue(key, out var raw) || raw == null) return result;

        if (raw is IEnumerable<object> items)
        {
            foreach (var item in items)
            {
                var text = item?.ToString();
                if (!string.IsNullOrWhiteSpace(text)) result.Add(text.Trim());
            }
        }
        return result;
    }



    private ModelDoc2 TryGetOpenModelByPath(string path)
    {
        try
        {
            var model = _swApp.GetOpenDocumentByName(path) as ModelDoc2;
            return model;
        }
        catch (Exception ex)
        {
            LogIgnoredException("TryGetOpenModelByPath", ex);
            return null;
        }
    }

    private ModelDoc2 OpenDoc6WithDialogHandling(
        string path,
        int docType,
        int options,
        string configuration,
        ref int errors,
        ref int warnings)
    {
        return OpenDoc6WithDialogHandling(_swApp, path, docType, options, configuration, ref errors, ref warnings);
    }

    private static ModelDoc2 OpenDoc6WithDialogHandling(
        SldWorks.SldWorks swApp,
        string path,
        int docType,
        int options,
        string configuration,
        ref int errors,
        ref int warnings)
    {
        using var fontDialogHandler = StartSolidWorksFontDialogWatcher(TimeSpan.FromSeconds(60));
        var model = swApp.OpenDoc6(path, docType, options, configuration ?? "", ref errors, ref warnings) as ModelDoc2;
        if (fontDialogHandler.HandledCount > 0)
            AddinLog.Write("OpenDoc6 temporary font replacement dialogs handled: " +
                           fontDialogHandler.HandledCount + ", path=" + path);
        return model;
    }

    private static SolidWorksFontDialogWatcher StartSolidWorksFontDialogWatcher(TimeSpan maxDuration)
    {
        return new SolidWorksFontDialogWatcher(maxDuration);
    }

    private sealed class SolidWorksFontDialogWatcher : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _handledCount;

        public SolidWorksFontDialogWatcher(TimeSpan maxDuration)
        {
            Task.Run(() => RunAsync(maxDuration, _cts.Token));
        }

        public int HandledCount => Volatile.Read(ref _handledCount);

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
            }
            catch (Exception ex)
            {
                LogIgnoredException("SolidWorksFontDialogWatcher.Dispose", ex);
            }
        }

        private async Task RunAsync(TimeSpan maxDuration, CancellationToken token)
        {
            var stopAtUtc = DateTime.UtcNow + maxDuration;
            while (!token.IsCancellationRequested && DateTime.UtcNow < stopAtUtc)
            {
                try
                {
                    if (TryDismissSolidWorksFontDialog())
                        Interlocked.Increment(ref _handledCount);
                }
                catch (Exception ex)
                {
                    LogIgnoredException("SolidWorksFontDialogWatcher", ex);
                }

                try
                {
                    await Task.Delay(150, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }
    }

    private static bool TryDismissSolidWorksFontDialog()
    {
        var targetProcessId = Process.GetCurrentProcess().Id;
        var handled = false;
        EnumWindows((window, _) =>
        {
            if (handled) return false;
            if (!IsWindowVisible(window)) return true;

            GetWindowThreadProcessId(window, out var processId);
            if (processId != targetProcessId) return true;

            var windowText = GetWindowTextValue(window);
            var childTexts = GetChildWindowTexts(window);
            var dialogText = windowText + "\n" + string.Join("\n", childTexts);
            if (!LooksLikeMissingFontDialog(dialogText)) return true;

            var button = FindChildWindow(window, IsTemporaryFontReplacementButton);
            if (button == IntPtr.Zero) return true;

            SendMessage(button, BmClick, IntPtr.Zero, IntPtr.Zero);
            AddinLog.Write("SolidWorks missing font dialog handled with temporary font replacement: " + windowText);
            handled = true;
            return false;
        }, IntPtr.Zero);

        return handled;
    }

    private static bool LooksLikeMissingFontDialog(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var hasFont = ContainsIgnoreCase(text, "字体") || ContainsIgnoreCase(text, "font");
        if (!hasFont) return false;

        var hasMissingText =
            ContainsIgnoreCase(text, "未安装") ||
            ContainsIgnoreCase(text, "缺失") ||
            ContainsIgnoreCase(text, "找不到") ||
            ContainsIgnoreCase(text, "not installed") ||
            ContainsIgnoreCase(text, "missing") ||
            ContainsIgnoreCase(text, "not found");
        var hasTemporaryReplacement =
            ContainsIgnoreCase(text, "临时") && ContainsIgnoreCase(text, "替换") ||
            ContainsIgnoreCase(text, "temporary") && (ContainsIgnoreCase(text, "replace") ||
                                                      ContainsIgnoreCase(text, "substitute"));

        return hasMissingText || hasTemporaryReplacement;
    }

    private static bool IsTemporaryFontReplacementButton(IntPtr window)
    {
        if (!string.Equals(GetWindowClassName(window), "Button", StringComparison.OrdinalIgnoreCase))
            return false;

        var text = GetWindowTextValue(window);
        if (string.IsNullOrWhiteSpace(text)) return false;

        return ContainsIgnoreCase(text, "临时") && ContainsIgnoreCase(text, "替换") ||
               ContainsIgnoreCase(text, "temporary") && (ContainsIgnoreCase(text, "replace") ||
                                                         ContainsIgnoreCase(text, "substitute")) ||
               ContainsIgnoreCase(text, "temporarily") && ContainsIgnoreCase(text, "font");
    }

    private static IntPtr FindChildWindow(IntPtr parent, Func<IntPtr, bool> predicate)
    {
        var result = IntPtr.Zero;
        EnumChildWindows(parent, (child, _) =>
        {
            if (predicate(child))
            {
                result = child;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static string[] GetChildWindowTexts(IntPtr parent)
    {
        var texts = new List<string>();
        EnumChildWindows(parent, (child, _) =>
        {
            var text = GetWindowTextValue(child);
            if (!string.IsNullOrWhiteSpace(text))
                texts.Add(text);
            return true;
        }, IntPtr.Zero);
        return texts.ToArray();
    }

    private static bool ContainsIgnoreCase(string value, string token)
    {
        return value?.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetWindowTextValue(IntPtr window)
    {
        var builder = new StringBuilder(512);
        GetWindowText(window, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetWindowClassName(IntPtr window)
    {
        var builder = new StringBuilder(128);
        GetClassName(window, builder, builder.Capacity);
        return builder.ToString();
    }

    private const int BmClick = 0x00F5;

    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private static int GetDocumentTypeFromPath(string path)
    {
        var ext = Path.GetExtension(path)?.ToUpperInvariant();
        switch (ext)
        {
            case ".SLDPRT": return (int)swDocumentTypes_e.swDocPART;
            case ".SLDASM": return (int)swDocumentTypes_e.swDocASSEMBLY;
            case ".SLDDRW": return (int)swDocumentTypes_e.swDocDRAWING;
            default: return 0;
        }
    }

    // --- Drawing Save ---

    private object SaveActiveDrawingAs(string extension, string formatName)
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw CommandFailure("drawing_required");

        var docPath = model.GetPathName();
        if (string.IsNullOrWhiteSpace(docPath))
            throw CommandFailure("drawing_must_be_saved", "format", formatName);

        var outputPath = Path.ChangeExtension(docPath, extension);
        model.SaveAs3(outputPath, 0, 2);
        return new { outputPath, format = formatName };
    }

    // --- Rotate Drawing View ---

    // ──────────── 共享辅助方法 ────────────

    private static string GetComponentSelectionName(Component2 component)
    {
        var selectionName = Safe(component.GetSelectByIDString);
        if (!string.IsNullOrWhiteSpace(selectionName)) return selectionName;

        return Safe(() => component.Name2);
    }

    private static Component2 FindComponentByName(ModelDoc2 model, string name)
    {
        var configuration = model?.GetActiveConfiguration() as Configuration;
        var rootComponent = configuration?.GetRootComponent() as Component2;
        return FindComponentByName(GetComponentChildren(rootComponent), name);
    }

    private static Component2 FindComponentByName(object[] components, string name)
    {
        foreach (var component in EnumerateComponents(components))
        {
            var componentName = Safe(() => component.Name2) ?? "";
            if (string.Equals(componentName, name, StringComparison.OrdinalIgnoreCase))
                return component;

            var child = FindComponentByName(GetComponentChildren(component), name);
            if (child != null) return child;
        }

        return null;
    }

    private static void MarkDocDirty(ModelDoc2 model)
    {
        try
        {
            model?.SetSaveFlag();
        }
        catch (Exception ex)
        {
            LogIgnoredException("MarkDocDirty", ex);
        }
    }

    /// <summary>
    /// 清除文件的只读属性。
    /// </summary>
    private static void ClearReadOnlyAttribute(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) == 0) return;

            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            AddinLog.Write("Cleared read-only attribute: " + path);
        }
        catch (Exception ex)
        {
            LogIgnoredException("ClearReadOnlyAttribute", ex);
        }
    }
}
