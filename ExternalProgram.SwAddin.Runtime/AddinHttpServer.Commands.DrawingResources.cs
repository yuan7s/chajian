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
        var properties = ToObjectArray(Safe(() => sheet?.GetProperties()));
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

}
