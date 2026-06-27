using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var model = _swApp.OpenDoc6(path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors,
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

    private object RotateSelectedDrawingView()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw CommandFailure("drawing_required");

        var selMgr = model.SelectionManager as SelectionMgr;
        var swView = selMgr?.GetSelectedObject6(1, -1) as View;
        if (swView == null)
            throw CommandFailure("drawing_view_required");

        if (swView.Angle > 4.5)
            swView.Angle = 0;
        else
            swView.Angle += Math.PI / 2;

        model.Extension.SelectByID2(swView.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        MarkDocDirty(model);

        return new { viewName = swView.Name, angle = swView.Angle };
    }

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

    private object ReplaceDrawingStandard(Dictionary<string, object> args)
    {
        var path = GetDrawingResourcePath(args, ".sldstd", "绘图标准文件");
        var model = GetActiveDrawingModel();
        var layersDeleted = DeleteDrawingLayers(model);

        var standardLoaded = LoadDrawingStandard(model, path);

        model.ForceRebuild3(true);
        MarkDocDirty(model);
        return new { path, standardLoaded, layersDeleted, success = true };
    }

    private object ReplaceSheetFormat(Dictionary<string, object> args)
    {
        var path = GetDrawingResourcePath(args, ".slddrt", "图纸格式文件");
        var model = GetActiveDrawingModel();
        var drawing = GetDrawingDoc(model);
        if (drawing == null)
            throw CommandFailure("drawing_doc_unavailable");

        var layersDeleted = DeleteDrawingLayers(model);
        var changed = ApplySheetFormatToAllSheets(drawing, path);
        if (changed == 0)
            AddinLog.Write("ReplaceSheetFormat completed without confirmed sheet change: " + path);

        model.ForceRebuild3(true);
        var annotationLayers = AssignDrawingAnnotationsToLayers(model, drawing);
        MarkDocDirty(model);
        return new { path, sheetsChanged = changed, layersDeleted, annotationLayers, success = true, confirmed = changed > 0 };
    }

    private object ReplaceDrawingSettings(Dictionary<string, object> args)
    {
        var standardPath = GetDrawingResourcePath(args, "standardPath", ".sldstd", "绘图标准文件");
        var sheetFormatPath = GetDrawingResourcePath(args, "sheetFormatPath", ".slddrt", "图纸格式文件");
        var model = GetActiveDrawingModel();
        var drawing = GetDrawingDoc(model);
        if (drawing == null)
            throw CommandFailure("drawing_doc_unavailable");

        var layersDeleted = DeleteDrawingLayers(model);
        var standardLoaded = LoadDrawingStandard(model, standardPath);
        var sheetsChanged = ApplySheetFormatToAllSheets(drawing, sheetFormatPath);
        if (sheetsChanged == 0)
            AddinLog.Write("ReplaceDrawingSettings completed without confirmed sheet change: " + sheetFormatPath);

        model.ForceRebuild3(true);
        var annotationLayers = AssignDrawingAnnotationsToLayers(model, drawing);
        MarkDocDirty(model);
        return new
        {
            standardPath,
            sheetFormatPath,
            standardLoaded,
            sheetsChanged,
            layersDeleted,
            annotationLayers,
            success = true,
            standardConfirmed = standardLoaded,
            sheetFormatConfirmed = sheetsChanged > 0,
            confirmed = standardLoaded && sheetsChanged > 0
        };
    }

    private ModelDoc2 GetActiveDrawingModel()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            throw CommandFailure("drawing_required");
        return model;
    }

    private static DrawingDoc GetDrawingDoc(ModelDoc2 model)
    {
        if (model == null || Safe(model.GetType) != (int)swDocumentTypes_e.swDocDRAWING) return null;

        object swModel = model;
        return (DrawingDoc)swModel;
    }

    private static string GetDrawingResourcePath(Dictionary<string, object> args, string expectedExtension, string name)
    {
        return GetDrawingResourcePath(args, "path", expectedExtension, name);
    }

    private static string GetDrawingResourcePath(
        Dictionary<string, object> args,
        string key,
        string expectedExtension,
        string name)
    {
        var path = GetArgString(args, key);
        if (string.IsNullOrWhiteSpace(path))
            throw CommandFailure("argument_required", "name", name);

        if (!File.Exists(path))
            throw CommandFailure("file_not_found", "path", path);

        if (!string.Equals(Path.GetExtension(path), expectedExtension, StringComparison.OrdinalIgnoreCase))
            throw CommandFailure("unsupported_file_type", "path", path);

        return path;
    }

    private static bool LoadDrawingStandard(ModelDoc2 model, string path)
    {
        var extension = model.Extension;
        if (extension == null)
            throw CommandFailure("document_extension_unavailable");

        var loaded = extension.LoadDraftingStandard(path);
        if (!loaded)
        {
            AddinLog.Write("LoadDraftingStandard returned false: " + path);
            loaded = LoadDrawingStandardLateBound(model, path);
            if (!loaded && Safe(extension.DeleteDraftingStandard))
            {
                loaded = extension.LoadDraftingStandard(path);
                if (!loaded)
                    loaded = LoadDrawingStandardLateBound(model, path);
                AddinLog.Write("LoadDraftingStandard retry after DeleteDraftingStandard result=" + loaded + ": " + path);
            }
        }

        var selected = extension.SetUserPreferenceInteger(
            (int)swUserPreferenceIntegerValue_e.swDetailingDimensionStandard,
            0,
            (int)swDetailingStandard_e.swDetailingStandardUserDefined);

        if (!selected)
            AddinLog.Write("Set detailing standard to user-defined returned false: " + path);

        var currentStandard = Safe(() => extension.GetUserPreferenceInteger(
            (int)swUserPreferenceIntegerValue_e.swDetailingDimensionStandard,
            0));

        return loaded ||
               selected ||
               currentStandard == (int)swDetailingStandard_e.swDetailingStandardUserDefined;
    }

    private static bool LoadDrawingStandardLateBound(ModelDoc2 model, string path)
    {
        try
        {
            dynamic extension = model.Extension;
            bool loaded = extension.LoadDraftingStandard(path);
            AddinLog.Write("LoadDraftingStandard late-bound result=" + loaded + ": " + path);
            return loaded;
        }
        catch (Exception ex)
        {
            LogIgnoredException("LoadDrawingStandardLateBound", ex);
            return false;
        }
    }

    private static int ApplySheetFormatToAllSheets(DrawingDoc drawing, string path)
    {
        var sheetNames = ToStringArray(drawing.GetSheetNames());
        var originalSheetName = Safe(() => ((Sheet)drawing.GetCurrentSheet())?.GetName()) ?? "";
        var changed = 0;

        foreach (var sheetName in sheetNames)
        {
            if (!drawing.ActivateSheet(sheetName)) continue;
            var sheet = drawing.GetCurrentSheet() as Sheet;
            if (sheet == null) continue;

            if (ApplySheetFormat(drawing, sheet, path))
                changed++;
        }

        if (!string.IsNullOrWhiteSpace(originalSheetName))
            drawing.ActivateSheet(originalSheetName);

        return changed;
    }

    private static int DeleteDrawingLayers(ModelDoc2 model)
    {
        var manager = model.GetLayerManager() as LayerMgr;
        if (manager == null) return 0;

        var currentLayer = Safe(manager.GetCurrentLayer) ?? "";
        var layerNames = ToStringArray(manager.GetLayerList())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (layerNames.Length == 0) return 0;

        if (!string.IsNullOrWhiteSpace(currentLayer))
            Safe(() => manager.SetCurrentLayer(""));

        var deleted = 0;
        foreach (var layerName in layerNames)
        {
            if (Safe(() => manager.DeleteLayer(layerName)))
                deleted++;
        }

        return deleted;
    }

    private static DrawingAnnotationLayerResult AssignDrawingAnnotationsToLayers(ModelDoc2 model, DrawingDoc drawing)
    {
        var manager = model.GetLayerManager() as LayerMgr;
        if (manager == null || drawing == null)
            return new DrawingAnnotationLayerResult();

        var existingLayers = GetDrawingLayerNames(manager);
        var dimensionLayer = EnsureDrawingLayer(
            drawing,
            manager,
            existingLayers,
            "标注尺寸",
            ["标注尺寸", "尺寸标注", "尺寸", "DIMENSION", "DIMENSIONS", "DIM"]);
        var noteLayer = EnsureDrawingLayer(
            drawing,
            manager,
            existingLayers,
            "注释",
            ["注释", "文字", "说明", "NOTE", "NOTES", "ANNOTATION", "ANNOTATIONS", "TEXT"]);

        var result = new DrawingAnnotationLayerResult
        {
            dimensionLayer = dimensionLayer,
            noteLayer = noteLayer
        };

        var view = drawing.GetFirstView() as View;
        while (view != null)
        {
            var annotation = view.GetFirstAnnotation3();
            while (annotation != null)
            {
                var next = Safe(annotation.GetNext3);
                ApplyAnnotationLayer(annotation, dimensionLayer, noteLayer, result);
                annotation = next;
            }

            view = view.GetNextView() as View;
        }

        return result;
    }

    private static string[] GetDrawingLayerNames(LayerMgr manager)
    {
        return ToStringArray(manager.GetLayerList())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string EnsureDrawingLayer(
        DrawingDoc drawing,
        LayerMgr manager,
        string[] existingLayers,
        string fallbackName,
        string[] candidates)
    {
        var layerName = FindDrawingLayerName(existingLayers, candidates);
        if (!string.IsNullOrWhiteSpace(layerName)) return layerName;

        drawing.CreateLayer2(
            fallbackName,
            fallbackName,
            0,
            (int)swLineStyles_e.swLineCONTINUOUS,
            (int)swLineWeights_e.swLW_THIN,
            true,
            true);

        layerName = FindDrawingLayerName(GetDrawingLayerNames(manager), [fallbackName]);
        return string.IsNullOrWhiteSpace(layerName) ? fallbackName : layerName;
    }

    private static string FindDrawingLayerName(string[] layerNames, string[] candidates)
    {
        if (layerNames == null || candidates == null) return "";

        foreach (var candidate in candidates)
        {
            var exact = layerNames.FirstOrDefault(layer =>
                string.Equals(layer, candidate, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exact)) return exact;
        }

        foreach (var candidate in candidates)
        {
            var contains = layerNames.FirstOrDefault(layer =>
                layer.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(contains)) return contains;
        }

        return "";
    }

    private static void ApplyAnnotationLayer(
        Annotation annotation,
        string dimensionLayer,
        string noteLayer,
        DrawingAnnotationLayerResult result)
    {
        if (annotation == null) return;

        var annotationType = Safe(annotation.GetType);
        if (annotationType == (int)swAnnotationType_e.swDisplayDimension)
        {
            if (SetAnnotationLayer(annotation, dimensionLayer))
                result.dimensions++;
            else
                result.failed++;
        }
        else if (annotationType == (int)swAnnotationType_e.swNote)
        {
            if (SetAnnotationLayer(annotation, noteLayer))
                result.notes++;
            else
                result.failed++;
        }
    }

    private static bool SetAnnotationLayer(Annotation annotation, string layerName)
    {
        if (annotation == null || string.IsNullOrWhiteSpace(layerName)) return false;

        try
        {
            annotation.Layer = layerName;
            return string.Equals(annotation.Layer, layerName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogIgnoredException("SetAnnotationLayer", ex);
            return false;
        }
    }

    private static bool ApplySheetFormat(DrawingDoc drawing, Sheet sheet, string sheetFormatPath)
    {
        var sheetName = Safe(sheet.GetName) ?? "";
        var propertyViewName = Safe(() => sheet.CustomPropertyView) ?? "";
        var properties = ToObjectArray(Safe(sheet.GetProperties));
        var scale1 = GetSheetPropertyDouble(properties, 2, 1);
        var scale2 = GetSheetPropertyDouble(properties, 3, 1);
        var firstAngle = GetSheetPropertyBool(properties, 4, false);
        var width = GetSheetPropertyDouble(properties, 5, 0);
        var height = GetSheetPropertyDouble(properties, 6, 0);
        if (width <= 0 || height <= 0)
        {
            double sizeWidth = 0;
            double sizeHeight = 0;
            sheet.GetSize(ref sizeWidth, ref sizeHeight);
            if (width <= 0) width = sizeWidth;
            if (height <= 0) height = sizeHeight;
        }

        var currentTemplateName = Safe(sheet.GetTemplateName) ?? "";

        var ok = drawing.SetupSheet5(
            sheetName,
            (int)swDwgPaperSizes_e.swDwgPapersUserDefined,
            (int)swDwgTemplates_e.swDwgTemplateCustom,
            scale1,
            scale2,
            firstAngle,
            sheetFormatPath,
            width,
            height,
            propertyViewName,
            true);

        if (!ok && !string.IsNullOrWhiteSpace(currentTemplateName))
        {
            drawing.SetupSheet5(
                sheetName,
                (int)swDwgPaperSizes_e.swDwgPaperA3size,
                (int)swDwgTemplates_e.swDwgTemplateA3size,
                scale1,
                scale2,
                firstAngle,
                currentTemplateName,
                width,
                height,
                propertyViewName,
                true);

            ok = drawing.SetupSheet5(
                sheetName,
                (int)swDwgPaperSizes_e.swDwgPapersUserDefined,
                (int)swDwgTemplates_e.swDwgTemplateCustom,
                scale1,
                scale2,
                firstAngle,
                sheetFormatPath,
                width,
                height,
                propertyViewName,
                true);
        }

        var appliedSheet = drawing.GetCurrentSheet() as Sheet;
        var appliedTemplateName = Safe(() => appliedSheet?.GetTemplateName()) ?? Safe(sheet.GetTemplateName) ?? "";
        if (ok || IsSameReferencePath(appliedTemplateName, sheetFormatPath)) return true;

        AddinLog.Write("SetupSheet5 returned false: sheet=" + sheetName + ", format=" + sheetFormatPath);
        return false;
    }

    private static double GetSheetPropertyDouble(object[] values, int index, double fallback)
    {
        if (values == null || index < 0 || index >= values.Length || values[index] == null) return fallback;
        return double.TryParse(values[index].ToString(), out var value) ? value : fallback;
    }

    private static bool GetSheetPropertyBool(object[] values, int index, bool fallback)
    {
        if (values == null || index < 0 || index >= values.Length || values[index] == null) return fallback;
        if (values[index] is bool boolValue) return boolValue;
        if (bool.TryParse(values[index].ToString(), out var parsedBool)) return parsedBool;
        return double.TryParse(values[index].ToString(), out var parsedDouble)
            ? Math.Abs(parsedDouble) > double.Epsilon
            : fallback;
    }

    private sealed class DrawingAnnotationLayerResult
    {
        public string dimensionLayer { get; set; } = "";
        public string noteLayer { get; set; } = "";
        public int dimensions { get; set; }
        public int notes { get; set; }
        public int failed { get; set; }
    }

    private object MateReferencePlanes()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var assemblyDoc = GetAssemblyDoc(model);
        if (assemblyDoc == null) throw CommandFailure("assembly_doc_unavailable");

        var selectedComponents = GetSelectedComponentsForReferencePlaneMate(model);
        if (selectedComponents.Count == 0)
            throw CommandFailure("component_selection_required");

        var totalMates = 0;
        var failed = 0;
        var details = new List<object>();

        foreach (var component in selectedComponents)
        {
            var componentName = Safe(() => component.Name2) ?? "";
            var componentMates = 0;
            var failures = new List<string>();

            foreach (var planeNames in ReferencePlaneMateNameGroups)
                if (TryAddReferencePlaneMate(model, assemblyDoc, component, planeNames, out var errorMessage))
                {
                    totalMates++;
                    componentMates++;
                }
                else
                {
                    failed++;
                    failures.Add(errorMessage);
                }

            details.Add(new { component = componentName, mates = componentMates, failures });
        }

        if (totalMates == 0)
            throw CommandFailure("reference_plane_mate_failed");

        model.EditRebuild3();
        MarkDocDirty(model);
        return new { components = selectedComponents.Count, mates = totalMates, failed, details };
    }

    private static List<Component2> GetSelectedComponentsForReferencePlaneMate(ModelDoc2 model)
    {
        var selectionMgr = model.SelectionManager as SelectionMgr;
        if (selectionMgr == null) return new List<Component2>();

        var components = new List<Component2>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = Safe(() => selectionMgr.GetSelectedObjectCount2(-1));
        for (var i = 1; i <= count; i++)
        {
            var selectedIndex = i;
            var component = Safe(() => selectionMgr.GetSelectedObjectsComponent3(selectedIndex, -1)) ??
                            Safe(() => selectionMgr.GetSelectedObjectsComponent(selectedIndex) as Component2);
            if (component == null) continue;

            var key = GetComponentSelectionName(component);
            if (string.IsNullOrWhiteSpace(key)) key = Safe(() => component.Name2) ?? selectedIndex.ToString();
            if (seen.Add(key)) components.Add(component);
        }

        return components;
    }

    private static bool TryAddReferencePlaneMate(
        ModelDoc2 model,
        AssemblyDoc assemblyDoc,
        Component2 component,
        string[] planeNames,
        out string errorMessage)
    {
        errorMessage = "";

        var componentSelectionName = GetComponentSelectionName(component);
        if (string.IsNullOrWhiteSpace(componentSelectionName))
        {
            errorMessage = "component_selection_path_unavailable";
            return false;
        }

        foreach (var planeName in planeNames)
        {
            var componentPlaneName = planeName + "@" + componentSelectionName;
            try
            {
                model.ClearSelection2(true);
                var assemblyPlaneSelected = model.Extension.SelectByID2(planeName, "PLANE", 0, 0, 0, false, 1, null, 0);
                var componentPlaneSelected =
                    model.Extension.SelectByID2(componentPlaneName, "PLANE", 0, 0, 0, true, 1, null, 0);
                if (!assemblyPlaneSelected || !componentPlaneSelected)
                {
                    errorMessage = "reference_plane_select_failed:" + planeName;
                    continue;
                }

                var mate = assemblyDoc.AddMate4(
                    (int)swMateType_e.swMateCOINCIDENT,
                    (int)swMateAlign_e.swMateAlignALIGNED,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    false,
                    out var errorStatus);

                if (mate != null && errorStatus == 0) return true;

                errorMessage = "add_mate_failed:" + planeName + ":" + errorStatus;
            }
            catch (Exception ex)
            {
                errorMessage = "add_mate_exception:" + planeName;
                LogIgnoredException("TryAddReferencePlaneMate", ex);
            }
            finally
            {
                try
                {
                    model.ClearSelection2(true);
                }
                catch (Exception ex)
                {
                    LogIgnoredException("TryAddReferencePlaneMate.ClearSelection", ex);
                }
            }
        }

        return false;
    }

    private static string GetComponentSelectionName(Component2 component)
    {
        var selectionName = Safe(component.GetSelectByIDString);
        if (!string.IsNullOrWhiteSpace(selectionName)) return selectionName;

        return Safe(() => component.Name2);
    }

    private object SelectComponent(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var name = GetArgString(args, "name");
        if (string.IsNullOrWhiteSpace(name))
            throw CommandFailure("argument_required", "name", "name");

        var component = FindComponentByName(model, name);
        if (component == null)
            throw CommandFailure("component_not_found", "name", name);

        model.ClearSelection2(true);
        var ok = Safe(() => component.Select4(false, null, false));
        if (!ok)
            ok = Safe(() => component.Select2(false, 0));
        if (!ok)
            throw CommandFailure("component_select_failed", "name", name);

        return new
        {
            selected = true,
            name = Safe(() => component.Name2) ?? "",
            path = Safe(component.GetPathName) ?? ""
        };
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

    private object RunSwpMacro(Dictionary<string, object> args)
    {
        var path = GetArgString(args, "path");
        if (string.IsNullOrWhiteSpace(path))
            throw CommandFailure("macro_file_required");
        if (!File.Exists(path))
            throw CommandFailure("file_not_found", "path", path);
        if (!string.Equals(Path.GetExtension(path), ".swp", StringComparison.OrdinalIgnoreCase))
            throw CommandFailure("unsupported_macro_file", "path", path);

        var ok = _swApp.RunMacro2(path, "", "main", 0, out var error);
        if (!ok || error != 0)
            throw CommandFailure("macro_run_failed", "error", error);

        CheckAndBroadcastDocChange();
        return new { done = true, path };
    }

    // --- Hide Config Names (FeatureManager) ---

    private object HideConfigNames()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var featMgr = model.FeatureManager;
        if (featMgr == null) throw CommandFailure("feature_manager_unavailable");

        ConfigureFeatureManagerDisplay(featMgr);

        RecursiveHideConfigNames(_swApp, model);

        return new { done = true };
    }

    private static void RecursiveHideConfigNames(SldWorks.SldWorks swApp, ModelDoc2 asmDoc)
    {
        var configuration = asmDoc?.GetActiveConfiguration() as Configuration;
        var rootComponent = configuration?.GetRootComponent() as Component2;

        foreach (var child in EnumerateComponents(GetComponentChildren(rootComponent)))
            try
            {
                var childModel = child.GetModelDoc() as ModelDoc2;
                if (childModel == null) continue;

                var childType = childModel.GetType();
                if (childType == (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    var longstatus = 0;
                    var longWarnings = 0;
                    var childPath = Safe(() => child.GetPathName()) ?? "";
                    if (!string.IsNullOrWhiteSpace(childPath))
                    {
                        var fopen = swApp.OpenDoc6(childPath, (int)swDocumentTypes_e.swDocASSEMBLY,
                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref longstatus, ref longWarnings);
                        if (longstatus == 0 && fopen != null)
                        {
                            var swFeatMgr = fopen.FeatureManager;
                            if (swFeatMgr != null)
                                ConfigureFeatureManagerDisplay(swFeatMgr);
                        }
                    }

                    RecursiveHideConfigNames(swApp, childModel);
                }
            }
            catch (Exception ex)
            {
                LogIgnoredException("RecursiveHideConfigNames", ex);
            }
    }

    private static void ConfigureFeatureManagerDisplay(FeatureManager featureManager)
    {
        featureManager.HideComponentSingleConfigurationOrDisplayStateNames = false;
        featureManager.SetComponentIdentifiers(4, 0, 0);
        featureManager.SetComponentIdentifiers(2, 0, 0);
        featureManager.ShowComponentConfigurationNames = false;
        featureManager.ShowComponentConfigurationDescriptions = false;
        featureManager.ShowDisplayStateNames = false;
    }

    // --- Get Component Tree ---

    private object GetComponentTree()
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var components = new List<object>();
        var configuration = model.GetActiveConfiguration() as Configuration;
        var rootComponent = configuration?.GetRootComponent() as Component2;

        foreach (var child in EnumerateComponents(GetComponentChildren(rootComponent)))
        {
            var childModel = child.GetModelDoc() as ModelDoc2;
            var info = new
            {
                name = Safe(() => child.Name2),
                path = Safe(child.GetPathName),
                referencedPath = childModel != null ? Safe(childModel.GetPathName) : null,
                type = childModel != null ? Safe(childModel.GetType) : -1,
                isSuppressed = Safe(child.IsSuppressed),
                isEnvelope = Safe(child.IsEnvelope)
            };
            components.Add(info);
        }

        return new { components };
    }

    // --- Sort Components ---

    private object SortComponents(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var assemblyDoc = GetAssemblyDoc(model);
        if (assemblyDoc == null) throw CommandFailure("assembly_doc_unavailable");
        var options = SortOptions.FromArgs(args);

        var vFeats = GetFeatureArray(model);

        // Find start/end indices
        var startIdx = 0;
        var endIdx = vFeats.Length - 1;
        for (var i = 0; i < vFeats.Length; i++)
        {
            var feature = vFeats[i] as Feature;
            if (feature == null) continue;

            var t = Safe(feature.GetTypeName2) ?? "";
            if (t == "OriginProfileFeature") startIdx = i + 1;
            if (t == "MateGroup")
            {
                endIdx = i - 1;
                break;
            }
        }

        // Group features
        var folders = new List<Feature>();
        var folderComponents = new List<List<Feature>>();
        var topLevelFeats = new List<Feature>();
        var suppressedEnvFeats = new List<Feature>();
        List<Feature> currentFolderComps = null;

        for (var i = startIdx; i <= endIdx; i++)
        {
            var feat = vFeats[i] as Feature;
            if (feat == null) continue;

            var featType = Safe(feat.GetTypeName2) ?? "";
            if (featType == "FtrFolder")
            {
                var featureName = Safe(() => feat.Name) ?? "";
                if (featureName.IndexOf("___EndTag___", StringComparison.Ordinal) < 0)
                {
                    folders.Add(feat);
                    currentFolderComps = new List<Feature>();
                    folderComponents.Add(currentFolderComps);
                }
                else
                {
                    currentFolderComps = null;
                }
            }
            else if (featType == "Reference")
            {
                var isSupOrEnv = false;
                try
                {
                    if (feat.GetSpecificFeature2() is Component2 comp)
                        isSupOrEnv = comp.IsSuppressed() || comp.IsEnvelope();
                }
                catch (Exception ex)
                {
                    LogIgnoredException("SortComponents.ReferenceState", ex);
                }

                if (isSupOrEnv)
                {
                    if (currentFolderComps != null) currentFolderComps.Add(feat);
                    else suppressedEnvFeats.Add(feat);
                }
                else if (currentFolderComps != null)
                {
                    currentFolderComps.Add(feat);
                }
                else
                {
                    topLevelFeats.Add(feat);
                }
            }
        }

        // Collect and sort
        var sortedPartComps = CollectAndSort(topLevelFeats, false, options);
        var sortedAsmComps = CollectAndSort(topLevelFeats, true, options);
        var sortedSupPart = CollectAndSort(suppressedEnvFeats, false, options);
        var sortedSupAsm = CollectAndSort(suppressedEnvFeats, true, options);

        var allSorted =
            BuildSortedComponentOrder(sortedAsmComps, sortedPartComps, sortedSupAsm, sortedSupPart, options);

        if (allSorted.Count > 0)
        {
            var lastFolder = folders.Count > 0 ? folders[folders.Count - 1] : null;
            if (lastFolder != null)
                assemblyDoc.ReorderComponents(allSorted[0], lastFolder,
                    (int)swReorderComponentsWhere_e.swReorderComponents_After);
            for (var i = 1; i < allSorted.Count; i++)
                assemblyDoc.ReorderComponents(allSorted[i], allSorted[i - 1], 1);
        }

        var foldersSorted = 0;
        if (options.SortFolders)
            for (var i = 0; i < folders.Count; i++)
            {
                var folderFeats = i < folderComponents.Count ? folderComponents[i] : null;
                if (SortComponentsInFolder(folders[i], folderFeats, assemblyDoc, options))
                    foldersSorted++;
            }

        if (options.RecursiveSubAssemblies)
        {
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RecursiveSortSubAssemblies(topLevelFeats, processed, options);
            foreach (var fc in folderComponents)
                RecursiveSortSubAssemblies(fc, processed, options);
        }

        model.EditRebuild3();
        model.ClearSelection2(true);

        return new { done = true, topLevelSorted = allSorted.Count, foldersSorted };
    }

    private static List<Component2> CollectAndSort(List<Feature> feats, bool assemblies, SortOptions options)
    {
        var result = new List<Component2>();
        var targetType = assemblies ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        foreach (var feat in feats)
        {
            var comp = Safe(() => feat.GetSpecificFeature2() as Component2);
            if (comp == null) continue;
            try
            {
                if (comp.GetModelDoc() is ModelDoc2 childModel && childModel.GetType() == targetType)
                    result.Add(comp);
            }
            catch (Exception ex)
            {
                LogIgnoredException("CollectAndSort", ex);
            }
        }

        result.Sort((a, b) => CompareComponents(a, b, options));
        return result;
    }

    private static List<Component2> BuildSortedComponentOrder(
        List<Component2> asmComps,
        List<Component2> partComps,
        List<Component2> suppressedAsmComps,
        List<Component2> suppressedPartComps,
        SortOptions options)
    {
        var all = new List<Component2>();
        if (!options.SuppressedLast)
        {
            var mergedAsmComps = new List<Component2>();
            mergedAsmComps.AddRange(asmComps);
            mergedAsmComps.AddRange(suppressedAsmComps);
            mergedAsmComps.Sort((a, b) => CompareComponents(a, b, options));

            var mergedPartComps = new List<Component2>();
            mergedPartComps.AddRange(partComps);
            mergedPartComps.AddRange(suppressedPartComps);
            mergedPartComps.Sort((a, b) => CompareComponents(a, b, options));

            AppendTypeGroups(all, mergedAsmComps, mergedPartComps, options);
            return all;
        }

        AppendTypeGroups(all, asmComps, partComps, options);
        AppendTypeGroups(all, suppressedAsmComps, suppressedPartComps, options);

        return all;
    }

    private static void AppendTypeGroups(List<Component2> target, List<Component2> asmComps, List<Component2> partComps,
        SortOptions options)
    {
        if (options.AssemblyFirst)
        {
            target.AddRange(asmComps);
            target.AddRange(partComps);
        }
        else
        {
            target.AddRange(partComps);
            target.AddRange(asmComps);
        }
    }

    private static int CompareComponents(Component2 a, Component2 b, SortOptions options)
    {
        var left = GetComponentSortKey(a, options);
        var right = GetComponentSortKey(b, options);
        var result = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        return options.Descending ? -result : result;
    }

    private static string GetComponentSortKey(Component2 component, SortOptions options)
    {
        if (component == null) return "";

        if (string.Equals(options.NameSource, "FileName", StringComparison.OrdinalIgnoreCase))
        {
            var path = Safe(component.GetPathName) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
        }

        return Safe(() => component.Name2) ?? "";
    }

    private static bool SortComponentsInFolder(Feature folder, List<Feature> folderFeats, AssemblyDoc assemblyDoc,
        SortOptions options)
    {
        var partComps = new List<Component2>();
        var asmComps = new List<Component2>();
        var supPartComps = new List<Component2>();
        var supAsmComps = new List<Component2>();

        var feats = folderFeats is { Count: > 0 }
            ? folderFeats
            : GetFolderReferenceFeaturesFromSubFeatures(folder);

        foreach (var currentSubFeat in feats)
            if ((Safe(currentSubFeat.GetTypeName2) ?? "") == "Reference")
            {
                var comp = Safe(() => currentSubFeat.GetSpecificFeature2() as Component2);
                if (comp != null)
                {
                    var isSupEnv = Safe(comp.IsSuppressed) || Safe(comp.IsEnvelope);
                    var childModel = Safe(() => comp.GetModelDoc() as ModelDoc2);
                    if (childModel is { } && childModel.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        if (isSupEnv) supAsmComps.Add(comp);
                        else asmComps.Add(comp);
                    }
                    else if (childModel is { } && childModel.GetType() == (int)swDocumentTypes_e.swDocPART)
                    {
                        if (isSupEnv) supPartComps.Add(comp);
                        else partComps.Add(comp);
                    }
                }
            }

        asmComps.Sort((a, b) => CompareComponents(a, b, options));
        partComps.Sort((a, b) => CompareComponents(a, b, options));
        supAsmComps.Sort((a, b) => CompareComponents(a, b, options));
        supPartComps.Sort((a, b) => CompareComponents(a, b, options));

        var all = BuildSortedComponentOrder(asmComps, partComps, supAsmComps, supPartComps, options);

        if (all.Count > 1)
        {
            for (var i = 1; i < all.Count; i++)
                assemblyDoc.ReorderComponents(all[i], all[i - 1], 1);
            return true;
        }

        return false;
    }

    private static List<Feature> GetFolderReferenceFeaturesFromSubFeatures(Feature folder)
    {
        var result = new List<Feature>();
        var subFeat = Safe(() => folder.GetFirstSubFeature() as Feature);
        while (subFeat != null)
        {
            var currentSubFeat = subFeat;
            if ((Safe(currentSubFeat.GetTypeName2) ?? "") == "Reference")
                result.Add(currentSubFeat);

            subFeat = Safe(() => currentSubFeat.GetNextSubFeature() as Feature);
        }

        return result;
    }

    private static void RecursiveSortSubAssemblies(List<Feature> feats, HashSet<string> processed, SortOptions options)
    {
        foreach (var feat in feats)
        {
            if ((Safe(feat.GetTypeName2) ?? "") != "Reference") continue;
            var comp = Safe(() => feat.GetSpecificFeature2() as Component2);
            if (comp == null) continue;
            try
            {
                var model = comp.GetModelDoc() as ModelDoc2;
                if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY) continue;
                var path = model.GetPathName();
                if (string.IsNullOrWhiteSpace(path) || !processed.Add(path)) continue;
                SortSubAsmByFeatures(model, processed, options);
            }
            catch (Exception ex)
            {
                LogIgnoredException("RecursiveSortSubAssemblies", ex);
            }
        }
    }

    private static void SortSubAsmByFeatures(ModelDoc2 asmModel, HashSet<string> processed, SortOptions options)
    {
        try
        {
            var vFeats = GetFeatureArray(asmModel);
            var refFeats = new List<Feature>();
            foreach (var item in vFeats)
            {
                if (item is Feature feature && (Safe(feature.GetTypeName2) ?? "") == "Reference")
                    refFeats.Add(feature);
            }

            var asmComps = CollectAndSort(refFeats, true, options);
            var partComps = CollectAndSort(refFeats, false, options);
            var all = BuildSortedComponentOrder(asmComps, partComps, new List<Component2>(), new List<Component2>(),
                options);

            if (all.Count > 1)
            {
                var asmDoc = GetAssemblyDoc(asmModel);
                if (asmDoc != null)
                    for (var i = 1; i < all.Count; i++)
                        asmDoc.ReorderComponents(all[i], all[i - 1], 1);
            }

            RecursiveSortSubAssemblies(refFeats, processed, options);
        }
        catch (Exception ex)
        {
            LogIgnoredException("SortSubAsmByFeatures", ex);
        }
    }

    // --- Read Properties ---

    private object ReadProperties(Dictionary<string, object> args)
    {
        var target = ResolvePropertyTarget(args);
        if (target.FileProperties != null)
            return new
            {
                title = target.Title,
                path = target.Path,
                configuration = string.IsNullOrWhiteSpace(target.ConfigurationName)
                    ? "custom"
                    : target.ConfigurationName,
                source = target.Source,
                selectedComponent = target.SelectedComponent,
                properties = target.FileProperties
            };

        var manager = GetCustomPropertyManager(target.Model, target.ConfigurationName);
        if (manager == null) throw CommandFailure("property_manager_unavailable");

        var values = new Dictionary<string, string>();

        try
        {
            foreach (var name in GetPropertyNames(manager))
                try
                {
                    manager.Get5(name, false, out var rawValue, out var resolvedValue, out _);
                    values[name] = !string.IsNullOrWhiteSpace(resolvedValue) ? resolvedValue : rawValue ?? "";
                }
                catch (Exception ex)
                {
                    LogIgnoredException("ReadProperties.GetValue", ex);
                }
        }
        catch (Exception ex)
        {
            LogIgnoredException("ReadProperties", ex);
        }

        return new
        {
            title = target.Title,
            path = target.Path,
            configuration = string.IsNullOrWhiteSpace(target.ConfigurationName) ? "custom" : target.ConfigurationName,
            source = target.Source,
            selectedComponent = target.SelectedComponent,
            properties = values
        };
    }

    // --- Write Properties ---

    private object WriteProperties(Dictionary<string, object> args)
    {
        var target = ResolvePropertyTarget(args);
        if (target.FileProperties != null) throw CommandFailure("lightweight_component_write_unsupported");

        var propsArg = args.TryGetValue("properties", out var p) ? p : null;
        var properties = propsArg as Dictionary<string, object>;
        if (properties == null) throw CommandFailure("argument_required", "name", "properties");

        var manager = GetCustomPropertyManager(target.Model, target.ConfigurationName);
        if (manager == null) throw CommandFailure("property_manager_unavailable");

        var written = new List<string>();
        foreach (var kvp in properties)
        {
            manager.Add3(kvp.Key, 30, kvp.Value?.ToString() ?? "", 2);
            written.Add(kvp.Key);
        }

        MarkDocDirty(target.Model);

        return new
        {
            title = target.Title,
            configuration = target.ConfigurationName,
            source = target.Source,
            selectedComponent = target.SelectedComponent,
            written = written.ToArray()
        };
    }

    private PropertyTarget ResolvePropertyTarget(Dictionary<string, object> args)
    {
        var activeModel = GetActiveModel();
        var targetModel = activeModel;
        var selectedComponent = false;
        var title = Safe(activeModel.GetTitle) ?? "";
        var path = Safe(activeModel.GetPathName) ?? "";

        var comp = GetSelectedComponent(activeModel);
        if (comp != null)
        {
            var componentModel = Safe(() => comp.GetModelDoc() as ModelDoc2) ??
                                 Safe(() => comp.GetModelDoc2() as ModelDoc2);
            if (componentModel != null)
            {
                targetModel = componentModel;
                selectedComponent = true;
                title = Safe(() => comp.Name2) ?? Safe(componentModel.GetTitle) ?? title;
                path = Safe(componentModel.GetPathName) ?? path;
            }
            else
            {
                selectedComponent = true;
                title = Safe(() => comp.Name2) ?? title;
                path = Safe(comp.GetPathName) ?? "";
            }
        }

        var hasExplicitConfiguration = args.ContainsKey("configuration");
        var explicitSource = GetArgString(args, "source");
        var useCustomProperties =
            string.Equals(explicitSource, "custom", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(explicitSource, "file", StringComparison.OrdinalIgnoreCase) ||
            (!hasExplicitConfiguration && GetArgBool(args, "customProperties"));

        var configName = hasExplicitConfiguration ? GetArgString(args, "configuration") : "";
        if (!useCustomProperties && !hasExplicitConfiguration)
            configName = selectedComponent
                ? Safe(() => comp.ReferencedConfiguration) ?? ""
                : GetActiveConfigurationName(targetModel);

        Dictionary<string, string> fileProperties = null;
        if (selectedComponent && targetModel == activeModel && !string.IsNullOrWhiteSpace(path))
            fileProperties = ReadFileProperties(path, useCustomProperties ? "" : configName);

        return new PropertyTarget
        {
            Model = targetModel,
            Title = title,
            Path = path,
            ConfigurationName = configName ?? "",
            Source = useCustomProperties ? "custom" : "configuration",
            SelectedComponent = selectedComponent,
            FileProperties = fileProperties
        };
    }

    private static Component2 GetSelectedComponent(ModelDoc2 activeModel)
    {
        if (activeModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY) return null;

        try
        {
            var selMgr = activeModel.SelectionManager as SelectionMgr;
            if (selMgr == null) return null;

            var count = Safe(() => selMgr.GetSelectedObjectCount2(-1));
            for (var i = 1; i <= count; i++)
            {
                var selectedIndex = i;
                var comp = Safe(() => selMgr.GetSelectedObjectsComponent(selectedIndex) as Component2);
                if (comp != null) return comp;
            }
        }
        catch (Exception ex)
        {
            LogIgnoredException("GetSelectedComponent", ex);
        }

        return null;
    }

    private Dictionary<string, string> ReadFileProperties(string path, string configurationName)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw CommandFailure("component_file_property_path_invalid", "path", path);

        ISwDMDocument document = null;

        try
        {
            var application = CreateDocumentManagerApplication();
            var documentType = GetDocumentManagerType(path);
            if (documentType == SwDmDocumentType.swDmDocumentUnknown)
                throw CommandFailure("unsupported_solidworks_file_type", "path", path);

            document = application.GetDocument(path, documentType, true, out var error);
            if (document == null || error != SwDmDocumentOpenError.swDmDocumentOpenErrorNone)
                throw CommandFailure("document_manager_open_failed", "error", error.ToString(), "path", path);

            return string.IsNullOrWhiteSpace(configurationName)
                ? ReadDocumentManagerCustomProperties(document)
                : ReadDocumentManagerConfigurationProperties(document, configurationName);
        }
        finally
        {
            try
            {
                document?.CloseDoc();
            }
            catch (Exception ex)
            {
                LogIgnoredException("ReadFileProperties.CloseDoc", ex);
            }
        }
    }

    private static SwDMApplication CreateDocumentManagerApplication()
    {
        var factory = new SwDMClassFactoryClass();
        foreach (var license in LoadDocumentManagerLicenseCandidates())
            try
            {
                var application = factory.GetApplication(license);
                if (application != null) return application;
            }
            catch
            {
                // Try the next normalized license token.
            }

        throw CommandFailure("document_manager_unavailable");
    }

    private static IEnumerable<string> LoadDocumentManagerLicenseCandidates()
    {
        const string embeddedLicense =
            "SOLIDWORKS_2022:swdocmgr_general-11785-02051-00064-50177-06612-36864-49921-00003-21424-27712-02333-41243-17858-56070-14468-62467-35817-38164-41304-28396-16118-35795-11666-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-4,swdocmgr_previews-11785-02051-00064-50177-06612-36864-49921-00003-55360-53060-25343-42769-59069-52473-22473-52225-12074-04081-32909-30728-28589-33225-11507-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-3,swdocmgr_geometry-11785-02051-00064-50177-06612-36864-49921-00003-47348-23774-43673-03705-12160-02780-32459-34818-63205-17331-46285-63135-49863-07899-11787-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-2,swdocmgr_dimxpert-11785-02051-00064-50177-06612-36864-49921-00003-29996-08472-47529-56591-62456-46237-10843-31747-32567-27053-59927-17093-07167-05996-11461-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-7,swdocmgr_tessellation-11785-02051-00064-50177-06612-36864-49921-00003-10436-56910-32567-34976-20928-11850-62944-24577-40211-48738-24555-37037-32570-31276-11315-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-9,swdocmgr_xml-11785-02051-00064-50177-06612-36864-49921-00003-42016-05489-35279-24819-52264-14236-16400-48131-43581-35953-11290-28017-22653-22728-11882-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-8";

        if (string.IsNullOrWhiteSpace(embeddedLicense)) yield break;

        yield return embeddedLicense;
        foreach (var entry in embeddedLicense.Split([','], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0) continue;

            var parts = trimmed.Split(['-'], 2, StringSplitOptions.None);
            yield return parts.Length == 2 ? parts[1] : parts[0];
        }
    }

    private static SwDmDocumentType GetDocumentManagerType(string path)
    {
        switch ((Path.GetExtension(path) ?? "").ToUpperInvariant())
        {
            case ".SLDPRT": return SwDmDocumentType.swDmDocumentPart;
            case ".SLDASM": return SwDmDocumentType.swDmDocumentAssembly;
            case ".SLDDRW": return SwDmDocumentType.swDmDocumentDrawing;
            default: return SwDmDocumentType.swDmDocumentUnknown;
        }
    }

    private static Dictionary<string, string> ReadDocumentManagerCustomProperties(ISwDMDocument document)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in GetDocumentManagerPropertyNames(document.GetCustomPropertyNames))
            try
            {
                var value = document is ISwDMDocument5 document5
                    ? document5.GetCustomPropertyValues(name, out _, out _)
                    : document.GetCustomProperty(name, out _);
                values[name] = value ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogIgnoredException("ReadDocumentManagerCustomProperties", ex);
            }

        return values;
    }

    private static Dictionary<string, string> ReadDocumentManagerConfigurationProperties(
        ISwDMDocument document,
        string configurationName)
    {
        var configuration = ResolveDocumentManagerConfiguration(document, configurationName);
        if (configuration == null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in GetDocumentManagerPropertyNames(configuration.GetCustomPropertyNames))
            try
            {
                var value = configuration is ISwDMConfiguration5 configuration5
                    ? configuration5.GetCustomPropertyValues(name, out _, out _)
                    : configuration.GetCustomProperty(name, out _);
                values[name] = value ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogIgnoredException("ReadDocumentManagerConfigurationProperties", ex);
            }

        return values;
    }

    private static ISwDMConfiguration ResolveDocumentManagerConfiguration(ISwDMDocument document,
        string configurationName)
    {
        if (document?.ConfigurationManager == null) return null;

        foreach (var candidate in GetDocumentManagerConfigurationCandidates(document, configurationName))
        {
            if (document.ConfigurationManager.GetConfigurationByName(candidate) is ISwDMConfiguration configuration)
                return configuration;
        }

        return null;
    }

    private static IEnumerable<string> GetDocumentManagerConfigurationCandidates(ISwDMDocument document,
        string configurationName)
    {
        if (!string.IsNullOrWhiteSpace(configurationName)) yield return configurationName;

        foreach (var name in GetDocumentManagerPropertyNames(document.ConfigurationManager.GetConfigurationNames))
            if (!string.IsNullOrWhiteSpace(name) &&
                !string.Equals(name, configurationName, StringComparison.OrdinalIgnoreCase))
                yield return name;
    }

    private static IEnumerable<string> GetDocumentManagerPropertyNames(Func<object> getNames)
    {
        object names;
        try
        {
            names = getNames();
        }
        catch
        {
            yield break;
        }

        if (names is string[] stringNames)
        {
            foreach (var name in stringNames)
                if (!string.IsNullOrWhiteSpace(name))
                    yield return name;
        }
        else if (names is object[] objectNames)
        {
            foreach (var item in objectNames)
            {
                var name = item?.ToString();
                if (!string.IsNullOrWhiteSpace(name)) yield return name;
            }
        }
    }

    private static IEnumerable<string> GetPropertyNames(CustomPropertyManager manager)
    {
        if (manager == null) return Enumerable.Empty<string>();

        var names = manager.GetNames();
        if (names is string[] stringNames)
            return stringNames.Where(name => !string.IsNullOrWhiteSpace(name));

        if (names is object[] objectNames)
            return objectNames.Select(item => item?.ToString()).Where(name => !string.IsNullOrWhiteSpace(name));

        return Enumerable.Empty<string>();
    }

    private object DeleteCustomProperties()
    {
        var model = GetActiveModel();
        var stats = DeletePropertiesRecursive(model, false, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return new { done = true, documents = stats.Documents, deleted = stats.Deleted };
    }

    private object DeleteConfigurationProperties()
    {
        var model = GetActiveModel();
        var stats = DeletePropertiesRecursive(model, true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return new { done = true, documents = stats.Documents, deleted = stats.Deleted };
    }

    private static DeletePropertyStats DeletePropertiesRecursive(ModelDoc2 model, bool deleteConfigurationProperties,
        HashSet<string> processed)
    {
        var stats = new DeletePropertyStats();
        if (model == null) return stats;

        var key = GetModelProcessKey(model);
        if (!processed.Add(key)) return stats;

        stats.Documents++;
        stats.Deleted += deleteConfigurationProperties
            ? DeleteAllConfigurationProperties(model)
            : DeleteManagerProperties(GetCustomPropertyManager(model, ""));

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY) return stats;

        try
        {
            var configuration = model.GetActiveConfiguration() as Configuration;
            var rootComponent = configuration?.GetRootComponent() as Component2;
            var children = GetComponentChildren(rootComponent);

            foreach (var child in EnumerateComponents(children))
            {
                var childModel = Safe(() => child.GetModelDoc() as ModelDoc2);
                if (childModel == null) continue;

                var childStats = DeletePropertiesRecursive(childModel, deleteConfigurationProperties, processed);
                stats.Documents += childStats.Documents;
                stats.Deleted += childStats.Deleted;
            }
        }
        catch (Exception ex)
        {
            LogIgnoredException("DeletePropertiesRecursive", ex);
        }

        return stats;
    }

    private static int DeleteAllConfigurationProperties(ModelDoc2 model)
    {
        var deleted = 0;
        var configurationNames = GetConfigurationNames(model).ToArray();
        if (configurationNames.Length == 0)
        {
            var activeConfiguration = GetActiveConfigurationName(model);
            if (!string.IsNullOrWhiteSpace(activeConfiguration))
                configurationNames = [activeConfiguration];
        }

        foreach (var configurationName in configurationNames)
            deleted += DeleteManagerProperties(GetCustomPropertyManager(model, configurationName));

        return deleted;
    }

    private static int DeleteManagerProperties(CustomPropertyManager manager)
    {
        if (manager == null) return 0;

        var deleted = 0;
        foreach (var name in GetPropertyNames(manager).ToArray())
            try
            {
                manager.Delete2(name);
                deleted++;
            }
            catch (Exception ex)
            {
                LogIgnoredException("DeleteManagerProperties", ex);
            }

        return deleted;
    }

    private static IEnumerable<string> GetConfigurationNames(ModelDoc2 model)
    {
        var names = model.GetConfigurationNames();
        if (names is string[] stringNames)
            return stringNames.Where(name => !string.IsNullOrWhiteSpace(name));

        if (names is object[] objectNames)
            return objectNames.Select(item => item?.ToString()).Where(name => !string.IsNullOrWhiteSpace(name));

        return Enumerable.Empty<string>();
    }

    private static string GetModelProcessKey(ModelDoc2 model)
    {
        var path = Safe(model.GetPathName) ?? "";
        if (!string.IsNullOrWhiteSpace(path)) return path;

        var title = Safe(model.GetTitle) ?? "";
        return "unsaved:" + title + ":" + model.GetHashCode();
    }

    // --- Get Bounding Box ---

    private object GetBoundingBox(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var configName = GetArgString(args, "configuration");

        if (!string.IsNullOrWhiteSpace(configName))
            try
            {
                model.ShowConfiguration2(configName);
            }
            catch (Exception ex)
            {
                LogIgnoredException("GetBoundingBox.ShowConfiguration", ex);
            }

        var part = GetPartDoc(model);
        if (part == null) throw CommandFailure("part_required_for_bounding_box");

        var box = part.GetPartBox(false) as double[];
        if (box == null || box.Length < 6) throw CommandFailure("bounding_box_unavailable");

        return new
        {
            x1 = box[0], y1 = box[1], z1 = box[2],
            x2 = box[3], y2 = box[4], z2 = box[5],
            dx = box[3] - box[0],
            dy = box[4] - box[1],
            dz = box[5] - box[2]
        };
    }

    // --- Sync Coding Props ---

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

    // --- Rename Component ---

    private object GetRenameTargetInfo()
    {
        var target = ResolveRenameTarget(true);
        return CreateRenameTargetInfo(target);
    }

    private object CheckNameConflict(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(true);
        var destPath = BuildRenameDestinationPath(target, GetArgString(args, "newName"));
        var drawingPath = Path.ChangeExtension(destPath, ".SLDDRW");
        var existsFile = File.Exists(destPath);
        var existsDrawing = File.Exists(drawingPath);
        var existsOpen = TryGetOpenModelByPath(destPath) != null;

        return new
        {
            conflict = existsFile || existsOpen,
            existsFile,
            existsOpen,
            existsDrawing,
            path = destPath,
            drawingPath
        };
    }

    private object SaveAsNew(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(true);
        var newBaseName = NormalizeNewBaseName(GetArgString(args, "newName"));
        var destPath = BuildRenameDestinationPath(target, newBaseName);
        var copyDrawing = GetArgBool(args, "copyDrawing", true);
        EnsureRenameDestinationAvailable(destPath);

        SaveModelCopy(target.Model, destPath);
        var drawing = CopyRelatedDrawing(target.Path, destPath, copyDrawing);

        var newModel = OpenModelForRename(destPath, true);
        ApplyRenameProperties(newModel, target.Component, args, newBaseName);
        SaveModel(newModel);
        ActivateModel(newModel);

        return new
        {
            success = true,
            path = destPath,
            title = Safe(newModel.GetTitle) ?? Path.GetFileName(destPath),
            selectedComponent = target.SelectedComponent,
            drawing
        };
    }

    private object RenameComponent(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(true);
        var newBaseName = NormalizeNewBaseName(GetArgString(args, "newName"));
        var destPath = BuildRenameDestinationPath(target, newBaseName);
        EnsureRenameDestinationAvailable(destPath);

        var oldPath = target.Path;
        var copyDrawing = GetArgBool(args, "copyDrawing", true);
        var renameError = RenameDocumentInSolidWorks(target, newBaseName);
        if (renameError != (int)swRenameDocumentError_e.swRenameDocumentError_None)
            throw CommandFailure("solidworks_rename_failed", "error", renameError);

        SaveRenamedDocumentBeforeProperties(target);
        ApplyRenameProperties(target.Model, target.Component, args, newBaseName);
        SaveRenamePropertyChanges(target);

        var drawing = CopyRelatedDrawing(oldPath, destPath, copyDrawing);

        return new
        {
            success = true,
            oldName = target.BaseName,
            newName = newBaseName,
            oldPath,
            path = destPath,
            directRename = true,
            replaced = false,
            drawing
        };
    }

    private object SaveAsReplace(Dictionary<string, object> args)
    {
        var target = ResolveRenameTarget(true);
        if (!target.SelectedComponent || target.Component == null)
            throw CommandFailure("assembly_component_selection_required");

        var assemblyDoc = GetAssemblyDoc(target.ActiveModel);
        if (assemblyDoc == null)
            throw CommandFailure("assembly_doc_unavailable");

        var newBaseName = NormalizeNewBaseName(GetArgString(args, "newName"));
        var destPath = BuildRenameDestinationPath(target, newBaseName);
        var copyDrawing = GetArgBool(args, "copyDrawing", true);
        EnsureRenameDestinationAvailable(destPath);

        SaveModelCopy(target.Model, destPath);
        var drawing = CopyRelatedDrawing(target.Path, destPath, copyDrawing);

        var newModel = OpenModelForRename(destPath);
        ApplyRenameProperties(newModel, target.Component, args, newBaseName);
        SaveModel(newModel);
        var activateErrors = 0;
        Safe(() => _swApp.ActivateDoc3(Safe(() => target.ActiveModel.GetTitle()) ?? "", false, 0, ref activateErrors));

        target.ActiveModel.ClearSelection2(true);
        if (!Safe(() => target.Component.Select4(false, null, false)))
            throw CommandFailure("replace_component_reselect_failed");

        var configurationName = Safe(() => target.Component.ReferencedConfiguration) ?? "";
        var replaced = assemblyDoc.ReplaceComponents2(destPath, configurationName, false, 0, true);
        if (!replaced)
            throw CommandFailure("replace_component_failed");

        target.ActiveModel.EditRebuild3();
        MarkDocDirty(target.ActiveModel);

        return new
        {
            success = true,
            oldName = target.BaseName,
            newName = newBaseName,
            oldPath = target.Path,
            savedAsPath = destPath,
            replaced = true,
            drawing
        };
    }

    private int RenameDocumentInSolidWorks(RenameTarget target, string newBaseName)
    {
        if (target.SelectedComponent && target.Component != null)
        {
            target.ActiveModel.ClearSelection2(true);
            if (!Safe(() => target.Component.Select4(false, null, false)))
                throw CommandFailure("rename_component_select_failed");

            var assemblyExtension = target.ActiveModel.Extension;
            if (assemblyExtension == null)
                throw CommandFailure("assembly_extension_unavailable");

            return assemblyExtension.RenameDocument(newBaseName);
        }

        var extension = target.Model.Extension;
        if (extension == null)
            throw CommandFailure("document_extension_unavailable");

        return extension.RenameDocument(newBaseName);
    }

    private static void SaveRenamedDocumentBeforeProperties(RenameTarget target)
    {
        if (target.SelectedComponent)
        {
            target.ActiveModel.EditRebuild3();
            MarkDocDirty(target.Model);
            MarkDocDirty(target.ActiveModel);
            SaveModel(target.ActiveModel, "save_assembly_failed", true);
            return;
        }

        SaveModel(target.Model);
    }

    private static void SaveRenamePropertyChanges(RenameTarget target)
    {
        if (target.SelectedComponent)
        {
            target.ActiveModel.EditRebuild3();
            MarkDocDirty(target.Model);
            MarkDocDirty(target.ActiveModel);
            SaveModel(target.ActiveModel, "save_assembly_failed", true);
            return;
        }

        SaveModel(target.Model);
    }

    private static void ApplyRenameProperties(ModelDoc2 targetModel, Component2 component,
        Dictionary<string, object> args, string newName)
    {
        var propertyTarget = GetArgString(args, "propertyTarget", GetArgString(args, "source", "configuration"));
        if (GetArgBool(args, "customProperties")) propertyTarget = "custom";

        var configurationName = "";
        if (!string.Equals(propertyTarget, "custom", StringComparison.OrdinalIgnoreCase))
        {
            configurationName = component != null ? Safe(() => component.ReferencedConfiguration) ?? "" : "";
            if (string.IsNullOrWhiteSpace(configurationName))
                configurationName = GetActiveConfigurationName(targetModel);
        }

        var manager = GetCustomPropertyManager(targetModel, configurationName);
        if (manager == null) return;

        if (GetArgBool(args, "fileName"))
            manager.Add3("文件名称", 30, newName, 2);
        if (GetArgBool(args, "materialCode"))
            manager.Add3("物料编码", 30, newName, 2);
        if (GetArgBool(args, "partNumber"))
            manager.Add3("零件图号", 30, newName, 2);
        if (GetArgBool(args, "design"))
            manager.Add3("设计出图", 30, GetArgString(args, "designText"), 2);
        if (GetArgBool(args, "version"))
            manager.Add3("版本", 30, GetArgString(args, "versionText"), 2);

        if (args.TryGetValue("renameProperties", out var rawProperties))
            ApplyRenamePropertyList(manager, rawProperties);

        MarkDocDirty(targetModel);
    }

    private static void ApplyRenamePropertyList(CustomPropertyManager manager, object rawProperties)
    {
        var items = rawProperties as IEnumerable<object>;
        if (items == null) return;

        foreach (var item in items)
        {
            if (item is not Dictionary<string, object> dict) continue;
            var name = GetDictionaryString(dict, "name");
            if (string.IsNullOrWhiteSpace(name)) name = GetDictionaryString(dict, "Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var value = GetDictionaryString(dict, "value");
            if (string.IsNullOrWhiteSpace(value)) value = GetDictionaryString(dict, "Value");
            try
            {
                manager.Add3(name.Trim(), 30, value ?? "", 2);
            }
            catch (Exception ex)
            {
                LogIgnoredException("ApplyRenamePropertyList", ex);
            }
        }
    }

    private static string GetDictionaryString(Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var value) && value != null ? value.ToString() : "";
    }

    private RenameTarget ResolveRenameTarget(bool preferSelectedComponent)
    {
        var activeModel = GetActiveModel();
        var targetModel = activeModel;
        Component2 component = null;
        var selectedComponent = false;
        var componentName = "";

        if (preferSelectedComponent)
        {
            component = GetSelectedComponent(activeModel);
            var componentModel = component != null ? Safe(() => component.GetModelDoc() as ModelDoc2) : null;
            if (componentModel != null)
            {
                targetModel = componentModel;
                selectedComponent = true;
                componentName = Safe(() => component.Name2) ?? "";
            }
        }

        var path = Safe(() => targetModel.GetPathName()) ?? "";
        if (string.IsNullOrWhiteSpace(path))
            throw CommandFailure(selectedComponent
                ? "selected_component_must_be_saved_for_rename"
                : "document_must_be_saved_for_rename");

        var title = Safe(() => targetModel.GetTitle()) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = Path.GetFileNameWithoutExtension(title);

        return new RenameTarget
        {
            ActiveModel = activeModel,
            Model = targetModel,
            Component = component,
            SelectedComponent = selectedComponent,
            ComponentName = componentName,
            Title = title,
            Path = path,
            BaseName = baseName,
            Extension = Path.GetExtension(path).ToLowerInvariant(),
            DocumentType = Safe(() => targetModel.GetType())
        };
    }

    private static object CreateRenameTargetInfo(RenameTarget target)
    {
        return new
        {
            title = target.Title,
            path = target.Path,
            targetTitle = target.Title,
            targetPath = target.Path,
            baseName = target.BaseName,
            extension = target.Extension,
            type = target.DocumentType,
            selectedComponent = target.SelectedComponent,
            componentName = target.ComponentName
        };
    }

    private static string NormalizeNewBaseName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw CommandFailure("new_file_name_required");

        var trimmed = Path.GetFileNameWithoutExtension(newName.Trim());
        if (string.IsNullOrWhiteSpace(trimmed))
            throw CommandFailure("new_file_name_required");

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw CommandFailure("invalid_file_name");

        return trimmed;
    }

    private static string BuildRenameDestinationPath(RenameTarget target, string newName)
    {
        var baseName = NormalizeNewBaseName(newName);
        var directory = Path.GetDirectoryName(target.Path);
        if (string.IsNullOrWhiteSpace(directory))
            throw CommandFailure("target_directory_unavailable");

        var extension = string.IsNullOrWhiteSpace(target.Extension)
            ? GetDocumentExtension(target.DocumentType)
            : target.Extension;
        if (string.IsNullOrWhiteSpace(extension))
            throw CommandFailure("unsupported_document_type");

        return Path.Combine(directory, baseName + extension);
    }

    private static string GetDocumentExtension(int docType)
    {
        switch (docType)
        {
            case (int)swDocumentTypes_e.swDocPART: return ".sldprt";
            case (int)swDocumentTypes_e.swDocASSEMBLY: return ".sldasm";
            case (int)swDocumentTypes_e.swDocDRAWING: return ".slddrw";
            default: return "";
        }
    }

    private static void EnsureRenameDestinationAvailable(string destPath)
    {
        if (File.Exists(destPath))
            throw CommandFailure("target_file_exists", "path", destPath);
    }

    private static void SaveModelCopy(ModelDoc2 model, string destPath)
    {
        var extension = model.Extension;
        if (extension == null)
            throw CommandFailure("document_extension_unavailable");

        var errors = 0;
        var warnings = 0;
        var options = (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy;
        var ok = extension.SaveAs3(destPath, 0, options, null, null, ref errors, ref warnings);
        if (!ok || errors != 0)
            throw CommandFailure("save_new_file_failed", "errors", errors, "warnings", warnings, "path", destPath);
    }

    private ModelDoc2 OpenModelForRename(string path, bool activate = false)
    {
        var existing = TryGetOpenModelByPath(path);
        if (existing != null)
        {
            if (activate) ActivateModel(existing);
            return existing;
        }

        var docType = GetDocumentTypeFromPath(path);
        var errors = 0;
        var warnings = 0;
        var wasVisible = true;
        try
        {
            wasVisible = Safe(() => _swApp.GetDocumentVisible(docType));
            _swApp.DocumentVisible(activate, docType);

            var model = _swApp.OpenDoc6(path, docType, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors,
                ref warnings);
            if (model == null || errors != 0)
                throw CommandFailure("open_new_file_failed", "errors", errors, "warnings", warnings, "path", path);

            if (activate) ActivateModel(model);
            return model;
        }
        finally
        {
            if (!activate)
            {
                try
                {
                    _swApp.DocumentVisible(wasVisible, docType);
                }
                catch (Exception ex)
                {
                    LogIgnoredException("OpenModelForRename.DocumentVisible", ex);
                }
            }
        }
    }

    private static string GetActivationTitle(ModelDoc2 model)
    {
        var title = Safe(model.GetTitle) ?? "";
        if (!string.IsNullOrWhiteSpace(title)) return title;

        var path = Safe(model.GetPathName) ?? "";
        return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFileName(path);
    }

    private void ActivateModel(ModelDoc2 model)
    {
        var title = GetActivationTitle(model);
        if (string.IsNullOrWhiteSpace(title)) return;

        var errors = 0;
        Safe(() => _swApp.ActivateDoc3(title, false, 0, ref errors));
    }

    private static void SaveModel(
        ModelDoc2 model,
        string failureCode = "save_properties_failed",
        bool saveReferenced = false)
    {
        var errors = 0;
        var warnings = 0;
        var options = (int)swSaveAsOptions_e.swSaveAsOptions_Silent;
        if (saveReferenced)
            options |= (int)swSaveAsOptions_e.swSaveAsOptions_SaveReferenced;

        var ok = model.Save3(options, ref errors, ref warnings);
        if (!ok || errors != 0)
            throw CommandFailure(failureCode, "errors", errors, "warnings", warnings);
    }

    private DrawingCopyResult CopyRelatedDrawing(string sourceModelPath, string destModelPath, bool enabled)
    {
        if (!enabled)
            return DrawingCopyResult.Skipped("copy drawing disabled");
        if (string.IsNullOrWhiteSpace(sourceModelPath) || string.IsNullOrWhiteSpace(destModelPath))
            return DrawingCopyResult.Skipped("model path is empty");

        var sourceDrawing = Path.ChangeExtension(sourceModelPath, ".SLDDRW");
        if (!File.Exists(sourceDrawing))
            return DrawingCopyResult.Skipped("source drawing does not exist");

        var destDrawing = Path.ChangeExtension(destModelPath, ".SLDDRW");
        if (File.Exists(destDrawing))
            return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "target_drawing_exists");

        try
        {
            var sourceReference = EnsureSourceDrawingReferencesCurrentModel(sourceDrawing, sourceModelPath);
            if (sourceReference == null)
                return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "source_drawing_reference_mismatch");

            File.Copy(sourceDrawing, destDrawing);
            AddinLog.Write("CopyRelatedDrawing copied: " + sourceDrawing + " -> " + destDrawing);

            var replaced = _swApp.ReplaceReferencedDocument(destDrawing, sourceReference, destModelPath);
            AddinLog.Write("CopyRelatedDrawing ReplaceReferencedDocument result=" + replaced + ", drawing=" +
                           destDrawing + ", old=" + sourceReference + ", new=" + destModelPath);
            if (replaced)
                return DrawingCopyResult.Copied(sourceDrawing, destDrawing, sourceReference, sourceReference);

            try
            {
                File.Delete(destDrawing);
            }
            catch (Exception ex)
            {
                LogIgnoredException("CopyRelatedDrawing.DeleteFailedDrawing", ex);
            }

            return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "drawing_reference_replace_failed");
        }
        catch (Exception ex)
        {
            AddinLog.Write("CopyRelatedDrawing failed: " + ex);
            try
            {
                if (File.Exists(destDrawing)) File.Delete(destDrawing);
            }
            catch (Exception deleteEx)
            {
                LogIgnoredException("CopyRelatedDrawing.DeleteFailedDrawing", deleteEx);
            }

            return DrawingCopyResult.Failed(sourceDrawing, destDrawing, "drawing_copy_failed");
        }
    }

    private string EnsureSourceDrawingReferencesCurrentModel(string sourceDrawing, string sourceModelPath)
    {
        var sourceFullPath = Path.GetFullPath(sourceModelPath);
        var dependencies = GetDrawingModelDependencies(sourceDrawing).ToArray();
        var currentReference = dependencies.FirstOrDefault(item =>
            IsSameReferencePath(item, sourceFullPath) || IsSameFileIfExists(item, sourceFullPath));
        if (!string.IsNullOrWhiteSpace(currentReference))
        {
            AddinLog.Write("CopyRelatedDrawing source reference already ok: drawing=" + sourceDrawing + ", model=" +
                           currentReference);
            return currentReference;
        }

        foreach (var oldReference in GetSourceDrawingRepairCandidates(dependencies, sourceModelPath))
        {
            var repaired = _swApp.ReplaceReferencedDocument(sourceDrawing, oldReference, sourceModelPath);
            AddinLog.Write("CopyRelatedDrawing repair source result=" + repaired + ", drawing=" + sourceDrawing +
                           ", old=" + oldReference + ", new=" + sourceModelPath);
            if (repaired) return sourceModelPath;
        }

        return null;
    }

    private IEnumerable<string> GetDrawingModelDependencies(string drawingPath)
    {
        foreach (var dependency in ToStringArray(_swApp.GetDocumentDependencies2(drawingPath, false, true, false)))
        {
            if (string.IsNullOrWhiteSpace(dependency)) continue;
            if (!Path.IsPathRooted(dependency)) continue;
            if (!IsSolidWorksModelPath(dependency)) continue;
            yield return dependency;
        }
    }

    private IEnumerable<string> GetSourceDrawingRepairCandidates(IEnumerable<string> dependencies,
        string sourceModelPath)
    {
        var candidates = new List<string>();
        var dependencyList = dependencies.ToArray();
        var sourceExt = Path.GetExtension(sourceModelPath);
        var sourceName = Path.GetFileName(sourceModelPath);
        var sourceBaseName = Path.GetFileNameWithoutExtension(sourceModelPath);
        var sameExtensionDependencies = dependencyList
            .Where(item => string.Equals(Path.GetExtension(item), sourceExt, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var dependency in sameExtensionDependencies)
        {
            var depName = Path.GetFileName(dependency);
            var depBaseName = Path.GetFileNameWithoutExtension(dependency);
            if (string.Equals(depName, sourceName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(depBaseName, sourceBaseName, StringComparison.OrdinalIgnoreCase) ||
                IsSameFileIfExists(dependency, sourceModelPath) ||
                sameExtensionDependencies.Length == 1)
                AddReferenceCandidate(candidates, dependency);
        }

        foreach (var candidate in candidates)
            yield return candidate;
    }

    private static void AddReferenceCandidate(List<string> candidates, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (candidates.Any(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase))) return;
        candidates.Add(path);
    }

    private static bool IsSolidWorksModelPath(string path)
    {
        var ext = Path.GetExtension(path);
        return string.Equals(ext, ".SLDPRT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ext, ".SLDASM", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameFileIfExists(string left, string right)
    {
        try
        {
            if (!File.Exists(left) || !File.Exists(right)) return false;
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSameReferencePath(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    // --- Coding Cleanup ---

    private object CodingCleanup(Dictionary<string, object> args)
    {
        var model = GetActiveModel();
        var nameFilter = GetArgString(args, "nameFilter");
        var processAsm = GetArgBool(args, "processAsm", true);
        var processPart = GetArgBool(args, "processPart", true);
        var excludeVirtual = GetArgBool(args, "excludeVirtual", true);
        var excludeStandard = GetArgBool(args, "excludeStandard", true);
        var excludePurchased = GetArgBool(args, "excludePurchased", true);

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            throw CommandFailure("assembly_required");

        var configuration = model.GetActiveConfiguration() as Configuration;
        if (configuration == null)
            throw CommandFailure("assembly_configuration_unavailable");

        var rootComponent = configuration.GetRootComponent() as Component2;
        if (rootComponent == null)
            throw CommandFailure("assembly_root_component_unavailable");

        var comps = GetComponentChildren(rootComponent);

        var results = new List<object>();
        ProcessCodingCleanup(comps, nameFilter, processAsm, processPart,
            excludeVirtual, excludeStandard, excludePurchased, results);

        model.EditRebuild3();
        return new { processed = results.Count, results };
    }

    private static void ProcessCodingCleanup(object[] comps, string nameFilter, bool processAsm, bool processPart,
        bool excludeVirtual, bool excludeStandard, bool excludePurchased, List<object> results)
    {
        foreach (var child in EnumerateComponents(comps))
            try
            {
                var childModel = child.GetModelDoc() as ModelDoc2;
                if (childModel == null) continue;

                var childType = childModel.GetType();
                var childName = Safe(() => child.Name2);
                var isAssembly = childType == (int)swDocumentTypes_e.swDocASSEMBLY;
                var isPart = childType == (int)swDocumentTypes_e.swDocPART;

                var configName = Safe(() => child.ReferencedConfiguration);
                if (string.IsNullOrWhiteSpace(configName)) configName = "";

                var isVirtual = Safe(() => child.IsVirtual);
                var excludedByVirtual = excludeVirtual && isVirtual;
                var excludedByFlag =
                    IsExcludedCodingCleanupComponent(childModel, configName, excludeStandard, excludePurchased);
                var matchesNameFilter = string.IsNullOrWhiteSpace(nameFilter) ||
                                        string.IsNullOrWhiteSpace(childName) ||
                                        childName.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                var shouldProcessCurrent = matchesNameFilter &&
                                           !excludedByVirtual &&
                                           !excludedByFlag &&
                                           ((isPart && processPart) || (isAssembly && processAsm));

                if (shouldProcessCurrent)
                {
                    var title = Safe(childModel.GetTitle) ?? "";
                    var dotIdx = title.IndexOf(".", StringComparison.Ordinal);
                    if (dotIdx > 0) title = title.Substring(0, dotIdx);

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        var materialCode = Safe(() => childModel.GetCustomInfoValue(configName, "物料编码"));
                        var partNumber = Safe(() => childModel.GetCustomInfoValue(configName, "零件图号"));

                        if (title != materialCode || title != partNumber)
                        {
                            var cusPropMgr = GetCustomPropertyManager(childModel, configName);
                            if (cusPropMgr != null)
                            {
                                cusPropMgr.Add3("物料编码", 30, title, 2);
                                cusPropMgr.Add3("零件图号", 30, title, 2);
                                cusPropMgr.Add3("文件名称", 30, title, 2);
                                MarkDocDirty(childModel);
                                results.Add(
                                    new { name = childName, title, materialCode, partNumber, action = "synced" });
                            }
                        }
                    }
                }

                if (isAssembly && !excludedByVirtual && !excludedByFlag)
                {
                    var subComps = GetComponentChildrenForConfiguration(childModel, configName);
                    ProcessCodingCleanup(subComps, nameFilter, processAsm, processPart,
                        excludeVirtual, excludeStandard, excludePurchased, results);
                }
            }
            catch (Exception ex)
            {
                LogIgnoredException("ProcessCodingCleanup", ex);
            }
    }

    private static object[] GetComponentChildrenForConfiguration(ModelDoc2 model, string configName)
    {
        if (model == null) return Array.Empty<object>();

        if (!string.IsNullOrWhiteSpace(configName))
        {
            var activeConfigName = GetActiveConfigurationName(model);
            try
            {
                if (!string.Equals(activeConfigName, configName, StringComparison.OrdinalIgnoreCase))
                    Safe(() => model.ShowConfiguration2(configName));

                return GetActiveConfigurationChildren(model);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(activeConfigName) &&
                    !string.Equals(activeConfigName, configName, StringComparison.OrdinalIgnoreCase))
                    Safe(() => model.ShowConfiguration2(activeConfigName));
            }
        }

        return GetActiveConfigurationChildren(model);
    }

    private static object[] GetActiveConfigurationChildren(ModelDoc2 model)
    {
        var configuration = Safe(() => model.GetActiveConfiguration() as Configuration);
        var rootComponent = Safe(() => configuration?.GetRootComponent() as Component2);
        return GetComponentChildren(rootComponent);
    }

    private static bool IsExcludedCodingCleanupComponent(ModelDoc2 model, string configName, bool excludeStandard,
        bool excludePurchased)
    {
        if (!excludeStandard && !excludePurchased) return false;

        return (excludeStandard && HasPositiveCodingCleanupFlag(model, configName, StandardComponentFlagProperties)) ||
               (excludePurchased && HasPositiveCodingCleanupFlag(model, configName, PurchasedComponentFlagProperties));
    }

    private static string GetCodingCleanupPropertyValue(ModelDoc2 model, string configName, string propertyName)
    {
        var value = Safe(() => model.GetCustomInfoValue(configName, propertyName));
        if (!string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(configName)) return value;

        return Safe(() => model.GetCustomInfoValue("", propertyName));
    }

    private static bool HasPositiveCodingCleanupFlag(ModelDoc2 model, string configName,
        IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetCodingCleanupPropertyValue(model, configName, propertyName);
            if (TryConvertCodingCleanupFlag(value, out var enabled) && enabled) return true;
        }

        return false;
    }

    private static bool TryConvertCodingCleanupFlag(string value, out bool enabled)
    {
        enabled = false;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (bool.TryParse(text, out enabled)) return true;

        switch (text.ToUpperInvariant())
        {
            case "1":
            case "是":
            case "真":
            case "YES":
            case "Y":
                enabled = true;
                return true;
            case "0":
            case "否":
            case "假":
            case "NO":
            case "N":
                enabled = false;
                return true;
            default:
                return false;
        }
    }

    private sealed class SortOptions
    {
        public bool AssemblyFirst { get; private set; } = true;
        public bool SuppressedLast { get; private set; } = true;
        public bool SortFolders { get; private set; } = true;
        public bool RecursiveSubAssemblies { get; private set; } = true;
        public bool Descending { get; private set; }
        public string NameSource { get; private set; } = "ComponentName";

        public static SortOptions FromArgs(Dictionary<string, object> args)
        {
            var options = new SortOptions();
            if (args == null) return options;

            options.AssemblyFirst = GetBool(args, "assemblyFirst", true);
            options.SuppressedLast = GetBool(args, "suppressedLast", true);
            options.SortFolders = GetBool(args, "sortFolders", true);
            options.RecursiveSubAssemblies = GetBool(args, "recursiveSubAssemblies", true);
            options.Descending = GetBool(args, "descending", false);
            options.NameSource = GetString(args, "nameSource", "ComponentName");
            return options;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool fallback)
        {
            if (!args.TryGetValue(key, out var value) || value == null) return fallback;
            if (value is bool boolValue) return boolValue;
            return bool.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
        }

        private static string GetString(Dictionary<string, object> args, string key, string fallback)
        {
            if (!args.TryGetValue(key, out var value) || value == null) return fallback;
            var text = value.ToString();
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
    }

    private sealed class PropertyTarget
    {
        public ModelDoc2 Model { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string ConfigurationName { get; set; }
        public string Source { get; set; }
        public bool SelectedComponent { get; set; }
        public Dictionary<string, string> FileProperties { get; set; }
    }

    private sealed class DeletePropertyStats
    {
        public int Documents { get; set; }
        public int Deleted { get; set; }
    }

    // ReSharper disable InconsistentNaming
    // ReSharper disable UnusedAutoPropertyAccessor.Local
    private sealed class DrawingCopyResult
    {
        public bool attempted { get; set; }
        public bool success { get; set; }
        public bool skipped { get; set; }
        public string sourcePath { get; set; }
        public string path { get; set; }
        public string replacedReference { get; set; }
        public string sourceReference { get; set; }
        public string error { get; set; }

        public static DrawingCopyResult Skipped(string reason)
        {
            return new DrawingCopyResult { attempted = false, success = false, skipped = true, error = reason };
        }

        public static DrawingCopyResult Copied(string sourcePath, string path, string replacedReference = null,
            string sourceReference = null)
        {
            return new DrawingCopyResult
            {
                attempted = true, success = true, skipped = false, sourcePath = sourcePath, path = path,
                replacedReference = replacedReference, sourceReference = sourceReference
            };
        }

        public static DrawingCopyResult Failed(string sourcePath, string path, string error)
        {
            return new DrawingCopyResult
            {
                attempted = true, success = false, skipped = false, sourcePath = sourcePath, path = path, error = error
            };
        }
    }
    // ReSharper restore UnusedAutoPropertyAccessor.Local
    // ReSharper restore InconsistentNaming

    private sealed class RenameTarget
    {
        public ModelDoc2 ActiveModel { get; set; }
        public ModelDoc2 Model { get; set; }
        public Component2 Component { get; set; }
        public bool SelectedComponent { get; set; }
        public string ComponentName { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string BaseName { get; set; }
        public string Extension { get; set; }
        public int DocumentType { get; set; }
    }
}
