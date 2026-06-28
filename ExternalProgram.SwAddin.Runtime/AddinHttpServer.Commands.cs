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
    private static readonly string[] StandardComponentFlagProperties = ["标准件"];
    private static readonly string[] PurchasedComponentFlagProperties = ["外购件"];

    private static readonly string[][] ReferencePlaneMateNameGroups =
    [
        ["前视基准面", "Front Plane"],
        ["上视基准面", "Top Plane"],
        ["右视基准面", "Right Plane"]
    ];

    private const int DrawingHoleCalloutAnnotationOptions = 1048576;
    private const int DrawingModelAnnotationOptions =
        8 | 16 | 32768 | 131072 | DrawingHoleCalloutAnnotationOptions | 16777216;

    private const double DrawingSheetWidth = 0.42;
    private const double DrawingSheetHeight = 0.297;
    private const double DrawingViewLayoutGap = 0.032;
    private const double DrawingViewLayoutClearance = 0.012;
    private const double DrawingIsoViewLayoutClearance = 0.004;
    private const double DrawingViewLayoutMarginX = 0.04;
    private const double DrawingViewLayoutTopMargin = 0.035;
    private const double DrawingViewLayoutBottomMargin = 0.065;
    private const double DrawingIsoViewLayoutBottomMargin = 0.23;
    private const double DrawingAnnotationMargin = 0.008;
    private const double DrawingAnnotationSpacing = 0.007;
    private static readonly string[] DrawingDefaultTemplateFileNames =
    [
        "工程图.drwdot",
        "Drawing.drwdot"
    ];
    private static readonly DrawingScaleRatio[] DrawingStandardScales =
    [
        new DrawingScaleRatio(20, 1),
        new DrawingScaleRatio(15, 1),
        new DrawingScaleRatio(12, 1),
        new DrawingScaleRatio(10, 1),
        new DrawingScaleRatio(8, 1),
        new DrawingScaleRatio(7, 1),
        new DrawingScaleRatio(6, 1),
        new DrawingScaleRatio(5, 1),
        new DrawingScaleRatio(4, 1),
        new DrawingScaleRatio(3, 1),
        new DrawingScaleRatio(2, 1),
        new DrawingScaleRatio(1, 1),
        new DrawingScaleRatio(1, 2),
        new DrawingScaleRatio(1, 3),
        new DrawingScaleRatio(1, 4),
        new DrawingScaleRatio(1, 5),
        new DrawingScaleRatio(1, 8),
        new DrawingScaleRatio(1, 10),
        new DrawingScaleRatio(1, 15),
        new DrawingScaleRatio(1, 20),
        new DrawingScaleRatio(1, 25),
        new DrawingScaleRatio(1, 50),
        new DrawingScaleRatio(1, 75),
        new DrawingScaleRatio(1, 100),
        new DrawingScaleRatio(1, 150),
        new DrawingScaleRatio(1, 200),
        new DrawingScaleRatio(1, 500)
    ];

    private static readonly DrawingViewSpec[] StandardDrawingViewSpecs =
    [
        new DrawingViewSpec("前视图", 0.30, 0.47, "*Front", "*前视", "*前视图"),
        new DrawingViewSpec("俯视图", 0.30, 0.75, "*Top", "*上视", "*俯视", "*上视图", "*俯视图"),
        new DrawingViewSpec("右视图", 0.58, 0.47, "*Right", "*右视", "*右视图")
    ];

    private static readonly DrawingViewSpec IsoDrawingViewSpec =
        new DrawingViewSpec("等轴测", 0.64, 0.41, "*Isometric", "*等轴测", "*等轴侧", "*等轴测图");

    private static readonly DrawingViewSlotSpec[] StandardDrawingViewSlots =
    [
        new DrawingViewSlotSpec("主视图", 0.3324447719576762, 0.7117505740745859),
        new DrawingViewSlotSpec("右视图", 0.6015328888757833, 0.7117505740745859),
        new DrawingViewSlotSpec("俯视图", 0.3324447719576762, 0.4073276539248047)
    ];

    private static readonly DrawingViewSlotSpec IsoDrawingViewSlot =
        new DrawingViewSlotSpec("等轴测", 0.64, 0.4073276539248047);

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
        switch ((request.Command ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "PING":
                result = new { message = "pong", time = DateTime.Now };
                break;

            case "ACTIVE-DOCUMENT":
                result = GetActiveDocumentInfo();
                break;

            case "SAVE-DWG":
                result = SaveActiveDrawingAs(".dwg", "DWG");
                break;

            case "SAVE-PDF":
                result = SaveActiveDrawingAs(".pdf", "PDF");
                break;

            case "OPEN-FILE-LOCATION":
                result = GetActiveOrSelectedModelPath();
                break;

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

            case "DRAWING-AUTOMATION-RUN":
                result = RunDrawingAutomation(args);
                break;

            case "DRAWING-IMPORT-MODEL-ITEMS":
                result = ImportDrawingModelItemsCommand(args);
                break;

            case "DRAWING-IMPORT-HOLE-CALLOUTS":
                result = ImportDrawingHoleCalloutsCommand(args);
                break;

            case "DRAWING-AUTOMATION-CHECK":
                result = CheckDrawingAutomation(args);
                break;

            case "MATE-REFERENCE-PLANES":
                result = MateReferencePlanes();
                break;

            case "DELETE-ERROR-MATES":
                result = DeleteErrorMates();
                break;

            case "RUN-SWP-MACRO":
                result = RunSwpMacro(args);
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

    private ModelDoc2 GetActiveModel()
    {
        var model = _swApp.ActiveDoc as ModelDoc2;
        if (model == null) throw CommandFailure("active_document_required");
        return model;
    }

    private static CommandFailureException CommandFailure(string code, params object[] details)
    {
        return CommandFailureException.Create(code, details);
    }

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

    // --- Open File Location ---

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

    // --- Rotate Drawing View ---

}
